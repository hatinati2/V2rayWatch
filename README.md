# V2rayWatch

V2rayWatch is a multi-platform project with separate Android and Windows applications.

## Latest release — V2rayWatch 0.2.3

The current multi-platform release includes:
- **Windows 0.2.3:** portable desktop client based on v2rayN 7.25.6, with an integrated V2rayWatch dashboard, independent per-profile monitoring, 20-check uptime history, System Proxy and TUN support.
- **Android 0.2.2:** bilingual proxy-link monitoring app with the same 20-check per-profile uptime history. The APK is a debug-signed sideload preview, not a full-device VPN/TUN client or Google Play build.

## Current main branch — Android roadmap integrated

The Android roadmap stages 1–3 are now merged into `main`. The development source includes portable encrypted backup/restore, more resilient per-profile monitoring, independent disconnect/recovery alert controls with custom sounds and vibration patterns, and a launcher icon matching the Windows artwork. The merged code passed GitHub Actions unit tests and debug APK builds, and the project owner verified the final development APK on a physical Android device. These changes are in the source branch but are not yet part of a new signed Android release. See [Android release signing instructions](AndroidApp/ANDROID_RELEASE_SIGNING.md) for the remaining permanent signing setup.

## Uptime history

Both platform UIs show a 20-cell strip under each profile: green for successful automatic checks, red for failed checks, gray for no recorded result. The newest 20 automatic monitoring results persist across app restarts. Manual one-off tests do not change the history.

## Source and licensing

- Android source: [AndroidApp/](AndroidApp/) (app component version 0.2.2).
- Windows source modifications and branding/dashboard scripts are distributed with the Windows release under the applicable GPL-3.0 obligations.
- Windows upstream: [2dust/v2rayN 7.25.6](https://github.com/2dust/v2rayN).

## Build and releases

- [GitHub Releases](https://github.com/hatinati2/V2rayWatch/releases)
- [GitHub Actions](https://github.com/hatinati2/V2rayWatch/actions)