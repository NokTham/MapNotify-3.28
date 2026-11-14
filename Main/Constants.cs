using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MapNotify_3_28
{
    public static class Constants
    {
        public static readonly Regex MapTierRegex = new Regex(
            @"\s*\(Tier\s+\d+\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        public static readonly Regex MapNameRegex = new Regex(
            @"<[^>]*>{([^}]*)}",
            RegexOptions.Compiled
        );
        public static readonly int[] AtlasNodeOffsets = { 0x120, 0x110, 0x118, 0x128 };

        public static class UIIndices
        {
            public const int MapDeviceRoot = 76; // Map Device / Invitations window (fallback; prefer IngameUi.MapDeviceWindow)
            public const int HeistLockerDefault = 108;
            public const int ExpeditionLockerDefault = 111;
            public const int PurchaseWindowTabDetails = 8;
            public const int StashMapTabItems = 3;
            public const int AtlasPanel = 30;
        }

        public static readonly string[] ModNameBlacklist =
        {
            "AfflictionMapDeliriumStacks",
            "AfflictionMapReward",
            "InfectedMap",
            "MapForceCorruptedSideArea",
            "MapGainsRandomZanaMod",
            "MapDoesntConsumeSextantCharge",
            "MapBossSurroundedByTormentedSpirits",
            "MapZanaSubAreaMissionDetails",
            "MapZanaInfluence",
            "IsUberMap",
            "MapConqueror",
            "MapElder",
            "MapVaalTempleContainsVaalVessels",
            "MavenInvitation",
            "MapCorruptionRandomAtlasNotables",
            "MapCorruptionAtlasEffect",
            "MapCorruptionBossCorruption",
            "MapCorruptionSoulGainPrevention",
            "MapCorruptionItemQuantity",
            "ChampionsFragment",
            "MapShaperInfluence",
            "BlightFragmentMushrune",
            "MapCorruptionPackSize",
            "MapCorruptionModEffect",
            "MapCorruptionItemRarity",
            "MapCorruptionRandomScarab", // 3.30: Scarabs → Threads, verify renamed id
            "MapCorruptionVaalVessel",
        };

        private static readonly ConcurrentDictionary<string, bool> ModBlacklistCache =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>
        /// Returns true when the raw mod name contains any blacklisted fragment.
        /// Results are memoized because mod names repeat across every item update.
        /// </summary>
        public static bool IsModBlacklisted(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
                return false;

            return ModBlacklistCache.GetOrAdd(
                rawName,
                static name =>
                {
                    foreach (var black in ModNameBlacklist)
                    {
                        if (name.Contains(black, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }

                    return false;
                }
            );
        }
    }

    /// <summary>
    /// Stat-key, mod-name and description data for map yield stats.
    /// Keep the game-data details here so a league change only requires updating
    /// these lists rather than the matching code in several files.
    /// Known 3.30 changes: Item Quantity becomes Atlas Drops, Scarabs are replaced
    /// by Threads, and Maven's Chisel of Scarabs becomes Maven's Chisel of Threads.
    /// Legacy keys are kept alongside the new ones until the league transition is
    /// complete.
    /// </summary>
    public static class MapYield
    {
        public enum Stat
        {
            None,
            AtlasDrops,
            ItemRarity,
            PackSize,
            MoreMaps,
            MoreThreads, // 3.30 name; Scarabs were replaced by Threads
            MoreCurrency,
        }

        /// <summary>
        /// Fragments of ModRecord.StatNames keys (snake_case).
        /// Atlas Drops uses "map_drop_scaling_+%" in 3.30 and is not the same
        /// mechanic as the legacy Item Quantity stat; the legacy key is kept here
        /// only while both data sets need to be supported.
        /// </summary>
        public static readonly string[] AtlasDropsStatKeys =
        {
            "map_drop_scaling", // 3.30 Atlas Drops key (map_drop_scaling_+%)
            "map_item_drop_quantity", // legacy pre-3.30 Item Quantity key
            "map_total_item_quantity",
        };

        public static readonly string[] ItemRarityStatKeys =
        {
            "map_item_drop_rarity",
            "map_total_item_rarity",
        };

        public static readonly string[] PackSizeStatKeys =
        {
            "map_pack_size",
            "map_monster_pack_size",
            "map_total_pack_size",
        };

        public static readonly string[] MoreMapsStatKeys =
        {
            "map_item_drop_chance",
            "map_map_item_drop_chance",
        };

        /// <summary>
        /// "More Threads" is the 3.30 name for the special-drop slot (Scarabs were
        /// removed from the game). The underlying stat key was NOT renamed, so it
        /// still reads "map_scarab_drop_chance_+%".
        /// </summary>
        public static readonly string[] MoreThreadsStatKeys =
        {
            "map_scarab_drop_chance", // 3.30 Threads key (unchanged name; Scarabs are gone)
        };

        public static readonly string[] MoreCurrencyStatKeys = { "map_currency_drop_chance" };

        /// <summary>
        /// Mod RawName prefixes (CamelCase) used to keep yield mods out of the
        /// Mod Browser. Add future 3.30 raw-name prefixes here.
        /// </summary>
        public static readonly string[] ModNamePrefixes =
        {
            "MapItemDropChance",
            "MapItemQuantity",
            "MapItemRarity",
            "MapPackSize",
            "MapMonsterPackSize",
            "MapScarabDropChance", // 3.30 Threads raw name is expected to stay unchanged too
            "MapCurrencyDropChance",
            "MapMapDropChance",
            "MapTotalItemQuantity",
            "MapTotalItemRarity",
            "MapTotalPackSize",
            "MapTierDropChance",
            "MapOriginator",
            "MapDropScaling", // likely 3.30 Atlas Drops raw-name prefix, verify
            // "MapAtlasDrops", // possible alternative 3.30 raw-name prefix
        };

        /// <summary>
        /// Human-readable fragments used when filtering yield stat lines out of
        /// mods.json and the Mod Browser.
        /// </summary>
        public static readonly string[] DescriptionPhrases =
        {
            "quantity of items",
            "atlas drops",
            "rarity of items",
            "pack size",
            "thread drop", // 3.30 Threads wording
            "additional thread", // 3.30 Threads wording
            "quantity of threads", // 3.30 patch notes: "quantity of Threads found"
        };

        private static readonly Regex MoreDropsRegex = new Regex(
            @"\bmore\s+(scarabs?|threads?|currency(\s+items?)?|maps?)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Regex IncreasedDropsRegex = new Regex(
            @"\b(increased|more)\s+(map|scarab|thread|currency)\s+drop",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        /// <summary>
        /// Classifies a ModRecord.StatNames key into a yield category.
        /// </summary>
        public static Stat ClassifyStatKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return Stat.None;

            // More-specific keys first: "map_item_drop_chance" (More Maps) and
            // "map_item_drop_quantity" (Atlas Drops / legacy quantity) share a prefix.
            if (ContainsAny(key, MoreThreadsStatKeys))
                return Stat.MoreThreads;
            if (ContainsAny(key, MoreCurrencyStatKeys))
                return Stat.MoreCurrency;
            if (ContainsAny(key, MoreMapsStatKeys))
                return Stat.MoreMaps;
            if (ContainsAny(key, AtlasDropsStatKeys))
                return Stat.AtlasDrops;
            if (ContainsAny(key, ItemRarityStatKeys))
                return Stat.ItemRarity;
            if (ContainsAny(key, PackSizeStatKeys))
                return Stat.PackSize;

            return Stat.None;
        }

        /// <summary>
        /// True when a mod RawName belongs to a yield stat (used to hide it from the browser).
        /// </summary>
        public static bool IsYieldModName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
                return false;

            foreach (var prefix in ModNamePrefixes)
            {
                if (rawName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True when a mod description looks like a yield stat line.
        /// </summary>
        public static bool IsYieldDescription(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            foreach (var phrase in DescriptionPhrases)
            {
                if (line.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return MoreDropsRegex.IsMatch(line) || IncreasedDropsRegex.IsMatch(line);
        }

        private static bool ContainsAny(string key, string[] fragments)
        {
            foreach (var fragment in fragments)
            {
                if (key.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
