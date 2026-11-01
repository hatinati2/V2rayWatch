#!/usr/bin/env bash
set -Eeuo pipefail

PORT=6100
ADMIN_PATH=""
DIR="$(cd -- "$(dirname -- "$0")/.." && pwd)"
PUBLISH_DIR="$DIR/publish"

usage() { echo "Usage: sudo $0 [--port PORT] [--path /PATH] [--publish-dir DIR]"; }
while (($#)); do
  case "$1" in
    --port) [[ $# -ge 2 ]] || { usage; exit 2; }; PORT="$2"; shift 2 ;;
    --path) [[ $# -ge 2 ]] || { usage; exit 2; }; ADMIN_PATH="$2"; shift 2 ;;
    --publish-dir) [[ $# -ge 2 ]] || { usage; exit 2; }; PUBLISH_DIR="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; usage; exit 2 ;;
  esac
done

[[ $EUID -eq 0 ]] || { echo "Run with sudo/root." >&2; exit 1; }
. /etc/os-release
case "${ID:-}:${VERSION_ID:-}:${VERSION_CODENAME:-}" in
  ubuntu:24.04:noble|ubuntu:26.04:resolute) ;;
  *)
    echo "Ubuntu 24.04 (noble) or Ubuntu 26.04 (resolute) required." >&2
    echo "Detected: ${PRETTY_NAME:-unknown} (${VERSION_CODENAME:-unknown})." >&2
    exit 1
    ;;
esac
[[ "$PORT" =~ ^[0-9]+$ ]] && (( PORT >= 1024 && PORT <= 65535 )) || { echo "Port must be 1024-65535." >&2; exit 2; }
command -v apt-get >/dev/null || { echo "apt-get is required." >&2; exit 1; }

# Use disk-backed storage rather than the often small /tmp tmpfs for extraction.
export TMPDIR="${TMPDIR:-/var/tmp}"
mkdir -p "$TMPDIR"

apt_source_matches="$(grep -RniE '\\b(focal|jammy)(-security|-updates|-backports)?\\b' /etc/apt/sources.list /etc/apt/sources.list.d 2>/dev/null | head -n 8 || true)"
if [[ -n "$apt_source_matches" ]]; then
  echo "WARNING: stale focal/jammy APT source entries detected on this Ubuntu release."
  echo "$apt_source_matches"
  echo "The installer will not rewrite your package sources automatically."
fi

apt-get update
DEBIAN_FRONTEND=noninteractive apt-get install -y ca-certificates curl jq unzip iproute2 openssl
mkdir -p /opt/dotnet
if [[ ! -x /opt/dotnet/dotnet ]] || ! /opt/dotnet/dotnet --list-runtimes | grep -q '^Microsoft.AspNetCore.App 10\.'; then
  curl --fail --silent --show-error --location https://dot.net/v1/dotnet-install.sh -o "$TMPDIR/v2raywatch-dotnet-install.sh"
  bash "$TMPDIR/v2raywatch-dotnet-install.sh" --runtime aspnetcore --channel 10.0 --install-dir /opt/dotnet
  rm -f "$TMPDIR/v2raywatch-dotnet-install.sh"
fi

[[ -f "$PUBLISH_DIR/V2rayWatch.Server.Web.dll" ]] || { echo "Publish first using Server/scripts/publish-linux.sh." >&2; exit 1; }
if ss -H -ltn "sport = :$PORT" | grep -q .; then echo "TCP port $PORT is already occupied." >&2; exit 1; fi
[[ -n "$ADMIN_PATH" ]] || ADMIN_PATH="/vw-$(openssl rand -hex 16)"
[[ "$ADMIN_PATH" =~ ^/[A-Za-z0-9_-]{16,64}$ ]] || { echo "PATH must be / followed by 16-64 safe characters." >&2; exit 2; }
[[ ! -e /etc/v2raywatch/server.json ]] || { echo "Existing installation found; refusing to overwrite." >&2; exit 1; }

case "$(uname -m)" in
  x86_64|amd64) XRAY_ASSET="Xray-linux-64.zip" ;;
  aarch64|arm64) XRAY_ASSET="Xray-linux-arm64-v8a.zip" ;;
  *) echo "Unsupported CPU architecture: $(uname -m). Supported: x86_64 and arm64." >&2; exit 1 ;;
esac

