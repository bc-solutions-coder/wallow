#!/usr/bin/env bash
# Package-publish surface check for the three built workspace packages.
#
# publint lints the manifest (exports map, files, bin, type) against what is
# actually in the packed tarball; @arethetypeswrong/cli resolves every declared
# entrypoint the way a consumer's TypeScript would and reports entrypoints that
# resolve to no types.
#
# Both tools inspect a PACKED tarball, so `pnpm --filter './packages/*' build`
# must have run first — an unbuilt dist/ reads as a broken exports map.
#
# The tarball is built here, with `pnpm pack`, and handed to both tools. That is
# load-bearing rather than tidiness: in-repo every package's `exports` points at
# `src/` so apps resolve from source with no prebuilt dist, and the `dist/` map
# consumers get is applied at publish time from `publishConfig.exports`. pnpm
# applies that field; `npm pack` does not. attw's own `--pack` flag shells out to
# npm, so it would resolve the SOURCE map against a tarball containing only
# `dist/` and report every entrypoint as unresolvable — a failure describing a
# package nobody ever publishes. publint already packs with pnpm internally.
#
# Analysis scope, and why it is narrowed:
#   --profile esm-only   these packages are ESM-only ("type": "module", no CJS
#                        output), so the node10 and node16-from-CJS resolutions
#                        describe consumers they never claimed to support.
#   --ignore-rules internal-resolution-error
#                        every workspace member compiles under
#                        moduleResolution "Bundler" (tsconfig.base.json), where
#                        relative imports carry no file extension. tsc emits
#                        those extensionless specifiers into the .d.ts files
#                        verbatim, which Node16 resolution rejects and Bundler
#                        resolution — the one every consumer here uses — accepts.
#   styles' './styles.css'
#                        a raw stylesheet passthrough, not a JS/TS entrypoint;
#                        there is nothing for attw to resolve types for.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

packages=(packages/auth packages/query packages/sdk packages/styles packages/testing packages/utils)
attw_common=(--profile esm-only --ignore-rules internal-resolution-error --no-summary)

tarball_dir="$(mktemp -d)"
trap 'rm -rf "$tarball_dir"' EXIT

for package in "${packages[@]}"; do
  echo "==> $package"

  pnpm exec publint --strict "$package"

  tarball="$(pnpm --dir "$package" pack --pack-destination "$tarball_dir" | tail -n 1)"

  attw_args=("${attw_common[@]}")
  if [ "$package" = "packages/styles" ]; then
    attw_args+=(--exclude-entrypoints ./styles.css)
  fi
  pnpm exec attw "$tarball" "${attw_args[@]}"
done
