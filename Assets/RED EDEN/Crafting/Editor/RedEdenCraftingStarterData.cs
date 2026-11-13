using System.Collections.Generic;
using DevionGames.InventorySystem;
using RedEden.Crafting;
using UnityEditor;
using UnityEngine;

namespace RedEden.Crafting.Editor
{
    public static class RedEdenCraftingStarterData
    {
        private const string Root = "Assets/RED EDEN/Crafting/Data";

        [MenuItem("Tools/RED EDEN/Crafting/Create Starter Data")]
        public static void CreateStarterData()
        {
            EnsureFolder("Assets/RED EDEN");
            EnsureFolder("Assets/RED EDEN/Crafting");
            EnsureFolder(Root);
            EnsureFolder(Root + "/Items");
            EnsureFolder(Root + "/Resources");
            EnsureFolder(Root + "/Recipes");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Prefabs/ResourceNodes");

            Dictionary<string, Item> items = new Dictionary<string, Item>
            {
                { "Regolith", LoadOrCreateItem("Regolith", 99) },
                { "Water Ice", LoadOrCreateItem("Water Ice", 99) },
                { "Ferric Ore", LoadOrCreateItem("Ferric Ore", 99) },
                { "Silica", LoadOrCreateItem("Silica", 99) },
                { "Carbonaceous Ore", LoadOrCreateItem("Carbonaceous Ore", 99) },
                { "Copper Ore", LoadOrCreateItem("Copper Ore", 99) },
                { "Compacted Regolith Brick", LoadOrCreateItem("Compacted Regolith Brick", 50) },
                { "Metal Plate", LoadOrCreateItem("Metal Plate", 50) },
                { "Silicate Glass", LoadOrCreateItem("Silicate Glass", 50) },
                { "Electrical Cable", LoadOrCreateItem("Electrical Cable", 50) },
                { "Pure Oxygen Cylinder", LoadOxygenCylinderItem() },
                { "Potato", LoadOrCreateFoodItem("Potato", "Potatoes grown in the hydroponic farm.") },
                { "Wheat", LoadOrCreateFoodItem("Wheat", "Wheat grown in the hydroponic farm.") },
                { "Lettuce", LoadOrCreateFoodItem("Lettuce", "Fresh lettuce grown in the hydroponic farm.") }
            };

            Dictionary<string, RedEdenResourceDefinition> resources = new Dictionary<string, RedEdenResourceDefinition>
            {
                { "Regolith", LoadOrCreateResource("Regolith", items["Regolith"], MartianResourceType.Regolith, "Loose surface material collected from the Martian regolith.", "Scoop or sieve") },
                { "Water Ice", LoadOrCreateResource("Water Ice", items["Water Ice"], MartianResourceType.Ice, "Raw subsurface ice. It must be purified before use as water.", "Thermal extraction") },
                { "Ferric Ore", LoadOrCreateResource("Ferric Ore", items["Ferric Ore"], MartianResourceType.MetalOre, "Iron-oxide-rich Martian ore.", "Rock breaker") },
                { "Silica", LoadOrCreateResource("Silica", items["Silica"], MartianResourceType.Mineral, "Silicate-rich mineral suitable for glass feedstock.", "Mineral separator") },
                { "Carbonaceous Ore", LoadOrCreateResource("Carbonaceous Ore", items["Carbonaceous Ore"], MartianResourceType.Organic, "Carbon-bearing feedstock recovered from a protected deposit.", "Sealed extractor") },
                { "Copper Ore", LoadOrCreateResource("Copper Ore", items["Copper Ore"], MartianResourceType.MetalOre, "Copper-bearing mineral feedstock.", "Mineral separator") },
                { "Pure Oxygen Cylinder", LoadOxygenCylinderResource(items["Pure Oxygen Cylinder"]) },
                { "Potato", LoadOrCreateResource("Potato", items["Potato"], MartianResourceType.Organic, "Potatoes grown in the hydroponic farm.", "Hydroponic cultivation") },
                { "Wheat", LoadOrCreateResource("Wheat", items["Wheat"], MartianResourceType.Organic, "Wheat grown in the hydroponic farm.", "Hydroponic cultivation") },
                { "Lettuce", LoadOrCreateResource("Lettuce", items["Lettuce"], MartianResourceType.Organic, "Fresh lettuce grown in the hydroponic farm.", "Hydroponic cultivation") }
            };

            List<Item> allItems = new List<Item>(items.Values);
            ItemDatabase inventoryDatabase = LoadOrCreateDatabase();
            inventoryDatabase.items = allItems;
            EditorUtility.SetDirty(inventoryDatabase);

            RedEdenCraftingRecipe[] recipes =
            {
                LoadOrCreateRecipe("Compacted Regolith Brick", items["Compacted Regolith Brick"], 3,
                    new[] { resources["Regolith"] }, new[] { 3 }, 3f),
                LoadOrCreateRecipe("Metal Plate", items["Metal Plate"], 1,
                    new[] { resources["Ferric Ore"], resources["Carbonaceous Ore"] }, new[] { 2, 1 }, 6f),
                LoadOrCreateRecipe("Silicate Glass", items["Silicate Glass"], 1,
                    new[] { resources["Silica"] }, new[] { 2 }, 5f),
                LoadOrCreateRecipe("Electrical Cable", items["Electrical Cable"], 2,
                    new[] { resources["Copper Ore"], resources["Carbonaceous Ore"] }, new[] { 2, 1 }, 4f)
            };

            RedEdenCraftingDatabase craftingDatabase = LoadOrCreateCraftingDatabase();
            SerializedObject serializedDatabase = new SerializedObject(craftingDatabase);
            SetObjectArray(serializedDatabase.FindProperty("resources"), new List<Object>(resources.Values));
            SetObjectArray(serializedDatabase.FindProperty("recipes"), new List<Object>(recipes));
            serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(craftingDatabase);

            foreach (RedEdenResourceDefinition resource in resources.Values)
                CreateOrUpdateResourceNodePrefab(resource);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = craftingDatabase;
            Debug.Log("RED EDEN starter resources and recipes are ready. Add the generated Item Database as an InventoryManager child database, then assign the RED EDEN crafting database to each station.");
        }

