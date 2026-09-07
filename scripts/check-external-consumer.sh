#!/usr/bin/env bash
# Install the public packages outside the workspace, then run and typecheck a consumer.
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
consumer_dir="$(mktemp -d)"
trap 'rm -rf "$consumer_dir"' EXIT
cd "$repo_root"

if [[ $# == 0 && -n "${CI_PACKAGE_DIR:-}" ]]; then
  errors_package="$CI_PACKAGE_DIR/api-errors.tgz"
  sdk_package="$CI_PACKAGE_DIR/sdk.tgz"
elif [[ $# == 0 ]]; then
  pnpm exec turbo run build --filter=@bc-solutions-coder/sdk...
  mkdir "$consumer_dir/archives"
  errors_package="$(pnpm --dir packages/api-errors pack --pack-destination "$consumer_dir/archives" | tail -n 1)"
  sdk_package="$(pnpm --dir packages/sdk pack --pack-destination "$consumer_dir/archives" | tail -n 1)"
elif [[ $# == 2 ]]; then
  sdk_package="@bc-solutions-coder/sdk@$1"
  errors_package="@bc-solutions-coder/api-errors@$2"
else
  echo 'Usage: check-external-consumer.sh [SDK_VERSION API_ERRORS_VERSION]' >&2
  exit 1
fi

cp scripts/external-consumer.mjs "$consumer_dir/consumer.mjs"
printf '%s\n' '{"private":true,"type":"module"}' > "$consumer_dir/package.json"
printf '%s\n' '@bc-solutions-coder:registry=https://npm.pkg.github.com' > "$consumer_dir/.npmrc"
cd "$consumer_dir"
npm install --ignore-scripts --no-audit --no-fund --registry=https://registry.npmjs.org \
  "$errors_package" "$sdk_package" 'redis@^4.7.0' '@tanstack/react-query@^5' '@types/node@^24' '@types/react@^19'
node consumer.mjs
"$repo_root/packages/sdk/node_modules/.bin/tsc" --allowJs --checkJs --noEmit --strict \
  --module esnext --moduleResolution bundler --target es2022 --types node consumer.mjs
