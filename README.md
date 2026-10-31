# V2rayWatch

V2rayWatch is a multi-platform project with separate Android and Windows applications.

## Android 0.2.2

The Android application is a bilingual Persian/English proxy-link monitoring tool. It imports supported links and subscriptions, scans QR codes, and tests each profile through its own Xray outbound. It is not a full-device VPN/TUN client.

Each profile now displays a **20-check uptime history strip** beneath its card: green for successful checks, red for failed checks, and gray for slots with no recorded result. The newest 20 automatic monitoring results are stored with the encrypted profile data and survive app restarts. Manual one-off tests do not alter this history.

Android source is maintained in [`AndroidApp/`](AndroidApp/). The older `V2rayWatch-project-0.2.1.zip` remains in the repository as a historical source snapshot.

## Windows

The Windows desktop client is based on upstream [v2rayN](https://github.com/2dust/v2rayN) 7.25.6, with V2rayWatch branding and Persian/English UI. It is intended to include upstream Xray and sing-box engines and corresponding profile management, QR import, subscriptions, routing, Windows System Proxy, and TUN features.

The Windows component is derived from GPL-3.0 software. See [Windows/THIRD_PARTY_NOTICES.md](Windows/THIRD_PARTY_NOTICES.md). The corresponding modified source is supplied with Windows releases.

## Build and releases

- [GitHub Actions](https://github.com/hatinati2/V2rayWatch/actions)
- [GitHub Releases](https://github.com/hatinati2/V2rayWatch/releases)

Android 0.2.2 is an experimental debug-signed sideload preview and is not a Google Play release. Windows builds are experimental until validated on a real Windows 10/11 device.