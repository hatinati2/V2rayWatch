#!/usr/bin/env bash
set -Eeuo pipefail

REPOSITORY="hatinati2/V2rayWatch"
SOURCE_REF="${V2RAYWATCH_BALE_TEST_REF:-feature/bale-notifications-test}"
APP_DIR="/opt/v2raywatch-bale-test"
SERVICE="v2raywatch-bale-test"
CONFIG_FILE="/etc/v2raywatch-bale-test/server.json"

if [[ $EUID -ne 0 ]]; then
  echo "Run with sudo: curl ... | sudo bash" >&2
  exit 1
fi

[[ -d "$APP_DIR" && -f "$CONFIG_FILE" ]] || {
  echo "The isolated Bale test instance is not installed." >&2
  exit 1
}
systemctl cat "$SERVICE" >/dev/null 2>&1 || {
  echo "The isolated Bale test systemd service was not found." >&2
  exit 1
}
SERVICE_USER="$(systemctl show "$SERVICE" -p User --value)"
SERVICE_GROUP="$(systemctl show "$SERVICE" -p Group --value)"
[[ "$SERVICE_USER" == "v2raywatch-bale-test" && "$SERVICE_GROUP" == "v2raywatch-bale-test" ]] || {
  echo "The test service is not using its dedicated OS account. Reinstall the isolated test instance first." >&2
  exit 1
}
[[ -x /opt/dotnet/dotnet ]] || {
  echo ".NET SDK/runtime not found at /opt/dotnet/dotnet." >&2
  exit 1
}
/opt/dotnet/dotnet --list-sdks | grep -q '^10\.' || {
  echo ".NET 10 SDK is required." >&2
  exit 1
}
command -v python3 >/dev/null 2>&1 || {
  echo "python3 is required to read the test-instance configuration." >&2
  exit 1
}

PORT="$(python3 -c 'import json; print(json.load(open("'"$CONFIG_FILE"'"))["Server"]["Port"])')"
BASE_PATH="$(python3 -c 'import json; print(json.load(open("'"$CONFIG_FILE"'"))["Server"]["BasePath"])')"
[[ "$PORT" =~ ^[0-9]+$ && "$BASE_PATH" =~ ^/[A-Za-z0-9_-]+$ ]] || {
  echo "The test-instance configuration has an invalid port or base path." >&2
  exit 1
}

WORK_DIR="$(mktemp -d /var/tmp/v2raywatch-bale-update.XXXXXX)"
STAGE_DIR="$(mktemp -d /opt/.v2raywatch-bale-test-stage.XXXXXX)"
BACKUP_DIR="/opt/.v2raywatch-bale-test-backup.$$"
SWAPPED=0
cleanup() {
  rc=$?
  if [[ "$SWAPPED" -eq 1 && -d "$BACKUP_DIR" ]]; then
    echo "Update did not complete; restoring the previous Bale test application..." >&2
    systemctl stop "$SERVICE" >/dev/null 2>&1 || true
    rm -rf "$APP_DIR"
    mv "$BACKUP_DIR" "$APP_DIR"
    systemctl start "$SERVICE" >/dev/null 2>&1 || true
  fi
  [[ -d "$STAGE_DIR" ]] && rm -rf "$STAGE_DIR"
  rm -rf "$WORK_DIR"
  exit "$rc"
}
trap cleanup EXIT

export DOTNET_ROOT=/opt/dotnet
export PATH="/opt/dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_TMP="$WORK_DIR"

echo "[1/4] Downloading Bale test source ($SOURCE_REF)..."
curl --fail --silent --show-error --location --retry 4 --retry-all-errors \
  "https://codeload.github.com/$REPOSITORY/tar.gz/refs/heads/$SOURCE_REF" \
  -o "$WORK_DIR/source.tar.gz"
mkdir "$WORK_DIR/source"
tar -xzf "$WORK_DIR/source.tar.gz" --strip-components=1 -C "$WORK_DIR/source"

echo "[2/4] Building the updated experimental server..."
dotnet restore "$WORK_DIR/source/Server/V2rayWatch.Server.sln"
dotnet publish "$WORK_DIR/source/Server/src/V2rayWatch.Server.Web/V2rayWatch.Server.Web.csproj" \
  --configuration Release --no-restore --output "$WORK_DIR/publish"
[[ -f "$WORK_DIR/publish/V2rayWatch.Server.Web.dll" ]] || {
  echo "The experimental server build did not produce its application assembly." >&2
  exit 1
}
cp -a "$WORK_DIR/publish/." "$STAGE_DIR/"
chown -R root:root "$STAGE_DIR"
find "$STAGE_DIR" -type d -exec chmod 0755 {} \;
find "$STAGE_DIR" -type f -exec chmod 0644 {} \;

echo "[3/4] Replacing only the Bale test application..."
systemctl stop "$SERVICE"
mv "$APP_DIR" "$BACKUP_DIR"
SWAPPED=1
mv "$STAGE_DIR" "$APP_DIR"
STAGE_DIR=""

echo "[4/4] Starting and checking the Bale test service..."
systemctl start "$SERVICE"
healthy=0
for _ in $(seq 1 20); do
  if curl --fail --silent --show-error --max-time 5 \
      "http://127.0.0.1:$PORT$BASE_PATH/login" -o /dev/null 2>/dev/null; then
    healthy=1
    break
  fi
  sleep 1
done

if [[ "$healthy" -ne 1 ]] || ! systemctl is-active --quiet "$SERVICE"; then
  echo "Updated Bale test service failed its local health check." >&2
  journalctl -u "$SERVICE" -n 60 --no-pager >&2 || true
  exit 1
fi

rm -rf "$BACKUP_DIR"
SWAPPED=0
echo "Bale test instance updated and healthy."
echo "The stable v2raywatch application, settings and database were not changed."
echo "Open the existing test panel and try Detect Chat ID / Test Message again."
