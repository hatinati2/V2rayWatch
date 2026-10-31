# V2rayWatch for Windows — standalone link tester and monitor

This branch builds an independent Windows version of V2rayWatch. Its purpose is to import proxy profiles and test each link independently, not to manage a system VPN or route all Windows traffic. It does not use the v2rayN application UI, settings, view models, or client orchestration.

## Preview features

- Import one link, multiple links, clipboard text, text/config files, and Xray JSON
- Import HTTPS subscriptions with a real Fetch and add links action
- Decode plain-text or Base64 subscription responses and add the extracted profiles
- Save subscription sources and update one or all sources later
- Preserve manually imported profiles when a subscription is updated; only profiles owned by that source are synchronized
- Test one profile or all profiles using a temporary Xray Core process and an HTTPS request through that profile's own outbound
- Scheduled monitoring, 20/30/60/120-second intervals, a 1–5 consecutive-failure threshold, and configurable timeouts
- Per-profile status, latest delay, last-check time, and a 20-check automatic uptime strip
- Tray notifications on disconnect and recovery
- Persian and English UI
- Profile/subscription data stored at LocalAppData/V2rayWatch/data.v2vw, protected with Windows DPAPI

## How link testing works

For each profile, V2rayWatch starts a temporary xray.exe process using that profile's JSON. It creates only a local SOCKS listener on 127.0.0.1 for the duration of that test; it does not modify Windows System Proxy, VPN/TUN, or machine network settings. It then sends an HTTPS request to the configured test URL through that local SOCKS listener and accepts HTTP 200 or 204. The fallback URL is tried when the primary endpoint fails. The temporary core process and test files are removed afterward.

This is not a port ping or a TCP-only probe: it checks an HTTPS request routed through the profile's own outbound. Results still depend on the test URLs being reachable and the bundled Xray version supporting the profile's options.

## Subscriptions

The Add Subscription dialog actually downloads and parses the URL; HTTPS only, a 2 MiB response cap, at most five HTTPS redirects, and bounded timeouts are enforced. If the response is empty or invalid, the app reports the error and does not add an empty source.

On update:
- The complete subscription response is fetched and parsed before changes are applied.
- Duplicate links are skipped.
- New entries are assigned to that subscription source.
- Previously saved entries missing from the latest response are kept; the user can remove stale profiles manually.
- Manually imported profiles and profiles owned by other sources are preserved as well.

## Current limitations

- The test engine in this preview is Xray. Exact protocol/transport support depends on the bundled Xray version. sing-box-based testing is not implemented in this preview.
- Some full Xray JSON configurations or advanced transports may need additional compatibility work.
- Monitoring runs while the app remains open (including in the system tray). Automatic monitoring as a Windows Service or after a full process exit is not implemented yet.
- A monitoring round may take longer than the selected interval depending on profile count and probe timeouts; rounds do not overlap.
- Portable encrypted backup/restore is not implemented yet. The current data file is protected for the same Windows user on the same device.
- This preview still requires live testing on Windows 10/11. Passing CI does not replace testing real profiles and real subscription providers.

## Build from source

Requires Windows and the .NET 10 SDK:

    dotnet build Windows/DesktopPrototype/V2rayWatch.DesktopPrototype.csproj -c Release
    dotnet run --project Windows/DesktopPrototype/V2rayWatch.DesktopPrototype.csproj

Parser and generated-Xray-config smoke tests:

    dotnet run --project Windows/DesktopPrototype.Tests/V2rayWatch.DesktopPrototype.Tests.csproj -c Release

The portable GitHub Actions artifact includes V2rayWatch.exe and core/xray.exe. Keep the Xray license and third-party notices with the package.