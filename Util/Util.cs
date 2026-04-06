using System;
using nuVector4 = System.Numerics.Vector4;
using sdxVector4 = SharpDX.Vector4;

namespace MapNotify_3_28
{
    public partial class MapNotify_3_28
    {
        // Converts SharpDX Vector4 (used by ExileCore/Game) to System.Numerics Vector4 (used by ImGui)
        public static nuVector4 SharpToNu(sdxVector4 v) => new nuVector4(v.X, v.Y, v.Z, v.W);

        // Converts Vector4 to a packed uint for ImGui/Graphics draw lists
        public static uint ColorToUint(nuVector4 color)
        {
            uint r = (uint)(color.X * 255);
            uint g = (uint)(color.Y * 255);
            uint b = (uint)(color.Z * 255);
            uint a = (uint)(color.W * 255);
            return (a << 24) | (b << 16) | (g << 8) | r;
        }

        // Parses RGBA Hex strings from config files into SharpDX Vector4
        public static sdxVector4 HexToSDXVector4(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return sdxVector4.One;
            try
            {
                uint rgba = uint.Parse(hex, System.Globalization.NumberStyles.HexNumber);
                return new sdxVector4(
                    ((rgba >> 24) & 255) / 255f,
                    ((rgba >> 16) & 255) / 255f,
                    ((rgba >> 8) & 255) / 255f,
                    (rgba & 255) / 255f
                );
            }
            catch { return sdxVector4.One; }
        }

        // Helper to convert Vector4 back to Hex for saving to config files
        public static string ToHex(nuVector4 color) => $"{(byte)(color.X * 255):X2}{(byte)(color.Y * 255):X2}{(byte)(color.Z * 255):X2}{(byte)(color.W * 255):X2}";
    }
}