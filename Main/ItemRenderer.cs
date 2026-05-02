using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ImGuiNET;
using ExileCore.Shared.Helpers;
using SharpDX;
using System.Linq;
using nuVector2 = System.Numerics.Vector2;
using nuVector4 = System.Numerics.Vector4;

namespace MapNotify_3_28
{
    public partial class MapNotify_3_28
    {
        /// <summary>
        /// Renders the custom ImGui tooltip overlay for a specific item.
        /// Handles positioning logic, including offsets for other popular plugins (NinjaPricer, etc.).
        /// </summary>
        public void RenderItem(NormalInventoryItem Item, Entity Entity)
        {
            var pushedColors = 0;
            var entity = Entity;
            if (entity != null && entity.Address != 0 && entity.IsValid)
            {
                var ItemDetails = Entity.GetHudComponent<ItemDetails>();
                if (ItemDetails == null)
                {
                    ItemDetails = new ItemDetails(Item, Entity);
                    Entity.SetHudComponent(ItemDetails);
                }
                if (ItemDetails == null) return;

                // Refresh item details to ensure the tooltip reflects any changes made in the Mod Preview window or config files.
                ItemDetails.Update();

                var alwaysShow = Settings.AlwaysShowTooltip.Value;
                if (alwaysShow || ItemDetails.ActiveGoodMods.Count > 0 || ItemDetails.ActiveBadMods.Count > 0)
                {
                    var mousePos = MouseLite.GetCursorPositionVector();
                    var boxOrigin = new nuVector2(mousePos.X + Settings.TooltipOffsetX.Value, mousePos.Y + Settings.TooltipOffsetY.Value);

                    if (Settings.PadForBigCursor.Value && ItemDetails.NeedsPadding)
                        boxOrigin += new nuVector2(20, 35);
                    if (Settings.PadForNinjaPricer.Value && ItemDetails.NeedsPadding)
                        boxOrigin += new nuVector2(0, 56);
                    if (Settings.PadForNinjaPricer2.Value && ItemDetails.NeedsPadding)
                        boxOrigin += new nuVector2(0, 114);
                    if (Settings.PadForAltPricer.Value && ItemDetails.NeedsPadding)
                        boxOrigin += new nuVector2(0, 30);

                    var windowId = ItemDetails.WindowID;

                    var _opened = true;
                    pushedColors += 1;
                    ImGui.PushStyleColor(ImGuiCol.WindowBg, 0xFF3F3F3F);
                    if (ImGui.Begin(windowId, ref _opened, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoNavInputs))
                    {
                        ImGui.BeginGroup();
                        DrawTooltipContent(ItemDetails, entity);
                        ImGui.EndGroup();

                        // Border Logic
                        if (ItemDetails.Bricked)
                        {
                            var min = ImGui.GetItemRectMin(); min.X -= 8; min.Y -= 8;
                            var max = ImGui.GetItemRectMax(); max.X += 8; max.Y += 8;
                            var brickColor = ColorToUint(SharpToNu(Settings.Bricked.Value.ToVector4()));
                            var drawList = ImGui.GetForegroundDrawList();
                            for (int i = 0; i < Settings.BorderThickness.Value; i++) drawList.AddRect(new nuVector2(min.X - i, min.Y - i), new nuVector2(max.X + i, max.Y + i), brickColor, 0f, 0, 1.0f);
                        }

                        var size = ImGui.GetWindowSize();
                        var finalPos = boxOrigin;
                        if (finalPos.X + size.X > windowArea.Width) finalPos.X = windowArea.Width - size.X - 10;
                        if (finalPos.Y + size.Y > windowArea.Height) finalPos.Y = windowArea.Height - size.Y - 10;
                        ImGui.SetWindowPos(finalPos, ImGuiCond.Always);
                    }
                    ImGui.End();
                    ImGui.PopStyleColor(pushedColors);
                }
            }
        }

        private void DrawTooltipContent(ItemDetails details, Entity entity)
        {
            if (Settings.ShowMapName.Value)
                ImGui.TextColored(details.ItemColor, details.EscapedMapName);

            DrawStatsBlock(details);
            if (Settings.ShowHeistInfo.Value) DrawHeistBlock(details);
            if (Settings.ShowLogbookInfo.Value) DrawLogbookBlock(details);
            DrawOriginatorBlock(details);

            if (Settings.HorizontalLines.Value) ImGui.Separator();

            if (Settings.ShowModCount.Value && details.ModCount != 0)
            {
                var modCountStr = $"{details.ModCount} Mods";
                var color = details.Corrupted ? new nuVector4(1f, 0f, 0f, 1f) : new nuVector4(1f, 1f, 1f, 1f);
                ImGui.TextColored(color, details.Corrupted ? $"{modCountStr}, Corrupted" : modCountStr);
            }

            if (Settings.ShowModWarnings.Value) DrawWarningsBlock(details);
        }

