namespace Overworked.Core;

/// <summary>An expected failure (git refused, bad input) whose message is fit to show the user as it is.</summary>
public sealed class OverworkedException(string message) : Exception(message);
