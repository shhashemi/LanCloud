using System.Globalization;
using System.Security.Claims;
using LanCloud.Data;
using LanCloud.Models;
using LanCloud.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// خروجی اعداد و تاریخ در همه سیستم‌ها یکسان باشد
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// ───────────── مسیر دیتابیس و پوشه فایل‌ها ─────────────
var contentRoot = builder.Environment.ContentRootPath;

var dbPath = Path.GetFullPath(builder.Configuration["Database:Path"] ?? "App_Data/lancloud.db", contentRoot);
var storageRoot = Path.GetFullPath(builder.Configuration["Storage:RootPath"] ?? "App_Data/storage", contentRoot);
var defaultQuotaGb = builder.Configuration.GetValue<long>("Storage:DefaultQuotaGB", 10);

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
Directory.CreateDirectory(storageRoot);

// ───────────── سرویس‌ها ─────────────
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSingleton(new StorageOptions
{
    RootPath = storageRoot,
    DefaultQuotaBytes = defaultQuotaGb * 1024L * 1024L * 1024L
});
builder.Services.AddScoped<StorageService>();
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// آپلود فایل‌های حجیم: محدودیت‌ها برداشته می‌شوند (سهمیه‌ی هر کاربر کنترل را در دست دارد)
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = null);
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = long.MaxValue;
    o.ValueLengthLimit = int.MaxValue;
    o.MemoryBufferThreshold = 1024 * 128; // بیشتر از این روی دیسک موقت بافر می‌شود
});

builder.Services
    .AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// درخواست‌های fetch/XHR توکن CSRF را در هدر می‌فرستند
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.Cookie.Name = "lancloud.auth";
        o.Cookie.HttpOnly = true;
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;

        // اگر مدیر کاربری را غیرفعال/حذف کند یا نقشش را عوض کند، همان لحظه اثر می‌کند
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var idText = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(idText, out var userId))
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync();
                return;
            }

            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var info = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsActive, u.IsAdmin })
                .FirstOrDefaultAsync();

            var claimIsAdmin = ctx.Principal!.IsInRole("Admin");
            if (info == null || !info.IsActive || info.IsAdmin != claimIsAdmin)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync();
            }
        };
    });

// همه صفحات به‌جز ورود/ثبت‌نام نیاز به ورود دارند
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

var app = builder.Build();

// ───────────── ساخت دیتابیس در اولین اجرا ─────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ───────────── Pipeline ─────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "same-origin";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Files}/{action=Index}/{id?}");

app.Run();
