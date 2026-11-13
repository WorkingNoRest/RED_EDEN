using System.Linq;
using System.Collections.Generic;
using DevionGames.InventorySystem;
using DevionGames.UIWidgets;
using UnityEngine;

namespace RedEden.Crafting
{
    internal static class RedEdenInventoryBridge
    {
        private static readonly HashSet<string> ReportedMissingDependencies = new HashSet<string>();

        public static ItemContainer FindInventory(string windowName)
        {
            if (InventoryManager.current == null)
            {
                ReportMissing("InventoryManager", "RED EDEN crafting requires an active InventoryManager.");
                return null;
            }

            ItemContainer inventory = WidgetUtility.Find<ItemContainer>(windowName);
            if (inventory == null)
                ReportMissing(windowName, "RED EDEN crafting could not find the inventory window '" + windowName + "'.");
            else
                ReportedMissingDependencies.Remove(windowName);
            return inventory;
        }

        private static void ReportMissing(string key, string message)
        {
            if (ReportedMissingDependencies.Add(key))
                Debug.LogError(message);
        }

        public static bool HasIngredients(ItemContainer inventory, RedEdenCraftingRecipe recipe)
        {
            foreach (IGrouping<string, RedEdenRecipeIngredient> group in recipe.Ingredients
                .GroupBy(ingredient => ingredient.Resource.InventoryItem.Id))
            {
                RedEdenRecipeIngredient first = group.First();
                if (!inventory.HasItem(first.Resource.InventoryItem, group.Sum(ingredient => ingredient.Amount)))
                    return false;
            }
            return true;
        }

        public static bool TryConsumeIngredients(ItemContainer inventory, RedEdenCraftingRecipe recipe)
        {
            if (!HasIngredients(inventory, recipe))
                return false;

            List<IngredientAmount> consumed = new List<IngredientAmount>();
            foreach (IGrouping<string, RedEdenRecipeIngredient> group in recipe.Ingredients
                .GroupBy(ingredient => ingredient.Resource.InventoryItem.Id))
            {
                RedEdenRecipeIngredient first = group.First();
                int amount = group.Sum(ingredient => ingredient.Amount);
                if (inventory.RemoveItem(first.Resource.InventoryItem, amount))
                {
                    consumed.Add(new IngredientAmount(first, amount));
                    continue;
                }

                for (int i = consumed.Count - 1; i >= 0; i--)
                {
                    IngredientAmount removed = consumed[i];
                    Item restored = Object.Instantiate(removed.Ingredient.Resource.InventoryItem);
                    restored.Stack = removed.Amount;
                    if (!TryAdd(inventory, restored, removed.Amount))
                    {
                        Debug.LogError("RED EDEN failed to roll back consumed crafting ingredients. Inventory state may require recovery.");
                        Object.Destroy(restored);
                    }
                    else
                    {
                        Object.Destroy(restored);
                    }
                }
                return false;
            }
            return true;
        }

        private sealed class IngredientAmount
        {
            public readonly RedEdenRecipeIngredient Ingredient;
            public readonly int Amount;

            public IngredientAmount(RedEdenRecipeIngredient ingredient, int amount)
            {
                Ingredient = ingredient;
                Amount = amount;
            }
        }

        public static bool CanReceive(ItemContainer inventory, Item definition, int amount)
        {
            if (inventory == null || definition == null || amount < 1)
                return false;

            int existingCapacity = inventory.GetItems(definition.Id)
                .Where(item => item != null && item.Rarity == definition.Rarity)
                .Sum(item => Mathf.Max(0, item.MaxStack - item.Stack));
            int remaining = Mathf.Max(0, amount - existingCapacity);
            if (remaining == 0)
                return true;

            Item testItem = Object.Instantiate(definition);
            testItem.Stack = 1;
            int maxStack = Mathf.Max(1, testItem.MaxStack);
            foreach (Slot slot in inventory.Slots)
            {
                if (!slot.IsEmpty || !inventory.CanAddItem(slot.Index, testItem))
                    continue;

                remaining -= maxStack;
                if (remaining <= 0)
                {
                    Object.Destroy(testItem);
                    return true;
                }
            }

            Object.Destroy(testItem);
            return false;
        }

        public static bool TryAdd(ItemContainer inventory, Item definition, int amount)
        {
            if (!CanReceive(inventory, definition, amount))
                return false;

            Dictionary<Item, int> originalStacks = inventory.GetItems(definition.Id)
                .Where(item => item != null && item.Rarity == definition.Rarity)
                .Distinct()
                .ToDictionary(item => item, item => item.Stack);
            HashSet<Item> originalItems = new HashSet<Item>(originalStacks.Keys);
            List<Item> insertedItems = new List<Item>();
            int maxStack = Mathf.Max(1, definition.MaxStack);
            int committed = 0;
            while (committed < amount)
            {
                Item instance = Object.Instantiate(definition);
                instance.Stack = Mathf.Min(maxStack, amount - committed);
                int stackAmount = instance.Stack;
                if (!inventory.StackOrAdd(instance))
                {
                    Object.Destroy(instance);
                    foreach (KeyValuePair<Item, int> original in originalStacks)
                        original.Key.Stack = original.Value;

                    foreach (Item item in inventory.GetItems(definition.Id).ToArray())
                    {
                        if (item != null && item.Rarity == definition.Rarity && !originalItems.Contains(item))
                        {
                            inventory.RemoveItem(item);
                            Object.Destroy(item);
                        }
                    }
                    foreach (Item inserted in insertedItems)
                    {
                        if (inserted != null)
                        {
                            inventory.RemoveItem(inserted);
                            Object.Destroy(inserted);
                        }
                    }
                    return false;
                }
                committed += stackAmount;
                if (instance.Slot == null && instance.ReferencedSlots.Count == 0)
                    Object.Destroy(instance);
                else
                    insertedItems.Add(instance);
            }
            return true;
        }
    }
}