RELEASE_JSON="$(curl --fail --silent --show-error --location https://api.github.com/repos/XTLS/Xray-core/releases/latest)"
XRAY_TAG="$(jq -r '.tag_name' <<< "$RELEASE_JSON")"
XRAY_URL="$(jq -r --arg asset "$XRAY_ASSET" '.assets[] | select(.name == $asset) | .browser_download_url' <<< "$RELEASE_JSON" | head -n1)"
XRAY_DIGEST="$(jq -r --arg asset "$XRAY_ASSET" '.assets[] | select(.name == $asset) | .digest // empty' <<< "$RELEASE_JSON" | head -n1)"
[[ "$XRAY_URL" == https://github.com/XTLS/Xray-core/releases/download/* && "$XRAY_DIGEST" =~ ^sha256:[a-f0-9]{64}$ ]] || {
  echo "No official Xray release asset with SHA-256 digest found for $XRAY_ASSET." >&2
  exit 1
}

TMP_DIR="$(mktemp -d)"
cleanup() { rm -rf "$TMP_DIR"; }
trap cleanup EXIT
curl --fail --silent --show-error --location --retry 3 "$XRAY_URL" -o "$TMP_DIR/xray.zip"
EXPECTED_HASH="${XRAY_DIGEST#sha256:}"
echo "$EXPECTED_HASH  $TMP_DIR/xray.zip" | sha256sum --check --status || {
  echo "Xray archive SHA-256 verification failed." >&2
  exit 1
}
mkdir "$TMP_DIR/xray"
unzip -q "$TMP_DIR/xray.zip" -d "$TMP_DIR/xray"
XRAY_BIN="$(find "$TMP_DIR/xray" -maxdepth 2 -type f -name xray -print -quit)"
[[ -n "$XRAY_BIN" ]] || { echo "Xray executable was not found in the official archive." >&2; exit 1; }

ADMIN_PASSWORD="$(openssl rand -hex 24)"
PROFILE_KEY="$(openssl rand -base64 32 | tr -d '\r\n')"
id v2raywatch >/dev/null 2>&1 || useradd --system --home-dir /var/lib/v2raywatch --shell /usr/sbin/nologin --user-group v2raywatch

install -d -o root -g root -m 0755 /opt/v2raywatch
cp -a "$PUBLISH_DIR/." /opt/v2raywatch/
install -o root -g root -m 0755 "$XRAY_BIN" /opt/v2raywatch/xray
for data in geoip.dat geosite.dat; do
  [[ ! -f "$TMP_DIR/xray/$data" ]] || install -o root -g root -m 0644 "$TMP_DIR/xray/$data" "/opt/v2raywatch/$data"
done
chown -R root:root /opt/v2raywatch
find /opt/v2raywatch -type d -exec chmod 0755 {} \;
find /opt/v2raywatch -type f -exec chmod 0644 {} \;
chmod 0755 /opt/v2raywatch/xray

install -d -o root -g v2raywatch -m 0750 /etc/v2raywatch
install -d -o v2raywatch -g v2raywatch -m 0700 /var/lib/v2raywatch /var/lib/v2raywatch/runtime
cat > /etc/v2raywatch/server.json <<EOF
{"Server":{"ListenAddress":"0.0.0.0","Port":$PORT,"BasePath":"$ADMIN_PATH","DataDirectory":"/var/lib/v2raywatch"}}
EOF
chown root:v2raywatch /etc/v2raywatch/server.json
chmod 0640 /etc/v2raywatch/server.json
printf 'V2RAYWATCH_ADMIN_PASSWORD=%s\n' "$ADMIN_PASSWORD" > /etc/v2raywatch/admin.env
chown root:v2raywatch /etc/v2raywatch/admin.env
chmod 0640 /etc/v2raywatch/admin.env
printf '%s\n' "$PROFILE_KEY" > /etc/v2raywatch/profile.key
chown root:v2raywatch /etc/v2raywatch/profile.key
chmod 0640 /etc/v2raywatch/profile.key

install -D -o root -g root -m 0644 "$DIR/deploy/systemd/v2raywatch.service" /etc/systemd/system/v2raywatch.service
umask 077
cat > /root/v2raywatch-initial-credentials.txt <<EOF
V2rayWatch Server initial credentials
URL: http://SERVER-IP:$PORT$ADMIN_PATH
Username: admin
Password: $ADMIN_PASSWORD
Xray Core: $XRAY_TAG
Profile encryption key: stored in /etc/v2raywatch/profile.key; back it up securely.
Save this file securely and delete it after recording the credentials.
EOF
chmod 0600 /root/v2raywatch-initial-credentials.txt

systemctl daemon-reload
systemctl enable --now v2raywatch.service

echo
echo "V2rayWatch Server installed."
echo "URL:      http://SERVER-IP:$PORT$ADMIN_PATH"
echo "Username: admin"
echo "Password: $ADMIN_PASSWORD"
echo "Xray:     $XRAY_TAG"
echo "Credentials file: /root/v2raywatch-initial-credentials.txt (0600)"
echo "IMPORTANT: back up /etc/v2raywatch/profile.key; saved profiles need it for decryption."
echo "Firewall unchanged. Restrict TCP/$PORT to trusted IP addresses."
echo "WARNING: HTTP does not encrypt credentials or session cookies in transit."
