---
name: managing-worktrees
description: Use when creating, opening, listing, finding, switching or removing git worktrees, or switching a repo's branch, in repos managed by Overworked (the `ow` CLI) — e.g. "spin up a worktree for PROJ-123", "work on X alongside my branch", "which worktrees do I have", "where's the worktree for X", "clean up that worktree", "tidy up what's been merged".
---

# Managing worktrees with Overworked

## Overview

The user manages worktrees with **Overworked**: a desktop app plus the `ow` CLI, sharing one set of rules. Do worktree work through `ow`, never raw `git worktree`. `ow` puts worktrees where the app shows them (`<root>/.worktrees/<repo>/<branch>`, not loose beside the repos), branches new work from `origin/main` (or `origin/master`), initialises submodules, copies in the repo's local files, and refuses to remove a worktree when that would lose work. The app updates live, so the user sees what you do.

## Before the first worktree command

1. `ow root` — proves `ow` is installed and prints the folder that holds the repos. If `ow` is not found, tell the user and stop; don't fall back to raw git (it installs as a .NET tool, see Overworked's README).
2. `ow app` — opens the app if it's closed (does nothing if open).

## Commands

| Need | Command |
|---|---|
| What exists | `ow list [repo]` — each checkout with its branch, "N changed", "N unpushed", "gone" (deleted on origin), ticket and PR |
| A folder | `ow path <repo> [branch]` (no branch = the repo's own folder) |
| Branches + where each is checked out | `ow branches <repo> [--fetch]` |
| Open a branch as a worktree | `ow new <repo> <branch>` — existing branch (local or origin) is checked out; a new name branches from origin/main. Prints the folder. |
| Change the repo's own folder's branch | `ow switch <repo> <branch> [--create]` — never makes a worktree |
| Remove a worktree | `ow remove <repo> <branch>` — keeps the branch |
| Tidy up after PRs merge | `ow cleanup [repo] [branch]` — fetches origin, then for each branch deleted there removes its worktree (or switches the repo's own folder back to main) and deletes the branch only if origin had all of it. Refuses where there's uncommitted work and carries on with the rest. |
| Tag a branch with its PR | `ow pr <repo> <branch> <PR url>` — the app shows "PR #n"; `/pr` does this itself |
| Tag a branch with its ticket | `ow ticket <repo> <branch> <ticket url>` — any link that names a key like PROJ-123 (Jira's `/browse/PROJ-123`, or a board with `?selectedIssue=PROJ-123`); the app shows the key in that ticket's colour; `/jira` does this itself |

`<repo>` is the folder name under the root (see `ow list`). Add `--json` when you need to parse output.

## Rules

- Work in the folder `ow new` prints, or look it up with `ow path`. Don't guess paths.
- If `ow remove` refuses (uncommitted changes, unpushed submodule commits), show the user its message. Add `--force` only when the user says to.
- Removing a worktree never means deleting its branch; a PR may still need it. The one exception is `ow cleanup`, which deletes only branches whose PR was merged (deleted on origin) and that origin had in full. Run it when the user asks to tidy merged work, and show its output, including anything it refused.
- "Its empty folder stays until…" after a remove is normal on Windows (an editor or terminal still has it open). Overworked deletes the folder later; nothing to do.
- Local files a worktree needs but git ignores (appsettings.local.json, .vscode/) are copied in from `<root>/.worktrees/<repo>/.worktreeinclude`, in gitignore syntax. Overworked never reads one from inside a repo, so never create a `.worktreeinclude` (or any other Overworked file) in a repo, and never gitignore one there.

## Common mistakes

| Instead of | Do |
|---|---|
| `git worktree add ../repo-proj-123 …` | `ow new <repo> PROJ-123` |
| `git worktree remove --force …` | `ow remove <repo> <branch>`; `--force` only on the user's say-so |
| `git branch -D` after removing | Keep the branch; once its PR is merged, `ow cleanup` deletes it if that's safe |
| `git switch` in the repo's own folder via a new worktree | `ow switch <repo> <branch>` |
