using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using nuVector4 = System.Numerics.Vector4;

namespace MapNotify_3_28
{
    public partial class MapNotify_3_28
    {
        private static readonly Regex MapTierRegex = new Regex(@"\s*\(Tier\s+\d+\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Encapsulates the processed state of an item (Map, Heist Contract, etc.).
        /// This class handles the extraction of data from game components and evaluates them against user-defined mod filters.
        /// </summary>
        public class ItemDetails
        {
            // References
            public NormalInventoryItem Item { get; set; }
            public Entity Entity { get; }

            // Mod state
            public List<StyledText> ActiveGoodMods { get; set; } = new List<StyledText>();
            public List<StyledText> ActiveBadMods { get; set; } = new List<StyledText>();
            public List<StyledText> ConflictingMods { get; set; } = new List<StyledText>();
            public bool Bricked { get; set; }
            public bool Corrupted { get; set; }

            // Base Stats
            public int Tier { get; set; }
            public int ModCount { get; set; }
            public int PackSize { get; set; }
            public int Quantity { get; set; }
            public int Rarity { get; set; }

            // Visuals
            public string MapName { get; set; }
            public string EscapedMapName { get; set; }
            public nuVector4 ItemColor { get; set; }
            public string ClassID { get; set; }
            public string WindowID { get; private set; }
            public bool NeedsPadding { get; set; }

            // Chisel / Quality info
            public string ChiselName { get; set; }
            public int ChiselValue { get; set; }

            // Heist & Expedition
            public int HeistAreaLevel { get; set; }
            public int LogbookAreaLevel { get; set; }
            public List<LogbookArea> LogbookAreas { get; set; } = new List<LogbookArea>();
            public List<HeistJobLine> HeistJobLines { get; set; } = new List<HeistJobLine>();

            // Originator / Nightmare stats
            public int OriginatorScarabs { get; set; }
            public int OriginatorCurrency { get; set; }
            public int OriginatorMaps { get; set; }
            public MapModStats PrefixStats { get; set; } = new MapModStats();
            public MapModStats SuffixStats { get; set; } = new MapModStats();

            // Item Classification
            public bool IsOriginatorMap { get; set; }
            public bool IsMavenMap { get; set; }
            public bool IsLogbook { get; set; }
            public bool IsHeist { get; set; }
            public bool IsValdoMap { get; set; }

            public class MapModStats
            {
                public int Quantity { get; set; }
                public int Rarity { get; set; }
                public int PackSize { get; set; }
                public int MoreMaps { get; set; }
                public int MoreScarabs { get; set; }
                public int MoreCurrency { get; set; }
            }

            public class LogbookArea
            {
                public string Name { get; set; }
                public string Faction { get; set; }
                public List<StyledText> Implicits { get; set; } = new List<StyledText>();
            }

            public ItemDetails(NormalInventoryItem Item, Entity Entity)
            {
                this.Item = Item;
                this.Entity = Entity;
                Update();
            }

            /// <summary>
            /// Refreshes all item properties by retrieving data from game components.
            /// </summary>
            public void Update()
            {
                WindowID = $"##{Entity.Address}";
                ActiveGoodMods.Clear();
                ActiveBadMods.Clear();
                ConflictingMods.Clear();
                string path = Entity.Path ?? string.Empty;

                var BaseItem = gameController?.Files?.BaseItemTypes?.Translate(path);
                string itemName = BaseItem?.BaseName ?? "Unknown";
                ClassID = BaseItem?.ClassName ?? string.Empty;

                var modsComponent = Entity.GetComponent<Mods>();
                var baseComponent = Entity.GetComponent<Base>();
                var mapComponent = Entity.GetComponent<ExileCore.PoEMemory.Components.MapKey>();
                var qualityComponent = Entity.GetComponent<Quality>();
                var heistContract = Entity.GetComponent<HeistContract>();
                var heistBlueprint = Entity.GetComponent<HeistBlueprint>();
                var expeditionSaga = Entity.GetComponent<ExpeditionSaga>();

                Tier = mapComponent?.Tier ?? -1;
                IsMavenMap = path.Contains("MavenMap", StringComparison.OrdinalIgnoreCase) || 
                             path.Contains("Invitations/Maven", StringComparison.OrdinalIgnoreCase) || 
                             path.Contains("MavenInvitation", StringComparison.OrdinalIgnoreCase);
                IsLogbook = path.Contains("ExpeditionLogbook", StringComparison.OrdinalIgnoreCase);
                IsValdoMap = path.Contains("Valdo", StringComparison.OrdinalIgnoreCase) || 
                             itemName.Contains("Valdo", StringComparison.OrdinalIgnoreCase);

                ProcessFlags(baseComponent);

                LogbookAreaLevel = IsLogbook ? (expeditionSaga?.AreaLevel ?? (Tier > 0 ? Tier : 0)) : 0;
                
                if (IsLogbook) ProcessLogbookDetails(expeditionSaga);

                // Performance and Stability: Only parse tooltips if the item is currently hovered.
                // Tooltip parsing is expensive and prone to 'startIndex' errors when items are in lockers/stashes.
                bool isHovered = ingameState?.UIHover?.Address == Item?.Address;
                if (isHovered)
                {
                    UpdateHeistDetails(heistContract, heistBlueprint, modsComponent);
                    ProcessQuality(qualityComponent, modsComponent);
                }
                
                ProcessMods(modsComponent, path);
                ProcessMapName(mapComponent, baseComponent, modsComponent, path, itemName);
                ProcessItemColor(modsComponent);
            }

            private void ProcessLogbookDetails(ExpeditionSaga expeditionSaga)
            {
                LogbookAreas.Clear();
                if (expeditionSaga?.Areas == null) return;

                foreach (var area in expeditionSaga.Areas)
                {
                    if (area == null) continue;
                    var logArea = new LogbookArea { Name = area.Name, Faction = area.Faction };
                    if (area.ImplicitMods == null) continue;

                    foreach (var mod in area.ImplicitMods)
                    {
                        if (mod == null) continue;
                        var modText = !string.IsNullOrEmpty(mod.Translation) ? mod.Translation : mod.Name;
                        var cleanText = modText ?? string.Empty;
                        if (string.IsNullOrEmpty(cleanText)) continue;

                        var (match, _) = MatchMod(mod.RawName);
                        var styledMod = new StyledText
                        {
                            Text = match?.Text ?? cleanText,
                            EscapedText = EscapeImGui(match?.Text ?? cleanText),
                            Color = match?.Color ?? new nuVector4(0.9f, 0.9f, 0.9f, 1f),
                            Bricking = match?.Bricking ?? false
                        };

                        if (match != null)
                        {
                            if (match.Bricking) Bricked = true;
                            ProcessModWarnings(mod.RawName);
                        }
                        logArea.Implicits.Add(styledMod);
                    }
                    LogbookAreas.Add(logArea);
                }
            }

            /// <summary>
            /// Checks a mod against Good and Bad dictionaries and updates the item's active warning lists.
            /// </summary>
            private void ProcessModWarnings(string rawName)
            {
                if (string.IsNullOrEmpty(rawName)) return;

                // Using the helper method to avoid redundant dictionary iterations
                var (matchedKey, match, isGood) = MatchModWithKey(rawName);
                
                if (match != null && isGood)
                {
                    if (string.IsNullOrEmpty(match.EscapedText)) match.EscapedText = EscapeImGui(match.Text);
                    if (match.Bricking) Bricked = true;
                    ActiveGoodMods.Add(match);
                }
                else if (match != null && !isGood)
                {
                    if (string.IsNullOrEmpty(match.EscapedText)) match.EscapedText = EscapeImGui(match.Text);
                    if (match.Bricking) Bricked = true;
                    ActiveBadMods.Add(match);
                }
            }

            /// <summary>
            /// Generates the formatted Prefix/Suffix breakdown lines for the tooltip.
            /// Highlights the higher value between Prefix and Suffix in green.
            /// </summary>
            public List<List<StyledText>> GetPrefixSuffixLines()
            {
                var lines = new List<List<StyledText>>();

                var mods = Entity?.GetComponent<Mods>();
                bool isUnique = mods?.ItemRarity == ItemRarity.Unique;
                bool isSpecial = Tier == 17;
                if (ModCount <= 0 || (isUnique && !isSpecial) || IsLogbook || IsHeist || IsMavenMap || IsValdoMap) return lines;

                var green = new nuVector4(0f, 1f, 0f, 1f);
                var red = new nuVector4(1f, 0.4f, 0.4f, 1f);
                var lime = new nuVector4(0.4f, 1f, 0.4f, 1f);
                var white = new nuVector4(1f, 1f, 1f, 1f);

                var qCol = white;
                if (pluginSettings.ColorQuantityPercent.Value) qCol = Quantity < pluginSettings.ColorQuantity.Value ? red : lime;
                var pCol = white;
                if (pluginSettings.ColorPackSizePercent.Value) pCol = PackSize < pluginSettings.ColorPackSize.Value ? red : lime;
                var rCol = white;
                if (pluginSettings.ColorRarityPercent.Value) rCol = Rarity < pluginSettings.ColorRarity.Value ? red : lime;

                bool showQuant = pluginSettings.ShowQuantityPercent.Value && Quantity != 0;
                bool showPack = pluginSettings.ShowPackSizePercent.Value && PackSize != 0;
                bool showRarity = pluginSettings.ShowRarityPercent.Value && Rarity != 0;

                var tLine = new List<StyledText> { new StyledText { Text = string.Empty, Color = white, EscapedText = string.Empty } };
                if (showQuant) tLine.Add(new StyledText { Text = $"{Quantity}% IIQ", Color = qCol, EscapedText = $"{Quantity}%% IIQ" });
                if (showPack) tLine.Add(new StyledText { Text = $"{PackSize}% PS", Color = pCol, EscapedText = $"{PackSize}%% PS" });
                if (showRarity) tLine.Add(new StyledText { Text = $"{Rarity}% IIR", Color = rCol, EscapedText = $"{Rarity}%% IIR" });
                if (tLine.Count > 1) lines.Add(tLine);

                if (pluginSettings.ShowPrefixSuffixStats.Value)
                {
                    // P: % IIQ % PS % IIR
                    var pLine = new List<StyledText> { new StyledText { Text = "P: ", Color = white, EscapedText = "P: " } };
                    if (showQuant) pLine.Add(new StyledText { Text = $"{PrefixStats.Quantity}% IIQ", Color = PrefixStats.Quantity > SuffixStats.Quantity ? green : white, EscapedText = $"{PrefixStats.Quantity}%% IIQ" });
                    if (showPack) pLine.Add(new StyledText { Text = $"{PrefixStats.PackSize}% PS", Color = PrefixStats.PackSize > SuffixStats.PackSize ? green : white, EscapedText = $"{PrefixStats.PackSize}%% PS" });
                    if (showRarity) pLine.Add(new StyledText { Text = $"{PrefixStats.Rarity}% IIR", Color = PrefixStats.Rarity > SuffixStats.Rarity ? green : white, EscapedText = $"{PrefixStats.Rarity}%% IIR" });
                    if (pLine.Count > 1) lines.Add(pLine);

                    // S: % IIQ % PS % IIR
                    var sLine = new List<StyledText> { new StyledText { Text = "S: ", Color = white, EscapedText = "S: " } };
                    if (showQuant) sLine.Add(new StyledText { Text = $"{SuffixStats.Quantity}% IIQ", Color = SuffixStats.Quantity > PrefixStats.Quantity ? green : white, EscapedText = $"{SuffixStats.Quantity}%% IIQ" });
                    if (showPack) sLine.Add(new StyledText { Text = $"{SuffixStats.PackSize}% PS", Color = SuffixStats.PackSize > PrefixStats.PackSize ? green : white, EscapedText = $"{SuffixStats.PackSize}%% PS" });
                    if (showRarity) sLine.Add(new StyledText { Text = $"{SuffixStats.Rarity}% IIR", Color = SuffixStats.Rarity > PrefixStats.Rarity ? green : white, EscapedText = $"{SuffixStats.Rarity}%% IIR" });
                    if (sLine.Count > 1) lines.Add(sLine);
                }

                return lines;
            }

            /// <summary>
            /// Generates the Originator (Nightmare) stat breakdown lines.
            /// </summary>
            public List<List<StyledText>> GetOriginatorBreakdownLines()
            {
                var lines = new List<List<StyledText>>();
                var white = new nuVector4(1f, 1f, 1f, 1f);
                var mapsColor = new nuVector4(0.5f, 0.85f, 1f, 1f);
                var scarabsColor = new nuVector4(0.85f, 0.45f, 0.85f, 1f);
                var currencyColor = new nuVector4(0.0f, 1.0f, 0.0f, 1.0f);

                if (IsLogbook || IsHeist || IsMavenMap || IsValdoMap) return lines;

                if (pluginSettings.ShowOriginatorMaps.Value)
                {
                    lines.Add(new List<StyledText> {
                        new StyledText { Text = OriginatorMaps == 0 ? "--" : $"{OriginatorMaps}%", Color = mapsColor, EscapedText = OriginatorMaps == 0 ? "--" : $"{OriginatorMaps}%%" },
                        new StyledText { Text = "Maps", Color = mapsColor, EscapedText = "Maps" },
                        new StyledText { Text = PrefixStats.MoreMaps == 0 ? "P: --" : $"P: {PrefixStats.MoreMaps}%", Color = white, EscapedText = PrefixStats.MoreMaps == 0 ? "P: --" : $"P: {PrefixStats.MoreMaps}%%" },
                        new StyledText { Text = SuffixStats.MoreMaps == 0 ? "S: --" : $"S: {SuffixStats.MoreMaps}%", Color = white, EscapedText = SuffixStats.MoreMaps == 0 ? "S: --" : $"S: {SuffixStats.MoreMaps}%%" }
                    });
                }

                if (pluginSettings.ShowOriginatorScarabs.Value)
                {
                    lines.Add(new List<StyledText> {
                        new StyledText { Text = OriginatorScarabs == 0 ? "--" : $"{OriginatorScarabs}%", Color = scarabsColor, EscapedText = OriginatorScarabs == 0 ? "--" : $"{OriginatorScarabs}%%" },
                        new StyledText { Text = "Scarabs", Color = scarabsColor, EscapedText = "Scarabs" },
                        new StyledText { Text = PrefixStats.MoreScarabs == 0 ? "P: --" : $"P: {PrefixStats.MoreScarabs}%", Color = white, EscapedText = PrefixStats.MoreScarabs == 0 ? "P: --" : $"P: {PrefixStats.MoreScarabs}%%" },
                        new StyledText { Text = SuffixStats.MoreScarabs == 0 ? "S: --" : $"S: {SuffixStats.MoreScarabs}%", Color = white, EscapedText = SuffixStats.MoreScarabs == 0 ? "S: --" : $"S: {SuffixStats.MoreScarabs}%%" }
                    });
                }

                if (pluginSettings.ShowOriginatorCurrency.Value)
                {
                    lines.Add(new List<StyledText> {
                        new StyledText { Text = OriginatorCurrency == 0 ? "--" : $"{OriginatorCurrency}%", Color = currencyColor, EscapedText = OriginatorCurrency == 0 ? "--" : $"{OriginatorCurrency}%%" },
                        new StyledText { Text = "Currency", Color = currencyColor, EscapedText = "Currency" },
                        new StyledText { Text = PrefixStats.MoreCurrency == 0 ? "P: --" : $"P: {PrefixStats.MoreCurrency}%", Color = white, EscapedText = PrefixStats.MoreCurrency == 0 ? "P: --" : $"P: {PrefixStats.MoreCurrency}%%" },
                        new StyledText { Text = SuffixStats.MoreCurrency == 0 ? "S: --" : $"S: {SuffixStats.MoreCurrency}%", Color = white, EscapedText = SuffixStats.MoreCurrency == 0 ? "S: --" : $"S: {SuffixStats.MoreCurrency}%%" }
                    });
                }

                return lines;
            }

            private void ProcessFlags(Base baseComponent)
            {
                IsOriginatorMap = false;
                Bricked = false;
                Corrupted = baseComponent?.isCorrupted ?? false;
                IsHeist = Entity.HasComponent<HeistContract>() || Entity.HasComponent<HeistBlueprint>();
                NeedsPadding = Tier != -1 || IsMavenMap || IsLogbook || IsHeist || ClassID.Contains("MiscMapItem");
            }

            private void ProcessQuality(Quality qualityComponent, Mods modsComponent)
            {
                ChiselName = string.Empty; ChiselValue = 0;
                int quality = qualityComponent?.ItemQuality ?? 0;
                int quantity = (modsComponent?.AlternateQualityType?.Id != "MapRarityQuality") ? quality : 0;
                int rarity = (modsComponent?.AlternateQualityType?.Id == "MapRarityQuality") ? quality : 0;

                if (modsComponent?.AlternateQualityType != null)
                {
                    var qId = modsComponent.AlternateQualityType.Id;
                    if (qId == "MapRarityQuality") { ChiselName = "Rarity Chisel"; ChiselValue = 40; }
                    else if (qId == "MapQuantityQuality") { ChiselName = "Quantity Chisel"; if (quantity == 0) quantity = ParseElementForQuality(Item?.Tooltip); ChiselValue = quantity; }
                    else if (qId == "MapPackSizeQuality") { ChiselName = "Pack Size Chisel"; ChiselValue = 10; }
                    else if (qId == "MapDivinationCardQuality") { ChiselName = "Divination Chisel"; ChiselValue = 50; }
                    else if (qId == "MapScarabQuality") { ChiselName = "Scarab Chisel"; ChiselValue = 50; }
                    else if (qId == "MapCurrencyQuality") { ChiselName = "Currency Chisel"; ChiselValue = 50; }
                }
                else if (quantity == 0) quantity = ParseElementForQuality(Item?.Tooltip);
                Quantity = quantity; Rarity = rarity;
            }

            private void ProcessMods(Mods modsComponent, string path)
            {
                int packSize = 0, quantity = Quantity, rarity = Rarity;
                int originatorScarabs = 0, originatorCurrency = 0, originatorMaps = 0;
                PrefixStats = new MapModStats();
                SuffixStats = new MapModStats();

                var itemMods = modsComponent?.ItemMods;
                ModCount = itemMods?.Count ?? 0;
                if (modsComponent != null && itemMods != null && ModCount > 0)
                {
                    if (modsComponent.ItemRarity != ItemRarity.Unique || Tier == 17)
                    {
                        foreach (var mod in itemMods)
                        {
                            if (string.IsNullOrEmpty(mod.RawName)) continue;
                            if (ModNameBlacklist.Any(black => mod.RawName.Contains(black))) { ModCount--; continue; }

                            var type = mod.ModRecord?.AffixType.ToString() ?? "";
                            bool isPrefix = type.Contains("Prefix");
                            bool isSuffix = type.Contains("Suffix");

                            var stats = mod.ModRecord?.StatNames; var values = mod.Values;
                            if (stats != null && values != null)
                            {
                                for (int i = 0; i < stats.Length && i < values.Count; i++)
                                    UpdateStatsFromMod(stats[i].Key, values[i], isPrefix, isSuffix, 
                                        ref quantity, ref rarity, ref packSize, 
                                        ref originatorScarabs, ref originatorCurrency, ref originatorMaps);
                            }
                            if (!IsOriginatorMap && (mod.RawName == "IsUberMap" || mod.RawName.Contains("MapZanaInfluence"))) IsOriginatorMap = true;
                            ProcessModWarnings(mod.RawName);
                        }
                    }
                }
                Quantity = quantity; PackSize = packSize; Rarity = rarity;
                OriginatorScarabs = originatorScarabs; OriginatorCurrency = originatorCurrency; OriginatorMaps = originatorMaps;
            }

            private void UpdateStatsFromMod(string key, int val, bool isPrefix, bool isSuffix, 
                ref int quantity, ref int rarity, ref int packSize, 
                ref int scarabs, ref int currency, ref int maps)
            {
                if (string.IsNullOrEmpty(key)) return;
                
                // Using StringComparison.OrdinalIgnoreCase avoids allocating a new string with .ToLower()
                if (key.Contains("quantity", StringComparison.OrdinalIgnoreCase))
                {
                    quantity += val;
                    if (isPrefix) PrefixStats.Quantity += val; else if (isSuffix) SuffixStats.Quantity += val;
                }
                else if (key.Contains("rarity", StringComparison.OrdinalIgnoreCase))
                {
                    rarity += val;
                    if (isPrefix) PrefixStats.Rarity += val; else if (isSuffix) SuffixStats.Rarity += val;
                }
                else if (key.Contains("pack_size", StringComparison.OrdinalIgnoreCase))
                {
                    packSize += val;
                    if (isPrefix) PrefixStats.PackSize += val; else if (isSuffix) SuffixStats.PackSize += val;
                }
                else if (key.Contains("scarab_drop_chance", StringComparison.OrdinalIgnoreCase))
                {
                    scarabs += val; IsOriginatorMap = true;
                    if (isPrefix) PrefixStats.MoreScarabs += val; else if (isSuffix) SuffixStats.MoreScarabs += val;
                }
                else if (key.Contains("currency_drop_chance", StringComparison.OrdinalIgnoreCase))
                {
                    currency += val; IsOriginatorMap = true;
                    if (isPrefix) PrefixStats.MoreCurrency += val; else if (isSuffix) SuffixStats.MoreCurrency += val;
                }
                else if (key.Contains("map_item_drop_chance", StringComparison.OrdinalIgnoreCase))
                {
                    maps += val; IsOriginatorMap = true;
                    if (isPrefix) PrefixStats.MoreMaps += val; else if (isSuffix) SuffixStats.MoreMaps += val;
                }
            }

            private void ProcessMapName(ExileCore.PoEMemory.Components.MapKey mapComponent, Base baseComponent, Mods modsComponent, string path, string itemName)
            {
                var mapTrim = !string.IsNullOrEmpty(itemName) ? itemName : (baseComponent?.Name ?? "Unknown");
                if (modsComponent?.ItemRarity == ItemRarity.Unique)
                {
                    if (!string.IsNullOrEmpty(modsComponent.UniqueName)) mapTrim = modsComponent.UniqueName;
                    else
                    {
                        try
                        {
                            var addr = mapComponent?.Address ?? 0; if (addr == 0) addr = Entity.Address;
                            var mapUnique = gameController.IngameState.M.Read<long>(addr + 0x10, 0x10, 0x20);
                            if (mapUnique != 0) { var resolvedArea = gameController.Files.WorldAreas.GetByAddress(mapUnique); if (resolvedArea != null) mapTrim = resolvedArea.Name; }
                        }
                        catch { }
                    }
                }
                mapTrim = MapTierRegex.Replace(mapTrim, "").Replace(" Map", "", StringComparison.OrdinalIgnoreCase).Trim();
                var displayTier = mapComponent?.Tier ?? Tier;
                MapName = displayTier > 0 ? $"[T{displayTier}] {mapTrim}" : mapTrim;
                EscapedMapName = EscapeImGui(MapName);
            }

            private void ProcessItemColor(Mods modsComponent)
            {
                switch (modsComponent?.ItemRarity ?? ItemRarity.Normal)
                {
                    case ItemRarity.Rare: ItemColor = new nuVector4(0.99f, 0.99f, 0.46f, 1f); break;
                    case ItemRarity.Magic: ItemColor = new nuVector4(0.68f, 0.68f, 1f, 1f); break;
                    case ItemRarity.Unique: ItemColor = new nuVector4(1f, 0.50f, 0.10f, 1f); break;
                    default: ItemColor = new nuVector4(1F, 1F, 1F, 1F); break;
                }
            }

            private void UpdateHeistDetails(HeistContract heistContract, HeistBlueprint heistBlueprint, Mods mods)
            {
                HeistJobLines.Clear();
                if (heistContract != null)
                {
                    HeistAreaLevel = mods?.ItemLevel ?? 0;
                    var jobName = heistContract.RequiredJob?.Name ?? "Unknown Job";
                    HeistJobLines.Add(new HeistJobLine { Text = $"{jobName} (Level {heistContract.RequiredJobLevel})", IsRevealed = true });
                }
                else if (heistBlueprint != null)
                {
                    HeistAreaLevel = heistBlueprint.AreaLevel;
                    if (heistBlueprint.Wings != null)
                    {
                        var revealedWingsCount = ParseElementForWings(Item?.Tooltip);
                        var summaryRequirements = ParseElementForRequirements(Item?.Tooltip);
                        int actuallyRevealed = 1;
                        for (int i = 0; i < heistBlueprint.Wings.Count; i++)
                        {
                            var w = heistBlueprint.Wings[i]; var wingReqs = new List<string>(); bool fitsSummary = true;
                            if (w.Jobs != null)
                            {
                                foreach (var job in w.Jobs)
                                {
                                    if (job.Item1 == null) continue;
                                    var jobName = job.Item1.Name; var jobLevel = job.Item2;
                                    wingReqs.Add($"{jobName} {jobLevel}");
                                    if (summaryRequirements.TryGetValue(jobName, out var maxLevel)) { if (jobLevel > maxLevel) fitsSummary = false; }
                                    else fitsSummary = false;
                                }
                            }
                            bool isRevealed = i == 0 || (summaryRequirements.Count > 0 ? (actuallyRevealed < revealedWingsCount && fitsSummary) : i < revealedWingsCount);
                            HeistJobLines.Add(new HeistJobLine { Text = $"Wing {i + 1}: {string.Join(", ", wingReqs)}", IsRevealed = isRevealed });
                            if (i > 0 && isRevealed) actuallyRevealed++;
                        }
                    }
                }
            }
        }
    }
}