using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using nuVector2 = System.Numerics.Vector2;
using System.Text.RegularExpressions;
using nuVector4 = System.Numerics.Vector4;

namespace MapNotify_3_28;

public partial class MapNotify_3_28 : BaseSettingsPlugin<MapNotifySettings>
{
    private RectangleF windowArea;
    private static GameController gameController;
    private static IngameState ingameState;
    private static MapNotifySettings pluginSettings;
    public static Dictionary<string, StyledText> GoodModsDictionary;
    public static Dictionary<string, StyledText> BadModsDictionary;
    public static HashSet<string> ExpectedNodes = new HashSet<string>();
    public static HashSet<string> ExpectedBonusNodes = new HashSet<string>();
    private CachedValue<List<NormalInventoryItem>> _inventoryItems;
    private CachedValue<(int stashIndex, List<NormalInventoryItem>)> _stashItems;
    private CachedValue<(int stashIndex, List<NormalInventoryItem>)> _mapStashItems;
    private CachedValue<List<NormalInventoryItem>> _merchantItems;
    private CachedValue<List<NormalInventoryItem>> _purchaseWindowItems;
    private CachedValue<List<NormalInventoryItem>> _heistLockerItems;
    private bool _showPreviewWindow;
    private List<CapturedMod> _capturedMods = new List<CapturedMod>();

    public MapNotify_3_28()
    {
        Name = "MapNotify-3.28";
    }

