using System.Text.RegularExpressions;

namespace UABEANext4.Util;

public static class SearchUtils
{
    // cheap * search check
    public static bool WildcardMatches(string test, string pattern, bool caseSensitive = true)
    {
        RegexOptions options = 0;
        if (!caseSensitive)
            options |= RegexOptions.IgnoreCase;

        return Regex.IsMatch(test, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", options);
    }
}
