using System.Security.Claims;
using LanCloud.Data;
using LanCloud.Models;
using LanCloud.Services;
using LanCloud.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanCloud.Controllers;

public class AccountController : AppController
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly StorageOptions _storageOptions;
    private readonly StorageService _storage;
    private readonly IConfiguration _config;

    public AccountController(
        AppDbContext db,
        IPasswordHasher<AppUser> hasher,
        StorageOptions storageOptions,
        StorageService storage,
        IConfiguration config)
    {
        _db = db;
        _hasher = hasher;
        _storageOptions = storageOptions;
        _storage = storage;
        _config = config;
    }

    private bool RegistrationOpen => _config.GetValue("App:AllowRegistration", true);

    // ───────────── ورود ─────────────

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Files");

        ViewBag.CanRegister = RegistrationOpen || !await _db.Users.AnyAsync();
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        ViewBag.CanRegister = RegistrationOpen || !await _db.Users.AnyAsync();
        if (!ModelState.IsValid) return View(vm);

        var username = vm.Username.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

        var result = user == null
            ? PasswordVerificationResult.Failed
            : _hasher.VerifyHashedPassword(user, user.PasswordHash, vm.Password);

        if (user == null || result == PasswordVerificationResult.Failed)
        {
            await Task.Delay(500); // کمی کند کردن حدس‌زدن رمز
            ModelState.AddModelError("", "نام کاربری یا رمز عبور اشتباه است.");
            return View(vm);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError("", "این حساب غیرفعال شده است. با مدیر شبکه تماس بگیرید.");
            return View(vm);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _hasher.HashPassword(user, vm.Password);

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await SignInAsync(user, vm.RememberMe);
        return RedirectToLocal(vm.ReturnUrl);
    }

    // ───────────── ثبت‌نام ─────────────

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Files");

        var isFirst = !await _db.Users.AnyAsync();
        if (!RegistrationOpen && !isFirst) return RedirectToAction(nameof(Login));

        ViewBag.IsFirst = isFirst;
        return View(new RegisterVm());
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        var isFirst = !await _db.Users.AnyAsync();
        if (!RegistrationOpen && !isFirst) return RedirectToAction(nameof(Login));

        ViewBag.IsFirst = isFirst;
        if (!ModelState.IsValid) return View(vm);

        var username = vm.Username.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Username == username))
        {
            ModelState.AddModelError(nameof(vm.Username), "این نام کاربری قبلاً گرفته شده است.");
            return View(vm);
        }

        var user = new AppUser
        {
            Username = username,
            DisplayName = vm.DisplayName.Trim(),
            IsAdmin = isFirst, // اولین کاربر ثبت‌شده مدیر می‌شود
            IsActive = true,
            QuotaBytes = _storageOptions.DefaultQuotaBytes,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, vm.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SignInAsync(user, persistent: true);
        TempData["Msg"] = isFirst
            ? "خوش آمدی! تو اولین کاربر هستی و مدیر شبکه محسوب می‌شوی."
            : "حساب شما ساخته شد. خوش آمدید!";
        return RedirectToAction("Index", "Files");
    }

    // ───────────── خروج ─────────────

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ───────────── پروفایل ─────────────

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var uid = UserId;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid);
        if (user == null) return RedirectToAction(nameof(Login));

        var (used, quota) = await _storage.GetUsageAsync(uid);
        var fileCount = await _db.Items.CountAsync(i => i.OwnerId == uid && !i.IsFolder);

        return View(new ProfileVm
        {
            Username = user.Username,
            DisplayName = user.DisplayName,
            IsAdmin = user.IsAdmin,
            CreatedAt = user.CreatedAt,
            Used = used,
            Quota = quota,
            FileCount = fileCount
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProfile(string? displayName)
    {
        var name = (displayName ?? "").Trim();
        if (name.Length < 2 || name.Length > 60)
        {
            TempData["Error"] = "نام نمایشی باید بین ۲ تا ۶۰ کاراکتر باشد.";
            return RedirectToAction(nameof(Profile));
        }

        var uid = UserId;
        var user = await _db.Users.FirstAsync(u => u.Id == uid);
        user.DisplayName = name;
        await _db.SaveChangesAsync();

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await SignInAsync(user, persistent: true); // تا نام جدید در کوکی هم بیاید

        TempData["Msg"] = "پروفایل به‌روزرسانی شد.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(string? currentPassword, string? newPassword, string? confirmPassword)
    {
        var uid = UserId;
        var user = await _db.Users.FirstAsync(u => u.Id == uid);

        if (string.IsNullOrEmpty(currentPassword) ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            TempData["Error"] = "رمز فعلی اشتباه است.";
        }
        else if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
        {
            TempData["Error"] = "رمز جدید باید حداقل ۶ کاراکتر باشد.";
        }
        else if (newPassword != confirmPassword)
        {
            TempData["Error"] = "رمز جدید و تکرار آن یکسان نیست.";
        }
        else
        {
            user.PasswordHash = _hasher.HashPassword(user, newPassword);
            await _db.SaveChangesAsync();
            TempData["Msg"] = "رمز عبور تغییر کرد.";
        }

        return RedirectToAction(nameof(Profile));
    }

    // ───────────── کمکی‌ها ─────────────

    private async Task SignInAsync(AppUser user, bool persistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.DisplayName)
        };
        if (user.IsAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = persistent });
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Files");
    }
}
