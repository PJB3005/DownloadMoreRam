using System.Text.RegularExpressions;

namespace DownloadMoreRam;

public static partial class Helpers
{
    public static long? GetSizeMB(string input)
    {
        var match = SizeRegex().Match(input);
        if (!match.Success)
            return null;

        if (!long.TryParse(match.Groups[1].Value, out var size))
            return null;

        switch (match.Groups[2].Value)
        {
            case "GB":
                size *= 1024;
                break;
            case "TB":
                size *= 1024 * 1024;
                break;
        }

        return size;
    }

    [GeneratedRegex(@"(\d+)(MB|GB|TB)")]
    private static partial Regex SizeRegex();
}