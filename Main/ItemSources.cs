using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;

namespace MapNotify_3_28
{
    public partial class MapNotify_3_28
    {
        // Diagnostics for item discovery. Only written to the verbose log, at most
        // once per second, and only when the message changes, so a missing highlight
        // can be traced to discovery vs. drawing without attaching a debugger.
        private long _lastSourceDiagMs;
        private string _lastSourceDiagMessage;

        private bool SourceDiagnosticsDue()
        {
            return Environment.TickCount64 - _lastSourceDiagMs >= 1000;
        }

        private void LogSourceDiagnostics(string message)
        {
            var now = Environment.TickCount64;
            if (now - _lastSourceDiagMs < 1000) return;
            if (message == _lastSourceDiagMessage) return;

            _lastSourceDiagMs = now;
            _lastSourceDiagMessage = message;
            LogMessage($"MapNotify: {message}", 5);
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
                   path.Contains("ExpeditionLogbook", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("Maven", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("Valdo", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("Metadata/Items/MapFragments/Primordial/Currency", StringComparison.Ordinal);
        }

        private List<NormalInventoryItem> GetItemsFromCollection(IEnumerable<NormalInventoryItem> items)
        {
            if (items == null) return new List<NormalInventoryItem>();
            var result = new List<NormalInventoryItem>();
            var seen = new HashSet<long>();
            foreach (var it in items)
            {
                if (it?.Item != null && ItemIsMap(it.Item) && seen.Add(it.Item.Address))
                    result.Add(it);
            }
            return result;
        }

        private List<NormalInventoryItem> GetInventoryItems()
        {
            if (Settings.FilterInventory.Value && ingameState?.IngameUi?.InventoryPanel?.IsVisible == true)
            {
                var inventoryPanel = ingameState.IngameUi.InventoryPanel;
                var collection = inventoryPanel[InventoryIndex.PlayerInventory]?.VisibleInventoryItems;
                var result = GetItemsFromCollection(collection);
                int directCount = result.Count;

                // 3.30: VisibleInventoryItems can come back empty while the panel is
                // open. Fall back to walking the UI tree, like the stash and locker
                // scans already do, so inventory highlights keep working.
                if (result.Count == 0)
                    FindMapsInElement(inventoryPanel, result);

                if (SourceDiagnosticsDue())
                    LogSourceDiagnostics(
                        $"inventory panelVisible={inventoryPanel.IsVisible} collection={(collection == null ? -1 : collection.Count)} direct={directCount} maps={result.Count}"
                    );

                return result;
            }
            return new List<NormalInventoryItem>();
        }

        private void FindMapsInElementRecursive(Element element, List<NormalInventoryItem> result, HashSet<long> seenAddresses, int depth, bool requireVisible = true)
        {
            if (element == null || depth > 13) return;
            if (requireVisible && !element.IsVisible) return;
            try
            {
                var item = element.AsObject<NormalInventoryItem>();
                if (item?.Item != null && item.Address != 0)
                {
                    if (ItemIsMap(item.Item) && seenAddresses.Add(item.Address))
                        result.Add(item);
                }

                int count = (int)element.ChildCount;
                if (count > 0 && count < 500)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var child = element.GetChildAtIndex(i);
                        if (child != null) FindMapsInElementRecursive(child, result, seenAddresses, depth + 1, requireVisible);
                    }
                }
            }
            catch
            {
                // Ignore errors from specific UI elements that might be in an invalid state during recursion
            }
        }

        private (int stashIndex, List<NormalInventoryItem>) GetRegularStashItems()
        {
            var result = new List<NormalInventoryItem>();
            var stashElement = ingameState?.IngameUi?.StashElement;
            if (stashElement?.IsVisible == true && stashElement.VisibleStash != null && Settings.FilterStash.Value &&
                stashElement.VisibleStash.InvType != InventoryType.MapStash)
            {
                result = GetItemsFromCollection(stashElement.VisibleStash.VisibleInventoryItems);

                // Fallback: if the collection is empty, the API might not have populated it yet; try UI recursion.
                if (result.Count == 0) FindMapsInElement(stashElement, result);
            }
            return (stashElement != null ? (int)stashElement.IndexVisibleStash : -1, result);
        }

        private (int stashIndex, List<NormalInventoryItem>) GetMapStashItems()
        {
            var result = new List<NormalInventoryItem>();
            var stashElement = ingameState?.IngameUi?.StashElement;
            if (stashElement?.IsVisible == true && stashElement.VisibleStash != null)
            {
                // Use the standardized InvType check to avoid misidentifying other specialized tabs.
                var isMapStash = stashElement.VisibleStash.InvType == InventoryType.MapStash;

                if (isMapStash && Settings.FilterMapStash.Value)
                {
                    result = GetItemsFromCollection(stashElement.VisibleStash.VisibleInventoryItems);

                    // Fallback to searching the specialized UI container (Index 3) for Map Stashes.
                    if (result.Count == 0) FindMapsInElement(stashElement.ChildCount > 3 ? stashElement.GetChildAtIndex(3) : stashElement, result);
                }
            }
            return (stashElement != null ? (int)stashElement.IndexVisibleStash : -1, result);
        }

        private void FindMapsInElement(Element element, List<NormalInventoryItem> result)
        {
            var seenAddresses = new HashSet<long>();
            FindMapsInElementRecursive(element, result, seenAddresses, 0);
        }

        private void AddMapDeviceSlotItems(IEnumerable<NormalInventoryItem> items, List<NormalInventoryItem> result, HashSet<long> seen)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item?.Item != null && item.Address != 0 && ItemIsMap(item.Item) && seen.Add(item.Address))
                    result.Add(item);
            }
        }

        private List<NormalInventoryItem> GetMapDeviceItems()
        {
            var result = new List<NormalInventoryItem>();
            if (!Settings.ShowForInvitations.Value && !Settings.FilterTrade.Value) return result;
            var ui = ingameState?.IngameUi;
            if (ui == null) return result;

            var seen = new HashSet<long>();

            // Preferred: typed accessors, which survive the UI layout reshuffles that keep
            // invalidating raw child indices. Guarded because they are best-effort only.
            try
            {
                var window = ui.MapDeviceWindow;
                if (window != null)
                {
                    AddMapDeviceSlotItems(window.Items, result, seen);
                    AddMapDeviceSlotItems(window.MapSlot?.VisibleInventoryItems, result, seen);

                    var scarabSlots = window.ScarabSlots;
                    if (scarabSlots != null)
                    {
                        foreach (var slot in scarabSlots)
                            AddMapDeviceSlotItems(slot?.VisibleInventoryItems, result, seen);
                    }
                }
            }
            catch
            {
                // Ignore typed-accessor failures; the traversal below is the source of truth.
            }

            // Source of truth: walk the whole map device root instead of fixed child indices
            // (the slot layout moved from 67 -> 8/7 to 76 -> 10 -> 1).
            var mapDeviceRoot = ui.ChildCount > Constants.UIIndices.MapDeviceRoot
                ? ui.GetChildAtIndex(Constants.UIIndices.MapDeviceRoot)
                : null;
            if (mapDeviceRoot != null && mapDeviceRoot.IsVisible)
                FindMapsInElementRecursive(mapDeviceRoot, result, seen, 0, requireVisible: false);

            return result;
        }

        private List<NormalInventoryItem> GetMerchantItems()
        {
            if (!Settings.FilterShops.Value) return new List<NormalInventoryItem>();

            var merchantPanel = ingameState?.IngameUi?.OfflineMerchantPanel;
            if (merchantPanel == null || !merchantPanel.IsVisible)
                return new List<NormalInventoryItem>();

            var result = GetItemsFromCollection(merchantPanel.VisibleStash?.VisibleInventoryItems);
            int directCount = result.Count;

            // 3.30: same empty-collection fallback as the inventory scan.
            if (result.Count == 0)
                FindMapsInElement(merchantPanel, result);

            if (SourceDiagnosticsDue())
                LogSourceDiagnostics($"merchant direct={directCount} maps={result.Count}");

            return result;
        }

        private List<NormalInventoryItem> GetHeistLockerItems()
        {
            var result = new List<NormalInventoryItem>();
            if (!Settings.ShowHeistLockerHighlights.Value) return result;

            var ui = ingameState?.IngameUi;
            if (ui == null) return result;

            // Try known index first, then fallback to robust search
            var heistLocker = ui.ChildCount > Constants.UIIndices.HeistLockerDefault ? ui.GetChildAtIndex(Constants.UIIndices.HeistLockerDefault) : null;
            if (heistLocker == null || !heistLocker.IsVisible || heistLocker.ChildCount < 10)
            {
                for (int i = 0; i < ui.ChildCount; i++)
                {
                    var child = ui.GetChildAtIndex(i);
                    if (child != null && child.IsVisible && child.ChildCount is >= 24 and <= 45)
                    {
                        heistLocker = child;
                        break;
                    }
                }
            }

            if (heistLocker != null && heistLocker.IsVisible)
            {
                var seenAddresses = new HashSet<long>();
                try
                {
                    var count = heistLocker.ChildCount;
                    for (int i = 7; i <= 24; i++)
                    {
                        if (i >= count) break;
                        var container = heistLocker.GetChildAtIndex(i);
                        if (container != null && container.IsVisible) FindMapsInElementRecursive(container, result, seenAddresses, 0);
                    }
                }
                catch { }
                if (result.Count == 0) FindMapsInElementRecursive(heistLocker, result, seenAddresses, 0);
            }

            // Safety check: Ensure the Heist Locker scanner only returns Heist items to avoid crosstalk with other lockers
            result.RemoveAll(x => !x.Item.HasComponent<HeistContract>() && !x.Item.HasComponent<HeistBlueprint>());

            return result;
        }

        private List<NormalInventoryItem> GetExpeditionLockerItems()
        {
            var result = new List<NormalInventoryItem>();
            if (!Settings.ShowExpeditionLockerHighlights.Value) return result;

            var ui = ingameState?.IngameUi;
            if (ui == null) return result;

            // Robust search for Expedition Locker (Commonly at 101, 102, 103, or 104)
            int[] possibleExpeditionIndices = { Constants.UIIndices.ExpeditionLockerDefault, 102, 103, 104 };
            var expeditionLocker = possibleExpeditionIndices
                .Where(idx => ui.ChildCount > idx)
                .Select(ui.GetChildAtIndex)
                .FirstOrDefault(c => c is { IsVisible: true });

            if (expeditionLocker == null || !expeditionLocker.IsVisible) // Expedition Locker usually has 10-30 children
            {
                for (int i = 0; i < ui.ChildCount; i++)
                {
                    var child = ui.GetChildAtIndex(i);
                    if (child != null && child.IsVisible && child.ChildCount is >= 10 and <= 35)
                    {
                        expeditionLocker = child;
                        break;
                    }
                }
            }

            if (expeditionLocker != null && expeditionLocker.IsVisible)
            {
                var seenAddresses = new HashSet<long>();
                try
                {
                    var count = expeditionLocker.ChildCount;
                    var logbookTab = count > 26 ? expeditionLocker.GetChildAtIndex(26) :
                                     count > 5 ? expeditionLocker.GetChildAtIndex(5) : null;

                    if (logbookTab != null && logbookTab.IsVisible)
                    {
                        FindMapsInElementRecursive(logbookTab, result, seenAddresses, 0);
                    }
                }
                catch { }
                if (result.Count == 0) FindMapsInElementRecursive(expeditionLocker, result, seenAddresses, 0);
            }

            // Safety check: Ensure the Expedition Locker scanner only returns Logbooks to avoid crosstalk with other lockers
            result.RemoveAll(x => !x.Item.Path.Contains("ExpeditionLogbook"));

            return result;
        }

        private List<NormalInventoryItem> GetMapDeviceStorageItems()
        {
            var result = new List<NormalInventoryItem>();
            if (!Settings.ShowMapDeviceStorageHighlights.Value) return result;

            var ui = ingameState?.IngameUi;
            if (ui == null) return result;

            var atlasPanel = ui.Atlas?.IsVisible == true ? ui.Atlas : ui.GetChildAtIndex(Constants.UIIndices.AtlasPanel);
            if (atlasPanel == null || !atlasPanel.IsVisible) return result;

            var seenAddresses = new HashSet<long>();

            // Path 1: (Atlas/AtlasPanel)29->3->0->1->X (where X is 1 to 20)
            var sidebar = atlasPanel.GetChildAtIndex(3)?.GetChildAtIndex(0)?.GetChildAtIndex(1);
            if (sidebar != null && sidebar.IsVisible)
            {
                for (int i = 1; i <= 20; i++)
                {
                    var container = sidebar.GetChildAtIndex(i);
                    if (container != null && container.IsVisible)
                        FindMapsInElementRecursive(container, result, seenAddresses, 0);
                }
            }

            // Path 2: (Atlas/AtlasPanel)30->7->(MapDeviceWindow)->0->2->0->1
            // This is the Map Device window embedded in the Atlas panel, so it overlaps with
            // GetMapDeviceItems when the typed MapDeviceWindow accessor is available.
            // RenderMapDevice de-duplicates the two sources by entity address.
            var internalDevice = atlasPanel.GetChildAtIndex(7)?.GetChildAtIndex(0)?.GetChildAtIndex(2)?.GetChildAtIndex(0)?.GetChildAtIndex(1);
            if (internalDevice != null && internalDevice.IsVisible)
            {
                FindMapsInElementRecursive(internalDevice, result, seenAddresses, 0);
            }

            return result;
        }

        private List<NormalInventoryItem> GetPurchaseWindowItems()
        {
            var ui = ingameState?.IngameUi;
            if (ui == null) return new List<NormalInventoryItem>();
            if (!Settings.FilterShops.Value && !Settings.FilterTrade.Value)
                return new List<NormalInventoryItem>();

            // Player trade is its own window; the vendor windows below are gated on
            // either toggle so enabling only one of them keeps working.
            var tradeWindow = ui.TradeWindow;
            if (Settings.FilterTrade.Value && tradeWindow?.IsVisible == true)
            {
                var tradeResult = GetItemsFromCollection(tradeWindow.YourOffer);
                tradeResult.AddRange(GetItemsFromCollection(tradeWindow.OtherOffer));
                if (tradeResult.Count == 0)
                    FindMapsInElement(tradeWindow, tradeResult);
                return tradeResult;
            }

            // Typed accessors instead of fixed child indices: the vendor layout moved
            // between patches and the old children[8]->children[1] path silently
            // returned nothing. PurchaseWindow also covers the hideout variant
            // (PurchaseWindowHideout is just an obsolete alias for it).
            Element window = null;
            var result = new List<NormalInventoryItem>();
            try
            {
                var purchaseWindow = ui.PurchaseWindow;
                if (purchaseWindow?.IsVisible == true)
                {
                    window = purchaseWindow;
                    result = GetItemsFromCollection(
                        purchaseWindow.TabContainer?.VisibleStash?.VisibleInventoryItems
                    );
                }
                else
                {
                    var haggleWindow = ui.HaggleWindow;
                    if (haggleWindow?.IsVisible == true)
                    {
                        window = haggleWindow;
                        var haggleItems = haggleWindow.InventoryItems;
                        result = GetItemsFromCollection(haggleItems);
                    }
                }
            }
            catch
            {
                // Best-effort typed accessors; the traversal below is the source of truth.
            }

            if (window == null) return result;

            // Fallback: walk the whole window when the typed accessors produced nothing.
            if (result.Count == 0)
                FindMapsInElement(window, result);

            if (SourceDiagnosticsDue())
                LogSourceDiagnostics(
                    $"shop window={window.GetType().Name} maps={result.Count}"
                );

            return result;
        }
    }
}
