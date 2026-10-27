using LanCloud.Data;
using LanCloud.Models;
using LanCloud.Services;
using LanCloud.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanCloud.Controllers;

public class ShareController : AppController
{
    private readonly AppDbContext _db;
    private readonly StorageService _storage;

    public ShareController(AppDbContext db, StorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    // ───────────── اشتراک‌گذاشته‌شده با من ─────────────

    public async Task<IActionResult> Index(string? q)
    {
        var uid = UserId;
        q = q?.Trim();

        var query = _db.Items.AsNoTracking()
            .Include(i => i.Owner)
            .Where(i => i.Shares.Any(s => s.UserId == uid));

        if (!string.IsNullOrEmpty(q)) query = query.Where(i => i.Name.Contains(q));

        var items = await query.ToListAsync();

        ViewBag.Query = q;
        return View(BuildList(items, "هنوز چیزی با تو به اشتراک گذاشته نشده",
            "وقتی یکی از اعضا فایل یا پوشه‌ای را برای تو به اشتراک بگذارد، اینجا پیدا می‌شود."));
    }

    // ───────────── فایل‌های عمومی شبکه ─────────────

    public async Task<IActionResult> Network(string? q)
    {
        var uid = UserId;
        q = q?.Trim();

        var query = _db.Items.AsNoTracking()
            .Include(i => i.Owner)
            .Where(i => i.IsPublic && i.OwnerId != uid && i.Owner.IsActive);

        if (!string.IsNullOrEmpty(q)) query = query.Where(i => i.Name.Contains(q));

        var items = await query.ToListAsync();

        ViewBag.Query = q;
        return View(BuildList(items, "هنوز کسی چیزی را عمومی نکرده",
            "فایل‌هایی که اعضا برای همه‌ی شبکه به اشتراک می‌گذارند اینجا نمایش داده می‌شود."));
    }

    // ───────────── مرور داخل یک پوشه‌ی اشتراکی/عمومی ─────────────

    public async Task<IActionResult> Folder(int id)
    {
        var uid = UserId;
        var folder = await _db.Items.AsNoTracking()
            .Include(i => i.Owner)
            .FirstOrDefaultAsync(i => i.Id == id && i.IsFolder);

        if (folder == null) return NotFound();
        if (folder.OwnerId == uid) return RedirectToAction("Index", "Files", new { folder = id });
        if (!await _storage.CanAccessAsync(uid, folder)) return NotFound();

        // breadcrumb از بالاترین پوشه‌ای شروع می‌شود که دسترسی از آن‌جا داده شده
        var chain = await _storage.GetChainAsync(folder);
        var chainIds = chain.Select(c => c.Id).ToList();
        var sharedIds = await _db.Shares.AsNoTracking()
            .Where(s => s.UserId == uid && chainIds.Contains(s.ItemId))
            .Select(s => s.ItemId)
            .ToListAsync();

        var topIndex = chain.FindIndex(c => c.IsPublic || sharedIds.Contains(c.Id));
        if (topIndex < 0) topIndex = chain.Count - 1;

        var top = chain[topIndex];
        var crumbs = chain.Skip(topIndex).ToList();

        var children = await _db.Items.AsNoTracking()
            .Include(i => i.Owner)
            .Where(i => i.ParentId == id)
            .ToListAsync();

        return View(new ShareFolderVm
        {
            Folder = folder,
            Crumbs = crumbs,
            ViaShare = sharedIds.Contains(top.Id),
            List = BuildList(children, "این پوشه خالی است", "")
        });
    }

    // ───────────── پنجره‌ی مدیریت اشتراک (فقط مالک) ─────────────

    [HttpGet]
    public async Task<IActionResult> Manage(int id)
    {
        var uid = UserId;
        var item = await _db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == uid);
        if (item == null) return NotFound();

        var selected = await _db.Shares.AsNoTracking()
            .Where(s => s.ItemId == id)
            .Select(s => s.UserId)
            .ToListAsync();

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.Id != uid && u.IsActive)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        return PartialView("_ShareDialog", new ShareDialogVm
        {
            Item = item,
            Users = users.Select(u => new ShareUserRow
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Username = u.Username,
                Selected = selected.Contains(u.Id)
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save(int id, bool isPublic, int[]? userIds)
    {
        var uid = UserId;
        var item = await _db.Items.Include(i => i.Shares)
            .FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == uid);
        if (item == null) return NotFound();

        var requested = (userIds ?? Array.Empty<int>()).Where(x => x != uid).Distinct().ToList();
        var wanted = await _db.Users.AsNoTracking()
            .Where(u => requested.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        item.IsPublic = isPublic;

        foreach (var share in item.Shares.Where(s => !wanted.Contains(s.UserId)).ToList())
            _db.Shares.Remove(share);

        foreach (var userId in wanted.Where(w => item.Shares.All(s => s.UserId != w)))
        {
            _db.Shares.Add(new ItemShare { ItemId = item.Id, UserId = userId, CreatedAt = DateTime.UtcNow });
        }

        await _db.SaveChangesAsync();

        TempData["Msg"] = "تنظیمات اشتراک‌گذاری ذخیره شد.";
        return RedirectToAction("Index", "Files", new { folder = item.ParentId });
    }

    // ───────────── کمکی ─────────────

    private static ItemListVm BuildList(List<StorageItem> items, string emptyTitle, string emptyText)
    {
        var sorted = items
            .OrderByDescending(i => i.IsFolder)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase);

        return new ItemListVm
        {
            Owned = false,
            ShowOwner = true,
            EmptyTitle = emptyTitle,
            EmptyText = emptyText,
            Items = sorted.Select(i => new ItemRow
            {
                Item = i,
                OwnerName = i.Owner?.DisplayName
            }).ToList()
        };
    }
}
