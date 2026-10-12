---
name: jira
description: Create a Jira ticket (story, bug or task), or take an existing one, then open an Overworked worktree for it in each repo the user picks, named after the ticket (e.g. proj-123-retry-failed-payments) and tagged with it, so work can start straight away. Use when the user wants a Jira story, bug or task written or created, or wants to start work on one — "/jira", "card this up", "log a bug for...", "write me a story for...", "create a ticket for...", "start work on PROJ-123". Arguments: [bug|story|task] [PROJECT_KEY] ["custom title"], or an existing ticket's link or key.
---

# Jira ticket + worktrees

## 1. Get the ticket

**An existing ticket** (the user gave its link or key): use it, and go to step 2. A bare key needs its link: take the site from any ticket already tagged in Overworked (`ow list --json`, a worktree's `ticket`) and make `https://<site>/browse/<KEY>`; if none is tagged, ask for the link.

**A new ticket** needs Jira tools in this session (e.g. the Atlassian MCP). If there are none, say so, give the draft below as text to paste into Jira, and ask for the new ticket's link.

1. Work out the type (story, bug or task; story by default), the project key (from the arguments, else ask; the Jira tools can list the projects the user sees) and what it's about. Ask only for what the conversation doesn't already say.
2. Draft it, sized to the work: only the parts that earn their keep.
   - **Title**: short and specific; for a bug, the symptom. Use the custom title if one was given.
   - **Story**: what's needed and why, in a few sentences, then acceptance criteria as a short checklist.
   - **Bug**: what happens, what should happen, how to reproduce it, and where it was seen.
   - **Task**: what needs doing, and how to tell it's done.
3. Show the draft, and **in the same message** ask the repo question from step 2, so one reply approves the ticket and picks the repos.
4. Once approved, create it (with the Atlassian MCP: `getAccessibleAtlassianResources` for the site's cloudId, then `createJiraIssue` with the project key, issue type, title and a Markdown description). Note the new key and its link.

## 2. Ask which repos need a worktree

Run `ow root`. If `ow` isn't found, say so and skip the worktrees; the ticket still stands. Otherwise run `ow list --json` and show every repo as a numbered list:

```
Which repos need a worktree for this ticket? Reply with numbers or names (e.g. "3 7"), or "none".
Branch will be <key>-retry-failed-payments; give a name if you want a different one.
 1. api
 2. billing-service
 ...
```

Use a plain numbered list, not a multiple-choice question tool: those cap at a few options, and there are usually more repos than that.

In the "Branch will be" line, show the actual slug you'll use (for a new ticket the key isn't known until it exists, so write `<key>` for it). The slug is 2–5 words that say what the work is, taken from the title: lowercase, a–z/0–9 and hyphens only, about 40 characters at most, filler words dropped. The user has to recognise the branch from its name alone, so the bare key isn't enough. Examples: `proj-123-retry-failed-payments`, `proj-98-wrong-shipping-total`.

## 3. Open the worktrees

Once the ticket exists and the user picked at least one repo:

1. `ow app` — opens the Overworked app if it's closed, so the user sees the worktrees appear.
2. For each picked repo: `ow new <repo> <branch>`, where `<branch>` is the key lowercased plus the slug (`PROJ-123` → `proj-123-retry-failed-payments`) unless the user gave another name. If one repo fails, report its error and carry on with the rest.
3. Tag each new worktree with the ticket: `ow ticket <repo> <branch> <ticket link>`. The app then shows the key on every row for this ticket, in a colour of its own, and `/pr` picks it up for the PR's title and summary. If `ow ticket` fails, say so in one line; the worktrees still stand.
4. Finish with the ticket link and one line per worktree: repo → the folder `ow new` printed.

Worktree work goes through `ow` only, never raw `git worktree` (see the managing-worktrees skill). To bring another repo into the ticket later, the app's "Add a repo to PROJ-123…" does steps 2–3 for that repo.
