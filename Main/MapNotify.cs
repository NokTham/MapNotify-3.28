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
    private static readonly Regex TooltipTagsRegex = new Regex(@"<[^>]*>", RegexOptions.Compiled);

    public static readonly string[] ModNameBlacklist =
    {
        "AfflictionMapDeliriumStacks",
        "AfflictionMapReward",
        "InfectedMap",
        "MapForceCorruptedSideArea",
        "MapGainsRandomZanaMod",
        "MapDoesntConsumeSextantCharge",
        "MapEnchant",
        "Enchantment",
        "MapBossSurroundedByTormentedSpirits",
        "MapZanaSubAreaMissionDetails",
        "MapZanaInfluence",
        "IsUberMap",
        "MapConqueror",
        "MapElder",
        "MapVaalTempleContainsVaalVessels",
        "MavenInvitation",
        "MapCorruptionRandomAtlasNotables",
        "MapCorruptionAtlasEffect",
        "MapCorruptionBossCorruption",
        "MapCorruptionSoulGainPrevention"
    };

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
    private CachedValue<List<NormalInventoryItem>> _mapDeviceItems;
    private CachedValue<List<NormalInventoryItem>> _merchantItems;
    private CachedValue<List<NormalInventoryItem>> _purchaseWindowItems;
    private CachedValue<List<NormalInventoryItem>> _heistLockerItems;
    private CachedValue<List<NormalInventoryItem>> _expeditionLockerItems;

    private bool _showPreviewWindow;
    private string _modFilter = string.Empty;
    private List<CapturedMod> _capturedMods = new List<CapturedMod>();

    public MapNotify_3_28()
    {
        Name = "MapNotify-3.28";
    }

    public new string ConfigDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "MapNotify-3.28");

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
        _mapDeviceItems = new TimeCache<List<NormalInventoryItem>>(
            GetMapDeviceItems,
            Settings.InventoryCacheInterval.Value
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
        _expeditionLockerItems = new TimeCache<List<NormalInventoryItem>>(
            GetExpeditionLockerItems,
            Settings.StashCacheInterval.Value
        );
        InitializeAtlasHighlighter(); // Call the new method to initialize Atlas-related caches
        return true;
    }

    public static nuVector2 boxSize;
    public static float maxSize;
    public static float rowSize;
    public static int lastCol;

    public static string EscapeImGui(string text)
    {
        return text?.Replace("%%", "%").Replace("%", "%%") ?? string.Empty;
    }

    /// <summary>
    /// Main rendering entry point called every frame.
    /// Iterates through visible UI panels (Inventory, Stash, Shops) and applies highlights.
    /// </summary>
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
            HandleCaptureHotkey();
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
            // Synchronized detection: Rely on InventoryType. Fallback checks for ChildIndex(3) 
            // often cause conflicts with other specialized stash tabs (Blight, etc.).
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
        // We remove the hardcoded index check for rendering and rely on the cached item's own visibility.
        if (Settings.ShowHeistLockerHighlights.Value)
        {
            try
            {
                foreach (var item in _heistLockerItems.Value)
                {
                    if (item?.Item == null) continue;
                    DrawMapBorders(item, item.Item);
                }
            }
            catch (Exception ex)
            {
                LogError($"Error drawing heist locker borders: {ex.Message}", 10);
            }
        }

        // 2.6 Expedition Locker (Logbooks)
        if (Settings.ShowExpeditionLockerHighlights.Value)
        {
            foreach (var item in _expeditionLockerItems.Value)
            {
                if (item?.Item == null) continue;
                try
                {
                    DrawMapBorders(item, item.Item);
                }
                catch (Exception ex)
                {
                    LogError($"Error drawing expedition locker borders: {ex.Message}", 10);
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

        // Use the specialized TradeWindow property for more robust detection
        var tradeWindow = ui.TradeWindow;
        bool isTradeWindowVisible = tradeWindow != null && tradeWindow.IsVisible;

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

        // 4.5. Map Device & Maven Invitations
        // Using MapReceptacleWindow to replace hardcoded paths (67->8->1)
        if (Settings.ShowForInvitations.Value || Settings.FilterTrade.Value)
        {
            var deviceItems = _mapDeviceItems.Value;
            foreach (var item in deviceItems)
            {
                if (item?.Item == null || !item.IsVisible) continue;

                // Separate logic for invitation slot vs regular map pieces in the device
                var path = item.Item.Path;
                bool isInvitation = path != null && (path.Contains("MavenMap") || path.Contains("Invitations/Maven") || path.Contains("MavenInvitation"));
                
                if (isInvitation && !Settings.ShowForInvitations.Value) continue;
                if (!isInvitation && !Settings.FilterTrade.Value) continue;

                try
                {
                    DrawMapBorders(item, item.Item);
                }
                catch (Exception ex)
                {
                    LogError($"Error drawing map device borders: {ex.Message}", 10);
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

    /// <summary>
    /// Orchestrates the "Capture Mod" logic. 
    /// Extracts raw mod data and tooltip descriptions from the hovered item 
    /// to prepare the Mod Preview window.
    /// </summary>
    private void HandleCaptureHotkey()
    {
        // Force reload from files before capturing to ensure we have the latest settings
        GoodModsDictionary = LoadConfigGoodMod();
        BadModsDictionary = LoadConfigBadMod();

        var captureHover = ingameState.UIHover;
        if (captureHover?.IsVisible != true) return;

        var hoverItem = captureHover.AsObject<NormalInventoryItem>();
        if (hoverItem?.Item == null || !ItemIsMap(hoverItem.Item)) return;

        var mods = hoverItem.Item.GetComponent<Mods>();
        if (mods == null) return;

        _capturedMods.Clear();
        var descriptions = GetModDescriptionsFromTooltip(hoverItem.Tooltip);
        var availableDescriptions = new List<string>(descriptions);

        var explicitModsFromItem = mods.ItemMods
            .Where(m => m.ModRecord != null)
            .Where(m => m.ModRecord.AffixType.ToString().Contains("Prefix") || 
                        m.ModRecord.AffixType.ToString().Contains("Suffix") || 
                        m.ModRecord.AffixType.ToString().Contains("Implicit") || 
                        m.ModRecord.AffixType.ToString().Contains("Unique") || // Include Unique modifiers for Valdo maps
                        m.ModRecord.AffixType.ToString().Contains("Enchant"))
            .Where(m => !string.IsNullOrEmpty(m.Name))
            .Where(m => !ModNameBlacklist.Any(black => m.RawName.Contains(black)))
            .GroupBy(m => Regex.Replace(m.RawName, @"\s+", ""), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(m => {
                var type = m.ModRecord.AffixType.ToString();
                if (type.Contains("Enchant")) return 0;
                if (type.Contains("Implicit")) return 1;
                if (type.Contains("Unique")) return 2; // Order Unique modifiers after Implicit
                if (type.Contains("Prefix")) return 2;
                if (type.Contains("Suffix")) return 3;
                return 4;
            })
            .ToList();

        foreach (var mod in explicitModsFromItem)
        {
            string matchedDescription = null;
            int descIndex = -1;
            string nameToMatch = !string.IsNullOrEmpty(mod.Translation) ? mod.Translation : mod.Name;
            
            if (!string.IsNullOrEmpty(nameToMatch))
            {
                var cleanModName = TooltipTagsRegex.Replace(nameToMatch, "").Replace("{", "").Replace("}", "").Replace("%%", "%").Replace("..", ".").Trim();
                for (int k = 0; k < availableDescriptions.Count; k++)
                {
                    var fragments = cleanModName.Split('#', StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).Where(f => f.Length > 1).ToList();
                    var uiTextNoRanges = Regex.Replace(availableDescriptions[k], @"\(\d+-\d+\)", "").Trim();
                    uiTextNoRanges = Regex.Replace(uiTextNoRanges, @"\s+", " ");

                    if (fragments.Count > 0 && fragments.All(f => uiTextNoRanges.Contains(f, StringComparison.OrdinalIgnoreCase)) ||
                        (fragments.Count == 0 && uiTextNoRanges.Contains(cleanModName, StringComparison.OrdinalIgnoreCase)))
                    {
                        matchedDescription = availableDescriptions[k];
                        descIndex = k;
                        break;
                    }
                }
            }

            if (matchedDescription == null && availableDescriptions.Count > 0)
            {
                matchedDescription = availableDescriptions[0];
                descIndex = 0;
            }

            if (matchedDescription != null && (matchedDescription.Contains("CachedStatDescription") || matchedDescription.Contains("System.Collections.Generic") || matchedDescription.StartsWith("<unknown")))
            {
                if (descIndex != -1) availableDescriptions.RemoveAt(descIndex);
                matchedDescription = null;
                descIndex = -1;
            }

            if (descIndex != -1) availableDescriptions.RemoveAt(descIndex);

            var existingEntry = GoodModsDictionary.FirstOrDefault(x => mod.RawName.Contains(x.Key)).Value ??
                                BadModsDictionary.FirstOrDefault(x => mod.RawName.Contains(x.Key)).Value;

            _capturedMods.Add(new CapturedMod
            {
                RawName = mod.RawName.Trim(),
                AffixType = mod.ModRecord.AffixType.ToString(),
                DisplayName = existingEntry?.Text ?? (matchedDescription ?? mod.Name),
                Description = matchedDescription ?? (!string.IsNullOrEmpty(mod.Translation) ? mod.Translation : mod.Name),
                Color = existingEntry != null ? existingEntry.Color : new nuVector4(1, 1, 1, 1),
                IsBricking = existingEntry?.Bricking ?? false
            });
        }

        // Capture Logbook Implicits from ExpeditionSaga
        var saga = hoverItem.Item.GetComponent<ExpeditionSaga>();
        if (saga != null && saga.Areas != null)
        {
            foreach (var area in saga.Areas)
            {
                if (area?.ImplicitMods == null) continue;
                foreach (var mod in area.ImplicitMods)
                {
                    if (string.IsNullOrEmpty(mod.RawName)) continue;
                    // Avoid duplicates if multiple areas have the same mod type
                    if (_capturedMods.Any(m => m.RawName == mod.RawName)) continue;

                    var existingEntry = GoodModsDictionary.FirstOrDefault(x => mod.RawName.Contains(x.Key)).Value ??
                                        BadModsDictionary.FirstOrDefault(x => mod.RawName.Contains(x.Key)).Value;

                    var modText = !string.IsNullOrEmpty(mod.Translation) ? mod.Translation : mod.Name;
                    var cleanDesc = TooltipTagsRegex.Replace(modText, "").Replace("{", "").Replace("}", "").Trim();

                    _capturedMods.Add(new CapturedMod
                    {
                        RawName = mod.RawName.Trim(),
                        AffixType = "Logbook Implicit",
                        DisplayName = existingEntry?.Text ?? cleanDesc,
                        Description = cleanDesc,
                        Color = existingEntry != null ? existingEntry.Color : new nuVector4(1, 1, 1, 1),
                        IsBricking = existingEntry?.Bricking ?? false
                    });
                }
            }
        }

        _modFilter = string.Empty;
        _showPreviewWindow = true;
    }
}
