# V2rayWatch Server

نسخهٔ مستقل Linux با پنل وب؛ این commit فقط پایهٔ اولیه است و جایگزین نسخه‌های Android و Windows نیست.

## آماده در این مرحله
- ASP.NET Core روی .NET 10 با ساختار Core / Engine / Web
- PATH اختصاصی از اولین نصب و پشتیبانی تمام endpointها زیر prefix
- پورت پیش‌فرض **6000** و قابل تغییر
- ورود مدیر با رمز تصادفی نصب، کوکی HttpOnly و محدودیت تلاش ورود
- systemd و اجرای خودکار پس از reboot با کاربر غیر root
- اسکریپت Native مخصوص Ubuntu 24.04

**هنوز آماده نیست:** موتور Xray لینوکس، مدیریت پروفایل/Subscription، پایش دائمی، SQLite، تاریخچه و نمودار. ظرفیت 100 پروفایل فعلاً هدف طراحی است، نه قابلیت فعال این build.

## ساخت و نصب
روی سیستم build، .NET 10 SDK نصب باشد:
```bash
bash Server/scripts/publish-linux.sh
```
پوشهٔ `Server/publish` را به Ubuntu 24.04 منتقل کنید و .NET 10 ASP.NET Core Runtime را نصب کنید:
```bash
sudo bash Server/scripts/install-ubuntu.sh
```
پورت قابل تغییر است:
```bash
sudo bash Server/scripts/install-ubuntu.sh --port 6100
```
PATH تصادفی تولید می‌شود. PATH دلخواه:
```bash
sudo bash Server/scripts/install-ubuntu.sh --port 6000 --path /vw-my-private-panel-path
```
PATH باید با / شروع شود و 16 تا 64 نویسهٔ امن داشته باشد. ریشهٔ / پنل را نمایش نمی‌دهد.

آدرس نمونه: `http://SERVER-IP:6000/vw-<random-token>`
نام کاربری `admin` است. رمز اولیه در خروجی و فایل `/root/v2raywatch-initial-credentials.txt` با مجوز 0600 ذخیره می‌شود؛ آن را امن نگه دارید و فایل را حذف کنید.

تنظیمات در `/etc/v2raywatch/server.json` است. پس از تغییر پورت یا PATH:
```bash
sudo systemctl restart v2raywatch
sudo systemctl status v2raywatch
sudo journalctl -u v2raywatch -n 100 --no-pager
```

## امنیت
پنل فعلاً فقط HTTP است؛ PATH تصادفی جایگزین احراز هویت نیست و HTTP رمز عبور/کوکی نشست را در مسیر شبکه رمزنگاری نمی‌کند. برای تست Public پورت را در Firewall فقط برای IPهای مورد اعتماد باز کنید. نصب‌کننده Firewall را تغییر نمی‌دهد.

برنامه: `/opt/v2raywatch` · تنظیمات: `/etc/v2raywatch` · داده‌ها: `/var/lib/v2raywatch` · سرویس: `v2raywatch`
