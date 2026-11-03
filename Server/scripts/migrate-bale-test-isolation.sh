#!/usr/bin/env bash
set -Eeuo pipefail

SERVICE="v2raywatch-bale-test"
APP_DIR="/opt/v2raywatch-bale-test"
CONFIG_DIR="/etc/v2raywatch-bale-test"
CONFIG_FILE="$CONFIG_DIR/server.json"
DATA_DIR="/var/lib/v2raywatch-bale-test"
UNIT_FILE="/etc/systemd/system/$SERVICE.service"
TEST_USER="v2raywatch-bale-test"
TEST_GROUP="v2raywatch-bale-test"
BACKUP_DIR="$(mktemp -d /root/v2raywatch-bale-isolation.XXXXXX)"
MIGRATING=0

if [[ $EUID -ne 0 ]]; then
  echo "Run with sudo: sudo bash migrate-bale-test-isolation.sh" >&2
  exit 1
fi

for path in "$APP_DIR" "$CONFIG_DIR" "$DATA_DIR"; do
  [[ -d "$path" ]] || { echo "Existing Bale test path not found: $path" >&2; exit 1; }
done
[[ -f "$CONFIG_FILE" && -f "$UNIT_FILE" ]] || {
  echo "The existing Bale test config or systemd unit is missing." >&2
  exit 1
}
systemctl cat "$SERVICE" >/dev/null 2>&1 || {
  echo "The Bale test systemd service is not installed." >&2
  exit 1
}

CURRENT_USER="$(systemctl show "$SERVICE" -p User --value)"
CURRENT_GROUP="$(systemctl show "$SERVICE" -p Group --value)"
if [[ "$CURRENT_USER" == "$TEST_USER" && "$CURRENT_GROUP" == "$TEST_GROUP" ]]; then
  echo "The Bale test service already uses its dedicated account."
  exit 0
fi
if [[ "$CURRENT_USER" != "v2raywatch" || "$CURRENT_GROUP" != "v2raywatch" ]]; then
  echo "Refusing migration: unexpected current service identity ($CURRENT_USER:$CURRENT_GROUP)." >&2
  exit 1
fi
id v2raywatch >/dev/null 2>&1 || { echo "Stable v2raywatch account not found." >&2; exit 1; }

cp -a "$CONFIG_FILE" "$BACKUP_DIR/server.json"
cp -a "$UNIT_FILE" "$BACKUP_DIR/service.unit"
chmod 0700 "$BACKUP_DIR"

rollback() {
  rc=$?
  if [[ "$MIGRATING" -eq 1 ]]; then
    echo "Migration failed; restoring the previous test-instance permissions and service..." >&2
    systemctl stop "$SERVICE" >/dev/null 2>&1 || true
    cp -a "$BACKUP_DIR/server.json" "$CONFIG_FILE"
    cp -a "$BACKUP_DIR/service.unit" "$UNIT_FILE"
    chown -R v2raywatch:v2raywatch "$DATA_DIR" || true
    chown -R root:v2raywatch "$CONFIG_DIR" || true
    chmod 0750 "$CONFIG_DIR" || true
    find "$CONFIG_DIR" -type f -exec chmod 0640 {} \; || true
    systemctl daemon-reload >/dev/null 2>&1 || true
    systemctl start "$SERVICE" >/dev/null 2>&1 || true
  fi
  rm -rf "$BACKUP_DIR"
  exit "$rc"
}
trap rollback ERR

if ! getent group "$TEST_GROUP" >/dev/null 2>&1; then
  groupadd --system "$TEST_GROUP"
fi
if ! id "$TEST_USER" >/dev/null 2>&1; then
  useradd --system --gid "$TEST_GROUP" --home-dir /nonexistent --no-create-home --shell /usr/sbin/nologin "$TEST_USER"
fi
if id -nG "$TEST_USER" | tr ' ' '\n' | grep -Fxq v2raywatch; then
  echo "The dedicated test account is a member of the stable service group; refusing migration." >&2
  exit 1
fi

MIGRATING=1
echo "[1/4] Stopping only the experimental Bale test service..."
systemctl stop "$SERVICE"

echo "[2/4] Moving the test config and database to the dedicated account..."
chown root:"$TEST_GROUP" "$CONFIG_DIR"
chmod 0750 "$CONFIG_DIR"
chown root:"$TEST_GROUP" "$CONFIG_FILE" "$CONFIG_DIR/admin.env" "$CONFIG_DIR/profile.key"
chmod 0640 "$CONFIG_FILE" "$CONFIG_DIR/admin.env" "$CONFIG_DIR/profile.key"
chown -R "$TEST_USER:$TEST_GROUP" "$DATA_DIR"
chmod 0700 "$DATA_DIR"
if [[ -d "$DATA_DIR/runtime" ]]; then
  chown -R "$TEST_USER:$TEST_GROUP" "$DATA_DIR/runtime"
  chmod 0700 "$DATA_DIR/runtime"
fi

python3 - "$CONFIG_FILE" <<'PY'
import json, sys
path = sys.argv[1]
with open(path, encoding="utf-8") as stream:
    config = json.load(stream)
server = config.setdefault("Server", {})
server["ListenAddress"] = "127.0.0.1"
with open(path, "w", encoding="utf-8") as stream:
    json.dump(config, stream, ensure_ascii=False, separators=(",", ":"))
    stream.write("\\n")
PY
chown root:"$TEST_GROUP" "$CONFIG_FILE"
chmod 0640 "$CONFIG_FILE"

echo "[3/4] Updating only the experimental systemd identity..."
sed -i 's/^User=v2raywatch$/User=v2raywatch-bale-test/; s/^Group=v2raywatch$/Group=v2raywatch-bale-test/' "$UNIT_FILE"
grep -Fxq "User=$TEST_USER" "$UNIT_FILE"
grep -Fxq "Group=$TEST_GROUP" "$UNIT_FILE"
systemctl daemon-reload
systemctl start "$SERVICE"

echo "[4/4] Checking the experimental panel on loopback..."
read -r PORT BASE_PATH < <(python3 - "$CONFIG_FILE" <<'PY'
import json, sys
server = json.load(open(sys.argv[1], encoding="utf-8"))["Server"]
print(server["Port"], server["BasePath"])
PY
)
healthy=0
for _ in $(seq 1 20); do
  if systemctl is-active --quiet "$SERVICE" &&
     curl --fail --silent --show-error --max-time 5 "http://127.0.0.1:$PORT$BASE_PATH/login" -o /dev/null 2>/dev/null; then
    healthy=1
    break
  fi
  sleep 1
done
[[ "$healthy" -eq 1 ]]

MIGRATING=0
trap - ERR
rm -rf "$BACKUP_DIR"
echo "Migration completed. The existing Bale test database, token, Chat ID and encryption key were preserved."
echo "The test service now runs as $TEST_USER:$TEST_GROUP and listens only on 127.0.0.1:$PORT."
echo "The stable v2raywatch service and its files were not changed."
echo "Use SSH forwarding to open the panel: ssh -L $PORT:127.0.0.1:$PORT USER@SERVER-IP"
