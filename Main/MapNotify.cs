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
    // JSON Database helper classes
    public class ModsJson { public Dictionary<string, ModGroup> groups { get; set; } }
    public class ModGroup { public List<string> BaseMods { get; set; } public List<string> Descriptions { get; set; } }
    public class ModEntry { public string GroupKey { get; set; } public List<string> Descriptions { get; set; } public List<string> BaseMods { get; set; } }
    public class ModData { public string BaseMod { get; set; } public string Description { get; set; } }

    private static readonly Regex TooltipTagsRegex = new Regex(@"<[^>]*>", RegexOptions.Compiled);

    public static class UIIndices
    {
        public const int MapDeviceRoot = 67;
        public const int HeistLockerDefault = 98;
        public const int ExpeditionLockerDefault = 101;
        public const int PurchaseWindowTabDetails = 8;
        public const int StashMapTabItems = 3;
    }

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
    private List<string> _availableProfiles = new List<string>();
    private string _newProfileName = string.Empty;
    private List<ModData> _allModsList = new List<ModData>();
    private bool _forceCapturedTab;
    private List<ModEntry> _modEntries = new List<ModEntry>(); // Store grouped mod entries for browser

    public MapNotify_3_28()
    {
        Name = "MapNotify-3.28";
    }

    public new string ConfigDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "MapNotify-3.28");

    public override bool Initialise()
    {
        base.Initialise();
        EnsureProfileStructure();
        RefreshProfileList();
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
        LoadModsDatabase();
        InitializeAtlasHighlighter(); // Call the new method to initialize Atlas-related caches
        return true;
    }

    private void LoadModsDatabase()
    {
        // First, try the standard location relative to the plugin's DLL (for compiled plugins)
        var path = Path.Combine(DirectoryFullName, "data", "mods.json");
        LogMessage($"MapNotify: Attempting to load mods database from standard path: {path}", 5);

        if (!File.Exists(path))
        {
            // If not found, try the source directory location (for development/source plugins)
            var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Source", "MapNotify-3.28", "data", "mods.json");
            LogMessage($"MapNotify: mods.json not found at standard path. Trying source path: {sourcePath}", 5);
            path = sourcePath;
        }

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var database = Newtonsoft.Json.JsonConvert.DeserializeObject<ModsJson>(json);

                if (database?.groups != null)
                {
                    _modEntries = database.groups.Select(kvp => new ModEntry
                    {
                        GroupKey = kvp.Key,
                        Descriptions = kvp.Value.Descriptions ?? new List<string>(),
                        BaseMods = kvp.Value.BaseMods
                    }).ToList();

                    _allModsList = _modEntries
                        .Where(m => m.BaseMods != null)
                        .SelectMany(m => m.BaseMods.Select(bm => new ModData { BaseMod = bm, Description = m.Descriptions.FirstOrDefault() ?? m.GroupKey }))
                        .ToList();

                    LogMessage($"MapNotify: Successfully loaded {_allModsList.Count} mods from database at {path}.", 5);
                }
                else
                {
                    LogError($"MapNotify: mods.json loaded but 'groups' section is null or empty at {path}.", 10);
                }
            }
            catch (Exception ex)
            {
                LogError($"MapNotify: Error parsing mods.json at {path}: {ex.Message}", 10);
            }
        }
        else
        {
            LogError($"MapNotify: mods.json not found at either standard or source path: {path}", 10);
        }
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
    /// Common helper to check a mod against configured Good and Bad mod dictionaries.
    /// </summary>
    private static (StyledText match, bool isGood) MatchMod(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return (null, false);
        var goodMatch = GoodModsDictionary.FirstOrDefault(x => BaseModExtractor.AreEquivalent(rawName, x.Key)).Value;
        if (goodMatch != null) return (goodMatch, true);
        var badMatch = BadModsDictionary.FirstOrDefault(x => BaseModExtractor.AreEquivalent(rawName, x.Key)).Value;
        return (badMatch, false);
    }

    private static bool RemoveModFromFile(string path, string rawName)
    {
        if (!File.Exists(path)) return false;
        var lines = File.ReadAllLines(path).ToList();
        bool removed = lines.RemoveAll(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#") &&
                                            BaseModExtractor.AreEquivalent(rawName, l.Split(';')[0].Trim())) > 0;
        if (removed) File.WriteAllLines(path, lines);
        return removed;
    }

    /// <summary>
    /// Main rendering entry point called every frame.
    /// Iterates through visible UI panels (Inventory, Stash, Shops) and applies highlights.
    /// </summary>
    public override void Render()
    {
        windowArea = GameController.Window.GetWindowRectangle();

        ProcessHotkeys();

        if (_showPreviewWindow) DrawPreviewWindow();
        if (ingameState == null) return;

        if (Settings.ShowAtlasHighlight || Settings.ShowAtlasBonusHighlight || Settings.ShowMavenWitnessHighlight)
            DrawAtlasHighlights();

        var ui = ingameState.IngameUi;

        if (Settings.FilterInventory && ui.InventoryPanel.IsVisible)
            DrawBordersForItems(_inventoryItems.Value, "Inventory");

        RenderStash(ui);
        RenderLockers();
        RenderShopsAndTrade(ui);
        RenderMapDevice();
        RenderHoveredItem();
    }

    private void ProcessHotkeys()
    {
        bool ctrlHeld = Input.GetKeyState(System.Windows.Forms.Keys.LControlKey) || Input.GetKeyState(System.Windows.Forms.Keys.RControlKey);
        bool shiftHeld = Input.GetKeyState(System.Windows.Forms.Keys.LShiftKey) || Input.GetKeyState(System.Windows.Forms.Keys.RShiftKey);
        bool altHeld = Input.GetKeyState(System.Windows.Forms.Keys.LMenu) || Input.GetKeyState(System.Windows.Forms.Keys.RMenu);

        bool modifiersMatch = (Settings.UseControl == ctrlHeld) &&
                             (Settings.UseShift == shiftHeld) &&
                             (Settings.UseAlt == altHeld);

        if (modifiersMatch && Settings.CaptureHotkey.PressedOnce()) HandleCaptureHotkey();
    }

    private void RenderStash(IngameUIElements ui)
    {
        var stashUI = ui.StashElement;
        if (stashUI.IsVisible && stashUI.VisibleStash != null)
        {
            var isMapStash = stashUI.VisibleStash.InvType == InventoryType.MapStash;
            var cache = isMapStash ? _mapStashItems.Value : _stashItems.Value;
            if (stashUI.IndexVisibleStash == cache.stashIndex)
                DrawBordersForItems(cache.Item2, "Stash");
        }
    }

    private void RenderLockers()
    {
        if (Settings.ShowHeistLockerHighlights.Value)
            DrawBordersForItems(_heistLockerItems.Value, "Heist Locker");

        if (Settings.ShowExpeditionLockerHighlights.Value)
            DrawBordersForItems(_expeditionLockerItems.Value, "Expedition Locker");
    }

    private void RenderShopsAndTrade(IngameUIElements ui)
    {
        if (Settings.FilterShops && ui.OfflineMerchantPanel.IsVisible)
            DrawBordersForItems(_merchantItems.Value, "Merchant");

        bool isShopVisible =
            ui.PurchaseWindow?.IsVisible == true
            || ui.PurchaseWindowHideout?.IsVisible == true
            || ui.HaggleWindow?.IsVisible == true;

        var tradeWindow = ui.TradeWindow;
        bool isTradeWindowVisible = tradeWindow != null && tradeWindow.IsVisible;

        if (Settings.FilterTrade && (isShopVisible || isTradeWindowVisible))
            DrawBordersForItems(_purchaseWindowItems.Value, "Shop/Trade");
    }

    private void RenderMapDevice()
    {
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
                DrawBordersForItems(new[] { item }, "Map Device");
            }
        }
    }

    private void RenderHoveredItem()
    {
        var uiHover = ingameState.UIHover;
        if (uiHover?.IsVisible == true)
        {
            var hoverItem = uiHover.AsObject<NormalInventoryItem>();
            if (hoverItem?.Item != null && ItemIsMap(hoverItem.Item))
                RenderItem(hoverItem, hoverItem.Item);
        }
    }

    private void DrawBordersForItems(IEnumerable<NormalInventoryItem> items, string sourceName)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (item?.Item == null || !item.IsVisible) continue;
            try
            {
                DrawMapBorders(item, item.Item);
            }
            catch (Exception ex)
            {
                LogError($"Error drawing {sourceName} borders: {ex.Message}", 10);
            }
        }
    }

    /// <summary>
    /// Orchestrates the "Capture Mod" logic. 
    /// Extracts raw mod data and tooltip descriptions from the hovered item 
    /// to prepare the Mod Preview window.
    /// </summary>
    private void HandleCaptureHotkey()
    {
        if (ingameState == null) return;

        // Force reload from files before capturing to ensure we have the latest settings
        GoodModsDictionary = LoadConfigGoodMod();
        BadModsDictionary = LoadConfigBadMod();
        RefreshProfileList();

        var hoverItem = ingameState.UIHover?.AsObject<NormalInventoryItem>();
        var mods = hoverItem?.Item?.GetComponent<Mods>();

        if (hoverItem?.Item == null || !ItemIsMap(hoverItem.Item) || mods == null)
        {
            _showPreviewWindow = !_showPreviewWindow;
            if (_showPreviewWindow) _capturedMods.Clear();
            return;
        }

        _capturedMods.Clear();
        var descriptions = GetModDescriptionsFromTooltip(hoverItem.Tooltip);
        var availableDescriptions = new List<string>(descriptions);

        _capturedMods = GetCapturableMods(mods, availableDescriptions);

        // Capture Logbook Implicits from ExpeditionSaga
        CaptureLogbookMods(hoverItem.Item);

        _modFilter = string.Empty;
        _showPreviewWindow = true;
        _forceCapturedTab = true;
    }

    private List<CapturedMod> GetCapturableMods(Mods mods, List<string> availableDescriptions)
    {
        var captured = new List<CapturedMod>();

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
            .OrderBy(m =>
            {
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

            var (existingEntry, _) = MatchMod(mod.RawName);

            captured.Add(new CapturedMod
            {
                RawName = mod.RawName.Trim(),
                AffixType = mod.ModRecord.AffixType.ToString(),
                DisplayName = existingEntry?.Text ?? (matchedDescription ?? mod.Name),
                Description = matchedDescription ?? (!string.IsNullOrEmpty(mod.Translation) ? mod.Translation : mod.Name),
                Color = existingEntry?.Color ?? new nuVector4(1, 1, 1, 1),
                IsBricking = existingEntry?.Bricking ?? false
            });
        }
        return captured;
    }

    /// <summary>
    /// Specialized capture for Logbook implicit modifiers which are stored in the ExpeditionSaga component.
    /// </summary>
    private void CaptureLogbookMods(Entity item)
    {
        if (item == null) return;
        var saga = item.GetComponent<ExpeditionSaga>();
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

                    var (existingEntry, _) = MatchMod(mod.RawName);

                    var modText = !string.IsNullOrEmpty(mod.Translation) ? mod.Translation : mod.Name;
                    var cleanDesc = TooltipTagsRegex.Replace(modText, "").Replace("{", "").Replace("}", "").Trim();

                    _capturedMods.Add(new CapturedMod
                    {
                        RawName = mod.RawName.Trim(),
                        AffixType = "Logbook Implicit",
                        DisplayName = existingEntry?.Text ?? cleanDesc,
                        Description = cleanDesc,
                        Color = existingEntry?.Color ?? new nuVector4(1, 1, 1, 1),
                        IsBricking = existingEntry?.Bricking ?? false
                    });
                }
            }
        }
    }
}
