# V2rayWatch 0.2.3

## Windows 0.2.3
- Integrated Windows client based on v2rayN 7.25.6 with V2rayWatch branding.
- Independent per-profile real outbound monitoring; does not require System Proxy or TUN.
- Custom check interval from 1 to 99,999 seconds and configurable consecutive-failure threshold.
- Per-profile 20-check uptime history (green = successful, red = failed, gray = no data), persisted locally across app restarts.
- Down/recovery System Tray notifications.
- Profile import/deletion, System Proxy, TUN, routing, Xray and sing-box cores.
- Improved language switching, readability controls, and V2rayWatch Taskbar/System Tray identity.
- The project owner confirmed this build works on their Windows device.

## Android 0.2.2 preview
- Android monitoring app with persistent 20-check uptime history under each profile.
- The APK is debug-signed and intended for sideload testing; it is not a full Android VPN/TUN client and is not a Google Play release.

## Licensing
- Windows source and dashboard/rebrand scripts are included in the Windows GPL-3.0 source archive.
- Android source and third-party licenses remain in AndroidApp/.
- SHA-256 checksum files are attached.

## Notes
- Windows package is portable and unsigned; Windows SmartScreen may show an unknown publisher warning.
- Continuous monitoring runs while the app process remains alive (including System Tray); start it again after a full exit.