        [MenuItem("Tools/RED EDEN/Crafting/Validate Crafting Data")]
        public static void ValidateCraftingData()
        {
            string[] resourceGuids = AssetDatabase.FindAssets("t:RedEdenResourceDefinition");
            string[] recipeGuids = AssetDatabase.FindAssets("t:RedEdenCraftingRecipe");
            string[] databaseGuids = AssetDatabase.FindAssets("t:RedEdenCraftingDatabase");
            Dictionary<string, string> resourceIds = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            int errors = 0;

            foreach (string guid in resourceGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RedEdenResourceDefinition resource = AssetDatabase.LoadAssetAtPath<RedEdenResourceDefinition>(path);
                if (resource == null || string.IsNullOrWhiteSpace(resource.Id) || resource.InventoryItem == null)
                {
                    Debug.LogError("Resource is missing a stable ID or inventory item: " + path, resource);
                    errors++;
                    continue;
                }

                if (resourceIds.TryGetValue(resource.Id, out string existingPath))
                {
                    Debug.LogError("Duplicate RED EDEN resource ID '" + resource.Id + "' in " +
                        existingPath + " and " + path + ".", resource);
                    errors++;
                }
                else
                {
                    resourceIds.Add(resource.Id, path);
                }
            }

            foreach (string guid in databaseGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RedEdenCraftingDatabase database = AssetDatabase.LoadAssetAtPath<RedEdenCraftingDatabase>(path);
                if (database == null)
                    continue;

                foreach (RedEdenCraftingRecipe recipe in database.Recipes)
                {
                    if (recipe == null)
                    {
                        Debug.LogError("Crafting database has an empty recipe slot: " + path, database);
                        errors++;
                        continue;
                    }
                    if (!recipe.IsValid(out string reason))
                    {
                        Debug.LogError("Invalid recipe '" + recipe.name + "': " + reason, recipe);
                        errors++;
                    }
                    foreach (RedEdenRecipeIngredient ingredient in recipe.Ingredients)
                    {
                        if (ingredient != null && ingredient.Resource != null &&
                            !database.ContainsResource(ingredient.Resource))
                        {
                            Debug.LogError("Recipe '" + recipe.name + "' references a resource not listed in " +
                                database.name + ".", recipe);
                            errors++;
                        }
                    }
                }
            }

            foreach (string guid in resourceGuids)
            {
                string resourcePath = AssetDatabase.GUIDToAssetPath(guid);
                RedEdenResourceDefinition resource = AssetDatabase.LoadAssetAtPath<RedEdenResourceDefinition>(resourcePath);
                if (resource == null)
                    continue;

                string prefabPath = Root + "/Prefabs/ResourceNodes/" + resource.name + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                MartianResourceNode node = prefab != null ? prefab.GetComponent<MartianResourceNode>() : null;
                if (node == null || node.Resource != resource)
                {
                    Debug.LogWarning("No matching world resource node prefab was found for '" +
                        resource.DisplayName + "'. Run Create Starter Data to generate or repair it.", resource);
                }
            }

            if (errors == 0)
                Debug.Log("RED EDEN crafting data validation passed. Resource definitions: " +
                    resourceGuids.Length + "; recipes: " + recipeGuids.Length + ".");
            else
                Debug.LogError("RED EDEN crafting data validation found " + errors + " error(s).");
        }

