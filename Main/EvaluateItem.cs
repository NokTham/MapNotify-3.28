using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using nuVector4 = System.Numerics.Vector4;

namespace MapNotify_3_28
{
    public partial class MapNotify_3_28 : BaseSettingsPlugin<MapNotifySettings>
    {
        private static readonly Regex TooltipTagsRegex = new Regex(@"<[^>]*>", RegexOptions.Compiled); // Used for cleaning text

        private List<string> GetModDescriptionsFromTooltip(ExileCore.PoEMemory.Element tooltip)
        {
            var descriptions = new List<string>();
            if (tooltip == null) return descriptions;

            // --- Path-Based UI Traversal ---
            // Logic: tooltip -> [0] (frame) -> [1] (content area)
            // Index [0] of the frame contains rarity name/tier.
            // Index [1] contains the actual item properties and mods.
            var contentArea = tooltip.GetChildAtIndex(0)?.GetChildAtIndex(1);
            if (contentArea != null)
            {
                // Search for the Advanced Mod container within the content area.
                // As you noted, this is often an invisible element (index 14 in your example).
                for (int i = 0; i < contentArea.ChildCount; i++)
                {
                    var container = contentArea.GetChildAtIndex(i);
                    
                    // Logic: The mod container is usually the only large, invisible child 
                    // that contains the Advanced Mod headers.
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

                    // Process Prefix and Suffix groups within the container
                    for (int groupIdx = 0; groupIdx < container.ChildCount; groupIdx++)
                    {
                        var affixGroup = container.GetChildAtIndex(groupIdx);
                        if (affixGroup == null || affixGroup.ChildCount == 0) continue;

                        // Skip the header element itself if it's mixed into the child list
                        var groupHeader = affixGroup.GetChildAtIndex(0);
                        if ((groupHeader?.TextNoTags ?? groupHeader?.Text)?.Contains("Modifier") == true) continue;

                        for (int modIdx = 0; modIdx < (int)affixGroup.ChildCount; modIdx++)
                        {
                            var modEntry = affixGroup.GetChildAtIndex(modIdx);
                            if (modEntry == null) continue;

                            var modLines = new List<string>();
                            void ExtractModText(ExileCore.PoEMemory.Element el, int depth)
                            {
                                if (el == null || depth > 4) return;
                                string text = el.TextNoTags ?? el.Text;
                                if (!string.IsNullOrEmpty(text))
                                    modLines.Add(text);

                                for (int k = 0; k < (int)el.ChildCount; k++)
                                    ExtractModText(el.GetChildAtIndex(k), depth + 1);
                            }

                            ExtractModText(modEntry, 0);
                            if (modLines.Any())
                            {
                                var joined = string.Join("\n", modLines.Distinct());
                                // Filter out internal technical/debug strings that sometimes leak from the API
                                if (!joined.Contains("CachedStatDescription") && 
                                    !joined.StartsWith("<unknownSection") && // Use StartsWith for better precision
                                    !joined.Contains("System.Collections.Generic")) // Add this for robustness
                                    descriptions.Add(joined);
                            }
                        }
                    }
                    break; // Stop after processing the primary mod container
                }
            }

            return descriptions.Distinct().ToList();
        }

        