    public new string ConfigDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "MapNotify-3.28");

    public class CapturedMod
    {
        public string RawName;
        public string DisplayName;
        public string Description;
        public nuVector4 Color = new nuVector4(1, 0, 0, 1); // Default Red
        public bool IsBricking;
    }

    private bool ItemIsMap(Entity entity)
    {
        if (entity == null || entity.Address == 0) return false;
        if (entity.HasComponent<ExileCore.PoEMemory.Components.MapKey>()) return true;
        if (entity.HasComponent<HeistContract>() || entity.HasComponent<HeistBlueprint>()) return true;

        var path = entity.Path;
        if (string.IsNullOrEmpty(path)) return false;

        return path.StartsWith("Metadata/Items/Maps/MavenMap", StringComparison.Ordinal) ||
               path.StartsWith("Metadata/Items/Heist/HeistContract", StringComparison.Ordinal) ||
               path.StartsWith("Metadata/Items/Heist/HeistBlueprint", StringComparison.Ordinal) ||
               path.Contains("Maven");
    }

    private List<NormalInventoryItem> GetInventoryItems()
    {
        var result = new List<NormalInventoryItem>();
        if (ingameState?.IngameUi?.InventoryPanel?.IsVisible == true)
        {
            var playerInv = ingameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
            var visible = playerInv?.VisibleInventoryItems;
            if (visible != null && visible.Count > 0)
            {
                foreach (var it in visible)
                {
                    if (it?.Item != null && ItemIsMap(it.Item))
                        result.Add(it);
                }
            }
        }
        return result;
    }

    private void FindMapsInElementRecursive(Element element, List<NormalInventoryItem> result, HashSet<long> seenAddresses, int depth)
    {
        if (element == null || !element.IsVisible || depth > 20) return;

        // ExileAPI's NormalInventoryItem check
        // Sometimes elements are both an Element and a NormalInventoryItem
        var item = element.AsObject<NormalInventoryItem>();
        if (item?.Item != null && item.Address != 0)
        {
            if (ItemIsMap(item.Item) && seenAddresses.Add(item.Address))
            {
                result.Add(item); // Add only if not already seen
            }
        }

        // If this wasn't an item, check all of its children (Recursion)
        int childCount = (int)element.ChildCount;
        if (childCount > 0 && childCount < 1000)
        {
            for (int i = 0; i < childCount; i++)
            {
                var child = element.GetChildAtIndex(i);
                if (child != null) FindMapsInElementRecursive(child, result, seenAddresses, depth + 1);
            }
        }
    }

    private (int stashIndex, List<NormalInventoryItem>) GetRegularStashItems()
    {
        var result = new List<NormalInventoryItem>();
        var stashElement = ingameState?.IngameUi?.StashElement;

        if (stashElement?.IsVisible == true && stashElement.VisibleStash != null &&
            stashElement.VisibleStash.InvType != InventoryType.MapStash && Settings.FilterStash.Value)
        {
            FindMapsInElement(stashElement, result);
        }

        int index = stashElement != null ? (int)stashElement.IndexVisibleStash : -1;
        return (index, result);
    }

    private (int stashIndex, List<NormalInventoryItem>) GetMapStashItems()
    {
        var result = new List<NormalInventoryItem>();
        var stashElement = ingameState?.IngameUi?.StashElement;

        if (stashElement?.IsVisible == true && stashElement.VisibleStash != null &&
            stashElement.VisibleStash.InvType == InventoryType.MapStash && Settings.FilterMapStash.Value)
        {
            // Jump closer to the items container using indices provided: 2->0->0->1->1->2->0->4
            var mapContainer = stashElement.GetChildAtIndex(2)?.GetChildAtIndex(0)?.GetChildAtIndex(0)?
                                            .GetChildAtIndex(1)?.GetChildAtIndex(1)?.GetChildAtIndex(2)?
                                            .GetChildAtIndex(0)?.GetChildAtIndex(4);
            FindMapsInElement(mapContainer ?? stashElement, result);
        }

        int index = stashElement != null ? (int)stashElement.IndexVisibleStash : -1;
        return (index, result);
    }

    private void FindMapsInElement(Element element, List<NormalInventoryItem> result)
    { // This overload is called by GetRegularStashItems and GetMapStashItems.
        // It needs to create its own HashSet for uniqueness.
        var seenAddresses = new HashSet<long>();
        FindMapsInElementRecursive(element, result, seenAddresses, 0);
    }

    private List<NormalInventoryItem> GetMerchantItems()
    {
        var result = new List<NormalInventoryItem>();
        var seenAddresses = new HashSet<long>();
        var merchantPanel = ingameState?.IngameUi?.OfflineMerchantPanel;
        if (merchantPanel != null && merchantPanel.IsVisible)
        {
            // Use VisibleStash here as well, as OfflineMerchantPanel inherits from StashElement
            var visibleInv = merchantPanel.VisibleStash?.VisibleInventoryItems;
            if (visibleInv != null && visibleInv.Count > 0)
            {
                foreach (var it in visibleInv)
                {
                    if (it?.Item != null && ItemIsMap(it.Item) && seenAddresses.Add(it.Item.Address))
                        result.Add(it);
                }
            }
        }
        return result;
    }

    private List<NormalInventoryItem> GetHeistLockerItems()
    {
        var result = new List<NormalInventoryItem>();
        var ui = ingameState?.IngameUi;
        if (ui == null || ui.ChildCount <= 98) return result;

        var heistLocker = ui.GetChildAtIndex(98);
        if (heistLocker?.IsVisible == true && Settings.FilterStash.Value)
        {
            var seenAddresses = new HashSet<long>();
            // Iterate through category containers (indices 7 to 24) to find items in all tabs
            for (int i = 7; i <= 24; i++)
            {
                var container = heistLocker.GetChildAtIndex(i);
                if (container != null && container.IsVisible)
                {
                    FindMapsInElementRecursive(container, result, seenAddresses, 0);
                }
            }
        }
        return result;
    }

    private List<NormalInventoryItem> GetPurchaseWindowItems()
    {
        var ui = ingameState?.IngameUi;
        if (ui == null)
            return new List<NormalInventoryItem>();
        ExileCore.PoEMemory.Element window = null;
        if (ui.PurchaseWindow?.IsVisible == true)
            window = ui.PurchaseWindow;
        else if (ui.PurchaseWindowHideout?.IsVisible == true)
            window = ui.PurchaseWindowHideout;
        else if (ui.HaggleWindow?.IsVisible == true)
            window = ui.HaggleWindow;

        var tradeWindow = ui.ChildCount > 108 ? ui.GetChildAtIndex(108) : null;
        if (window == null && tradeWindow?.IsVisible == true)
            window = tradeWindow;

        if (window == null)
            return new List<NormalInventoryItem>();

        var result = new List<NormalInventoryItem>();
        var seenAddresses = new HashSet<long>(); // New HashSet for uniqueness
        bool isTradeWindow = (window == tradeWindow); // Determine if it's the TradeWindow

        if (isTradeWindow)
        {
            // Safe chain prevents log spam when UI indices aren't fully loaded
            var tradeRoot = window.GetChildAtIndex(3)?.GetChildAtIndex(1)?.GetChildAtIndex(0)?.GetChildAtIndex(0);

            // Target the side containers to scan all items in the trade
            var otherSide = tradeRoot?.GetChildAtIndex(1);
            if (otherSide != null) FindMapsInElementRecursive(otherSide, result, seenAddresses, 0);

            var selfSide = tradeRoot?.GetChildAtIndex(0);
            if (selfSide != null) FindMapsInElementRecursive(selfSide, result, seenAddresses, 0);
        }
        else // PurchaseWindow, PurchaseWindowHideout, HaggleWindow
        {
            var currentTabContainer = window.GetChildAtIndex(8)?.GetChildAtIndex(1);
            if (currentTabContainer != null)
            {
                foreach (var tab in currentTabContainer.Children)
                {
                    if (tab.IsVisible)
                    {   // Assuming tab.GetChildAtIndex(0) is the inventory grid
                        FindMapsInElementRecursive(tab.GetChildAtIndex(0), result, seenAddresses, 0);
                    }
                }
            }
        }
        return result;
    }

    public override bool Initialise()
    {
        base.Initialise();
        windowArea = GameController.Window.GetWindowRectangle();
        GoodModsDictionary = LoadConfigGoodMod();
        BadModsDictionary = LoadConfigBadMod();
        ExpectedNodes = LoadExpectedNodes();
        ExpectedBonusNodes = LoadExpectedBonusNodes();
        gameController = GameController;
        ingameState = gameController.IngameState;
        pluginSettings = Settings;
        _inventoryItems = new TimeCache<List<NormalInventoryItem>>(
            GetInventoryItems,
            Settings.InventoryCacheInterval.Value
        );
        _stashItems = new TimeCache<(int stashIndex, List<NormalInventoryItem>)>(
            GetRegularStashItems,
            Settings.StashCacheInterval.Value
        );
        _mapStashItems = new TimeCache<(int stashIndex, List<NormalInventoryItem>)>(
            GetMapStashItems,
            Settings.MapStashCacheInterval.Value
        );
        _merchantItems = new TimeCache<List<NormalInventoryItem>>(
            GetMerchantItems,
            Settings.InventoryCacheInterval.Value
        );
        _purchaseWindowItems = new TimeCache<List<NormalInventoryItem>>(
            GetPurchaseWindowItems,
            Settings.InventoryCacheInterval.Value
        );
        _heistLockerItems = new TimeCache<List<NormalInventoryItem>>(
            GetHeistLockerItems,
            Settings.StashCacheInterval.Value
        );
        InitializeAtlasHighlighter(); // Call the new method to initialize Atlas-related caches
        return true;
    }

    public static nuVector2 boxSize;
    public static float maxSize;
    public static float rowSize;
    public static int lastCol;

    public void RenderItem(
        NormalInventoryItem Item,
        Entity Entity,
        bool isInventory = false,
        int mapNum = 0
    )
    {
        var pushedColors = 0;
        var entity = Entity;
        if (entity != null && entity.Address != 0 && entity.IsValid)
        {
            // Evaluate
            var ItemDetails = Entity.GetHudComponent<ItemDetails>();
            if (ItemDetails == null)
            {
                ItemDetails = new ItemDetails(Item, Entity);
                Entity.SetHudComponent(ItemDetails);
            }

            // Fail-safe: if ItemDetails couldn't be created or retrieved, skip rendering this frame
            if (ItemDetails == null) return;

            var alwaysShow = Settings.AlwaysShowTooltip.Value;
            if (alwaysShow || ItemDetails.ActiveGoodMods.Count > 0 || ItemDetails.ActiveBadMods.Count > 0)
            {
                // Get mouse position
                var mousePos = MouseLite.GetCursorPositionVector();
                var boxOrigin = new nuVector2(mousePos.X + 25, mousePos.Y);

                // Pad horizontally if using big cursor and the cursor overlaps the tooltip
                if (Settings.PadForBigCursor.Value && ItemDetails.NeedsPadding)
                    boxOrigin = new nuVector2(mousePos.X + 45, mousePos.Y + 35);

                // Pad vertically as well if using ninja pricer tooltip
                if (Settings.PadForNinjaPricer.Value && ItemDetails.NeedsPadding)
                    boxOrigin = new nuVector2(mousePos.X + 25, mousePos.Y + 56);

                // Pad vertically as well if using ninja pricer tooltip 2nd padding
                if (Settings.PadForNinjaPricer2.Value && ItemDetails.NeedsPadding)
                    boxOrigin = new nuVector2(mousePos.X + 25, mousePos.Y + 114);

                // Personal pricer
                if (Settings.PadForAltPricer.Value && ItemDetails.NeedsPadding)
                    boxOrigin = new nuVector2(mousePos.X + 25, mousePos.Y + 30);

                // Use cached window ID to avoid per-frame string allocations
                var windowId = ItemDetails.WindowID;

                // Parsing inventory, don't use boxOrigin
                if (isInventory)
                {
                    // wrap on fourth
                    if (mapNum < lastCol) //((float)mapNum % (float)4 == (float)0)
                    {
                        boxSize = new nuVector2(0, 0);
                        rowSize += maxSize + 2;
                        maxSize = 0;
                    }
                    var framePos = ingameState.UIHover.Parent.GetClientRect().TopRight;
                    framePos.X += 10 + boxSize.X;
                    framePos.Y -= 200;
                    boxOrigin = new nuVector2(framePos.X, framePos.Y + rowSize);
                }
                // create the imgui faux tooltip
                var _opened = true;
                // Color background
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

                    // Optimization: Check path once. 
                    // Ideally, move this 'isFragment' check into the ItemDetails class constructor to cache it.
                    bool isFragment = ItemDetails.IsFragment;

                    if (!isFragment && (isInventory || Settings.ShowMapName.Value))
                    {
                        ImGui.TextColored(ItemDetails.ItemColor, $"{ItemDetails.MapName}");
                    }

                    // Quantity and Packsize for maps
                    {
                        var qCol = new nuVector4(1f, 1f, 1f, 1f);
                        if (Settings.ColorQuantityPercent.Value)
                        {
                            if (ItemDetails.Quantity < Settings.ColorQuantity.Value)
                                qCol = new nuVector4(1f, 0.4f, 0.4f, 1f);
                            else
                                qCol = new nuVector4(0.4f, 1f, 0.4f, 1f);
                        }

                        var showQuant = Settings.ShowQuantityPercent.Value;
                        var showPack = Settings.ShowPackSizePercent.Value;
                        var showRarity = Settings.ShowRarityPercent.Value;

                        if (showQuant && ItemDetails.Quantity != 0 && showPack && ItemDetails.PackSize != 0)
                        {
                            ImGui.TextColored(qCol, $"{ItemDetails.Quantity}%% IIQ");
                            ImGui.SameLine();
                            ImGui.TextColored(new nuVector4(1f, 1f, 1f, 1f), $"{ItemDetails.PackSize}%% PS");
                        }
                        else if (showQuant && ItemDetails.Quantity != 0)
                        {
                            ImGui.TextColored(qCol, $"{ItemDetails.Quantity}%% IIQ");
                        }
                        else if (showPack && ItemDetails.PackSize != 0)
                        {
                            ImGui.TextColored(new nuVector4(1f, 1f, 1f, 1f), $"{ItemDetails.PackSize}%% PS");
                        }

                        if (showRarity && ItemDetails.Rarity != 0)
                        {
                            if ((showQuant && ItemDetails.Quantity != 0) || (showPack && ItemDetails.PackSize != 0)) ImGui.SameLine();
                            ImGui.TextColored(new nuVector4(1f, 1f, 1f, 1f), $"{ItemDetails.Rarity}%% IIR");
                        }

                        if (Settings.ShowChisel.Value && !string.IsNullOrEmpty(ItemDetails.ChiselName))
                        {
                            ImGui.TextColored(Settings.ChiselColor, $"+{ItemDetails.ChiselValue}%% {ItemDetails.ChiselName}");
                        }

                        if (Settings.ShowHeistInfo.Value && (ItemDetails.HeistAreaLevel > 0 || ItemDetails.HeistJobLines.Count > 0))
                        {
                            var heistColor = new nuVector4(0.5f, 0.8f, 1f, 1f);
                            if (ItemDetails.HeistAreaLevel > 0)
                                ImGui.TextColored(heistColor, $"Area Level: {ItemDetails.HeistAreaLevel}");

                            foreach (var line in ItemDetails.HeistJobLines)
                            {
                                ImGui.TextColored(line.IsRevealed ? heistColor : new nuVector4(0.5f, 0.8f, 1f, 0.5f), line.Text);
                            }
                        }

                        var showOMaps = Settings.ShowOriginatorMaps.Value;
                        var showOScarabs = Settings.ShowOriginatorScarabs.Value;
                        var showOCurrency = Settings.ShowOriginatorCurrency.Value;

                        if (ItemDetails.IsOriginatorMap && (showOMaps || showOScarabs || showOCurrency))
                        {
                            if (Settings.HorizontalLines.Value)
                                ImGui.Separator();
                            if (showOMaps)
                                ImGui.TextColored(
                                    new nuVector4(0.5f, 0.85f, 1f, 1f),
                                    $"+{ItemDetails.OriginatorMaps}%% Maps"
                                );
                            if (showOScarabs)
                                ImGui.TextColored(
                                    new nuVector4(0.85f, 0.45f, 0.85f, 1f),
                                    $"+{ItemDetails.OriginatorScarabs}%% Scarabs"
                                );
                            if (showOCurrency)
                                ImGui.TextColored(
                                    new nuVector4(0.0f, 1.0f, 0.0f, 1.0f),
                                    $"+{ItemDetails.OriginatorCurrency}%% Currency"
                                );
                        }

                        if (Settings.HorizontalLines.Value)
                            ImGui.Separator();
                    }

                    if (Settings.ShowModCount.Value && ItemDetails.ModCount != 0)
                        if (entity.GetComponent<Base>().isCorrupted)
                            ImGui.TextColored(new nuVector4(1f, 0f, 0f, 1f), $"{(isInventory ? ItemDetails.ModCount - 1 : ItemDetails.ModCount)} Mods, Corrupted"
                            );
                        else
                            ImGui.TextColored(
                                new nuVector4(1f, 1f, 1f, 1f),
                                $"{(isInventory ? ItemDetails.ModCount - 1 : ItemDetails.ModCount)} Mods"
                            );

                    // Mod StyledTexts
                    if (Settings.ShowModWarnings.Value)
                    {
                        if (ItemDetails.ActiveGoodMods.Count > 0)
                            foreach (var StyledText in ItemDetails.ActiveGoodMods)
                                ImGui.TextColored(SharpToNu(StyledText.Color), $"{StyledText.Text}");

                        if (ItemDetails.ActiveGoodMods.Count > 0 && ItemDetails.ActiveBadMods.Count > 0)
                            ImGui.Dummy(new nuVector2(0, 5)); // Adds a 10-pixel vertical space

                        if (ItemDetails.ActiveBadMods.Count > 0)
                            foreach (var StyledText in ItemDetails.ActiveBadMods)
                                ImGui.TextColored(SharpToNu(StyledText.Color), $"{(StyledText.Bricking ? "[B] " : "")}{StyledText.Text}");
                    }
                    ImGui.EndGroup();

                    // border for most notable maps in inventory
                    if (
                        ItemDetails.Bricked
                        || (ItemIsMap(entity) && isInventory)
                    )
                    {
                        var min = ImGui.GetItemRectMin();
                        min.X -= 8;
                        min.Y -= 8;
                        var max = ImGui.GetItemRectMax();
                        max.X += 8;
                        max.Y += 8;

                        if (ItemDetails.Bricked)
                            ImGui
                                .GetForegroundDrawList()
                                .AddRect(
                                    min,
                                    max,
                                    ColorToUint(Settings.Bricked),
                                    0f,
                                    0,
                                    Settings.BorderThickness.Value
                                );
                        else if (isInventory)
                            ImGui.GetForegroundDrawList().AddRect(min, max, 0xFF4A4A4A);
                    }

                    // Detect and adjust for edges
                    var size = ImGui.GetWindowSize();
                    var pos = ImGui.GetWindowPos();
                    if (boxOrigin.X + size.X > windowArea.Width)
                        ImGui.SetWindowPos(
                            new nuVector2(
                                boxOrigin.X - (boxOrigin.X + size.X - windowArea.Width) - 4,
                                boxOrigin.Y + 24
                            ),
                            ImGuiCond.Always
                        );
                    else
                        ImGui.SetWindowPos(boxOrigin, ImGuiCond.Always);

                    // padding when parsing an inventory
                    if (isInventory)
                    {
                        boxSize.X += (int)size.X + 2;
                        if (maxSize < size.Y)
                            maxSize = size.Y;
                        lastCol = mapNum;
                    }
                }
                ImGui.End();
                ImGui.PopStyleColor(pushedColors);
            }
        }
    }

    private void DrawMapBorders(NormalInventoryItem item, Entity entity, RectangleF? rectOverride = null)
    {
        var rect = rectOverride ?? item.GetClientRect();
        double deflatePercent = Settings.BorderDeflation;
        var deflateWidth = (int)(rect.Width * (deflatePercent / 100.0));
        var deflateHeight = (int)(rect.Height * (deflatePercent / 100.0));
        rect.Inflate(-deflateWidth, -deflateHeight);
        var itemDetails = entity.GetHudComponent<ItemDetails>() ?? new ItemDetails(item, entity);
        entity.SetHudComponent(itemDetails);

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
        else if (hasBadMod)
        {
            Graphics.DrawBox(rect, Settings.MapBorderBad.ToSharpColor());
        }
        else if (hasGoodMod)
        {
            Graphics.DrawBox(rect, Settings.MapBorderGood.ToSharpColor());
        }
    }

    public override void Render()
    {
        // Update window area every frame in case the game window was moved or resized
        windowArea = GameController.Window.GetWindowRectangle();
        // Capture Hotkey Logic
        bool ctrlHeld = Input.GetKeyState(System.Windows.Forms.Keys.LControlKey) || Input.GetKeyState(System.Windows.Forms.Keys.RControlKey);
        bool shiftHeld = Input.GetKeyState(System.Windows.Forms.Keys.LShiftKey) || Input.GetKeyState(System.Windows.Forms.Keys.RShiftKey);
        bool altHeld = Input.GetKeyState(System.Windows.Forms.Keys.LMenu) || Input.GetKeyState(System.Windows.Forms.Keys.RMenu);

        bool modifiersMatch = (Settings.UseControl == ctrlHeld) &&
                             (Settings.UseShift == shiftHeld) &&
                             (Settings.UseAlt == altHeld);
        if (modifiersMatch && Settings.CaptureHotkey.PressedOnce())
        {
            // Force reload from files before capturing to ensure we have the latest settings
            GoodModsDictionary = LoadConfigGoodMod();
            BadModsDictionary = LoadConfigBadMod();

            var captureHover = ingameState.UIHover;
            if (captureHover?.IsVisible == true)
            {
                var hoverItem = captureHover.AsObject<NormalInventoryItem>();
                if (hoverItem?.Item != null && ItemIsMap(hoverItem.Item))
                {
                    var mods = hoverItem.Item.GetComponent<Mods>();
                    if (mods != null)
                    {
                        _capturedMods.Clear();
                        var descriptions = GetModDescriptionsFromTooltip(hoverItem.Tooltip);
                        // LogMessage($"Render: GetModDescriptionsFromTooltip returned {descriptions.Count} descriptions for {hoverItem.Item.Path}.", 1);

                        // Filter mods.ItemMods to only include explicit mods that are not blacklisted,
                        // to match the descriptions list from the tooltip.
                        var explicitModsFromItem = new List<ExileCore.PoEMemory.MemoryObjects.ItemMod>();
                        foreach (var mod in mods.ItemMods)
                        {
                            bool blacklisted = ModNameBlacklist.Any(black => mod.RawName.Contains(black));
                            if (!blacklisted)
                            {
                                explicitModsFromItem.Add(mod);
                            }
                        }

                        // Now, iterate through the filtered explicit mods and assign descriptions
                        for (int i = 0; i < explicitModsFromItem.Count; i++)
                        {
                            var mod = explicitModsFromItem[i];
                            var existingEntry = GoodModsDictionary.FirstOrDefault(x => mod.RawName.Contains(x.Key)).Value ??
                                                BadModsDictionary.FirstOrDefault(x => mod.RawName.Contains(x.Key)).Value;

                            _capturedMods.Add(new CapturedMod
                            {
                                RawName = mod.RawName,
                                DisplayName = existingEntry?.Text ?? mod.Name,
                                Description = i < descriptions.Count ? descriptions[i] : null, // Use 'i' directly for descriptions
                                Color = existingEntry != null ? SharpToNu(existingEntry.Color) : new nuVector4(1, 1, 1, 1),
                                IsBricking = existingEntry?.Bricking ?? false
                            });
                        }
                        _showPreviewWindow = true;
                    }
                }
            }
        }

        if (_showPreviewWindow)
        {
            DrawPreviewWindow();
        }
        if (ingameState == null)
            return;

        var ui = ingameState.IngameUi;

        if (Settings.ShowAtlasHighlight || Settings.ShowAtlasBonusHighlight || Settings.ShowMavenWitnessHighlight)
        {
            DrawAtlasHighlights();
        }

        // 1. Player Inventory
        if (Settings.FilterInventory && ui.InventoryPanel.IsVisible)
        {
            foreach (var item in _inventoryItems.Value)
            {
                if (item?.Item == null)
                    continue;
                try
                {
                    DrawMapBorders(item, item.Item);
                }
                catch (System.Exception ex)
                {
                    LogError($"Error drawing player inventory borders: {ex.Message}", 10);
                }
            }
        }

        // 2. Stash
        var stashUI = ui.StashElement;
        if (stashUI.IsVisible && stashUI.VisibleStash != null)
        {
            var isMapStash = stashUI.VisibleStash.InvType == InventoryType.MapStash;
            var cache = isMapStash ? _mapStashItems.Value : _stashItems.Value;
            if (stashUI.IndexVisibleStash == cache.stashIndex)
            {
                foreach (var item in cache.Item2)
                {
                    if (item?.Item == null) continue;
                    try
                    {
                        DrawMapBorders(item, item.Item);
                    }
                    catch (System.Exception ex)
                    {
                        LogError($"Error drawing stash borders: {ex.Message}", 10);
                    }
                }
            }
        }

        // 2.5 Heist Locker
        var heistLocker = ui.ChildCount > 98 ? ui.GetChildAtIndex(98) : null;
        if (Settings.FilterStash.Value && heistLocker?.IsVisible == true)
        {
            foreach (var item in _heistLockerItems.Value)
            {
                if (item?.Item == null) continue;
                try
                {
                    DrawMapBorders(item, item.Item);
                }
                catch (Exception ex)
                {
                    LogError($"Error drawing heist locker borders: {ex.Message}", 10);
                }
            }
        }

        // 3. Kingsmarch / Offline Merchant
        if (Settings.FilterShops && ui.OfflineMerchantPanel.IsVisible)
        {
            foreach (var item in _merchantItems.Value)
            {
                if (item?.Item == null)
                    continue;
                try
                {
                    DrawMapBorders(item, item.Item);
                }
                catch (System.Exception ex)
                {
                    LogError($"Error drawing merchant borders: {ex.Message}", 10);
                }
            }
        }
        // 4. Combined Purchase/Haggle Optimized Block
        bool isShopVisible =
            ui.PurchaseWindow?.IsVisible == true
            || ui.PurchaseWindowHideout?.IsVisible == true
            || ui.HaggleWindow?.IsVisible == true;
        var tradeWindow = ui.ChildCount > 108 ? ui.GetChildAtIndex(108) : null;
        bool isTradeWindowVisible = tradeWindow?.IsVisible == true;

        if (Settings.FilterTrade && (isShopVisible || isTradeWindowVisible))
        {
            // Use the cached value to prevent CPU lag, GetPurchaseWindowItems now handles all these windows.
            var cachedShopItems = _purchaseWindowItems.Value;
            if (cachedShopItems != null)
            {
                foreach (var item in cachedShopItems)
                {
                    if (item?.Item == null || !item.IsVisible)
                        continue;
                    try
                    {
                        DrawMapBorders(item, item.Item);
                    }
                    catch (System.Exception ex)
                    {
                        LogError($"Error drawing shop borders: {ex.Message}", 10);
                    }
                }
            }
        }

        // 4.5. Maven Invitation (Fixed UI Element at 67->8->1)
        if (Settings.ShowForInvitations.Value)
        {
            if (ui != null && ui.Address != 0 && ui.ChildCount > 67)
            {
                var root = ui.GetChildAtIndex(67);
                if (root != null && root.IsVisible && root.ChildCount > 8)
                {
                    var mid = root.GetChildAtIndex(8);
                    var mavenInvitationElement = mid?.ChildCount > 1 ? mid.GetChildAtIndex(1) : null;
                    if (mavenInvitationElement != null && mavenInvitationElement.IsVisible)
                    {
                        var invItem = mavenInvitationElement.AsObject<NormalInventoryItem>();
                        if (invItem?.Item != null)
                        {
                            DrawMapBorders(invItem, invItem.Item, mavenInvitationElement.GetClientRect());
                        }
                    }
                }
            }
        }

        // 5. Hovered Item
        var uiHover = ingameState.UIHover;
        if (uiHover?.IsVisible == true)
        {
            var hoverItem = uiHover.AsObject<NormalInventoryItem>();
            if (hoverItem?.Item != null && ItemIsMap(hoverItem.Item))
                RenderItem(hoverItem, hoverItem.Item);
        }
    }
}
