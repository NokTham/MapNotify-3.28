using System;
using System.Text.RegularExpressions;

namespace MapNotify_3_28
{
    public static class BaseModExtractor
    {
        private static readonly Regex UberPrefixRegex = new Regex(@"^Map(UberWeak|UberMod|Uber)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TierNumberRegex = new Regex(@"(\d+)(?=MapWorlds|Worlds|Expedition|Maven|Unique|_|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SuffixRegex = new Regex(@"(MapWorlds|Worlds|Expedition|Maven|Unique|_+).*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CamelCaseSplitRegex = new Regex(@"([A-Z])", RegexOptions.Compiled);
        private static readonly Regex ExtraSpacesRegex = new Regex(@"\s+", RegexOptions.Compiled);

        public static string GetBaseMod(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return modId;

            var normalized = SuffixRegex.Replace(TierNumberRegex.Replace(UberPrefixRegex.Replace(modId, "Map"), ""), "");
            
            // Consistently remove the "Map" prefix to allow matching against config entries that omit it
            if (normalized.StartsWith("Map", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(3);

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