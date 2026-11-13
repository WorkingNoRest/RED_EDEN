using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DevionGames.InventorySystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RedEden.Crafting.Tests
{
    public sealed class RedEdenCraftingAuditTests
    {
        private const string DataRoot = "Assets/RED EDEN/Crafting/Data";

        [Test]
        public void StarterDatabaseHasUniqueResourceIdsAndValidRecipes()
        {
            RedEdenCraftingDatabase database =
                AssetDatabase.LoadAssetAtPath<RedEdenCraftingDatabase>(DataRoot + "/RED EDEN Crafting Database.asset");

            Assert.That(database, Is.Not.Null);
            Assert.That(database.Resources, Is.Not.Empty);
            Assert.That(database.Recipes, Is.Not.Empty);
            Assert.That(database.Resources.All(resource => resource != null), Is.True);
            Assert.That(database.Resources.Select(resource => resource.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(database.Resources.Count));

            foreach (RedEdenCraftingRecipe recipe in database.Recipes)
            {
                Assert.That(recipe, Is.Not.Null);
                Assert.That(recipe.IsValid(out string reason), Is.True, recipe.name + ": " + reason);
                Assert.That(recipe.Ingredients.All(ingredient =>
                    ingredient != null && database.ContainsResource(ingredient.Resource)), Is.True, recipe.name);
            }
        }

        [Test]
        public void StarterItemDatabaseContainsEveryResourceAndRecipeOutput()
        {
            ItemDatabase inventoryDatabase =
                AssetDatabase.LoadAssetAtPath<ItemDatabase>(DataRoot + "/RED EDEN Item Database.asset");
            RedEdenCraftingDatabase craftingDatabase =
                AssetDatabase.LoadAssetAtPath<RedEdenCraftingDatabase>(DataRoot + "/RED EDEN Crafting Database.asset");

            Assert.That(inventoryDatabase, Is.Not.Null);
            Assert.That(craftingDatabase, Is.Not.Null);
            foreach (RedEdenResourceDefinition resource in craftingDatabase.Resources)
                Assert.That(inventoryDatabase.allItems.Any(item => item == resource.InventoryItem),
                    Is.True, resource.name + " must be registered in the inventory database.");
            foreach (RedEdenCraftingRecipe recipe in craftingDatabase.Recipes)
                Assert.That(inventoryDatabase.allItems.Any(item => item == recipe.OutputItem),
                    Is.True, recipe.name + " output must be registered in the inventory database.");
        }

        [Test]
        public void StarterResourcePrefabsReferenceTheirDefinitions()
        {
            RedEdenCraftingDatabase database =
                AssetDatabase.LoadAssetAtPath<RedEdenCraftingDatabase>(DataRoot + "/RED EDEN Crafting Database.asset");
            string prefabRoot = DataRoot + "/Prefabs/ResourceNodes";
            RedEdenResourceDefinition[] prefabResources = AssetDatabase.FindAssets("t:Prefab", new[] { prefabRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(prefab => prefab != null)
                .Select(prefab => prefab.GetComponent<MartianResourceNode>())
                .Where(node => node != null)
                .Select(node => node.Resource)
                .Where(resource => resource != null)
                .ToArray();

            foreach (RedEdenResourceDefinition resource in database.Resources)
            {
                Assert.That(prefabResources, Does.Contain(resource), resource.name);
            }
        }

        [Test]
        public void CraftingDataContainsNoBrokenAssetGuidReferences()
        {
            string[] paths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith(DataRoot + "/", StringComparison.Ordinal) &&
                    (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            Regex guidPattern = new Regex(@"guid:\s*([a-fA-F0-9]{32})", RegexOptions.Compiled);

            foreach (string path in paths)
            {
                string absolutePath = Path.GetFullPath(path);
                foreach (Match match in guidPattern.Matches(File.ReadAllText(absolutePath)))
                {
                    string guid = match.Groups[1].Value;
                    if (guid.StartsWith("0000000000000000", StringComparison.Ordinal))
                        continue;
                    Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Not.Empty,
                        path + " contains an unresolved GUID: " + guid);
                }
            }
        }

        [Test]
        public void CraftingStationsKeepTheirStateAndInterfaceSeparate()
        {
            GameObject firstObject = new GameObject("First Fabricator");
            GameObject secondObject = new GameObject("Second Fabricator");
            try
            {
                RedEdenCraftingStation first = firstObject.AddComponent<RedEdenCraftingStation>();
                RedEdenCraftingStation second = secondObject.AddComponent<RedEdenCraftingStation>();

                first.SetOperational(false);
                Assert.That(first.State, Is.EqualTo(RedEdenStationState.Disabled));
                Assert.That(second.State, Is.EqualTo(RedEdenStationState.Idle));

                MethodInfo open = typeof(RedEdenCraftingStation).Assembly
                    .GetType("RedEden.Crafting.RedEdenCraftingUI")
                    .GetMethod("Open", BindingFlags.Public | BindingFlags.Static);
                Assert.That(open, Is.Not.Null);
                Component firstUi = open.Invoke(null, new object[] { first }) as Component;
                Component secondUi = open.Invoke(null, new object[] { second }) as Component;

                Assert.That(firstUi, Is.Not.Null);
                Assert.That(secondUi, Is.Not.Null);
                Assert.That(secondUi, Is.Not.SameAs(firstUi));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void RecipeWithMissingOutputIsRejected()
        {
            RedEdenCraftingRecipe recipe = ScriptableObject.CreateInstance<RedEdenCraftingRecipe>();
            try
            {
                Assert.That(recipe.IsValid(out string reason), Is.False);
                Assert.That(reason, Does.Contain("output"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recipe);
            }
        }
    }
}
