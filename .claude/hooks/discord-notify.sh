#!/bin/bash
# Discord notification hook for Claude Code
# Fires on PostToolUse(Bash) — only sends if a git commit just happened.
# Also handles SessionStart and SessionEnd events.

WEBHOOK_URL="https://discord.com/api/webhooks/1481020848302456924/M4h5jmZ1l9HNAQ0MwaX28WiYoa0DL68CZJ4xB-MS1VWE2WILZZtgpr5j4KJ6Z31VxxGs"

EVENT=$(cat)

HOOK_EVENT=$(echo "$EVENT" | jq -r '.hook_event_name // "Unknown"')
SESSION_ID=$(echo "$EVENT" | jq -r '.session_id // "unknown"' | cut -c1-8)
CWD=$(echo "$EVENT" | jq -r '.cwd // "unknown"')
PROJECT=$(basename "$CWD")

case "$HOOK_EVENT" in
  SessionStart)
    TITLE="Session Started"
    DESC="Claude Code session started in **${PROJECT}**"
    COLOR=3066993
    ;;
  SessionEnd)
    TITLE="Session Ended"
    DESC="Claude Code session ended in **${PROJECT}**"
    COLOR=15158332
    ;;
  PostToolUse)
    # Only proceed if the Bash command was a git commit (not add, push, etc.)
    COMMAND=$(echo "$EVENT" | jq -r '.tool_input.command // ""')
    echo "$COMMAND" | grep -q "git commit" || exit 0

    # Skip if the command failed (no tool_response or error)
    RESPONSE=$(echo "$EVENT" | jq -r '.tool_response // ""')
    echo "$RESPONSE" | grep -qi "nothing to commit\|error\|fatal" && exit 0

    # Get the latest commit info
    COMMIT_HASH=$(cd "$CWD" && git log -1 --format="%h" 2>/dev/null)
    COMMIT_MSG=$(cd "$CWD" && git log -1 --format="%s" 2>/dev/null)
    COMMIT_BODY=$(cd "$CWD" && git log -1 --format="%b" 2>/dev/null | head -5)
    BRANCH=$(cd "$CWD" && git branch --show-current 2>/dev/null)

    TITLE="Commit — ${PROJECT}"
    NL=$'\n'
    DESC="\`${COMMIT_HASH}\` on \`${BRANCH}\`${NL}${NL}**${COMMIT_MSG}**"
    [ -n "$COMMIT_BODY" ] && DESC="${DESC}${NL}${COMMIT_BODY}"

    COLOR=3447003
    ;;
  *)
    exit 0
    ;;
esac

PAYLOAD=$(jq -n \
  --arg title "$TITLE" \
  --arg desc "$DESC" \
  --argjson color "${COLOR:-9807270}" \
  --arg footer "Session: $SESSION_ID | Project: $PROJECT" \
  '{
    "embeds": [{
      "title": $title,
      "description": $desc,
      "color": $color,
      "footer": {"text": $footer},
      "timestamp": (now | todate)
    }]
  }')

curl -s -X POST "$WEBHOOK_URL" \
  -H "Content-Type: application/json" \
  -d "$PAYLOAD" \
  >/dev/null 2>&1

exit 0
