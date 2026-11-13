using System;
using System.Collections.Generic;
using System.Linq;
using DevionGames.InventorySystem;
using UnityEngine;

namespace RedEden.Crafting
{
    [CreateAssetMenu(menuName = "RED EDEN/Crafting/Database")]
    public sealed class RedEdenCraftingDatabase : ScriptableObject
    {
        [SerializeField] private List<RedEdenResourceDefinition> resources = new List<RedEdenResourceDefinition>();
        [SerializeField] private List<RedEdenCraftingRecipe> recipes = new List<RedEdenCraftingRecipe>();

        public IReadOnlyList<RedEdenResourceDefinition> Resources => resources;
        public IReadOnlyList<RedEdenCraftingRecipe> Recipes => recipes;

        public IEnumerable<RedEdenCraftingRecipe> GetRecipesForStation(string stationType)
        {
            return recipes.Where(recipe => recipe != null &&
                (string.IsNullOrWhiteSpace(recipe.StationType) ||
                 string.Equals(recipe.StationType, stationType, StringComparison.OrdinalIgnoreCase)));
        }

        public bool ContainsResource(RedEdenResourceDefinition resource)
        {
            return resource != null && resources.Contains(resource);
        }

        public bool ContainsRecipe(RedEdenCraftingRecipe recipe)
        {
            return recipe != null && recipes.Contains(recipe);
        }

        public RedEdenCraftingRecipe FindRecipe(string recipeId)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
                return null;

            RedEdenCraftingRecipe match = null;
            foreach (RedEdenCraftingRecipe recipe in recipes)
            {
                if (recipe == null || !string.Equals(recipe.Id, recipeId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (match != null)
                {
                    Debug.LogError("RED EDEN crafting database contains duplicate recipe ID '" + recipeId + "'.", this);
                    return null;
                }
                match = recipe;
            }
            return match;
        }

        public Item FindItemDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            Item match = null;
            foreach (RedEdenResourceDefinition resource in resources)
            {
                if (resource != null && resource.InventoryItem != null &&
                    string.Equals(resource.InventoryItem.Id, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    if (match != null && match != resource.InventoryItem)
                    {
                        Debug.LogError("RED EDEN crafting database resolves item ID '" + itemId + "' ambiguously.", this);
                        return null;
                    }
                    match = resource.InventoryItem;
                }
            }
            foreach (RedEdenCraftingRecipe recipe in recipes)
            {
                if (recipe != null && recipe.OutputItem != null &&
                    string.Equals(recipe.OutputItem.Id, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    if (match != null && match != recipe.OutputItem)
                    {
                        Debug.LogError("RED EDEN crafting database resolves item ID '" + itemId + "' ambiguously.", this);
                        return null;
                    }
                    match = recipe.OutputItem;
                }
            }
            return match;
        }

        private void OnValidate()
        {
            if (resources == null || recipes == null)
                return;

            IEnumerable<string> duplicateIds = resources
                .Where(resource => resource != null && !string.IsNullOrWhiteSpace(resource.Id))
                .GroupBy(resource => resource.Id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (string duplicateId in duplicateIds)
                Debug.LogWarning("RED EDEN resource ID '" + duplicateId + "' is duplicated in " + name + ".", this);

            foreach (RedEdenResourceDefinition resource in resources)
            {
                if (resource == null)
                    Debug.LogWarning("RED EDEN crafting database contains an empty resource reference.", this);
                else if (string.IsNullOrWhiteSpace(resource.Id) || resource.InventoryItem == null)
                    Debug.LogWarning("RED EDEN resource '" + resource.name + "' needs a stable ID and an inventory item.", resource);
            }

            foreach (RedEdenCraftingRecipe recipe in recipes)
            {
                if (recipe == null)
                {
                    Debug.LogWarning("RED EDEN crafting database contains an empty recipe reference.", this);
                    continue;
                }

                if (!recipe.IsValid(out string reason))
                    Debug.LogWarning("RED EDEN recipe '" + recipe.name + "' is invalid: " + reason, recipe);

                if (recipe.Ingredients != null)
                {
                    foreach (RedEdenRecipeIngredient ingredient in recipe.Ingredients)
                    {
                        if (ingredient != null && ingredient.Resource != null && !resources.Contains(ingredient.Resource))
                            Debug.LogWarning("RED EDEN recipe '" + recipe.name + "' references resource '" +
                                ingredient.Resource.name + "' which is not listed in this database.", recipe);
                    }
                }
            }
        }
    }
}
