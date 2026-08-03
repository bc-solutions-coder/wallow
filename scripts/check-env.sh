#!/usr/bin/env bash
# Documentation-completeness check for Compose environment variables.
#
# Every ${VAR} a docker/*.yml file interpolates must appear in that file's
# paired .env.example. Commented out counts — an example file's commented lines
# are documentation, and several are deliberately commented (see below).
#
# The assertion is COMPLETENESS, not REQUIREDNESS, and the difference is the
# whole design. Compose's interpolation grammar distinguishes required (${V})
# from defaulted (${V:-d}, ${V-d}) from erroring (${V:?e}, ${V?e}), but this
# check reads only the NAME — everything up to `:`, `-`, `?` or `}` — and never
# decides whether a variable must be set. Two reasons that is the right call:
#
#   * Defaulted variables are still knobs. All five GARAGE_* settings in
#     docker-compose.yml carry `:-` defaults, so Compose never warns about them,
#     yet a fork retuning object storage has no way to learn they exist except by
#     reading the compose file. A requiredness check is silent on exactly the
#     drift that actually happened here.
#
#   * Bare ${VAR} is not always drift. docker-compose.production.yml passes the
#     four seeded client secrets as bare ${VAR} ON PURPOSE, with a comment saying
#     so: an unset secret must abort the seeder rather than register a public
#     client. Three of them are documented as COMMENTED lines under "Additional
#     clients (optional)" in .env.production.example, which is correct — you
#     uncomment the ones whose seed indices you use. A requiredness check would
#     flag that fail-closed design forever.
#
# Only one direction is checked. The reverse — an example entry no compose file
# reads — is not drift here: .env.example also documents variables consumed by
# application code (COOKIE_PASSWORD, the OIDC_* set) rather than by Compose
# interpolation, so sweeping that way would report every one of them.
#
# Docker is never invoked. `docker compose config` would resolve interpolation
# for us, but it reports only what Compose considers unset, which is the
# requiredness question above — and it would put a daemon-less CI runner between
# this check and a green build for no gain.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root/docker"

# compose file : the .env.example a reader of that file would consult.
pairs=(
  "docker-compose.yml:.env.example"
  "docker-compose.production.yml:.env.production.example"
  "docker-compose.test.yml:.env.example"
)

failed=0

for pair in "${pairs[@]}"; do
  compose="${pair%%:*}"
  example="${pair##*:}"

  if [ ! -f "$compose" ] || [ ! -f "$example" ]; then
    echo "==> $compose -> $example: FAIL (missing file; update the pairs list in ${BASH_SOURCE[0]##*/})"
    failed=1
    continue
  fi

  # Newline-joined rather than an array: an empty bash array expanded under
  # `set -u` is not portable back to bash 3.2, which is what macOS ships.
  missing=""
  while IFS= read -r name; do
    # The trailing `=` is load-bearing — without it a GARAGE_S3_PORT_ALT entry
    # would satisfy GARAGE_S3_PORT. The optional `#` accepts a commented line.
    grep -qE "^#? *${name}=" "$example" || missing+="    ${name}"$'\n'
  done < <(grep -ohE '\$\{[A-Za-z_][A-Za-z_0-9]*' "$compose" | cut -c3- | sort -u)

  if [ -z "$missing" ]; then
    echo "==> $compose -> $example: ok"
  else
    echo "==> $compose -> $example: FAIL — referenced but not documented in $example"
    printf '%s' "$missing"
    failed=1
  fi
done

if [ "$failed" -ne 0 ]; then
  echo
  echo "Add each variable to its .env.example with the default the compose file uses."
  echo "Commenting the line out is fine when the value is optional or must stay unset."
fi

exit "$failed"
