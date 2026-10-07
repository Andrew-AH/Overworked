namespace Overworked.Core;

public static class Plural
{
    /// <summary>"1 commit", "2 commits". Only for nouns that take a plain "s".</summary>
    public static string Of(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
