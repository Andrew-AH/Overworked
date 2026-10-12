<p align="center"><img src="src/Overworked.App/Assets/logo-256.png" width="96" alt=""></p>

<h1 align="center">Overworked</h1>

<p align="center">One window for every repo and git worktree you're juggling. Windows and macOS.</p>

<p align="center"><img src="docs/main.png" width="460" alt="Overworked listing five repos with their worktrees, ticket tags and pull requests"></p>

Git worktrees let you work on several branches at once, a folder each, but they pile up across repos. Overworked lists
every repo in your code folder with its worktrees underneath, and keeps the list current as you work.

- **Tickets and PRs on every row.** Tag a worktree with its ticket and pull request; each ticket gets its own colour.
- **Nothing lost.** Uncommitted changes and unpushed commits show on each row, and removing a worktree asks first.
- **Tidy after merging.** Once a PR's branch is deleted on origin, **Clean up** removes its worktree in one click.
- **Ready to run.** New worktrees get copies of your local config files, like `appsettings.local.json`.
- **`ow` on the command line** does the same things, for scripts and AI agents like Claude Code.

## Install

Download `Overworked.exe` (Windows) or the `.dmg` (Apple silicon Mac) from the
[latest release](https://github.com/Andrew-AH/Overworked/releases/latest).

Or build it, with [git](https://git-scm.com/downloads) and the
[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):

```powershell
# Windows: builds publish\win\Overworked.exe
git clone https://github.com/Andrew-AH/Overworked.git
cd Overworked
dotnet publish src/Overworked.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish/win
```

```bash
# macOS: builds a .dmg (on an Intel Mac: ./mac/package.sh osx-x64)
git clone https://github.com/Andrew-AH/Overworked.git
cd Overworked
./mac/package.sh
open publish/mac/Overworked-*.dmg
```

To add the `ow` command, from the same folder:

```
dotnet pack src/Overworked.Cli -c Release -o nupkg
dotnet tool install -g Overworked.Cli --add-source ./nupkg
```

On first run, click **Choose folder** and pick the folder your repos live in. New worktrees go in
`.worktrees/<repo>/<branch>` inside it.

## Using it

| Do this | To |
|---|---|
| **Double-click** a row | open it in your editor |
| **+** on a repo | open a branch as a worktree, or type a new name to create one |
| **Right-click** a row | copy its branch, tag it, work with its ticket, remove it; on a repo, switch its branch |
| **Click** a tag | open the ticket or PR; **right-click** it to copy the link |
| **Clean up** | after a merge, remove the worktree, and its branch if origin had all of it |
| **↻** | refresh now; it also refreshes by itself every minute |

<table>
  <tr>
    <td><img src="docs/menu.png" alt="The right-click menu on a worktree"></td>
    <td><img src="docs/branches.png" alt="Picking a branch to open as a worktree"></td>
  </tr>
  <tr>
    <td align="center">Right-click a worktree</td>
    <td align="center">+ opens a branch as a worktree</td>
  </tr>
</table>

### Tags

Right-click a row, choose **Tag ticket…** or **Tag pull request…**, and paste the link. Ticket links can come from
Jira or any tracker with keys like `PROJ-123`.

Once one ticket of a project is tagged, a new worktree named after another, like `proj-124-add-refunds`, is tagged
with it automatically.

<p align="center"><img src="docs/tag.png" width="400" alt="The Tag ticket dialog"></p>

## Command line

`ow` does what the app does, and the app updates as soon as it changes something. Add `--json` for output a script
can read.

```
ow app                               open the app
ow root [folder]                     show or set the folder that holds your repos
ow list [repo]                       repos and their worktrees
ow path <repo> [branch]              folder of a repo, or of a branch's worktree
ow branches <repo> [--fetch]         branches, newest first, and where each is checked out
ow new <repo> <branch>               open a branch as a worktree
ow switch <repo> <branch> [--create] switch a repo's own branch
ow remove <repo> <branch> [--force]  remove a worktree; refuses if work would be lost unless --force
ow cleanup [repo] [branch]           clean up every branch deleted on origin, as Clean up does
ow pr <repo> <branch> <url>          tag a branch with its pull request
ow ticket <repo> <branch> <url>      tag a branch with its ticket
```

## Claude Code

The [`overworked` skill](.claude/skills/overworked/SKILL.md) has Claude Code keep Overworked up to date as it works,
without changing how you work: it makes worktrees with `ow` and tags branches with their ticket and PR.

It's active in this repo. To use it in every repo, copy it into your personal skills:

```powershell
# Windows
New-Item -ItemType Directory -Force "$HOME\.claude\skills" | Out-Null
Copy-Item -Recurse -Force .claude\skills\overworked "$HOME\.claude\skills\"
```

```bash
# macOS
mkdir -p ~/.claude/skills && cp -R .claude/skills/overworked ~/.claude/skills/
```

So Claude uses it on every relevant task, also add this to `~/.claude/CLAUDE.md`, with your repos folder:

```markdown
When a task touches tickets, branches, worktrees or PRs in `C:\code`, do it however it's asked, and also keep
Overworked (`ow`) in step on the side: load the `overworked` skill.
```

Both apply from your next Claude Code session. Copy the skill again to update it.

## Good to know

- Overworked keeps no data of its own: tags live in each repo's git config. Its settings file
  (`%APPDATA%\Overworked\config.json`, or `~/.config/Overworked/config.json` on macOS) holds your repos folder and,
  optionally, `"Editor"` to open rows in something other than VS Code (`"cursor"` on Windows, `"Cursor"` on macOS).
- **Clean up** never loses work: it refuses while there are uncommitted changes, and keeps the branch unless origin had
  all of it. On a repo's own folder, it switches back to the default branch instead.
- On Windows, **Clean up** and removing a worktree close its VS Code window first. They find it by title, so if two
  repos have a folder of the same name, set VS Code's `"window.title"` to
  `"${activeRepositoryName}${separator}${activeRepositoryBranchName}"`.
- To copy local files into new worktrees, list them in `.worktrees/<repo>/.worktreeinclude` under your repos folder,
  in `.gitignore` syntax (e.g. `**/appsettings.local.json`). Only files git ignores are copied.
- The Mac build isn't signed with an Apple Developer ID, so the first time you open it, allow it under
  System Settings → Privacy & Security → Open Anyway.

## Development

```
dotnet test
```

To update your installed `ow`, bump `<Version>` in `Directory.Build.props`, pack again, and run
`dotnet tool update -g Overworked.Cli --add-source ./nupkg`. To release, bump the version and push a tag like
`v0.7.0`: a workflow builds both apps and attaches them to a GitHub release.

```
src/Overworked.Core    git operations, scanning, tags, config (no UI)
src/Overworked.App     the Avalonia app
src/Overworked.Cli     the `ow` command; JsonOutput.cs is its --json contract
mac/package.sh         builds Overworked.app and its .dmg
tests/                 real git against throwaway repos, plus unit tests
```
