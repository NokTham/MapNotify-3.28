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
                   path.Contains("Valdo", StringComparison.OrdinalIgnoreCase);
        }

        private List<NormalInventoryItem> GetItemsFromCollection(IEnumerable<NormalInventoryItem> items)
        {
            if (items == null) return new List<NormalInventoryItem>();
            return items.Where(it => it?.Item != null && ItemIsMap(it.Item))
                        .GroupBy(it => it.Item.Address)
                        .Select(g => g.First())
                        .ToList();
        }

        private List<NormalInventoryItem> GetInventoryItems()
        {
            if (ingameState?.IngameUi?.InventoryPanel?.IsVisible == true)
            {
                return GetItemsFromCollection(ingameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory]?.VisibleInventoryItems);
            }
            return new List<NormalInventoryItem>();
        }

        private void FindMapsInElementRecursive(Element element, List<NormalInventoryItem> result, HashSet<long> seenAddresses, int depth)
        {
            if (element == null || !element.IsVisible || depth > 20) return;
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
                        if (child != null) FindMapsInElementRecursive(child, result, seenAddresses, depth + 1);
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

        private List<NormalInventoryItem> GetMapDeviceItems()
        {
            var result = new List<NormalInventoryItem>();
            var ui = ingameState?.IngameUi;
            if (ui == null) return result;

            // Reverting to manual path traversal as MapReceptacleWindow is not accessible.
            // Root is verified at Index 67.
            if (ui.ChildCount > UIIndices.MapDeviceRoot)
            {
                var mapDeviceRoot = ui.GetChildAtIndex(UIIndices.MapDeviceRoot);
                if (mapDeviceRoot != null && mapDeviceRoot.IsVisible)
                {
                    const int InvitationSlotParentIndex = 8;
                    const int StandardSlotsParentIndex = 7;

                    if (mapDeviceRoot.ChildCount > InvitationSlotParentIndex)
                    {
                        var slot8 = mapDeviceRoot.GetChildAtIndex(InvitationSlotParentIndex);
                        var invitationSlot = slot8 != null && slot8.ChildCount > 1 ? slot8.GetChildAtIndex(1) : null;
                        var item = invitationSlot?.AsObject<NormalInventoryItem>();
                        if (item?.Item != null && ItemIsMap(item.Item))
                            result.Add(item);
                    }

                    if (mapDeviceRoot.ChildCount > StandardSlotsParentIndex)
                    {
                        var piecesPanel = mapDeviceRoot.GetChildAtIndex(StandardSlotsParentIndex);
                        if (piecesPanel != null && piecesPanel.IsVisible)
                        {
                            foreach (var child in piecesPanel.Children)
                            {
                                var item = child?.AsObject<NormalInventoryItem>();
                                if (item?.Item != null && ItemIsMap(item.Item))
                                    result.Add(item);
                            }
                        }
                    }
                }
            }
            return result;
        }

        private List<NormalInventoryItem> GetMerchantItems()
        {
            var merchantPanel = ingameState?.IngameUi?.OfflineMerchantPanel;
            return (merchantPanel != null && merchantPanel.IsVisible) 
                ? GetItemsFromCollection(merchantPanel.VisibleStash?.VisibleInventoryItems) 
                : new List<NormalInventoryItem>();
        }

        private List<NormalInventoryItem> GetHeistLockerItems()
        {
            var result = new List<NormalInventoryItem>();
            if (!Settings.ShowHeistLockerHighlights.Value) return result;

            var ui = ingameState?.IngameUi;
            if (ui == null) return result;

            // Try known index first, then fallback to robust search
            var heistLocker = ui.ChildCount > UIIndices.HeistLockerDefault ? ui.GetChildAtIndex(UIIndices.HeistLockerDefault) : null;
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
            int[] possibleExpeditionIndices = { UIIndices.ExpeditionLockerDefault, 102, 103, 104 };
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

        private List<NormalInventoryItem> GetPurchaseWindowItems()
        {
            var ui = ingameState?.IngameUi;
            if (ui == null) return new List<NormalInventoryItem>();
            Element window = ui.PurchaseWindow?.IsVisible == true ? ui.PurchaseWindow :
                             ui.PurchaseWindowHideout?.IsVisible == true ? ui.PurchaseWindowHideout :
                             ui.HaggleWindow?.IsVisible == true ? ui.HaggleWindow : null;
            
            var tradeWindow = ui.TradeWindow;
            if (window == null && tradeWindow.IsVisible) window = tradeWindow;
            if (window == null) return new List<NormalInventoryItem>();

            var result = new List<NormalInventoryItem>();
            if (window is TradeWindow trade)
            {
                result.AddRange(GetItemsFromCollection(trade.YourOffer));
                result.AddRange(GetItemsFromCollection(trade.OtherOffer));
            }
            else
            {
                var seenAddresses = new HashSet<long>();
                try
                {
                    var children = window.Children;
                    var child8 = children.Count > 8 ? children[8] : null;
                    var tabChildren = child8?.Children;
                    var currentTabContainer = tabChildren != null && tabChildren.Count > 1 ? tabChildren[1] : null;
                    if (currentTabContainer != null)
                    {
                        foreach (var tab in currentTabContainer.Children)
                            if (tab.IsVisible && tab.ChildCount > 0)
                                FindMapsInElementRecursive(tab.GetChildAtIndex(0), result, seenAddresses, 0);
                    }
                }
                catch { }
            }
            return result;
        }
    }
}