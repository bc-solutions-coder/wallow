#!/usr/bin/env bash
# Exercise the packed package from an isolated consumer on the disposable observability network.
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
consumer_dir="$(mktemp -d)"
trap 'rm -rf "$consumer_dir"' EXIT
cd "$repo_root"
pnpm exec turbo run build --filter=@bc-solutions-coder/telemetry...
mkdir "$consumer_dir/archives"
archive="$(pnpm --dir packages/telemetry pack --pack-destination "$consumer_dir/archives" | tail -n 1)"
cp scripts/telemetry-consumer.mjs "$consumer_dir/consumer.mjs"
printf '%s\n' '{"private":true,"type":"module"}' > "$consumer_dir/package.json"
cd "$consumer_dir"
npm install --ignore-scripts --no-audit --no-fund --registry=https://registry.npmjs.org "$archive" '@types/node@^24'
"$repo_root/packages/telemetry/node_modules/.bin/tsc" --allowJs --checkJs --noEmit --strict \
  --module nodenext --moduleResolution nodenext --target es2023 --types node consumer.mjs
cd "$repo_root/docker"
docker run --rm --env-file observability/gateway.local \
  --network "${OBSERVABILITY_PROOF_NETWORK:-wallow-245-proof_storage}" \
  --mount "type=bind,source=$consumer_dir,target=/consumer,readonly" \
  --workdir /consumer node:24-alpine node consumer.mjs
