namespace Penopticon;

/// <summary>
/// Parses the user-entered list of process names that should get mouse input.
/// </summary>
internal static class AppList
{
    private static readonly char[] Separators = { ',', ';', '\r', '\n' };

    /// <summary>
    /// Accepts names separated by commas, semicolons or new lines. Case-insensitive;
    /// a trailing ".exe" is optional ("Illustrator" and "illustrator.exe" are the same).
    /// </summary>
    public static HashSet<string> Parse(string? raw)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
            return names;

        foreach (var part in raw.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = part.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? part[..^4].TrimEnd() : part;
            if (name.Length > 0)
                names.Add(name);
        }
        return names;
    }
}
