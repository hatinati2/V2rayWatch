# V2rayWatch Android 0.2.2 release

- Version: 0.2.2 (versionCode 4).
- Adds a persistent 20-result uptime history strip under every proxy profile.
- Green: successful automatic outbound check; red: failed automatic check; gray: no recorded result.
- The newest 20 results are stored with each encrypted profile. Manual one-off tests do not affect the history.
- Existing profiles and settings from 0.2.1 are retained; the history begins filling after the upgrade when continuous monitoring runs.

This is an experimental debug-signed sideload preview, not a Google Play release. Test it on a physical device and confirm background monitoring under the device's battery restrictions.