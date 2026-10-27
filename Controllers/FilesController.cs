using System.IO.Compression;
using LanCloud.Data;
using LanCloud.Models;
using LanCloud.Services;
using LanCloud.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanCloud.Controllers;

public class FilesController : AppController
{
    private readonly AppDbContext _db;
    private readonly StorageService _storage;

    public FilesController(AppDbContext db, StorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    // ───────────── لیست فایل‌های من ─────────────

    public async Task<IActionResult> Index(int? folder, string? q, string? sort)
    {
        var uid = UserId;

        StorageItem? current = null;
        var crumbs = new List<StorageItem>();
        if (folder != null)
        {
            current = await _db.Items.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == folder && i.OwnerId == uid && i.IsFolder);
            if (current == null) return NotFound();
            crumbs = await _storage.GetChainAsync(current);
        }

        q = q?.Trim();
        var query = _db.Items.AsNoTracking().Where(i => i.OwnerId == uid);
        query = string.IsNullOrEmpty(q)
            ? query.Where(i => i.ParentId == folder)
            : query.Where(i => i.Name.Contains(q)); // جستجو در تمام فایل‌های کاربر

        var list = await query.ToListAsync();

        sort = sort is "date" or "size" ? sort : "name";
        var ordered = list.OrderByDescending(i => i.IsFolder);
        var sorted = sort switch
        {
            "date" => ordered.ThenByDescending(i => i.UpdatedAt),
            "size" => ordered.ThenByDescending(i => i.Size),
            _ => ordered.ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
        };

        var ids = list.Select(i => i.Id).ToList();
        var shareCounts = await _db.Shares.AsNoTracking()
            .Where(s => ids.Contains(s.ItemId))
            .GroupBy(s => s.ItemId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count);

        var (used, quota) = await _storage.GetUsageAsync(uid);

        var vm = new FilesIndexVm
        {
            Folder = current,
            Crumbs = crumbs,
            Query = q,
            Sort = sort,
            Used = used,
            Quota = quota,
            List = new ItemListVm
            {
                Owned = true,
                EmptyTitle = string.IsNullOrEmpty(q) ? "این پوشه خالی است" : "چیزی پیدا نشد",
                EmptyText = string.IsNullOrEmpty(q)
                    ? "فایل‌هایت را اینجا بکش و رها کن یا دکمه «آپلود» را بزن."
                    : "عبارت دیگری را جستجو کن.",
                Items = sorted.Select(i => new ItemRow
                {
                    Item = i,
                    ShareCount = shareCounts.TryGetValue(i.Id, out var c) ? c : 0
                }).ToList()
            }
        };

        return View(vm);
    }

    // ───────────── ساخت پوشه ─────────────

