using System.Collections.Generic;
using System.Linq;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
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
                var ItemDetails = entity.GetHudComponent<ItemDetails>(); // Use entity.GetHudComponent
                if (ItemDetails == null)
                {
                    ItemDetails = new ItemDetails(Item, Entity);
                    Entity.SetHudComponent(ItemDetails);
                }
                if (ItemDetails == null)
                    return;

                // Refresh item details to ensure the tooltip reflects any changes made in the Mod Preview window or config files.
                ItemDetails.Update();

                var alwaysShow = Settings.AlwaysShowTooltip.Value;
                if (
                    alwaysShow
                    || ItemDetails.ActiveGoodMods.Count > 0
                    || ItemDetails.ActiveBadMods.Count > 0
                    || (Settings.ShowModsOfInterest.Value && ItemDetails.ActiveInterestMods.Count > 0)
                )
                {
                    var mousePos = MouseLite.GetCursorPositionVector();
                    var boxOrigin = new nuVector2(
                        mousePos.X + Settings.TooltipOffsetX.Value,
                        mousePos.Y + Settings.TooltipOffsetY.Value
                    );

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
                    if (
                        ImGui.Begin(
                            windowId,
                            ref _opened,
                            ImGuiWindowFlags.NoScrollbar
                                | ImGuiWindowFlags.AlwaysAutoResize
                                | ImGuiWindowFlags.NoMove
                                | ImGuiWindowFlags.NoResize
                                | ImGuiWindowFlags.NoInputs
                                | ImGuiWindowFlags.NoSavedSettings
                                | ImGuiWindowFlags.NoTitleBar
                                | ImGuiWindowFlags.NoNavInputs
                        )
                    )
                    {
                        ImGui.BeginGroup();
                        DrawTooltipContent(ItemDetails, entity);
                        ImGui.EndGroup();

                        // Border Logic
                        if (ItemDetails.Bricked)
                        {
                            var min = ImGui.GetItemRectMin();
                            min.X -= 8;
                            min.Y -= 8;
                            var max = ImGui.GetItemRectMax();
                            max.X += 8;
                            max.Y += 8;
                            var brickColor = ColorToUint(
                                SharpToNu(Settings.Bricked.Value.ToVector4())
                            );
                            var drawList = ImGui.GetForegroundDrawList();
                            for (int i = 0; i < Settings.BorderThickness.Value; i++)
                                drawList.AddRect(
                                    new nuVector2(min.X - i, min.Y - i),
                                    new nuVector2(max.X + i, max.Y + i),
                                    brickColor,
                                    0f,
                                    0,
                                    1.0f
                                );
                        }

                        var size = ImGui.GetWindowSize();
                        var finalPos = boxOrigin;
                        if (finalPos.X + size.X > windowArea.Width)
                            finalPos.X = windowArea.Width - size.X - 10;
                        if (finalPos.Y + size.Y > windowArea.Height)
                            finalPos.Y = windowArea.Height - size.Y - 10;
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
            if (Settings.ShowHeistInfo.Value)
                DrawHeistBlock(details);
            if (Settings.ShowLogbookInfo.Value)
                DrawLogbookBlock(details);
            DrawOriginatorBlock(details);

            if (Settings.HorizontalLines.Value)
                ImGui.Separator();

            if (Settings.ShowModCount.Value && details.ModCount != 0)
            {
                var modCountStr = $"{details.ModCount} Mods";
                var color = details.Corrupted
                    ? new nuVector4(1f, 0f, 0f, 1f)
                    : new nuVector4(1f, 1f, 1f, 1f);
                ImGui.TextColored(
                    color,
                    details.Corrupted ? $"{modCountStr}, Corrupted" : modCountStr
                );
            }

            if (Settings.ShowModWarnings.Value || Settings.ShowModsOfInterest.Value)
                DrawWarningsBlock(
                    details,
                    Settings.ShowModWarnings.Value,
                    Settings.ShowModsOfInterest.Value
                );
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
                            ImGui.SetCursorPosX(
                                startX
                                    + (
                                        i == 1 ? 20
                                        : i == 2 ? 87
                                        : 152
                                    )
                            );
                        }
                        ImGui.TextColored(line[i].Color, line[i].EscapedText);
                    }
                    if (lineIdx == 0)
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 1f);
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
                    var col = Settings.ColorQuantityPercent.Value
                        ? (details.Quantity < Settings.ColorQuantity.Value ? red : green)
                        : white;
                    ImGui.TextColored(col, $"{details.Quantity}%% IIQ");
                    drawn = true;
                }
                if (Settings.ShowPackSizePercent.Value && details.PackSize != 0)
                {
                    if (drawn)
                        ImGui.SameLine();
                    var col = Settings.ColorPackSizePercent.Value
                        ? (details.PackSize < Settings.ColorPackSize.Value ? red : green)
                        : white;
                    ImGui.TextColored(col, $"{details.PackSize}%% PS");
                    drawn = true;
                }
                if (Settings.ShowRarityPercent.Value && details.Rarity != 0)
                {
                    if (drawn)
                        ImGui.SameLine();
                    var col = Settings.ColorRarityPercent.Value
                        ? (details.Rarity < Settings.ColorRarity.Value ? red : green)
                        : white;
                    ImGui.TextColored(col, $"{details.Rarity}%% IIR");
                }
            }
            if (Settings.ShowChisel.Value && !string.IsNullOrEmpty(details.ChiselName))
                ImGui.TextColored(
                    SharpToNu(Settings.ChiselColor.Value.ToVector4()),
                    $"+{details.ChiselValue}%% {details.ChiselName}"
                );
        }

        private void DrawHeistBlock(ItemDetails details)
        {
            if (details.HeistAreaLevel <= 0 && details.HeistJobLines.Count <= 0)
                return;
            if (Settings.HorizontalLines.Value)
                ImGui.Separator();
            var heistColor = new nuVector4(0.5f, 0.8f, 1f, 1f);
            if (details.HeistAreaLevel > 0)
                ImGui.TextColored(heistColor, $"Area Level: {details.HeistAreaLevel}");
            foreach (var line in details.HeistJobLines)
                ImGui.TextColored(
                    line.IsRevealed ? heistColor : new nuVector4(0.5f, 0.8f, 1f, 0.5f),
                    line.Text
                );
        }

        private void DrawLogbookBlock(ItemDetails details)
        {
            if (!details.IsLogbook || details.LogbookAreaLevel <= 0)
                return;
            if (Settings.HorizontalLines.Value)
                ImGui.Separator();
            var logbookColor = new nuVector4(1f, 0.7f, 0.3f, 1f);
            ImGui.TextColored(logbookColor, $"Logbook Area Level: {details.LogbookAreaLevel}");
            foreach (var area in details.LogbookAreas)
            {
                if (Settings.HorizontalLines.Value)
                    ImGui.Separator();
                ImGui.TextColored(logbookColor, area.Name);
                ImGui.SameLine();
                ImGui.TextColored(new nuVector4(0.7f, 0.7f, 0.7f, 1f), $"({area.Faction})");
                foreach (var imp in area.Implicits)
                    ImGui.TextColored(
                        imp.Color,
                        $"{(imp.Bricking ? "[B] " : "- ")}{imp.EscapedText}"
                    );
            }
        }

        private void DrawOriginatorBlock(ItemDetails details)
        {
            if (
                !details.IsOriginatorMap
                || !details.Identified
                || (
                    details.OriginatorMaps == 0
                    && details.OriginatorScarabs == 0
                    && details.OriginatorCurrency == 0
                )
            )
                return;
            var lines = Settings.ShowPrefixSuffixStats.Value
                ? details.GetOriginatorBreakdownLines()
                : null;

            if (Settings.ShowPrefixSuffixStats.Value && lines?.Count > 0)
            {
                if (Settings.HorizontalLines.Value)
                    ImGui.Separator();
                foreach (var line in lines)
                {
                    var startX = ImGui.GetCursorPosX();
                    for (int i = 0; i < line.Count; i++)
                    {
                        if (i > 0)
                        {
                            ImGui.SameLine();
                            ImGui.SetCursorPosX(
                                startX
                                    + (
                                        i == 1 ? (line[i].Text == "Maps" ? 31 : 30)
                                        : i == 2 ? 92
                                        : 162
                                    )
                            );
                        }
                        ImGui.TextColored(line[i].Color, line[i].EscapedText);
                    }
                }
            }
            else if (
                Settings.ShowOriginatorMaps.Value
                || Settings.ShowOriginatorScarabs.Value
                || Settings.ShowOriginatorCurrency.Value
            )
            {
                if (Settings.HorizontalLines.Value)
                    ImGui.Separator();
                if (Settings.ShowOriginatorMaps.Value)
                    DrawStatLine(
                        details.OriginatorMaps,
                        "Maps",
                        new nuVector4(0.5f, 0.85f, 1f, 1f),
                        31
                    );
                if (Settings.ShowOriginatorScarabs.Value)
                    DrawStatLine(
                        details.OriginatorScarabs,
                        "Scarabs",
                        new nuVector4(0.85f, 0.45f, 0.85f, 1f),
                        30
                    );
                if (Settings.ShowOriginatorCurrency.Value)
                    DrawStatLine(
                        details.OriginatorCurrency,
                        "Currency",
                        new nuVector4(0.0f, 1.0f, 0.0f, 1.0f),
                        30
                    );
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

        private void DrawWarningsBlock(ItemDetails details, bool showGoodBad, bool showInterest)
        {
            bool IsShownInLogbook(StyledText styled) =>
                details.IsLogbook
                && Settings.ShowLogbookInfo.Value
                && details.LogbookAreas.Any(a => a.Implicits.Contains(styled));

            bool IsShadowed(StyledText styled) =>
                showInterest && details.InterestShadowedMods.Contains(styled);

            var conflictLines = showGoodBad
                ? details.ConflictingMods
                : new List<StyledText>();
            var goodLines = showGoodBad
                ? details
                    .ActiveGoodMods.Where(s => !IsShownInLogbook(s) && !IsShadowed(s))
                    .ToList()
                : new List<StyledText>();
            var interestLines = showInterest
                ? details.ActiveInterestMods.ToList()
                : new List<StyledText>();
            var badLines = showGoodBad
                ? details
                    .ActiveBadMods.Where(s => !IsShownInLogbook(s) && !IsShadowed(s))
                    .ToList()
                : new List<StyledText>();

            bool drawnAny = false;

            void DrawGroup(List<StyledText> lines, bool addBrickPrefix)
            {
                if (lines.Count == 0)
                    return;
                if (drawnAny)
                    ImGui.Dummy(new nuVector2(0, 5));
                foreach (var styled in lines)
                {
                    var text = addBrickPrefix
                        ? $"{(styled.Bricking ? "[B] " : "")}{styled.EscapedText}"
                        : styled.EscapedText;
                    ImGui.TextColored(styled.Color, text);
                }
                drawnAny = true;
            }

            DrawGroup(conflictLines, false);
            DrawGroup(goodLines, false);
            DrawGroup(interestLines, true);
            DrawGroup(badLines, true);
        }

        /// <summary>
        /// Draws one colored arc per mod of interest on an inner ring around the map icon.
        /// Interest is independent of the Good/Bad highlight.
        /// </summary>
        private void DrawInterestArcs(RectangleF rect, ItemDetails details)
        {
            if (!Settings.MarkModsOfInterest.Value || details.ActiveInterestMods.Count == 0)
                return;

            float minSide = System.Math.Min(rect.Width, rect.Height);
            if (minSide <= 0)
                return;

            var center = new nuVector2(
                (rect.TopLeft.X + rect.BottomRight.X) * 0.5f,
                (rect.TopLeft.Y + rect.BottomRight.Y) * 0.5f
            );

            float circleR = Settings.CircleHighlightRadius.Value / 100f * minSide * 0.5f;
            float ringR = circleR * Settings.InterestArcRingRadius.Value / 100f;
            if (ringR <= 0.5f)
                return;

            float thick = minSide * Settings.InterestArcThickness.Value / 100f;
            int maxMods = System.Math.Max(1, Settings.InterestArcMaxMods.Value);
            float step = 360f / maxMods;

            const float pi = (float)System.Math.PI;
            float gapPx = minSide * Settings.InterestArcGap.Value / 100f;
            float gapDeg = System.Math.Min((gapPx / ringR) * (180f / pi), step * 0.5f);
            float arcDeg = step - gapDeg;
            if (arcDeg <= 0f)
                return;

            int n = System.Math.Min(details.ActiveInterestMods.Count, maxMods);
            float spanDeg = n * step - gapDeg;

            var drawList = ImGui.GetBackgroundDrawList();
            float outlineThickness = thick * 0.2f;
            float ext = outlineThickness / ringR; // radians

            // Outline pass for every arc, then the colored pass on top.
            for (int i = 0; i < n; i++)
            {
                var styled = details.ActiveInterestMods[i];
                float a2 = 90f + spanDeg / 2f - i * step;
                float a1 = a2 - arcDeg;
                float rad1 = (a1 - ext) * pi / 180f;
                float rad2 = (a2 + ext) * pi / 180f;
                int segs = System.Math.Max(6, (int)((rad2 - rad1) / (2f * pi) * 48f));

                var outlineColor = new nuVector4(
                    0x0C / 255f,
                    0x10 / 255f,
                    0x24 / 255f,
                    styled.Color.W
                );
                drawList.PathArcTo(center, ringR, rad1, rad2, segs);
                drawList.PathStroke(
                    ColorToUint(outlineColor),
                    ImDrawFlags.None,
                    thick + 2f * outlineThickness
                );
            }

            for (int i = 0; i < n; i++)
            {
                var styled = details.ActiveInterestMods[i];
                float a2 = 90f + spanDeg / 2f - i * step;
                float a1 = a2 - arcDeg;
                float rad1 = a1 * pi / 180f;
                float rad2 = a2 * pi / 180f;
                int segs = System.Math.Max(6, (int)((rad2 - rad1) / (2f * pi) * 48f));

                drawList.PathArcTo(center, ringR, rad1, rad2, segs);
                drawList.PathStroke(ColorToUint(styled.Color), ImDrawFlags.None, thick);
            }
        }

        private void DrawEightModTriangle(RectangleF rect, ItemDetails itemDetails)
        {
            if (!Settings.ShowEightModTriangle || itemDetails.ModCount < 8)
                return;

            var drawList = ImGui.GetBackgroundDrawList();
            var triangleColor =
                itemDetails.ModCount >= 9
                    ? Settings.NineModTriangleColor.Value
                    : Settings.EightModTriangleColor.Value;
            var color = ColorToUint(SharpToNu(triangleColor.ToVector4()));

            float size = System.Math.Min(rect.Width, rect.Height) * 0.35f;

            nuVector2 corner,
                alongHorizontal,
                alongVertical;

            switch (Settings.EightModTriangleCorner.Value)
            {
                case "Top Left":
                    corner = rect.TopLeft.ToVector2Num();
                    alongHorizontal = new nuVector2(size, 0);
                    alongVertical = new nuVector2(0, size);
                    break;
                case "Bottom Right":
                    corner = rect.BottomRight.ToVector2Num();
                    alongHorizontal = new nuVector2(-size, 0);
                    alongVertical = new nuVector2(0, -size);
                    break;
                case "Bottom Left":
                    corner = rect.BottomLeft.ToVector2Num();
                    alongHorizontal = new nuVector2(size, 0);
                    alongVertical = new nuVector2(0, -size);
                    break;
                default: // "Top Right"
                    corner = rect.TopRight.ToVector2Num();
                    alongHorizontal = new nuVector2(-size, 0);
                    alongVertical = new nuVector2(0, size);
                    break;
            }

            drawList.AddTriangleFilled(
                corner,
                corner + alongHorizontal,
                corner + alongVertical,
                color
            );
        }

        /// <summary>
        /// Draws colored highlights (borders or filled rects) around items in inventory or stash tabs.
        /// </summary>
        private void DrawMapBorders(
            NormalInventoryItem item,
            Entity entity,
            RectangleF? rectOverride = null
        )
        {
            var rect = rectOverride ?? item.GetClientRect();
            double fillPercent = Settings.AreaFill; // 0 = nothing, 100 = full item area
            double shrinkPerSide = (100.0 - fillPercent) / 200.0;
            var deflateWidth = (int)(rect.Width * shrinkPerSide);
            var deflateHeight = (int)(rect.Height * shrinkPerSide);
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
                    var brickRect = rect;
                    float sizeMult = Settings.SimpleOutlinesBrickedSize.Value / 100f;
                    float inflateX = -(rect.Width * (1f - sizeMult) / 2f);
                    float inflateY = -(rect.Height * (1f - sizeMult) / 2f);
                    brickRect.Inflate(inflateX, inflateY);
                    Graphics.DrawLine(
                        brickRect.TopLeft.ToVector2Num(),
                        brickRect.BottomRight.ToVector2Num(),
                        Settings.BorderThicknessMap.Value,
                        Settings.Bricked.Value
                    );
                    Graphics.DrawLine(
                        brickRect.TopRight.ToVector2Num(),
                        brickRect.BottomLeft.ToVector2Num(),
                        Settings.BorderThicknessMap.Value,
                        Settings.Bricked.Value
                    );
                }
                else
                {
                    Graphics.DrawFrame(rect, Settings.Bricked.Value, Settings.BorderThicknessMap);
                }
            }

            // Mapping logic to match UI labels:
            var hasBadMod =
                Settings.BoxForMapWarnings
                && (itemDetails.ActiveBadMods.Count > 0 || itemDetails.ConflictingMods.Count > 0);
            var hasGoodMod =
                Settings.BoxForMapBadWarnings
                && (itemDetails.ActiveGoodMods.Count > 0 || itemDetails.ConflictingMods.Count > 0);

            if (Settings.UseSimpleOutlines)
            {
                DrawSimpleOutlines(rect, hasGoodMod, hasBadMod);
                DrawInterestArcs(rect, itemDetails);
                DrawEightModTriangle(rect, itemDetails);
                return; // Exit after drawing advanced outlines
            }

            if (Settings.UseCircleHighlight.Value)
                DrawCircleHighlight(rect, hasGoodMod, hasBadMod);
            else
                DrawBoxHighlight(rect, hasGoodMod, hasBadMod);

            DrawInterestArcs(rect, itemDetails);
            DrawEightModTriangle(rect, itemDetails);
        }

        /// <summary>
        /// Draws solid filled circle(s) for Good/Bad mod highlights.
        /// For both mods: X-split circle using 4 arc sectors, no blending.
        /// </summary>
        private void DrawCircleHighlight(RectangleF rect, bool hasGoodMod, bool hasBadMod)
        {
            if (!hasGoodMod && !hasBadMod)
                return;

            var drawList = ImGui.GetBackgroundDrawList();
            var center = new nuVector2(
                (rect.TopLeft.X + rect.BottomRight.X) * 0.5f,
                (rect.TopLeft.Y + rect.BottomRight.Y) * 0.5f
            );
            float radius =
                Settings.CircleHighlightRadius.Value
                / 100f
                * System.Math.Min(rect.Width, rect.Height)
                * 0.5f;
            const int segs = 32;

            if (!hasBadMod)
            {
                // Good only — solid circle
                uint goodCol = ColorToUint(SharpToNu(Settings.MapBorderGood.Value.ToVector4()));
                drawList.AddCircleFilled(center, radius, goodCol, segs);
                return;
            }
            if (!hasGoodMod)
            {
                // Bad only — solid circle
                uint badCol = ColorToUint(SharpToNu(Settings.MapBorderBad.Value.ToVector4()));
                drawList.AddCircleFilled(center, radius, badCol, segs);
                return;
            }

            // Both — X-split circle: 4 arc sectors radiating from center.
            // Sector boundary angles at 45°/135°/225°/315° (matching the rectangular X-split).
            uint bad = ColorToUint(SharpToNu(Settings.MapBorderBad.Value.ToVector4()));
            uint good = ColorToUint(SharpToNu(Settings.MapBorderGood.Value.ToVector4()));

            bool goodOnTop = Settings.GoodModXPosition.Value == "Top/Bottom";
            uint topBotCol = goodOnTop ? good : bad;
            uint sideCol = goodOnTop ? bad : good;

            float pi = (float)System.Math.PI;
            // Angles (radians): 0=right, π/2=down, in screen-space (Y+ down)
            float a45 = pi * 0.25f; // 45°
            float a135 = pi * 0.75f; // 135°
            float a225 = pi * 1.25f; // 225°
            float a315 = pi * 1.75f; // 315°

            // Top sector  225°→315° — topBotCol
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, a225, a315, segs / 4);
            drawList.PathFillConvex(topBotCol);

            // Right sector 315°→405° (315°→45° wrapping) — sideCol
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, a315, a45 + 2 * pi, segs / 4);
            drawList.PathFillConvex(sideCol);

            // Bottom sector 45°→135° — topBotCol
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, a45, a135, segs / 4);
            drawList.PathFillConvex(topBotCol);

            // Left sector 135°→225° — sideCol
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, a135, a225, segs / 4);
            drawList.PathFillConvex(sideCol);
        }

        /// <summary>
        /// Draws filled box/X-split highlights for Good/Bad mods (original behaviour).
        /// </summary>
        private void DrawBoxHighlight(RectangleF rect, bool hasGoodMod, bool hasBadMod)
        {
            if (hasGoodMod && hasBadMod)
            {
                // Both good and bad mods: draw four solid triangles forming an X split.
                // No blending — each triangle uses a single uniform color.
                var drawList = ImGui.GetBackgroundDrawList();
                var tl = rect.TopLeft.ToVector2Num();
                var tr = rect.TopRight.ToVector2Num();
                var bl = rect.BottomLeft.ToVector2Num();
                var br = rect.BottomRight.ToVector2Num();
                var center = new nuVector2((tl.X + br.X) * 0.5f, (tl.Y + br.Y) * 0.5f);

                uint badCol = ColorToUint(SharpToNu(Settings.MapBorderBad.Value.ToVector4()));
                uint goodCol = ColorToUint(SharpToNu(Settings.MapBorderGood.Value.ToVector4()));

                bool goodOnTop = Settings.GoodModXPosition.Value == "Top/Bottom";
                uint topBotCol = goodOnTop ? goodCol : badCol;
                uint sideCol = goodOnTop ? badCol : goodCol;

                // Top triangle    (TL → TR → Center)
                drawList.AddTriangleFilled(tl, tr, center, topBotCol);
                // Right triangle  (TR → BR → Center)
                drawList.AddTriangleFilled(tr, br, center, sideCol);
                // Bottom triangle (BR → BL → Center)
                drawList.AddTriangleFilled(br, bl, center, topBotCol);
                // Left triangle   (BL → TL → Center)
                drawList.AddTriangleFilled(bl, tl, center, sideCol);
            }
            else if (hasBadMod)
                Graphics.DrawBox(rect, Settings.MapBorderBad.Value);
            else if (hasGoodMod)
                Graphics.DrawBox(rect, Settings.MapBorderGood.Value);
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
                        Graphics.DrawLine(
                            rect.TopLeft.ToVector2Num(),
                            rect.TopRight.ToVector2Num(),
                            thickness,
                            color
                        );
                        Graphics.DrawLine(
                            rect.TopLeft.ToVector2Num(),
                            rect.TopLeft.ToVector2Num() + new nuVector2(0, wingHeight),
                            thickness,
                            color
                        );
                        Graphics.DrawLine(
                            rect.TopRight.ToVector2Num(),
                            rect.TopRight.ToVector2Num() + new nuVector2(0, wingHeight),
                            thickness,
                            color
                        );
                    }
                    else // Bottom bracket ⊔
                    {
                        Graphics.DrawLine(
                            rect.BottomLeft.ToVector2Num(),
                            rect.BottomRight.ToVector2Num(),
                            thickness,
                            color
                        );
                        Graphics.DrawLine(
                            rect.BottomLeft.ToVector2Num(),
                            rect.BottomLeft.ToVector2Num() - new nuVector2(0, wingHeight),
                            thickness,
                            color
                        );
                        Graphics.DrawLine(
                            rect.BottomRight.ToVector2Num(),
                            rect.BottomRight.ToVector2Num() - new nuVector2(0, wingHeight),
                            thickness,
                            color
                        );
                    }
                }
                else
                {
                    if (topOrLeft) // Left bracket [
                    {
                        Graphics.DrawLine(
                            rect.TopLeft.ToVector2Num(),
                            rect.BottomLeft.ToVector2Num(),
                            thickness,
                            color
                        );
                        Graphics.DrawLine(
                            rect.TopLeft.ToVector2Num(),
                            rect.TopLeft.ToVector2Num() + new nuVector2(wingWidth, 0),
                            thickness,
                            color
                        );
                        Graphics.DrawLine(
                            rect.BottomLeft.ToVector2Num(),
                            rect.BottomLeft.ToVector2Num() + new nuVector2(wingWidth, 0),
                            thickness,
                            color
                        );
                    }
                    else // Right bracket ]
                    {
                        Graphics.DrawLine(
                            rect.TopRight.ToVector2Num(),
                            rect.BottomRight.ToVector2Num(),
                            thickness,
                            color
                        );
                        Graphics.DrawLine(
                            rect.TopRight.ToVector2Num(),
                            rect.TopRight.ToVector2Num() - new nuVector2(wingWidth, 0),
                            thickness,
                            color
                        );
                        Graphics.DrawLine(
                            rect.BottomRight.ToVector2Num(),
                            rect.BottomRight.ToVector2Num() - new nuVector2(wingWidth, 0),
                            thickness,
                            color
                        );
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
