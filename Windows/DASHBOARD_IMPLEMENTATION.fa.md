# پیاده‌سازی داشبورد اختصاصی Windows

## وضعیت فعلی

داشبورد V2rayWatch در فرایند Build از سورس upstream نسخهٔ 7.25.6 به پنجرهٔ اصلی WPF تزریق می‌شود. اسکریپت `Windows/apply-dashboard.ps1`، نمای اصلی را به رابط V2rayWatch تغییر می‌دهد و نمای قدیمی را در XAML نگه می‌دارد تا ViewModelها و handlerهای upstream حفظ شوند.

## اتصال به امکانات واقعی upstream

داشبورد از ViewModelهای اصلی برنامه استفاده می‌کند، نه داده‌های ساختگی:

| بخش رابط | منبع / فرمان |
|---|---|
| فهرست سرورها | `ProfilesViewModel.ProfileItems` |
| سرور انتخاب‌شده | `ProfilesViewModel.SelectedProfile` |
| انتخاب سرور فعال | `ProfilesViewModel.SetDefaultServerCmd` |
| تست سریع تأخیر | `ProfilesViewModel.FastRealPingCmd` |
| تست گروهی سرورها | `ProfilesViewModel.MixedTestServerCmd` |
| افزودن از کلیپ‌بورد | `AddServerViaClipboardCmd` |
| اطلاعات اتصال و ورودی | `StatusBarViewModel.RunningServerDisplay`, `RunningInfoDisplay`, `InboundDisplay` |
| ترافیک متنی زنده | `StatusBarViewModel.SpeedProxyDisplay`, `SpeedDirectDisplay` |
| نمودار زنده دانلود/آپلود | رویداد `AppEvents.DispatcherStatisticsRequested`؛ نمونه‌های واقعی `ProxyDown` و `ProxyUp` رسم می‌شوند (۴۸ نمونه اخیر) |
| System Proxy | `SystemProxySetCmd`, `SystemProxyClearCmd` |
| TUN | `StatusBarViewModel.EnableTun` |
| مسیریابی | `RoutingItems`, `SelectedRouting`, `RoutingSettingCmd` |
| اشتراک‌ها | `SubSettingCmd`, `SubUpdateCmd` |
| تنظیمات | `OptionSettingCmd` |
| بازخوانی | `ReloadCmd` |

برچسب‌های ناوبری از منابع ترجمهٔ `ResUI` استفاده می‌کنند. جهت کلی داشبورد در شروع بر اساس فرهنگ فعلی برنامه بین RTL و LTR انتخاب می‌شود.

## ابزار پیش‌نمایش مستقل

پوشهٔ `Windows/DesktopPrototype` یک پوستهٔ WPF جداگانه و ابزار import آزمایشی دارد. parser آن لینک‌های VLESS، VMess، Trojan، Shadowsocks، Hysteria2، SOCKS و HTTP را تشخیص می‌دهد. آزمون‌های smoke برای چند پروتکل و Subscription کدگذاری‌شده در `Windows/DesktopPrototype.Tests` نگهداری می‌شود.

این prototype مستقل، موتور VPN ندارد و پروفایل‌هایش در LocalAppData ذخیره می‌شوند. نسخهٔ اصلی یکپارچه‌شده باید از profile store و موتور upstream استفاده کند؛ این دو مسیر را نباید با هم اشتباه گرفت.

## Build و کنترل کیفیت

Workflow ویندوز دو کار انجام می‌دهد:
1. Build پیش‌نمایش WPF و اجرای smoke testهای parser.
2. دریافت سورس pin‌شدهٔ v2rayN 7.25.6، اعمال branding و داشبورد، سپس Build برنامهٔ یکپارچه.

Build موفق، خطای کامپایل را رد می‌کند اما جای تست اجرای برنامه روی Windows 10/11 واقعی، بررسی rendering فونت فارسی، اتصال به سرور معتبر، System Proxy و TUN را نمی‌گیرد.

## پیش‌نمایش قابل دریافت

یک بستهٔ آزمایشی portable از workflow ویندوز در بخش Artifacts این اجرا ذخیره شده است. این بسته پیش‌نمایش کامیت قبلی است و نمودار لحظه‌ای که در آخرین تغییرات اضافه شده هنوز داخل آن نیست:
https://github.com/hatinati2/V2rayWatch/actions/runs/41144162561

این artifact از branch توسعه ساخته شده و Release پایدار نیست؛ فایل artifact موقت است و تاریخ انقضا دارد.

## محدودیت‌ها و گام بعدی

- هنوز هیچ Release جدیدی از این شاخه منتشر نشده است.
- کنترل‌های پراکسی/TUN اکنون از فرمان‌های upstream استفاده می‌کنند؛ اجرای واقعی آن‌ها نیازمند تست روی دستگاه ویندوزی است.
- نمودار ترافیک با داده‌های واقعی ProxyUp/ProxyDown اضافه شده است؛ نمایش آن وابسته به فعال بودن آمار/سرعت لحظه‌ای در تنظیمات upstream است.
- ظاهر و ترجمهٔ تمام پیام‌ها/پنجره‌های ثانویه هنوز یکدست‌سازی نشده است.
- بسته‌بندی قابل توزیع باید همراه فایل‌های core و مجوزها انجام شود و سپس روی ویندوز واقعی آزموده شود.
