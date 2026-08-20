using System.Text.RegularExpressions;

namespace MapNotify_3_28
{
    public static class Constants
    {
        public static readonly Regex MapTierRegex = new Regex(@"\s*\(Tier\s+\d+\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex MapNameRegex = new Regex(@"<[^>]*>{([^}]*)}", RegexOptions.Compiled);
        public static readonly int[] AtlasNodeOffsets = { 0x120, 0x110, 0x118, 0x128 };

        public static class UIIndices
        {
            public const int MapDeviceRoot = 76; /// Invitations Receptacle
            public const int HeistLockerDefault = 107;
            public const int ExpeditionLockerDefault = 110;
            public const int PurchaseWindowTabDetails = 8;
            public const int StashMapTabItems = 3;
            public const int AtlasPanel = 29;
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
            "MapCorruptionRandomAtlasNotables",
            "MapCorruptionAtlasEffect",
            "MapCorruptionBossCorruption",
            "MapCorruptionSoulGainPrevention",
            "MapCorruptionItemQuantity"
            "ChampionsFragment",
            "MapShaperInfluence"
        };
    }
}
