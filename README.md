# V2rayWatch

V2rayWatch is a cross-platform tool for importing proxy profiles and monitoring each profile through its own Xray outbound connection. It is a link-health monitor, not a replacement for v2rayNG/v2rayN and not a full-device VPN client.

## Latest release — V2rayWatch 0.3.2

Android and Windows are published together under one shared release tag.

- **Android 0.3.2:** signed sideload APK with 50 recent automatic checks, 24-hour and 7-day local connection history, compact refreshed UI, screen-off monitoring support, encrypted profile storage, QR import, and encrypted portable backup.
  - [Download Android APK](https://github.com/hatinati2/V2rayWatch/releases/download/v0.3.2/V2rayWatch-Android-0.3.2-release.apk)
  - [Android SHA-256 manifest](https://github.com/hatinati2/V2rayWatch/releases/download/v0.3.2/V2rayWatch-Android-0.3.2-SHA256SUMS.txt)
- **Windows 0.3.2:** standalone Windows 10/11 x64 Portable package with 50 recent automatic checks, 24-hour and 7-day local connection history, profile monitoring, encrypted cross-platform backup/restore, QR display, and QR image import.
  - [Download Windows ZIP](https://github.com/hatinati2/V2rayWatch/releases/download/v0.3.2/V2rayWatch-Windows-0.3.2-win-x64.zip)
  - [Windows SHA-256 manifest](https://github.com/hatinati2/V2rayWatch/releases/download/v0.3.2/V2rayWatch-Windows-0.3.2-SHA256SUMS.txt)

See the [combined V2rayWatch 0.3.2 release](https://github.com/hatinati2/V2rayWatch/releases/tag/v0.3.2) for both packages and the combined checksum manifest.

## Shared encrypted backup

Android and Windows use the same authenticated backup format (`V2VWBAK1`). A `.v2vwbackup` file can be exported on either platform and restored on the other with the same passphrase. The payload contains profiles, monitoring settings, notification preferences, and uptime history. Existing profiles are preserved during restore and duplicate links are skipped.

Use a unique passphrase of at least 12 characters and keep it separately from the backup file. Do not share backup files publicly: they contain proxy credentials.

The shared payload does not include Windows-only subscription source URLs, application theme/language choices, or Windows-specific UI settings.

## QR code support

- Android scans proxy QR codes with the device camera.
- Windows can generate and save a QR image for a selected profile and import a link by decoding a QR code from an image file.
- QR codes contain the complete proxy link, including credentials. Share them privately.
- Live webcam scanning is not included in the Windows app yet.

## Android application

The Android app imports proxy profiles and checks each one through a real Xray Core outbound connection. It can run continuous monitoring through a foreground service and send separate disconnect/recovery notifications.

Supported inputs include proxy links, HTTPS subscriptions, shared text, QR codes, and Xray JSON. The app supports VLESS, VMess, Trojan, Shadowsocks, and Hysteria2 links.

See the [Persian Android documentation](AndroidApp/README.fa.md), [English Android documentation](AndroidApp/README.en.md), and [Android release signing guide](AndroidApp/ANDROID_RELEASE_SIGNING.md). The official APK is a sideload release, not a Google Play build.

## Windows application

The Windows app is a standalone WPF/.NET 10 Portable application. It starts a temporary Xray Core process for each profile test and makes an HTTPS request through that profile's own outbound. It does **not** modify Windows System Proxy, TUN, or system routing.

Monitoring runs while the app remains open, including in the System Tray. It does not yet run as a Windows Service after the application exits.

See the [Persian Windows documentation](Windows/README-standalone.fa.md) or [English Windows documentation](Windows/README-standalone.en.md).

## Build and releases

- [All GitHub Releases](https://github.com/hatinati2/V2rayWatch/releases)
- [GitHub Actions](https://github.com/hatinati2/V2rayWatch/actions)
- [Android source](AndroidApp/)
- [Windows source](Windows/DesktopPrototype/)
