using System.Collections.Generic;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace MapNotify_3_28
{
    public class MapNotifySettings : ISettings
    {
        // Framework and Profile Management
        public ToggleNode Enable { get; set; } = new(true);
        public TextNode SelectedProfile { get; set; } = new TextNode("Default");
        public ToggleNode AutoSwitchProfile { get; set; } = new(false);

        // Core Settings
        public HotkeyNode CaptureHotkey { get; set; } = new(System.Windows.Forms.Keys.F1);
        public ToggleNode UseControl { get; set; } = new(false);
        public ToggleNode UseShift { get; set; } = new(false);
        public ToggleNode UseAlt { get; set; } = new(false);
        public RangeNode<int> InventoryCacheInterval { get; set; } = new(200, 1, 2000);
        public RangeNode<int> StashCacheInterval { get; set; } = new(500, 1, 2000);
        public RangeNode<int> MapStashCacheInterval { get; set; } = new(500, 1, 2000);
        public ToggleNode FilterInventory { get; set; } = new(true);
        public ToggleNode FilterStash { get; set; } = new(false);
        public ToggleNode FilterMapStash { get; set; } = new(false);
        public ToggleNode FilterShops { get; set; } = new(false);
        public ToggleNode FilterTrade { get; set; } = new(false);
        public ToggleNode ShowForInvitations { get; set; } = new(false);
        public ToggleNode ShowHeistLockerHighlights { get; set; } = new(false);
        public ToggleNode ShowExpeditionLockerHighlights { get; set; } = new(false);
        public ToggleNode BoxForBricked { get; set; } = new(true);
        public ToggleNode BoxForMapWarnings { get; set; } = new(true);
        public ToggleNode BoxForMapBadWarnings { get; set; } = new(true);

        // Map Tooltip Settings
        public ToggleNode AlwaysShowTooltip { get; set; } = new(false);
        public ToggleNode HorizontalLines { get; set; } = new(true);
        public ToggleNode ShowMapName { get; set; } = new(true);
        public ToggleNode ShowModWarnings { get; set; } = new(true);
        public ToggleNode ShowModCount { get; set; } = new(true);
        public ToggleNode ShowQuantityPercent { get; set; } = new(true);
        public ToggleNode ShowPackSizePercent { get; set; } = new(true);
        public ToggleNode ShowRarityPercent { get; set; } = new(true);
        public ToggleNode ColorQuantityPercent { get; set; } = new(true);
        public RangeNode<int> ColorQuantity { get; set; } = new(60, 0, 225);
        public ToggleNode ColorPackSizePercent { get; set; } = new(false);
        public RangeNode<int> ColorPackSize { get; set; } = new(25, 0, 100);
        public ToggleNode ColorRarityPercent { get; set; } = new(false);
        public RangeNode<int> ColorRarity { get; set; } = new(60, 0, 500);
        public ToggleNode ShowChisel { get; set; } = new(false);
        public ColorNode ChiselColor { get; set; } = new ColorNode(new Color(1f, 0.80f, 0.30f, 1f));
        public ToggleNode ShowHeistInfo { get; set; } = new(false);
        public ToggleNode ShowLogbookInfo { get; set; } = new(false);
        public ToggleNode ShowOriginatorMaps { get; set; } = new(false);
        public ToggleNode ShowOriginatorScarabs { get; set; } = new(false);
        public ToggleNode ShowOriginatorCurrency { get; set; } = new(false);
        public ToggleNode ShowPrefixSuffixStats { get; set; } = new(false);

        // Atlas Highlights
        public ToggleNode ShowAtlasHighlight { get; set; } = new(false);
        public ColorNode AtlasNotCompletedColor { get; set; } = new ColorNode(new Color(1f, 0f, 0f, 1.0f));
        public RangeNode<int> AtlasHighlightRadius { get; set; } = new(15, 1, 100);
        public ToggleNode ShowAtlasBonusHighlight { get; set; } = new(false);
        public ColorNode AtlasBonusIncompleteColor { get; set; } = new ColorNode(new Color(1f, 0.5f, 0f, 1.0f));
        public RangeNode<int> AtlasBonusHighlightRadius { get; set; } = new(18, 1, 100);
        public ToggleNode ShowMavenWitnessHighlight { get; set; } = new(false);
        public ColorNode MavenWitnessColor { get; set; } = new ColorNode(new Color(0.7f, 0f, 1f, 1.0f));
        public RangeNode<int> MavenWitnessHighlightRadius { get; set; } = new(21, 1, 100);

        // Borders and Highlight Colours
        public RangeNode<int> BorderDeflation { get; set; } = new(4, 0, 50);
        public RangeNode<int> BorderThickness { get; set; } = new(1, 1, 6);
        public RangeNode<int> SimpleOutlinesThickness { get; set; } = new(2, 1, 6);
        public RangeNode<int> SimpleOutlinesBrickedSize { get; set; } = new(60, 10, 100);
        public ToggleNode UseSimpleOutlines { get; set; } = new(false);
        public ListNode SimpleOutlineOrientation { get; set; } = new ListNode { Value = "Horizontal", Values = new List<string> { "Horizontal", "Vertical" } };
        public ListNode SimpleOutlineGoodPosition { get; set; } = new ListNode { Value = "Top/Left", Values = new List<string> { "Top/Left", "Bottom/Right" } };
        public ColorNode MapBorderGood { get; set; } = new ColorNode(new Color(0f, 1f, 0f, 0.35f));
        public ColorNode MapBorderBad { get; set; } = new ColorNode(new Color(1f, 0f, 0f, 0.35f));
        public ColorNode Bricked { get; set; } = new ColorNode(new Color(1f, 0f, 0f, 1f));
        public RangeNode<int> BorderThicknessMap { get; set; } = new(2, 1, 6);

        // Config Files and Other
        public RangeNode<int> TooltipOffsetX { get; set; } = new(25, -150, 150);
        public RangeNode<int> TooltipOffsetY { get; set; } = new(0, -150, 150);
        public ToggleNode PadForBigCursor { get; set; } = new(false);
        public ToggleNode PadForNinjaPricer { get; set; } = new(false);
        public ToggleNode PadForNinjaPricer2 { get; set; } = new(false);
        public ToggleNode PadForAltPricer { get; set; } = new(false);
    }
}
