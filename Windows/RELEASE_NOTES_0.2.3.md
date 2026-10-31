# V2rayWatch 0.2.3

## Windows 0.2.3 — owner-tested portable release

- Windows x64 client based on v2rayN 7.25.6 with the integrated V2rayWatch dashboard.
- Independent real outbound link monitoring; System Proxy and TUN are optional and are not required for link checks.
- Monitoring interval is user-configurable from 1 to 99,999 seconds.
- Configurable consecutive-failure alert threshold and down/recovery System Tray notifications.
- A persistent 20-check uptime strip under every profile: green = successful, red = failed, gray = no recorded result.
- History survives restarts and only automatic monitoring rounds append entries.
- Profile import and deletion, subscriptions, QR import, System Proxy, TUN, routing, Xray and sing-box cores.
- Improved language switching, font/text appearance controls, and independent V2rayWatch Taskbar/System Tray identity.
- The project owner tested this Windows version on a real device and confirmed that it works.

## Android 0.2.2 — debug preview

- Bilingual proxy-link monitoring app with a persistent per-profile 20-check uptime strip.
- The Android APK is debug-signed for sideload testing, not a Google Play release or full-device VPN/TUN client.
- Existing Android profiles/settings from 0.2.1 are retained when updating in place with a compatible signing certificate.

## Download assets

- V2rayWatch-Windows-x64.zip: portable Windows x64 application and core binaries.
- v2rayN-windows-64.zip: upstream-compatible package filename.
- V2rayWatch-Windows-source-GPL-3.0.zip: corresponding modified upstream source plus V2rayWatch rebrand/dashboard scripts.
- V2rayWatch-Android-0.2.2-debug-preview.apk: Android monitoring preview.
- SHA256SUMS and APK .sha256 files verify downloaded binaries.

## Notes

- Windows binaries are not Authenticode-signed; SmartScreen may show an unknown-publisher warning.
- Monitoring continues while the application is running (including in System Tray). Start monitoring again after fully exiting and relaunching.
- Android foreground monitoring is subject to Android battery policies and device-vendor restrictions.

## Licensing

The Windows component is based on v2rayN 7.25.6 and is distributed under GPL-3.0. The corresponding source archive and third-party notices are attached. Android source and licensing notices are in AndroidApp/.