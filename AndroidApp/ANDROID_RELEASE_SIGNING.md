# Android release signing and safe upgrades

## Current release

The official **V2rayWatch Android 0.2.3** release has been published successfully:

- Release tag: `android-v0.2.3`
- Android version: `versionName 0.2.3`, `versionCode 5`
- [Official signed APK](https://github.com/hatinati2/V2rayWatch/releases/download/android-v0.2.3/V2rayWatch-Android-0.2.3-release.apk)
- [SHA-256 checksum file](https://github.com/hatinati2/V2rayWatch/releases/download/android-v0.2.3/V2rayWatch-Android-0.2.3-SHA256SUMS.txt)
- [GitHub Release page](https://github.com/hatinati2/V2rayWatch/releases/tag/android-v0.2.3)

The Android-only tag and release are separate from the Windows / multi-platform `v0.2.3` release. Do not move, replace, or reuse either tag for the other platform.

## Signing key: keep it safe

Android release APKs must all use the same persistent signing key. Never generate a new key for each release, commit the keystore to Git, or share the keystore/passwords in chat.

Keep at least two secure copies of the `.jks` file, including an encrypted offline backup. Store the keystore password and key password separately in a trusted password manager or another secure location. Losing the keystore or its passwords can prevent future in-place updates signed with this key.

## One-time migration from old 0.2.2 preview APKs

The 0.2.2 APKs produced by the development workflow used a runner-generated GitHub Actions debug key. That key is different from the persistent release key used for the official 0.2.3 APK, so Android will reject an in-place update across those signatures.

For a device that still has one of those old previews installed:

1. Create an encrypted `.v2vwbackup` file from inside the installed app.
2. Verify that the backup file exists and that its passphrase is recorded safely and separately.
3. Uninstall the old debug-signed preview.
4. Install the official signed APK from the release link above.
5. Restore the backup and verify profiles, monitoring preferences, alert preferences, and uptime history.

Do not uninstall the old app until the backup and passphrase have both been checked. After the persistent-key release is installed, later releases signed with the same key should install in place.

## Generate the upload keystore once

This is a one-time operation. Run it locally on a trusted computer with JDK 17 or newer:

```bash
keytool -genkeypair -v -keystore v2raywatch-upload.jks -keyalg RSA -keysize 4096 -validity 10000 -alias v2raywatch-upload
```

Choose strong, distinct store and key passwords. Keep the generated file outside the repository. Do not send the file or passwords in chat.

## Configure GitHub Actions secrets

Open the repository **Settings → Secrets and variables → Actions** and configure these repository secrets:

- `ANDROID_RELEASE_KEYSTORE_BASE64`: base64 encoding of the complete `.jks` file.
- `ANDROID_SIGNING_STORE_PASSWORD`: keystore password.
- `ANDROID_SIGNING_KEY_ALIAS`: alias entered during key generation (the current release uses `v2raywatch-upload`).
- `ANDROID_SIGNING_KEY_PASSWORD`: key password.

On Windows PowerShell, copy the base64 value without a newline:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes('.\\v2raywatch-upload.jks')) | Set-Clipboard
```

On macOS/Linux:

```bash
base64 < v2raywatch-upload.jks | tr -d '\n'
```

Never expose secret values in workflow logs, commits, screenshots, or chat.

## Checklist for future Android releases

Each Android release must have a new, higher `versionCode`; never reuse a version code. The Android `versionName` must match the Android release tag exactly.

1. Update `versionName` and increment `versionCode` in `AndroidApp/app/build.gradle.kts`. For example, a future `0.2.4` release must use the tag `android-v0.2.4`.
2. Merge the reviewed source changes into `main`.
3. From **Actions → Android build**, run the workflow on `main` with **Also build a signed release APK** enabled. This produces a signed candidate artifact; it does not publish a GitHub Release.
4. Install the signed candidate as an update on a test device. Verify that the existing profiles and settings remain present, then test backup/restore, monitoring, delay checks, alerts, app restart, and any new functionality. Do not uninstall the already signed app for normal upgrades.
5. Only after the candidate passes device testing, create the Android-only tag `android-vX.Y.Z` on the approved commit in `main`, where `X.Y.Z` exactly matches `versionName`.
6. The **Android signed release** workflow runs automatically for tags matching `android-v*`. It validates the tag/version match, runs unit tests, builds and verifies the signed APK, creates a SHA-256 checksum file, and publishes a separate Android-only GitHub Release.
7. Open the published release and verify that both the APK and checksum assets are present. Keep the existing Windows / multi-platform release and tag unchanged.

Do not create the release tag before candidate validation. The release workflow uses the persistent signing secrets and fails if any required secret is missing.

## Encrypted profile backup format

The in-app `.v2vwbackup` file uses PBKDF2-HMAC-SHA256 to derive an AES-256-GCM key from a passphrase of at least 12 characters. Keep the passphrase separately from the backup file; neither can recover the other.

Restore merges profiles without deleting existing ones, skips duplicates, restores monitoring and alert settings, preserves uptime history, and never starts monitoring automatically. Current connection states reset to Unknown until checked again.
