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
        private static readonly Regex MapTierRegex = new Regex(@"\s*\(Tier\s+\d+\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    }
    public partial class MapNotify_3_28
    {

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

                Tier = mapComponent?.Tier ?? -1;
                IsMavenMap = path.Contains("MavenMap") || path.Contains("Invitations/Maven");

                UpdateHeistDetails(heistContract, heistBlueprint, modsComponent);

                ProcessFlags(baseComponent);
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
                // NeedsPadding is primarily for UI compatibility with other plugins (like NinjaPricer)
                // It ensures the tooltip offset is respected even for items without "Map Mods"
                NeedsPadding = Tier != -1 || IsMavenMap || HeistJobLines.Count > 0 || ClassID.Contains("MiscMapItem");
            }

            private void ProcessQuality(Quality qualityComponent, Mods modsComponent)
            {
                ChiselName = string.Empty;
                ChiselValue = 0;
                int quality = qualityComponent?.ItemQuality ?? 0;
                int quantity = 0;
                int rarity = 0;

                if (modsComponent?.AlternateQualityType?.Id == "MapRarityQuality")
                {
                    rarity = quality;
                }
                else
                {
                    quantity = quality;
                }

                if (modsComponent?.AlternateQualityType != null)
                {
                    var qualityId = modsComponent.AlternateQualityType.Id;
                    if (qualityId == "MapRarityQuality") { ChiselName = "Rarity Chisel"; ChiselValue = 40; }
                    else if (qualityId == "MapQuantityQuality")
                    {
                        ChiselName = "Quantity Chisel";
                        if (quantity == 0) quantity = ParseElementForQuality(Item?.Tooltip);
                        ChiselValue = quantity;
                    }
                    else if (qualityId == "MapPackSizeQuality") { ChiselName = "Pack Size Chisel"; ChiselValue = 10; }
                    else if (qualityId == "MapDivinationCardQuality") { ChiselName = "Divination Chisel"; ChiselValue = 50; }
                    else if (qualityId == "MapScarabQuality") { ChiselName = "Scarab Chisel"; ChiselValue = 50; }
                    else if (qualityId == "MapCurrencyQuality") { ChiselName = "Currency Chisel"; ChiselValue = 50; }
                }
                else if (quantity == 0)
                {
                    quantity = ParseElementForQuality(Item?.Tooltip);
                }

                Quantity = quantity;
                Rarity = rarity;
            }

            private void ProcessMods(Mods modsComponent, string path)
            {
                int packSize = 0;
                int quantity = Quantity;
                int rarity = Rarity;
                int originatorScarabs = 0;
                int originatorCurrency = 0;
                int originatorMaps = 0;

                var itemMods = modsComponent?.ItemMods;
                ModCount = itemMods?.Count ?? 0;

                if (modsComponent != null && itemMods != null && ModCount > 0)
                {
                    if (modsComponent.ItemRarity != ItemRarity.Unique || path.Contains("Maven") || Tier == 17)
                    {
                        foreach (var mod in itemMods)
                        {
                            if (string.IsNullOrEmpty(mod.RawName)) continue;
                            bool blacklisted = false;
                            foreach (var black in ModNameBlacklist) { if (mod.RawName.Contains(black)) { blacklisted = true; break; } }
                            if (blacklisted) { ModCount--; continue; }

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
                                        case "map_pack_size_+%": packSize += values[i]; break;
                                        case "map_item_drop_quantity_+%": quantity += values[i]; break;
                                        case "map_item_drop_rarity_+%": rarity += values[i]; break;
                                        case "map_scarab_drop_chance_+%_final_from_uber_mod": originatorScarabs += values[i]; IsOriginatorMap = true; break;
                                        case "map_currency_drop_chance_+%_final_from_uber_mod": originatorCurrency += values[i]; IsOriginatorMap = true; break;
                                        case "map_map_item_drop_chance_+%_final_from_uber_mod": originatorMaps += values[i]; IsOriginatorMap = true; break;
                                    }
                                }
                            }

                            if (!IsOriginatorMap && (mod.RawName == "IsUberMap" || mod.RawName.Contains("MapZanaInfluence")))
                                IsOriginatorMap = true;

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
            }

            private void ProcessMapName(ExileCore.PoEMemory.Components.MapKey mapComponent, Base baseComponent, Mods modsComponent, string path, string itemName)
            {
                // Prioritize the translated itemName (from BaseItemTypes) since modern maps 
                // act as "Keys" and often lack traditional Area associations in memory.
                var mapTrim = !string.IsNullOrEmpty(itemName) ? itemName : (baseComponent?.Name ?? "Unknown");
                
                if (modsComponent?.ItemRarity == ItemRarity.Unique)
                {
                    // Prioritize the UniqueName property from the Mods component
                    if (!string.IsNullOrEmpty(modsComponent.UniqueName))
                    {
                        mapTrim = modsComponent.UniqueName;
                    }
                    else
                    {
                        try
                        {
                            var addr = mapComponent?.Address ?? 0;
                            if (addr == 0) addr = Entity.Address;
                            var mapUnique = gameController.IngameState.M.Read<long>(addr + 0x10, 0x10, 0x20);
                            if (mapUnique != 0)
                            {
                                var resolvedArea = gameController.Files.WorldAreas.GetByAddress(mapUnique);
                                if (resolvedArea != null) mapTrim = resolvedArea.Name;
                            }
                        }
                        catch { }
                    }
                }

                // Clean the name of suffixes to prevent double-tiering
                mapTrim = MapTierRegex.Replace(mapTrim, "");
                mapTrim = mapTrim.Replace(" Map", "", StringComparison.OrdinalIgnoreCase);
                mapTrim = mapTrim.Trim();

                var displayTier = mapComponent?.Tier ?? Tier;
                // Use the [TX] Name format
                MapName = displayTier > 0 ? $"[T{displayTier}] {mapTrim}" : mapTrim;
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
                        var revealedWingsCount = ParseElementForWings(Item?.Tooltip); // Get the actual count from the tooltip
                        var summaryRequirements = ParseElementForRequirements(Item?.Tooltip); // Still useful for job level checks
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
