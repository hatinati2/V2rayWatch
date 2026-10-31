# Android release signing and safe upgrades

## Purpose

Android release APKs must use one persistent upload key. Never generate a new key for each release, commit a keystore to Git, or share the keystore/passwords in chat.

## Important migration note for 0.2.2 previews

The 0.2.2 APKs published from GitHub Actions used the default debug signing configuration. The corresponding runner-generated debug keystore was not saved as a release asset or repository secret. A new persistent release key therefore cannot be assumed to match an already-installed 0.2.2 preview. To avoid losing saved profiles, first create an encrypted V2rayWatch backup from the installed app, then install the first permanently signed release and restore that backup. Do not uninstall the old app before verifying the backup file and passphrase.

## Generate the upload keystore once

Run this locally on a trusted computer with JDK 17 or newer. Keep the generated `.jks` file in an encrypted offline backup. Losing this key prevents future in-place updates signed with it.

```bash
keytool -genkeypair -v -keystore v2raywatch-upload.jks -keyalg RSA -keysize 4096 -validity 10000 -alias v2raywatch-upload
```

Choose strong, distinct store and key passwords. Do not send them to ChatGPT or commit the keystore.

## Configure GitHub Actions secrets

Open the repository Settings → Secrets and variables → Actions and add these repository secrets:

- `ANDROID_RELEASE_KEYSTORE_BASE64`: base64 encoding of the complete `.jks` file.
- `ANDROID_SIGNING_STORE_PASSWORD`: keystore password.
- `ANDROID_SIGNING_KEY_ALIAS`: alias entered during key generation.
- `ANDROID_SIGNING_KEY_PASSWORD`: key password.

On Windows PowerShell, copy the base64 value without a newline:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes('.\v2raywatch-upload.jks')) | Set-Clipboard
```

On macOS/Linux:

```bash
base64 < v2raywatch-upload.jks | tr -d '\n'
```

The workflow only builds a signed candidate when manually dispatched with `build_signed_release` enabled. It does not publish a GitHub Release. The signed artifact should be checked on a device before any future release/tag is created.

## Encrypted profile backup

Use the in-app Backup button to create a `.v2vwbackup` file. The file uses PBKDF2-HMAC-SHA256 to derive an AES-256-GCM key from a passphrase of at least 12 characters. Keep the passphrase separately from the backup file; neither can recover the other.

Restore merges profiles without deleting existing ones, skips duplicates, restores monitoring settings, preserves uptime history, and never starts monitoring automatically. Current connection states are reset to Unknown until checked again.

## Release process

1. Create and verify an encrypted backup from the currently installed Android app.
2. Generate the upload keystore once and configure the four GitHub Actions secrets.
3. Run the Android workflow manually with signed release candidate enabled.
4. Install the candidate on a test device (export/restore the backup if replacing 0.2.2), verify its signer, profile migration, backup restore, monitoring, and app restart.
5. Keep this branch separate until tests pass. Do not move the existing `v0.2.3` tag or alter its assets.