        private static Item LoadOrCreateItem(string itemName, int maxStack)
        {
            string path = Root + "/Items/" + itemName.Replace(" ", string.Empty) + ".asset";
            Item item = AssetDatabase.LoadAssetAtPath<Item>(path);
            if (item != null)
                return item;

            item = ScriptableObject.CreateInstance<Item>();
            item.Id = System.Guid.NewGuid().ToString();
            item.Name = itemName;
            item.DisplayName = itemName;
            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.FindProperty("m_MaxStack").intValue = maxStack;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        private static Item LoadOrCreateFoodItem(string itemName, string description)
        {
            Item item = LoadOrCreateItem(itemName, 99);
            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.FindProperty("m_Description").stringValue = description;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static Item LoadOxygenCylinderItem()
        {
            string path = Root + "/Items/PureOxygenCylender.asset";
            Item item = AssetDatabase.LoadAssetAtPath<Item>(path);
            if (item == null)
                item = LoadOrCreateItem("PureOxygenCylender", 10);

            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.FindProperty("m_UseItemNameAsDisplayName").boolValue = false;
            serializedItem.FindProperty("m_DisplayName").stringValue = "Pure Oxygen Cylinder";
            serializedItem.FindProperty("m_Description").stringValue =
                "Pressurized pure oxygen for habitat life support.";
            serializedItem.FindProperty("m_MaxStack").intValue = 10;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static RedEdenResourceDefinition LoadOxygenCylinderResource(Item item)
        {
            string path = Root + "/Resources/PureOxygenCYlender.asset";
            RedEdenResourceDefinition resource =
                AssetDatabase.LoadAssetAtPath<RedEdenResourceDefinition>(path);
            if (resource == null)
            {
                resource = ScriptableObject.CreateInstance<RedEdenResourceDefinition>();
                AssetDatabase.CreateAsset(resource, path);
            }

            SerializedObject serializedResource = new SerializedObject(resource);
            serializedResource.FindProperty("id").stringValue = "PureOxygenCylender";
            serializedResource.FindProperty("displayName").stringValue = "Pure Oxygen Cylinder";
            serializedResource.FindProperty("description").stringValue =
                "A sealed, pressurized oxygen canister for habitat life support.";
            serializedResource.FindProperty("type").enumValueIndex = (int)MartianResourceType.Manufactured;
            serializedResource.FindProperty("inventoryItem").objectReferenceValue = item;
            serializedResource.FindProperty("purity").floatValue = 1f;
            serializedResource.FindProperty("contamination").floatValue = 0f;
            serializedResource.FindProperty("densityKilogramsPerCubicMeter").floatValue = 1.429f;
            serializedResource.FindProperty("harvestAmount").intValue = 1;
            serializedResource.FindProperty("extractionMethod").stringValue = "Atmospheric oxygen processing";
            serializedResource.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(resource);
            return resource;
        }

        private static ItemDatabase LoadOrCreateDatabase()
        {
            const string path = Root + "/RED EDEN Item Database.asset";
            ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(database, path);
            }
            return database;
        }

        private static RedEdenResourceDefinition LoadOrCreateResource(
            string resourceName, Item item, MartianResourceType type, string description, string extraction)
        {
            string path = Root + "/Resources/" + resourceName.Replace(" ", string.Empty) + ".asset";
            RedEdenResourceDefinition resource = AssetDatabase.LoadAssetAtPath<RedEdenResourceDefinition>(path);
            if (resource != null)
                return resource;

            resource = ScriptableObject.CreateInstance<RedEdenResourceDefinition>();
            SerializedObject serialized = new SerializedObject(resource);
            serialized.FindProperty("id").stringValue = resourceName.Replace(" ", "_").ToLowerInvariant();
            serialized.FindProperty("displayName").stringValue = resourceName;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("type").enumValueIndex = (int)type;
            serialized.FindProperty("inventoryItem").objectReferenceValue = item;
            serialized.FindProperty("harvestAmount").intValue = 1;
            serialized.FindProperty("extractionMethod").stringValue = extraction;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(resource, path);
            return resource;
        }

        private static RedEdenCraftingRecipe LoadOrCreateRecipe(
            string recipeName, Item output, int outputAmount,
            RedEdenResourceDefinition[] ingredients, int[] amounts, float duration)
        {
            string path = Root + "/Recipes/" + recipeName.Replace(" ", string.Empty) + ".asset";
            RedEdenCraftingRecipe recipe = AssetDatabase.LoadAssetAtPath<RedEdenCraftingRecipe>(path);
            if (recipe != null)
                return recipe;

            recipe = ScriptableObject.CreateInstance<RedEdenCraftingRecipe>();
            SerializedObject serialized = new SerializedObject(recipe);
            serialized.FindProperty("id").stringValue = recipeName.Replace(" ", "_").ToLowerInvariant();
            serialized.FindProperty("displayName").stringValue = recipeName;
            serialized.FindProperty("description").stringValue = "Fabricated from locally recovered Martian materials.";
            serialized.FindProperty("stationType").stringValue = "Fabricator";
            serialized.FindProperty("durationSeconds").floatValue = duration;
            serialized.FindProperty("outputItem").objectReferenceValue = output;
            serialized.FindProperty("outputAmount").intValue = outputAmount;

            SerializedProperty ingredientList = serialized.FindProperty("ingredients");
            ingredientList.arraySize = ingredients.Length;
            for (int i = 0; i < ingredients.Length; i++)
            {
                SerializedProperty ingredient = ingredientList.GetArrayElementAtIndex(i);
                ingredient.FindPropertyRelative("resource").objectReferenceValue = ingredients[i];
                ingredient.FindPropertyRelative("amount").intValue = amounts[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(recipe, path);
            return recipe;
        }

        private static RedEdenCraftingDatabase LoadOrCreateCraftingDatabase()
        {
            const string path = Root + "/RED EDEN Crafting Database.asset";
            RedEdenCraftingDatabase database = AssetDatabase.LoadAssetAtPath<RedEdenCraftingDatabase>(path);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<RedEdenCraftingDatabase>();
                AssetDatabase.CreateAsset(database, path);
            }
            return database;
        }

        private static void SetObjectArray(SerializedProperty property, List<Object> objects)
        {
            property.arraySize = objects.Count;
            for (int i = 0; i < objects.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }

        private static void CreateOrUpdateResourceNodePrefab(RedEdenResourceDefinition resource)
        {
            string path = Root + "/Prefabs/ResourceNodes/" + resource.name + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                GameObject nodeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                nodeObject.name = resource.DisplayName + " Deposit";
                nodeObject.transform.localScale = Vector3.one * 0.8f;
                MartianResourceNode node = nodeObject.AddComponent<MartianResourceNode>();
                SerializedObject serializedNode = new SerializedObject(node);
                serializedNode.FindProperty("resource").objectReferenceValue = resource;
                serializedNode.FindProperty("remainingAmount").intValue = 10;
                serializedNode.FindProperty("amountPerUse").intValue = resource.HarvestAmount;
                serializedNode.FindProperty("nodeId").stringValue = System.Guid.NewGuid().ToString("N");
                serializedNode.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(nodeObject, path);
                Object.DestroyImmediate(nodeObject);
                return;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MartianResourceNode node = prefabContents.GetComponent<MartianResourceNode>();
                if (node == null)
                {
                    Debug.LogWarning("Resource node prefab is missing MartianResourceNode: " + path, prefab);
                    return;
                }

                SerializedObject serializedNode = new SerializedObject(node);
                serializedNode.FindProperty("resource").objectReferenceValue = resource;
                SerializedProperty nodeId = serializedNode.FindProperty("nodeId");
                if (string.IsNullOrWhiteSpace(nodeId.stringValue))
                    nodeId.stringValue = System.Guid.NewGuid().ToString("N");
                serializedNode.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefabContents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string folder = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
