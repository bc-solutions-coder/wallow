#!/usr/bin/env bash
# Generate theme from branding.json and serve the docs site locally.
# Usage: ./scripts/docs-serve.sh [--build-only] [--port PORT]

set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

PORT=5004
BUILD_ONLY=false

for arg in "$@"; do
  case "$arg" in
    --build-only) BUILD_ONLY=true ;;
    --port=*) PORT="${arg#*=}" ;;
  esac
done

echo "Generating docs theme from branding.json..."
node scripts/generate-docs-theme.mjs

if [[ "$BUILD_ONLY" == "true" ]]; then
  echo "Building docs site..."
  dotnet docfx docfx.json
  echo "Site built to _site/"
else
  echo "Building and serving docs site on http://localhost:${PORT}..."
  dotnet docfx docfx.json --serve --port "$PORT"
fi
