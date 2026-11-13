using System;
using System.Collections.Generic;
using System.Linq;
using DevionGames.InventorySystem;
using UnityEngine;

namespace RedEden.Crafting
{
    [Serializable]
    public sealed class RedEdenRecipeIngredient
    {
        [SerializeField] private RedEdenResourceDefinition resource;
        [SerializeField, Min(1)] private int amount = 1;

        public RedEdenResourceDefinition Resource => resource;
        public int Amount => Mathf.Max(1, amount);
    }

    [CreateAssetMenu(menuName = "RED EDEN/Crafting/Recipe")]
    public sealed class RedEdenCraftingRecipe : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string stationType = "Fabricator";
        [SerializeField, Min(0.1f)] private float durationSeconds = 3f;
        [SerializeField] private List<RedEdenRecipeIngredient> ingredients = new List<RedEdenRecipeIngredient>();
        [SerializeField] private Item outputItem;
        [SerializeField, Min(1)] private int outputAmount = 1;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public string StationType => stationType;
        public float DurationSeconds => Mathf.Max(0.1f, durationSeconds);
        public IReadOnlyList<RedEdenRecipeIngredient> Ingredients => ingredients;
        public Item OutputItem => outputItem;
        public int OutputAmount => Mathf.Max(1, outputAmount);

        public bool IsValid(out string reason)
        {
            if (outputItem == null)
            {
                reason = "Recipe has no output item.";
                return false;
            }
            if (outputAmount < 1)
            {
                reason = "Recipe output amount must be positive.";
                return false;
            }
            if (ingredients == null || ingredients.Count == 0)
            {
                reason = "Recipe has no ingredients.";
                return false;
            }
            if (ingredients.Any(x => x == null || x.Resource == null || x.Resource.InventoryItem == null || x.Amount < 1))
            {
                reason = "Recipe contains an invalid ingredient.";
                return false;
            }

            reason = null;
            return true;
        }

        private void OnValidate()
        {
            durationSeconds = Mathf.Max(0.1f, durationSeconds);
            outputAmount = Mathf.Max(1, outputAmount);
            if (string.IsNullOrWhiteSpace(id))
                id = name;
        }
    }
}
