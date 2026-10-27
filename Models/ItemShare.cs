namespace LanCloud.Models;

/// <summary>اشتراک‌گذاری یک فایل/پوشه با یک عضو مشخص (فقط‌خواندنی).</summary>
public class ItemShare
{
    public int Id { get; set; }

    public int ItemId { get; set; }
    public StorageItem Item { get; set; } = null!;

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
