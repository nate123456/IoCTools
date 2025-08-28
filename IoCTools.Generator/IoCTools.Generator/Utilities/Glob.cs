namespace IoCTools.Generator.Utilities;

using System;
using System.Text.RegularExpressions;

internal static class Glob
{
    // Simple glob: * => any sequence, ? => single char. Case-sensitive.
    public static bool IsMatch(string text,
        string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return string.Equals(text, pattern, StringComparison.Ordinal);
        var regex = '^' + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + '$';
        return Regex.IsMatch(text, regex);
    }
}
