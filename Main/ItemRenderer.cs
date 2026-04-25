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
        public void RenderItem(NormalInventoryItem Item, Entity Entity, bool isInventory = false, int mapNum = 0)
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

                var alwaysShow = Settings.AlwaysShowTooltip.Value;
                if (alwaysShow || ItemDetails.ActiveGoodMods.Count > 0 || ItemDetails.ActiveBadMods.Count > 0)
                {
                    var mousePos = MouseLite.GetCursorPositionVector();
                    var boxOrigin = new nuVector2(mousePos.X + 25, mousePos.Y);

                    if (Settings.PadForBigCursor.Value && ItemDetails.NeedsPadding)
                        boxOrigin = new nuVector2(mousePos.X + 45, mousePos.Y + 35);
                    if (Settings.PadForNinjaPricer.Value && ItemDetails.NeedsPadding)
                        boxOrigin = new nuVector2(mousePos.X + 25, mousePos.Y + 56);
                    if (Settings.PadForNinjaPricer2.Value && ItemDetails.NeedsPadding)
                        boxOrigin = new nuVector2(mousePos.X + 25, mousePos.Y + 114);
                    if (Settings.PadForAltPricer.Value && ItemDetails.NeedsPadding)
                        boxOrigin = new nuVector2(mousePos.X + 25, mousePos.Y + 30);

                    var windowId = ItemDetails.WindowID;
                    if (isInventory)
                    {
                        if (mapNum < lastCol) { boxSize = new nuVector2(0, 0); rowSize += maxSize + 2; maxSize = 0; }
                        var framePos = ingameState.UIHover.Parent.GetClientRect().TopRight;
                        framePos.X += 10 + boxSize.X;
                        framePos.Y -= 200;
                        boxOrigin = new nuVector2(framePos.X, framePos.Y + rowSize);
                    }

                    var _opened = true;
                    pushedColors += 1;
                    ImGui.PushStyleColor(ImGuiCol.WindowBg, 0xFF3F3F3F);
                    if (ImGui.Begin(windowId, ref _opened, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoNavInputs))
                    {
                        ImGui.BeginGroup();
                        if (isInventory || Settings.ShowMapName.Value) ImGui.TextColored(ItemDetails.ItemColor, ItemDetails.EscapedMapName);

                        // Map Stats Block
                        var prefixSuffixLines = ItemDetails.GetPrefixSuffixLines();
                        if (Settings.ShowPrefixSuffixStats.Value && prefixSuffixLines.Count > 0)
                        {
                            for (int lineIdx = 0; lineIdx < prefixSuffixLines.Count; lineIdx++)
                            {
                                var line = prefixSuffixLines[lineIdx];
                                var startX = ImGui.GetCursorPosX();
                                for (int i = 0; i < line.Count; i++)
                                {
                                    var text = line[i].EscapedText;
                                    if (i > 0)
                                    {
                                        ImGui.SameLine();
                                        if (i == 1) ImGui.SetCursorPosX(startX + 20); 
                                        else if (i == 2) ImGui.SetCursorPosX(startX + 87); 
                                        else if (i == 3) ImGui.SetCursorPosX(startX + 152); 
                                    }
                                    ImGui.TextColored(line[i].Color, text);
                                }
                                if (lineIdx == 0) ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 1f);
                            }
                            if (Settings.ShowChisel.Value && !string.IsNullOrEmpty(ItemDetails.ChiselName)) ImGui.TextColored(Settings.ChiselColor, $"+{ItemDetails.ChiselValue}%% {ItemDetails.ChiselName}");
                        }
                        else
                        {
                            var qCol = new nuVector4(1f, 1f, 1f, 1f);
                            if (Settings.ColorQuantityPercent.Value) qCol = ItemDetails.Quantity < Settings.ColorQuantity.Value ? new nuVector4(1f, 0.4f, 0.4f, 1f) : new nuVector4(0.4f, 1f, 0.4f, 1f);

                            var pCol = new nuVector4(1f, 1f, 1f, 1f);
                            if (Settings.ColorPackSizePercent.Value) pCol = ItemDetails.PackSize < Settings.ColorPackSize.Value ? new nuVector4(1f, 0.4f, 0.4f, 1f) : new nuVector4(0.4f, 1f, 0.4f, 1f);

                            var rCol = new nuVector4(1f, 1f, 1f, 1f);
                            if (Settings.ColorRarityPercent.Value) rCol = ItemDetails.Rarity < Settings.ColorRarity.Value ? new nuVector4(1f, 0.4f, 0.4f, 1f) : new nuVector4(0.4f, 1f, 0.4f, 1f);

                            var showQuant = Settings.ShowQuantityPercent.Value;
                            var showPack = Settings.ShowPackSizePercent.Value;
                            var showRarity = Settings.ShowRarityPercent.Value;

                            bool drawnSomething = false;
                            if (showQuant && ItemDetails.Quantity != 0)
                            {
                                ImGui.TextColored(qCol, $"{ItemDetails.Quantity}%% IIQ");
                                drawnSomething = true;
                            }

                            if (showPack && ItemDetails.PackSize != 0)
                            {
                                if (drawnSomething) ImGui.SameLine();
                                ImGui.TextColored(pCol, $"{ItemDetails.PackSize}%% PS");
                                drawnSomething = true;
                            }

                            if (showRarity && ItemDetails.Rarity != 0)
                            {
                                if (drawnSomething) ImGui.SameLine();
                                ImGui.TextColored(rCol, $"{ItemDetails.Rarity}%% IIR");
                            }
                            if (Settings.ShowChisel.Value && !string.IsNullOrEmpty(ItemDetails.ChiselName)) ImGui.TextColored(Settings.ChiselColor, $"+{ItemDetails.ChiselValue}%% {ItemDetails.ChiselName}");
                        }

                        // Heist Info
                        if (Settings.ShowHeistInfo.Value && (ItemDetails.HeistAreaLevel > 0 || ItemDetails.HeistJobLines.Count > 0))
                        {
                            if (Settings.HorizontalLines.Value) ImGui.Separator();
                            var heistColor = new nuVector4(0.5f, 0.8f, 1f, 1f);
                            if (ItemDetails.HeistAreaLevel > 0) ImGui.TextColored(heistColor, $"Area Level: {ItemDetails.HeistAreaLevel}");
                            foreach (var line in ItemDetails.HeistJobLines) ImGui.TextColored(line.IsRevealed ? heistColor : new nuVector4(0.5f, 0.8f, 1f, 0.5f), line.Text);
                        }

                        // Logbook Info
                        if (Settings.ShowLogbookInfo.Value && ItemDetails.IsLogbook && ItemDetails.LogbookAreaLevel > 0)
                        {
                            if (Settings.HorizontalLines.Value) ImGui.Separator();
                            var logbookColor = new nuVector4(1f, 0.7f, 0.3f, 1f);
                            ImGui.TextColored(logbookColor, $"Logbook Area Level: {ItemDetails.LogbookAreaLevel}");
                            foreach (var area in ItemDetails.LogbookAreas)
                            {
                                if (Settings.HorizontalLines.Value) ImGui.Separator();
                                ImGui.TextColored(logbookColor, area.Name); ImGui.SameLine();
                                ImGui.TextColored(new nuVector4(0.7f, 0.7f, 0.7f, 1f), $"({area.Faction})");
                                foreach (var implicitMod in area.Implicits) ImGui.TextColored(implicitMod.Color, $"{(implicitMod.Bricking ? "[B] " : "- ")}{implicitMod.EscapedText}");
                            }
                        }

                        // Originator Stats
                        if (ItemDetails.IsOriginatorMap)
                        {
                            if (Settings.ShowPrefixSuffixStats.Value)
                            {
                                var originatorLines = ItemDetails.GetOriginatorBreakdownLines();
                                if (originatorLines.Count > 0)
                                {
                                    if (Settings.HorizontalLines.Value) ImGui.Separator();
                                    foreach (var line in originatorLines)
                                    {
                                        var startX = ImGui.GetCursorPosX();
                                        for (int i = 0; i < line.Count; i++)
                                        {
                                            var text = line[i].EscapedText;
                                            if (i > 0)
                                            {
                                                ImGui.SameLine();
                                                if (text.Contains("P:")) ImGui.SetCursorPosX(startX + 102); 
                                                else if (text.Contains("S:")) ImGui.SetCursorPosX(startX + 172); 
                                            }
                                            ImGui.TextColored(line[i].Color, text);
                                        }
                                    }
                                }
                            }
                            else if (Settings.ShowOriginatorMaps.Value || Settings.ShowOriginatorScarabs.Value || Settings.ShowOriginatorCurrency.Value)
                            {
                                if (Settings.HorizontalLines.Value) ImGui.Separator();
                                if (Settings.ShowOriginatorMaps.Value) ImGui.TextColored(new nuVector4(0.5f, 0.85f, 1f, 1f), $"+{ItemDetails.OriginatorMaps}%% Maps");
                                if (Settings.ShowOriginatorScarabs.Value) ImGui.TextColored(new nuVector4(0.85f, 0.45f, 0.85f, 1f), $"+{ItemDetails.OriginatorScarabs}%% Scarabs");
                                if (Settings.ShowOriginatorCurrency.Value) ImGui.TextColored(new nuVector4(0.0f, 1.0f, 0.0f, 1.0f), $"+{ItemDetails.OriginatorCurrency}%% Currency");
                            }
                        }

                        if (Settings.HorizontalLines.Value) ImGui.Separator();

                        // Mod Count & Warnings
                        if (Settings.ShowModCount.Value && ItemDetails.ModCount != 0)
                        {
                            var modCountStr = $"{(isInventory ? ItemDetails.ModCount - 1 : ItemDetails.ModCount)} Mods";
                            if (entity.GetComponent<Base>().isCorrupted) ImGui.TextColored(new nuVector4(1f, 0f, 0f, 1f), $"{modCountStr}, Corrupted");
                            else ImGui.TextColored(new nuVector4(1f, 1f, 1f, 1f), modCountStr);
                        }

                        if (Settings.ShowModWarnings.Value)
                        {
                            foreach (var styledText in ItemDetails.ActiveGoodMods)
                            {
                                if (ItemDetails.IsLogbook && Settings.ShowLogbookInfo.Value && ItemDetails.LogbookAreas.Any(a => a.Implicits.Contains(styledText))) continue;
                                ImGui.TextColored(styledText.Color, styledText.EscapedText);
                            }
                            if (ItemDetails.ActiveGoodMods.Count > 0 && ItemDetails.ActiveBadMods.Count > 0) ImGui.Dummy(new nuVector2(0, 5));
                            foreach (var styledText in ItemDetails.ActiveBadMods)
                            {
                                if (ItemDetails.IsLogbook && Settings.ShowLogbookInfo.Value && ItemDetails.LogbookAreas.Any(a => a.Implicits.Contains(styledText))) continue;
                                ImGui.TextColored(styledText.Color, $"{(styledText.Bricking ? "[B] " : "")}{styledText.EscapedText}");
                            }
                        }
                        ImGui.EndGroup();

                        // Border Logic
                        if (ItemDetails.Bricked || (ItemIsMap(entity) && isInventory))
                        {
                            var min = ImGui.GetItemRectMin(); min.X -= 8; min.Y -= 8;
                            var max = ImGui.GetItemRectMax(); max.X += 8; max.Y += 8;
                            if (ItemDetails.Bricked)
                            {
                                var brickColor = ColorToUint(Settings.Bricked);
                                var drawList = ImGui.GetForegroundDrawList();
                                for (int i = 0; i < Settings.BorderThickness.Value; i++) drawList.AddRect(new nuVector2(min.X - i, min.Y - i), new nuVector2(max.X + i, max.Y + i), brickColor, 0f, 0, 1.0f);
                            }
                            else if (isInventory) ImGui.GetForegroundDrawList().AddRect(min, max, 0xFF4A4A4A);
                        }

                        var size = ImGui.GetWindowSize();
                        var finalPos = boxOrigin;
                        if (finalPos.X + size.X > windowArea.Width) finalPos.X = windowArea.Width - size.X - 10;
                        if (finalPos.Y + size.Y > windowArea.Height) finalPos.Y = windowArea.Height - size.Y - 10;
                        ImGui.SetWindowPos(finalPos, ImGuiCond.Always);

                        if (isInventory) { boxSize.X += (int)size.X + 2; if (maxSize < size.Y) maxSize = size.Y; lastCol = mapNum; }
                    }
                    ImGui.End();
                    ImGui.PopStyleColor(pushedColors);
                }
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
                Graphics.DrawFrame(rect, Settings.Bricked.ToSharpColor(), Settings.BorderThicknessMap);
            }

            // Mapping logic to match UI labels:
            var hasBadMod = Settings.BoxForMapWarnings && itemDetails.ActiveBadMods.Count > 0;
            var hasGoodMod = Settings.BoxForMapBadWarnings && itemDetails.ActiveGoodMods.Count > 0;

            if (hasGoodMod && hasBadMod)
            {
                // Both good and bad mods: multi-color filled rectangle for a diagonal gradient effect
                Graphics.DrawRectFilledMultiColor(
                    rect.TopLeft.ToVector2Num(),
                    rect.BottomRight.ToVector2Num(),
                    Settings.MapBorderBad.ToSharpColor(),  // TopLeft
                    Settings.MapBorderGood.ToSharpColor(), // TopRight
                    Settings.MapBorderBad.ToSharpColor(),  // BottomRight
                    Settings.MapBorderGood.ToSharpColor()  // BottomLeft
                );
            }
            else if (hasBadMod) Graphics.DrawBox(rect, Settings.MapBorderBad.ToSharpColor());
            else if (hasGoodMod) Graphics.DrawBox(rect, Settings.MapBorderGood.ToSharpColor());
        }
    }
}