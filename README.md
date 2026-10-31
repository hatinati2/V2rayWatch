# V2rayWatch

V2rayWatch is a multi-platform project with separate Android and Windows applications.

## Latest releases

- **Windows 0.2.3:** portable desktop client based on v2rayN 7.25.6, with an integrated V2rayWatch dashboard, independent per-profile monitoring, a 20-check uptime history, System Proxy, and TUN support. See the [Windows / multi-platform release](https://github.com/hatinati2/V2rayWatch/releases/tag/v0.2.3).
- **Android 0.2.3:** signed sideload APK with persistent release signing, encrypted backup and restore, resilient per-profile monitoring, independent disconnect and recovery alerts, custom alert sounds and vibration patterns, and a matching launcher icon. Download the [official Android APK](https://github.com/hatinati2/V2rayWatch/releases/download/android-v0.2.3/V2rayWatch-Android-0.2.3-release.apk) and its [SHA-256 checksum](https://github.com/hatinati2/V2rayWatch/releases/download/android-v0.2.3/V2rayWatch-Android-0.2.3-SHA256SUMS.txt), or view the [Android 0.2.3 release page](https://github.com/hatinati2/V2rayWatch/releases/tag/android-v0.2.3).

The Android component has its own `android-v0.2.3` tag and GitHub Release. It is intentionally separate from the existing Windows / multi-platform `v0.2.3` release; neither release replaces the other.

## Android application

The Android app imports proxy profiles and checks each profile through a real Xray Core outbound connection. It is a monitoring tool, not a replacement for v2rayNG and not a full-device VPN/TUN client.

The Android source on `main` includes:

- Persian and English interfaces, with profile import from links, subscriptions, QR codes, shared text, and Xray JSON.
- Real delay checks and continuous per-profile monitoring.
- A 20-result uptime history for each profile.
- Portable encrypted backup and restore.
- Independent disconnect and recovery notification controls, custom sounds, and separate vibration patterns.
- A launcher icon aligned with the Windows artwork.

See the [Android documentation](AndroidApp/README.fa.md) in Persian or the [English documentation](AndroidApp/README.en.md). The official Android APK is a sideload release, not a Google Play build.

## Windows application

The Windows client is based on [2dust/v2rayN 7.25.6](https://github.com/2dust/v2rayN) and includes the V2rayWatch dashboard and per-profile monitoring. Windows source modifications and branding/dashboard scripts are distributed with the Windows release under the applicable GPL-3.0 obligations.

## Uptime history

Both platform UIs show a 20-cell strip under each profile: green for successful automatic checks, red for failed checks, and gray for no recorded result. The newest 20 automatic monitoring results persist across app restarts. Manual one-off tests do not change the history.

## Android release signing

Android release APKs use one persistent signing key so that future signed releases can update an existing installation in place. Never commit the keystore or its passwords to source control. Read the [Android release signing and upgrade guide](AndroidApp/ANDROID_RELEASE_SIGNING.md) before preparing another Android release.

## Build and releases

- [All GitHub Releases](https://github.com/hatinati2/V2rayWatch/releases)
- [GitHub Actions](https://github.com/hatinati2/V2rayWatch/actions)