    [HttpPost]
    public async Task<IActionResult> CreateFolder(int? parentId, string? name)
    {
        var uid = UserId;
        if (!await _storage.OwnsFolderAsync(uid, parentId)) return NotFound();

        var clean = StorageService.SanitizeName(name);
        if (clean.Length == 0)
        {
            TempData["Error"] = "نام پوشه نامعتبر است.";
        }
        else if (await _storage.NameExistsAsync(uid, parentId, clean))
        {
            TempData["Error"] = "در این مسیر مورد دیگری با همین نام وجود دارد.";
        }
        else
        {
            var now = DateTime.UtcNow;
            _db.Items.Add(new StorageItem
            {
                OwnerId = uid,
                ParentId = parentId,
                Name = clean,
                IsFolder = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            await _db.SaveChangesAsync();
            TempData["Msg"] = $"پوشه «{clean}» ساخته شد.";
        }

        return RedirectToAction(nameof(Index), new { folder = parentId });
    }

    // ───────────── آپلود (هر درخواست یک فایل؛ از JS با نوار پیشرفت) ─────────────

    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> Upload(int? folderId, IFormFile? file)
    {
        var uid = UserId;

        if (file == null)
            return BadRequest(new { ok = false, error = "فایلی دریافت نشد." });

        if (!await _storage.OwnsFolderAsync(uid, folderId))
            return NotFound(new { ok = false, error = "پوشه مقصد پیدا نشد." });

        var (used, quota) = await _storage.GetUsageAsync(uid);
        if (quota > 0 && used + file.Length > quota)
            return BadRequest(new { ok = false, error = "فضای کافی نیست." });

        var name = StorageService.SanitizeName(Path.GetFileName(file.FileName));
        if (name.Length == 0) name = "file";
        name = await _storage.UniqueNameAsync(uid, folderId, name);

        await using var stream = file.OpenReadStream();
        var item = await _storage.SaveFileAsync(uid, folderId, name, stream);

        return Json(new { ok = true, id = item.Id, name = item.Name });
    }

    // ───────────── تغییر نام / حذف / انتقال ─────────────

    [HttpPost]
    public async Task<IActionResult> Rename(int id, string? name)
    {
        var uid = UserId;
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == uid);
        if (item == null) return NotFound();

        var clean = StorageService.SanitizeName(name);
        if (clean.Length == 0)
        {
            TempData["Error"] = "نام نامعتبر است.";
        }
        else if (await _storage.NameExistsAsync(uid, item.ParentId, clean, item.Id))
        {
            TempData["Error"] = "در این مسیر مورد دیگری با همین نام وجود دارد.";
        }
        else
        {
            item.Name = clean;
            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Msg"] = "نام تغییر کرد.";
        }

        return RedirectToAction(nameof(Index), new { folder = item.ParentId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var uid = UserId;
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == uid);
        if (item == null) return NotFound();

        var parentId = item.ParentId;
        var name = item.Name;
        await _storage.DeleteAsync(item);

        TempData["Msg"] = $"«{name}» حذف شد.";
        return RedirectToAction(nameof(Index), new { folder = parentId });
    }

    /// <summary>انتقال با کشیدن و رها کردن (JS). targetId خالی یعنی ریشه.</summary>
    [HttpPost]
    public async Task<IActionResult> Move(int id, int? targetId)
    {
        var uid = UserId;
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == uid);
        if (item == null)
            return NotFound(new { ok = false, error = "مورد پیدا نشد." });

        if (!await _storage.OwnsFolderAsync(uid, targetId))
            return NotFound(new { ok = false, error = "پوشه مقصد پیدا نشد." });

        if (item.ParentId == targetId)
            return Json(new { ok = true });

        if (item.IsFolder && targetId != null && await _storage.IsSelfOrDescendantAsync(item.Id, targetId.Value))
            return BadRequest(new { ok = false, error = "نمی‌شود پوشه را داخل خودش منتقل کرد." });

        if (await _storage.NameExistsAsync(uid, targetId, item.Name))
            return BadRequest(new { ok = false, error = "در مقصد مورد دیگری با همین نام وجود دارد." });

        item.ParentId = targetId;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Json(new { ok = true });
    }

    // ───────────── دانلود و پیش‌نمایش (مالک یا کسی که دسترسی دارد) ─────────────

    public async Task<IActionResult> Download(int id)
    {
        var item = await _db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && !i.IsFolder);
        if (item == null || !await _storage.CanAccessAsync(UserId, item)) return NotFound();

        var path = _storage.GetPath(item);
        if (!System.IO.File.Exists(path)) return NotFound();

        var kind = FileTypes.Get(item.Name);
        return PhysicalFile(path, FileTypes.ContentTypeFor(item.Name, kind, inline: false), item.Name, enableRangeProcessing: true);
    }

    public async Task<IActionResult> Preview(int id)
    {
        var item = await _db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && !i.IsFolder);
        if (item == null || !await _storage.CanAccessAsync(UserId, item)) return NotFound();

        var kind = FileTypes.Get(item.Name);
        if (kind.Preview == null)
            return RedirectToAction(nameof(Download), new { id });

        var path = _storage.GetPath(item);
        if (!System.IO.File.Exists(path)) return NotFound();

        // محتوای هر فایل ثابت است (فایل جدید = شناسه‌ی جدید)؛ مرورگر می‌تواند کش کند.
        Response.Headers.CacheControl = "private, max-age=3600";

        // enableRangeProcessing: مرورگر با هدر Range فقط تکه‌ی لازم را می‌گیرد (استریم ویدیو/صوت و جلو بردن).
        // بدون نام دانلود => نمایش داخل مرورگر؛ فقط برای انواع امن (بالا بررسی شد)
        return PhysicalFile(path, FileTypes.ContentTypeFor(item.Name, kind, inline: true), enableRangeProcessing: true);
    }

    public async Task<IActionResult> DownloadFolder(int id)
    {
        var folder = await _db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && i.IsFolder);
        if (folder == null || !await _storage.CanAccessAsync(UserId, folder)) return NotFound();

        var descendants = await _storage.GetDescendantsAsync(folder.Id);

        // مسیر نسبی هر پوشه داخل zip (descendants سطح‌به‌سطح است، پس والدین زودتر می‌آیند)
        var paths = new Dictionary<int, string> { [folder.Id] = "" };

        var tempPath = Path.Combine(Path.GetTempPath(), "lancloud-" + Guid.NewGuid().ToString("N") + ".zip");
        var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
            81920, FileOptions.DeleteOnClose);

        try
        {
            using (var zip = new ZipArchive(tempStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var item in descendants)
                {
                    if (item.ParentId == null || !paths.TryGetValue(item.ParentId.Value, out var parentPath))
                        continue;

                    var entryPath = parentPath + item.Name;

                    if (item.IsFolder)
                    {
                        paths[item.Id] = entryPath + "/";
                        zip.CreateEntry(entryPath + "/");
                        continue;
                    }

                    var physical = _storage.GetPath(item);
                    if (!System.IO.File.Exists(physical)) continue;

                    var entry = zip.CreateEntry(entryPath, CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await using var source = System.IO.File.OpenRead(physical);
                    await source.CopyToAsync(entryStream);
                }
            }
        }
        catch
        {
            await tempStream.DisposeAsync();
            throw;
        }

        tempStream.Position = 0;
        return File(tempStream, "application/zip", folder.Name + ".zip");
    }
}
