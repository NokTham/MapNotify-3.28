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
                int GetStatValue(ItemMod m, string key)
                {
                var stats = m?.ModRecord?.StatNames;
                var values = m?.Values;
                if (stats == null || values == null)
                        return 0;

                for (int i = 0; i < stats.Length; i++)
                    {
                    if (stats[i].Key == key && i < values.Count)
                        return values[i];
                    }
                    return 0;
                }
                WindowID = $"##{Entity.Address}";
                var BaseItem = gameController?.Files?.BaseItemTypes?.Translate(Entity?.Path);
                var ItemName = BaseItem?.BaseName ?? "Unknown";
                ClassID = BaseItem?.ClassName ?? string.Empty;
                ChiselName = string.Empty;
                ChiselValue = 0;
                var packSize = 0;
                var rarity = 0;
                var modsComponent = Entity.GetComponent<Mods>();
                var qualityComponent = Entity.GetComponent<Quality>();
                var originatorScarabs = 0;
                var originatorCurrency = 0;
                var originatorMaps = 0;
                var settings = pluginSettings;
                // get and evaluate mods
                var mapComponent = Entity.GetComponent<ExileCore.PoEMemory.Components.MapKey>() ?? null;
                Tier = mapComponent?.Tier ?? -1;
                var path = Entity.Path ?? string.Empty;
                IsFragment = path.Contains("Fragments/") && !path.Contains("Maven"); // Fragments/Maven is not a fragment for this purpose
                IsMavenMap = path.Contains("MavenMap") || path.Contains("Invitations/Maven");
                NeedsPadding = Tier != -1 || IsMavenMap || IsFragment;
                Bricked = false;
                Corrupted = Entity.GetComponent<Base>()?.isCorrupted ?? false;

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

                int ParseTooltipQuality()
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

                var itemMods = modsComponent?.ItemMods;
                ModCount = itemMods?.Count ?? 0;

                if (modsComponent != null && itemMods != null && ModCount > 0)
                {
                    // Only evaluate mods for non-unique maps, but ALWAYS evaluate for Maven items
                    if (modsComponent.ItemRarity != ItemRarity.Unique || path.Contains("Maven"))
                    {
                        foreach (var mod in itemMods)
                        {
                            if (string.IsNullOrEmpty(mod.RawName) || ModNameBlacklist.Any(m => mod.RawName.Contains(m)))
                            {
                                ModCount--;
                                continue;
                            }

                            packSize += GetStatValue(mod, "map_pack_size_+%");
                            quantity += GetStatValue(mod, "map_item_drop_quantity_+%");
                            rarity += GetStatValue(mod, "map_item_drop_rarity_+%");

                            // Originator map bonus stats
                            originatorScarabs += GetStatValue(mod, "map_scarab_drop_chance_+%_final_from_uber_mod");
                            originatorCurrency += GetStatValue(mod, "map_currency_drop_chance_+%_final_from_uber_mod");
                            originatorMaps += GetStatValue(mod, "map_map_item_drop_chance_+%_final_from_uber_mod");

                            // Optimize Dictionary Search
                            foreach (var entry in GoodModsDictionary)
                            {
                                if (mod.RawName.Contains(entry.Key))
                                {
                                    var warning = entry.Value;
                                    if (warning.Bricking)
                                    {
                                        Bricked = true;
                                    }
                                    ActiveGoodMods.Add(warning);
                                    break;
                                }
                            }

                            foreach (var entry in BadModsDictionary)
                            {
                                if (mod.RawName.Contains(entry.Key))
                                {
                                    var bad = entry.Value;
                                    if (bad.Bricking)
                                    {
                                        Bricked = true;
                                    }
                                    ActiveBadMods.Add(bad);
                                    break;
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
                IsOriginatorMap =
                    modsComponent?.ItemMods.Any(
                        x => x.RawName == "IsUberMap" || x.RawName.Contains("MapZanaInfluence")
                    ) == true;
                {
                    var baseComp = Entity.GetComponent<Base>();
                    var mapTrim = baseComp != null ? baseComp.Name.Replace(" Map", "") : "Unknown";

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
        }

    }
}
