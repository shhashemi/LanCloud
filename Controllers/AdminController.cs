using LanCloud.Data;
using LanCloud.Models;
using LanCloud.Services;
using LanCloud.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LanCloud.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : AppController
{
    private readonly AppDbContext _db;
    private readonly StorageService _storage;
    private readonly IPasswordHasher<AppUser> _hasher;

    public AdminController(AppDbContext db, StorageService storage, IPasswordHasher<AppUser> hasher)
    {
        _db = db;
        _storage = storage;
        _hasher = hasher;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _db.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync();

        var stats = await _db.Items.AsNoTracking()
            .Where(i => !i.IsFolder)
            .GroupBy(i => i.OwnerId)
            .Select(g => new { UserId = g.Key, Used = g.Sum(x => x.Size), Count = g.Count() })
            .ToListAsync();

        var rows = users.Select(u =>
        {
            var s = stats.FirstOrDefault(x => x.UserId == u.Id);
            return new AdminUserRow { User = u, Used = s?.Used ?? 0, FileCount = s?.Count ?? 0 };
        }).ToList();

        return View(rows);
    }

    [HttpPost]
    public async Task<IActionResult> SetQuota(int id, double? quotaGb)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        user.QuotaBytes = quotaGb is null or <= 0
            ? 0
            : (long)(quotaGb.Value * 1024d * 1024d * 1024d);

        await _db.SaveChangesAsync();
        TempData["Msg"] = $"سهمیه‌ی «{user.DisplayName}» به‌روزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        if (id == UserId)
        {
            TempData["Error"] = "نمی‌توانی حساب خودت را غیرفعال کنی.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();

        TempData["Msg"] = user.IsActive
            ? $"«{user.DisplayName}» فعال شد."
            : $"«{user.DisplayName}» غیرفعال شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleAdmin(int id)
    {
        if (id == UserId)
        {
            TempData["Error"] = "نمی‌توانی نقش مدیر خودت را تغییر بدهی.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        user.IsAdmin = !user.IsAdmin;
        await _db.SaveChangesAsync();

        TempData["Msg"] = user.IsAdmin
            ? $"«{user.DisplayName}» مدیر شد."
            : $"«{user.DisplayName}» دیگر مدیر نیست.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(int id, string? newPassword)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
        {
            TempData["Error"] = "رمز جدید باید حداقل ۶ کاراکتر باشد.";
            return RedirectToAction(nameof(Index));
        }

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        await _db.SaveChangesAsync();

        TempData["Msg"] = $"رمز «{user.DisplayName}» تغییر کرد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (id == UserId)
        {
            TempData["Error"] = "نمی‌توانی حساب خودت را حذف کنی.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var name = user.DisplayName;
        await _storage.DeleteUserAsync(user);

        TempData["Msg"] = $"کاربر «{name}» و همه‌ی فایل‌هایش حذف شد.";
        return RedirectToAction(nameof(Index));
    }
}
