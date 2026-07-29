#!/usr/bin/env bash
# Fork smoke: build a scratch TanStack Start app OUTSIDE this workspace from the
# packed @bc-solutions-coder tarballs, the way a fork consumes the published
# packages.
#
# What it proves that `pnpm build` inside the workspace cannot: the packages are
# consumable as TARBALLS. Inside the workspace every @bc-solutions-coder import
# resolves through a pnpm symlink into the package's source tree, so a file
# missing from `files`, an `exports` subpath pointing at a path `pnpm pack` never
# ships, a runtime dependency listed under devDependencies, or a .d.ts
# referencing a type that stayed behind all resolve fine — and break the moment a
# fork installs the package from a registry. This script is that install.
#
# Steps:
#   1. build packages/sdk and packages/styles (pack ships dist/, so it must exist)
#   2. `pnpm pack` both into a work directory outside the repo
#   3. copy the committed scaffold (scripts/fork-smoke/) next to the tarballs
#   4. `pnpm install` there — its package.json points at `file:./vendor/*.tgz`
#   5. `pnpm build`, then `pnpm typecheck` (the route tree is emitted BY the
#      build, so typecheck can only run after it)
#   6. assert the Nitro server bundle actually landed
#
# The work directory is outside the repo on purpose: under `scripts/fork-smoke/`
# pnpm would find the repo's pnpm-workspace.yaml and .npmrc walking up, and the
# app would stop being an outside-the-workspace consumer.
#
# Env knobs:
#   FORK_SMOKE_DIR=<dir>   Work directory (default: a fresh mktemp -d).
#   FORK_SMOKE_KEEP=1      Leave the work directory behind (for debugging).
#
# Usage:
#   ./scripts/fork-smoke.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SCAFFOLD_DIR="$REPO_ROOT/scripts/fork-smoke"
WORK_DIR="${FORK_SMOKE_DIR:-$(mktemp -d "${TMPDIR:-/tmp}/wallow-fork-smoke.XXXXXX")}"
APP_DIR="$WORK_DIR/app"

log() { printf '\n=== %s ===\n' "$1"; }

cleanup() {
  if [[ -n "${FORK_SMOKE_KEEP:-}" ]]; then
    log "FORK_SMOKE_KEEP set — leaving $WORK_DIR in place"
    return
  fi
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

case "$WORK_DIR" in
"$REPO_ROOT" | "$REPO_ROOT"/*)
  echo "::error::FORK_SMOKE_DIR must be outside the repo (got $WORK_DIR)" >&2
  exit 1
  ;;
esac

log "Building the packages that get packed"
pnpm --dir "$REPO_ROOT" --filter @bc-solutions-coder/sdk --filter @bc-solutions-coder/styles build

log "Packing the tarballs"
mkdir -p "$APP_DIR/vendor"
# Fixed names (not <name>-<version>.tgz) so the scaffold's package.json can spell
# out its `file:` specifiers instead of the script rewriting JSON.
pnpm --dir "$REPO_ROOT/packages/sdk" pack --out "$APP_DIR/vendor/sdk.tgz"
pnpm --dir "$REPO_ROOT/packages/styles" pack --out "$APP_DIR/vendor/styles.tgz"

log "Scaffolding the scratch app in $APP_DIR"
# The scaffold only — no node_modules, no build output, and no repo config
# (.npmrc / pnpm-workspace.yaml stay behind, which is the point).
tar -cf - -C "$SCAFFOLD_DIR" package.json tsconfig.json vite.config.ts src | tar -xf - -C "$APP_DIR"

log "Installing from the packed tarballs"
# No lockfile is committed for the scaffold, so this resolves fresh — a fork's
# very first install.
pnpm --dir "$APP_DIR" install --no-frozen-lockfile

log "Building the scratch app"
pnpm --dir "$APP_DIR" build

log "Typechecking against the packed .d.ts files"
pnpm --dir "$APP_DIR" typecheck

SERVER_BUNDLE="$APP_DIR/.output/server/index.mjs"
if [[ ! -f "$SERVER_BUNDLE" ]]; then
  echo "::error::fork smoke built without emitting $SERVER_BUNDLE" >&2
  exit 1
fi

log "Fork smoke passed"
