# پیاده‌سازی داشبورد اختصاصی Windows

## وضعیت فعلی

داشبورد V2rayWatch در فرایند Build از سورس upstream نسخهٔ 7.25.6 به پنجرهٔ اصلی WPF تزریق می‌شود. اسکریپت `Windows/apply-dashboard.ps1`، نمای اصلی را به رابط V2rayWatch تغییر می‌دهد و نمای قدیمی را در XAML نگه می‌دارد تا ViewModelها و handlerهای upstream حفظ شوند.

## اصلاح اتصال ViewModel پس از آزمون دستگاه

آزمون اولیه روی ویندوز نشان داد که رابط ثابت نمایش داده می‌شد، اما واردکردن پروفایل و کنترل‌های System Proxy و TUN واکنشی نداشتند. علت کدیِ مشخص این بود که داشبورد تزریق‌شده Bindingهای معمول WPF داشت، درحالی‌که پنجرهٔ میزبان از `ReactiveWindow<MainWindowViewModel>` استفاده می‌کند. ریشهٔ داشبورد اکنون `DataContext` را صریحاً از `BindingRoot` پنجره می‌گیرد تا تمام Bindingهای فرزند به `MainWindowViewModel` واقعی و ViewModelهای تو‌در‌توی آن برسند.

این اصلاح، علت مشترک محتمل برای هر سه علامت را برطرف می‌کند؛ بااین‌حال، موفقیت Build به‌تنهایی اثبات عملکرد روی ویندوز واقعی نیست. این نسخه باید مجدداً روی همان دستگاه اجرا و مسیرهای افزودن سرور، System Proxy و TUN جداگانه تأیید شوند.

## پایش دوره‌ای سلامت لینک‌ها

پایش لینک‌ها از اتصال ترافیک Windows جداست و به روشن‌بودن System Proxy یا TUN نیاز ندارد.

- کنترل Start/Stop در داشبورد، با فاصله‌های ۲۰/۳۰/۶۰/۱۲۰ ثانیه و آستانهٔ ۱ تا ۳ شکست متوالی (پیش‌فرض ۲۰ ثانیه و ۲ شکست).
- هر دور از `ProfilesViewModel.ServerSpeedtest(ESpeedActionType.FastRealping)` استفاده می‌کند. upstream برای هر پروفایل Core موقت Xray یا sing-box را راه‌اندازی و Real Ping را از SOCKS محلی همان Core اجرا می‌کند.
- callback تست‌ها تا تکمیل به‌روزرسانی UI await می‌شود تا حلقهٔ مانیتور پیش از دریافت نتایج یک دور، دور بعدی را شروع نکند.
- نتایج تست دستی به مانیتور ارسال نمی‌شوند. فهرست پروفایل‌ها برای هر دور snapshot می‌شود تا پروفایل‌های تازه‌اضافه‌شده با نتیجهٔ خالی به‌عنوان قطع‌شده شناخته نشوند.
- نتیجه در فیلدهای UI-only مدل ردیف پروفایل (`VwHealth`, `VwLastChecked`) نمایش داده می‌شود: Connected، Unstable، Disconnected یا Not testable.
- اعلان tray فقط هنگام انتقال به Disconnected و هنگام بازیابی اتصال صادر می‌شود؛ خطاهای متوالی جلوی هشدار کاذب یک timeout را می‌گیرند.
- فاصله و آستانه در `%LOCALAPPDATA%\V2rayWatch\monitor-settings.json` نگهداری می‌شوند.
- بستن پنجرهٔ اصلی v2rayN آن را به System Tray مخفی می‌کند، بنابراین مانیتور تا وقتی برنامه واقعاً Exit نشده در حال اجرا می‌ماند. بعد از خروج کامل یا reboot، مانیتور خودکار شروع نمی‌شود.
- فاصلهٔ انتخاب‌شده حداقل فاصلهٔ بین شروع دورهاست؛ اگر جمع زمان تست/timeout از ۲۰ ثانیه بیشتر شود، دور بعد بعد از پایان دور فعلی شروع خواهد شد. دقت real-time تضمین نمی‌شود.

این پیاده‌سازی از URL Real Ping تنظیم‌شده در v2rayN استفاده می‌کند؛ fallback دوم مثل Android در این نسخه اضافه نشده است.
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

CI آخرین commit پایش با موفقیت کامل شده است: Build نمونهٔ WPF و Parser smoke testها، Build و Publish کلاینت یکپارچه، افزودن Coreهای Xray/sing-box، بسته‌بندی و آپلود Artifact همگی سبز هستند. Build موفق، خطای کامپایل را رد می‌کند اما جای تست اجرای برنامه روی Windows 10/11 واقعی، بررسی rendering فونت فارسی، ورود کانفیگ، تغییر وضعیت لینک، اعلان tray، System Proxy و TUN را نمی‌گیرد.

## پیش‌نمایش قابل دریافت

آخرین بستهٔ یکپارچهٔ Portable از commit توسعهٔ پایش ساخته شده است:
https://github.com/hatinati2/V2rayWatch/actions/runs/41149489875#artifacts

Artifact: `V2rayWatch-Windows-integrated-preview` (حدود ۱۶۲ MiB)؛ SHA-256: `cbebb7e3ffd5eee5b6fb039060b79b18b90105315632792aba5aa90acf899e1c`.

این Artifact پیش‌نمایش شاخهٔ توسعه است و Release پایدار نیست؛ فایل موقت GitHub Actions است و در ۲۹ ژانویهٔ ۲۰۲۷ منقضی می‌شود.

## محدودیت‌ها و گام بعدی

- هنوز هیچ Release جدیدی از این شاخه منتشر نشده است.
- کنترل‌های پراکسی/TUN اکنون از فرمان‌های upstream استفاده می‌کنند؛ اجرای واقعی آن‌ها نیازمند تست روی دستگاه ویندوزی است.
- نمودار ترافیک با داده‌های واقعی ProxyUp/ProxyDown اضافه شده است؛ نمایش آن وابسته به فعال بودن آمار/سرعت لحظه‌ای در تنظیمات upstream است.
- ظاهر و ترجمهٔ تمام پیام‌ها/پنجره‌های ثانویه هنوز یکدست‌سازی نشده است.
- بسته‌بندی قابل توزیع باید همراه فایل‌های core و مجوزها انجام شود و سپس روی ویندوز واقعی آزموده شود.
