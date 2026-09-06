#!/usr/bin/env bash
# Regenerates every openapi-ts output in the workspace from the committed
# snapshot and fails when the result differs from what is committed.
#
# Two packages generate from packages/sdk/openapi/v1.json: the SDK (its typed
# client, under packages/sdk/src/generated) and api-errors (the ErrorCode
# catalogue, under packages/api-errors/src/generated). The snapshot is kept in
# step with the API by openapi-drift.yml; this check is the other half — that
# the committed outputs are what the snapshot and each generator config
# produce, so neither can go stale when the snapshot or a config moves.
#
# Part of `pnpm check`. Needs no running API: both generators read the snapshot.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

generated_dirs=(packages/sdk/src/generated packages/api-errors/src/generated)

pnpm --filter @bc-solutions-coder/sdk generate
pnpm --filter @bc-solutions-coder/api-errors generate

# Compared against the index, not HEAD, so a freshly staged regeneration passes
# before it is committed. Untracked files are checked separately: `git diff`
# alone would miss a newly emitted module.
untracked="$(git ls-files --others --exclude-standard -- "${generated_dirs[@]}")"
if [ -n "$untracked" ] || ! git diff --quiet -- "${generated_dirs[@]}"; then
  [ -n "$untracked" ] && printf 'untracked: %s\n' "$untracked"
  git --no-pager diff --stat -- "${generated_dirs[@]}"
  echo "error: generated output is out of date. Run \`pnpm check:generated\` and stage the result." >&2
  exit 1
fi

echo "generated output is current."
