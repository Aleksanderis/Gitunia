#!/usr/bin/env bash
# Separates the owner's own issues, PRs and comments from the agent-generated pile.
# Agents and the owner share one GitHub login, so the only signal is the agent marker: a comment or body that contains
# `<!-- agent:`, an `agent · Claude` signature, `Opened by ...` or an `Agent:` trailer was written by an agent; everything else
# by the owner (case-insensitive).
# Usage:
#   scripts/owner-feedback.sh sweep       label every issue/PR `owner` (owner-created or owner commented) or `agent-created`,
#                                         then list owner comments that have no agent reply after them
#   scripts/owner-feedback.sh unanswered  only the list, no label changes
# An owner comment counts as answered once an agent-marked comment follows it. The label `feedback-handled` (set by an agent after
# it replied, or by the owner to dismiss an item) hides an item from the list; remove it to bring the item back.
# Filters on GitHub: label:owner (everything the owner wrote or commented on), -label:agent-created (owner-created only).
set -euo pipefail

mode="${1:-}"
case "$mode" in
  sweep|unanswered) ;;
  *) sed -n '2,10p' "$0" >&2; exit 2 ;;
esac

cd "$(cd "$(dirname "$0")/.." && pwd)"

login="$(gh api user --jq .login)"
repository="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
owner_label="owner"
agent_label="agent-created"

if [ "$mode" = "sweep" ]; then
  gh label create "$owner_label" --color "D93F0B" --description "Created or commented by the owner" 2>/dev/null || true
  gh label create "feedback-handled" --color "0E8A16" --description "Owner comments reviewed and settled; skipped by owner-feedback.sh" 2>/dev/null || true
  gh label create "$agent_label" --color "BFD4F2" --description "Created by an agent" 2>/dev/null || true
fi

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

gh issue list --state all --limit 2000 --json number,title,state,author,body,labels,comments > "$work_dir/issues.json"
gh pr list --state all --limit 2000 --json number,title,state,author,body,labels,comments,reviews > "$work_dir/pulls.json"
gh api "repos/$repository/pulls/comments" --paginate --jq '[.[]|{pull:(.pull_request_url|split("/")|last|tonumber),login:.user.login,body:.body,at:.created_at}]' |
  jq -s 'add // []' > "$work_dir/inline.json"

# One record per issue or PR: owner-written body, owner comments, and owner comments without a later agent comment.
jq -n --arg login "$login" --arg ownerLabel "$owner_label" --arg agentLabel "$agent_label" \
  --slurpfile issues "$work_dir/issues.json" --slurpfile pulls "$work_dir/pulls.json" --slurpfile inline "$work_dir/inline.json" '
  def agent: (. // "" | ascii_downcase) | (contains("<!-- agent:") or contains("agent · claude") or contains("opened by") or contains("\nagent: "));
  def entry($author; $body; $at): {mine: ($author == $login), agent: ($body | agent), at: $at};
  [ ($issues[0][] | . + {kind: "issue", reviews: []}), ($pulls[0][] | . + {kind: "pull"}) ]
  | map(. as $item
    | ([ .comments[] | entry(.author.login; .body; .createdAt) ]
       + [ .reviews[] | select((.body // "") != "") | entry(.author.login; .body; .submittedAt) ]
       + [ $inline[0][] | select(.pull == $item.number) | entry(.login; .body; .at) ]) as $comments
    | ($comments | map(select(.mine and (.agent | not)))) as $ownerComments
    | ($comments | map(select(.agent))) as $agentComments
    | {
        kind, number, title, state,
        labels: [.labels[].name],
        ownerBody: ((.author.login == $login) and ((.body | agent) | not)),
        ownerCommentCount: ($ownerComments | length),
        unanswered: ([ $ownerComments[] | .at as $when | select(([ $agentComments[] | select(.at > $when) ] | length) == 0) ] | length)
      })
  | map(. + {wantLabel: (if (.ownerBody or .ownerCommentCount > 0) then $ownerLabel else $agentLabel end)})
' > "$work_dir/items.json"

if [ "$mode" = "sweep" ]; then
  # Fix the labels in both directions: add the wanted one, remove the other.
  jq -r --arg ownerLabel "$owner_label" --arg agentLabel "$agent_label" '.[] | . as $item
    | (if $item.wantLabel == $ownerLabel then $agentLabel else $ownerLabel end) as $otherLabel
    | (if ($item.labels | index($item.wantLabel)) then empty else "add\t\($item.number)\t\($item.wantLabel)" end),
      (if ($item.labels | index($otherLabel)) then "remove\t\($item.number)\t\($otherLabel)" else empty end)' "$work_dir/items.json" |
    while IFS=$'\t' read -r action number label; do
      gh issue edit "$number" "--$action-label" "$label" >/dev/null 2>&1 || echo "could not $action label $label on #$number" >&2
    done
  jq -r '"Classified: owner \(map(select(.wantLabel == "owner")) | length), agent-created \(map(select(.wantLabel == "agent-created")) | length) of \(length) issues and PRs"' "$work_dir/items.json"
fi

echo "== Owner comments with no agent reply after them"
jq -r '.[] | select(.unanswered > 0 and ((.labels | index("feedback-handled")) | not)) | "#\(.number) [\(.state)] \(.unanswered) unanswered: \(.title)"' "$work_dir/items.json"
