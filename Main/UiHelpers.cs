using ExileCore;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using System.Collections.Generic;
using nuVector4 = System.Numerics.Vector4;
using nuVector2 = System.Numerics.Vector2;

namespace MapNotify_3_28
{
    partial class MapNotify_3_28
    {
        public static void HelpMarker(string desc)
        {
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted(desc);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }

        public static void WarningMarker(string desc)
        {
            ImGui.TextColored(new nuVector4(1.0f, 0.0f, 0.0f, 1.0f), "(!)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted(desc);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }

        public static int IntSlider(string labelString, RangeNode<int> setting, float width = 0)
        {
            var refValue = setting.Value;
            if (width > 0)
                ImGui.SetNextItemWidth(width);
            ImGui.SliderInt(labelString, ref refValue, setting.Min, setting.Max);
            return refValue;
        }

        public static nuVector4 ColorButton(string labelString, nuVector4 setting)
        {
            var refValue = setting;
            ImGui.ColorEdit4(
                labelString,
                ref refValue,
                ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar
            );
            return refValue;
        }

        public static bool Checkbox(string labelString, bool boolValue)
        {
            ImGui.Checkbox(labelString, ref boolValue);
            return boolValue;
        }

        public static void FixedWidthSeparator(float width, float thickness = 1.0f)
        {
            var pos = ImGui.GetCursorScreenPos();
            // Draw the line manually using the window's draw list
            ImGui.GetWindowDrawList().AddLine(
                pos,
                new nuVector2(pos.X + width, pos.Y),
                ImGui.GetColorU32(ImGuiCol.Separator),
                thickness
            );
            // Advance the cursor vertically to account for the line and standard spacing
            ImGui.Dummy(new nuVector2(0, thickness + ImGui.GetStyle().ItemSpacing.Y));
        }

        private bool IsModifierKey(System.Windows.Forms.Keys key)
        {
            return key == System.Windows.Forms.Keys.ControlKey ||
                   key == System.Windows.Forms.Keys.LControlKey ||
                   key == System.Windows.Forms.Keys.RControlKey ||
                   key == System.Windows.Forms.Keys.ShiftKey ||
                   key == System.Windows.Forms.Keys.LShiftKey ||
                   key == System.Windows.Forms.Keys.RShiftKey ||
                   key == System.Windows.Forms.Keys.Menu ||
                   key == System.Windows.Forms.Keys.LMenu ||
                   key == System.Windows.Forms.Keys.RMenu;
        }
    }
}