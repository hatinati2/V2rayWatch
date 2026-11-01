#!/usr/bin/env bash
set -Eeuo pipefail

REPOSITORY="hatinati2/V2rayWatch"
BRANCH="feature/linux-server"
APP_DIR="/opt/v2raywatch"
CONFIG_DIR="/etc/v2raywatch"
DATA_DIR="/var/lib/v2raywatch"

if [[ $EUID -ne 0 ]]; then
  echo "Run this updater as root: curl ... | bash" >&2
  exit 1
fi
[[ -r /etc/os-release ]] || { echo "Cannot detect operating system." >&2; exit 1; }
. /etc/os-release
case "${ID:-}:${VERSION_ID:-}:${VERSION_CODENAME:-}" in
  ubuntu:24.04:noble|ubuntu:26.04:resolute) ;;
  *)
    echo "V2rayWatch Server supports Ubuntu 24.04 (noble) and 26.04 (resolute) only." >&2
    echo "Detected: ${PRETTY_NAME:-unknown} (${VERSION_CODENAME:-unknown})." >&2
    exit 1
    ;;
esac

for required in "$APP_DIR/V2rayWatch.Server.Web.dll" "$APP_DIR/xray" "$CONFIG_DIR/server.json" "$CONFIG_DIR/admin.env" "$CONFIG_DIR/profile.key" "/etc/systemd/system/v2raywatch.service"; do
  [[ -e "$required" ]] || { echo "Existing V2rayWatch installation is incomplete: $required" >&2; exit 1; }
done
command -v systemctl >/dev/null || { echo "systemctl is required." >&2; exit 1; }

apt_source_matches="$(grep -RniE '\\b(focal|jammy)(-security|-updates|-backports)?\\b' /etc/apt/sources.list /etc/apt/sources.list.d 2>/dev/null | head -n 8 || true)"
if [[ -n "$apt_source_matches" ]]; then
  echo "WARNING: stale focal/jammy APT source entries detected."
  echo "$apt_source_matches"
  echo "The updater will not rewrite package sources automatically."
fi

WORK_DIR="$(mktemp -d /var/tmp/v2raywatch-update.XXXXXX)"
NEXT_DIR=""
ROLLBACK_DIR=""
SERVICE_BACKUP=""
SWAPPED=0
cleanup() {
  [[ -z "$NEXT_DIR" || ! -d "$NEXT_DIR" ]] || rm -rf "$NEXT_DIR"
  [[ -z "$ROLLBACK_DIR" || ! -d "$ROLLBACK_DIR" ]] || rm -rf "$ROLLBACK_DIR"
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

export TMPDIR="$WORK_DIR/tmp"
mkdir -p "$TMPDIR"

echo "[1/5] Installing build prerequisites..."
apt-get update
DEBIAN_FRONTEND=noninteractive apt-get install -y ca-certificates curl tar jq unzip iproute2 openssl

echo "[2/5] Checking .NET 10 SDK..."
mkdir -p /opt/dotnet
if [[ ! -x /opt/dotnet/dotnet ]] || ! /opt/dotnet/dotnet --list-sdks | grep -q '^10\.'; then
  curl --fail --silent --show-error --location --retry 3 \
    https://dot.net/v1/dotnet-install.sh -o "$WORK_DIR/dotnet-install.sh"
  bash "$WORK_DIR/dotnet-install.sh" --channel 10.0 --install-dir /opt/dotnet
else
  echo ".NET 10 SDK already installed; reusing it."
fi
export DOTNET_ROOT=/opt/dotnet
export PATH="/opt/dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "[3/5] Downloading and building $BRANCH..."
curl --fail --silent --show-error --location --retry 3 \
  "https://codeload.github.com/$REPOSITORY/tar.gz/refs/heads/$BRANCH" \
  -o "$WORK_DIR/source.tar.gz"
mkdir "$WORK_DIR/source"
tar -xzf "$WORK_DIR/source.tar.gz" --strip-components=1 -C "$WORK_DIR/source"
dotnet restore "$WORK_DIR/source/Server/V2rayWatch.Server.sln"
dotnet publish "$WORK_DIR/source/Server/src/V2rayWatch.Server.Web/V2rayWatch.Server.Web.csproj" \
  --configuration Release --no-restore --output "$WORK_DIR/publish"
[[ -f "$WORK_DIR/publish/V2rayWatch.Server.Web.dll" ]] || {
  echo "Published application assembly is missing." >&2
  exit 1
}

echo "[4/5] Preparing an atomic application update (data and credentials are preserved)..."
NEXT_DIR="$(mktemp -d /opt/v2raywatch.next.XXXXXX)"
cp -a "$WORK_DIR/publish/." "$NEXT_DIR/"
install -o root -g root -m 0755 "$APP_DIR/xray" "$NEXT_DIR/xray"
for data in geoip.dat geosite.dat; do
  [[ ! -f "$APP_DIR/$data" ]] || install -o root -g root -m 0644 "$APP_DIR/$data" "$NEXT_DIR/$data"
done
chown -R root:root "$NEXT_DIR"
find "$NEXT_DIR" -type d -exec chmod 0755 {} \;
find "$NEXT_DIR" -type f -exec chmod 0644 {} \;
chmod 0755 "$NEXT_DIR/xray"

ROLLBACK_DIR="$(mktemp -d /opt/v2raywatch.rollback.XXXXXX)"
SERVICE_BACKUP="$ROLLBACK_DIR/v2raywatch.service"
cp -a /etc/systemd/system/v2raywatch.service "$SERVICE_BACKUP"
install -o root -g root -m 0644 \
  "$WORK_DIR/source/Server/deploy/systemd/v2raywatch.service" \
  /etc/systemd/system/v2raywatch.service

echo "[5/5] Restarting the updated service..."
systemctl stop v2raywatch
mv "$APP_DIR" "$ROLLBACK_DIR/app"
mv "$NEXT_DIR" "$APP_DIR"
NEXT_DIR=""
SWAPPED=1
systemctl daemon-reload

if ! systemctl start v2raywatch || ! systemctl is-active --quiet v2raywatch; then
  echo "Updated service did not become active. Rolling back the application." >&2
  journalctl -u v2raywatch -n 50 --no-pager >&2 || true
  systemctl stop v2raywatch >/dev/null 2>&1 || true
  mv "$APP_DIR" "$ROLLBACK_DIR/failed-app" 2>/dev/null || true
  mv "$ROLLBACK_DIR/app" "$APP_DIR"
  install -o root -g root -m 0644 "$SERVICE_BACKUP" /etc/systemd/system/v2raywatch.service
  systemctl daemon-reload
  if systemctl start v2raywatch && systemctl is-active --quiet v2raywatch; then
    echo "Previous application restored and service is active." >&2
  else
    echo "Rollback completed, but service did not start. Inspect: journalctl -u v2raywatch -n 100" >&2
  fi
  exit 1
fi

PORT="$(jq -r '.Server.Port' "$CONFIG_DIR/server.json")"
BASE_PATH="$(jq -r '.Server.BasePath' "$CONFIG_DIR/server.json")"
echo "V2rayWatch Server updated successfully."
echo "Existing profiles, settings, subscriptions, admin password, panel PATH and encryption key were preserved."
echo "Panel URL: http://SERVER-IP:$PORT$BASE_PATH"
echo "Service: systemctl status v2raywatch"
echo "Logs: journalctl -u v2raywatch -n 100 --no-pager"
# Successful update: old binary snapshot is no longer needed.
rm -rf "$ROLLBACK_DIR"
ROLLBACK_DIR=""
