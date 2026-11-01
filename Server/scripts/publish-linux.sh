#!/usr/bin/env bash
set -Eeuo pipefail
DIR="$(cd -- "$(dirname -- "$0")/.." && pwd)"
command -v dotnet >/dev/null 2>&1 || { echo "Install .NET 10 SDK first." >&2; exit 1; }
dotnet publish "$DIR/src/V2rayWatch.Server.Web/V2rayWatch.Server.Web.csproj" -c Release -o "$DIR/publish"
echo "Publish output: $DIR/publish"
