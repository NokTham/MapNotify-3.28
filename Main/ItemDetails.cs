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

namespace MapNotify_3_28 // Keep the same namespace
{
    public partial class MapNotify_3_28
    {
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
        public List<StyledText> ActiveInterestMods { get; set; } = new List<StyledText>();
        public HashSet<StyledText> InterestShadowedMods { get; set; } = new HashSet<StyledText>();
        public List<StyledText> ConflictingMods { get; set; } = new List<StyledText>();
        public bool Bricked { get; set; }
        public bool Corrupted { get; set; }
        public bool Identified { get; set; }

        // Base Stats
        public int Tier { get; set; }
        // Number of map mods (enchantments excluded). Used by the mod counter and the 8/9-mod marker.
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
        public int OriginatorThreads { get; set; }
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

        private long _lastUpdate;
        private long _lastUpdateCall;

        // Tooltip line builders are only refreshed when Update() runs, not every frame.
        private List<List<StyledText>> _prefixSuffixLines;
        private List<List<StyledText>> _originatorBreakdownLines;

        public class MapModStats
        {
            public int Quantity { get; set; }
            public int Rarity { get; set; }
            public int PackSize { get; set; }
            public int MoreMaps { get; set; }
            public int MoreThreads { get; set; }
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
        /// Rebinds this details object to the item's current slot wrapper. The HUD
        /// component lives on the Entity, so it survives moves while the stored
        /// wrapper still points at the previous slot. When the slot changes, tooltip
        /// parsing must run again against the new wrapper.
        /// </summary>
        public void SetItem(NormalInventoryItem item)
        {
            if (item == null) return;

            bool slotChanged = Item == null || Item.Address != item.Address;
            Item = item;

            if (slotChanged)
                _lastUpdate = 0;
        }

        /// <summary>
        /// Refreshes all item properties by retrieving data from game components.
        /// </summary>
        public void Update()
        {
            var now = Environment.TickCount64;

            // Update() is only called while the item is hovered (plus once at
            // creation), so a gap means hovering just started: re-parse the
            // tooltip-only data immediately instead of waiting for the interval.
            if (now - _lastUpdateCall > 100)
                _lastUpdate = 0;
            _lastUpdateCall = now;

            if (now - _lastUpdate < pluginSettings.InventoryCacheInterval.Value)
                return;

            _lastUpdate = now;
            WindowID = $"##{Entity.Address}";

            var localGood = new List<StyledText>();
            var localBad = new List<StyledText>();
            var localInterest = new List<StyledText>();
            var localInterestKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var localInterestShadowed = new HashSet<StyledText>();

            // Good/Bad entries are appended once per matching mod occurrence. Logbooks repeat
            // the same implicit across several areas, so track normalized keys to avoid
            // rendering the same warning line many times.
            var localMatchedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var localConflicting = new List<StyledText>();

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

            ProcessFlags(baseComponent, modsComponent);

            LogbookAreaLevel = IsLogbook ? (expeditionSaga?.AreaLevel ?? (Tier > 0 ? Tier : 0)) : 0;

            if (IsLogbook) ProcessLogbookDetails(expeditionSaga, localGood, localBad, localInterest, localInterestKeys, localInterestShadowed, localMatchedKeys);

            // Performance and Stability: Only parse tooltips if the item is currently hovered.
            // Tooltip parsing is expensive and prone to 'startIndex' errors when items are in lockers/stashes.
            bool isHovered = ingameState?.UIHover?.Address == Item?.Address;
            if (isHovered)
            {
                var tooltipData = ParseTooltip(Item?.Tooltip);
                UpdateHeistDetails(heistContract, heistBlueprint, modsComponent, tooltipData);
                ProcessQuality(qualityComponent, modsComponent, tooltipData);
            }

            ProcessMods(modsComponent, path, localGood, localBad, localInterest, localInterestKeys, localInterestShadowed, localMatchedKeys);
            ProcessMapName(mapComponent, baseComponent, modsComponent, path, itemName);
            ProcessItemColor(modsComponent);

            ActiveGoodMods = localGood;
            ActiveBadMods = localBad;
            ActiveInterestMods = localInterest;
            InterestShadowedMods = localInterestShadowed;
            ConflictingMods = localConflicting;

            // Build the expensive tooltip lines once per update instead of every frame.
            if (pluginSettings.ShowPrefixSuffixStats.Value)
            {
                _prefixSuffixLines = BuildPrefixSuffixLines();
                _originatorBreakdownLines = BuildOriginatorBreakdownLines();
            }
            else
            {
                _prefixSuffixLines = null;
                _originatorBreakdownLines = null;
            }
        }

