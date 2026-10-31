# V2rayWatch 0.3.1

## Unified Android + Windows release
- Android and Windows now share one release tag and one GitHub Release.
- Android release APK remains signed by the project's persistent upload key and uses versionCode 7.
- Windows standalone WPF/.NET 10 Portable package includes encrypted cross-platform backup/restore using the Android-compatible `V2VWBAK1` envelope.
- Windows can generate/save profile QR images and import links by decoding QR image files.
- Restores preserve existing profiles, skip duplicate raw links, and reset transient connection state.
- Windows keeps its device-local DPAPI store; portable backups use a separate passphrase.
- The Windows app does not modify System Proxy, TUN, or system routing.

## Security and compatibility notes
- Use the same 12+ character passphrase on both platforms; keep it separate from the backup file.
- Backup files and QR images contain proxy credentials; share them privately.
- The shared backup contains profiles, monitoring settings, alert preferences, and uptime history. Windows-only subscription source URLs and UI preferences are not part of the Android-compatible payload.
- Windows QR import currently reads image files; live webcam scanning is not included.
- Windows portable binaries are not publisher-code-signed.

---

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