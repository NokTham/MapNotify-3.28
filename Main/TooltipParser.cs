using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExileCore.PoEMemory;

namespace MapNotify_3_28
{
    public partial class MapNotify_3_28
    {
        private List<string> GetModDescriptionsFromTooltip(Element tooltip)
        {
            var descriptions = new List<string>();
            if (tooltip == null) return descriptions;
            var contentArea = tooltip.GetChildAtIndex(0)?.GetChildAtIndex(1);
            if (contentArea != null)
            {
                for (int i = 0; i < contentArea.ChildCount; i++)
                {
                    var container = contentArea.GetChildAtIndex(i);
                    if (container == null || container.ChildCount < 1 || container.IsVisible) continue;
                    bool isModContainer = false;
                    for (int j = 0; j < container.ChildCount; j++)
                    {
                        var headerElem = container.GetChildAtIndex(j)?.GetChildAtIndex(0);
                        var headerText = headerElem?.TextNoTags ?? headerElem?.Text;
                        if (headerText != null && (headerText.Contains("Modifier") || headerText.Contains("Prefix") || headerText.Contains("Suffix")))
                        {
                            isModContainer = true;
                            break;
                        }
                    }
                    if (!isModContainer) continue;
                    for (int groupIdx = 0; groupIdx < container.ChildCount; groupIdx++)
                    {
                        var affixGroup = container.GetChildAtIndex(groupIdx);
                        if (affixGroup == null || affixGroup.ChildCount == 0) continue;
                        var groupHeader = affixGroup.GetChildAtIndex(0);
                        if ((groupHeader?.TextNoTags ?? groupHeader?.Text)?.Contains("Modifier") == true) continue;
                        for (int modIdx = 0; modIdx < (int)affixGroup.ChildCount; modIdx++)
                        {
                            var modEntry = affixGroup.GetChildAtIndex(modIdx);
                            if (modEntry == null) continue;
                            var modLines = new List<string>();
                            ExtractTextRecursive(modEntry, modLines, 0);
                            if (modLines.Any())
                            {
                                var joined = string.Join("\n", modLines.Distinct());
                                if (!joined.Contains("CachedStatDescription") && !joined.StartsWith("<unknownSection") && !joined.Contains("System.Collections.Generic"))
                                    descriptions.Add(joined);
                            }
                        }
                    }
                    break;
                }
            }
            return descriptions.Distinct().ToList();
        }

        private void ExtractTextRecursive(Element el, List<string> lines, int depth)
        {
            if (el == null || depth > 4) return;
            string text = el.TextNoTags ?? el.Text;
            if (!string.IsNullOrEmpty(text)) lines.Add(text);
            for (int k = 0; k < (int)el.ChildCount; k++) ExtractTextRecursive(el.GetChildAtIndex(k), lines, depth + 1);
        }

        public static int ParseElementForQuality(Element tooltip)
        {
            if (tooltip == null) return 0;
            string FindQualityText(Element element, int depth = 0)
            {
                if (element == null || depth > 20) return null;
                if (!string.IsNullOrEmpty(element.Text) && element.Text.Contains("Quality")) return element.Text;
                int count = (int)element.ChildCount;
                if (count <= 0 || count > 100) return null;
                for (int i = 0; i < count; i++)
                {
                    var found = FindQualityText(element.GetChildAtIndex(i), depth + 1);
                    if (found != null) return found;
                }
                return null;
            }
            var qualityLine = FindQualityText(tooltip);
            if (string.IsNullOrEmpty(qualityLine)) return 0;
            int start = -1;
            for (int i = 0; i < qualityLine.Length; i++) { if (char.IsDigit(qualityLine[i])) { start = i; break; } }
            if (start == -1) return 0;
            int end = qualityLine.IndexOf('%', start);
            return (end != -1 && int.TryParse(qualityLine.Substring(start, end - start), out var res)) ? res : 0;
        }

        public static int ParseElementForWings(Element tooltip)
        {
            if (tooltip == null) return 1;
            string FindWingsText(Element element, int depth = 0)
            {
                if (element == null || depth > 20) return null;
                if (!string.IsNullOrEmpty(element.Text) && element.Text.Contains("Wings Revealed")) return element.Text;
                int count = (int)element.ChildCount;
                if (count <= 0 || count > 100) return null;
                for (int i = 0; i < count; i++)
                {
                    var found = FindWingsText(element.GetChildAtIndex(i), depth + 1);
                    if (found != null) return found;
                }
                return null;
            }
            var wingsLine = FindWingsText(tooltip);
            if (string.IsNullOrEmpty(wingsLine)) return 1;
            int colonIndex = wingsLine.IndexOf(':');
            if (colonIndex == -1) return 1;
            int start = -1;
            for (int i = colonIndex + 1; i < wingsLine.Length; i++) { if (char.IsDigit(wingsLine[i])) { start = i; break; } }
            if (start == -1) return 1;
            int end = start;
            while (end < wingsLine.Length && char.IsDigit(wingsLine[end])) end++;
            return int.TryParse(wingsLine.Substring(start, end - start), out var res) ? res : 1;
        }

        public static Dictionary<string, int> ParseElementForRequirements(Element tooltip)
        {
            var requirements = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (tooltip == null) return requirements;
            void FindRequirementsRecursive(Element element, int depth = 0)
            {
                if (element == null || depth > 20) return;
                var text = element.Text;
                if (!string.IsNullOrEmpty(text) && text.Contains("Requires "))
                {
                    var cleanText = TooltipTagsRegex.Replace(text, "").Replace("{", "").Replace("}", "");
                    int reqIndex = cleanText.IndexOf("Requires ");
                    int openParen = cleanText.IndexOf('(', reqIndex);
                    int closeParen = cleanText.IndexOf(')', openParen);
                    if (reqIndex != -1 && openParen > reqIndex + 8 && closeParen > openParen)
                    {
                        var jobName = cleanText.Substring(reqIndex + 9, openParen - (reqIndex + 9)).Trim();
                        var levelPart = cleanText.Substring(openParen, closeParen - openParen);
                        int level = 0;
                        for (int i = 0; i < levelPart.Length; i++) if (char.IsDigit(levelPart[i])) { level = (int)char.GetNumericValue(levelPart[i]); break; }
                        if (!string.IsNullOrEmpty(jobName) && level > 0) requirements[jobName] = level;
                    }
                }
                int count = (int)element.ChildCount;
                if (count > 0 && count < 100) for (int i = 0; i < count; i++) FindRequirementsRecursive(element.GetChildAtIndex(i), depth + 1);
            }
            FindRequirementsRecursive(tooltip);
            return requirements;
        }
    }
}