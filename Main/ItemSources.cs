using System;
using System.Collections.Generic;
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
            if (stashElement?.IsVisible == true && stashElement.VisibleStash != null &&
                stashElement.VisibleStash.InvType != InventoryType.MapStash && Settings.FilterStash.Value)
            {
                FindMapsInElement(stashElement, result);
            }
            return (stashElement != null ? (int)stashElement.IndexVisibleStash : -1, result);
        }

        private (int stashIndex, List<NormalInventoryItem>) GetMapStashItems()
        {
            var result = new List<NormalInventoryItem>();
            var stashElement = ingameState?.IngameUi?.StashElement;
            if (stashElement?.IsVisible == true && stashElement.VisibleStash != null &&
                stashElement.VisibleStash.InvType == InventoryType.MapStash && Settings.FilterMapStash.Value)
            {
                var mapContainer = stashElement.GetChildAtIndex(2)?.GetChildAtIndex(0)?.GetChildAtIndex(0)?
                                                .GetChildAtIndex(1)?.GetChildAtIndex(1)?.GetChildAtIndex(2)?
                                                .GetChildAtIndex(0)?.GetChildAtIndex(4);
                FindMapsInElement(mapContainer ?? stashElement, result);
            }
            return (stashElement != null ? (int)stashElement.IndexVisibleStash : -1, result);
        }

        private void FindMapsInElement(Element element, List<NormalInventoryItem> result)
        {
            var seenAddresses = new HashSet<long>();
            FindMapsInElementRecursive(element, result, seenAddresses, 0);
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
            var ui = ingameState?.IngameUi;
            if (ui == null || ui.ChildCount <= 98) return result;
            var heistLocker = ui.GetChildAtIndex(98);
            if (heistLocker?.IsVisible == true && Settings.FilterStash.Value)
            {
                var seenAddresses = new HashSet<long>();
                for (int i = 7; i <= 24; i++)
                {
                    var container = heistLocker.GetChildAtIndex(i);
                    if (container != null && container.IsVisible) FindMapsInElementRecursive(container, result, seenAddresses, 0);
                }
            }
            return result;
        }

        private List<NormalInventoryItem> GetPurchaseWindowItems()
        {
            var ui = ingameState?.IngameUi;
            if (ui == null) return new List<NormalInventoryItem>();
            Element window = ui.PurchaseWindow?.IsVisible == true ? ui.PurchaseWindow :
                             ui.PurchaseWindowHideout?.IsVisible == true ? ui.PurchaseWindowHideout :
                             ui.HaggleWindow?.IsVisible == true ? ui.HaggleWindow : null;
            var tradeWindow = ui.ChildCount > 108 ? ui.GetChildAtIndex(108) : null;
            if (window == null && tradeWindow?.IsVisible == true) window = tradeWindow;
            if (window == null) return new List<NormalInventoryItem>();

            var result = new List<NormalInventoryItem>();
            var seenAddresses = new HashSet<long>();
            if (window == tradeWindow)
            {
                var tradeRoot = window.GetChildAtIndex(3)?.GetChildAtIndex(1)?.GetChildAtIndex(0)?.GetChildAtIndex(0);
                if (tradeRoot != null) FindMapsInElementRecursive(tradeRoot, result, seenAddresses, 0);
            }
            else
            {
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