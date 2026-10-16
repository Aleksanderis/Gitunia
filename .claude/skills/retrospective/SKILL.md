---
name: retrospective
description: Owner-run review of how the agents work. Mines recent PR review comments and fixes for repeated findings, audits CLAUDE.md, rules, skills and agents for stale, duplicate, unused or contradicting text, and proposes changes the owner approves one by one. Use when the owner types /retrospective or asks to tune, clean up or extend the instructions.
model: sonnet
effort: high
---

# Retrospective

Improve the instructions from evidence. Nothing is changed until the owner approves it item by item; this skill never runs unattended and never edits the fenced files below.

## Fenced (never propose changes, only report a problem you see)

Safety and destructive-operation rules, pushing and merge rules, scope (MVP) rules, agent identity, model choice, and the autonomy invariants in `CLAUDE.md` "Working autonomously". Only the owner edits these.

## 1. Collect

- Range: since the last entry in `docs/retrospectives.md` (PR number or date); the first run takes the last 40 merged PRs.
- Run `.claude/skills/retrospective/scripts/collect.sh <scratchpad>/retro.md [since-pr] [limit]`. It writes every PR's comments, tagged OWNER, REVIEWER or IMPLEMENTER and cut to 1500 characters, and prints counts. Read the file in parts; do not paste raw `gh` output into the session. Work in the scratchpad, not in the repo.
- Many PRs (more than ~30) or a long session: do the reading in a forked subagent and bring back only the grouped findings, so the session stays small.
- Weight the evidence: an OWNER comment outweighs a reviewer finding (the owner is telling you what they care about); a reviewer finding the implementer fixed counts as a real miss; a finding the implementer skipped ("Not done", "accepted as a nit") in 3 or more PRs is reviewer noise, which is a proposal against `reviewer.md` (drop or demote the check), not a new rule.

## 2. Find what repeats

- Group findings by cause, not wording (line length, stale text after rename, missing decisions entry, untested wiring...). Count the distinct PRs for each group.
- Thresholds: 3 PRs for a prose rule, 2 for something a script, `.editorconfig` severity or test can enforce. One-offs stay out.
- For each group that qualifies, choose the weakest fix that works, in this order:
  1. **Enforce:** a check in `scripts/check-diff.sh`, an `.editorconfig` severity, an analyzer or a test. Cannot be ignored or drift.
  2. **Fix an existing rule:** it exists but was missed, so make it findable (shorter, in the file loaded for those paths, a checklist item in `work-task`).
  3. **New rule:** one line, in the narrowest place that is loaded when it matters (`.claude/rules/` by path before `CLAUDE.md`).
- A mechanical fix the agents keep doing by hand (same command, same edit) is a candidate for a script in `scripts/`.

## 3. Audit what exists

Read `CLAUDE.md`, `.claude/rules/*`, `.claude/agents/*`, every `SKILL.md`, and the owner's memory feedback files (`MEMORY.md` in the project memory directory): a memory that restates a repo rule is a duplicate (keep the repo copy, ask before deleting the memory), and a memory the repo contradicts is stale. Report, with file:line:
- **Enforceable:** prose rules a script, `.editorconfig` or test could now enforce (and so can leave the prose).
- **No findings is not "unused".** A rule agents follow produces no findings, so silence never justifies trimming a rule. Trim only duplicates, stale text, and prose that is now enforced mechanically.
- **Duplicates:** the same rule in two places (keep the narrowest, point to it from the other).
- **Contradictions** between files, or with `docs/decisions.md`.
- **Stale references:** files, scripts, commands, labels, issue numbers or doc sections that no longer exist (check each with ls/grep/gh).
- **Size:** line counts per file against the previous run; any file that grew should say what it dropped. Long rule files get a rule-of-thumb target: new text means old text trimmed.

## 4. Present

One report to the owner in the terminal, no edits yet:
- Table of proposals: id, evidence (PR numbers), proposed action (enforce / fix / add / merge / delete), exact new wording or diff, target file, what it replaces.
- Audit findings as a short list.
- Anything in a fenced area as "for the owner to decide".
Ask which ids to apply (AskUserQuestion with multiSelect when there are few, otherwise a numbered list to answer by id).

## 5. Apply what was approved

- Only approved ids, exactly as worded in the report; anything new that comes up becomes a new proposal, not a silent edit.
- Owner-requested instruction and script changes go straight to `main` as a local commit (`chore:` with an `Agent: orchestrator` trailer), not pushed; say "committed locally, not pushed". Changes that answer PR review comments go through a PR branch instead (CLAUDE.md).
- Run `scripts/check-diff.sh` and `dotnet build` if a script or `.editorconfig` changed; a new enforcing check must be run once against recent history to confirm it does not flag clean code.
- Append an entry to `docs/retrospectives.md`: date, PR range covered, counts per recurring group, proposals applied or declined (with the owner's reason, so a declined idea is not proposed again), and the line counts of the instruction files. Create the file with a one-line header on the first run.

## Do not

- Add a rule for a single occurrence, or one the owner declined before (check `docs/retrospectives.md`).
- Grow the instructions without naming what is trimmed or enforced instead.
- Weaken a rule or a gate to make agents faster. Say so to the owner if that seems to be the right call.
