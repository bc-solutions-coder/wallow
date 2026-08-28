#!/usr/bin/env bash
# actionlint over .github/workflows. Prefers a local binary; falls back to the
# pinned docker image so no contributor has to install Go tooling. Deliberately
# NOT part of `pnpm check`: check stays runnable offline, and the docker
# fallback needs a one-time image pull.
set -euo pipefail
cd "$(dirname "$0")/.."

ACTIONLINT_VERSION="1.7.12"

if command -v actionlint >/dev/null 2>&1; then
  exec actionlint -color
fi

if command -v docker >/dev/null 2>&1; then
  exec docker run --rm -v "$PWD":/repo -w /repo \
    "rhysd/actionlint:${ACTIONLINT_VERSION}" -color
fi

echo "error: actionlint is not installed and docker is unavailable." >&2
echo "install guide: https://github.com/rhysd/actionlint/blob/main/docs/install.md" >&2
exit 1
