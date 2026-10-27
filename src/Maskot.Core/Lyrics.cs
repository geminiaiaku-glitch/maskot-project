using System.Globalization;
using System.Text.RegularExpressions;

namespace Maskot.Core;

public sealed record LyricLine(TimeSpan At, string Text);
public static partial class LrcParser
{
    [GeneratedRegex(@"\[(\d+):(\d{2}(?:\.\d+)?)\]")]
    private static partial Regex Timestamp();
    [GeneratedRegex(@"\[offset:([+-]?\d+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex Offset();
    public static IReadOnlyList<LyricLine> Parse(string lrc)
    {
        var offset = Offset().Match(lrc);
        var milliseconds = offset.Success && int.TryParse(offset.Groups[1].Value, out var ms) ? ms : 0;
        var lines = new List<LyricLine>();
        foreach (var row in lrc.Split('\n'))
        {
            var matches = Timestamp().Matches(row);
            if (matches.Count == 0) continue;
            var text = row[(matches[^1].Index + matches[^1].Length)..].Trim();
            foreach (Match m in matches)
            {
                if (!int.TryParse(m.Groups[1].Value, out var minutes) ||
                    !double.TryParse(m.Groups[2].Value, CultureInfo.InvariantCulture, out var seconds)) continue;
                lines.Add(new(TimeSpan.FromSeconds(minutes * 60.0 + seconds) + TimeSpan.FromMilliseconds(milliseconds), text));
            }
        }
        return lines.OrderBy(l => l.At).ToArray();
    }
    public static string Current(IReadOnlyList<LyricLine> lines, TimeSpan position) =>
        lines.LastOrDefault(l => l.At <= position)?.Text ?? "";
}
