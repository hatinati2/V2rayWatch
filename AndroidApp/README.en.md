# V2rayWatch — Real proxy profile monitoring

**Current release: 0.3.1** · [Download the signed Android APK](https://github.com/hatinati2/V2rayWatch/releases/download/v0.3.1/V2rayWatch-Android-0.3.1-release.apk) · [SHA-256 checksum](https://github.com/hatinati2/V2rayWatch/releases/download/v0.3.1/V2rayWatch-Android-0.3.1-SHA256SUMS.txt) · [Combined Android + Windows release](https://github.com/hatinati2/V2rayWatch/releases/tag/v0.3.1)

V2rayWatch is an independent Android app for importing proxy profiles and checking each profile through a real Xray Core outbound connection. It is a monitoring tool, not a replacement for v2rayNG and not a full-device VPN/TUN client. The APK is a signed sideload build; it is not distributed through Google Play.

## Languages

The app includes Persian and English interfaces. Tap **EN** or **فارسی** in the top app bar to switch languages. The selected language is saved on the device and remains selected after restarting the app. The layout direction changes automatically between RTL (Persian) and LTR (English). Monitoring notifications follow the selected language as well.

## How delay testing works

For each profile, the app starts a temporary Xray Core instance using that profile's JSON and sends an HTTPS request through its outbound to the configured test URL. It records the response time and accepts HTTP 200 or 204. This is not a port ping or a bare TCP-connect test.

The app uses `CoreController.measureDelay(url)` from AndroidLibXrayLite. The default primary URL is `https://www.google.com/generate_204`; the fallback is `https://www.gstatic.com/generate_204`. Both URLs can be changed in Monitoring Settings.

## Features

- Persian and English UI with persisted language selection.
- Import one profile or multiple links on separate lines.
- Import subscriptions over HTTPS.
- Receive shared text links from other Android apps.
- Scan proxy links and config QR codes.
- Parse `vless://`, `vmess://`, `trojan://`, `ss://`, `hysteria2://`, and `hy2://`.
- Common Xray transports: TCP, WebSocket, gRPC, HTTPUpgrade, and XHTTP with common path/host/mode/extra fields.
- Import full Xray JSON containing `outbounds`.
- Encrypted local profile storage using AES-GCM and Android Keystore.
- Manual real-delay tests and continuous foreground monitoring.
- A 20-result uptime strip under each profile: green for successful automatic checks, red for failed checks, and gray for empty history; oldest on the left and newest on the right.
- History is persisted with each encrypted profile (AES-GCM); it survives app restarts and is updated by scheduled monitoring checks, not manual tests.
- Portable encrypted backup/restore using a user passphrase. The file uses PBKDF2-HMAC-SHA256 and AES-256-GCM and includes profiles, monitoring settings, alert preferences, and uptime history.
- Configurable check intervals: 20, 30, 60, or 120 seconds.
- Configurable consecutive-failure threshold for disconnect detection.
- Independent controls for disconnect and recovery alerts; preferences are stored on-device and included in encrypted backups.
- An original approximately 5.6-second disconnect warning motif with an extended vibration pattern, plus a mellow approximately 4.9-second recovery melody with a separate, gentler vibration pattern.
- An Android launcher icon matching the Windows stopwatch artwork and color palette, including a themed monochrome layer on Android 13+.

## Uptime history

Each profile retains its 20 most recent automatic monitoring results. The history is stored with the encrypted profile data and survives app restarts. Manual one-off tests do not add entries to this strip.

## Secure backup and restore

Use the **Backup** button in the top app bar to create a `.v2vwbackup` file. The passphrase must be at least 12 characters and must be stored separately from the backup file.

Restore merges new profiles, skips duplicates, never deletes existing profiles, restores monitoring and alert settings, and preserves uptime history. Monitoring does not start automatically after restore. Live connection states reset to **Unknown** until the profiles are checked again.

## Cross-platform backup and upgrade compatibility

The `.v2vwbackup` file uses the shared Android/Windows `V2VWBAK1` format. Export on either platform and restore on the other with the same passphrase. Windows-only subscription source URLs and UI preferences are not included in the shared payload.

The official 0.3.1 APK is signed with the project's persistent Android release key. Future Android releases must use the same keystore and alias to support in-place updates.

Older 0.2.2 preview APKs produced by the development workflow used a temporary GitHub Actions debug key. Android does not allow an APK signed by a different key to update that installation in place. If you still have one of those old previews installed, create and verify an encrypted in-app backup first, uninstall the old preview, install the official signed APK, and restore the backup. Do not uninstall before confirming that the backup file and its passphrase are safe. Once the official persistent-key release is installed, later releases signed with the same key can be installed as updates.

See [ANDROID_RELEASE_SIGNING.md](ANDROID_RELEASE_SIGNING.md) for maintainer instructions. Never commit or share the signing keystore or passwords.

## Disconnect and recovery notification channels

Disconnect and recovery alerts use separate Android notification channels. Both audio motifs are bundled with the app, and each event has its own vibration pattern. On Android 8 and later, channel preferences changed by the user in system settings (for example, muting a channel or overriding its behavior) take precedence over the app's defaults.

## Current limitations

- This is an experimental MVP. A successful CI build does not prove that every config works on every device; real-device tests are still required.
- TUIC, WireGuard, SSH, legacy `hysteria://`, and Shadowsocks plugins are not supported. Use `hysteria2://` or `hy2://` for Hysteria 2.
- Some advanced transport options may require additional parser work.
- At most two temporary Xray Core instances are tested concurrently.
- Xray Core is started temporarily for each profile test and stopped afterward. This app does not yet create a full-device VPN/TUN tunnel.
- Continuous monitoring uses an Android foreground service and a persistent notification. Battery optimizations and device-vendor restrictions may delay checks.
- Monitoring is not automatically restarted after a device reboot; start it again from inside the app.
- If another VPN is active, Android may route V2rayWatch's own test connection through that VPN.
- Subscriptions are fetched over HTTPS only.
- Because the test endpoints are public URLs, a network that blocks both configured test URLs may cause a valid profile to appear unavailable. Change the URLs in Monitoring Settings when needed.

## Build

The Android GitHub Actions workflow builds and tests the project with JDK 17, Android SDK 35, and Gradle 8.11.1. The Xray AAR is downloaded from its pinned upstream release and verified with SHA-256 before compilation.

For local development, open the `AndroidApp` directory in Android Studio with JDK 17 and Android SDK 35, or run `gradle assembleDebug` from that directory with Gradle 8.11.1.

## Privacy and licenses

Profiles are stored in the app's private storage and credentials are encrypted with AES-GCM using Android Keystore. The app has no account system or telemetry. The app's source is MIT-licensed. The embedded Xray wrapper/core use their respective LGPL-3.0 and MPL-2.0 licenses; notices are included in `THIRD_PARTY_NOTICES.md` and `licenses/`.
