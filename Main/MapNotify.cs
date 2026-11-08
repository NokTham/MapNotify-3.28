using System.Collections.Generic;
using System.IO;
using System;
using System.Linq; // Add this if not already present
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.FilesInMemory;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using nuVector2 = System.Numerics.Vector2;
using System.Text.RegularExpressions;
using nuVector4 = System.Numerics.Vector4;

namespace MapNotify_3_28;

public partial class MapNotify_3_28 : BaseSettingsPlugin<MapNotifySettings> // This remains a partial class
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
    private CachedValue<List<NormalInventoryItem>> _mapDeviceItems;
    private CachedValue<List<NormalInventoryItem>> _merchantItems;
    private CachedValue<List<NormalInventoryItem>> _purchaseWindowItems;
    private CachedValue<List<NormalInventoryItem>> _heistLockerItems;
    private CachedValue<List<NormalInventoryItem>> _expeditionLockerItems;
    private CachedValue<List<NormalInventoryItem>> _mapDeviceStorageItems;

    private bool _showPreviewWindow;
    private string _modFilter = string.Empty;
    private List<CapturedMod> _capturedMods = new List<CapturedMod>();
    private List<string> _availableProfiles = new List<string>();
    private string _newProfileName = string.Empty;
    private bool _forceCapturedTab;
    private List<ModEntry> _modEntries = new List<ModEntry>(); // Store grouped mod entries for browser
    private bool _modEntriesLoadedFromMemory;
    private string _lastCharacterName = string.Empty;

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
        _mapDeviceStorageItems = new TimeCache<List<NormalInventoryItem>>(
            GetMapDeviceStorageItems,
            Settings.InventoryCacheInterval.Value
        );
        LoadModsDatabase();
        InitializeAtlasHighlighter(); // Call the new method to initialize Atlas-related caches
        return true;
    }

    private void LoadModsDatabase()
    {
        if (LoadModsFromMemory())
            return;

        LoadModsFromJson();
    }

    public bool LoadModsFromMemory()
    {
        try
        {
            var modsTable = GameController?.Files?.Mods;
            var records = modsTable?.records;
            if (records == null || records.Count == 0)
            {
                LogMessage("MapNotify: GameController.Files.Mods is not loaded yet or empty. Will fall back to mods.json.", 5);
                return false;
            }

            var mapStatDesc = GameController.Files?.MapStatDescriptions;
            var statDesc = GameController.Files?.StatDescriptions;

            bool IsYieldModKey(string key)
            {
                if (string.IsNullOrEmpty(key)) return false;
                return key.StartsWith("MapItemDropChance", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapItemQuantity", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapItemRarity", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapPackSize", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapMonsterPackSize", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapScarabDropChance", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapCurrencyDropChance", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapMapDropChance", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapTotalItemQuantity", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapTotalItemRarity", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapTotalPackSize", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapTierDropChance", StringComparison.OrdinalIgnoreCase) ||
                       key.StartsWith("MapOriginator", StringComparison.OrdinalIgnoreCase);
            }

            bool IsYieldStatLine(string line)
            {
                if (string.IsNullOrWhiteSpace(line)) return false;
                if (line.Contains("quantity of items", StringComparison.OrdinalIgnoreCase)) return true;
                if (line.Contains("rarity of items", StringComparison.OrdinalIgnoreCase)) return true;
                if (line.Contains("pack size", StringComparison.OrdinalIgnoreCase)) return true;
                if (Regex.IsMatch(line, @"\bmore\s+(scarabs?|currency(\s+items?)?|maps?)\b", RegexOptions.IgnoreCase)) return true;
                if (Regex.IsMatch(line, @"\b(increased|more)\s+(map|scarab|currency)\s+drop", RegexOptions.IgnoreCase)) return true;
                return false;
            }

            // Load known mappings from mods.json if available
            var knownLookup = new Dictionary<string, (string GroupKey, List<string> Descriptions, string Type)>(StringComparer.OrdinalIgnoreCase);
            var jsonPath = Path.Combine(DirectoryFullName, "data", "mods.json");
            if (!File.Exists(jsonPath))
            {
                var srcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Source", "MapNotify-3.28", "data", "mods.json");
                if (File.Exists(srcPath)) jsonPath = srcPath;
            }

            if (File.Exists(jsonPath))
            {
                try
                {
                    var json = File.ReadAllText(jsonPath);
                    var database = Newtonsoft.Json.JsonConvert.DeserializeObject<ModsJson>(json);
                    if (database?.groups != null)
                    {
                        foreach (var kvp in database.groups)
                        {
                            if (IsYieldStatLine(kvp.Key) || IsYieldModKey(kvp.Key)) continue;
                            if (kvp.Value.BaseMods != null)
                            {
                                var cleanDescs = (kvp.Value.Descriptions ?? new List<string>()).Where(d => !IsYieldStatLine(d)).ToList();
                                foreach (var bm in kvp.Value.BaseMods)
                                {
                                    if (string.IsNullOrWhiteSpace(bm) || IsYieldModKey(bm)) continue;
                                    var norm = BaseModExtractor.GetBaseMod(bm);
                                    if (!knownLookup.ContainsKey(norm))
                                        knownLookup[norm] = (kvp.Key, cleanDescs, kvp.Value.Type);
                                    if (!knownLookup.ContainsKey(bm))
                                        knownLookup[bm] = (kvp.Key, cleanDescs, kvp.Value.Type);
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Filter strictly for true rollable map mods
            var relevantMods = records.Values
                .Where(m => m != null && !string.IsNullOrEmpty(m.Key))
                .Where(m =>
                {
                    if (IsYieldModKey(m.Key)) return false;

                    // Blacklist check
                    if (Constants.ModNameBlacklist.Any(black => m.Key.Contains(black, StringComparison.OrdinalIgnoreCase)))
                        return false;

                    // 1. Strict AffixType filter - only true rollable affixes
                    bool isRollableAffix = m.AffixType == ModType.Prefix ||
                                           m.AffixType == ModType.Suffix ||
                                           m.AffixType == ModType.Unique ||
                                           m.AffixType == ModType.ExpeditionLogbook ||
                                           m.AffixType == ModType.Enchantment;
                    if (!isRollableAffix) return false;

                    // 2. Strict Domain / Family filter
                    var normKey = BaseModExtractor.GetBaseMod(m.Key);
                    bool isKnown = knownLookup.ContainsKey(m.Key) || knownLookup.ContainsKey(normKey);
                    bool isMapWorlds = m.Key.Contains("Worlds", StringComparison.OrdinalIgnoreCase);
                    bool isUber = m.Key.Contains("Uber", StringComparison.OrdinalIgnoreCase) || m.Domain == ModDomain.UberMap;
                    bool isExpedition = m.Key.Contains("Expedition", StringComparison.OrdinalIgnoreCase) ||
                                        m.Key.Contains("Logbook", StringComparison.OrdinalIgnoreCase) ||
                                        m.Domain == ModDomain.ExpeditionRelic ||
                                        m.AffixType == ModType.ExpeditionLogbook;
                    bool isValdo = m.Key.Contains("Valdo", StringComparison.OrdinalIgnoreCase) ||
                                   m.Key.Contains("Puzzle", StringComparison.OrdinalIgnoreCase) ||
                                   (m.AffixType == ModType.Unique && m.Key.StartsWith("Map", StringComparison.OrdinalIgnoreCase));

                    if (!isKnown && !isMapWorlds && !isUber && !isExpedition && !isValdo)
                        return false;

                    // Exclude non-map domains unless Valdo or known
                    if (m.Domain == ModDomain.Flask || m.Domain == ModDomain.Monster || m.Domain == ModDomain.Chest)
                        return false;

                    return true;
                })
                .ToList();

            if (relevantMods.Count == 0)
            {
                LogMessage("MapNotify: No relevant map mods found in memory records. Will fall back to mods.json.", 5);
                return false;
            }

            string DetermineModType(ModsDat.ModRecord m)
            {
                if (m.Key.Contains("Expedition", StringComparison.OrdinalIgnoreCase) ||
                    m.Key.Contains("Logbook", StringComparison.OrdinalIgnoreCase) ||
                    m.Domain == ModDomain.ExpeditionRelic ||
                    m.AffixType == ModType.ExpeditionLogbook)
                    return "Expedition";

                if (m.Key.Contains("Uber", StringComparison.OrdinalIgnoreCase) ||
                    m.Domain == ModDomain.UberMap)
                    return "Uber";

                if (m.Key.Contains("Valdo", StringComparison.OrdinalIgnoreCase) ||
                    m.Key.Contains("Puzzle", StringComparison.OrdinalIgnoreCase) ||
                    m.AffixType == ModType.Unique)
                    return "Valdo";

                return "Generic";
            }

            string DetermineAffix(ModsDat.ModRecord m)
            {
                if (m.AffixType == ModType.Prefix) return "Prefix";
                if (m.AffixType == ModType.Suffix) return "Suffix";
                if (m.AffixType == ModType.Unique) return "Unique";
                if (m.AffixType == ModType.ExpeditionLogbook) return "Logbook";
                return m.AffixType.ToString();
            }

            bool IsCleanTranslation(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return false;
                if (text.Contains("<unknown", StringComparison.OrdinalIgnoreCase)) return false;
                if (text.Contains("CachedStatDescription", StringComparison.OrdinalIgnoreCase)) return false;
                if (text.Contains("System.Collections.Generic", StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }

            List<string> TranslateFromMemory(ModsDat.ModRecord mod)
            {
                var descs = new List<string>();
                if (mod.StatNames == null || mod.StatNames.Length == 0)
                    return descs;

                try
                {
                    var statDict = new Dictionary<GameStat, int>();
                    for (int i = 0; i < mod.StatNames.Length; i++)
                    {
                        var statRec = mod.StatNames[i];
                        if (statRec == null) continue;
                        var stat = statRec.MatchingStat;
                        int val = 1;
                        if (mod.StatRange != null && i < mod.StatRange.Length)
                        {
                            var r = mod.StatRange[i];
                            val = r.Min != 0 ? r.Min : (r.Max != 0 ? r.Max : 1);
                        }
                        statDict[stat] = val;
                    }

                    string translated = null;
                    if (mapStatDesc != null)
                    {
                        try { translated = mapStatDesc.TranslateMod(statDict); } catch { }
                    }
                    if ((!IsCleanTranslation(translated)) && statDesc != null)
                    {
                        try { translated = statDesc.TranslateMod(statDict); } catch { }
                    }

                    if (IsCleanTranslation(translated))
                    {
                        var lines = translated.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(l => l.Trim())
                                              .Where(l => IsCleanTranslation(l) && !IsYieldStatLine(l));
                        descs.AddRange(lines);
                    }
                }
                catch { }

                return descs;
            }

            // Group relevant mods by resolved group, type, and affix
            var groupedEntries = new Dictionary<string, (string GroupKey, List<string> Descriptions, string Type, string Affix, HashSet<string> BaseMods)>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in relevantMods)
            {
                string groupKey = null;
                List<string> descriptions = null;
                string type = DetermineModType(mod);
                string affix = DetermineAffix(mod);

                // 1. Try matching with known lookup from raw name
                var normKey = BaseModExtractor.GetBaseMod(mod.Key);
                if (knownLookup.TryGetValue(mod.Key, out var known) || knownLookup.TryGetValue(normKey, out known))
                {
                    groupKey = known.GroupKey;
                    descriptions = known.Descriptions;
                    if (!string.IsNullOrEmpty(known.Type))
                        type = known.Type;
                }

                // 2. If not known, try translating from memory
                if (descriptions == null || descriptions.Count == 0)
                {
                    var memoryDescs = TranslateFromMemory(mod);
                    if (memoryDescs.Count > 0)
                    {
                        descriptions = memoryDescs;
                        groupKey = memoryDescs[0];
                    }
                }

                // 3. If translation not available or was unknown, use human-readable mod name from raw name
                if (descriptions == null || descriptions.Count == 0)
                {
                    var readableName = BaseModExtractor.GetReadableName(mod.Key);
                    descriptions = new List<string> { readableName };
                    groupKey = readableName;
                }

                // Clean descriptions of any yield stat lines
                descriptions = descriptions?.Where(d => !IsYieldStatLine(d)).ToList();

                // If this is purely a yield stat mod, ignore it
                if (IsYieldStatLine(groupKey))
                {
                    if (descriptions != null && descriptions.Count > 0)
                        groupKey = descriptions[0];
                    else
                        continue;
                }

                if (string.IsNullOrWhiteSpace(groupKey) || IsYieldStatLine(groupKey))
                    continue;

                if (descriptions == null || descriptions.Count == 0)
                    descriptions = new List<string> { groupKey };

                // Key for deduplication/grouping
                var dictKey = $"{type}_{affix}_{groupKey}".ToLowerInvariant();
                if (!groupedEntries.TryGetValue(dictKey, out var entry))
                {
                    entry = (groupKey, descriptions, type, affix, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    groupedEntries[dictKey] = entry;
                }

                entry.BaseMods.Add(mod.Key);
            }

            var entries = groupedEntries.Values
                .Where(v => !IsYieldStatLine(v.GroupKey) && v.Descriptions.Count > 0)
                .Select(v => new ModEntry
                {
                    GroupKey = v.GroupKey,
                    Descriptions = v.Descriptions.Where(d => !IsYieldStatLine(d)).ToList(),
                    BaseMods = v.BaseMods.OrderBy(k => k).ToList(),
                    Type = v.Type,
                    Affix = v.Affix
                })
                .Where(e => e.Descriptions.Count > 0)
                .OrderBy(e => e.Type).ThenBy(e => e.Affix).ThenBy(e => e.GroupKey).ToList();

            _modEntries = entries;
            _modEntriesLoadedFromMemory = true;
            LogMessage($"MapNotify: Successfully loaded {_modEntries.Count} mod groups ({relevantMods.Count} raw mods) from game memory.", 5);
            return true;
        }
        catch (Exception ex)
        {
            LogError($"MapNotify: Error fetching mods from memory: {ex.Message}", 10);
            return false;
        }
    }

    public void LoadModsFromJson()
    {
        _modEntriesLoadedFromMemory = false;
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
                        BaseMods = kvp.Value.BaseMods,
                        Type = kvp.Value.Type
                    }).ToList();

                    LogMessage($"MapNotify: Successfully loaded {_modEntries.Count} mods from database at {path}.", 5);
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

    public static string EscapeImGui(string text)
    {
        return text?.Replace("%%", "%").Replace("%", "%%") ?? string.Empty;
    }

    public override Job Tick()
    {
        if (Settings.AutoSwitchProfile.Value)
        {
            var playerName = GameController.Player?.GetComponent<Player>()?.PlayerName;
            if (!string.IsNullOrEmpty(playerName) && playerName != _lastCharacterName)
            {
                _lastCharacterName = playerName;
                
                // Check if a profile folder exists with exactly the character's name
                var profilePath = Path.Combine(ConfigDirectory, "Profiles", playerName);
                var profileToSet = Directory.Exists(profilePath) ? playerName : "Default";

                if (Settings.SelectedProfile.Value != profileToSet)
                {
                    Settings.SelectedProfile.Value = profileToSet;
                    GoodModsDictionary = LoadConfigGoodMod();
                    BadModsDictionary = LoadConfigBadMod();
                    LogMessage($"MapNotify: Auto-switched to {(profileToSet == "Default" ? "Default profile" : $"character profile: {playerName}")}", 5);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Common helper to check a mod against configured Good and Bad mod dictionaries.
    /// </summary>
    private static (StyledText match, bool isGood) MatchMod(string rawName)
    {
        var result = MatchModWithKey(rawName);
        return (result.match, result.isGood);
    }

    private static (string matchedKey, StyledText match, bool isGood) MatchModWithKey(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return (null, null, false);
        var normalizedInput = BaseModExtractor.GetBaseMod(rawName);

        if (GoodModsDictionary.TryGetValue(normalizedInput, out var goodMatch)) return (normalizedInput, goodMatch, true);
        if (BadModsDictionary.TryGetValue(normalizedInput, out var badMatch)) return (normalizedInput, badMatch, false);
        
        return (null, null, false);
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

        if (Settings.ShowMapDeviceStorageHighlights.Value)
            DrawBordersForItems(_mapDeviceStorageItems.Value, "Map Device Storage");
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
                        m.ModRecord.AffixType.ToString().Contains("Enchant") ||
                        m.RawName.Contains("Enchant"))
            .Where(m => !string.IsNullOrEmpty(m.Name))
            .Where(m => !Constants.ModNameBlacklist.Any(black => m.RawName.Contains(black)))
            .GroupBy(m => Regex.Replace(m.RawName, @"\s+", ""), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(m =>
            {
                var type = m.ModRecord.AffixType.ToString();
                if (type.Contains("Enchant") || m.RawName.Contains("Enchant")) return 0;
                if (type.Contains("Implicit")) return 1;
                if (type.Contains("Prefix")) return 2;
                if (type.Contains("Suffix")) return 3;
                if (type.Contains("Unique")) return 4;
                return 5;
            })
            .ToList();

        foreach (var mod in explicitModsFromItem)
        {
            string matchedDescription = null;
            int descIndex = -1;
            string nameToMatch = !string.IsNullOrEmpty(mod.Translation) ? mod.Translation : mod.Name;

            if (!string.IsNullOrEmpty(nameToMatch))
            {
                var cleanModName = nameToMatch.Replace("{", "").Replace("}", "").Replace("%%", "%").Replace("..", ".").Trim();
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

            // Extract per-mod stat contributions
            int modIIQ = 0, modIIR = 0, modPS = 0, modMM = 0, modMS = 0, modMC = 0;
            var stats = mod.ModRecord?.StatNames;
            var values = mod.Values;
            if (stats != null && values != null)
            {
                for (int i = 0; i < stats.Length && i < values.Count; i++)
                {
                    var key = stats[i].Key;
                    if (string.IsNullOrEmpty(key)) continue;
                    var val = values[i];
                    if (key.Contains("quantity", StringComparison.OrdinalIgnoreCase)) modIIQ += val;
                    else if (key.Contains("rarity", StringComparison.OrdinalIgnoreCase)) modIIR += val;
                    else if (key.Contains("pack_size", StringComparison.OrdinalIgnoreCase)) modPS += val;
                    else if (key.Contains("scarab_drop_chance", StringComparison.OrdinalIgnoreCase)) modMS += val;
                    else if (key.Contains("currency_drop_chance", StringComparison.OrdinalIgnoreCase)) modMC += val;
                    else if (key.Contains("map_item_drop_chance", StringComparison.OrdinalIgnoreCase)) modMM += val;
                }
            }

            string affixType = mod.ModRecord.AffixType.ToString();
            if (mod.RawName.Contains("Enchant") || affixType.Contains("Enchant"))
                affixType = "Enchant";

            captured.Add(new CapturedMod
            {
                RawName = mod.RawName.Trim(),
                AffixType = affixType,
                DisplayName = existingEntry?.Text ?? (matchedDescription ?? mod.Name),
                Description = matchedDescription ?? (!string.IsNullOrEmpty(mod.Translation) && !mod.Translation.Contains("<unknown") ? mod.Translation : BaseModExtractor.GetReadableName(mod.RawName)),
                Color = existingEntry?.Color ?? new nuVector4(1, 1, 1, 1),
                IsBricking = existingEntry?.Bricking ?? false,
                Quantity = modIIQ,
                Rarity = modIIR,
                PackSize = modPS,
                MoreMaps = modMM,
                MoreScarabs = modMS,
                MoreCurrency = modMC
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
                    var cleanDesc = modText.Replace("{", "").Replace("}", "").Trim();

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
