using System.Collections.Generic;
using nuVector4 = System.Numerics.Vector4;

namespace MapNotify_3_28
{
    public partial class MapNotify_3_28
    {
    // JSON Database helper classes
    public class ModsJson { public Dictionary<string, ModGroup> groups { get; set; } }
    public class ModGroup { public List<string> BaseMods { get; set; } public List<string> Descriptions { get; set; } public string Type { get; set; } }
    public class ModEntry { public string GroupKey { get; set; } public List<string> Descriptions { get; set; } public List<string> BaseMods { get; set; } public string Type { get; set; } }
    public class ModData { public string BaseMod { get; set; } public string Description { get; set; } }

    /// <summary>
    /// Represents a text line in the tooltip with specific styling.
    /// </summary>
    public class StyledText
    {
        public string Text { get; set; }
        public string EscapedText { get; set; }
        public nuVector4 Color { get; set; }
        public bool Bricking { get; set; }
    }

    /// <summary>
    /// Data structure for a mod captured via the hotkey, used in the Preview Window.
    /// </summary>
    public class CapturedMod
    {
        public string RawName { get; set; }
        public string AffixType { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public nuVector4 Color { get; set; }
        public bool IsBricking { get; set; }
    }

    /// <summary>
    /// Simple model for Heist-specific information.
    /// </summary>
    public class HeistJobLine
    {
        public string Text { get; set; }
        public bool IsRevealed { get; set; }
    }

    public class TooltipData
    {
        public int Quality { get; set; }
        public int Wings { get; set; } = 1;
        public System.Collections.Generic.Dictionary<string, int> Requirements { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
    }
    }
}