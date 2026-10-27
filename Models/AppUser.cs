namespace LanCloud.Models;

/// <summary>عضو شبکه محلی که حساب کاربری دارد.</summary>
public class AppUser
{
    public int Id { get; set; }

    /// <summary>نام کاربری (همیشه با حروف کوچک ذخیره می‌شود).</summary>
    public string Username { get; set; } = "";

    /// <summary>نامی که به بقیه اعضا نمایش داده می‌شود.</summary>
    public string DisplayName { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public bool IsAdmin { get; set; }

    /// <summary>کاربر غیرفعال نمی‌تواند وارد شود.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>سهمیه فضا به بایت. صفر یعنی نامحدود.</summary>
    public long QuotaBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public List<StorageItem> Items { get; set; } = new();
}
