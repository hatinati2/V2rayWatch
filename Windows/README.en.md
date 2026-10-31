# V2rayWatch for Windows

**Desktop version 0.1.0 — Windows x64**

This is a Windows desktop client based on the upstream open-source v2rayN project, version 7.25.6, with V2rayWatch branding. It is not an independently reimplemented proxy engine; the upstream origin and GPL license are disclosed in the package.

## Persian and English

Both Persian and English resources are included. On a fresh install, Persian is selected when the Windows UI culture is Persian; otherwise English is the default. The dashboard language button saves the preference and automatically restarts the app in the selected language.

## Main features

- Import single links, multiple links, and subscriptions
- QR import from the screen or an image, plus clipboard import
- Proxy protocols and cores supported by the included Xray and sing-box engines
- XHTTP through Xray Core and Hysteria2 through sing-box
- Server management, latency testing, and active server selection
- Windows System Proxy
- TUN mode for system-wide routing (requires Administrator privileges and the included Wintun driver)
- Routing rules, DNS, and advanced settings inherited from upstream v2rayN
- System tray integration

Exact support depends on the selected core and the fields in each profile. Private or vendor-specific options may still require validation.

## Scheduled link monitoring in the development preview

The development branch adds a **Start monitoring** control. It uses v2rayN's real per-profile Real Ping path to test every testable profile independently of System Proxy and TUN. Enter any monitoring interval from 1 to 99,999 seconds (Persian, Arabic, or Latin digits are accepted); the failure threshold is 1–3 consecutive failures (default: 20 seconds and 2 failures). A delete-selected-profile button uses the upstream v2rayN removal command and confirmation flow. The profile table shows Connected, Unstable, Disconnected, or Not testable. Under each profile, a 20-cell history strip shows successful checks in green, failed checks in red, and unknown/not-testable results in gray. History is saved locally and remains available after restarting the app; cells run oldest-to-newest from left to right. Tray notifications are sent when a profile transitions to disconnected and when it recovers.

Manual Real Ping results are kept separate from monitor rounds. New profiles added during a round are not treated as failures until the next round. Monitoring continues while the app remains running in the system tray, but stops when the process exits; start it again after relaunch. A round can take longer than the selected interval when probes time out, and rounds are never overlapped.

This is available in the development preview only. Stable public release v0.1.0 has not been replaced by this feature branch.

## Install and run

1. Download V2rayWatch-Windows-x64.zip from GitHub Releases.
2. Extract it to a folder.
3. Run V2rayWatch.exe.
4. For ordinary proxy mode, enable System Proxy from the app menu.
5. For TUN mode, run as Administrator and enable TUN.

This is a portable ZIP package; no separate installer is included.

## Important

This is an experimental Windows x64 build based on v2rayN 7.25.6. It has been compiled on a Windows runner, but it still needs real-device validation on Windows 10/11, including System Proxy, TUN, and representative profiles. It is not Authenticode-signed, so Windows SmartScreen may display an unknown-publisher warning.

## License and source

The Windows application is derived from v2rayN and is distributed under GNU GPL-3.0. The corresponding modified source is supplied as the V2rayWatch-Windows-source-GPL-3.0.zip release asset. Upstream and bundled core notices are included in THIRD_PARTY_NOTICES.md and the licenses folder.
