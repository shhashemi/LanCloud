using System.Globalization;

namespace LanCloud.Services;

/// <summary>توابع کمکی نمایش اندازه و تاریخ (تاریخ شمسی).</summary>
public static class Fmt
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Size(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";

        double value = bytes;
        var i = 0;
        while (value >= 1024 && i < Units.Length - 1)
        {
            value /= 1024;
            i++;
        }

        return $"{value.ToString("0.#", CultureInfo.InvariantCulture)} {Units[i]}";
    }

    public static string Date(DateTime utc)
    {
        var local = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        var pc = new PersianCalendar();
        return $"{pc.GetYear(local):0000}/{pc.GetMonth(local):00}/{pc.GetDayOfMonth(local):00}";
    }

    public static string DateTimeText(DateTime utc)
    {
        var local = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        return $"{Date(utc)} - {local.ToString("HH:mm", CultureInfo.InvariantCulture)}";
    }

    public static int Percent(long used, long quota)
    {
        if (quota <= 0) return 0;
        var p = (int)Math.Min(100, used * 100 / quota);
        return p < 0 ? 0 : p;
    }
}
