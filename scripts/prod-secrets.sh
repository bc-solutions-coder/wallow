#!/usr/bin/env bash
#
# Generate docker/.env.production from docker/.env.production.example, replacing
# every generatable secret with a fresh random value.
#
# BOOTSTRAP ONLY — this writes a new file and REFUSES to touch an existing one.
# There is deliberately no rotate mode. Rotating a live deployment's secrets is
# not a text substitution: POSTGRES_PASSWORD has to change in the database in the
# same breath as the file, GARAGE_ACCESS_KEY reimports a key inside the cluster
# volume, and BFF_COOKIE_PASSWORD has an overlap protocol of its own
# (BFF_COOKIE_PASSWORDS, documented in the example file). A script that rewrote
# those in place would look like it had rotated them while leaving the running
# stack unable to authenticate to itself.
#
# WHAT IT DOES NOT FILL IN. Two kinds of value are left exactly as the example
# file has them, because no amount of entropy is the right answer:
#
#   - Operator-supplied credentials. SMTP_PASSWORD is issued by your mail
#     provider. It stays CHANGE_ME and is reported at the end.
#   - Deployment identity. API_PUBLIC_URL, COOKIE_DOMAIN, ADMIN_EMAIL,
#     SEED_FILE_HOST_PATH and friends describe where this deployment lives.
#     They ship with wallow.dev defaults that are wrong for your fork.
#
# So the output is not deployable on its own — it is the half a machine can do,
# and the summary printed at the end is the half you still owe it.
#
# HEX, NOT BASE64, everywhere. Several of these land inside URLs that
# docker-compose.production.yml builds by interpolation — VALKEY_PASSWORD
# becomes redis://:${VALKEY_PASSWORD}@valkey:6379 — where a base64 '/' or '+'
# silently truncates the host instead of failing loudly.

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
template="$repo_root/docker/.env.production.example"
output="${1:-$repo_root/docker/.env.production}"

if ! command -v openssl >/dev/null 2>&1; then
  echo "error: openssl not found on PATH; it is the only source of entropy here" >&2
  exit 1
fi

if [ ! -f "$template" ]; then
  echo "error: template not found: $template" >&2
  exit 1
fi

# Refusing beats overwriting. An existing .env.production is either a live
# deployment's secrets or a half-finished edit; both are worse to lose than a
# rerun is to be denied. Write elsewhere by passing a path, or move the old one.
if [ -e "$output" ]; then
  echo "error: $output already exists — refusing to overwrite it." >&2
  echo "       Move it aside first, or pass a different output path:" >&2
  echo "         ${BASH_SOURCE[0]##*/} /tmp/.env.production.new" >&2
  exit 1
fi

# The shapes below are not interchangeable, and the ones that look arbitrary are
# not: GARAGE_ACCESS_KEY must be 'GK' plus exactly 24 hex characters and
# GARAGE_SECRET_KEY exactly 64, because Garage validates both on import.
secret_for() {
  case "$1" in
    POSTGRES_PASSWORD | VALKEY_PASSWORD | GARAGE_RPC_SECRET | GARAGE_ADMIN_TOKEN | \
      GARAGE_SECRET_KEY | OIDC_CLIENT_SECRET | BFF_COOKIE_PASSWORD | \
      OPENIDDICT_SIGNING_CERT_PASSWORD | OPENIDDICT_ENCRYPTION_CERT_PASSWORD)
      openssl rand -hex 32
      ;;
    IDENTITY_SIGNING_KEY)
      openssl rand -hex 48
      ;;
    GARAGE_ACCESS_KEY)
      printf 'GK%s' "$(openssl rand -hex 12)"
      ;;
    GF_ADMIN_PASSWORD)
      openssl rand -hex 24
      ;;
    ADMIN_PASSWORD)
      # The only value here that must satisfy a character-class policy rather
      # than a length, so it is the only one that is not plain hex. The suffix
      # guarantees the four classes that 32 random base64 characters merely make
      # likely; the entropy is all in the 24 random bytes ahead of it. Base64 is
      # safe for this one specifically — it is passed to the seeder as a plain
      # environment value and never interpolated into a URL.
      printf '%sAa1!' "$(openssl rand -base64 24 | tr -d '\n')"
      ;;
    *)
      return 1
      ;;
  esac
}

# Newline-joined rather than an array: an empty bash array expanded under
# `set -u` is not portable back to bash 3.2, which is what macOS ships.
generated=""

umask 077
while IFS= read -r line || [ -n "$line" ]; do
  case "$line" in
    [A-Z]*=*)
      name="${line%%=*}"
      if value="$(secret_for "$name")"; then
        printf '%s=%s\n' "$name" "$value"
        generated+="$name"$'\n'
        continue
      fi
      ;;
  esac
  printf '%s\n' "$line"
done <"$template" >"$output"
chmod 600 "$output"

count="$(printf '%s' "$generated" | grep -c . || true)"
echo "Wrote $output ($count secrets generated, mode 600)."
echo

# Only UNCOMMENTED placeholders block a deployment. The commented ones are the
# optional BCORDES_* client secrets and the BFF_COOKIE_PASSWORDS rotation
# example, which are supposed to stay placeholders until someone opts in.
remaining="$(grep -nE '^[A-Z][A-Z_0-9]*=.*CHANGE_ME' "$output" || true)"
if [ -n "$remaining" ]; then
  echo "STILL PLACEHOLDER — no random value is the right one for these:"
  printf '%s\n' "$remaining" | sed 's/^/  /'
  echo
fi

echo "STILL YOURS TO SET — the example file's values describe wallow.dev, not your deployment:"
echo "  API_PUBLIC_URL / AUTH_PUBLIC_URL / WEB_PUBLIC_URL / COOKIE_DOMAIN"
echo "  ADMIN_EMAIL, SMTP_HOST / SMTP_FROM_ADDRESS, SEED_FILE_HOST_PATH"
echo
echo "Then bring the stack up from docker/, with ONE edge profile — 'direct'"
echo "(Caddy on :80/:443) or 'pangolin' (newt tunnel, needs the PANGOLIN_* /"
echo "NEWT_* values in .env.production):"
echo "  docker compose -f docker-compose.production.yml --env-file .env.production --profile direct up --build"
