using ImGuiNET;
using System.IO;
using System.Linq;
using nuVector4 = System.Numerics.Vector4;
using nuVector2 = System.Numerics.Vector2;

namespace MapNotify_3_28
{
    partial class MapNotify_3_28
    {
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
                        }
                    }
                    ImGui.EndCombo();
                }

                ImGui.SameLine();
                if (ImGui.Button("New")) ImGui.OpenPopup("CreateProfilePopup");

                if (Settings.SelectedProfile.Value != "Default")
                {
                    ImGui.SameLine();
                    if (ImGui.Button("Del"))
                    {
                        Directory.Delete(GetProfileDirectory(), true);
                        Settings.SelectedProfile.Value = "Default";
                        RefreshProfileList();
                        GoodModsDictionary = LoadConfigGoodMod();
                        BadModsDictionary = LoadConfigBadMod();
                    }
                }

                if (ImGui.BeginPopup("CreateProfilePopup"))
                {
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

                ImGui.InputTextWithHint("##modfilter", "Filter Mods...", ref _modFilter, 100);
                ImGui.Separator();

                if (ImGui.TreeNodeEx("Active Mods", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (GoodModsDictionary != null && GoodModsDictionary.Count > 0)
                    {
                        var filteredGood = GoodModsDictionary
                            .Where(m => string.IsNullOrEmpty(_modFilter) || m.Key.Contains(_modFilter, System.StringComparison.OrdinalIgnoreCase) || m.Value.Text.Contains(_modFilter, System.StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        ImGui.TextColored(new nuVector4(0.4f, 1f, 0.4f, 1f), "Good Mods:");
                        ImGui.Indent();
                        foreach (var mod in filteredGood)
                        {
                            ImGui.PushID($"active_good_{mod.Key}");
                            if (ImGui.Button("X")) DeleteModFromConfig(mod.Key);
                            ImGui.SameLine();
                            var col = mod.Value.Color;
                            string conflictStatus = (BadModsDictionary?.ContainsKey(mod.Key) ?? false) ? " [!] CONFLICT" : "";
                            string brickStatus = mod.Value.Bricking ? " [BRICKED]" : "";
                            string cleanText = EscapeImGui(mod.Value.Text);
                            ImGui.TextColored(col, $"{cleanText} ({mod.Key}){brickStatus}{conflictStatus}");
                            ImGui.PopID();
                        }
                        ImGui.Unindent();
                    }

                    if (BadModsDictionary != null && BadModsDictionary.Count > 0)
                    {
                        var filteredBad = BadModsDictionary
                            .Where(m => string.IsNullOrEmpty(_modFilter) || m.Key.Contains(_modFilter, System.StringComparison.OrdinalIgnoreCase) || m.Value.Text.Contains(_modFilter, System.StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        ImGui.TextColored(new nuVector4(1f, 0.4f, 0.4f, 1f), "Bad Mods:");
                        ImGui.Indent();
                        foreach (var mod in filteredBad)
                        {
                            ImGui.PushID($"active_bad_{mod.Key}");
                            if (ImGui.Button("X")) DeleteModFromConfig(mod.Key);
                            ImGui.SameLine();
                            var col = mod.Value.Color;
                            string conflictStatus = (GoodModsDictionary?.ContainsKey(mod.Key) ?? false) ? " [!] CONFLICT" : "";
                            string brickPrefix = mod.Value.Bricking ? "[B] " : "";
                            string cleanText = EscapeImGui(mod.Value.Text);
                            ImGui.TextColored(col, $"{brickPrefix}{cleanText} ({mod.Key}){conflictStatus}");
                            ImGui.PopID();
                        }
                        ImGui.Unindent();
                    }
                    ImGui.TreePop();
                }
                ImGui.Separator();

                if (ImGui.BeginChild("ScrollingRegion", new nuVector2(0, -35), ImGuiChildFlags.Border))
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

                        // Visual Headers for Hierarchy (Prefix/Suffix/Implicit)
                        if (mod.AffixType != lastAffixType)
                        {
                            if (i > 0) ImGui.Dummy(new System.Numerics.Vector2(0, 10));
                            ImGui.SetWindowFontScale(1.0f);
                            ImGui.TextColored(new nuVector4(0.502f, 0.8f, 1f, 1f), $"--- {mod.AffixType} ---");
                            ImGui.SetWindowFontScale(1.0f);
                            ImGui.Separator();
                            lastAffixType = mod.AffixType;
                        }
                        ImGui.PushID(mod.RawName);

                        bool inGood = GoodModsDictionary?.Any(x => BaseModExtractor.AreEquivalent(mod.RawName, x.Key)) ?? false;
                        bool inBad = BadModsDictionary?.Any(x => BaseModExtractor.AreEquivalent(mod.RawName, x.Key)) ?? false;
                        if (inGood && inBad) ImGui.TextColored(new nuVector4(1f, 1f, 0f, 1f), "[!] CONFLICT: Present in both Good and Bad lists.");
                        
                        HelpMarker($"Internal Name: {mod.RawName}");
                        ImGui.SameLine();
                        string displayDesc = EscapeImGui(string.IsNullOrEmpty(mod.Description) ? mod.RawName : mod.Description);

                        ImGui.TextWrapped(displayDesc);
                        var dispName = mod.DisplayName;
                        if (ImGui.InputText("##displayname", ref dispName, 100))
                        {
                            mod.DisplayName = dispName;
                            AutoSaveIfExisting(mod);
                        }
                        ImGui.SameLine();
                        var color = mod.Color;
                        if (ImGui.ColorEdit4("", ref color, ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.NoInputs))
                        {
                            mod.Color = color;
                            AutoSaveIfExisting(mod);
                        }
                        ImGui.Dummy(new nuVector2(0, 5));
                        if (ImGui.Button("Add Good"))
                        {
                            // If color is White, Soft Red (UI), or Pure Red (Config Template), switch to Good default
                            if (mod.Color == new nuVector4(1f, 1f, 1f, 1f) || 
                                mod.Color == new nuVector4(1f, 0.4f, 0.4f, 1f) || 
                                mod.Color == new nuVector4(1f, 0f, 0f, 1f))
                            {
                                mod.Color = new nuVector4(0.4f, 1f, 0.4f, 1f);
                            }
                            SaveModToConfig(mod, "GoodMods.txt");
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Add Bad"))
                        {
                            // If color is White, Soft Green (UI), or Pure Green (Config Template), switch to Bad default
                            if (mod.Color == new nuVector4(1f, 1f, 1f, 1f) || 
                                mod.Color == new nuVector4(0.4f, 1f, 0.4f, 1f) || 
                                mod.Color == new nuVector4(0f, 1f, 0f, 1f))
                            {
                                mod.Color = new nuVector4(1f, 0.4f, 0.4f, 1f);
                            }
                            SaveModToConfig(mod, "BadMods.txt");
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Delete")) DeleteModFromConfig(mod.RawName);
                        ImGui.SameLine();
                        var brick = mod.IsBricking;
                        if (ImGui.Checkbox("Brick", ref brick))
                        {
                            mod.IsBricking = brick;
                            AutoSaveIfExisting(mod);
                        }
                        ImGui.Separator();
                        ImGui.PopID();
                    }
                    ImGui.EndChild();
                }

                if (ImGui.Button("Close Window", new nuVector2(-1, 0)))
                {
                    _showPreviewWindow = false;
                    _modFilter = string.Empty;
                }
                ImGui.End();
            }
        }

        private void AutoSaveIfExisting(CapturedMod mod)
        {
            var (match, isGood) = MatchMod(mod.RawName);
            if (match != null) SaveModToConfig(mod, isGood ? "GoodMods.txt" : "BadMods.txt");
        }

        /// <summary>
        /// Writes a captured mod entry to the specified configuration file.
        /// Automatically handles moving mods between Good and Bad lists if they already exist.
        /// </summary>
        private void SaveModToConfig(CapturedMod mod, string fileName, string baseMod = null)
        {
            if (baseMod == null)
                baseMod = BaseModExtractor.GetBaseMod(mod.RawName);

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
        private void DeleteModFromConfig(string rawName)
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
            else LogError($"Could not find {rawName} in GoodMods.txt or BadMods.txt", 5);
        }
    }
}
