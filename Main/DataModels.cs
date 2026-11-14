using System.Numerics;

namespace MapNotify_3_28
{
    public class StyledText
    {
        public string Text { get; set; }
        public string EscapedText { get; set; }
        public System.Numerics.Vector4 Color { get; set; }
        public bool Bricking { get; set; }
    }

    public class HeistJobLine
    {
        public string Text { get; set; }
        public bool IsRevealed { get; set; }
    }

    public class CapturedMod
    {
        public string RawName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string AffixType { get; set; }
        public System.Numerics.Vector4 Color { get; set; } = new System.Numerics.Vector4(1, 1, 1, 1);
        public bool IsBricking { get; set; }
        public bool IsOfInterest { get; set; }
        public System.Numerics.Vector4 InterestColor { get; set; }

        // Per-mod stat contributions
        public int Quantity { get; set; }
        public int Rarity { get; set; }
        public int PackSize { get; set; }
        public int MoreMaps { get; set; }
        public int MoreThreads { get; set; }
        public int MoreCurrency { get; set; }

        public bool HasAnyStats => Quantity != 0 || Rarity != 0 || PackSize != 0 || MoreMaps != 0 || MoreThreads != 0 || MoreCurrency != 0;
    }
}