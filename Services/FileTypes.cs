using Microsoft.AspNetCore.StaticFiles;

namespace LanCloud.Services;

/// <param name="Icon">نام آیکون در sprite (بدون پیشوند i-)</param>
/// <param name="Css">کلاس رنگ نوع فایل</param>
/// <param name="Label">برچسب فارسی</param>
/// <param name="Preview">نوع پیش‌نمایش داخل مرورگر (image/video/audio/pdf/text) یا null</param>
public sealed record FileKind(string Icon, string Css, string Label, string? Preview);

public static class FileTypes
{
    private static readonly FileExtensionContentTypeProvider Provider = new();

    private static readonly FileKind Image = new("file-image", "ft-image", "تصویر", "image");
    private static readonly FileKind ImageNoPreview = new("file-image", "ft-image", "تصویر", null);
    private static readonly FileKind Video = new("file-video", "ft-video", "ویدیو", "video");
    private static readonly FileKind VideoNoPreview = new("file-video", "ft-video", "ویدیو", null);
    private static readonly FileKind Audio = new("file-audio", "ft-audio", "صوت", "audio");
    private static readonly FileKind AudioNoPreview = new("file-audio", "ft-audio", "صوت", null);
    private static readonly FileKind Pdf = new("file-pdf", "ft-pdf", "PDF", "pdf");
    private static readonly FileKind Word = new("file-word", "ft-word", "سند Word", null);
    private static readonly FileKind Excel = new("file-excel", "ft-excel", "صفحه‌گسترده", null);
    private static readonly FileKind Ppt = new("file-ppt", "ft-ppt", "ارائه", null);
    private static readonly FileKind Archive = new("file-archive", "ft-archive", "فایل فشرده", null);
    private static readonly FileKind Code = new("file-code", "ft-code", "کد", "text");
    private static readonly FileKind Text = new("file-text", "ft-text", "متن", "text");
    private static readonly FileKind App = new("file-app", "ft-app", "برنامه", null);
    private static readonly FileKind Database = new("file-db", "ft-db", "پایگاه داده", null);
    private static readonly FileKind Other = new("file", "ft-other", "فایل", null);

    // ترتیب مهم است: Map باید بعد از فیلدهای بالا مقداردهی شود.
    private static readonly Dictionary<string, FileKind> Map = Build();

    private static Dictionary<string, FileKind> Build()
    {
        var d = new Dictionary<string, FileKind>(StringComparer.OrdinalIgnoreCase);

        void Add(FileKind kind, string extensions)
        {
            foreach (var e in extensions.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                d[e] = kind;
        }

        Add(Image, "jpg jpeg png gif webp bmp avif ico");
        // svg عمداً پیش‌نمایش داده نمی‌شود (می‌تواند اسکریپت داشته باشد)
        Add(ImageNoPreview, "svg tif tiff heic heif raw psd ai xcf");

        Add(Video, "mp4 webm ogv m4v mov");
        Add(VideoNoPreview, "mkv avi wmv flv mpg mpeg 3gp ts");

        Add(Audio, "mp3 wav ogg oga m4a aac flac opus");
        Add(AudioNoPreview, "wma mid midi amr aiff");

        Add(Pdf, "pdf");
        Add(Word, "doc docx dot dotx odt rtf pages");
        Add(Excel, "xls xlsx xlsm ods csv tsv numbers");
        Add(Ppt, "ppt pptx pps ppsx odp key");
        Add(Archive, "zip rar 7z tar gz tgz bz2 xz iso img cab");

        Add(Code, "html htm css js mjs ts tsx jsx json xml yml yaml cs cshtml razor csproj sln cpp c h hpp java kt py php rb go rs swift sh bat cmd ps1 lua vue sass scss less");
        Add(Text, "txt log md ini cfg conf env readme");

        Add(App, "exe msi apk dmg deb rpm appimage jar");
        Add(Database, "db sqlite sqlite3 mdb accdb sql bak mdf");

        return d;
    }

    public static FileKind Get(string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.');
        return ext.Length > 0 && Map.TryGetValue(ext, out var kind) ? kind : Other;
    }

    /// <summary>
    /// نوع محتوا برای پاسخ. فایل‌های متنی/کد در حالت inline همیشه text/plain سرو می‌شوند
    /// تا هرگز (مثلاً HTML) داخل مرورگر اجرا نشوند.
    /// </summary>
    public static string ContentTypeFor(string fileName, FileKind kind, bool inline)
    {
        if (inline && kind.Preview == "text")
            return "text/plain; charset=utf-8";

        return Provider.TryGetContentType(fileName, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}
