#!/usr/bin/env bash
set -Eeuo pipefail
PORT=6000; ADMIN_PATH=""
DIR="$(cd -- "$(dirname -- "$0")/.." && pwd)"
PUBLISH_DIR="$DIR/publish"
usage(){ echo "Usage: sudo $0 [--port PORT] [--path /PATH] [--publish-dir DIR]"; }
while (($#)); do case "$1" in
 --port) [[ $# -ge 2 ]] || { usage; exit 2; }; PORT="$2"; shift 2;;
 --path) [[ $# -ge 2 ]] || { usage; exit 2; }; ADMIN_PATH="$2"; shift 2;;
 --publish-dir) [[ $# -ge 2 ]] || { usage; exit 2; }; PUBLISH_DIR="$2"; shift 2;;
 -h|--help) usage; exit 0;;
 *) echo "Unknown option: $1" >&2; usage; exit 2;;
esac; done
[[ $EUID -eq 0 ]] || { echo "Run with sudo/root." >&2; exit 1; }
. /etc/os-release
[[ "$ID" == ubuntu && "$VERSION_ID" == 24.04 ]] || { echo "Ubuntu 24.04 required." >&2; exit 1; }
[[ "$PORT" =~ ^[0-9]+$ ]] && ((PORT>=1024 && PORT<=65535)) || { echo "Port must be 1024-65535." >&2; exit 2; }
command -v ss >/dev/null && command -v openssl >/dev/null || { echo "iproute2 and openssl are required." >&2; exit 1; }
command -v dotnet >/dev/null || { echo ".NET 10 ASP.NET Core Runtime is required." >&2; exit 1; }
dotnet --list-runtimes | grep -q '^Microsoft.AspNetCore.App 10\.' || { echo "ASP.NET Core Runtime 10.x missing." >&2; exit 1; }
[[ -f "$PUBLISH_DIR/V2rayWatch.Server.Web.dll" ]] || { echo "Publish first using Server/scripts/publish-linux.sh." >&2; exit 1; }
ss -H -ltn "sport = :$PORT" | grep -q . && { echo "TCP port $PORT is occupied." >&2; exit 1; }
[[ -n "$ADMIN_PATH" ]] || ADMIN_PATH="/vw-$(openssl rand -hex 16)"
[[ "$ADMIN_PATH" =~ ^/[A-Za-z0-9_-]{16,64}$ ]] || { echo "Invalid PATH format." >&2; exit 2; }
[[ ! -e /etc/v2raywatch/server.json ]] || { echo "Existing installation detected; refusing to overwrite." >&2; exit 1; }
PASS="$(openssl rand -hex 24)"
id v2raywatch >/dev/null 2>&1 || useradd --system --home-dir /var/lib/v2raywatch --shell /usr/sbin/nologin --user-group v2raywatch
install -d -o root -g root -m 0755 /opt/v2raywatch
cp -a "$PUBLISH_DIR/." /opt/v2raywatch/
chown -R root:root /opt/v2raywatch
find /opt/v2raywatch -type d -exec chmod 0755 {} \;
find /opt/v2raywatch -type f -exec chmod 0644 {} \;
install -d -o root -g v2raywatch -m 0750 /etc/v2raywatch
install -d -o v2raywatch -g v2raywatch -m 0750 /var/lib/v2raywatch
cat > /etc/v2raywatch/server.json <<EOF
{"Server":{"ListenAddress":"0.0.0.0","Port":$PORT,"BasePath":"$ADMIN_PATH","DataDirectory":"/var/lib/v2raywatch"}}
EOF
chown root:v2raywatch /etc/v2raywatch/server.json; chmod 0640 /etc/v2raywatch/server.json
printf 'V2RAYWATCH_ADMIN_PASSWORD=%s\n' "$PASS" > /etc/v2raywatch/admin.env
chown root:v2raywatch /etc/v2raywatch/admin.env; chmod 0640 /etc/v2raywatch/admin.env
install -D -o root -g root -m 0644 "$DIR/deploy/systemd/v2raywatch.service" /etc/systemd/system/v2raywatch.service
umask 077
cat > /root/v2raywatch-initial-credentials.txt <<EOF
URL: http://SERVER-IP:$PORT$ADMIN_PATH
Username: admin
Password: $PASS
Store securely and delete this file after saving the password.
EOF
chmod 0600 /root/v2raywatch-initial-credentials.txt
systemctl daemon-reload; systemctl enable --now v2raywatch.service
echo "Installed: http://SERVER-IP:$PORT$ADMIN_PATH"
echo "Username: admin"; echo "Password: $PASS"
echo "Credentials file: /root/v2raywatch-initial-credentials.txt (0600)"
echo "Firewall unchanged. Restrict TCP/$PORT to trusted IPs."
echo "WARNING: HTTP does not encrypt credentials or cookies in transit."
