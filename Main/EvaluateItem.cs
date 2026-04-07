using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using nuVector4 = System.Numerics.Vector4;

namespace MapNotify_3_28
{
    public partial class MapNotify_3_28 : BaseSettingsPlugin<MapNotifySettings>
    {
        public static nuVector4 GetRarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare:
                    return new nuVector4(0.99f, 0.99f, 0.46f, 1f);
                case ItemRarity.Magic:
                    return new nuVector4(0.68f, 0.68f, 1f, 1f); //0.52f, 0.52f, 0.99f, 1f
                case ItemRarity.Unique:
                    return new nuVector4(1f, 0.50f, 0.10f, 1f);
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
            public string HeistJob { get; set; }
            public int HeistAreaLevel { get; set; }
            public int HeistLevel { get; set; }
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

                Tier = mapComponent?.Tier ?? -1;
                IsFragment = path.Contains("Fragments/") && !path.Contains("Maven");
                IsMavenMap = path.Contains("MavenMap") || path.Contains("Invitations/Maven");

                UpdateHeistDetails();
                IsOriginatorMap = false;
                Bricked = false;
                Corrupted = baseComponent?.isCorrupted ?? false;
                NeedsPadding = Tier != -1 || IsMavenMap || IsFragment || !string.IsNullOrEmpty(HeistJob);

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
                ItemColor = GetRarityColor(modsComponent?.ItemRarity ?? ItemRarity.Normal);
            }

            private int ParseTooltipQuality()
            {
                if (Item?.Tooltip == null) return 0;
                string FindQualityText(ExileCore.PoEMemory.Element element)
                {
                    if (element == null) return null;
                    if (!string.IsNullOrEmpty(element.Text) && element.Text.Contains("Quality"))
                        return element.Text;
                    var children = element.Children;
                    for (int i = 0; i < children.Count; i++)
                    {
                        var found = FindQualityText(children[i]);
                        if (found != null) return found;
                    }
                    return null;
                }
                var qualityLine = FindQualityText(Item.Tooltip);
                if (string.IsNullOrEmpty(qualityLine)) return 0;
                int start = -1;
                for (int i = 0; i < qualityLine.Length; i++) { if (char.IsDigit(qualityLine[i])) { start = i; break; } }
                if (start == -1) return 0;
                int end = qualityLine.IndexOf('%', start);
                if (end != -1 && int.TryParse(qualityLine.Substring(start, end - start), out var res)) return res;
                return 0;
            }

            private void UpdateHeistDetails()
            {
                var heistContract = Entity.GetComponent<HeistContract>();
                var heistBlueprint = Entity.GetComponent<HeistBlueprint>();
                if (heistContract != null)
                {
                    HeistAreaLevel = Entity.GetComponent<Mods>()?.ItemLevel ?? 0;
                    HeistJob = heistContract.RequiredJob?.Name;
                    HeistLevel = heistContract.RequiredJobLevel;
                }
                else if (heistBlueprint != null)
                {
                    HeistAreaLevel = heistBlueprint.AreaLevel;
                    HeistLevel = -1; // Use -1 to indicate it's a blueprint for the renderer
                    if (heistBlueprint.Wings != null)
                    {
                        HeistJob = string.Join("\n", heistBlueprint.Wings
                            .Select((w, i) => $"Wing {i + 1}: {string.Join(", ", w.Jobs.Where(j => j.Item1 != null).Select(j => $"{j.Item1.Name} {j.Item2}"))}"));
                    }
                }
            }
        }

    }
}
