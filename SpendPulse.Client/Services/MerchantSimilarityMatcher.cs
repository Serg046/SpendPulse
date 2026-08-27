using System.Text.RegularExpressions;
using SpendPulse.Client.Models;

namespace SpendPulse.Client.Services;

public static class MerchantSimilarityMatcher
{
    private static readonly Regex DigitsOnly = new(@"^\d+$", RegexOptions.Compiled);

    public static Dictionary<string, List<string>> ComputeSimilarGroups(
        IEnumerable<string> merchantNames, HashSet<string> mappedNames, List<MerchantNameExclusion> exclusions)
    {
        var unmapped = merchantNames.Where(name => !mappedNames.Contains(name)).ToList();
        var wordsByName = unmapped.ToDictionary(name => name, name => GetSignificantWords(name, exclusions));

        var similarGroups = new Dictionary<string, List<string>>();
        foreach (var name in unmapped)
        {
            var words = wordsByName[name];
            if (words.Count == 0)
            {
                continue;
            }

            var candidates = unmapped
                .Where(other => other != name && wordsByName[other].Overlaps(words))
                .ToList();

            if (candidates.Count > 0)
            {
                similarGroups[name] = candidates;
            }
        }

        return similarGroups;
    }

    public static HashSet<string> GetSignificantWords(string name, List<MerchantNameExclusion> exclusions)
    {
        var excludedWords = exclusions
            .Where(e => e.MerchantName is null || string.Equals(e.MerchantName, name, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Word)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return name
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !DigitsOnly.IsMatch(word))
            .Where(word => !excludedWords.Contains(word))
            .Select(word => word.ToUpperInvariant())
            .ToHashSet();
    }
}
