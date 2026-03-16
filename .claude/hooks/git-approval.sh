#!/bin/bash

COMMAND=$(jq -r '.tool_input.command')

if echo "$COMMAND" | grep -q 'git.*commit'; then
  # Extract commit message from -m flag (handles both 'msg' and "msg" formats)
  MESSAGE=$(echo "$COMMAND" | grep -oP '(?<=-m\s)["\x27]([^"\x27]*)["\x27]' | tr -d '"\x27' | head -1)

  if [ -z "$MESSAGE" ]; then
    MESSAGE="(no message extracted)"
  fi

  jq -n --arg msg "$MESSAGE" '{
    "hookSpecificOutput": {
      "hookEventName": "PreToolUse",
      "permissionDecision": "ask",
      "permissionDecisionReason": ("Commit: " + $msg)
    }
  }'
  exit 0
fi

exit 0
