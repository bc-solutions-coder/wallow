#!/usr/bin/env bash
set -euo pipefail
mkdir -p .ci-artifacts/packages
for name in api-errors auth env logger navigation query sdk styles testing utils telemetry; do
  pnpm --dir "packages/$name" pack --out "$PWD/.ci-artifacts/packages/$name.tgz"
done
