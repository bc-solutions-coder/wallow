#!/usr/bin/env bash
# The test-file half of `pnpm lint`.
#
# `pnpm lint` lints SOURCE only, excluding `**/*.test.*` and `**/*.stories.tsx`
# with `--ignore-pattern`. This script lints exactly the files that pass
# excludes, with oxlint's vitest plugin (71 rules) additionally enabled. Run
# together the two passes cover every file oxlint would otherwise lint in one
# go, and `pnpm check` runs both.
#
# Why a script rather than a second `oxlint <glob>` invocation:
#
#   oxlint does NOT expand globs in path arguments. `oxlint '**/*.test.ts'`
#   passes the literal string through to the filesystem, matches nothing, and
#   exits 0 having linted zero files — the failure mode this whole split has to
#   defend against, because a silent pass looks exactly like success.
#   `ignorePatterns` cannot rescue it either: it has no `!` negation, so there
#   is no way to say "everything EXCEPT source".
#
# So the file list is enumerated instead, from oxlint's own `--debug=files`
# walk over the same three roots the source pass uses. Discovery and ignore
# semantics therefore cannot drift between the two passes: both come from one
# walker reading one `.oxlintrc.json` `ignorePatterns`. The count is printed and
# a zero count is a hard failure.
#
# Why no `-c`/`--config`: passing an explicit config file DISABLES oxlint's
# nested-config lookup, so `packages/ui/.oxlintrc.json` and
# `packages/forms/.oxlintrc.json` would stop being read and their test-file
# relaxations (`unicorn/prefer-query-selector`, `react/jsx-max-depth`, ...)
# would come back as errors. This pass deliberately passes no config flag: the
# root config plus every nested config apply exactly as they do for the source
# pass, and the vitest rule severities live in the root config's
# `**/*.test.*` override alongside the other test-file relaxations.
#
# Extra arguments are forwarded to the lint invocation (e.g. `--fix`).

set -euo pipefail

cd "$(dirname "$0")/.."

OXLINT="./node_modules/.bin/oxlint"
ROOTS=(apps packages tools)

# `--debug=files` prints the file list oxlint would lint, then exits. Keep only
# the test and story files the source pass excludes. Read into an array with a
# loop rather than `mapfile`, which macOS's bundled bash 3.2 does not have.
FILES=()
while IFS= read -r file; do
  FILES+=("$file")
done < <("$OXLINT" "${ROOTS[@]}" --debug=files | grep -E '\.test\.[^/]+$|\.stories\.tsx$')

if [ "${#FILES[@]}" -eq 0 ]; then
  echo "lint-tests: enumerated 0 test files — refusing to report a pass." >&2
  echo "lint-tests: check the --debug=files output of '$OXLINT ${ROOTS[*]}'." >&2
  exit 1
fi

echo "lint-tests: linting ${#FILES[@]} test/story files with the vitest plugin"

exec "$OXLINT" "${FILES[@]}" --vitest-plugin --deny-warnings "$@"
