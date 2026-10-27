using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LanCloud.Services;

public static class IconExtensions
{
    /// <summary>
    /// یک آیکون تک‌رنگ از sprite داخل _Sprite.cshtml را رندر می‌کند.
    /// استفاده: @Html.Icon("upload")
    /// </summary>
    public static IHtmlContent Icon(this IHtmlHelper html, string name, string? cssClass = null)
    {
        var cls = string.IsNullOrWhiteSpace(cssClass) ? "icon" : "icon " + cssClass;
        return new HtmlString($"<svg class=\"{cls}\" aria-hidden=\"true\"><use href=\"#i-{name}\"></use></svg>");
    }
}
