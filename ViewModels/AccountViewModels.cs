using System.ComponentModel.DataAnnotations;

namespace LanCloud.ViewModels;

public class LoginVm
{
    [Required(ErrorMessage = "نام کاربری را وارد کنید.")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "رمز عبور را وارد کنید.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }
}

public class RegisterVm
{
    [Required(ErrorMessage = "نام کاربری را وارد کنید.")]
    [RegularExpression(@"^[a-zA-Z0-9_.\-]{3,30}$", ErrorMessage = "نام کاربری باید ۳ تا ۳۰ کاراکتر و فقط شامل حروف انگلیسی، عدد، نقطه، خط تیره یا زیرخط باشد.")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "نام نمایشی را وارد کنید.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "نام نمایشی باید بین ۲ تا ۶۰ کاراکتر باشد.")]
    public string DisplayName { get; set; } = "";

    [Required(ErrorMessage = "رمز عبور را وارد کنید.")]
    [MinLength(6, ErrorMessage = "رمز عبور باید حداقل ۶ کاراکتر باشد.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "تکرار رمز عبور را وارد کنید.")]
    [Compare(nameof(Password), ErrorMessage = "رمز عبور و تکرار آن یکسان نیست.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public class ProfileVm
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public long Used { get; set; }
    public long Quota { get; set; }
    public int FileCount { get; set; }
}
