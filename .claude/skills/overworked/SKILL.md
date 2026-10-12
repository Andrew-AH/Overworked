---
name: overworked
description: Use alongside any task that touches git branches, worktrees, tickets or pull requests in repos managed by Overworked (the `ow` CLI), whatever skill or workflow does the task itself — opening a PR (a PR skill, gh pr create, anything), creating or picking up a ticket, creating, switching or removing branches and worktrees, merging. Keeps Overworked in step on the side.
---

# Overworked

The user tracks work in **Overworked**: a desktop app plus the `ow` CLI, showing every repo in one folder (`ow root` prints it) with its worktrees and their ticket and PR tags. It updates live.

**Do the task exactly as asked, with whatever skill or workflow it calls for; Overworked never changes how.** Alongside each step below, also make the matching `ow` update, and mention it in one line at the end ("Tagged proj-123 with PR #84 in Overworked"). If `ow` is missing or fails, say so in one line and carry on: it never blocks the task.

## Follow along

| When the task… | Also |
|---|---|
| needs a new worktree | make it with `ow new <repo> <branch>` rather than raw `git worktree`, so it sits where the app shows it, and work in the folder it prints |
| creates a branch or worktree for a ticket | check `ow list <repo>` shows the ticket. `ow new` tags a branch named after one by itself once any ticket of that project is tagged; otherwise `ow ticket <repo> <branch> <ticket link>`, a link naming the key (Jira's `/browse/PROJ-123`, or a board with `?selectedIssue=PROJ-123`) |
| creates a ticket for work already on a branch | `ow ticket <repo> <branch> <ticket link>` |
| opens a PR, any way | `ow pr <repo> <branch> <PR url>` |
| ends with a PR merged | nothing: the app flags it for Clean up. Run `ow cleanup [repo] [branch]` only when the user asks |

`<repo>` is the main checkout's folder name under the root; from inside a worktree, the parent folder of `git rev-parse --path-format=absolute --git-common-dir`. When it's up to you to name a branch for a ticket, `<key lowercase>-<2–5 word slug>` (e.g. `proj-123-retry-failed-payments`) lets `ow` tag it by itself.

## Commands

`ow list [repo]` (`--json` to parse) · `ow path <repo> [branch]` · `ow branches <repo> [--fetch]` · `ow new <repo> <branch>` · `ow switch <repo> <branch> [--create]` · `ow remove <repo> <branch>` · `ow cleanup [repo] [branch]` · `ow ticket|pr <repo> <branch> <url>` · `ow app`

## Never on Overworked's account

- Remove a worktree or delete a branch: only when the user asks (`ow remove` keeps the branch; `--force` only on the user's say-so).
- Put a `.worktreeinclude`, or any Overworked file, inside a repo, or gitignore one there. Local files for new worktrees are listed in `<root>/.worktrees/<repo>/.worktreeinclude`.
