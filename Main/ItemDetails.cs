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
            public NormalInventoryItem Item { get; }
            public Entity Entity { get; }
            public List<StyledText> ActiveGoodMods { get; set; }
            public List<StyledText> ActiveBadMods { get; set; }
            public nuVector4 ItemColor { get; set; }
            public string MapName { get; set; }
            public string EscapedMapName { get; set; }
            public string ClassID { get; set; }
            public string ChiselName { get; set; }
            public int ChiselValue { get; set; }
            public int PackSize { get; set; }
            public int Quantity { get; set; }
            public int Rarity { get; set; }
            public int ModCount { get; set; }
            public int HeistAreaLevel { get; set; }
            public int LogbookAreaLevel { get; set; }
            public class LogbookArea
            {
                public string Name { get; set; }
                public string Faction { get; set; }
                public List<StyledText> Implicits { get; set; } = new List<StyledText>();
            }
            public List<LogbookArea> LogbookAreas { get; set; } = new List<LogbookArea>();
            public List<HeistJobLine> HeistJobLines { get; set; } = new List<HeistJobLine>();
            public bool NeedsPadding { get; set; }
            public bool Bricked { get; set; }
            public bool Corrupted { get; set; }
            public int Tier { get; set; }
            public int OriginatorScarabs { get; set; }
            public int OriginatorCurrency { get; set; }
            public int OriginatorMaps { get; set; }
            public bool IsOriginatorMap { get; set; }
            public bool IsMavenMap { get; set; }
            public bool IsLogbook { get; set; }
            public string WindowID { get; private set; }
            public ItemDetails(NormalInventoryItem Item, Entity Entity)
            {
                this.Item = Item;
                this.Entity = Entity;
                ActiveGoodMods = new List<StyledText>();
                ActiveBadMods = new List<StyledText>();
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
                IsMavenMap = path.Contains("MavenMap") || path.Contains("Invitations/Maven") || path.Contains("MavenInvitation");
                IsLogbook = path.Contains("ExpeditionLogbook");
                
                ProcessFlags(baseComponent);

                LogbookAreaLevel = IsLogbook ? (expeditionSaga?.AreaLevel ?? (Tier > 0 ? Tier : 0)) : 0;

                LogbookAreas.Clear();
                if (IsLogbook && expeditionSaga != null && expeditionSaga.Areas != null)
                {
                    foreach (var area in expeditionSaga.Areas)
                    {
                        if (area == null) continue;
                        var logArea = new LogbookArea { Name = area.Name, Faction = area.Faction };
                        if (area.ImplicitMods != null)
                        {
                            foreach (var mod in area.ImplicitMods)
                            {
                                if (mod == null) continue;
                                var modText = !string.IsNullOrEmpty(mod.Translation) ? mod.Translation : mod.Name;
                                var cleanText = !string.IsNullOrEmpty(modText) ? TooltipTagsRegex.Replace(modText, "") : string.Empty;
                                if (!string.IsNullOrEmpty(cleanText))
                                {
                                    var styledMod = new StyledText { Text = cleanText, EscapedText = EscapeImGui(cleanText), Color = new nuVector4(0.9f, 0.9f, 0.9f, 1f) };
                                    if (!string.IsNullOrEmpty(mod.RawName))
                                    {
                                        bool found = false;
                                        foreach (var entry in GoodModsDictionary)
                                        {
                                            if (mod.RawName.Contains(entry.Key))
                                            {
                                                styledMod.Color = entry.Value.Color;
                                                if (entry.Value.Bricking) Bricked = true;
                                                ActiveGoodMods.Add(styledMod);
                                                found = true; break;
                                            }
                                        }
                                        if (!found)
                                        {
                                            foreach (var entry in BadModsDictionary)
                                            {
                                                if (mod.RawName.Contains(entry.Key))
                                                {
                                                    styledMod.Color = entry.Value.Color;
                                                    styledMod.Bricking = entry.Value.Bricking;
                                                    if (entry.Value.Bricking) Bricked = true;
                                                    ActiveBadMods.Add(styledMod);
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    logArea.Implicits.Add(styledMod);
                                }
                            }
                        }
                        LogbookAreas.Add(logArea);
                    }
                }

                UpdateHeistDetails(heistContract, heistBlueprint, modsComponent);
                ProcessQuality(qualityComponent, modsComponent);
                ProcessMods(modsComponent, path);
                ProcessMapName(mapComponent, baseComponent, modsComponent, path, itemName);
                ProcessItemColor(modsComponent);
            }

            private void ProcessFlags(Base baseComponent)
            {
                IsOriginatorMap = false;
                Bricked = false;
                Corrupted = baseComponent?.isCorrupted ?? false;
                bool isHeist = Entity.HasComponent<HeistContract>() || Entity.HasComponent<HeistBlueprint>();
                NeedsPadding = Tier != -1 || IsMavenMap || IsLogbook || isHeist || ClassID.Contains("MiscMapItem");
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
                int packSize = 0, quantity = Quantity, rarity = Rarity, originatorScarabs = 0, originatorCurrency = 0, originatorMaps = 0;
                var itemMods = modsComponent?.ItemMods;
                ModCount = itemMods?.Count ?? 0;
                if (modsComponent != null && itemMods != null && ModCount > 0)
                {
                    if (modsComponent.ItemRarity != ItemRarity.Unique || path.Contains("Maven") || Tier == 17)
                    {
                        foreach (var mod in itemMods)
                        {
                            if (string.IsNullOrEmpty(mod.RawName)) continue;
                            if (ModNameBlacklist.Any(black => mod.RawName.Contains(black))) { ModCount--; continue; }
                            var stats = mod.ModRecord?.StatNames; var values = mod.Values;
                            if (stats != null && values != null)
                            {
                                for (int i = 0; i < stats.Length && i < values.Count; i++)
                                {
                                    var key = stats[i].Key; if (string.IsNullOrEmpty(key)) continue;
                                    switch (key)
                                    {
                                        case "map_pack_size_+%": packSize += values[i]; break;
                                        case "map_item_drop_quantity_+%": quantity += values[i]; break;
                                        case "map_item_drop_rarity_+%": rarity += values[i]; break;
                                        case "map_scarab_drop_chance_+%_final_from_uber_mod": originatorScarabs += values[i]; IsOriginatorMap = true; break;
                                        case "map_currency_drop_chance_+%_final_from_uber_mod": originatorCurrency += values[i]; IsOriginatorMap = true; break;
                                        case "map_map_item_drop_chance_+%_final_from_uber_mod": originatorMaps += values[i]; IsOriginatorMap = true; break;
                                    }
                                }
                            }
                            if (!IsOriginatorMap && (mod.RawName == "IsUberMap" || mod.RawName.Contains("MapZanaInfluence"))) IsOriginatorMap = true;
                            bool foundMod = false;
                            foreach (var entry in GoodModsDictionary)
                            {
                                if (mod.RawName.Contains(entry.Key))
                                {
                                    var warning = entry.Value; if (string.IsNullOrEmpty(warning.EscapedText)) warning.EscapedText = EscapeImGui(warning.Text);
                                    if (warning.Bricking) Bricked = true; ActiveGoodMods.Add(warning); foundMod = true; break;
                                }
                            }
                            if (!foundMod)
                            {
                                foreach (var entry in BadModsDictionary)
                                {
                                    if (mod.RawName.Contains(entry.Key))
                                    {
                                        var bad = entry.Value; if (string.IsNullOrEmpty(bad.EscapedText)) bad.EscapedText = EscapeImGui(bad.Text);
                                        if (bad.Bricking) Bricked = true; ActiveBadMods.Add(bad); break;
                                    }
                                }
                            }
                        }
                    }
                }
                Quantity = quantity; PackSize = packSize; Rarity = rarity;
                OriginatorScarabs = originatorScarabs; OriginatorCurrency = originatorCurrency; OriginatorMaps = originatorMaps;
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
                        } catch { }
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