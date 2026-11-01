#!/usr/bin/env bash
set -Eeuo pipefail

BRANCH="feature/linux-server"
REPOSITORY="hatinati2/V2rayWatch"

if [[ $EUID -ne 0 ]]; then
  echo "Run this installer as root: curl ... | sudo bash" >&2
  exit 1
fi

if [[ ! -r /etc/os-release ]]; then
  echo "Cannot detect operating system." >&2
  exit 1
fi
. /etc/os-release
if [[ "${ID:-}" != "ubuntu" || "${VERSION_ID:-}" != "24.04" ]]; then
  echo "V2rayWatch Server installer supports Ubuntu 24.04 only." >&2
  exit 1
fi

for tool in apt-get systemctl; do
  command -v "$tool" >/dev/null 2>&1 || {
    echo "Required command not found: $tool" >&2
    exit 1
  }
done

WORK_DIR="$(mktemp -d /tmp/v2raywatch-setup.XXXXXX)"
cleanup() { rm -rf "$WORK_DIR"; }
trap cleanup EXIT

echo "[1/4] Installing build and runtime prerequisites..."
apt-get update
DEBIAN_FRONTEND=noninteractive apt-get install -y ca-certificates curl tar jq unzip iproute2 openssl

echo "[2/4] Installing .NET 10 SDK..."
curl --fail --silent --show-error --location --retry 3 \
  https://dot.net/v1/dotnet-install.sh -o "$WORK_DIR/dotnet-install.sh"
bash "$WORK_DIR/dotnet-install.sh" --channel 10.0 --install-dir /opt/dotnet
export DOTNET_ROOT=/opt/dotnet
export PATH="/opt/dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "[3/4] Downloading and building V2rayWatch Server ($BRANCH)..."
curl --fail --silent --show-error --location --retry 3 \
  "https://codeload.github.com/$REPOSITORY/tar.gz/refs/heads/$BRANCH" \
  -o "$WORK_DIR/source.tar.gz"
mkdir "$WORK_DIR/source"
tar -xzf "$WORK_DIR/source.tar.gz" --strip-components=1 -C "$WORK_DIR/source"
dotnet restore "$WORK_DIR/source/Server/V2rayWatch.Server.sln"
dotnet publish "$WORK_DIR/source/Server/src/V2rayWatch.Server.Web/V2rayWatch.Server.Web.csproj" \
  --configuration Release --no-restore --output "$WORK_DIR/publish"

echo "[4/4] Installing V2rayWatch Server and Xray Core..."
bash "$WORK_DIR/source/Server/scripts/install-ubuntu.sh" \
  --publish-dir "$WORK_DIR/publish" "$@"

echo
echo "Installation complete. The generated URL and admin password are shown above."
echo "Save the password and securely back up /etc/v2raywatch/profile.key."