        private void DrawStatsBlock(ItemDetails details)
        {
            var prefixSuffixLines = details.GetPrefixSuffixLines();
            if (Settings.ShowPrefixSuffixStats.Value && prefixSuffixLines.Count > 0)
            {
                for (int lineIdx = 0; lineIdx < prefixSuffixLines.Count; lineIdx++)
                {
                    var line = prefixSuffixLines[lineIdx];
                    var startX = ImGui.GetCursorPosX();
                    for (int i = 0; i < line.Count; i++)
                    {
                        if (i > 0)
                        {
                            ImGui.SameLine();
                            ImGui.SetCursorPosX(startX + (i == 1 ? 20 : i == 2 ? 87 : 152));
                        }
                        ImGui.TextColored(line[i].Color, line[i].EscapedText);
                    }
                    if (lineIdx == 0) ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 1f);
                }
            }
            else
            {
                var green = new nuVector4(0.4f, 1f, 0.4f, 1f);
                var red = new nuVector4(1f, 0.4f, 0.4f, 1f);
                var white = new nuVector4(1f, 1f, 1f, 1f);

                bool drawn = false;
                if (Settings.ShowQuantityPercent.Value && details.Quantity != 0)
                {
                    var col = Settings.ColorQuantityPercent.Value ? (details.Quantity < Settings.ColorQuantity.Value ? red : green) : white;
                    ImGui.TextColored(col, $"{details.Quantity}%% IIQ");
                    drawn = true;
                }
                if (Settings.ShowPackSizePercent.Value && details.PackSize != 0)
                {
                    if (drawn) ImGui.SameLine();
                    var col = Settings.ColorPackSizePercent.Value ? (details.PackSize < Settings.ColorPackSize.Value ? red : green) : white;
                    ImGui.TextColored(col, $"{details.PackSize}%% PS");
                    drawn = true;
                }
                if (Settings.ShowRarityPercent.Value && details.Rarity != 0)
                {
                    if (drawn) ImGui.SameLine();
                    var col = Settings.ColorRarityPercent.Value ? (details.Rarity < Settings.ColorRarity.Value ? red : green) : white;
                    ImGui.TextColored(col, $"{details.Rarity}%% IIR");
                }
            }
            if (Settings.ShowChisel.Value && !string.IsNullOrEmpty(details.ChiselName))
                ImGui.TextColored(SharpToNu(Settings.ChiselColor.Value.ToVector4()), $"+{details.ChiselValue}%% {details.ChiselName}");
        }

        private void DrawHeistBlock(ItemDetails details)
        {
            if (details.HeistAreaLevel <= 0 && details.HeistJobLines.Count <= 0) return;
            if (Settings.HorizontalLines.Value) ImGui.Separator();
            var heistColor = new nuVector4(0.5f, 0.8f, 1f, 1f);
            if (details.HeistAreaLevel > 0) ImGui.TextColored(heistColor, $"Area Level: {details.HeistAreaLevel}");
            foreach (var line in details.HeistJobLines)
                ImGui.TextColored(line.IsRevealed ? heistColor : new nuVector4(0.5f, 0.8f, 1f, 0.5f), line.Text);
        }

        private void DrawLogbookBlock(ItemDetails details)
        {
            if (!details.IsLogbook || details.LogbookAreaLevel <= 0) return;
            if (Settings.HorizontalLines.Value) ImGui.Separator();
            var logbookColor = new nuVector4(1f, 0.7f, 0.3f, 1f);
            ImGui.TextColored(logbookColor, $"Logbook Area Level: {details.LogbookAreaLevel}");
            foreach (var area in details.LogbookAreas)
            {
                if (Settings.HorizontalLines.Value) ImGui.Separator();
                ImGui.TextColored(logbookColor, area.Name); ImGui.SameLine();
                ImGui.TextColored(new nuVector4(0.7f, 0.7f, 0.7f, 1f), $"({area.Faction})");
                foreach (var imp in area.Implicits)
                    ImGui.TextColored(imp.Color, $"{(imp.Bricking ? "[B] " : "- ")}{imp.EscapedText}");
            }
        }

