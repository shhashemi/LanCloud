# ابر محلی (LanCloud)

وب‌سایت ASP.NET Core برای فضای ابری شبکه محلی: هر عضو فضای خودش را دارد و می‌تواند فایل‌هایش را برای بقیه در دسترس بگذارد.

## اجرا
1. .NET 8 SDK (یا جدیدتر) نصب باشد. برای .NET 10، `TargetFramework` و نسخه پکیج در `LanCloud.csproj` را به 10 تغییر بده.
2. در پوشه پروژه: `dotnet run`
3. از هر دستگاه شبکه: `http://IP-سرور:5000` (آدرس در `appsettings.json` → `Urls` است).
4. اولین کاربری که ثبت‌نام کند، مدیر شبکه می‌شود.
5. اگر فایروال ویندوز اجازه نداد: `netsh advfirewall firewall add rule name="LanCloud" dir=in action=allow protocol=TCP localport=5000`

## تنظیمات (appsettings.json)
- `Storage:RootPath` مسیر ذخیره فایل‌ها (می‌توانی روی دیسک بزرگ‌تر بگذاری)
- `Storage:DefaultQuotaGB` سهمیه پیش‌فرض هر کاربر (۰ = نامحدود)
- `App:AllowRegistration` ثبت‌نام آزاد (بعد از ساخت حساب‌ها می‌توانی false کنی)
- `Database:Path` مسیر دیتابیس SQLite

## فونت (اختیاری)
فایل `vazirmatn.woff2` را داخل `wwwroot/fonts` بگذار تا فونت وزیرمتن بدون اینترنت استفاده شود.

## ساختار
Models / Data (DbContext) / Services / Controllers / ViewModels / Views / wwwroot

## نمایش و استریم ویدیو
- ویدیو با درخواست‌های Range تکه‌تکه پخش می‌شود؛ با بازکردن ویدیو فقط اطلاعات اولیه و بخش لازم گرفته می‌شود و با جلو بردن، همان نقطه بارگیری می‌شود. با بستن یا رفتن به فایل بعدی، دانلود متوقف می‌شود.
- مرورگر فقط H.264 (mp4)، WebM و مشابه را پخش می‌کند. برای شروع سریع‌تر mp4ها، یک‌بار این را روی فایل اجرا کن:
  `ffmpeg -i in.mp4 -c copy -movflags +faststart out.mp4`
- در نمایشگر: کلیدهای ← و → (یا PageUp/PageDown) و دکمه‌های کناری برای قبلی/بعدی؛ در موبایل با کشیدن انگشت روی تصویر.
