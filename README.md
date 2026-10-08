# Overworked

A small desktop app (Windows + macOS) for juggling git worktrees across many repos.

Point it at the folder that holds your repos. Every direct subfolder with a `.git` directory is a
main repo; its worktrees show underneath it. New worktrees go in `<root>/.worktrees/<repo>/<branch>`
and new branches start from `origin/main`.

- **Double-click** a row to open it in VS Code.
- **Right-click** for: open in VS Code / folder / terminal, new worktree, tag ticket / pull request, remove worktree.
- The chevron on a repo collapses or expands its worktrees.
- **+ (or New worktree…)** opens the same branch search: pick any branch, local or on origin, to open it as a
  worktree, or type a new name to create one from origin/main. Worktree rows show just their branch.
- **Right-click a main repo** for its menu, topped by a "Switch branch" search box that changes the main folder's
  branch in place (no worktree involved). Click into it to list every
  branch, type to filter (VS Code style matching); ↑/↓ and Enter pick one, Esc closes. It fetches origin while open;
  remote-only branches become local tracking branches, and a new name creates a branch from origin/main. Branches
  open in a worktree are shown but can't be picked. Worktrees don't offer switching: they stay on the branch they
  were made for.
- Removing a worktree keeps its branch, and refuses to drop uncommitted changes or unpushed submodule
  commits without confirmation. (A linked worktree's submodule data is deleted with the worktree.)
  If VS Code or a terminal still has the folder open, the work is removed and the empty folder is
  deleted on a later refresh, once it's let go.
- Tag a row's branch with its ticket or pull request from its right-click menu (**Tag ticket…**, **Tag pull request…**:
  paste the link), or with `ow ticket` / `ow pr`. A branch can have both. Opening the menu item again shows the link,
  to change it.
- A row whose branch has a pull request shows a **PR #n** pill; click it to open the PR.
- A row whose branch has a ticket shows its key, e.g. **PROJ-123**,
  in that ticket's colour. Every ticket in view gets its own colour, shared by every repo working on it, so one ticket's
  worktrees read as a group. Click it to open the ticket.
- The tree updates on its own when worktrees change, including ones made from the CLI.

Git is the source of truth. Overworked only stores the root folder and where the app lives
(`%APPDATA%\Overworked\config.json` on Windows, `~/.config/Overworked/config.json` on macOS).
Tags live in the repo's git config as `branch.<name>.overworked-pr` and `branch.<name>.overworked-ticket`, so
deleting a branch drops its tags.

## CLI (`ow`)

The same operations, for scripts and for Claude (the global `managing-worktrees` skill tells Claude to use it):

```
ow app                               open the Overworked app (no-op if it's already open)
ow root [folder]                     show or set the folder that holds your repos
ow list [repo]                       repos and their worktrees (one repo is faster)
ow path <repo> [branch]              folder of the main checkout, or of a branch's worktree
ow branches <repo> [--fetch]         branches, newest first, and where each is checked out
ow new <repo> <branch>               open a branch as a worktree (a new name branches from origin/main)
ow switch <repo> <branch> [--create] switch the main folder's branch (--create: new branch from origin/main)
ow remove <repo> <branch> [--force]  remove a worktree; refuses if work would be lost unless --force
ow pr <repo> <branch> <url>          tag a worktree's branch with its pull request (the app shows it)
ow ticket <repo> <branch> <url>      tag a worktree's branch with its ticket (the app shows its key, one colour per ticket)
```

Add `--json` to any command for machine-readable output. Errors go to stderr with a non-zero exit code.

Install it as a global tool (same on Windows and macOS). Bump `<Version>` in the csproj before updating:

```
dotnet pack src/Overworked.Cli -c Release -o nupkg
dotnet tool install -g Overworked.Cli --add-source ./nupkg --ignore-failed-sources   # or: dotnet tool update -g ...
```

## Build and run

```
dotnet run --project src/Overworked.App
dotnet test
```

Publish a single executable on Windows:

```
dotnet publish src/Overworked.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish/win

# (Skia/HarfBuzz .pdb files still land next to the exe; they're not needed and can be deleted.)
```

## On a Mac

Build the app and its disk image on the Mac itself: it needs Apple's tools (`hdiutil`, `iconutil`, `codesign`), and
Apple silicon only runs code signed on a Mac. You need the .NET 9 SDK (`brew install --cask dotnet-sdk`) and git.

```
git clone <this repo> && cd Overworked
./mac/package.sh              # Intel Mac: ./mac/package.sh osx-x64
open publish/mac/Overworked-*.dmg
```

Drag Overworked into Applications and open it from there; it remembers where it lives, so `ow app` opens it after
that. Install `ow` as above, and put `~/.dotnet/tools` on your PATH if `dotnet tool install` asks you to.

The app is signed ad hoc, which is fine on the Mac that built it. Copied to another Mac it's quarantined: allow it in
System Settings → Privacy & Security → Open Anyway, or run `xattr -dr com.apple.quarantine /Applications/Overworked.app`.
Handing it out properly would need an Apple Developer ID and notarisation.

Apps started from Finder or the Dock don't get your shell's PATH, so Overworked (and `ow`) add `/opt/homebrew/bin`
and `/usr/local/bin` to find git.

## Layout

```
src/Overworked.Core            scanning, git worktree operations, config (no UI)
  Workspace                    the repos folder: load repos; create, remove and tidy worktrees
  Branches, Worktrees,         each pairs its git commands with a pure Parse of their output
  Submodules, Tags
  Repo, Worktree, BranchRef…   plain records
src/Overworked.App             Avalonia UI; Platform.cs is the only OS-specific file
  ViewModels/                  state and every change to a repo (MainViewModel), rows, branch search
  Views/                       the window, menus and dialogs: only what needs a view
src/Overworked.Cli             the `ow` command; JsonOutput.cs is the --json contract
mac/package.sh                 builds Overworked.app and its .dmg (run on a Mac)
tests/Overworked.Core.Tests    real git against throwaway repos, plus parser tests
tests/Overworked.App.Tests     branch search, ticket colours
```
