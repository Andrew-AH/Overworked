namespace Overworked.Core;

/// <summary>What <see cref="Workspace.CleanUp"/> did.</summary>
/// <param name="FolderRemoved">False when the empty folder stayed because something still has it open.</param>
/// <param name="BranchDeleted">False when the branch stayed: it may hold commits origin never got.</param>
public sealed record CleanUpResult(bool FolderRemoved, bool BranchDeleted);