        private void DrawOriginatorBlock(ItemDetails details)
        {
            if (!details.IsOriginatorMap || !details.Identified) return;
            var lines = Settings.ShowPrefixSuffixStats.Value ? details.GetOriginatorBreakdownLines() : null;

            if (Settings.ShowPrefixSuffixStats.Value && lines?.Count > 0)
            {
                if (Settings.HorizontalLines.Value) ImGui.Separator();
                foreach (var line in lines)
                {
                    var startX = ImGui.GetCursorPosX();
                    for (int i = 0; i < line.Count; i++)
                    {
                        if (i > 0)
                        {
                            ImGui.SameLine();
                            ImGui.SetCursorPosX(startX + (i == 1 ? (line[i].Text == "Maps" ? 31 : 30) : i == 2 ? 92 : 162));
                        }
                        ImGui.TextColored(line[i].Color, line[i].EscapedText);
                    }
                }
            }
            else if (Settings.ShowOriginatorMaps.Value || Settings.ShowOriginatorScarabs.Value || Settings.ShowOriginatorCurrency.Value)
            {
                if (Settings.HorizontalLines.Value) ImGui.Separator();
                if (Settings.ShowOriginatorMaps.Value) DrawStatLine(details.OriginatorMaps, "Maps", new nuVector4(0.5f, 0.85f, 1f, 1f), 31);
                if (Settings.ShowOriginatorScarabs.Value) DrawStatLine(details.OriginatorScarabs, "Scarabs", new nuVector4(0.85f, 0.45f, 0.85f, 1f), 30);
                if (Settings.ShowOriginatorCurrency.Value) DrawStatLine(details.OriginatorCurrency, "Currency", new nuVector4(0.0f, 1.0f, 0.0f, 1.0f), 30);
            }
        }

        private void DrawStatLine(int val, string label, nuVector4 color, float offset)
        {
            var startX = ImGui.GetCursorPosX();
            ImGui.TextColored(color, val == 0 ? "--" : $"{val}%%");
            ImGui.SameLine();
            ImGui.SetCursorPosX(startX + offset);
            ImGui.TextColored(color, label);
        }

        private void DrawWarningsBlock(ItemDetails details)
        {
            foreach (var styled in details.ConflictingMods)
            {
                ImGui.TextColored(styled.Color, styled.EscapedText);
            }
            if (details.ConflictingMods.Count > 0 && (details.ActiveGoodMods.Count > 0 || details.ActiveBadMods.Count > 0))
                ImGui.Dummy(new nuVector2(0, 5));

            foreach (var styled in details.ActiveGoodMods)
            {
                if (details.IsLogbook && Settings.ShowLogbookInfo.Value && details.LogbookAreas.Any(a => a.Implicits.Contains(styled))) continue;
                ImGui.TextColored(styled.Color, styled.EscapedText);
            }

            if ((details.ActiveGoodMods.Count > 0 || details.ConflictingMods.Count > 0) && details.ActiveBadMods.Count > 0) ImGui.Dummy(new nuVector2(0, 5));

            foreach (var styled in details.ActiveBadMods)
            {
                if (details.IsLogbook && Settings.ShowLogbookInfo.Value && details.LogbookAreas.Any(a => a.Implicits.Contains(styled))) continue;
                ImGui.TextColored(styled.Color, $"{(styled.Bricking ? "[B] " : "")}{styled.EscapedText}");
            }
        }

        /// <summary>
        /// Draws colored highlights (borders or filled rects) around items in inventory or stash tabs.
        /// </summary>
        private void DrawMapBorders(NormalInventoryItem item, Entity entity, RectangleF? rectOverride = null)
        {
            var rect = rectOverride ?? item.GetClientRect();
            double deflatePercent = Settings.BorderDeflation;
            var deflateWidth = (int)(rect.Width * (deflatePercent / 100.0));
            var deflateHeight = (int)(rect.Height * (deflatePercent / 100.0));
            rect.Inflate(-deflateWidth, -deflateHeight);
            var itemDetails = entity.GetHudComponent<ItemDetails>();
            if (itemDetails == null)
            {
                itemDetails = new ItemDetails(item, entity);
                entity.SetHudComponent(itemDetails);
            }

            // Bricked highlight (High priority frame)
            if (Settings.BoxForBricked && itemDetails.Bricked)
            {
                if (Settings.UseSimpleOutlines)
                {
                    // Draw an 'X' for bricked maps
                    Graphics.DrawLine(rect.TopLeft.ToVector2Num(), rect.BottomRight.ToVector2Num(), Settings.BorderThicknessMap.Value, Settings.Bricked.Value);
                    Graphics.DrawLine(rect.TopRight.ToVector2Num(), rect.BottomLeft.ToVector2Num(), Settings.BorderThicknessMap.Value, Settings.Bricked.Value);
                }
                else
                {
                    Graphics.DrawFrame(rect, Settings.Bricked.Value, Settings.BorderThicknessMap);
                }
            }

            // Mapping logic to match UI labels:
            var hasBadMod = Settings.BoxForMapWarnings && (itemDetails.ActiveBadMods.Count > 0 || itemDetails.ConflictingMods.Count > 0);
            var hasGoodMod = Settings.BoxForMapBadWarnings && (itemDetails.ActiveGoodMods.Count > 0 || itemDetails.ConflictingMods.Count > 0);


            if (Settings.UseSimpleOutlines)
            {
                DrawSimpleOutlines(rect, hasGoodMod, hasBadMod);
                return; // Exit after drawing advanced outlines
            }

            // Default to filled boxes if neither simple nor advanced outlines are enabled
            if (hasGoodMod && hasBadMod)
            {
                // Both good and bad mods: multi-color filled rectangle for a diagonal gradient effect
                Graphics.DrawRectFilledMultiColor(
                    rect.TopLeft.ToVector2Num(),
                    rect.BottomRight.ToVector2Num(),
                    Settings.MapBorderBad.Value,  // TopLeft
                    Settings.MapBorderGood.Value, // TopRight
                    Settings.MapBorderBad.Value,  // BottomRight
                    Settings.MapBorderGood.Value  // BottomLeft
                );
            }
            else if (hasBadMod) Graphics.DrawBox(rect, Settings.MapBorderBad.Value);
            else if (hasGoodMod) Graphics.DrawBox(rect, Settings.MapBorderGood.Value);
        }

