#!/bin/bash

COMMAND=$(jq -r '.tool_input.command')

if echo "$COMMAND" | grep -q 'git.*commit'; then
  jq -n '{
    "hookSpecificOutput": {
      "hookEventName": "PreToolUse",
      "permissionDecision": "ask",
      "permissionDecisionReason": "Review this git commit before proceeding"
    }
  }'
  exit 0
fi

exit 0
