# Third-party notices

LinkWatch uses the following unmodified upstream Android binary at build time:

- **AndroidLibXrayLite / `libv2ray.aar`**
  - Upstream: https://github.com/2dust/AndroidLibXrayLite
  - Release: `v26.10.29`
  - Asset: `libv2ray.aar`
  - SHA-256: `64fc529302dce728bde859f45d87a64f7289a5fbcca8ef962aae3d94c6e3fa82`
  - License: GNU Lesser General Public License v3.0 (see `licenses/LGPL-3.0.txt`)

- **Xray-core** (linked through AndroidLibXrayLite)
  - Upstream: https://github.com/XTLS/Xray-core
  - License: Mozilla Public License 2.0 (see `licenses/MPL-2.0.txt`)

The application calls the exported `Libv2ray.measureOutboundDelay` function. No v2rayNG application source files are copied into this project. v2rayNG is referenced only as documentation for the public Real Delay behavior/API.

When redistributing an APK that includes the AAR, retain these notices and comply with the corresponding upstream license terms, including applicable source-code and relinking requirements. This notice is not legal advice.
