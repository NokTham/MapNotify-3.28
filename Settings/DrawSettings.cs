using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nuVector4 = System.Numerics.Vector4;
using nuVector2 = System.Numerics.Vector2;

namespace MapNotify_3_28
{
    partial class MapNotify_3_28 : BaseSettingsPlugin<MapNotifySettings>
    {
        private string _rebindingNodeName = null;
        
        public void DrawHotkeySelector(string label, HotkeyNode node, ToggleNode ctrl, ToggleNode shift, ToggleNode alt)
        {
            ImGui.Text(label);

            var c = ctrl.Value;
            if (ImGui.Checkbox("Ctrl##" + label, ref c)) ctrl.Value = c;
            ImGui.SameLine();

            var s = shift.Value;
            if (ImGui.Checkbox("Shift##" + label, ref s)) shift.Value = s;
            ImGui.SameLine();

            var a = alt.Value;
            if (ImGui.Checkbox("Alt##" + label, ref a)) alt.Value = a;
            ImGui.SameLine();

            bool isRebinding = _rebindingNodeName == label;
            string prefix = "";
            if (ctrl.Value) prefix += "Ctrl + ";
            if (shift.Value) prefix += "Shift + ";
            if (alt.Value) prefix += "Alt + ";

            string buttonText = isRebinding ? "PRESS KEY...###" + label : $"{prefix}{node.Value}###{label}";

            if (ImGui.Button(buttonText, new nuVector2(150, 0)))
                _rebindingNodeName = label;

            if (isRebinding)
            {
                foreach (var key in System.Enum.GetValues<System.Windows.Forms.Keys>())
                {
                    if (IsModifierKey(key) || key == System.Windows.Forms.Keys.None) continue;
                    if (Input.GetKeyState(key))
                    {
                        node.Value = key;
                        _rebindingNodeName = null;
                        break;
                    }
                }
                if (Input.GetKeyState(System.Windows.Forms.Keys.Escape)) _rebindingNodeName = null;
            }
        }

        // The helper methods Checkbox, IntSlider, etc., are now in UiHelpers.cs

