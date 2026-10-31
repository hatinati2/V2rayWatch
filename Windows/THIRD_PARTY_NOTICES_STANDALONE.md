# Third-party notices — V2rayWatch standalone Windows Link Monitor

This preview is an independent WPF application written for V2rayWatch. It does not include the v2rayN application or its UI/runtime orchestration.

- The bundled Xray Core binary is downloaded from the official XTLS/Xray-core GitHub release: https://github.com/XTLS/Xray-core/releases/tag/v26.10.31
- Pinned official Xray release: v26.10.31 (Windows x64 archive SHA-256 is verified against the adjacent .dgst file during CI).
- The Xray core bundle is distributed under the upstream license included as core/CORE-LICENSE.txt.
- Xray Core is an upstream third-party component of Project X. V2rayWatch is not affiliated with or endorsed by the XTLS/Xray-core authors.
- The WPF application code in this standalone preview is project code; the application does not embed v2rayN's WPF application, its ViewModels, or its System Proxy/TUN implementation.

Retain this notice and the upstream core license with any redistribution of the preview package.