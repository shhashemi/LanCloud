using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace LanCloud.Controllers;

/// <summary>کنترلر پایه: شناسه کاربر واردشده را در دسترس می‌گذارد.</summary>
public abstract class AppController : Controller
{
    protected int UserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
