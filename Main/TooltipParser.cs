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

        public class TooltipData
        {
            public int Quality { get; set; }
            public int Wings { get; set; } = 1;
            public Dictionary<string, int> Requirements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        public static TooltipData ParseTooltip(Element tooltip)
        {
            var data = new TooltipData();
            if (tooltip == null) return data;
            bool passedSummary = false;

            void Walk(Element el, int depth)
            {
                if (el == null || depth > 20) return;
                var text = el.TextNoTags ?? el.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    if (text.Contains("Wing ") || text.Contains("Reward:")) passedSummary = true;

                    if (text.Contains("Quality"))
                    {
                        int start = text.IndexOfAny("0123456789".ToCharArray());
                        if (start != -1)
                        {
                            int end = text.IndexOf('%', start);
                            if (end != -1 && int.TryParse(text.Substring(start, end - start), out var res)) data.Quality = res;
                        }
                    }
                    else if (text.Contains("Wings Revealed"))
                    {
                        int colon = text.IndexOf(':');
                        if (colon != -1)
                        {
                            var match = Regex.Match(text.Substring(colon), @"\d+");
                            if (match.Success && int.TryParse(match.Value, out var res)) data.Wings = res;
                        }
                    }
                    else if (!passedSummary && text.Contains("Requires "))
                    {
                        var clean = text.Replace("{", "").Replace("}", "");
                        var match = Regex.Match(clean, @"Requires\s+(?<job>.+?)\s*\(.*?(?<lvl>\d+)\)", RegexOptions.IgnoreCase);
                        if (match.Success && int.TryParse(match.Groups["lvl"].Value, out var lvl))
                        {
                            var job = match.Groups["job"].Value.Trim();
                            if (!data.Requirements.ContainsKey(job))
                                data.Requirements[job] = lvl;
                        }
                    }
                }

                int count = (int)el.ChildCount;
                if (count > 0 && count < 100)
                {
                    for (int i = 0; i < count; i++) Walk(el.GetChildAtIndex(i), depth + 1);
                }
            }

            Walk(tooltip, 0);
            return data;
        }
    }
}