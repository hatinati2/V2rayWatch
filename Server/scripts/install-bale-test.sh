#!/usr/bin/env bash
set -Eeuo pipefail

REPOSITORY="hatinati2/V2rayWatch"
SOURCE_REF="${V2RAYWATCH_BALE_TEST_REF:-feature/bale-notifications-test}"
APP_DIR="/opt/v2raywatch-bale-test"
CONFIG_DIR="/etc/v2raywatch-bale-test"
DATA_DIR="/var/lib/v2raywatch-bale-test"
SERVICE="v2raywatch-bale-test"
PORT="${V2RAYWATCH_BALE_TEST_PORT:-6101}"

if [[ $EUID -ne 0 ]]; then
  echo "Run with sudo: curl ... | sudo bash" >&2
  exit 1
fi

[[ -r /etc/os-release ]] || { echo "Cannot detect operating system." >&2; exit 1; }
. /etc/os-release
case "${ID:-}:${VERSION_ID:-}:${VERSION_CODENAME:-}" in
  ubuntu:24.04:noble|ubuntu:26.04:resolute) ;;
  *)
    echo "The side-by-side Bale test installer supports Ubuntu 24.04 and 26.04 only." >&2
    echo "Detected: ${PRETTY_NAME:-unknown}" >&2
    exit 1
    ;;
esac

for required in /opt/dotnet/dotnet /opt/v2raywatch/xray; do
  [[ -x "$required" ]] || {
    echo "Required existing V2rayWatch component not found: $required" >&2
    echo "Install the stable V2rayWatch Linux Server first. This test installer will not modify it." >&2
    exit 1
  }
done
/opt/dotnet/dotnet --list-sdks | grep -q '^10\.' || {
  echo ".NET 10 SDK is required at /opt/dotnet. The stable installation normally installs it." >&2
  exit 1
}
id v2raywatch >/dev/null 2>&1 || {
  echo "The stable v2raywatch service account was not found; install the stable server first." >&2
  exit 1
}
[[ ! -e "$APP_DIR" && ! -e "$CONFIG_DIR" && ! -e "$DATA_DIR" ]] || {
  echo "A Bale test installation already exists. Refusing to overwrite it." >&2
  exit 1
}
[[ "$PORT" =~ ^[0-9]+$ ]] && (( PORT >= 1024 && PORT <= 65535 )) || {
  echo "Test port must be between 1024 and 65535." >&2
  exit 2
}
command -v ss >/dev/null 2>&1 || { echo "The ss command is required." >&2; exit 1; }
if ss -H -ltn "sport = :$PORT" | grep -q .; then
  echo "TCP port $PORT is already occupied." >&2
  exit 1
fi

WORK_DIR="$(mktemp -d /var/tmp/v2raywatch-bale-test.XXXXXX)"
cleanup() { rm -rf "$WORK_DIR"; }
trap cleanup EXIT
export DOTNET_ROOT=/opt/dotnet
export PATH="/opt/dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export TMPDIR="$WORK_DIR/tmp"
mkdir -p "$TMPDIR"

echo "[1/5] Downloading isolated test source ($SOURCE_REF)..."
curl --fail --silent --show-error --location --retry 4 --retry-all-errors \
  "https://codeload.github.com/$REPOSITORY/tar.gz/refs/heads/$SOURCE_REF" \
  -o "$WORK_DIR/source.tar.gz"
mkdir "$WORK_DIR/source"
tar -xzf "$WORK_DIR/source.tar.gz" --strip-components=1 -C "$WORK_DIR/source"

echo "[2/5] Building the experimental Bale-enabled server..."
dotnet restore "$WORK_DIR/source/Server/V2rayWatch.Server.sln"
dotnet publish "$WORK_DIR/source/Server/src/V2rayWatch.Server.Web/V2rayWatch.Server.Web.csproj" \
  --configuration Release --no-restore --output "$WORK_DIR/publish"
[[ -f "$WORK_DIR/publish/V2rayWatch.Server.Web.dll" ]] || {
  echo "The experimental server build did not produce its application assembly." >&2
  exit 1
}

echo "[3/5] Creating separate application, configuration and data directories..."
install -d -o root -g root -m 0755 "$APP_DIR"
cp -a "$WORK_DIR/publish/." "$APP_DIR/"
chown -R root:root "$APP_DIR"
find "$APP_DIR" -type d -exec chmod 0755 {} \;
find "$APP_DIR" -type f -exec chmod 0644 {} \;

