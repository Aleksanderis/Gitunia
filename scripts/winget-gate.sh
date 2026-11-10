#!/usr/bin/env bash
# Decides whether winget.yml may submit (#1297). Prints the reason to do nothing, or nothing when it may submit.
# Input env: EVENT (github.event_name), PRERELEASE, SWITCH (vars.WINGET_SUBMIT), HAS_TOKEN ("true" when WINGET_TOKEN is
# set), TAG. A manual run (workflow_dispatch) ignores the pre-release flag and the switch, never the token.
set -euo pipefail
reason=""
if [ "${EVENT:-}" != "workflow_dispatch" ]; then
  if [ "${PRERELEASE:-}" = "true" ]; then
    reason="$TAG is a pre-release, pre-releases are never submitted"
  elif [ "${SWITCH:-}" != "true" ]; then
    reason="repository variable WINGET_SUBMIT is not set to true (no Gitunia.Gitunia package is accepted yet), $TAG not submitted. Set it to true once the first package exists, or run this workflow by hand"
  fi
fi
if [ -z "$reason" ] && [ "${HAS_TOKEN:-}" != "true" ]; then
  reason="secret WINGET_TOKEN is not set, $TAG not submitted"
fi
printf '%s' "$reason"