        public override void DrawSettings()
        {
            ImGui.Text(
                "Plugin by Lachrymatory. -- Edited by Xcesius -- (vibecoded) Updated by NokTham"
            );

            if (ImGui.Button("NokTham's GitHub"))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/NokTham/MapNotify-3.28",
                    UseShellExecute = true
                });
            }
            ImGui.SameLine();
            if (ImGui.Button("Xcesius' Github"))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/Xcesius/MapNotify/",
                    UseShellExecute = true
                });
            }


            ImGui.Separator();

            if (ImGui.TreeNodeEx("Core Settings", ImGuiTreeNodeFlags.CollapsingHeader))
            {
                DrawHotkeySelector("Capture Hotkey", Settings.CaptureHotkey, Settings.UseControl, Settings.UseShift, Settings.UseAlt);
                ImGui.SameLine();
                HelpMarker("The key used to open the Map Mod Preview window while hovering over a map.");
                Settings.InventoryCacheInterval.Value = IntSlider(
                    "Inventory Item Caching Interval in ms",
                    Settings.InventoryCacheInterval,
                    400f
                );
                ImGui.SameLine();
                HelpMarker(
                    "This setting is only applied once upon Initialization\nReload the plugin to use the updated setting"
                );
                Settings.StashCacheInterval.Value = IntSlider(
                    "Stash Item Caching Interval in ms",
                    Settings.StashCacheInterval,
                    400f
                );
                ImGui.SameLine();
                HelpMarker(
                    "This setting is only applied once upon Initialization\nReload the plugin to use the updated setting"
                );
                Settings.MapStashCacheInterval.Value = IntSlider(
                    "Map Stash Caching Interval in ms",
                    Settings.MapStashCacheInterval,
                    400f
                );
                ImGui.SameLine();
                HelpMarker(
                    "Specific interval for Map Stash tabs due to high performance cost.\nThis setting is only applied once upon Initialization\nReload the plugin to use the updated setting"
                );

                ImGui.Dummy(new nuVector2(0, 5));

                ImGui.Text("Show Highlights:");
                Settings.ShowForInvitations.Value = Checkbox(
                    "Maven Invitations",
                    Settings.ShowForInvitations.Value
                );
                Settings.FilterInventory.Value = Checkbox("Inventory", Settings.FilterInventory.Value); ImGui.SameLine(0f, 25f);
                Settings.FilterStash.Value = Checkbox("Stash", Settings.FilterStash.Value); ImGui.SameLine(0f, 25f);
                Settings.FilterMapStash.Value = Checkbox("Map Stash", Settings.FilterMapStash.Value); ImGui.SameLine(0f, 0f);
                WarningMarker(
      "Filtering Map Stash Tab is very resource intensive, use with caution."
  ); ImGui.SameLine(0f, 25f);
                Settings.FilterShops.Value = Checkbox("Shops", Settings.FilterShops.Value); ImGui.SameLine(0f, 25f);
                Settings.FilterTrade.Value = Checkbox("Trade", Settings.FilterTrade.Value);

                ImGui.Dummy(new nuVector2(0, 5));

                Settings.BoxForBricked.Value = Checkbox("Mark Bricked", Settings.BoxForBricked.Value);
                ImGui.SameLine();
                HelpMarker("Marks bricked maps with a border. Configure color in the 'Borders and Colours' section.");
                Settings.BoxForMapWarnings.Value = Checkbox("Mark Bad Mods", Settings.BoxForMapWarnings.Value);
                ImGui.SameLine();
                HelpMarker("Highlights maps if there are bad mods. Configure color in the 'Borders and Colours' section.");
                Settings.BoxForMapBadWarnings.Value = Checkbox("Mark Good Mods", Settings.BoxForMapBadWarnings.Value);
                ImGui.SameLine();
                HelpMarker("Highlights maps if there are good mods. Configure color in the 'Borders and Colours' section.");
                // ImGui.SameLine(); HelpMarker("Add ';true' after a line in the config files to mark it as a bricked mod.");

            }

            if (ImGui.TreeNodeEx("Map Tooltip Settings", ImGuiTreeNodeFlags.CollapsingHeader))
            {
                Settings.AlwaysShowTooltip.Value = Checkbox(
                    "Show Tooltip Even Without Warnings",
                    Settings.AlwaysShowTooltip.Value
                );
                ImGui.SameLine();
                HelpMarker(
                    "Show tooltip even if there are no mods to warn you about on the map.\nThis means you will always be able to see tier, quantity, mod count, etc."
                );
                Settings.HorizontalLines.Value = Checkbox(
                    "Show Horizontal Lines",
                    Settings.HorizontalLines.Value
                );
                ImGui.SameLine();
                HelpMarker("Add Horizontal Lines separating Map name, Originator Stats and Mods.");
                Settings.ShowMapName.Value = Checkbox("Show Map Name", Settings.ShowMapName.Value);
                Settings.ShowModWarnings.Value = Checkbox(
                    "Show Good/Bad mods",
                    Settings.ShowModWarnings.Value
                );
                Settings.ShowModCount.Value = Checkbox(
                    "Show Number of Mods",
                    Settings.ShowModCount.Value
                );
                Settings.ShowPackSizePercent.Value = Checkbox(
                    "Show Pack Size %",
                    Settings.ShowPackSizePercent.Value
                );
                Settings.ShowRarityPercent.Value = Checkbox(
                    "Show Item Rarity %",
                    Settings.ShowRarityPercent.Value
                );
                Settings.ShowQuantityPercent.Value = Checkbox(
                    "Show Item Quantity %",
                    Settings.ShowQuantityPercent.Value
                );
                Settings.ColorQuantityPercent.Value = Checkbox(
                    "Warn Below Quantity Percentage",
                    Settings.ColorQuantityPercent.Value
                );
                Settings.ColorQuantity.Value = IntSlider("##ColorQuantity", Settings.ColorQuantity, 400f);
                ImGui.SameLine(0f, 10f);
                HelpMarker(
                    "The colour of the quantity text will be red below this amount and green above it."
                );
                Settings.ShowChisel.Value = Checkbox("Show Chisel %", Settings.ShowChisel.Value);
                ImGui.SameLine();
                HelpMarker(
                    "Show chisel applied to map, Maven or Cartographer's (legacy) ."
                );
                Settings.ShowHeistInfo.Value = Checkbox("Show Heist Job Info", Settings.ShowHeistInfo.Value);
                ImGui.SameLine();
                HelpMarker("Show Job type and Level for Contracts and Blueprints.");

                Settings.ShowOriginatorMaps.Value = Checkbox(
                    "Show More Maps %",
                    Settings.ShowOriginatorMaps.Value
                ); ImGui.SameLine(0f, 25f);
                Settings.ShowOriginatorScarabs.Value = Checkbox(
                    "Show More Scarabs %",
                    Settings.ShowOriginatorScarabs.Value
                ); ImGui.SameLine(0f, 25f);
                Settings.ShowOriginatorCurrency.Value = Checkbox(
                    "Show More Currency %",
                    Settings.ShowOriginatorCurrency.Value
                ); ImGui.SameLine(); HelpMarker(
                    "Originator/Nightmare Map Bonus Stats"
                );
            }

            if (ImGui.TreeNodeEx("Atlas Highlights", ImGuiTreeNodeFlags.CollapsingHeader))
            {
                Settings.ShowAtlasHighlight.Value = Checkbox("Incomplete Maps", Settings.ShowAtlasHighlight.Value);
                ImGui.SameLine();
                Settings.AtlasNotCompletedColor = ColorButton("Color##AtlasIncomplete", Settings.AtlasNotCompletedColor);
                Settings.AtlasHighlightRadius.Value = IntSlider("Radius##AtlasIncomplete", Settings.AtlasHighlightRadius, 400f);

                Settings.ShowAtlasBonusHighlight.Value = Checkbox("Incomplete Bonus", Settings.ShowAtlasBonusHighlight.Value);
                ImGui.SameLine();
                Settings.AtlasBonusIncompleteColor = ColorButton("Color##AtlasBonus", Settings.AtlasBonusIncompleteColor);
                Settings.AtlasBonusHighlightRadius.Value = IntSlider("Radius##AtlasBonus", Settings.AtlasBonusHighlightRadius, 400f);

                Settings.ShowMavenWitnessHighlight.Value = Checkbox("Maven Witness", Settings.ShowMavenWitnessHighlight.Value);
                ImGui.SameLine();
                Settings.MavenWitnessColor = ColorButton("Color##MavenWitness", Settings.MavenWitnessColor);
                Settings.MavenWitnessHighlightRadius.Value = IntSlider("Radius##MavenWitness", Settings.MavenWitnessHighlightRadius, 400f);

                HelpMarker(@"Highlights nodes on the Atlas that:
- Haven't been completed yet
- Have missing bonus objectives
- Are currently witnessed by the Maven
* Hover a node or type 'a|e' in the Atlas Search Box to force the client to load all nodes data, this is only needed upon logging in, this will also highlight nodes that are not visible");
            }

            if (ImGui.TreeNodeEx("Borders and Colours", ImGuiTreeNodeFlags.CollapsingHeader))
            {

                Settings.BorderDeflation.Value = IntSlider(
                    "Map Border Deflation##MapBorderDeflation",
                    Settings.BorderDeflation, 400f
                );
                Settings.BorderThickness.Value = IntSlider(
                    "Tooltip Border Thickness##BorderThickness",
                    Settings.BorderThickness, 400f
                );

                Settings.Bricked = ColorButton("Bricked Map", Settings.Bricked);
                Settings.MapBorderBad = ColorButton("Bad Mods Color", Settings.MapBorderBad);
                ImGui.SameLine();
                HelpMarker("Set transparency (Alpha) to see the map icon through the highlight.");

                Settings.MapBorderGood = ColorButton("Good Mods Color", Settings.MapBorderGood);
                ImGui.SameLine();
                HelpMarker("Set transparency (Alpha) to see the map icon through the highlight.");

                Settings.BorderThicknessMap.Value = IntSlider(
                    "Border Thickness for Bricked Maps##BorderThickness Maps",
                    Settings.BorderThicknessMap, 400f
                );
            }

            if (ImGui.TreeNodeEx("Config Files and Other", ImGuiTreeNodeFlags.CollapsingHeader))
            {
                if (ImGui.Button("Reload Warnings Text Files"))
                {
                    GoodModsDictionary = LoadConfigGoodMod();
                    BadModsDictionary = LoadConfigBadMod();
                    ExpectedNodes = LoadExpectedNodes();
                    ExpectedBonusNodes = LoadExpectedBonusNodes();
                }
                if (ImGui.Button("Recreate Default Warnings Text Files"))
                    ResetConfigs();
                ImGui.SameLine();
                HelpMarker(
                    "This will irreversibly delete all your existing warnings config files!"
                );
                Settings.PadForBigCursor.Value = Checkbox(
                    "Pad for Big Cursor",
                    Settings.PadForBigCursor.Value
                );
                ImGui.SameLine();
                HelpMarker(
                    "This will move the tooltip to the right to allow room for the large mouse cursor to be rendered without overlapping the tooltip. Only needed if you use a large cursor and find it overlaps with the tooltip."
                );
                Settings.PadForNinjaPricer.Value = Checkbox(
                    "Pad for Ninja Pricer",
                    Settings.PadForNinjaPricer.Value
                );
                ImGui.SameLine();
                HelpMarker(
                    "This will move the tooltip down vertically to allow room for the Ninja Pricer tooltip to be rendered. Only needed with that plugin active."
                );
                Settings.PadForNinjaPricer2.Value = Checkbox(
                    "More Pad for Ninja Pricer",
                    Settings.PadForNinjaPricer2.Value
                );
                Settings.PadForAltPricer.Value = Checkbox(
                    "Pad for Personal Pricer",
                    Settings.PadForAltPricer.Value
                );
                ImGui.SameLine();
                HelpMarker("It's unlikely you'll need this.");
            }
        }
    }
}
