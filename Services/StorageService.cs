using LanCloud.Data;
using LanCloud.Models;
using Microsoft.EntityFrameworkCore;

namespace LanCloud.Services;

/// <summary>منطق مشترک ذخیره‌سازی: مسیر فایل، سهمیه، نام‌ها، دسترسی و حذف.</summary>
public class StorageService
{
    private readonly AppDbContext _db;
    private readonly StorageOptions _options;

    public StorageService(AppDbContext db, StorageOptions options)
    {
        _db = db;
        _options = options;
    }

    // ───────────── مسیر و ذخیره روی دیسک ─────────────

    public string GetPath(StorageItem item) =>
        Path.Combine(_options.RootPath, item.OwnerId.ToString(), item.StoredName ?? "missing");

    public async Task<StorageItem> SaveFileAsync(int ownerId, int? parentId, string name, Stream content)
    {
        var dir = Path.Combine(_options.RootPath, ownerId.ToString());
        Directory.CreateDirectory(dir);

        var stored = Guid.NewGuid().ToString("N");
        var full = Path.Combine(dir, stored);

        await using (var fs = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(fs);
        }

        var now = DateTime.UtcNow;
        var item = new StorageItem
        {
            OwnerId = ownerId,
            ParentId = parentId,
            Name = name,
            IsFolder = false,
            Size = new FileInfo(full).Length,
            StoredName = stored,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            _db.Items.Add(item);
            await _db.SaveChangesAsync();
        }
        catch
        {
            try { File.Delete(full); } catch { /* ignore */ }
            throw;
        }

        return item;
    }

    // ───────────── سهمیه ─────────────

    public async Task<(long Used, long Quota)> GetUsageAsync(int userId)
    {
        var quota = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.QuotaBytes)
            .FirstOrDefaultAsync();

        var used = await _db.Items.AsNoTracking()
            .Where(i => i.OwnerId == userId && !i.IsFolder)
            .SumAsync(i => (long?)i.Size) ?? 0;

        return (used, quota);
    }

    // ───────────── نام‌ها ─────────────

    private static readonly char[] ExtraInvalid = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    /// <summary>نام را پاکسازی می‌کند. اگر چیزی باقی نماند رشته خالی برمی‌گرداند.</summary>
    public static string SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Where(c => !char.IsControl(c) && !invalid.Contains(c) && !ExtraInvalid.Contains(c)).ToArray();
        var clean = new string(chars).Trim().TrimEnd('.').Trim();

        if (clean is "" or "." or "..") return "";

        if (clean.Length > 200)
        {
            var ext = Path.GetExtension(clean);
            if (ext.Length > 20) ext = "";
            clean = clean.Substring(0, 200 - ext.Length) + ext;
        }

        return clean;
    }

    public async Task<bool> NameExistsAsync(int ownerId, int? parentId, string name, int? excludeId = null)
    {
        var lower = name.ToLower();
        return await _db.Items.AsNoTracking().AnyAsync(i =>
            i.OwnerId == ownerId &&
            i.ParentId == parentId &&
            i.Id != excludeId &&
            i.Name.ToLower() == lower);
    }

    /// <summary>اگر نام تکراری بود به شکل «name (1).ext» تغییرش می‌دهد.</summary>
    public async Task<string> UniqueNameAsync(int ownerId, int? parentId, string name)
    {
        if (!await NameExistsAsync(ownerId, parentId, name)) return name;

        var ext = Path.GetExtension(name);
        var baseName = Path.GetFileNameWithoutExtension(name);

        for (var n = 1; n < 10000; n++)
        {
            var candidate = $"{baseName} ({n}){ext}";
            if (!await NameExistsAsync(ownerId, parentId, candidate)) return candidate;
        }

        return $"{baseName} ({Guid.NewGuid():N}){ext}";
    }

    // ───────────── پوشه‌ها و دسترسی ─────────────

    public async Task<bool> OwnsFolderAsync(int userId, int? folderId)
    {
        if (folderId == null) return true;
        return await _db.Items.AsNoTracking()
            .AnyAsync(i => i.Id == folderId && i.OwnerId == userId && i.IsFolder);
    }

    /// <summary>زنجیره از ریشه تا خود آیتم (برای breadcrumb).</summary>
    public async Task<List<StorageItem>> GetChainAsync(StorageItem item)
    {
        var chain = new List<StorageItem> { item };
        var cur = item;

        while (cur.ParentId != null && chain.Count < 100)
        {
            var parentId = cur.ParentId.Value;
            var parent = await _db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == parentId);
            if (parent == null) break;
            chain.Insert(0, parent);
            cur = parent;
        }

        return chain;
    }

    /// <summary>همه فرزندان (مستقیم و غیرمستقیم) یک پوشه، به ترتیب سطح به سطح.</summary>
    public async Task<List<StorageItem>> GetDescendantsAsync(int folderId)
    {
        var result = new List<StorageItem>();
        var queue = new Queue<int>();
        queue.Enqueue(folderId);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            var kids = await _db.Items.Where(i => i.ParentId == id).ToListAsync();
            foreach (var kid in kids)
            {
                result.Add(kid);
                if (kid.IsFolder) queue.Enqueue(kid.Id);
            }
        }

        return result;
    }

    /// <summary>آیا candidateId همان folderId است یا داخل آن قرار دارد؟</summary>
    public async Task<bool> IsSelfOrDescendantAsync(int folderId, int candidateId)
    {
        int? cur = candidateId;
        var guard = 0;

        while (cur != null && guard++ < 200)
        {
            var id = cur.Value;
            if (id == folderId) return true;
            cur = await _db.Items.AsNoTracking()
                .Where(i => i.Id == id)
                .Select(i => i.ParentId)
                .FirstOrDefaultAsync();
        }

        return false;
    }

    /// <summary>
    /// مالک، یا آیتم/یکی از پوشه‌های بالاترش عمومی باشد، یا با کاربر به اشتراک گذاشته شده باشد.
    /// </summary>
    public async Task<bool> CanAccessAsync(int userId, StorageItem item)
    {
        if (item.OwnerId == userId) return true;

        StorageItem? cur = item;
        var guard = 0;

        while (cur != null && guard++ < 100)
        {
            if (cur.IsPublic) return true;

            var curId = cur.Id;
            if (await _db.Shares.AsNoTracking().AnyAsync(s => s.ItemId == curId && s.UserId == userId))
                return true;

            if (cur.ParentId == null) break;
            var parentId = cur.ParentId.Value;
            cur = await _db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == parentId);
        }

        return false;
    }

    // ───────────── حذف ─────────────

    /// <summary>یک فایل یا پوشه (با تمام محتویاتش) را از دیتابیس و دیسک حذف می‌کند. item باید tracked باشد.</summary>
    public async Task DeleteAsync(StorageItem item)
    {
        var all = new List<StorageItem> { item };
        if (item.IsFolder)
            all.AddRange(await GetDescendantsAsync(item.Id));

        _db.Items.RemoveRange(all);
        await _db.SaveChangesAsync();

        foreach (var file in all.Where(x => !x.IsFolder && x.StoredName != null))
        {
            try { File.Delete(GetPath(file)); } catch { /* ignore */ }
        }
    }

    /// <summary>کاربر را همراه با همه فایل‌هایش (دیتابیس + دیسک) حذف می‌کند.</summary>
    public async Task DeleteUserAsync(AppUser user)
    {
        var dir = Path.Combine(_options.RootPath, user.Id.ToString());

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch { /* ignore */ }
    }
}
