# V2rayWatch — Real proxy profile monitoring

**Experimental version: 0.2.2**

V2rayWatch is an independent Android app for importing proxy profiles and checking each profile through a real Xray Core outbound connection. It is a monitoring tool, not a replacement for v2rayNG or a full-device VPN client.

## Languages

The app includes Persian and English interfaces. Tap **EN** or **فارسی** in the top app bar to switch languages. The selected language is saved on the device and remains selected after restarting the app. The layout direction changes automatically between RTL (Persian) and LTR (English). Monitoring notifications follow the selected language as well.

## How delay testing works

For each profile, the app starts a temporary Xray Core instance using that profile's JSON and sends an HTTPS request through its outbound to the configured test URL. It records the response time and accepts HTTP 200 or 204. This is not a port ping or a bare TCP connect test.

The app uses `CoreController.measureDelay(url)` from AndroidLibXrayLite. The default primary URL is `https://www.google.com/generate_204`; the fallback is `https://www.gstatic.com/generate_204`. Both URLs can be changed in Monitoring Settings.

## Features in this build

- Persian and English UI with persisted language selection
- Import one profile or multiple links on separate lines
- Import subscriptions over HTTPS
- Receive shared text links from other Android apps
- Scan proxy links and config QR codes
- Parse `vless://`, `vmess://`, `trojan://`, `ss://`, `hysteria2://`, and `hy2://`
- Common Xray transports: TCP, WebSocket, gRPC, HTTPUpgrade, and XHTTP with common path/host/mode/extra fields
- Import full Xray JSON containing `outbounds`
- Encrypted local profile storage using AES-GCM and Android Keystore
- Manual real-delay tests and continuous foreground monitoring
- A 20-result uptime strip under each profile: green for successful checks, red for failed checks, and gray for empty history; oldest on the left and newest on the right
- History is persisted with each encrypted profile (AES-GCM); it survives app restarts and is updated only by scheduled monitoring checks, not manual tests
- Configurable check intervals: 20, 30, 60, or 120 seconds
- Alerts when a profile goes down or recovers

## Uptime history

Each profile retains its 20 most recent automatic monitoring results. When upgrading from 0.2.1, existing profiles and settings are preserved; the new history starts filling from monitoring rounds performed after the upgrade.

## Current limitations

- This is an experimental MVP. A successful CI build does not prove that every config works on every device; real-device tests are still required.
- TUIC, WireGuard, SSH, legacy `hysteria://`, and Shadowsocks plugins are not supported in this build. Use `hysteria2://` or `hy2://` for Hysteria 2.
- Some advanced transport options may require additional parser work.
- At most two temporary Xray Core instances are tested concurrently.
- Xray Core is started temporarily for each profile test and stopped afterward. This app does not yet create a full-device VPN/TUN tunnel.
- Continuous monitoring uses an Android foreground service and a persistent notification. Battery optimizations and device-vendor restrictions may delay checks.
- If another VPN is active, Android may route V2rayWatch's own test connection through that VPN.
- Subscriptions are fetched over HTTPS only.

## Build

The GitHub Actions workflow builds a debug APK with JDK 17, Android SDK 35, and Gradle 8.11.1. The Xray AAR is downloaded from its pinned upstream release and verified with SHA-256 before compilation.

For local development, open the `LinkWatchAndroid` directory in Android Studio with JDK 17 and Android SDK 35, or run `gradle assembleDebug` from the project root with a compatible Gradle installation.

## Privacy and licenses

Profiles are stored in the app's private storage and credentials are encrypted with AES-GCM using Android Keystore. The app has no account system or telemetry. The app's source is MIT-licensed. The embedded Xray wrapper/core use their respective LGPL-3.0 and MPL-2.0 licenses; notices are included in `THIRD_PARTY_NOTICES.md` and `licenses/`.
