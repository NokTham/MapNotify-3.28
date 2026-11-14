using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MapNotify_3_28
{
    public static class BaseModExtractor
    {
        private static readonly Regex UberPrefixRegex = new Regex(@"^Map(UberWeak|UberMod|Uber)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TierNumberRegex = new Regex(@"(\d+)(?=MapWorlds|Worlds|Expedition|Maven|Unique|_|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // "Expedition" is deliberately NOT a suffix marker here. Expedition and
        // logbook raw names start with it (ExpeditionLogbookMapExpeditionRelics),
        // so treating it as a suffix consumed the whole name and collapsed every
        // logbook mod to the same empty key. That made one Good/Bad/Interest entry
        // match every logbook implicit and produced the duplicated tooltip lines.
        private static readonly Regex SuffixRegex = new Regex(@"(MapWorlds|Worlds|Maven|Unique|_+).*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CamelCaseSplitRegex = new Regex(@"([A-Z])", RegexOptions.Compiled);
        private static readonly Regex ExtraSpacesRegex = new Regex(@"\s+", RegexOptions.Compiled);

        // Mod IDs are a finite, heavily reused set. Normalization runs on every item
        // update, so memoize it instead of repeating three regex passes each time.
        private static readonly ConcurrentDictionary<string, string> NormalizationCache =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        public static string GetBaseMod(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return modId;

            return NormalizationCache.GetOrAdd(modId, static id => NormalizeBaseMod(id));
        }

        private static string NormalizeBaseMod(string modId)
        {
            var normalized = SuffixRegex.Replace(TierNumberRegex.Replace(UberPrefixRegex.Replace(modId, "Map"), ""), "");
            
            // Consistently remove the "Map" prefix to allow matching against config entries that omit it
            if (normalized.StartsWith("Map", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(3);

            // A marker word can consume the whole name (UniqueWatchstoneMapAdditionalPrefix,
            // MapUberModMavenFollower). Falling back to a cleaned copy of the original keeps
            // those mods distinct; an empty key would match every mod in the config.
            if (normalized.Length == 0)
            {
                normalized = modId.StartsWith("Map", StringComparison.OrdinalIgnoreCase)
                    ? modId.Substring(3)
                    : modId;
                normalized = normalized.TrimEnd('_');
                if (normalized.Length == 0)
                    normalized = modId;
            }

            return normalized.ToLowerInvariant();
        }

        public static bool AreEquivalent(string modId1, string modId2)
        {
            return string.Equals(GetBaseMod(modId1), GetBaseMod(modId2), StringComparison.Ordinal);
        }

        public static string GetReadableName(string baseMod)
        {
            if (string.IsNullOrEmpty(baseMod))
                return baseMod;
            var nameWithoutPrefix = baseMod.StartsWith("Map", StringComparison.OrdinalIgnoreCase)
                ? baseMod.Substring(3)
                : baseMod;
            var result = CamelCaseSplitRegex.Replace(nameWithoutPrefix, " $1");
            result = ExtraSpacesRegex.Replace(result, " ").Trim();
            return result;
        }
    }
}