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
                   path.Contains("ExpeditionLogbook") ||
                   path.Contains("Maven");
        }

        private List<NormalInventoryItem> GetItemsFromCollection(IEnumerable<NormalInventoryItem> items)
        {
            var result = new List<NormalInventoryItem>();
            if (items == null) return result;
            var seenAddresses = new HashSet<long>();
            foreach (var it in items)
            {
                if (it?.Item != null && ItemIsMap(it.Item) && seenAddresses.Add(it.Item.Address))
                    result.Add(it);
            }
            return result;
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
            var item = element.AsObject<NormalInventoryItem>();
            if (item?.Item != null && item.Address != 0)
            {
                if (ItemIsMap(item.Item) && seenAddresses.Add(item.Address))
                    result.Add(item);
            }

            int childCount = (int)element.ChildCount;
            if (childCount > 0 && childCount < 1000)
            {
                for (int i = 0; i < childCount; i++)
                {
                    var child = element.GetChildAtIndex(i);
                    if (child != null) FindMapsInElementRecursive(child, result, seenAddresses, depth + 1);
                }
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
            if (ui.ChildCount > 67)
            {
                var mapDeviceRoot = ui.GetChildAtIndex(67);
                if (mapDeviceRoot != null && mapDeviceRoot.IsVisible)
                {
                    // Maven Invitation Slot (Path: 67 -> 8 -> 1)
                    if (mapDeviceRoot.ChildCount > 8)
                    {
                        var invitationSlot = mapDeviceRoot.GetChildAtIndex(8)?.GetChildAtIndex(1);
                        var item = invitationSlot?.AsObject<NormalInventoryItem>();
                        if (item?.Item != null && ItemIsMap(item.Item))
                            result.Add(item);
                    }

                    // Standard Map Device Slots (Path: 67 -> 7)
                    if (mapDeviceRoot.ChildCount > 7)
                    {
                        var piecesPanel = mapDeviceRoot.GetChildAtIndex(7);
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

            // ExileCore's HeistLockerElement is a stub (returns 'this'), so we must find it manually.
            // We try index 98 first, but fallback to a search if the child doesn't look like a locker.
            var heistLocker = ui.ChildCount > 98 ? ui.GetChildAtIndex(98) : null;
            if (heistLocker == null || !heistLocker.IsVisible || heistLocker.ChildCount < 10)
            {
                // Search for a visible child that has the expected category/tab structure (Heist usually has 24-45 children)
                heistLocker = ui.Children.FirstOrDefault(x => x.IsVisible && x.ChildCount >= 24 && x.ChildCount <= 45);
            }

            if (heistLocker != null && heistLocker.IsVisible)
            {
                var seenAddresses = new HashSet<long>();
                // Contracts are stored in sub-containers (indices 7-24 represent the job tabs)
                for (int i = 7; i <= 24; i++)
                {
                    var container = heistLocker.GetChildAtIndex(i);
                    if (container != null && container.IsVisible) FindMapsInElementRecursive(container, result, seenAddresses, 0);
                }
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
            var expeditionLocker = (ui.ChildCount > 101 && ui.GetChildAtIndex(101).IsVisible) ? ui.GetChildAtIndex(101) :
                                   (ui.ChildCount > 103 && ui.GetChildAtIndex(103).IsVisible) ? ui.GetChildAtIndex(103) :
                                   (ui.ChildCount > 102 && ui.GetChildAtIndex(102).IsVisible) ? ui.GetChildAtIndex(102) : 
                                   (ui.ChildCount > 104 && ui.GetChildAtIndex(104).IsVisible) ? ui.GetChildAtIndex(104) : null;
            
            if (expeditionLocker == null || !expeditionLocker.IsVisible) // Expedition Locker usually has 10-30 children
                expeditionLocker = ui.Children.FirstOrDefault(x => x.IsVisible && x.ChildCount >= 10 && x.ChildCount <= 35);
            
            if (expeditionLocker != null && expeditionLocker.IsVisible)
            {
                var seenAddresses = new HashSet<long>();
                // The Logbook tab index can vary. Based on input, check index 26 and fallback to index 5.
                var logbookTab = expeditionLocker.ChildCount > 26 ? expeditionLocker.GetChildAtIndex(26) : 
                                 expeditionLocker.ChildCount > 5 ? expeditionLocker.GetChildAtIndex(5) : null;

                if (logbookTab != null && logbookTab.IsVisible)
                {
                    FindMapsInElementRecursive(logbookTab, result, seenAddresses, 0);
                }
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
                var currentTabContainer = window.GetChildAtIndex(8)?.GetChildAtIndex(1);
                if (currentTabContainer != null)
                    foreach (var tab in currentTabContainer.Children)
                        if (tab.IsVisible && tab.ChildCount > 0 && tab.GetChildAtIndex(0) != null)
                            FindMapsInElementRecursive(tab.GetChildAtIndex(0), result, seenAddresses, 0);
            }
            return result;
        }
    }
}