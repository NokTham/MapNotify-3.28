using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.RegularExpressions;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Helpers;
using SharpDX;

namespace MapNotify_3_28;

public partial class MapNotify_3_28
{
    private static readonly Regex MapNameRegex = new Regex(@"<[^>]*>{([^}]*)}", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> SpecialNodeMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // These nodes have different names in the Atlas UI vs ServerData, so we need to check both.
        { "Moment of Loneliness", "IgnoranceBoss" },
        { "Eye of the Storm", "SirusBoss" },
        { "The Shaper's Realm", "ShaperBoss" },
        { "The Unleashed Cortex", "UberCortexBoss" },
        { "Moment of Trauma", "FearBoss" },
        { "Moment of Reverence", "BenevolenceBoss" },
        { "Absence of Symmetry and Harmony", "EaterOfWorldsBoss" },
        { "Absence of Patience and Wisdom", "SearingExarchBoss" },
        { "Absence of Value and Meaning", "ElderBoss" },
        { "Absence of Mercy and Empathy", "MavenBoss" },
        { "Polaric Void", "BlackStarBoss" },
        { "Crux of Nothingness", "KingInTheMistsBoss" },
        { "The Apex of Sacrifice", "AtziriBoss" }
    };
    private CachedValue<HashSet<long>> _cachedDiscoveryAddresses;
    private CachedValue<Dictionary<string, long>> _cachedNameToNodeAddress;
    private CachedValue<HashSet<string>> _completedNames;
    private CachedValue<HashSet<string>> _bonusNames;
    private CachedValue<HashSet<string>> _witnessedNames;
    private int _discoveredAtlasOffset = -1;

