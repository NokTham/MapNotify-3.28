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
    }
}