# V2rayWatch

V2rayWatch is a multi-platform project with separate Android and Windows applications.

## Android

The Android app is a bilingual Persian/English connection-monitoring tool. It imports supported links, scans QR codes, and measures real outbound delay using Xray Core. It is not yet a full Android VPN client.

Current Android source package: `V2rayWatch-project-0.2.1.zip`.

## Windows

The Windows desktop client is based on upstream [v2rayN](https://github.com/2dust/v2rayN) 7.25.6, with V2rayWatch branding and Persian/English UI. The current development preview combines two separate modes: (1) scheduled, per-profile real outbound monitoring with configurable intervals, failure thresholds, and down/recovery tray alerts; and (2) actual Windows traffic routing through System Proxy or TUN. Link monitoring does not require System Proxy or TUN. The stable public v0.1.0 release has not been replaced by this preview.

The Windows component is derived from GPL-3.0 software. See [Windows/THIRD_PARTY_NOTICES.md](Windows/THIRD_PARTY_NOTICES.md). The corresponding modified source is supplied with the Windows release.

The latest development preview is built by [GitHub Actions](https://github.com/hatinati2/V2rayWatch/actions); it is experimental and still needs real Windows 10/11 validation.

## Build and releases

- [GitHub Actions](https://github.com/hatinati2/V2rayWatch/actions)
- [GitHub Releases](https://github.com/hatinati2/V2rayWatch/releases)

Windows builds are experimental until validated on a real Windows 10/11 device. The Android debug APK is also a preview build, not a production release.