    private void InitializeAtlasHighlighter()
    {
        _cachedNameToNodeAddress = new TimeCache<Dictionary<string, long>>(() =>
        {
            var map = new Dictionary<string, long>();
            foreach (var node in gameController.Files?.AtlasNodes?.EntriesList ?? Enumerable.Empty<AtlasNode>())
            {
                if (node == null || node.Address == 0) continue;
                var name = node.Area?.Name;
                var id = node.Id;
                if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name))
                    map[name] = node.Address;
                if (!string.IsNullOrEmpty(id) && !map.ContainsKey(id))
                    map[id] = node.Address;
            }
            return map;
        }, 5000); // Refresh every 5 seconds, or when Atlas is opened/closed.

        _cachedDiscoveryAddresses = new TimeCache<HashSet<long>>(() =>
        {
            var discoverySet = new HashSet<long>();
            foreach (var node in gameController.IngameState.ServerData.CompletedNodes ?? Enumerable.Empty<AtlasNode>())
                if (node?.Address != 0) discoverySet.Add(node.Address);
            foreach (var node in gameController.IngameState.ServerData.BonusCompletedNodes ?? Enumerable.Empty<AtlasNode>())
                if (node?.Address != 0) discoverySet.Add(node.Address);
            foreach (var node in gameController.Files?.AtlasNodes?.EntriesList ?? Enumerable.Empty<AtlasNode>())
                if (node?.Address != 0) discoverySet.Add(node.Address);
            return discoverySet;
        }, 5000);

        _completedNames = new TimeCache<HashSet<string>>(() =>
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var serverData = gameController?.IngameState?.ServerData;
            if (serverData?.CompletedNodes == null) return set;
            foreach (var n in serverData.CompletedNodes)
            {
                if (n == null) continue;
                if (!string.IsNullOrEmpty(n.Id)) set.Add(n.Id);
                if (!string.IsNullOrEmpty(n.Area?.Name)) set.Add(n.Area.Name);
            }
            return set;
        }, 3000);

        _bonusNames = new TimeCache<HashSet<string>>(() =>
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var serverData = gameController?.IngameState?.ServerData;
            if (serverData?.BonusCompletedNodes == null) return set;
            foreach (var n in serverData.BonusCompletedNodes)
            {
                if (n == null) continue;
                if (!string.IsNullOrEmpty(n.Id)) set.Add(n.Id);
                if (!string.IsNullOrEmpty(n.Area?.Name)) set.Add(n.Area.Name);
            }
            return set;
        }, 3000);

        _witnessedNames = new TimeCache<HashSet<string>>(() =>
        {
            var serverData = gameController?.IngameState?.ServerData;
            return serverData?.MavenWitnessedAreas?.Where(x => x != null).Select(x => x.Name).ToHashSet() ?? new HashSet<string>();
        }, 5000);
    }

    private void DrawAtlasHighlights()
    {
        var atlasPanel = ingameState?.IngameUi?.Atlas ?? ingameState?.IngameUi?.GetChildAtIndex(29);
        if (atlasPanel == null || !atlasPanel.IsVisible || atlasPanel.Address == 0) return;

        var nodesContainer = atlasPanel.GetChildAtIndex(0);
        if (nodesContainer == null || nodesContainer.ChildCount == 0) return;

        // Use Cached HashSets to prevent rebuilding collections every single frame (Performance Fix)
        var completedNames = _completedNames.Value;
        var bonusNames = _bonusNames.Value;
        var witnessedNames = _witnessedNames.Value;
        var nameToNodeAddress = _cachedNameToNodeAddress.Value;

        // Performance: Fetch settings once outside the loop
        var showAtlas = Settings.ShowAtlasHighlight.Value;
        var showBonus = Settings.ShowAtlasBonusHighlight.Value;
        var showMaven = Settings.ShowMavenWitnessHighlight.Value;
        if (!showAtlas && !showBonus && !showMaven) return;

        var atlasColor = Settings.AtlasNotCompletedColor.ToSharpColor();
        var bonusColor = Settings.AtlasBonusIncompleteColor.ToSharpColor();
        var mavenColor = Settings.MavenWitnessColor.ToSharpColor();
        var atlasRadius = Settings.AtlasHighlightRadius.Value;
        var bonusRadius = Settings.AtlasBonusHighlightRadius.Value;
        var mavenRadius = Settings.MavenWitnessHighlightRadius.Value;
        var thickness = Settings.BorderThicknessMap.Value;

        for (int i = 0; i < nodesContainer.ChildCount; i++)
        {
            var element = nodesContainer.GetChildAtIndex(i);
            if (element == null || element.Address == 0) continue;

            long atlasNodePtr = 0;
            string areaName = null;
            string nodeId = null;
            bool isCompleted = false;
            bool isBonusCompleted = false;
            bool isWitnessedByMemory = false;

            // Verify or discover the memory offset
            if (_discoveredAtlasOffset != -1)
            {
                atlasNodePtr = gameController.IngameState.M.Read<long>(element.Address + _discoveredAtlasOffset);
                if (atlasNodePtr != 0)
                {
                    var node = gameController.Game.GetObject<AtlasNode>(atlasNodePtr);
                    if (node == null || string.IsNullOrEmpty(node.Id))
                    {
                        atlasNodePtr = 0;
                        _discoveredAtlasOffset = -1;
                    }
                }
                else _discoveredAtlasOffset = -1;
            }

            // Lazy Tooltip Access: Only fetch if memory identification failed or we are in discovery
            string uiMapName = null;
            if (atlasNodePtr == 0 || _discoveredAtlasOffset == -1)
            {
                var tooltip = element.Tooltip;
                if (tooltip != null && tooltip.Address != 0)
                {
                    var nameElement = tooltip.GetChildAtIndex(1)?.GetChildAtIndex(0);
                    uiMapName = nameElement?.Text;

                    if (nameElement != null) {
                        uiMapName = nameElement.Text;
                        if (string.IsNullOrEmpty(uiMapName) && nameElement.ChildCount > 0)
                            uiMapName = nameElement.GetChildAtIndex(0)?.Text;
                    }

                    if (uiMapName != null && uiMapName.Length > 0 && uiMapName[0] == '<')
                    {
                        uiMapName = MapNameRegex.Replace(uiMapName, "$1");
                    }
                }
            }

            if (_discoveredAtlasOffset == -1 && !string.IsNullOrEmpty(uiMapName))
            {
                if (nameToNodeAddress.TryGetValue(uiMapName, out var targetAddress))
                {
                    int[] offsets = { 0x120, 0x110, 0x118, 0x128 };
                    foreach (var off in offsets)
                    {
                        if (gameController.IngameState.M.Read<long>(element.Address + off) == targetAddress)
                        {
                            atlasNodePtr = targetAddress;
                            _discoveredAtlasOffset = off;
                            break;
                        }
                    }
                }
            }

            if (atlasNodePtr != 0)
            {
                var node = gameController.Game.GetObject<AtlasNode>(atlasNodePtr);
                areaName = node?.Area?.Name;
                nodeId = node?.Id;

                isCompleted = (!string.IsNullOrEmpty(areaName) && completedNames.Contains(areaName)) || 
                             (!string.IsNullOrEmpty(nodeId) && completedNames.Contains(nodeId));
                isBonusCompleted = (!string.IsNullOrEmpty(areaName) && bonusNames.Contains(areaName)) || 
                                  (!string.IsNullOrEmpty(nodeId) && bonusNames.Contains(nodeId));
                isWitnessedByMemory = (!string.IsNullOrEmpty(areaName) && witnessedNames.Contains(areaName));
            }

            // Fallback for UI tooltips
            if (!string.IsNullOrEmpty(uiMapName))
            {
                if (completedNames.Contains(uiMapName)) isCompleted = true;
                if (bonusNames.Contains(uiMapName)) isBonusCompleted = true;
                if (witnessedNames.Contains(uiMapName)) isWitnessedByMemory = true;

                if (SpecialNodeMapping.TryGetValue(uiMapName, out var mappedId))
                {
                    if (completedNames.Contains(mappedId)) isCompleted = true;
                    if (bonusNames.Contains(mappedId)) isBonusCompleted = true;
                    if (witnessedNames.Contains(mappedId)) isWitnessedByMemory = true;
                }
            }

            string checkName = uiMapName ?? areaName;
            bool isWhitelisted = (!string.IsNullOrEmpty(checkName) && ExpectedNodes.Contains(checkName)) || 
                                (!string.IsNullOrEmpty(nodeId) && ExpectedNodes.Contains(nodeId));
            if (!isWhitelisted && !string.IsNullOrEmpty(uiMapName) && SpecialNodeMapping.TryGetValue(uiMapName, out var sId))
                isWhitelisted = ExpectedNodes.Contains(sId);

            if (!isWhitelisted) continue;

            bool isBonusPossible = (!string.IsNullOrEmpty(checkName) && ExpectedBonusNodes.Contains(checkName)) || 
                                  (!string.IsNullOrEmpty(nodeId) && ExpectedBonusNodes.Contains(nodeId));
            if (!isBonusPossible && !string.IsNullOrEmpty(uiMapName) && SpecialNodeMapping.TryGetValue(uiMapName, out var bId))
                isBonusPossible = ExpectedBonusNodes.Contains(bId);

            var center = element.GetClientRect().Center.ToVector2Num();
            if (showAtlas && !isCompleted)
                Graphics.DrawCircle(center, atlasRadius, atlasColor, thickness, 30);

            if (showBonus && isBonusPossible && !isBonusCompleted)
                Graphics.DrawCircle(center, bonusRadius, bonusColor, thickness, 30);

            if (showMaven && isWitnessedByMemory)
                Graphics.DrawCircle(center, mavenRadius, mavenColor, thickness, 30);
        }
    }
}