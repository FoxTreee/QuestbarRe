using System.Text.RegularExpressions;

public static class ContentId
{
    private static readonly Regex ValidPattern =
        new(
            "^[a-z][a-z0-9_]*" +
            "(\\.[a-z][a-z0-9_]*){2,}$",
            RegexOptions.Compiled);

    /// <summary>
    /// Performs the is valid operation for Content ID.
    /// Uses the supplied arguments and current state and returns the resulting bool to the caller.
    /// </summary>
    public static bool IsValid(
        string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && ValidPattern.IsMatch(value);
    }
}