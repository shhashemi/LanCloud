namespace LanCloud.Services;

public class StorageOptions
{
    /// <summary>مسیر کامل پوشه‌ای که فایل‌های کاربران در آن ذخیره می‌شود.</summary>
    public string RootPath { get; set; } = "";

    /// <summary>سهمیه پیش‌فرض هر کاربر جدید (بایت). صفر یعنی نامحدود.</summary>
    public long DefaultQuotaBytes { get; set; }
}
