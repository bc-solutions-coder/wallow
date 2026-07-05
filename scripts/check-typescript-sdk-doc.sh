#!/usr/bin/env bash
# TDD check for T15 (Wallow-8yuz.6.3): TypeScript SDK integration guide.
# Asserts the guide file exists and is referenced in docs/toc.yml under Integrations.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
guide="$repo_root/docs/integrations/typescript-sdk.md"
toc="$repo_root/docs/toc.yml"

fail=0

# 1. Guide file exists
if [[ -f "$guide" ]]; then
  echo "PASS: docs/integrations/typescript-sdk.md exists"
else
  echo "FAIL: docs/integrations/typescript-sdk.md is missing"
  fail=1
fi

# 2. Filename is lowercase kebab-case (already fixed by name, verify no uppercase/underscore)
if [[ "$(basename "$guide")" =~ ^[a-z0-9]+(-[a-z0-9]+)*\.md$ ]]; then
  echo "PASS: filename is lowercase kebab-case"
else
  echo "FAIL: filename is not lowercase kebab-case"
  fail=1
fi

# 3. toc.yml references the guide by href under Integrations
if grep -q "integrations/typescript-sdk.md" "$toc"; then
  echo "PASS: docs/toc.yml references integrations/typescript-sdk.md"
else
  echo "FAIL: docs/toc.yml does not reference integrations/typescript-sdk.md"
  fail=1
fi

# 4. The reference sits within the Integrations section (after the bff-pattern entry,
#    before the next top-level section 'API Reference')
if awk '
  /^- name: Integrations$/ { inSec=1; next }
  /^- name: / && inSec { inSec=0 }
  inSec && /integrations\/typescript-sdk.md/ { found=1 }
  END { exit(found ? 0 : 1) }
' "$toc"; then
  echo "PASS: reference is inside the Integrations section"
else
  echo "FAIL: reference is not inside the Integrations section"
  fail=1
fi

if [[ "$fail" -ne 0 ]]; then
  echo "RESULT: FAIL"
  exit 1
fi
echo "RESULT: PASS"
