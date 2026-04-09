using ImGuiNET;
using System.IO;
using System.Linq;
using nuVector4 = System.Numerics.Vector4;
using nuVector2 = System.Numerics.Vector2;

namespace MapNotify_3_28
{
    partial class MapNotify_3_28
    {
        private void DrawPreviewWindow()
        {
            ImGui.SetNextWindowSize(new nuVector2(450, 600), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Map Mod Preview", ref _showPreviewWindow, ImGuiWindowFlags.NoCollapse))
            {
                ImGui.TextColored(new nuVector4(0.5f, 1f, 0.5f, 1f), "Captured Mods from Hovered Map:");
                ImGui.TextDisabled("Drag the bottom-right corner to resize.");
                ImGui.Separator();

                if (ImGui.TreeNodeEx("Active Mods", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (GoodModsDictionary != null && GoodModsDictionary.Count > 0)
                    {
                        ImGui.TextColored(new nuVector4(0.4f, 1f, 0.4f, 1f), "Good Mods:");
                        ImGui.Indent();
                        foreach (var mod in GoodModsDictionary)
                        {
                            ImGui.PushID($"active_good_{mod.Key}");
                            if (ImGui.Button("X")) DeleteModFromConfig(mod.Key);
                            ImGui.SameLine();
                            var col = SharpToNu(mod.Value.Color);
                            string brickStatus = mod.Value.Bricking ? " [BRICKED]" : "";
                            string cleanText = mod.Value.Text.Replace("%%", "%").Replace("%", "%%");
                            ImGui.TextColored(col, $"{cleanText} ({mod.Key}){brickStatus}");
                            ImGui.PopID();
                        }
                        ImGui.Unindent();
                    }

                    if (BadModsDictionary != null && BadModsDictionary.Count > 0)
                    {
                        ImGui.TextColored(new nuVector4(1f, 0.4f, 0.4f, 1f), "Bad Mods:");
                        ImGui.Indent();
                        foreach (var mod in BadModsDictionary)
                        {
                            ImGui.PushID($"active_bad_{mod.Key}");
                            if (ImGui.Button("X")) DeleteModFromConfig(mod.Key);
                            ImGui.SameLine();
                            var col = SharpToNu(mod.Value.Color);
                            string brickPrefix = mod.Value.Bricking ? "[B] " : "";
                            string cleanText = mod.Value.Text.Replace("%%", "%").Replace("%", "%%");
                            ImGui.TextColored(col, $"{brickPrefix}{cleanText} ({mod.Key})");
                            ImGui.PopID();
                        }
                        ImGui.Unindent();
                    }
                    ImGui.TreePop();
                }
                ImGui.Separator();

                if (ImGui.BeginChild("ScrollingRegion", new nuVector2(0, -35), ImGuiChildFlags.Border))
                {
                    string lastAffixType = null;
                    for (int i = 0; i < _capturedMods.Count; i++)
                    {
                        var mod = _capturedMods[i];

                        // Visual Headers for Hierarchy (Prefix/Suffix/Implicit)
                        if (mod.AffixType != lastAffixType)
                        {
                            if (i > 0) ImGui.Dummy(new System.Numerics.Vector2(0, 10));
                            ImGui.TextColored(new nuVector4(0.5f, 0.8f, 1f, 1f), $"--- {mod.AffixType}es ---");
                            ImGui.Separator();
                            lastAffixType = mod.AffixType;
                        }

                        ImGui.PushID(i);
                        ImGui.SameLine();
                        string displayDesc = (string.IsNullOrEmpty(mod.Description) ? mod.RawName : mod.Description).Replace("%%", "%").Replace("%", "%%");
                        ImGui.Dummy(new nuVector2(0, 5));
                        ImGui.TextWrapped(displayDesc);

                        var dispName = mod.DisplayName;
                        if (ImGui.InputText("Tooltip Name", ref dispName, 100))
                        {
                            mod.DisplayName = dispName;
                            AutoSaveIfExisting(mod);
                        }
                        var color = mod.Color;
                        if (ImGui.ColorEdit4("Color", ref color, ImGuiColorEditFlags.AlphaPreviewHalf))
                        {
                            mod.Color = color;
                            AutoSaveIfExisting(mod);
                        }
                        var brick = mod.IsBricking;
                        if (ImGui.Checkbox("Bricking Mod", ref brick))
                        {
                            mod.IsBricking = brick;
                            AutoSaveIfExisting(mod);
                        }
                        if (ImGui.Button("Add Good")) SaveModToConfig(mod, "GoodMods.txt");
                        ImGui.SameLine();
                        if (ImGui.Button("Add Bad")) SaveModToConfig(mod, "BadMods.txt");
                        ImGui.SameLine();
                        if (ImGui.Button("Delete")) DeleteModFromConfig(mod.RawName);
                        ImGui.Separator();
                        ImGui.PopID();
                    }
                    ImGui.EndChild();
                }

                if (ImGui.Button("Close Window", new nuVector2(-1, 0))) _showPreviewWindow = false;
                ImGui.End();
            }
        }

        private void AutoSaveIfExisting(CapturedMod mod)
        {
            if (GoodModsDictionary.Keys.Any(k => mod.RawName.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf(mod.RawName, System.StringComparison.OrdinalIgnoreCase) >= 0))
                SaveModToConfig(mod, "GoodMods.txt");
            else if (BadModsDictionary.Keys.Any(k => mod.RawName.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0 || k.IndexOf(mod.RawName, System.StringComparison.OrdinalIgnoreCase) >= 0))
                SaveModToConfig(mod, "BadMods.txt");
        }

        private void SaveModToConfig(CapturedMod mod, string fileName)
        {
            string otherFile = fileName == "GoodMods.txt" ? "BadMods.txt" : "GoodMods.txt";
            var otherPath = Path.Combine(ConfigDirectory, otherFile);
            if (File.Exists(otherPath))
            {
                var otherLines = File.ReadAllLines(otherPath).ToList();
                bool removedFromOther = otherLines.RemoveAll(l =>
                {
                    var configKey = l.Split(';')[0].Trim();
                    if (string.IsNullOrEmpty(configKey) || configKey.StartsWith("#")) return false;
                    return mod.RawName.IndexOf(configKey, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           configKey.IndexOf(mod.RawName, System.StringComparison.OrdinalIgnoreCase) >= 0;
                }) > 0;
                if (removedFromOther) File.WriteAllLines(otherPath, otherLines);
            }

            if (mod.Color == new nuVector4(1, 1, 1, 1))
            {
                if (fileName == "GoodMods.txt") mod.Color = new nuVector4(0.4f, 1f, 0.4f, 1f);
                else if (fileName == "BadMods.txt") mod.Color = new nuVector4(1f, 0.4f, 0.4f, 1f);
            }
            var path = Path.Combine(ConfigDirectory, fileName);
            var hexColor = ToHex(mod.Color);
            var newLine = $"{mod.RawName};{mod.DisplayName};{hexColor};{mod.IsBricking}";

            if (!File.Exists(path)) File.WriteAllText(path, "");
            var lines = File.ReadAllLines(path).ToList();
            lines.RemoveAll(l =>
            {
                var configKey = l.Split(';')[0].Trim();
                if (string.IsNullOrEmpty(configKey) || configKey.StartsWith("#")) return false;
                return mod.RawName.IndexOf(configKey, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       configKey.IndexOf(mod.RawName, System.StringComparison.OrdinalIgnoreCase) >= 0;
            });
            lines.Add(newLine);
            File.WriteAllLines(path, lines);
            GoodModsDictionary = LoadConfigGoodMod();
            BadModsDictionary = LoadConfigBadMod();
            LogMessage($"Saved/Updated mod: {mod.RawName}", 5);
        }

        private void DeleteModFromConfig(string rawName)
        {
            string[] files = { "GoodMods.txt", "BadMods.txt" };
            bool deleted = false;
            foreach (var fileName in files)
            {
                var path = Path.Combine(ConfigDirectory, fileName);
                if (!File.Exists(path)) continue;
                var lines = File.ReadAllLines(path).ToList();
                var lineToRemove = lines.FirstOrDefault(l =>
                {
                    var configKey = l.Split(';')[0].Trim();
                    if (string.IsNullOrEmpty(configKey) || configKey.StartsWith("#")) return false;
                    return rawName.IndexOf(configKey, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           configKey.IndexOf(rawName, System.StringComparison.OrdinalIgnoreCase) >= 0;
                });
                if (lineToRemove != null)
                {
                    lines.Remove(lineToRemove);
                    File.WriteAllLines(path, lines);
                    LogMessage($"Deleted mod entry: {lineToRemove.Split(';')[0]} from {fileName}", 5);
                    deleted = true;
                }
            }
            if (deleted) { GoodModsDictionary = LoadConfigGoodMod(); BadModsDictionary = LoadConfigBadMod(); }
            else LogError($"Could not find {rawName} in GoodMods.txt or BadMods.txt", 5);
        }
    }
}
