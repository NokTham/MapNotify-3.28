using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MapNotify_3_28
{
    /// <summary>
    /// Matches an <c>ItemMod</c>'s own text to the corresponding item tooltip line.
    ///
    /// ExileAPI exposes several text flavours per mod (<c>DisplayName</c>, <c>Translation</c>,
    /// <c>Name</c> and the raw name). Depending on the game patch one or more of them can be
    /// missing or still contain the raw mod id. The old code used a positional fallback that
    /// assigned the next unused tooltip line when matching failed, which made an unrelated
    /// modifier (for example "Players have Point Blank") appear for a completely different
    /// mod. This matcher only accepts a tooltip line when the word overlap is convincing and
    /// otherwise leaves the line untouched for the mod it actually belongs to.
    /// </summary>
    internal static class ModTextMatcher
    {
        private static readonly Regex HtmlTagRegex = new Regex(@"<[^>]*>", RegexOptions.Compiled);

        // Words that appear in most raw mod ids (MapMonstersAvoidPoisonBleedBlind3MapWorlds)
        // but carry no descriptive meaning. Leaving them in would water down the coverage
        // score, so they are ignored on both sides of the comparison.
        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "the", "and", "for", "with", "have", "has", "are", "was", "were", "from", "into",
            "your", "their", "this", "that", "you", "per", "its", "all", "any", "can", "may",
            "not", "but", "chance", "additional", "map", "maps", "world", "worlds", "maven",
            "unique", "uber", "weak", "mod", "mods", "implicit", "explicit", "prefix", "suffix"
        };

        /// <summary>
        /// True when the text is real, displayable mod text (not an empty string, a raw
        /// placeholder, or ExileAPI's "unknown" diagnostics).
        /// </summary>
        public static bool IsUsableText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var trimmed = text.Trim();
            if (trimmed.Length < 2) return false;
            if (trimmed.Contains("CachedStatDescription", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.Contains("System.Collections", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.Contains("<unknown", StringComparison.OrdinalIgnoreCase)) return false;
            if (trimmed.StartsWith("<", StringComparison.Ordinal)) return false;

            // Must contain at least two letters; guards against pure numbers/punctuation.
            int letters = 0;
            foreach (var c in trimmed)
            {
                if (char.IsLetter(c) && ++letters >= 2) return true;
            }
            return false;
        }

        /// <summary>
        /// Normalizes text into comparable stems. Splits camel case, drops placeholder
        /// punctuation/digits and applies a very light plural/continuous-tense stem so
        /// "avoid" matches "Avoids" and "bleed" matches "Bleeding".
        /// </summary>
        public static HashSet<string> Tokenize(string text)
        {
            var tokens = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(text)) return tokens;

            var cleaned = HtmlTagRegex.Replace(text, " ");
            var expanded = new StringBuilder(cleaned.Length + 8);

            for (int i = 0; i < cleaned.Length; i++)
            {
                char c = cleaned[i];
                if (char.IsUpper(c) && i > 0 && (char.IsLower(cleaned[i - 1]) || char.IsDigit(cleaned[i - 1])))
                    expanded.Append(' ');

                // Digits are tier/value noise in raw ids ("...Blind3MapWorlds"), so treat
                // them as separators instead of gluing them onto the preceding word.
                if (char.IsDigit(c))
                    expanded.Append(' ');
                else if (char.IsLetter(c))
                    expanded.Append(char.ToLowerInvariant(c));
                else
                    expanded.Append(' ');
            }

            foreach (var raw in expanded.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (raw.Length < 3) continue;
                if (StopWords.Contains(raw)) continue;
                if (IsAllDigits(raw)) continue;

                var stem = Stem(raw);
                if (stem.Length >= 3 && !StopWords.Contains(stem))
                    tokens.Add(stem);
            }

            return tokens;
        }

        /// <summary>
        /// Number of tokens from the mod text that are represented in the tooltip line,
        /// together with the fraction of the mod text that was covered.
        /// </summary>
        public static double MatchCoverage(HashSet<string> modTokens, HashSet<string> lineTokens, out int matched)
        {
            matched = 0;
            if (modTokens == null || lineTokens == null || modTokens.Count == 0 || lineTokens.Count == 0)
                return 0;

            foreach (var token in modTokens)
            {
                foreach (var lineToken in lineTokens)
                {
                    if (TokensMatch(token, lineToken))
                    {
                        matched++;
                        break;
                    }
                }
            }

            return (double)matched / modTokens.Count;
        }

        /// <summary>
        /// A match is only trusted when at least two meaningful words line up (or one long,
        /// distinctive word), and when they cover at least half of the mod text.
        /// </summary>
        public static bool IsConfident(int modTokenCount, int matched, double coverage)
        {
            if (modTokenCount <= 0 || matched <= 0) return false;

            if (modTokenCount == 1)
                return matched == 1 && coverage >= 0.99;

            return matched >= 2 && coverage >= 0.5;
        }

        private static bool TokensMatch(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.Ordinal)) return true;

            // Cheap stemming for related forms such as "resist"/"resistance".
            int minLength = Math.Min(a.Length, b.Length);
            if (minLength >= 4 &&
                (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal)))
            {
                return true;
            }

            return false;
        }

        private static string Stem(string word)
        {
            if (word.Length > 5 && word.EndsWith("ing", StringComparison.Ordinal))
                return word.Substring(0, word.Length - 3);
            // "Bleed"/"Need" end in "ed" but are not past tense; don't turn them into "ble".
            if (word.Length > 4 && word.EndsWith("ed", StringComparison.Ordinal) && !word.EndsWith("eed", StringComparison.Ordinal))
                return word.Substring(0, word.Length - 2);
            if (word.Length > 4 && word.EndsWith("es", StringComparison.Ordinal))
                return word.Substring(0, word.Length - 2);
            if (word.Length > 3 && word.EndsWith("s", StringComparison.Ordinal) && !word.EndsWith("ss", StringComparison.Ordinal))
                return word.Substring(0, word.Length - 1);
            return word;
        }

        private static bool IsAllDigits(string value)
        {
            foreach (var c in value)
            {
                if (!char.IsDigit(c)) return false;
            }
            return true;
        }
    }
}