        public static nuVector4 GetRarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare:
                    return new nuVector4(0.0f, 0.0f, 0.0f, 1f); // Placeholder, will be overwritten by actual color
                case ItemRarity.Magic:
                    return new nuVector4(0.0f, 0.0f, 0.0f, 1f); // Placeholder, will be overwritten by actual color
                case ItemRarity.Unique:
                    return new nuVector4(0.0f, 0.0f, 0.0f, 1f); // Placeholder, will be overwritten by actual color
                default:
                    return new nuVector4(1F, 1F, 1F, 1F);
            }
        }
        public static readonly string[] ModNameBlacklist =
        {
            "AfflictionMapDeliriumStacks",
            "AfflictionMapReward",
            "InfectedMap",
            "MapForceCorruptedSideArea",
            "MapGainsRandomZanaMod",
            "MapDoesntConsumeSextantCharge",
            "MapEnchant",
            "Enchantment",
            "MapBossSurroundedByTormentedSpirits",
            "MapZanaSubAreaMissionDetails",
            "MapZanaInfluence",
            "IsUberMap",
            "MapConqueror",
            "MapElder",
            "MapVaalTempleContainsVaalVessels",
            "MavenInvitation",
        };

        public class StyledText
        {
            public string Text { get; set; }
            public Vector4 Color { get; set; }
            public bool Bricking { get; set; }
        }

        public class ItemDetails
        {
            public NormalInventoryItem Item { get; }
            public ServerInventory.InventSlotItem ItemS { get; }
            public Entity Entity { get; }
            public List<StyledText> ActiveGoodMods { get; set; }
            public List<StyledText> ActiveBadMods { get; set; }
            public nuVector4 ItemColor { get; set; }
            public string MapName { get; set; }
            public string ClassID { get; set; }
            public string ChiselName { get; set; }
            public int ChiselValue { get; set; }
            public int PackSize { get; set; }
            public int Quantity { get; set; }
            public int Rarity { get; set; }
            public int ModCount { get; set; }
            public int HeistAreaLevel { get; set; }
            public class HeistJobLine
            {
                public string Text { get; set; }
                public bool IsRevealed { get; set; }
            }
            public List<HeistJobLine> HeistJobLines { get; set; } = new List<HeistJobLine>();
            public bool NeedsPadding { get; set; }
            public bool Bricked { get; set; }
            public bool Corrupted { get; set; }
            public int Tier { get; set; }
            public int OriginatorScarabs { get; set; }
            public int OriginatorCurrency { get; set; }
            public int OriginatorMaps { get; set; }
            public bool IsOriginatorMap { get; set; }
            public bool IsFragment { get; set; }
            public bool IsMavenMap { get; set; }
            public string WindowID { get; private set; }
            public ItemDetails(NormalInventoryItem Item, Entity Entity)
            {
                this.Item = Item;
                this.Entity = Entity;
                ActiveGoodMods = new List<StyledText>();
                ActiveBadMods = new List<StyledText>();
                Update();
            }

            public void Update()
            {
                WindowID = $"##{Entity.Address}";
                var path = Entity.Path ?? string.Empty;
                var BaseItem = gameController?.Files?.BaseItemTypes?.Translate(path);
                var ItemName = BaseItem?.BaseName ?? "Unknown";
                ClassID = BaseItem?.ClassName ?? string.Empty;
                ChiselName = string.Empty;
                ChiselValue = 0;
                var packSize = 0;
                var rarity = 0;

                var modsComponent = Entity.GetComponent<Mods>();
                var baseComponent = Entity.GetComponent<Base>();
                var mapComponent = Entity.GetComponent<ExileCore.PoEMemory.Components.MapKey>();
                var qualityComponent = Entity.GetComponent<Quality>();
                var heistContract = Entity.GetComponent<HeistContract>();
                var heistBlueprint = Entity.GetComponent<HeistBlueprint>();

                Tier = mapComponent?.Tier ?? -1;
                IsFragment = path.Contains("Fragments/") && !path.Contains("Maven");
                IsMavenMap = path.Contains("MavenMap") || path.Contains("Invitations/Maven");

                UpdateHeistDetails(heistContract, heistBlueprint, modsComponent);
                IsOriginatorMap = false;
                Bricked = false;
                Corrupted = baseComponent?.isCorrupted ?? false;
                NeedsPadding = Tier != -1 || IsMavenMap || IsFragment || HeistJobLines.Count > 0;

                // Get quality from component, or fallback to tooltip if memory returns 0
                int quantity = qualityComponent?.ItemQuality ?? 0;

                // If the map has Rarity quality, move the quality value to the rarity variable
                if (modsComponent?.AlternateQualityType?.Id == "MapRarityQuality")
                {
                    rarity = quantity;
                    quantity = 0;
                }

                if (modsComponent?.AlternateQualityType != null)
                {
                    var qualityId = modsComponent.AlternateQualityType.Id;

                    if (qualityId == "MapRarityQuality") { ChiselName = "Rarity Chisel"; ChiselValue = 40; }
                    else if (qualityId == "MapQuantityQuality")
                    {
                        ChiselName = "Quantity Chisel";
                        if (quantity == 0) quantity = ParseTooltipQuality();
                        ChiselValue = quantity;
                    }
                    else if (qualityId == "MapPackSizeQuality") { ChiselName = "Pack Size Chisel"; ChiselValue = 10; }
                    else if (qualityId == "MapDivinationCardQuality") { ChiselName = "Divination Chisel"; ChiselValue = 50; }
                    else if (qualityId == "MapScarabQuality") { ChiselName = "Scarab Chisel"; ChiselValue = 50; }
                    else if (qualityId == "MapCurrencyQuality") { ChiselName = "Currency Chisel"; ChiselValue = 50; }
                }
                else if (quantity == 0)
                {
                    // Fallback for standard Cartographer's Chisels
                    quantity = ParseTooltipQuality();
                }

                var originatorScarabs = 0;
                var originatorCurrency = 0;
                var originatorMaps = 0;
                var itemMods = modsComponent?.ItemMods;
                ModCount = itemMods?.Count ?? 0;

                if (modsComponent != null && itemMods != null && ModCount > 0)
                {
                    // Only evaluate mods for non-unique maps, but ALWAYS evaluate for Maven items and T17s
                    if (modsComponent.ItemRarity != ItemRarity.Unique || path.Contains("Maven") || Tier == 17)
                    {
                        foreach (var mod in itemMods)
                        {
                            if (string.IsNullOrEmpty(mod.RawName)) continue;

                            bool blacklisted = false;
                            foreach (var black in ModNameBlacklist) { if (mod.RawName.Contains(black)) { blacklisted = true; break; } }
                            if (blacklisted)
                            {
                                ModCount--;
                                continue;
                            }

                            // Optimized: Process all stats for this mod in a single pass
                            var stats = mod.ModRecord?.StatNames;
                            var values = mod.Values;
                            if (stats != null && values != null)
                            {
                                for (int i = 0; i < stats.Length && i < values.Count; i++)
                                {
                                    var key = stats[i].Key;
                                    if (string.IsNullOrEmpty(key)) continue;

                                    switch (key)
                                    {
                                        case "map_pack_size_+%":
                                            packSize += values[i]; break;
                                        case "map_item_drop_quantity_+%":
                                            quantity += values[i]; break;
                                        case "map_item_drop_rarity_+%":
                                            rarity += values[i]; break;
                                        case "map_scarab_drop_chance_+%_final_from_uber_mod":
                                            originatorScarabs += values[i];
                                            IsOriginatorMap = true; break;
                                        case "map_currency_drop_chance_+%_final_from_uber_mod":
                                            originatorCurrency += values[i];
                                            IsOriginatorMap = true; break;
                                        case "map_map_item_drop_chance_+%_final_from_uber_mod":
                                            originatorMaps += values[i];
                                            IsOriginatorMap = true; break;
                                    }
                                }
                            }

                            // Optimization: Check for Originator/Uber status during the main mod pass
                            if (!IsOriginatorMap && (mod.RawName == "IsUberMap" || mod.RawName.Contains("MapZanaInfluence")))
                                IsOriginatorMap = true;

                            // Optimized Dictionary Search: Early exit if mod is found in the first dictionary
                            bool foundMod = false;
                            foreach (var entry in GoodModsDictionary)
                            {
                                if (mod.RawName.Contains(entry.Key))
                                {
                                    var warning = entry.Value;
                                    if (warning.Bricking) Bricked = true;
                                    ActiveGoodMods.Add(warning);
                                    foundMod = true;
                                    break;
                                }
                            }

                            if (!foundMod)
                            {
                                foreach (var entry in BadModsDictionary)
                                {
                                    if (mod.RawName.Contains(entry.Key))
                                    {
                                        var bad = entry.Value;
                                        if (bad.Bricking) Bricked = true;
                                        ActiveBadMods.Add(bad);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                Quantity = quantity;
                PackSize = packSize;
                Rarity = rarity;
                OriginatorScarabs = originatorScarabs;
                OriginatorCurrency = originatorCurrency;
                OriginatorMaps = originatorMaps;
                {
                    var mapTrim = baseComponent != null ? baseComponent.Name.Replace(" Map", "") : "Unknown";

                    if (modsComponent?.ItemRarity == ItemRarity.Unique)
                    {
                        // For unique maps, try to resolve the real area name via memory read
                        try
                        {
                            // Multi-level pointer read to get the WorldArea address for unique maps
                            var addr = mapComponent?.Address ?? 0;
                            if (addr == 0) addr = Entity.Address;

                            var mapUnique = gameController.IngameState.M.Read<long>(addr + 0x10, 0x10, 0x20);
                            var resolvedArea = gameController.Files.WorldAreas.GetByAddress(mapUnique);
                            if (resolvedArea != null)
                                mapTrim = resolvedArea.Name;
                        }
                        catch { }
                    }
                    MapName = $"[T{mapComponent?.Tier ?? Tier}] {mapTrim}";
                }

                if (path.Contains("Maven") || path.Contains("Invitations"))
                {
                    MapName = ItemName;
                }
                if (ClassID.Contains("MapFragment"))
                {
                    MapName = ItemName;
                    NeedsPadding = true;
                }
                #region Maven Regions & Areas
                // All Maven-related logic for specific areas/invitations removed as it's no longer tied to map items.
                #endregion

                // evaluate rarity for colouring item name
                // The actual colors are defined in the game's UI, so we'll use a default and let the game's rendering handle it.
                // This method is primarily for consistency if we ever needed to override.
                switch (modsComponent?.ItemRarity ?? ItemRarity.Normal)
                {
                    case ItemRarity.Rare:
                        ItemColor = new nuVector4(0.99f, 0.99f, 0.46f, 1f); // Yellow
                        break;
                    case ItemRarity.Magic:
                        ItemColor = new nuVector4(0.68f, 0.68f, 1f, 1f); // Blue
                        break;
                    case ItemRarity.Unique:
                        ItemColor = new nuVector4(1f, 0.50f, 0.10f, 1f); // Orange
                        break;
                    default:
                        ItemColor = new nuVector4(1F, 1F, 1F, 1F); // White
                        break;
                }
            }
            private int ParseTooltipQuality()
            {
                var tooltip = Item?.Tooltip;
                if (tooltip == null) return 0;
                string FindQualityText(ExileCore.PoEMemory.Element element, int depth = 0)
                {
                    if (element == null || depth > 20) return null;
                    if (!string.IsNullOrEmpty(element.Text) && element.Text.Contains("Quality"))
                        return element.Text;
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
                if (end != -1 && int.TryParse(qualityLine.Substring(start, end - start), out var res)) return res;
                return 0;
            }

            private int ParseTooltipWings()
            {
                if (Item?.Tooltip == null) return 1;
                string FindWingsText(ExileCore.PoEMemory.Element element, int depth = 0)
                {
                    if (element == null || depth > 20) return null;
                    if (!string.IsNullOrEmpty(element.Text) && element.Text.Contains("Wings Revealed"))
                        return element.Text;

                    int count = (int)element.ChildCount;
                    if (count <= 0 || count > 100) return null;
                    for (int i = 0; i < count; i++)
                    {
                        var found = FindWingsText(element.GetChildAtIndex(i), depth + 1);
                        if (found != null) return found;
                    }
                    return null;
                }
                var wingsLine = FindWingsText(Item.Tooltip);
                if (string.IsNullOrEmpty(wingsLine)) return 1;

                // Use a robust digit scanner to ignore PoE markup tags (e.g., <white>{2}/4)
                int colonIndex = wingsLine.IndexOf(':');
                if (colonIndex == -1) return 1;

                int start = -1;
                for (int i = colonIndex + 1; i < wingsLine.Length; i++)
                {
                    if (char.IsDigit(wingsLine[i])) { start = i; break; }
                }

                if (start == -1) return 1;

                int end = start;
                while (end < wingsLine.Length && char.IsDigit(wingsLine[end])) end++;

                if (int.TryParse(wingsLine.Substring(start, end - start), out var res))
                    return res;

                return 1;
            }

            private Dictionary<string, int> ParseSummaryRequirements()
            {
                var requirements = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var tooltip = Item?.Tooltip;
                if (tooltip == null) return requirements;

                void FindRequirementsRecursive(ExileCore.PoEMemory.Element element, int depth = 0)
                {
                    if (element == null || depth > 20) return;
                    var text = element.Text;
                    if (!string.IsNullOrEmpty(text) && text.Contains("Requires "))
                    {
                        // Strip markup tags and brackets: <white>{Requires Lockpicking} (Level 5) -> Requires Lockpicking (Level 5)
                        var cleanText = TooltipTagsRegex.Replace(text, "").Replace("{", "").Replace("}", "");
                        int reqIndex = cleanText.IndexOf("Requires ");
                        int openParen = cleanText.IndexOf('(', reqIndex);
                        int closeParen = cleanText.IndexOf(')', openParen);

                        if (reqIndex != -1 && openParen > reqIndex + 8 && closeParen > openParen)
                        {
                            var jobName = cleanText.Substring(reqIndex + 9, openParen - (reqIndex + 9)).Trim();
                            var levelPart = cleanText.Substring(openParen, closeParen - openParen);

                            // Extract digit from "(Level 5)"
                            int level = 0;
                            for (int i = 0; i < levelPart.Length; i++)
                                if (char.IsDigit(levelPart[i])) { level = (int)char.GetNumericValue(levelPart[i]); break; }

                            if (!string.IsNullOrEmpty(jobName) && level > 0)
                                requirements[jobName] = level;
                        }
                    }

                    int count = (int)element.ChildCount;
                    if (count <= 0 || count > 100) return;
                    for (int i = 0; i < count; i++)
                        FindRequirementsRecursive(element.GetChildAtIndex(i), depth + 1);
                }

                FindRequirementsRecursive(tooltip);
                return requirements;
            }

            private void UpdateHeistDetails(HeistContract heistContract, HeistBlueprint heistBlueprint, Mods mods)
            {
                HeistJobLines.Clear();

                if (heistContract != null)
                {
                    HeistAreaLevel = mods?.ItemLevel ?? 0;
                    var jobName = heistContract.RequiredJob?.Name ?? "Unknown Job";
                    HeistJobLines.Add(new HeistJobLine
                    {
                        Text = $"{jobName} (Level {heistContract.RequiredJobLevel})",
                        IsRevealed = true
                    });
                }
                else if (heistBlueprint != null)
                {
                    HeistAreaLevel = heistBlueprint.AreaLevel;
                    if (heistBlueprint.Wings != null)
                    {
                        var revealedWingsCount = ParseTooltipWings(); // Get the actual count from the tooltip
                        var summaryRequirements = ParseSummaryRequirements(); // Still useful for job level checks
                        int actuallyRevealed = 1; // Wing 1 is always revealed
                        var wingReqs = new List<string>();
                        for (int i = 0; i < heistBlueprint.Wings.Count; i++)
                        {
                            var w = heistBlueprint.Wings[i];
                            wingReqs.Clear();
                            bool fitsSummary = true;

                            if (w.Jobs != null)
                            {
                                foreach (var job in w.Jobs)
                                {
                                    if (job.Item1 == null) continue;
                                    var jobName = job.Item1.Name;
                                    var jobLevel = job.Item2;
                                    wingReqs.Add($"{jobName} {jobLevel}");

                                    // Process of Elimination: 
                                    // If a wing requires a job level HIGHER than what is shown on the item summary,
                                    // or requires a job NOT shown on the summary, it is definitely unrevealed.
                                    if (summaryRequirements.TryGetValue(jobName, out var maxLevel))
                                    {
                                        if (jobLevel > maxLevel) fitsSummary = false;
                                    }
                                    else
                                    {
                                        fitsSummary = false;
                                    }
                                }
                            }

                            HeistJobLines.Add(new HeistJobLine
                            {
                                Text = $"Wing {i + 1}: {string.Join(", ", wingReqs)}",
                                // Optimized Reveal Logic:
                                // 1. Wing 1 is always revealed.
                                // 2. For others, if we haven't reached the count AND it fits the summary, it's revealed.
                                // 3. If summary is empty (parser delay), fallback to sequential index.
                                IsRevealed = i == 0 || (summaryRequirements.Count > 0
                                    ? (actuallyRevealed < revealedWingsCount && fitsSummary)
                                    : i < revealedWingsCount)
                            });

                            // Increment counter if we identified a revealed wing beyond the first
                            if (i > 0 && (summaryRequirements.Count > 0 ? (actuallyRevealed < revealedWingsCount && fitsSummary) : i < revealedWingsCount))
                            {
                                actuallyRevealed++;
                            }
                        }
                    }
                }
            }
        }

    }
}
