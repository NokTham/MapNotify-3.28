using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using Vector4 = System.Numerics.Vector4;

namespace MapNotify_3_28
{
    public class MapNotifySettings : ISettings
    {
        public ColorNode WarningColor { get; set; } = new SharpDX.Color(255, 0, 0, 255);
        public RangeNode<int> WarningTokenWidth { get; set; } = new RangeNode<int>(2, 1, 10);

        public ToggleNode UseControl { get; set; } = new(false);
        public ToggleNode UseShift { get; set; } = new(false);
        public ToggleNode UseAlt { get; set; } = new(false);
        public HotkeyNode CaptureHotkey { get; set; } = new(System.Windows.Forms.Keys.F1);
        public ToggleNode Enable { get; set; } = new(true);
        public RangeNode<int> InventoryCacheInterval { get; set; } = new(200, 1, 2000);
        public RangeNode<int> StashCacheInterval { get; set; } = new(500, 1, 2000);
        public RangeNode<int> MapStashCacheInterval { get; set; } = new(500, 1, 2000);
        public ToggleNode ShowMapName { get; set; } = new(true);
        public ToggleNode ShowModCount { get; set; } = new(true);
        public ToggleNode ShowQuantityPercent { get; set; } = new(true);
        public ToggleNode ShowRarityPercent { get; set; } = new(true);
        public ToggleNode ColorQuantityPercent { get; set; } = new(true);
          public RangeNode<int> ColorQuantity { get; set; } = new(100, 0, 220);
        public RangeNode<int> BorderDeflation { get; set; } = new(4, 0, 50);
        public RangeNode<int> BorderThickness { get; set; } = new(1, 1, 6);
        public RangeNode<int> MapQuantSetting { get; set; } = new(100, 0, 220);
        public RangeNode<int> MapPackSetting { get; set; } = new(100, 0, 220);
        public ToggleNode ShowPackSizePercent { get; set; } = new(true);
        public ToggleNode ShowModWarnings { get; set; } = new(true);
        public ToggleNode HorizontalLines { get; set; } = new(true);
        public ToggleNode PadForBigCursor { get; set; } = new(false);
        public ToggleNode PadForNinjaPricer { get; set; } = new(false);
        public ToggleNode PadForNinjaPricer2 { get; set; } = new(false);
        public ToggleNode PadForAltPricer { get; set; } = new(false);
        public ToggleNode AlwaysShowTooltip { get; set; } = new(true);
        public ToggleNode StyleTextForBorder { get; set; } = new(false);
        public ToggleNode ShowForInvitations { get; set; } = new(true);
        public ToggleNode FilterInventory { get; set; } = new(true);
        public ToggleNode FilterStash { get; set; } = new(true);
        public ToggleNode FilterMapStash { get; set; } = new(true);
        public ToggleNode FilterShops { get; set; } = new(true);
        public ToggleNode FilterTrade { get; set; } = new(true);
        public ToggleNode BoxForBricked { get; set; } = new(true);
        public ToggleNode BoxForMapWarnings { get; set; } = new(true);
        public ToggleNode BoxForMapBadWarnings { get; set; } = new(true);

        // Originator & Nightmare map bonus stats
        public ToggleNode ShowOriginatorScarabs { get; set; } = new(true);
        public ToggleNode ShowOriginatorCurrency { get; set; } = new(true);
        public ToggleNode ShowOriginatorMaps { get; set; } = new(true);
        public ToggleNode ShowChisel { get; set; } = new(true);

        public Vector4 Bricked { get; set; } = new(1f, 0f, 0f, 1f);
        public Vector4 MapBorderGood { get; set; } = new(0f, 1f, 0f, 0.35f);

        public Vector4 MapBorderBad { get; set; } = new(1f, 0f, 0f, 0.35f);
        public RangeNode<int> BorderThicknessMap { get; set; } = new(2, 1, 6);

        public ToggleNode ShowAtlasHighlight { get; set; } = new(false);
        public ToggleNode ShowAtlasBonusHighlight { get; set; } = new(false);
        public ToggleNode ShowMavenWitnessHighlight { get; set; } = new(false);
        public Vector4 AtlasNotCompletedColor { get; set; } = new(1f, 0f, 0f, 1.0f);
        public Vector4 AtlasBonusIncompleteColor { get; set; } = new(1f, 0.5f, 0f, 1.0f);
        public Vector4 MavenWitnessColor { get; set; } = new(0.7f, 0f, 1f, 1.0f);
        public RangeNode<int> AtlasHighlightRadius { get; set; } = new(15, 1, 100);
        public RangeNode<int> AtlasBonusHighlightRadius { get; set; } = new(18, 1, 100);
        public RangeNode<int> MavenWitnessHighlightRadius { get; set; } = new(21, 1, 100);
    }
}