        private void ProcessLogbookDetails(ExpeditionSaga expeditionSaga, List<StyledText> goodList, List<StyledText> badList,
            List<StyledText> interestList, HashSet<string> interestKeys, HashSet<StyledText> interestShadowed,
            HashSet<string> matchedKeys)
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
                    var interestMatch = MatchInterest(mod.RawName);
                    var styledMod = new StyledText
                    {
                        Text = match?.Text ?? cleanText,
                        EscapedText = EscapeImGui(match?.Text ?? cleanText),
                        Color = match?.Color ?? new nuVector4(0.9f, 0.9f, 0.9f, 1f),
                        Bricking = match?.Bricking ?? false
                    };

                    if (match != null || interestMatch != null)
                    {
                        if (match != null && match.Bricking) Bricked = true;
                        ProcessModWarnings(mod.RawName, goodList, badList, interestList, interestKeys, interestShadowed, matchedKeys);
                    }
                    logArea.Implicits.Add(styledMod);
                }
                LogbookAreas.Add(logArea);
            }
        }

        /// <summary>
        /// Checks a mod against Good and Bad dictionaries and updates the item's active warning lists.
        /// </summary>
        private void ProcessModWarnings(string rawName, List<StyledText> goodList, List<StyledText> badList,
            List<StyledText> interestList, HashSet<string> interestKeys, HashSet<StyledText> interestShadowed,
            HashSet<string> matchedKeys)
        {
            if (string.IsNullOrEmpty(rawName)) return;

            // Normalize once, then reuse the key for the Good/Bad/Interest lookups.
            var normalizedInput = BaseModExtractor.GetBaseMod(rawName);

            // An empty key would match every config entry, so never look it up.
            if (string.IsNullOrEmpty(normalizedInput)) return;

            StyledText match = null;
            bool isGood = false;
            if (GoodModsDictionary != null && GoodModsDictionary.TryGetValue(normalizedInput, out var goodMatch))
            {
                match = goodMatch;
                isGood = true;
            }
            else if (BadModsDictionary != null && BadModsDictionary.TryGetValue(normalizedInput, out var badMatch))
            {
                match = badMatch;
            }

            if (match != null && isGood)
            {
                if (string.IsNullOrEmpty(match.EscapedText)) match.EscapedText = EscapeImGui(match.Text);
                if (match.Bricking) Bricked = true;
                if (matchedKeys.Add(normalizedInput)) goodList.Add(match);
            }
            else if (match != null && !isGood)
            {
                if (string.IsNullOrEmpty(match.EscapedText)) match.EscapedText = EscapeImGui(match.Text);
                if (match.Bricking) Bricked = true;
                if (matchedKeys.Add(normalizedInput)) badList.Add(match);
            }

            // Interest is independent of Good/Bad: a mod may be both.
            StyledText interestMatch = null;
            if (InterestModsDictionary != null)
                InterestModsDictionary.TryGetValue(normalizedInput, out interestMatch);

            if (interestMatch != null)
            {
                if (interestKeys.Add(normalizedInput))
                {
                    interestList.Add(new StyledText
                    {
                        Text = interestMatch.Text,
                        EscapedText = string.IsNullOrEmpty(interestMatch.EscapedText)
                            ? EscapeImGui(interestMatch.Text)
                            : interestMatch.EscapedText,
                        Color = interestMatch.Color,
                        Bricking = match?.Bricking ?? false
                    });
                }

                if (match != null)
                    interestShadowed.Add(match);
            }
        }

        /// <summary>
        /// Generates the formatted Prefix/Suffix breakdown lines for the tooltip.
        /// Highlights the higher value between Prefix and Suffix in green.
        /// </summary>
        public List<List<StyledText>> GetPrefixSuffixLines()
        {
            return _prefixSuffixLines ??= BuildPrefixSuffixLines();
        }

        private List<List<StyledText>> BuildPrefixSuffixLines()
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
            if (showQuant) tLine.Add(new StyledText { Text = $"{Quantity}% AD", Color = qCol, EscapedText = $"{Quantity}%% AD" });
            if (showPack) tLine.Add(new StyledText { Text = $"{PackSize}% PS", Color = pCol, EscapedText = $"{PackSize}%% PS" });
            if (showRarity) tLine.Add(new StyledText { Text = $"{Rarity}% IIR", Color = rCol, EscapedText = $"{Rarity}%% IIR" });
            if (tLine.Count > 1) lines.Add(tLine);

            if (pluginSettings.ShowPrefixSuffixStats.Value)
            {
                // P: % AD % PS % IIR
                var pLine = new List<StyledText> { new StyledText { Text = "P: ", Color = white, EscapedText = "P: " } };
                if (showQuant) pLine.Add(new StyledText { Text = $"{PrefixStats.Quantity}% AD", Color = PrefixStats.Quantity > SuffixStats.Quantity ? green : white, EscapedText = $"{PrefixStats.Quantity}%% AD" });
                if (showPack) pLine.Add(new StyledText { Text = $"{PrefixStats.PackSize}% PS", Color = PrefixStats.PackSize > SuffixStats.PackSize ? green : white, EscapedText = $"{PrefixStats.PackSize}%% PS" });
                if (showRarity) pLine.Add(new StyledText { Text = $"{PrefixStats.Rarity}% IIR", Color = PrefixStats.Rarity > SuffixStats.Rarity ? green : white, EscapedText = $"{PrefixStats.Rarity}%% IIR" });
                if (pLine.Count > 1) lines.Add(pLine);

                // S: % AD % PS % IIR
                var sLine = new List<StyledText> { new StyledText { Text = "S: ", Color = white, EscapedText = "S: " } };
                if (showQuant) sLine.Add(new StyledText { Text = $"{SuffixStats.Quantity}% AD", Color = SuffixStats.Quantity > PrefixStats.Quantity ? green : white, EscapedText = $"{SuffixStats.Quantity}%% AD" });
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
            return _originatorBreakdownLines ??= BuildOriginatorBreakdownLines();
        }

        private List<List<StyledText>> BuildOriginatorBreakdownLines()
        {
            var lines = new List<List<StyledText>>();
            var white = new nuVector4(1f, 1f, 1f, 1f);
            var mapsColor = new nuVector4(0.5f, 0.85f, 1f, 1f);
            var threadsColor = new nuVector4(0.85f, 0.45f, 0.85f, 1f);
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
                        new StyledText { Text = OriginatorThreads == 0 ? "--" : $"{OriginatorThreads}%", Color = threadsColor, EscapedText = OriginatorThreads == 0 ? "--" : $"{OriginatorThreads}%%" },
                        new StyledText { Text = "Threads", Color = threadsColor, EscapedText = "Threads" },
                        new StyledText { Text = PrefixStats.MoreThreads == 0 ? "P: --" : $"P: {PrefixStats.MoreThreads}%", Color = white, EscapedText = PrefixStats.MoreThreads == 0 ? "P: --" : $"P: {PrefixStats.MoreThreads}%%" },
                        new StyledText { Text = SuffixStats.MoreThreads == 0 ? "S: --" : $"S: {SuffixStats.MoreThreads}%", Color = white, EscapedText = SuffixStats.MoreThreads == 0 ? "S: --" : $"S: {SuffixStats.MoreThreads}%%" }
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

        private void ProcessFlags(Base baseComponent, Mods modsComponent)
        {
            IsOriginatorMap = false;
            Bricked = false;
            Corrupted = baseComponent?.isCorrupted ?? false;
            Identified = modsComponent?.Identified ?? true;
            IsHeist = Entity.HasComponent<HeistContract>() || Entity.HasComponent<HeistBlueprint>();
            NeedsPadding = Tier != -1 || IsMavenMap || IsLogbook || IsHeist || ClassID.Contains("MiscMapItem");
        }

        private void ProcessQuality(Quality qualityComponent, Mods modsComponent, TooltipData tooltipData)
        {
            ChiselName = string.Empty; ChiselValue = 0;
            int quality = qualityComponent?.ItemQuality ?? 0;
            int quantity = (modsComponent?.AlternateQualityType?.Id != "MapRarityQuality") ? quality : 0;
            int rarity = (modsComponent?.AlternateQualityType?.Id == "MapRarityQuality") ? quality : 0;

            if (modsComponent?.AlternateQualityType != null)
            {
                var qId = modsComponent.AlternateQualityType.Id;
                // 3.30: Maven's Chisel of Procurement grants +25% Item Rarity at 20% Quality (previously +40%).
                if (qId == "MapRarityQuality") { ChiselName = "Rarity Chisel"; ChiselValue = 40; }
                // 3.30: Item Quantity becomes Atlas Drops; verify whether this chisel/id/label is renamed.
                else if (qId == "MapQuantityQuality") { ChiselName = "Quantity Chisel"; if (quantity == 0) quantity = tooltipData.Quality; ChiselValue = quantity; }
                // else if (qId == "MapAtlasDropsQuality") { ChiselName = "Atlas Drops Chisel"; if (quantity == 0) quantity = tooltipData.Quality; ChiselValue = quantity; }
                else if (qId == "MapPackSizeQuality") { ChiselName = "Pack Size Chisel"; ChiselValue = 10; }
                else if (qId == "MapDivinationCardQuality") { ChiselName = "Divination Chisel"; ChiselValue = 50; }
                // 3.30: Maven's Chisel of Scarabs is now Maven's Chisel of Threads and
                // increases Thread quantity. The statistic key is unchanged, so the
                // alternate-quality id is expected to remain MapScarabQuality.
                else if (qId == "MapScarabQuality") { ChiselName = "Thread Chisel"; ChiselValue = 50; }
                else if (qId == "MapCurrencyQuality") { ChiselName = "Currency Chisel"; ChiselValue = 50; }
            }
            else if (quantity == 0) quantity = tooltipData.Quality;
            Quantity = quantity; Rarity = rarity;
        }

        private void ProcessMods(Mods modsComponent, string path, List<StyledText> goodList, List<StyledText> badList,
            List<StyledText> interestList, HashSet<string> interestKeys, HashSet<StyledText> interestShadowed,
            HashSet<string> matchedKeys)
        {
            PrefixStats = new MapModStats();
            SuffixStats = new MapModStats();
            var totals = new MapModStats { Quantity = Quantity, Rarity = Rarity };

            var itemMods = modsComponent?.ItemMods;
            ModCount = itemMods?.Count ?? 0;

            // Count only map mods: blacklisted entries and enchantments are not part of the count.
            // 3.30: Delirium Orbs now add an explicit map modifier instead of an enchantment.
            // Patch notes describe one Delirium modifier whose Delirious percentage stacks
            // to 100% (not one modifier per orb). It is intended to count toward ModCount;
            // the tooltip should also show its Delirious % and reward type so players can
            // distinguish "8 mods" from "7 mods + Delirium".
            if (itemMods != null)
            {
                foreach (var mod in itemMods)
                {
                    if (string.IsNullOrEmpty(mod.RawName)) continue;
                    if (Constants.IsModBlacklisted(mod.RawName)) { ModCount--; continue; }

                    var type = mod.ModRecord?.AffixType.ToString() ?? "";
                    if (type.Contains("Enchant", StringComparison.OrdinalIgnoreCase) ||
                        mod.RawName.Contains("Enchant", StringComparison.OrdinalIgnoreCase))
                        ModCount--;
                }
            }

            if (modsComponent != null && itemMods != null && itemMods.Count > 0)
            {
                if (modsComponent.ItemRarity != ItemRarity.Unique || Tier == 17)
                {
                    foreach (var mod in itemMods)
                    {
                        if (string.IsNullOrEmpty(mod.RawName)) continue;

                        // Check for Originator/Nightmare flags before hitting the blacklist
                        if (!IsOriginatorMap && (mod.RawName == "IsUberMap" || mod.RawName.Contains("MapZanaInfluence"))) IsOriginatorMap = true;

                        if (Constants.IsModBlacklisted(mod.RawName)) continue;

                        var type = mod.ModRecord?.AffixType.ToString() ?? "";
                        bool isPrefix = type.Contains("Prefix");
                        bool isSuffix = type.Contains("Suffix");

                        var stats = mod.ModRecord?.StatNames; var values = mod.Values;
                        if (stats != null && values != null)
                        {
                            for (int i = 0; i < stats.Length && i < values.Count; i++)
                                UpdateStatsFromMod(stats[i].Key, values[i], isPrefix, isSuffix, totals);
                        }
                        ProcessModWarnings(mod.RawName, goodList, badList, interestList, interestKeys, interestShadowed, matchedKeys);
                    }
                }
            }
            Quantity = totals.Quantity; PackSize = totals.PackSize; Rarity = totals.Rarity;
            OriginatorThreads = totals.MoreThreads; OriginatorCurrency = totals.MoreCurrency; OriginatorMaps = totals.MoreMaps;
        }

        private void UpdateStatsFromMod(string key, int val, bool isPrefix, bool isSuffix, MapModStats totals)
        {
            if (string.IsNullOrEmpty(key)) return;

            void Apply(MapModStats target)
            {
                switch (MapYield.ClassifyStatKey(key))
                {
                    // Atlas Drops is a percentage scaling stat (map_drop_scaling_+%).
                    // The field is still named Quantity until the 3.30 model rename,
                    // so keep the legacy display behavior meanwhile.
                    case MapYield.Stat.AtlasDrops: target.Quantity += val; break;
                    case MapYield.Stat.ItemRarity: target.Rarity += val; break;
                    case MapYield.Stat.PackSize: target.PackSize += val; break;
                    case MapYield.Stat.MoreThreads: target.MoreThreads += val; IsOriginatorMap = true; break;
                    case MapYield.Stat.MoreCurrency: target.MoreCurrency += val; IsOriginatorMap = true; break;
                    case MapYield.Stat.MoreMaps: target.MoreMaps += val; IsOriginatorMap = true; break;
                }
            }

            // Always apply to the totals
            Apply(totals);

            // Apply specifically to Prefix or Suffix trackers if applicable
            if (isPrefix) Apply(PrefixStats);
            else if (isSuffix) Apply(SuffixStats);
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
            mapTrim = Constants.MapTierRegex.Replace(mapTrim, "").Replace(" Map", "", StringComparison.OrdinalIgnoreCase).Trim();
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

        private void UpdateHeistDetails(HeistContract heistContract, HeistBlueprint heistBlueprint, Mods mods, TooltipData tooltipData)
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
                    var revealedWingsCount = tooltipData.Wings;
                    var summaryRequirements = tooltipData.Requirements;
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