BASE_PATH="/vw-bale-test-$(openssl rand -hex 8)"
ADMIN_PASSWORD="$(openssl rand -hex 24)"
PROFILE_KEY="$(openssl rand -base64 32 | tr -d '\r\n')"

install -d -o root -g v2raywatch -m 0750 "$CONFIG_DIR"
install -d -o v2raywatch -g v2raywatch -m 0700 "$DATA_DIR" "$DATA_DIR/runtime"
cat > "$CONFIG_DIR/server.json" <<EOF
{"Server":{"ListenAddress":"0.0.0.0","Port":$PORT,"BasePath":"$BASE_PATH","DataDirectory":"$DATA_DIR"}}
EOF
chown root:v2raywatch "$CONFIG_DIR/server.json"
chmod 0640 "$CONFIG_DIR/server.json"
printf 'V2RAYWATCH_ADMIN_PASSWORD=%s\n' "$ADMIN_PASSWORD" > "$CONFIG_DIR/admin.env"
chown root:v2raywatch "$CONFIG_DIR/admin.env"
chmod 0640 "$CONFIG_DIR/admin.env"
printf '%s\n' "$PROFILE_KEY" > "$CONFIG_DIR/profile.key"
chown root:v2raywatch "$CONFIG_DIR/profile.key"
chmod 0640 "$CONFIG_DIR/profile.key"

echo "[4/5] Installing a separate systemd service..."
cat > "/etc/systemd/system/$SERVICE.service" <<EOF
[Unit]
Description=V2rayWatch Experimental Bale Notification Test
Wants=network-online.target
After=network-online.target

[Service]
Type=simple
User=v2raywatch
Group=v2raywatch
WorkingDirectory=$APP_DIR
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROOT=/opt/dotnet
Environment=V2RAYWATCH_CONFIG=$CONFIG_DIR/server.json
Environment=V2RAYWATCH_PROFILE_KEY_FILE=$CONFIG_DIR/profile.key
Environment=V2RAYWATCH_XRAY_PATH=/opt/v2raywatch/xray
Environment=V2RAYWATCH_RUNTIME_DIR=$DATA_DIR/runtime
EnvironmentFile=$CONFIG_DIR/admin.env
ExecStart=/opt/dotnet/dotnet $APP_DIR/V2rayWatch.Server.Web.dll
Restart=always
RestartSec=5
TimeoutStopSec=20
KillSignal=SIGINT
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ProtectKernelTunables=true
ProtectKernelModules=true
ProtectControlGroups=true
RestrictSUIDSGID=true
LockPersonality=true
CapabilityBoundingSet=
AmbientCapabilities=
ReadWritePaths=$DATA_DIR

[Install]
WantedBy=multi-user.target
EOF
chmod 0644 "/etc/systemd/system/$SERVICE.service"

umask 077
cat > /root/v2raywatch-bale-test-credentials.txt <<EOF
V2rayWatch experimental Bale notification test instance
URL: http://SERVER-IP:$PORT$BASE_PATH
Username: admin
Password: $ADMIN_PASSWORD
Service: $SERVICE
Config: $CONFIG_DIR
Data: $DATA_DIR
This instance is separate from the stable V2rayWatch service and database.
EOF
chmod 0600 /root/v2raywatch-bale-test-credentials.txt

echo "[5/5] Starting the isolated test instance..."
systemctl daemon-reload
systemctl enable --now "$SERVICE"
if ! systemctl is-active --quiet "$SERVICE" ||
   ! curl --fail --silent --show-error --retry 10 --retry-connrefused --retry-delay 1 --max-time 5 \
      "http://127.0.0.1:$PORT$BASE_PATH/login" -o /dev/null; then
  echo "Experimental service failed its local health check:" >&2
  journalctl -u "$SERVICE" -n 60 --no-pager >&2 || true
  systemctl disable --now "$SERVICE" >/dev/null 2>&1 || true
  exit 1
fi

echo
echo "Experimental Bale test instance installed and healthy."
echo "URL:      http://SERVER-IP:$PORT$BASE_PATH"
echo "Username: admin"
echo "Password: $ADMIN_PASSWORD"
echo "Credentials file: /root/v2raywatch-bale-test-credentials.txt (0600)"
echo "Service: systemctl status $SERVICE"
echo "Logs:    journalctl -u $SERVICE -n 100 --no-pager"
echo
echo "The stable v2raywatch service, app files, settings and database were not changed."
echo "The test instance has its own port, service, config, encryption key and database."
echo "Open the test URL, configure Bale, then enable monitoring only in this test instance."
echo "Firewall rules were not changed; restrict TCP/$PORT to trusted IPs."
