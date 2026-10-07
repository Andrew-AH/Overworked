using System.Diagnostics;
using System.Text.Json;
using Overworked.Cli;
using Overworked.Core;

const string Usage = """
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

    Add --json to any command for machine-readable output.
    """;

Homebrew.AddToPath();

var json = args.Contains("--json");
var force = args.Contains("--force");
var positional = args.Where(a => !a.StartsWith("--")).ToArray();
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

try
{
    switch (positional)
    {
        case ["app"]:
            OpenApp();
            break;

        case ["root"]:
            Print(new { root = RequireRoot() }, RequireRoot());
            break;

        case ["root", var folder]:
            if (!Directory.Exists(folder)) throw new OverworkedException($"No such folder: {folder}");
            Config.SaveRoot(folder);
            Print(new { root = Config.LoadRoot() }, $"Root set to {Config.LoadRoot()}");
            break;

        case ["list"]:
        {
            var repos = OpenWorkspace().LoadRepos();
            if (json) Print(repos.Select(RepoJson.From).ToList(), "");
            else foreach (var repo in repos) PrintRepo(repo);
            break;
        }

        case ["list", var repoName]:
        {
            var repo = OpenWorkspace().FindRepo(repoName);
            if (json) Print(RepoJson.From(repo), "");
            else PrintRepo(repo);
            break;
        }

        case ["path", var repoName]:
        {
            var repo = OpenWorkspace().FindRepo(repoName);
            Print(new { path = repo.Path }, repo.Path);
            break;
        }

        case ["path", var repoName, var branch]:
        {
            var worktree = FindWorktree(OpenWorkspace().FindRepo(repoName), branch);
            Print(new { path = worktree.Path }, worktree.Path);
            break;
        }

        case ["branches", var repoName]:
        {
            var repo = OpenWorkspace().FindRepo(repoName);
            if (args.Contains("--fetch")) Branches.Fetch(repo.Path);
            var branches = Branches.List(repo.Path);
            if (json) Print(branches.Select(BranchJson.From).ToList(), "");
            else foreach (var b in branches)
                Console.WriteLine($"  {b.Name,-50} {(b.IsRemote ? "origin" : ""),-7} {b.CheckedOutAt}");
            break;
        }

        case ["new", var repoName, var branch]:
        {
            var workspace = OpenWorkspace();
            var worktree = workspace.CreateWorktree(workspace.FindRepo(repoName), branch);
            Print(WorktreeJson.From(worktree), worktree.Path);
            break;
        }

        case ["switch", var repoName, var branch]:
        {
            var repo = OpenWorkspace().FindRepo(repoName);
            Branches.Switch(repo.Path, branch, create: args.Contains("--create"));
            Print(new { repo = repo.Name, path = repo.Path, branch }, $"{repo.Name} is now on {branch}");
            break;
        }

        case ["remove", var repoName, var name]:
        {
            var workspace = OpenWorkspace();
            var repo = workspace.FindRepo(repoName);
            var worktree = FindWorktree(repo, name);
            if (worktree.IsMain) throw new OverworkedException("That's the main checkout, not a worktree.");
            var folderGone = workspace.RemoveWorktree(repo, worktree, force);
            Print(new { removed = worktree.Path, branch = worktree.Branch, folderLeftBehind = !folderGone },
                folderGone ? $"Removed {worktree.Path}"
                    : $"Removed {worktree.Path}. Its empty folder stays until whatever has it open (VS Code, a terminal) closes; Overworked deletes it then.");
            break;
        }

        case ["pr", var repoName, var name, var url]:
        {
            var (repo, branch) = TagBranch(repoName, name, TagKind.PullRequest, url);
            Print(new { repo = repo.Name, branch, pullRequest = url }, $"Tagged {branch} with {url}");
            break;
        }

        case ["ticket", var repoName, var name, var url]:
        {
            var (repo, branch) = TagBranch(repoName, name, TagKind.Ticket, url);
            Print(new { repo = repo.Name, branch, ticket = url }, $"Tagged {branch} with {url}");
            break;
        }

        default:
            Console.Error.WriteLine(Usage);
            return 2;
    }
    return 0;
}
catch (OverworkedException e)
{
    if (json) Console.Error.WriteLine(JsonSerializer.Serialize(new { error = e.Message }));
    else Console.Error.WriteLine(e.Message);
    return 1;
}

void Print(object value, string text) => Console.WriteLine(json ? JsonSerializer.Serialize(value, jsonOptions) : text);

void PrintRepo(Repo repo)
{
    Console.WriteLine(repo.Error is null ? repo.Name : $"{repo.Name}  (error: {repo.Error})");
    foreach (var w in repo.Worktrees)
    {
        var state = w.IsMissing ? "missing" : w.ChangedFiles > 0 ? $"{w.ChangedFiles} changed" : "";
        Console.WriteLine($"  {(w.IsMain ? "*" : "-")} {w.Branch ?? "(detached)",-45} {state,-11} {w.TicketKey,-10} {(w.PullRequestUrl is null ? "" : "PR"),-2} {w.Path}");
    }
}

// The app records where it lives each time it starts, so this can open it from anywhere.
void OpenApp()
{
    if (Process.GetProcessesByName("Overworked").Length > 0)
    {
        Print(new { running = true, started = false }, "Overworked is already open");
        return;
    }
    var app = Config.LoadAppPath();
    if (app is null || !File.Exists(app))
        throw new OverworkedException("Don't know where the Overworked app is yet. Open it once by hand and ow will remember.");
    // On macOS open the .app around the executable: opening the executable itself runs it in a Terminal window.
    var bundle = app.IndexOf(".app/", StringComparison.Ordinal) is var at and >= 0 ? app[..(at + ".app".Length)] : null;
    Process.Start(new ProcessStartInfo(OperatingSystem.IsMacOS() && bundle is not null ? bundle : app) { UseShellExecute = true })?.Dispose();
    Print(new { running = true, started = true }, "Opened Overworked");
}

static string RequireRoot() =>
    Config.LoadRoot() ?? throw new OverworkedException("No root folder set. Run: ow root <folder>");

static Workspace OpenWorkspace() => new(RequireRoot());

/// <summary>Tags the branch checked out in a repo's worktree with a link.</summary>
static (Repo Repo, string Branch) TagBranch(string repoName, string worktreeName, TagKind kind, string url)
{
    var repo = OpenWorkspace().FindRepo(repoName);
    var branch = FindWorktree(repo, worktreeName).Branch
        ?? throw new OverworkedException($"'{worktreeName}' is on a detached HEAD, so there's no branch to tag.");
    Tags.Set(repo.Path, branch, kind, url);
    return (repo, branch);
}

/// <summary>A worktree by branch, folder name or path.</summary>
static Worktree FindWorktree(Repo repo, string name) =>
    repo.Worktrees.FirstOrDefault(w =>
        string.Equals(w.Branch, name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(w.FolderName, name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(w.Path, Path.GetFullPath(name), StringComparison.OrdinalIgnoreCase))
    ?? throw new OverworkedException($"No worktree for '{name}' in {repo.Name}. `ow new {repo.Name} {name}` opens one.");
