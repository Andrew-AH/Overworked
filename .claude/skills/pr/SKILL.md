---
name: pr
description: Create a GitHub PR for the current branch with a terse Summary + Changes body only — a two-sentence summary and at most a handful of one-sentence change bullets — then tag the branch with it in Overworked (`ow pr`) so the app shows its "PR #n" pill. Uses the branch's Overworked ticket tag for the title and summary. Detects git submodule changes and opens a matching PR in the submodule repo, cross-linking the two. No test plan, no reviewer notes, no extra sections. Trivial changes (comments, whitespace, formatting, auto-generated reformatting) and mechanical ripple (call-site updates, DI registrations, test plumbing) are excluded from the Changes list. Use when the user asks to "PR this", "make a PR", "open a pull request", or similar.
---

# Create PR (Summary + Changes only)

Open a GitHub PR for the current branch using `gh pr create`. The body must contain **exactly two sections**: `## Summary` and `## Changes`. Nothing else — no test plan, no reviewer notes, no routing tables, no acceptance criteria, no emoji-prefixed extras.

The **one** permitted addition is a single cross-link line above `## Summary`, used only when this branch also changes a git submodule (see step 3).

## Workflow

### 1. Gather context

Run these in parallel:

- `git status` (confirm clean working tree; if dirty, ask before proceeding)
- `git log <base>..HEAD --oneline` to see commits on the branch
- `git diff <base>...HEAD --stat` to see touched files
- `git diff <base>...HEAD` (or per-file) to read the actual changes
- `git remote -v` to confirm the GitHub repo
- `git submodule status` and `.gitmodules`, if the repo has submodules
- `gh auth status` — if unauthenticated, ask the user to run `gh auth login` (it needs a browser); don't attempt it yourself
- **The repo's name in Overworked**: the folder name of its main checkout, which is the parent folder of `git rev-parse --path-format=absolute --git-common-dir`. Used in steps 1 and 6.
- **The ticket**: `ow list <repo> --json`, and the `ticket` link on the worktree whose `path` is this checkout (`git rev-parse --show-toplevel`; on Windows git writes `/` where `ow` writes `\`, so compare ignoring that and case). Its key is the `PROJ-123`-style part of the link.
  - No `ow`, or no ticket tag: **the PR has no associated ticket**. Proceed without one; don't ask, and don't guess a key from the branch name.
  - A ticket tag: link it in the Summary and put its key in the title. If a tool for that tracker is connected (e.g. Jira through the Atlassian MCP), read the ticket so the Summary describes the problem accurately. If that lookup fails, tell the user which key it was and ask: fix the tag, or go ahead with just the link.

### 2. Determine the base branch

Default to `main`. If the repo uses `master` or `develop`, use that. Do not guess; check `git symbolic-ref refs/remotes/origin/HEAD` if uncertain. Resolve the base for a submodule separately — `.gitmodules` may record a `branch =` key, which is the submodule's base.

### 3. Detect submodule changes

Skip this step when the repo has no submodules.

For each submodule path, check whether the branch moved its pointer:

```
git diff <base>...HEAD -- <submodule-path>
```

A `Subproject commit <old> -> <new>` hunk means the pointer moved. If nothing changed for any submodule, skip to step 4 and open a single PR as normal.

If a pointer did move, work out **why** — this decides whether a second PR is needed. From inside the submodule:

```
git branch --show-current
git log origin/<sub-base>..HEAD --oneline     # submodule commits not yet on its base
git log origin/<sub-branch>..HEAD --oneline   # submodule commits not yet pushed
gh pr list --head <sub-branch> --state open --json number,url,title
```

Then:

- **New submodule commits not on its base** → a submodule PR is required. Reuse the open PR if `gh pr list` already found one for that head branch; otherwise create one in step 5.
- **Pointer moved only to a commit already on the submodule's base** (e.g. fast-forwarding onto merged work) → no submodule PR. Just describe the bump in the parent's Changes list and move on.
- **Submodule branch has unpushed commits** → `gh pr create` cannot open a PR for a branch the remote doesn't have. Tell the user and ask before running `git push -u origin <sub-branch>` from inside the submodule. Do not push without being asked or agreed.
- **Submodule is on a detached HEAD** with commits off-base → stop and tell the user; they need a real branch before a PR is possible.

### 4. Write the bodies

**Summary — two sentences, plain English.** What was going wrong (or missing), and what the change does about it. Reference the ticket as a markdown link if one exists. No bullets.

- Behaviour and outcomes, not implementation. "Orders paid partly by gift card were left marked unpaid, so support had to fix each one by hand" beats "adds a `GiftCardPaymentRef` write to the order mapper".
- No class names, method names, table names, or internal acronyms — they belong in Changes. Expand any other acronym on first use.
- Cut background, history, supporting statistics, and how-we-got-here. If the reviewer can approve the PR without it, it does not belong in the Summary.
- A third sentence only if the PR genuinely does two unrelated things. Never a fourth.

**Changes — one short bullet per meaningful change.** Lead with the file path in bold backticks, then an em-dash, then what changed, in **one sentence**. Group every file serving the same change into a single bullet. Aim for **five bullets or fewer**; past eight you are transcribing the diff, not summarising it.

The test for a bullet: would a reviewer read it and then look at something they wouldn't have looked at anyway? If not, it has not earned a line.

**Exclude** — noise, not changes:

- Mechanical ripple from another bullet: call sites updated to match a new signature, DI registrations, `sealed`/access-modifier changes, a new DTO that only carries a field already described, namespace or import churn.
- Test plumbing: package references, test helpers, existing tests updated to keep compiling.
- Comment-only or doc-only edits inside source files.
- Whitespace / formatting / line-ending / encoding-only edits.
- Auto-generated file reformatting that does not change behaviour.
- Trivial renames that don't change semantics.
- Lockfile churn unless it represents a deliberate dependency change.

**Include**:

- Logic changes — new branches, new behaviour, changed conditions.
- New or removed public APIs, event payload fields, templates, resources.
- Schema and migration changes.
- Deliberate dependency add/remove/upgrade (call this out as "Project hygiene" if cleanup-only).
- Build/project-file changes that affect what compiles or what a generator emits.
- New test coverage, as **one** bullet naming what is now covered — never one bullet per test file.
- The submodule pointer bump, as a single bullet summarising what the submodule change delivers — not a raw commit SHA diff. The detail belongs in the submodule PR.

Each PR gets its **own** Summary and Changes, scoped to that repo's diff. Do not copy the parent's Changes list into the submodule PR or vice versa. The submodule PR's Summary should still make sense standalone, then note in one clause that it is consumed by the parent branch.

**Titles** — `<TICKET-KEY> <imperative description>` if a ticket exists, otherwise `<imperative description>`. Match the repo's existing PR title style (an emoji prefix, brackets around the key) — check `git log <base> --oneline -10` for cues. Keep under 70 chars. Two PRs share the ticket key but each title describes its own change.

### 5. Create the PRs — submodule first

Write each body to a temporary file and pass it with `--body-file`, so no shell mangles backticks or `$`. Run `gh` with `-R <owner>/<repo>` so the target is unambiguous whatever the current folder.

**5a. The submodule PR** (skip if reusing an open one, or if there's none). Leave the cross-link out for now:

```
gh pr create -R <owner>/<submodule-repo> --base <sub-base> --head <sub-branch> --title "<sub-title>" --body-file <file>
```

with the body:

```
## Summary

<two plain-English sentences>

## Changes

- **`path/to/file`** — <change, one sentence>
```

**5b. This repo's PR**, with the submodule PR link, if there is one, as the first line of the body:

```
gh pr create -R <owner>/<repo> --base <base> --head <branch> --title "<title>" --body-file <file>
```

```
**Submodule PR:** https://github.com/<owner>/<submodule-repo>/pull/<N>

## Summary

<two plain-English sentences>

## Changes

- **`path/to/file`** — <change, one sentence>
- **`<submodule-path>`** — <what the submodule bump brings in>
```

**5c. Backlink the submodule PR** to this one by prepending `**Parent PR:** <url>` to its body and writing it back with `gh pr edit <N> -R <owner>/<submodule-repo> --body-file <file>`, which replaces the body wholesale. If reusing an existing submodule PR, read its body first (`gh pr view <N> -R <repo> --json body`), prepend or update the `**Parent PR:**` line, and write the whole thing back — do not clobber a body someone else wrote.

If this repo's PR fails to create after the submodule PR succeeded, say so plainly and give the user the submodule PR URL.

### 6. Tag the branch in Overworked

Once this repo's PR exists (created or reused), tag its branch so the app shows a "PR #n" pill on the row:

```
ow pr <repo> <branch> <PR url>
```

- Tag only this repo's PR, never the submodule's: the submodule branch isn't a worktree Overworked lists.
- If `ow` isn't installed or errors (e.g. the repo isn't under its root), skip and say so in one line. This step never fails the PR.

### 7. Report

Relay the PR URL(s) to the user as the final message, labelled (parent / submodule) when there are two. Then add one line reminding them the submodule PR merges first — otherwise the parent's pointer references a commit that isn't on the submodule's base branch yet. No further commentary unless something failed.

## What NOT to do

- Do not add a `## Test plan` section. Ever.
- Do not add `## Notes for reviewers`, `## Routing matrix`, `## Acceptance criteria`, or any other section beyond Summary and Changes.
- Do not add the merge-order reminder to the PR body — it goes in the chat report only.
- Do not list every line of the diff — group and summarise.
- Do not give a bullet to a file that only changed to keep the build green.
- Do not explain why a mechanical change was needed — fold it into the bullet it serves, or drop it.
- Do not pad the Summary with background, statistics, or how the problem was discovered.
- Do not write multi-sentence bullets. One sentence each.
- Do not write the Summary in implementation jargon. Plain English, always.
- Do not append a "Generated with Claude Code" trailer to either PR body.
- Do not push commits, amend, or modify either branch — only open PRs for what is already there. The single exception is pushing an unpushed submodule branch, and only after the user agrees.
- Do not open a submodule PR when the pointer only moved onto commits already merged into the submodule's base.
- Do not remove the worktree or delete the branch after the PRs open — leave both in place.
