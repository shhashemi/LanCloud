namespace LanCloud.Models;

/// <summary>
/// یک فایل یا پوشه در فضای ابری یک کاربر.
/// پوشه‌ها فقط در دیتابیس هستند؛ محتوای فایل‌ها روی دیسک با نام تصادفی (StoredName) ذخیره می‌شود.
/// </summary>
public class StorageItem
{
    public int Id { get; set; }

    public int OwnerId { get; set; }
    public AppUser Owner { get; set; } = null!;

    public int? ParentId { get; set; }
    public StorageItem? Parent { get; set; }
    public List<StorageItem> Children { get; set; } = new();

    /// <summary>نامی که کاربر می‌بیند.</summary>
    public string Name { get; set; } = "";

    public bool IsFolder { get; set; }

    /// <summary>اندازه فایل به بایت (برای پوشه صفر).</summary>
    public long Size { get; set; }

    /// <summary>نام فایل روی دیسک (GUID). برای پوشه خالی است.</summary>
    public string? StoredName { get; set; }

    /// <summary>اگر true باشد همه اعضای شبکه می‌توانند ببینند و دانلود کنند.</summary>
    public bool IsPublic { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ItemShare> Shares { get; set; } = new();
}
