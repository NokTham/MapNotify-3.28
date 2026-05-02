using System;
using ImGuiNET;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nuVector4 = System.Numerics.Vector4;
using nuVector2 = System.Numerics.Vector2;

namespace MapNotify_3_28
{
    partial class MapNotify_3_28
    {
        private Dictionary<string, string> _browserDisplayNameEdits = new Dictionary<string, string>();
        private Dictionary<string, bool> _browserBrickingEdits = new Dictionary<string, bool>();

        /// <summary>
        /// Renders the ImGui interface for the Map Mod Preview window.
        /// This allows users to filter, categorize, and save captured mods to config files.
        /// </summary>
        private void DrawPreviewWindow()
        {
            ImGui.SetNextWindowSize(new nuVector2(450, 600), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Map Mod Preview", ref _showPreviewWindow, ImGuiWindowFlags.NoCollapse))
            {
                // Profile Management Header
                ImGui.Text("Profile:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150);
                if (ImGui.BeginCombo("##profileselect", Settings.SelectedProfile.Value))
                {
                    foreach (var profile in _availableProfiles)
                    {
                        if (ImGui.Selectable(profile, profile == Settings.SelectedProfile.Value))
                        {
                            Settings.SelectedProfile.Value = profile;
                            GoodModsDictionary = LoadConfigGoodMod();
                            BadModsDictionary = LoadConfigBadMod();
                            _browserDisplayNameEdits.Clear();
                            _browserBrickingEdits.Clear();
                        }
                    }
                    ImGui.EndCombo();
                }

                ImGui.SameLine();
                if (ImGui.Button("New"))
                {
                    _newProfileName = string.Empty;
                    ImGui.OpenPopup("CreateProfilePopup");
                }

                if (Settings.SelectedProfile.Value != "Default")
                {
                    ImGui.SameLine();
                    if (ImGui.Button("Ren"))
                    {
                        _newProfileName = string.Empty;
                        ImGui.OpenPopup("RenameProfilePopup");
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Del"))
                    {
                        Directory.Delete(GetProfileDirectory(), true);
                        Settings.SelectedProfile.Value = "Default";
                        RefreshProfileList();
                        GoodModsDictionary = LoadConfigGoodMod();
                        BadModsDictionary = LoadConfigBadMod();
                        _browserDisplayNameEdits.Clear();
                        _browserBrickingEdits.Clear();
                    }
                }

                if (ImGui.BeginPopup("CreateProfilePopup"))
                {
                    var currentPlayerName = GameController.Player?.GetComponent<ExileCore.PoEMemory.Components.Player>()?.PlayerName;
                    if (!string.IsNullOrEmpty(currentPlayerName))
                    {
                        if (ImGui.Button($"Use Character Name: {currentPlayerName}")) _newProfileName = currentPlayerName;
                        ImGui.Separator();
                    }

                    ImGui.InputTextWithHint("##newprofilename", "Profile Name", ref _newProfileName, 50);
                    if (ImGui.Button("Create") && !string.IsNullOrWhiteSpace(_newProfileName))
                    {
                        Settings.SelectedProfile.Value = _newProfileName;
                        ResetConfigs(); // Creates the directory and default files
                        RefreshProfileList();
                        _newProfileName = string.Empty;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }

                if (ImGui.BeginPopup("RenameProfilePopup"))
                {
                    ImGui.Text($"Rename profile '{Settings.SelectedProfile.Value}' to:");
                    ImGui.InputTextWithHint("##renameprofilename", "New Profile Name", ref _newProfileName, 50);
                    if (ImGui.Button("Apply") && !string.IsNullOrWhiteSpace(_newProfileName))
                    {
                        var oldName = Settings.SelectedProfile.Value;
                        var oldPath = Path.Combine(ConfigDirectory, "Profiles", oldName);
                        var newPath = Path.Combine(ConfigDirectory, "Profiles", _newProfileName);
                        
                        if (Directory.Exists(oldPath) && !Directory.Exists(newPath))
                        {
                            try
                            {
                                Directory.Move(oldPath, newPath);
                                Settings.SelectedProfile.Value = _newProfileName;
                                RefreshProfileList();
                                LogMessage($"MapNotify: Profile renamed to {_newProfileName}", 5);
                            }
                            catch (Exception ex) { LogError($"MapNotify: Rename failed: {ex.Message}", 10); }
                        }
                        _newProfileName = string.Empty;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }

                Settings.AutoSwitchProfile.Value = Checkbox("Auto-switch Profile (by Character Name)", Settings.AutoSwitchProfile.Value);
                ImGui.SameLine();
                WarningMarker("Automatically switches to a profile matching your character's name. Note: You MUST create a new profile with the exact character name for this to function.");

                ImGui.InputTextWithHint("##modfilter", "Filter Mods...", ref _modFilter, 100);
                ImGui.Separator();

                if (ImGui.TreeNodeEx("Active Mods", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    DrawActiveMods();
                    ImGui.TreePop();
                }
                ImGui.Separator();

                if (ImGui.BeginTabBar("ModPreviewTabs"))
                {
                    bool opened;
                    if (_forceCapturedTab)
                    {
                        bool p_open = true;
                        opened = ImGui.BeginTabItem("Captured Mods", ref p_open, ImGuiTabItemFlags.SetSelected);
                        if (opened) _forceCapturedTab = false;
                    }
                    else
                    {
                        opened = ImGui.BeginTabItem("Captured Mods");
                    }

                    if (opened)
                    {
                        if (ImGui.BeginChild("CapturedScrollingRegion", new nuVector2(0, -35), ImGuiChildFlags.Border))
                        {
                            DrawCapturedMods();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Mod Browser"))
                    {
                        if (ImGui.BeginChild("BrowserScrollingRegion", new nuVector2(0, -35), ImGuiChildFlags.Border))
                        {
                            DrawModBrowser();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }

                if (ImGui.Button("Close Window", new nuVector2(-1, 0)))
                {
                    _showPreviewWindow = false;
                    _modFilter = string.Empty;
                }
                ImGui.End();
            }
        }

        private void DrawActiveMods()
        {
            void DrawSection(Dictionary<string, StyledText> dict, string label, nuVector4 color, Dictionary<string, StyledText> otherDict)
            {
                if (dict == null || dict.Count == 0) return;
                var groups = dict
                    .Where(m => string.IsNullOrEmpty(_modFilter) ||
                                m.Key.Contains(_modFilter, System.StringComparison.OrdinalIgnoreCase) ||
                                m.Value.Text.Contains(_modFilter, System.StringComparison.OrdinalIgnoreCase))
                    .GroupBy(m => string.IsNullOrWhiteSpace(m.Value.Text) ? m.Key : m.Value.Text)
                    .ToList();

                if (groups.Count == 0) return;
                ImGui.TextColored(color, label);
                ImGui.Indent();
                foreach (var group in groups)
                {
                    ImGui.PushID($"{label}_{group.Key}");
                    if (ImGui.Button("X")) { foreach (var m in group) DeleteModFromConfig(m.Key); }
                    ImGui.SameLine();
                    HelpMarker($"{string.Join("\n", group.Select(m => m.Key))}");
                    ImGui.SameLine();
                    var firstMod = group.First().Value;
                    bool isAnyBricked = group.Any(m => m.Value.Bricking);
                    bool hasConflict = group.Any(m => otherDict?.ContainsKey(m.Key) ?? false);
                    ImGui.TextColored(firstMod.Color, $"{EscapeImGui(group.Key)}{(isAnyBricked ? " [B]" : "")}{(hasConflict ? " [!] CONFLICT" : "")}");
                    ImGui.PopID();
                }
                ImGui.Unindent();
            }

            DrawSection(GoodModsDictionary, "Good Mods:", new nuVector4(0.4f, 1f, 0.4f, 1f), BadModsDictionary);
            DrawSection(BadModsDictionary, "Bad Mods:", new nuVector4(1f, 0.4f, 0.4f, 1f), GoodModsDictionary);
        }

        private void DrawCapturedMods()
        {
            if (_capturedMods.Count == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new nuVector4(0.7f, 0.7f, 0.7f, 1f));
                ImGui.TextWrapped("No mods captured. Hover over a map and press your hotkey to see and configure its modifiers.");
                ImGui.PopStyleColor();
            }
            string lastAffixType = null;
            for (int i = 0; i < _capturedMods.Count; i++)
            {
                var mod = _capturedMods[i];
                if (!string.IsNullOrEmpty(_modFilter) &&
                    !mod.RawName.Contains(_modFilter, System.StringComparison.OrdinalIgnoreCase) &&
                    !mod.Description.Contains(_modFilter, System.StringComparison.OrdinalIgnoreCase) &&
                    !mod.DisplayName.Contains(_modFilter, System.StringComparison.OrdinalIgnoreCase)) continue;

                if (mod.AffixType != lastAffixType)
                {
                    if (i > 0) ImGui.Dummy(new System.Numerics.Vector2(0, 10));
                    ImGui.TextColored(new nuVector4(0.502f, 0.8f, 1f, 1f), $"--- {mod.AffixType} ---");
                    ImGui.Separator();
                    lastAffixType = mod.AffixType;
                }
                DrawModEntry(mod);
            }
        }

        private void DrawModEntry(CapturedMod mod)
        {
            ImGui.PushID(mod.RawName + mod.AffixType);
            bool inGood = GoodModsDictionary?.Any(x => BaseModExtractor.AreEquivalent(mod.RawName, x.Key)) ?? false;
            bool inBad = BadModsDictionary?.Any(x => BaseModExtractor.AreEquivalent(mod.RawName, x.Key)) ?? false;
            if (inGood && inBad) ImGui.TextColored(new nuVector4(1f, 1f, 0f, 1f), "[!] CONFLICT: Present in both Good and Bad lists.");

            HelpMarker($"{mod.RawName}");
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, mod.Color);
            ImGui.TextWrapped(EscapeImGui(string.IsNullOrEmpty(mod.Description) ? mod.RawName : mod.Description).Replace("\\n", "\n"));
            ImGui.PopStyleColor();


            var displayName = mod.DisplayName;
            if (ImGui.InputText("##displayname", ref displayName, 100))
            {
                mod.DisplayName = displayName;
                AutoSaveIfExisting(mod);
            }

            ImGui.SameLine();
            var color = mod.Color;
            if (ImGui.ColorEdit4("", ref color, ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
            {
                mod.Color = color;
                AutoSaveIfExisting(mod);
            }

            ImGui.Dummy(new nuVector2(0, 5));
            if (ImGui.Button("Add Good")) { UpdateColorForCategory(mod, true); SaveModToConfig(mod, "GoodMods.txt"); }
            ImGui.SameLine();
            if (ImGui.Button("Add Bad")) { UpdateColorForCategory(mod, false); SaveModToConfig(mod, "BadMods.txt"); }
            ImGui.SameLine();
            if (ImGui.Button("Delete")) DeleteModFromConfig(mod.RawName);
            ImGui.SameLine();
            var isBricking = mod.IsBricking;
            if (ImGui.Checkbox("Brick", ref isBricking))
            {
                mod.IsBricking = isBricking;
                AutoSaveIfExisting(mod);
            }
            ImGui.Separator();
            ImGui.PopID();
        }

        private void DrawModBrowser()
        {
            if (_modEntries.Count == 0)
            {
                ImGui.Text("Mod database not loaded or empty.");
                return;
            }

            var filtered = _modEntries
                .Where(entry => string.IsNullOrEmpty(_modFilter) ||
                                entry.GroupKey.Contains(_modFilter, StringComparison.OrdinalIgnoreCase) ||
                                (entry.Descriptions != null && entry.Descriptions.Any(d => d.Contains(_modFilter, StringComparison.OrdinalIgnoreCase))) ||
                                (entry.BaseMods != null && entry.BaseMods.Any(bm => bm.Contains(_modFilter, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            ImGui.Text($"Showing {filtered.Count} mods from database.");
            ImGui.Separator();

            string[] typeOrder = { "Generic", "Uber", "Expedition", "Valdo" };
            foreach (var sectionType in typeOrder)
            {
                var sectionMods = filtered.Where(m =>
                    (string.IsNullOrEmpty(m.Type) ? "Generic" : m.Type).Equals(sectionType, StringComparison.OrdinalIgnoreCase)).ToList();

                if (sectionMods.Count == 0) continue;

                if (ImGui.TreeNodeEx($"{sectionType} ({sectionMods.Count})", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    foreach (var modEntry in sectionMods)
                    {
                        DrawModBrowserEntry(modEntry);
                    }
                    ImGui.TreePop();
                }
            }
        }

        private void DrawModBrowserEntry(ModEntry modEntry)
        {
            ImGui.PushID(modEntry.GroupKey);

            // Determine overall status for the ModEntry
            var browserMatches = modEntry.BaseMods.Select(MatchMod).ToList();
            bool anyGood = browserMatches.Any(m => m.match != null && m.isGood);
            bool anyBad = browserMatches.Any(m => m.match != null && !m.isGood);
            bool anyBricking = browserMatches.Any(m => m.match?.Bricking ?? false);

            if (!_browserBrickingEdits.TryGetValue(modEntry.GroupKey, out var currentBricking))
            {
                currentBricking = anyBricking;
                _browserBrickingEdits[modEntry.GroupKey] = currentBricking;
            }

            // Get the color from the first matched mod in the group, or fall back to defaults
            var matchResult = browserMatches.FirstOrDefault(m => m.match != null);
            nuVector4 displayColor = matchResult.match?.Color ?? new nuVector4(1, 1, 1, 1);

            if (matchResult.match == null)
            {
                if (anyGood && !anyBad) displayColor = new nuVector4(0.4f, 1f, 0.4f, 1f); // Default Green
                else if (anyBad && !anyGood) displayColor = new nuVector4(1f, 0.4f, 0.4f, 1f); // Default Red
            }
            if (anyGood && anyBad) ImGui.TextColored(new nuVector4(1f, 1f, 0f, 1f), "[!] CONFLICT: Present in both Good and Bad lists."); // Yellow for conflict

            HelpMarker($"{string.Join(", ", modEntry.BaseMods)}");
            ImGui.SameLine();

            for (int i = 0; i < modEntry.Descriptions.Count; i++)
            {
                if (i > 0) ImGui.Indent(30f);
                ImGui.PushStyleColor(ImGuiCol.Text, displayColor);
                ImGui.TextWrapped(EscapeImGui(modEntry.Descriptions[i]).Replace("\\n", "\n"));
                ImGui.PopStyleColor();
                if (i > 0) ImGui.Unindent(30f);
            }

            // Tooltipname field: pre-filled with nothing
            if (!_browserDisplayNameEdits.TryGetValue(modEntry.GroupKey, out var currentEditName))
            {
                // Find if any of the BaseMods for this description have a custom display name
                string defaultDisplayName = string.Empty;
                foreach (var bm in modEntry.BaseMods)
                {
                    var (existing, _) = MatchMod(bm);
                    if (existing != null && !string.Equals(existing.Text, bm, StringComparison.OrdinalIgnoreCase))
                    {
                        defaultDisplayName = existing.Text;
                        break; // Found one, use it
                    }
                }
                currentEditName = defaultDisplayName;
                _browserDisplayNameEdits[modEntry.GroupKey] = currentEditName;
            }

            if (ImGui.InputText("##displayname", ref currentEditName, 100))
            {
                _browserDisplayNameEdits[modEntry.GroupKey] = currentEditName;
                // If any of these BaseMods already exist in config, update their DisplayName
                foreach (var bm in modEntry.BaseMods)
                {
                    var (matchedKey, existing, isGood) = MatchModWithKey(bm);
                    if (existing != null)
                    {
                        // Create a temp CapturedMod to pass to SaveModToConfig for updating
                        SaveModToConfig(new CapturedMod { RawName = bm, DisplayName = currentEditName, Description = modEntry.Descriptions.FirstOrDefault() ?? modEntry.GroupKey, Color = existing.Color, IsBricking = existing.Bricking }, isGood ? "GoodMods.txt" : "BadMods.txt", matchedKey);
                    }
                }
            }

            ImGui.SameLine();
            var colorForPicker = displayColor; // Use the determined status color for the picker's initial state
            if (ImGui.ColorEdit4("", ref colorForPicker, ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
            {
                displayColor = colorForPicker; // Update the display color immediately
                // If any of these BaseMods already exist in config, update their Color
                foreach (var bm in modEntry.BaseMods)
                {
                    var (matchedKey, existing, isGood) = MatchModWithKey(bm);
                    if (existing != null)
                    {
                        SaveModToConfig(new CapturedMod { RawName = bm, DisplayName = existing.Text, Description = modEntry.Descriptions.FirstOrDefault() ?? modEntry.GroupKey, Color = colorForPicker, IsBricking = existing.Bricking }, isGood ? "GoodMods.txt" : "BadMods.txt", matchedKey);
                    }
                }
            }

            ImGui.Dummy(new nuVector2(0, 5));

            if (ImGui.Button("Add Good"))
            {
                foreach (var bm in modEntry.BaseMods)
                {
                    var tempMod = new CapturedMod { RawName = bm, DisplayName = currentEditName, Description = modEntry.Descriptions.FirstOrDefault() ?? modEntry.GroupKey, Color = displayColor, IsBricking = currentBricking };
                    UpdateColorForCategory(tempMod, true);
                    SaveModToConfig(tempMod, "GoodMods.txt", bm);
                }
                _browserDisplayNameEdits.Remove(modEntry.GroupKey); // Clear edit after action
                _browserBrickingEdits.Remove(modEntry.GroupKey);
                GoodModsDictionary = LoadConfigGoodMod(); // Reload dictionaries to reflect changes
                BadModsDictionary = LoadConfigBadMod();
            }
            ImGui.SameLine();
            if (ImGui.Button("Add Bad"))
            {
                foreach (var bm in modEntry.BaseMods)
                {
                    var tempMod = new CapturedMod
                    {
                        RawName = bm,
                        DisplayName = currentEditName,
                        Description = modEntry.Descriptions.FirstOrDefault() ?? modEntry.GroupKey,
                        Color = displayColor,
                        IsBricking = currentBricking
                    };
                    UpdateColorForCategory(tempMod, false);
                    SaveModToConfig(tempMod, "BadMods.txt", bm);
                }
                _browserDisplayNameEdits.Remove(modEntry.GroupKey); // Clear edit after action
                _browserBrickingEdits.Remove(modEntry.GroupKey);
                GoodModsDictionary = LoadConfigGoodMod(); // Reload dictionaries to reflect changes
                BadModsDictionary = LoadConfigBadMod();
            }
            ImGui.SameLine();
            if (ImGui.Button("Delete"))
            {
                foreach (var bm in modEntry.BaseMods)
                {
                    DeleteModFromConfig(bm, false);
                }
                _browserDisplayNameEdits.Remove(modEntry.GroupKey); // Clear edit after action
                _browserBrickingEdits.Remove(modEntry.GroupKey);
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("Brick", ref currentBricking))
            {
                _browserBrickingEdits[modEntry.GroupKey] = currentBricking;
                foreach (var bm in modEntry.BaseMods)
                {
                    // Need to create a CapturedMod to pass to AutoSaveIfExisting
                    var tempMod = new CapturedMod { RawName = bm, DisplayName = currentEditName, Description = modEntry.Descriptions.FirstOrDefault() ?? modEntry.GroupKey, Color = displayColor, IsBricking = currentBricking };
                    // AutoSaveIfExisting will handle finding if it's good/bad and saving
                    AutoSaveIfExisting(tempMod);
                }
                GoodModsDictionary = LoadConfigGoodMod(); // Reload dictionaries to reflect changes
                BadModsDictionary = LoadConfigBadMod();
            }

            ImGui.Separator();
            ImGui.PopID();
        }

        private void UpdateColorForCategory(CapturedMod mod, bool isGood)
        {
            if (mod.Color == new nuVector4(1f, 1f, 1f, 1f))
                mod.Color = isGood ? new nuVector4(0.4f, 1f, 0.4f, 1f) : new nuVector4(1f, 0.4f, 0.4f, 1f);
            else if (isGood && (mod.Color == new nuVector4(1f, 0.4f, 0.4f, 1f) || mod.Color == new nuVector4(1f, 0f, 0f, 1f)))
                mod.Color = new nuVector4(0.4f, 1f, 0.4f, 1f);
            else if (!isGood && (mod.Color == new nuVector4(0.4f, 1f, 0.4f, 1f) || mod.Color == new nuVector4(0f, 1f, 0f, 1f)))
                mod.Color = new nuVector4(1f, 0.4f, 0.4f, 1f);
        }

        private void AutoSaveIfExisting(CapturedMod mod)
        {
            var (matchedKey, match, isGood) = MatchModWithKey(mod.RawName);
            if (match != null)
            {
                // Update the existing entry in the dictionary directly
                match.Text = mod.DisplayName;
                match.Color = mod.Color;
                match.Bricking = mod.IsBricking;

                // Then save to file to persist the updated values
                SaveModToConfig(mod, isGood ? "GoodMods.txt" : "BadMods.txt", matchedKey);
            }
        }

        /// <summary>
        /// Writes a captured mod entry to the specified configuration file.
        /// Automatically handles moving mods between Good and Bad lists if they already exist.
        /// </summary>
        private void SaveModToConfig(CapturedMod mod, string fileName, string baseMod = null)
        {
            baseMod = BaseModExtractor.GetBaseMod(baseMod ?? mod.RawName);

            string pureFileName = Path.GetFileName(fileName);
            string otherFile = pureFileName == "GoodMods.txt" ? "BadMods.txt" : "GoodMods.txt";
            var otherPath = Path.Combine(GetProfileDirectory(), otherFile);
            RemoveModFromFile(otherPath, mod.RawName);

            // Set default colors if user hasn't picked one
            if (mod.Color == new nuVector4(1, 1, 1, 1))
                mod.Color = pureFileName == "GoodMods.txt" ? new nuVector4(0.4f, 1f, 0.4f, 1f) : new nuVector4(1f, 0.4f, 0.4f, 1f);

            var path = Path.Combine(GetProfileDirectory(), pureFileName);
            var hexColor = ToHex(mod.Color);
            var newLine = $"{baseMod};{mod.DisplayName};{hexColor};{mod.IsBricking}";

            UpdateModInFile(path, mod.RawName, newLine);
            GoodModsDictionary = LoadConfigGoodMod();
            BadModsDictionary = LoadConfigBadMod();
            LogMessage($"Saved/Updated mod: {mod.RawName}", 5);
        }

        private void UpdateModInFile(string path, string rawName, string newLine)
        {
            if (!File.Exists(path)) File.WriteAllText(path, "");
            var lines = File.ReadAllLines(path).ToList();
            lines.RemoveAll(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#") &&
                                 BaseModExtractor.AreEquivalent(rawName, l.Split(';')[0].Trim()));
            lines.Add(newLine);
            File.WriteAllLines(path, lines);
        }

        /// <summary>
        /// Removes a mod from both Good and Bad config files.
        /// Suggestion: Consolidate IO operations to reduce disk hits when managing multiple mods.
        /// </summary>
        private void DeleteModFromConfig(string rawName, bool logError = true)
        {
            string[] files = { "GoodMods.txt", "BadMods.txt" };
            bool deleted = false;

            foreach (var fileName in files)
            {
                if (RemoveModFromFile(Path.Combine(GetProfileDirectory(), fileName), rawName))
                {
                    deleted = true;
                    LogMessage($"Deleted mod entry: {rawName} from {fileName}", 5);
                }
            }
            if (deleted) { GoodModsDictionary = LoadConfigGoodMod(); BadModsDictionary = LoadConfigBadMod(); }
            else if (logError) LogError($"Could not find {rawName} in GoodMods.txt or BadMods.txt", 5);
        }
    }
}
