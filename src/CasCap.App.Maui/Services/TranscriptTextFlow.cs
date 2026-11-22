using System.Globalization;

namespace CasCap.Services;

/// <summary>Finds a lossless text boundary for lines beside a floating action.</summary>
internal static class TranscriptTextFlow
{
    /// <summary>Finds the longest fitting prefix, preferring whitespace and never splitting a text element.</summary>
    /// <param name="text">The full utterance.</param>
    /// <param name="fits">Measures whether a prefix fits beside the action.</param>
    /// <returns>The UTF-16 offset at which full-width continuation starts.</returns>
    public static int FindSplit(string text, Func<string, bool> fits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(fits);
        if (text.Length == 0 || fits(text))
            return text.Length;
        var starts = StringInfo.ParseCombiningCharacters(text);
        var low = 1;
        var high = starts.Length;
        var fitted = 0;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var end = middle == starts.Length ? text.Length : starts[middle];
            if (fits(text[..end]))
            {
                fitted = end;
                low = middle + 1;
            }
            else
                high = middle - 1;
        }
        if (fitted == 0)
            return starts.Length == 1 ? text.Length : starts[1];
        for (var index = fitted - 1; index >= 0; index--)
            if (char.IsWhiteSpace(text[index]))
                return index + 1;
        return fitted;
    }
}
