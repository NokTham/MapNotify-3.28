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
    public static Dictionary<string, StyledText> InterestModsDictionary;

    public static readonly string[] InterestPaletteHex = new[]
    {
        "B8B2F5FF",
        "FAC775FF",
        "5DCAFFFF",
        "F472B6FF",
        "F0F0F0FF",
        "F08C5AFF"
    };

    private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
    private static readonly Regex NumericRangeRegex = new Regex(@"\(\d+-\d+\)", RegexOptions.Compiled);

    private static readonly nuVector4[] InterestPalette = InterestPaletteHex
        .Select(h => SharpToNu(HexToSDXVector4(h)))
        .ToArray();

    public static nuVector4 GetNextInterestColor()
    {
        var palette = InterestPalette;
        if (InterestModsDictionary != null && InterestModsDictionary.Count > 0)
        {
            var usedColors = InterestModsDictionary.Values
                .Select(v => ToHex(v.Color).Substring(0, 6))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var hex in InterestPaletteHex)
            {
                if (!usedColors.Contains(hex.Substring(0, 6)))
                    return SharpToNu(HexToSDXVector4(hex));
            }

            return palette[InterestModsDictionary.Count % palette.Length];
        }

        return palette[0];
    }
    public static HashSet<string> ExpectedNodes = new HashSet<string>();
    public static HashSet<string> ExpectedBonusNodes = new HashSet<string>();

    // Bumped when Good/Bad/Interest dictionaries are reloaded so UI caches can invalidate.
    public static int ModDictionaryVersion;

    private int _modEntriesVersion;
    private Element _heistLockerElement;
    private Element _expeditionLockerElement;
    private long _lastHeistLockerProbe;
    private long _lastExpeditionLockerProbe;
    private long _lastWindowAreaUpdate;
    private long _lastLiveSlotRefreshMs;
    private long _liveSlotRefreshUntilMs;
    private bool _leftMouseWasDown;

    private static readonly int[] ExpeditionLockerIndices =
    {
        Constants.UIIndices.ExpeditionLockerDefault, 102, 103, 104
    };
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
        ReloadModDictionaries();
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

            bool IsYieldModKey(string key) => MapYield.IsYieldModName(key);

            bool IsYieldStatLine(string line) => MapYield.IsYieldDescription(line);

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
                    if (Constants.IsModBlacklisted(m.Key))
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
            _modEntriesVersion++;
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
                    _modEntriesVersion++;

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
                    ReloadModDictionaries();
                    LogMessage($"MapNotify: Auto-switched to {(profileToSet == "Default" ? "Default profile" : $"character profile: {playerName}")}", 5);
                }
            }
        }

        return null;
    }

    public static StyledText MatchInterest(string rawName)
    {
        if (string.IsNullOrEmpty(rawName) || InterestModsDictionary == null) return null;
        var normalizedInput = BaseModExtractor.GetBaseMod(rawName);
        if (InterestModsDictionary.TryGetValue(normalizedInput, out var match))
            return match;
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
        var now = Environment.TickCount64;
        if (now - _lastWindowAreaUpdate >= 1000)
        {
            windowArea = GameController.Window.GetWindowRectangle();
            _lastWindowAreaUpdate = now;
        }

        ProcessHotkeys();

        if (_showPreviewWindow) DrawPreviewWindow();
        if (ingameState == null) return;

        if (Settings.ShowAtlasHighlight || Settings.ShowAtlasBonusHighlight || Settings.ShowMavenWitnessHighlight)
            DrawAtlasHighlights();

        var ui = ingameState.IngameUi;

        // Cached slot wrappers keep the position they had when the list was built, so
        // while an item is being dragged they can still point at the old slot. Keep
        // the list live while an item is held and for a short window after a click:
        // the game may update the moved item's slot a frame or two after the drop,
        // so a single release-time refresh can still read the old position.
        const long LiveSlotWindowMs = 500;
        bool leftMouseDown = Input.IsKeyDown(System.Windows.Forms.Keys.LButton);
        bool leftMouseReleased = _leftMouseWasDown && !leftMouseDown;
        _leftMouseWasDown = leftMouseDown;

        bool holdingItem = IsHoldingItem();
        if (holdingItem || leftMouseReleased)
            _liveSlotRefreshUntilMs = now + LiveSlotWindowMs;

        bool refreshLiveSlots = holdingItem || now <= _liveSlotRefreshUntilMs;
        if (leftMouseDown && !refreshLiveSlots && now - _lastLiveSlotRefreshMs >= 100)
        {
            refreshLiveSlots = true;
            _lastLiveSlotRefreshMs = now;
        }

        if (Settings.FilterInventory && ui.InventoryPanel.IsVisible)
        {
            if (refreshLiveSlots) _inventoryItems.ForceUpdate();
            DrawBordersForItems(_inventoryItems.Value, "Inventory");
        }

        RenderStash(ui, refreshLiveSlots);
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

    /// <summary>
    /// True while an item is attached to the cursor. Used to keep the slot list live
    /// during a move so cached wrappers cannot draw at the previous position.
    /// </summary>
    private bool IsHoldingItem()
    {
        try
        {
            var cursor = ingameState?.IngameUi?.Cursor;
            if (cursor == null) return false;

            var action = cursor.Action;
            return action == MouseActionType.HoldItem || action == MouseActionType.HoldItemForSell;
        }
        catch
        {
            return false;
        }
    }

    private void RenderStash(IngameUIElements ui, bool refreshLiveSlots)
    {
        var stashUI = ui.StashElement;
        if (stashUI == null || !stashUI.IsVisible || stashUI.VisibleStash == null)
            return;

        var isMapStash = stashUI.VisibleStash.InvType == InventoryType.MapStash;
        var cache = isMapStash ? _mapStashItems : _stashItems;
        var value = cache.Value;

        // Refresh during a drag, right after a move, or on a tab switch so the
        // highlight follows the item instead of lingering on the old slot.
        if (refreshLiveSlots || stashUI.IndexVisibleStash != value.stashIndex)
        {
            cache.ForceUpdate();
            value = cache.Value;
        }

        if (stashUI.IndexVisibleStash == value.stashIndex)
            DrawBordersForItems(value.Item2, "Stash");
    }

    private void RenderLockers()
    {
        if (Settings.ShowHeistLockerHighlights.Value && GetVisibleHeistLocker() != null)
            DrawBordersForItems(_heistLockerItems.Value, "Heist Locker");

        if (Settings.ShowExpeditionLockerHighlights.Value && GetVisibleExpeditionLocker() != null)
            DrawBordersForItems(_expeditionLockerItems.Value, "Expedition Locker");
    }

    /// <summary>
    /// Cheap visibility probe used to avoid running the locker scanners while the
    /// lockers are closed.
    /// </summary>
    private static bool IsElementVisible(Element element)
    {
        if (element == null) return false;
        try
        {
            return element.IsVisible;
        }
        catch
        {
            return false;
        }
    }

    private Element GetVisibleHeistLocker()
    {
        var ui = ingameState?.IngameUi;
        if (ui == null) return null;

        if (_heistLockerElement != null)
        {
            if (IsElementVisible(_heistLockerElement))
                return _heistLockerElement;
            _heistLockerElement = null;
        }

        // Fast path: the known locker index is checked every frame.
        if (ui.ChildCount > Constants.UIIndices.HeistLockerDefault)
        {
            var element = ui.GetChildAtIndex(Constants.UIIndices.HeistLockerDefault);
            if (IsElementVisible(element) && element.ChildCount >= 10)
            {
                _heistLockerElement = element;
                return element;
            }
        }

        // Slow fallback scan is rate-limited so a closed locker costs almost nothing.
        var now = Environment.TickCount64;
        if (now - _lastHeistLockerProbe < 1000)
            return null;
        _lastHeistLockerProbe = now;

        for (int i = 0; i < ui.ChildCount; i++)
        {
            var child = ui.GetChildAtIndex(i);
            if (IsElementVisible(child) && child.ChildCount is >= 24 and <= 45)
            {
                _heistLockerElement = child;
                return child;
            }
        }

        return null;
    }

    private Element GetVisibleExpeditionLocker()
    {
        var ui = ingameState?.IngameUi;
        if (ui == null) return null;

        if (_expeditionLockerElement != null)
        {
            if (IsElementVisible(_expeditionLockerElement))
                return _expeditionLockerElement;
            _expeditionLockerElement = null;
        }

        // Fast path: check the known indices every frame.
        for (int i = 0; i < ExpeditionLockerIndices.Length; i++)
        {
            var index = ExpeditionLockerIndices[i];
            if (ui.ChildCount <= index) continue;

            var element = ui.GetChildAtIndex(index);
            if (IsElementVisible(element))
            {
                _expeditionLockerElement = element;
                return element;
            }
        }

        // Slow fallback scan is rate-limited so a closed locker costs almost nothing.
        var now = Environment.TickCount64;
        if (now - _lastExpeditionLockerProbe < 1000)
            return null;
        _lastExpeditionLockerProbe = now;

        for (int i = 0; i < ui.ChildCount; i++)
        {
            var child = ui.GetChildAtIndex(i);
            if (IsElementVisible(child) && child.ChildCount is >= 10 and <= 35)
            {
                _expeditionLockerElement = child;
                return child;
            }
        }

        return null;
    }

    private bool IsMapDeviceOpen()
    {
        var ui = ingameState?.IngameUi;
        if (ui == null) return false;

        var root = ui.ChildCount > Constants.UIIndices.MapDeviceRoot
            ? ui.GetChildAtIndex(Constants.UIIndices.MapDeviceRoot)
            : null;
        if (IsElementVisible(root))
            return true;

        // The raw root index has shifted between patches (67 -> 75 -> 76), so fall back to
        // the typed accessor instead of trusting the hardcoded child index alone.
        try
        {
            return ui.MapDeviceWindow?.IsVisible == true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsAtlasPanelVisible()
    {
        var ui = ingameState?.IngameUi;
        if (ui == null) return false;

        var atlas = ui.Atlas;
        if (IsElementVisible(atlas))
            return true;

        if (ui.ChildCount > Constants.UIIndices.AtlasPanel)
        {
            var panel = ui.GetChildAtIndex(Constants.UIIndices.AtlasPanel);
            return IsElementVisible(panel);
        }

        return false;
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
        // The Map Device window is also reachable through the Atlas panel
        // (AtlasPanel -> MapDeviceWindow), so the device scan and the storage scan can both
        // return the same inserted item. Track what was already drawn so an item never gets
        // two overlapping highlights.
        var drawnEntities = new HashSet<long>();

        if ((Settings.ShowForInvitations.Value || Settings.FilterTrade.Value) && IsMapDeviceOpen())
        {
            var deviceItems = _mapDeviceItems.Value;
            foreach (var item in deviceItems)
            {
                if (item?.Item == null || !item.IsVisible) continue;
                if (!drawnEntities.Add(item.Item.Address)) continue;

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
                    LogError($"Error drawing Map Device borders: {ex.Message}", 10);
                }
            }
        }

        if (Settings.ShowMapDeviceStorageHighlights.Value && IsAtlasPanelVisible())
            DrawBordersForItems(_mapDeviceStorageItems.Value, "Map Device Storage", drawnEntities);
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

    private void DrawBordersForItems(IEnumerable<NormalInventoryItem> items, string sourceName, HashSet<long> drawnEntities = null)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (item?.Item == null || !item.IsVisible) continue;
            if (drawnEntities != null && !drawnEntities.Add(item.Item.Address)) continue;
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
        ReloadModDictionaries();
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
            .Where(m => !Constants.IsModBlacklisted(m.RawName))
            .GroupBy(m => WhitespaceRegex.Replace(m.RawName, ""), StringComparer.OrdinalIgnoreCase)
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
                    var uiTextNoRanges = NumericRangeRegex.Replace(availableDescriptions[k], "").Trim();
                    uiTextNoRanges = WhitespaceRegex.Replace(uiTextNoRanges, " ");

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
            int modAD = 0, modIIR = 0, modPS = 0, modMM = 0, modMT = 0, modMC = 0;
            var stats = mod.ModRecord?.StatNames;
            var values = mod.Values;
            if (stats != null && values != null)
            {
                for (int i = 0; i < stats.Length && i < values.Count; i++)
                {
                    var key = stats[i].Key;
                    if (string.IsNullOrEmpty(key)) continue;
                    var val = values[i];
                    switch (MapYield.ClassifyStatKey(key))
                    {
                        case MapYield.Stat.AtlasDrops: modAD += val; break;
                        case MapYield.Stat.ItemRarity: modIIR += val; break;
                        case MapYield.Stat.PackSize: modPS += val; break;
                        case MapYield.Stat.MoreThreads: modMT += val; break;
                        case MapYield.Stat.MoreCurrency: modMC += val; break;
                        case MapYield.Stat.MoreMaps: modMM += val; break;
                    }
                }
            }

            string affixType = mod.ModRecord.AffixType.ToString();
            if (mod.RawName.Contains("Enchant") || affixType.Contains("Enchant"))
                affixType = "Enchant";

            var interestEntry = MatchInterest(mod.RawName);

            captured.Add(new CapturedMod
            {
                RawName = mod.RawName.Trim(),
                AffixType = affixType,
                DisplayName = existingEntry?.Text ?? interestEntry?.Text ?? (matchedDescription ?? mod.Name),
                Description = matchedDescription ?? (!string.IsNullOrEmpty(mod.Translation) && !mod.Translation.Contains("<unknown") ? mod.Translation : BaseModExtractor.GetReadableName(mod.RawName)),
                Color = existingEntry?.Color ?? new nuVector4(1, 1, 1, 1),
                IsBricking = existingEntry?.Bricking ?? false,
                IsOfInterest = interestEntry != null,
                InterestColor = interestEntry?.Color ?? GetNextInterestColor(),
                Quantity = modAD,
                Rarity = modIIR,
                PackSize = modPS,
                MoreMaps = modMM,
                MoreThreads = modMT,
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
                    var interestEntry = MatchInterest(mod.RawName);

                    var modText = !string.IsNullOrEmpty(mod.Translation) ? mod.Translation : mod.Name;
                    var cleanDesc = modText.Replace("{", "").Replace("}", "").Trim();

                    _capturedMods.Add(new CapturedMod
                    {
                        RawName = mod.RawName.Trim(),
                        AffixType = "Logbook Implicit",
                        DisplayName = existingEntry?.Text ?? interestEntry?.Text ?? cleanDesc,
                        Description = cleanDesc,
                        Color = existingEntry?.Color ?? new nuVector4(1, 1, 1, 1),
                        IsBricking = existingEntry?.Bricking ?? false,
                        IsOfInterest = interestEntry != null,
                        InterestColor = interestEntry?.Color ?? GetNextInterestColor()
                    });
                }
            }
        }
    }
}
