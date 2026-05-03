using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExileCore.Shared.Helpers;
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
                if (Input.GetKeyState(System.Windows.Forms.Keys.LButton) || Input.GetKeyState(System.Windows.Forms.Keys.RButton))
                {
                    _rebindingNodeName = null;
                }
                else if (Input.GetKeyState(System.Windows.Forms.Keys.Escape))
                {
                    node.Value = System.Windows.Forms.Keys.None;
                    _rebindingNodeName = null;
                }
                else
                {
                    foreach (var key in System.Enum.GetValues<System.Windows.Forms.Keys>())
                    {
                        if (IsModifierKey(key) || key == System.Windows.Forms.Keys.None ||
                            key == System.Windows.Forms.Keys.LButton || key == System.Windows.Forms.Keys.RButton)
                        {
                            continue;
                        }

                        if (Input.GetKeyState(key))
                        {
                            node.Value = key;
                            _rebindingNodeName = null;
                            break;
                        }
                    }
                }
            }
        }

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
                HelpMarker("Key used to open the Map Mod Preview window.\nESC to clear.");
                Settings.InventoryCacheInterval.Value = IntSlider(
                    "Inventory Item Caching Interval in ms",
                    Settings.InventoryCacheInterval,
                    400f
                );
                ImGui.SameLine();
                HelpMarker(
                    "Affects Inventory, Maven Inv and Shops.\nReload the plugin to use the updated setting."
                );
                Settings.StashCacheInterval.Value = IntSlider(
                    "Stash Item Caching Interval in ms",
                    Settings.StashCacheInterval,
                    400f
                );
                ImGui.SameLine();
                HelpMarker(
                    "Affects Regular/Premium Stahes, Heist and Expedition Lockers.\nReload the plugin to use the updated setting."
                );
                Settings.MapStashCacheInterval.Value = IntSlider(
                    "Map Stash Caching Interval in ms",
                    Settings.MapStashCacheInterval,
                    400f
                );
                ImGui.SameLine();
                HelpMarker(
                    "Specific interval for Map Stash tabs due to high performance cost.\nReload the plugin to use the updated setting"
                );

                ImGui.Dummy(new nuVector2(0, 5));

                ImGui.Text("Show Highlights:");

                Settings.FilterInventory.Value = Checkbox("Inventory", Settings.FilterInventory.Value); ImGui.SameLine(0f, 25f);
                Settings.FilterStash.Value = Checkbox("Stash", Settings.FilterStash.Value); ImGui.SameLine(0f, 25f);
                Settings.FilterMapStash.Value = Checkbox("Map Stash", Settings.FilterMapStash.Value); ImGui.SameLine(0f, 0f);
                WarningMarker("Filtering Map Stash Tab is very resource intensive, use with caution."); ImGui.SameLine(0f, 25f);
                Settings.FilterShops.Value = Checkbox("Shops", Settings.FilterShops.Value); ImGui.SameLine(0f, 25f);
                Settings.FilterTrade.Value = Checkbox("Trade", Settings.FilterTrade.Value);

                Settings.ShowForInvitations.Value = Checkbox("Maven Invitations", Settings.ShowForInvitations.Value); ImGui.SameLine(0f, 25f);
                Settings.ShowHeistLockerHighlights.Value = Checkbox("Heist Locker", Settings.ShowHeistLockerHighlights.Value); ImGui.SameLine(0f, 25f);
                Settings.ShowExpeditionLockerHighlights.Value = Checkbox("Expedition Locker", Settings.ShowExpeditionLockerHighlights.Value);

                ImGui.Dummy(new nuVector2(0, 5));

                Settings.BoxForBricked.Value = Checkbox("Mark Bricked", Settings.BoxForBricked.Value);
                ImGui.SameLine();
                HelpMarker("Marks bricked maps with a border. Configure color in the 'Borders and Highlight Colours' section.");
                Settings.BoxForMapWarnings.Value = Checkbox("Mark Bad Mods", Settings.BoxForMapWarnings.Value);
                ImGui.SameLine();
                HelpMarker("Highlights maps if there are bad mods. Configure color in the 'Borders and Highlight Colours' section.");
                Settings.BoxForMapBadWarnings.Value = Checkbox("Mark Good Mods", Settings.BoxForMapBadWarnings.Value);
                ImGui.SameLine();
                HelpMarker("Highlights maps if there are good mods. Configure color in the 'Borders and Highlight Colours' section.");
            }

            if (ImGui.TreeNodeEx("Map Tooltip Settings", ImGuiTreeNodeFlags.CollapsingHeader))
            {
                Settings.AlwaysShowTooltip.Value = Checkbox(
                    "Show Tooltip Even Without Warnings",
                    Settings.AlwaysShowTooltip.Value
                );
                ImGui.SameLine();
                HelpMarker(
                    "Show tooltip even if there are no mods to warn you about on the map.\nYou'll always be able to see tier, quantity, mod count, etc."
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
                Settings.ShowQuantityPercent.Value = Checkbox(
                    "Show Item Quantity %",
                    Settings.ShowQuantityPercent.Value
                ); ImGui.SameLine(0f, 25f);
                Settings.ShowPackSizePercent.Value = Checkbox(
                    "Show Pack Size %",
                    Settings.ShowPackSizePercent.Value
                ); ImGui.SameLine(0f, 25f);
                Settings.ShowRarityPercent.Value = Checkbox(
                    "Show Item Rarity %",
                    Settings.ShowRarityPercent.Value
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
                Settings.ColorPackSizePercent.Value = Checkbox(
                    "Warn Below Pack Size Percentage",
                    Settings.ColorPackSizePercent.Value
                );
                Settings.ColorPackSize.Value = IntSlider("##ColorPackSize", Settings.ColorPackSize, 400f);
                ImGui.SameLine(0f, 10f);
                HelpMarker(
                    "The colour of the pack size text will be red below this amount and green above it."
                );
                Settings.ColorRarityPercent.Value = Checkbox(
                    "Warn Below Rarity Percentage",
                    Settings.ColorRarityPercent.Value
                );
                Settings.ColorRarity.Value = IntSlider("##ColorRarity", Settings.ColorRarity, 400f);
                ImGui.SameLine(0f, 10f);
                HelpMarker(
                    "The colour of the rarity text will be red below this amount and green above it."
                );
                Settings.ShowChisel.Value = Checkbox("Show Chisel %", Settings.ShowChisel.Value);
                ImGui.SameLine();
                HelpMarker(
                    "Show chisel applied to map, Maven or Cartographer's (legacy)."
                );
                ImGui.SameLine();
                nuVector4 chiselColor = SharpToNu(Settings.ChiselColor.Value.ToVector4());
                if (ImGui.ColorEdit4("##ChiselColor", ref chiselColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
                    Settings.ChiselColor.Value = chiselColor.ToSharpColor();

                Settings.ShowHeistInfo.Value = Checkbox("Show Heist Job Info", Settings.ShowHeistInfo.Value);
                ImGui.SameLine();
                HelpMarker("Show Job type and Level for Contracts and Blueprints.");
                Settings.ShowLogbookInfo.Value = Checkbox("Show Logbook Faction Info", Settings.ShowLogbookInfo.Value);
                ImGui.SameLine();
                HelpMarker("Show Area names, Factions and Implicit mods for Expedition Logbooks.");

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
                Settings.ShowPrefixSuffixStats.Value = Checkbox(
                    "Show Prefix/Suffix Stats",
                    Settings.ShowPrefixSuffixStats.Value
                );
                ImGui.SameLine();
                HelpMarker("Displays detailed breakdown of stats contributed by Prefixes and Suffixes on the tooltip.\nHighest value is highlighted in green.");
            }

            bool atlasHeaderOpen = ImGui.TreeNodeEx("Atlas Highlights", ImGuiTreeNodeFlags.CollapsingHeader);
            ImGui.SameLine();
            HelpMarker(@"Highlights nodes on the Atlas that:
- Haven't been completed yet
- Have missing bonus objectives
- Are currently witnessed by the Maven
* Type 'a|e' in the Atlas Search Box to force the client to load all nodes data, this is only needed upon logging in.");

            if (atlasHeaderOpen)
            {
                Settings.ShowAtlasHighlight.Value = Checkbox("Incomplete Maps", Settings.ShowAtlasHighlight.Value);
                ImGui.SameLine();
                nuVector4 atlasNotCompletedColor = SharpToNu(Settings.AtlasNotCompletedColor.Value.ToVector4());
                if (ImGui.ColorEdit4("##AtlasIncomplete", ref atlasNotCompletedColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
                    Settings.AtlasNotCompletedColor.Value = atlasNotCompletedColor.ToSharpColor();
                Settings.AtlasHighlightRadius.Value = IntSlider("Radius##AtlasIncomplete", Settings.AtlasHighlightRadius, 400f);

                Settings.ShowAtlasBonusHighlight.Value = Checkbox("Incomplete Bonus", Settings.ShowAtlasBonusHighlight.Value);
                ImGui.SameLine();
                nuVector4 atlasBonusIncompleteColor = SharpToNu(Settings.AtlasBonusIncompleteColor.Value.ToVector4());
                if (ImGui.ColorEdit4("##AtlasBonus", ref atlasBonusIncompleteColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
                    Settings.AtlasBonusIncompleteColor.Value = atlasBonusIncompleteColor.ToSharpColor();
                Settings.AtlasBonusHighlightRadius.Value = IntSlider("Radius##AtlasBonus", Settings.AtlasBonusHighlightRadius, 400f);

                Settings.ShowMavenWitnessHighlight.Value = Checkbox("Maven Witness", Settings.ShowMavenWitnessHighlight.Value);
                ImGui.SameLine();
                nuVector4 mavenWitnessColor = SharpToNu(Settings.MavenWitnessColor.Value.ToVector4());
                if (ImGui.ColorEdit4("##MavenWitness", ref mavenWitnessColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
                    Settings.MavenWitnessColor.Value = mavenWitnessColor.ToSharpColor();
                Settings.MavenWitnessHighlightRadius.Value = IntSlider("Radius##MavenWitness", Settings.MavenWitnessHighlightRadius, 400f);
            }

            if (ImGui.TreeNodeEx("Borders and Highlight Colours", ImGuiTreeNodeFlags.CollapsingHeader))
            {

                Settings.BorderDeflation.Value = IntSlider(
                    "Map Border Deflation##MapBorderDeflation",
                    Settings.BorderDeflation, 400f
                );
                Settings.BorderThickness.Value = IntSlider(
                    "Tooltip Border Thickness##BorderThickness",
                    Settings.BorderThickness, 400f
                );
                Settings.UseSimpleOutlines.Value = Checkbox("Use Simple Outlines (Brackets)", Settings.UseSimpleOutlines.Value);
                ImGui.SameLine();
                HelpMarker("Replaces full borders with square brackets [ ] for Good/Bad mods and an 'X' for Bricked maps.\n" +
                           "Highly performant and clear on small icons.");
                if (Settings.UseSimpleOutlines.Value)
                {
                    ImGui.Indent();
                    Settings.SimpleOutlinesThickness.Value = IntSlider(
                        "Bracket Thickness##SimpleOutlinesThickness",
                        Settings.SimpleOutlinesThickness, 400f
                    );
                    Settings.SimpleOutlinesBrickedSize.Value = IntSlider(
                        "Bricked 'X' Size %##SimpleOutlinesBrickedSize",
                        Settings.SimpleOutlinesBrickedSize, 400f
                    );

                    ImGui.Text("Line Orientation:"); ImGui.SameLine();
                    ImGui.SetNextItemWidth(150);
                    if (ImGui.BeginCombo("##SimpleOutlineOrientation", Settings.SimpleOutlineOrientation.Value))
                    {
                        foreach (var val in Settings.SimpleOutlineOrientation.Values)
                            if (ImGui.Selectable(val, val == Settings.SimpleOutlineOrientation.Value)) Settings.SimpleOutlineOrientation.Value = val;
                        ImGui.EndCombo();
                    }
                    ImGui.Text("Good Mod Line Position:"); ImGui.SameLine();
                    ImGui.SetNextItemWidth(150);
                    if (ImGui.BeginCombo("##SimpleOutlineGoodPosition", Settings.SimpleOutlineGoodPosition.Value))
                    {
                        foreach (var val in Settings.SimpleOutlineGoodPosition.Values)
                            if (ImGui.Selectable(val, val == Settings.SimpleOutlineGoodPosition.Value)) Settings.SimpleOutlineGoodPosition.Value = val;
                        ImGui.EndCombo();
                    }

                    ImGui.Spacing();
                    ImGui.Text("Preview:"); ImGui.SameLine();
                    var drawList = ImGui.GetWindowDrawList();
                    var pPos = ImGui.GetCursorScreenPos();
                    float pSize = 40f;
                    ImGui.Dummy(new nuVector2(pSize, pSize));
                    
                    drawList.AddRectFilled(pPos, pPos + new nuVector2(pSize, pSize), ImGui.GetColorU32(ImGuiCol.FrameBg));
                    drawList.AddText(pPos + new nuVector2(8, 12), ImGui.GetColorU32(ImGuiCol.TextDisabled), "MAP");

                    var pThickness = Settings.SimpleOutlinesThickness.Value;
                    var pGoodColor = ImGui.ColorConvertFloat4ToU32(SharpToNu(Settings.MapBorderGood.Value.ToVector4()));
                    var pBadColor = ImGui.ColorConvertFloat4ToU32(SharpToNu(Settings.MapBorderBad.Value.ToVector4()));
                    bool pIsHorizontal = Settings.SimpleOutlineOrientation.Value == "Horizontal";
                    bool pGoodIsTopLeft = Settings.SimpleOutlineGoodPosition.Value == "Top/Left";
                    float pWing = pSize * 0.25f;

                    void DrawPreviewBracket(bool topOrLeft, uint color)
                    {
                        if (pIsHorizontal)
                        {
                            if (topOrLeft) // Top bracket ⊓
                            {
                                drawList.AddLine(pPos, pPos + new nuVector2(pSize, 0), color, pThickness);
                                drawList.AddLine(pPos, pPos + new nuVector2(0, pWing), color, pThickness);
                                drawList.AddLine(pPos + new nuVector2(pSize, 0), pPos + new nuVector2(pSize, pWing), color, pThickness);
                            }
                            else // Bottom bracket ⊔
                            {
                                drawList.AddLine(pPos + new nuVector2(0, pSize), pPos + new nuVector2(pSize, pSize), color, pThickness);
                                drawList.AddLine(pPos + new nuVector2(0, pSize), pPos + new nuVector2(0, pSize - pWing), color, pThickness);
                                drawList.AddLine(pPos + new nuVector2(pSize, pSize), pPos + new nuVector2(pSize, pSize - pWing), color, pThickness);
                            }
                        }
                        else // Vertical
                        {
                            if (topOrLeft) // Left bracket [
                            {
                                drawList.AddLine(pPos, pPos + new nuVector2(0, pSize), color, pThickness);
                                drawList.AddLine(pPos, pPos + new nuVector2(pWing, 0), color, pThickness);
                                drawList.AddLine(pPos + new nuVector2(0, pSize), pPos + new nuVector2(pWing, pSize), color, pThickness);
                            }
                            else // Right bracket ]
                            {
                                drawList.AddLine(pPos + new nuVector2(pSize, 0), pPos + new nuVector2(pSize, pSize), color, pThickness);
                                drawList.AddLine(pPos + new nuVector2(pSize, 0), pPos + new nuVector2(pSize - pWing, 0), color, pThickness);
                                drawList.AddLine(pPos + new nuVector2(pSize, pSize), pPos + new nuVector2(pSize - pWing, pSize), color, pThickness);
                            }
                        }
                    }
                    DrawPreviewBracket(pGoodIsTopLeft, pGoodColor);
                    DrawPreviewBracket(!pGoodIsTopLeft, pBadColor);
                    ImGui.Unindent();
                }
                ImGui.Dummy(new nuVector2(0, 2));
                nuVector4 mapBorderGood = SharpToNu(Settings.MapBorderGood.Value.ToVector4());
                if (ImGui.ColorEdit4("Good Mods Color", ref mapBorderGood, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
                    Settings.MapBorderGood.Value = mapBorderGood.ToSharpColor();
                ImGui.SameLine();
                HelpMarker("Set transparency (Alpha) to see the map icon through the highlight.");
                ImGui.SameLine(0f, 20f);
                nuVector4 mapBorderBad = SharpToNu(Settings.MapBorderBad.Value.ToVector4());
                if (ImGui.ColorEdit4("Bad Mods Color", ref mapBorderBad, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
                    Settings.MapBorderBad.Value = mapBorderBad.ToSharpColor();
                ImGui.SameLine();
                HelpMarker("Set transparency (Alpha) to see the map icon through the highlight.");
                ImGui.SameLine(0f, 20f);
                nuVector4 brickedColor = SharpToNu(Settings.Bricked.Value.ToVector4());
                if (ImGui.ColorEdit4("Bricked Map", ref brickedColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
                    Settings.Bricked.Value = brickedColor.ToSharpColor();
                ImGui.Dummy(new nuVector2(0, 2));
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
                    LoadModsDatabase();
                }
                if (ImGui.Button("Recreate Default Warnings Text Files"))
                    ResetConfigs();
                ImGui.SameLine();
                HelpMarker(
                    "This will irreversibly delete all your existing warnings config files!"
                );
                Settings.TooltipOffsetX.Value = IntSlider("Tooltip X Offset", Settings.TooltipOffsetX, 400f);
                Settings.TooltipOffsetY.Value = IntSlider("Tooltip Y Offset", Settings.TooltipOffsetY, 400f);
                ImGui.SameLine();
                HelpMarker("Adjust the base position of the tooltip relative to the cursor.");
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