        private void DrawSimpleOutlines(RectangleF rect, bool hasGoodMod, bool hasBadMod)
        {
            var thickness = Settings.SimpleOutlinesThickness.Value;
            var goodColor = Settings.MapBorderGood.Value;
            var badColor = Settings.MapBorderBad.Value;

            bool isHorizontal = Settings.SimpleOutlineOrientation.Value == "Horizontal";
            bool goodIsTopOrLeft = Settings.SimpleOutlineGoodPosition.Value == "Top/Left";

            // Use 25% of the side length for the bracket "wings"
            float wingWidth = rect.Width * 0.25f;
            float wingHeight = rect.Height * 0.25f;

            void DrawBracket(bool topOrLeft, Color color)
            {
                if (isHorizontal)
                {
                    if (topOrLeft) // Top bracket ⊓
                    {
                        Graphics.DrawLine(rect.TopLeft.ToVector2Num(), rect.TopRight.ToVector2Num(), thickness, color);
                        Graphics.DrawLine(rect.TopLeft.ToVector2Num(), rect.TopLeft.ToVector2Num() + new nuVector2(0, wingHeight), thickness, color);
                        Graphics.DrawLine(rect.TopRight.ToVector2Num(), rect.TopRight.ToVector2Num() + new nuVector2(0, wingHeight), thickness, color);
                    }
                    else // Bottom bracket ⊔
                    {
                        Graphics.DrawLine(rect.BottomLeft.ToVector2Num(), rect.BottomRight.ToVector2Num(), thickness, color);
                        Graphics.DrawLine(rect.BottomLeft.ToVector2Num(), rect.BottomLeft.ToVector2Num() - new nuVector2(0, wingHeight), thickness, color);
                        Graphics.DrawLine(rect.BottomRight.ToVector2Num(), rect.BottomRight.ToVector2Num() - new nuVector2(0, wingHeight), thickness, color);
                    }
                }
                else
                {
                    if (topOrLeft) // Left bracket [
                    {
                        Graphics.DrawLine(rect.TopLeft.ToVector2Num(), rect.BottomLeft.ToVector2Num(), thickness, color);
                        Graphics.DrawLine(rect.TopLeft.ToVector2Num(), rect.TopLeft.ToVector2Num() + new nuVector2(wingWidth, 0), thickness, color);
                        Graphics.DrawLine(rect.BottomLeft.ToVector2Num(), rect.BottomLeft.ToVector2Num() + new nuVector2(wingWidth, 0), thickness, color);
                    }
                    else // Right bracket ]
                    {
                        Graphics.DrawLine(rect.TopRight.ToVector2Num(), rect.BottomRight.ToVector2Num(), thickness, color);
                        Graphics.DrawLine(rect.TopRight.ToVector2Num(), rect.TopRight.ToVector2Num() - new nuVector2(wingWidth, 0), thickness, color);
                        Graphics.DrawLine(rect.BottomRight.ToVector2Num(), rect.BottomRight.ToVector2Num() - new nuVector2(wingWidth, 0), thickness, color);
                    }
                }
            }

            if (hasGoodMod && hasBadMod)
            {
                DrawBracket(goodIsTopOrLeft, goodColor);
                DrawBracket(!goodIsTopOrLeft, badColor);
            }
            else if (hasGoodMod)
            {
                DrawBracket(goodIsTopOrLeft, goodColor);
            }
            else if (hasBadMod)
            {
                DrawBracket(!goodIsTopOrLeft, badColor);
            }
        }
    }
}