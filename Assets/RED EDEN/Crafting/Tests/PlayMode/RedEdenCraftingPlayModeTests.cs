using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DevionGames.InventorySystem;
using DevionGames.InventorySystem.Configuration;
using DevionGames;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace RedEden.Crafting.Tests
{
    public sealed class RedEdenCraftingPlayModeTests
    {
        private readonly List<GameObject> testObjects = new List<GameObject>();
        private readonly List<ScriptableObject> testAssets = new List<ScriptableObject>();
        private readonly Dictionary<string, string> savedPreferenceValues = new Dictionary<string, string>();
        private readonly InputTestFixture input = new InputTestFixture();
        private Keyboard testKeyboard;
        private RedEdenCraftingDatabase database;

        [SetUp]
        public void SetUp()
        {
            input.Setup();
            CreateRuntimeDatabase();
            CreateInventoryManager();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject testObject in testObjects)
            {
                if (testObject != null)
                {
                    RedEdenCraftingStation station = testObject.GetComponent<RedEdenCraftingStation>();
                    if (station != null && station.PendingOutput != null)
                    {
                        UnityEngine.Object.Destroy(station.PendingOutput);
                        SetField(station, "pendingOutput", null);
                    }
                    UnityEngine.Object.Destroy(testObject);
                }
            }
            testObjects.Clear();
            foreach (ScriptableObject asset in testAssets)
                if (asset != null)
                    UnityEngine.Object.Destroy(asset);
            testAssets.Clear();
            foreach (KeyValuePair<string, string> preference in savedPreferenceValues)
                PlayerPrefs.SetString(preference.Key, preference.Value);
            savedPreferenceValues.Clear();
            PlayerPrefs.Save();
            yield return null;
            ResetInventoryManagerStatics();
            input.TearDown();
        }

        [Test]
        public void HydroponicFarmSaveRestoresCropTimersAndAllStoredOutputs()
        {
            GameObject farmObject = new GameObject("Hydroponic farm save test");
            testObjects.Add(farmObject);
            HydroponicFarm farm = farmObject.AddComponent<HydroponicFarm>();
            string farmId = "playmode-farm-" + Guid.NewGuid().ToString("N");
            SetField(farm, "farmId", farmId);

            Dictionary<string, object> savedState = new Dictionary<string, object>
            {
                { "farmId", farmId },
                { "operational", false },
                { "activeCrop", FarmCropType.Lettuce.ToString() },
                { "oxygenElapsed", 17f },
                { "cropElapsed", 23f },
                { "oxygenStored", 2 },
                { "food_Potato", 3 },
                { "food_Wheat", 4 },
                { "food_Lettuce", 5 }
            };

            farm.ReadSaveData(savedState, 1);
            Dictionary<string, object> roundTrip = new Dictionary<string, object>();
            farm.WriteSaveData(roundTrip);

            Assert.That(farm.Operational, Is.False);
            Assert.That(farm.ActiveCrop, Is.EqualTo(FarmCropType.Lettuce));
            Assert.That(roundTrip["oxygenElapsed"], Is.EqualTo(17f));
            Assert.That(roundTrip["cropElapsed"], Is.EqualTo(23f));
            Assert.That(roundTrip["oxygenStored"], Is.EqualTo(2));
            Assert.That(roundTrip["food_Potato"], Is.EqualTo(3));
            Assert.That(roundTrip["food_Wheat"], Is.EqualTo(4));
            Assert.That(roundTrip["food_Lettuce"], Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator InventoryManagerSaveRestoresIndependentJobsOutputsAndNodeYield()
        {
            RedEdenCraftingRecipe recipe = database.Recipes[0];
            RedEdenCraftingStation activeStation = CreateStation("Fabricator A");
            RedEdenCraftingStation readyStation = CreateStation("Fabricator B");
            RedEdenCraftingStation emptyStation = CreateStation("Fabricator C");
            SetField(activeStation, "stationId", "playmode-active-" + Guid.NewGuid().ToString("N"));
            SetField(readyStation, "stationId", "playmode-ready-" + Guid.NewGuid().ToString("N"));
            SetField(emptyStation, "stationId", "playmode-empty-" + Guid.NewGuid().ToString("N"));

            Dictionary<string, object> activeJob = CreateStationData(activeStation);
            activeJob["activeRecipeId"] = recipe.Id;
            activeJob["remainingSeconds"] = 4f;
            activeJob["durationSeconds"] = 8f;
            activeJob["progress"] = 0.5f;
            activeStation.ReadSaveData(activeJob, 1);

            Item output = database.Recipes[0].OutputItem;
            Dictionary<string, object> readyJob = CreateStationData(readyStation);
            readyJob["pendingOutputItemId"] = output.Id;
            readyJob["pendingOutputAmount"] = 3;
            readyStation.ReadSaveData(readyJob, 1);

            GameObject nodeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            testObjects.Add(nodeObject);
            MartianResourceNode newNode = nodeObject.AddComponent<MartianResourceNode>();
            SetField(newNode, "resource", database.Resources[0]);
            MartianResourceNode node = nodeObject.GetComponent<MartianResourceNode>();
            string nodeId = "playmode-node-" + Guid.NewGuid().ToString("N");
            SetField(node, "nodeId", nodeId);
            Dictionary<string, object> nodeState = new Dictionary<string, object>();
            node.WriteSaveData(nodeState);
            nodeState["remainingAmount"] = 2;
            nodeState["depleted"] = false;
            nodeState["supportsRespawn"] = true;
            nodeState["respawnTimeRemaining"] = 30f;
            node.ReadSaveData(nodeState, 1);

            string saveKey = "RED_EDEN_PLAYMODE_" + Guid.NewGuid().ToString("N");
            string sceneKey = saveKey + "." + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            CapturePlayerPreference(saveKey + ".UI");
            CapturePlayerPreference(sceneKey);
            CapturePlayerPreference(saveKey + ".Scenes");
            CapturePlayerPreference("InventorySystemSavedKeys");
            InventoryManager.Save(saveKey);
            string sceneData = PlayerPrefs.GetString(sceneKey);
            Assert.That(sceneData, Does.Contain("playmode-active-"));

            Dictionary<string, object> idleActiveStation = CreateStationData(activeStation);
            idleActiveStation["activeRecipeId"] = string.Empty;
            idleActiveStation["remainingSeconds"] = 0f;
            idleActiveStation["durationSeconds"] = 0f;
            activeStation.ReadSaveData(idleActiveStation, 1);

            Dictionary<string, object> idleReadyStation = CreateStationData(readyStation);
            idleReadyStation["pendingOutputItemId"] = string.Empty;
            idleReadyStation["pendingOutputAmount"] = 0;
            idleReadyStation["outputCollected"] = true;
            readyStation.ReadSaveData(idleReadyStation, 1);

            Dictionary<string, object> changedNodeState = new Dictionary<string, object>(nodeState)
            {
                ["remainingAmount"] = 10,
                ["depleted"] = false,
                ["respawnTimeRemaining"] = 0f
            };
            node.ReadSaveData(changedNodeState, 1);

            InventoryManager.Load(saveKey);
            Assert.That(activeStation.State, Is.EqualTo(RedEdenStationState.Crafting));
            Assert.That(activeStation.ActiveRecipe, Is.SameAs(recipe));
            Assert.That(activeStation.RemainingSeconds, Is.EqualTo(4f).Within(0.001f));
            Assert.That(activeStation.Progress, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(readyStation.State, Is.EqualTo(RedEdenStationState.ReadyToCollect));
            Assert.That(readyStation.PendingOutput, Is.Not.Null);
            Assert.That(readyStation.PendingOutput.Id, Is.EqualTo(output.Id));
            Assert.That(readyStation.PendingOutput.Stack, Is.EqualTo(3));
            Assert.That(emptyStation.State, Is.EqualTo(RedEdenStationState.Idle));
            Assert.That(node.RemainingAmount, Is.EqualTo(2));
            Assert.That(node.GetComponent<Collider>().enabled, Is.True);

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator RestoredFabricationProgressCompletesAndKeepsOutputPending()
        {
            RedEdenCraftingRecipe recipe = database.Recipes[0];
            RedEdenCraftingStation station = CreateStation("Progress Fabricator");
            SetField(station, "stationId", "playmode-progress-" + Guid.NewGuid().ToString("N"));

            Dictionary<string, object> activeJob = CreateStationData(station);
            activeJob["activeRecipeId"] = recipe.Id;
            activeJob["remainingSeconds"] = 0.25f;
            activeJob["durationSeconds"] = 0.5f;
            station.ReadSaveData(activeJob, 1);

            float initialProgress = station.Progress;
            yield return new WaitForSeconds(0.1f);
            Assert.That(station.Progress, Is.GreaterThan(initialProgress));
            Assert.That(station.State, Is.EqualTo(RedEdenStationState.Crafting));

            yield return new WaitForSeconds(0.5f);
            Assert.That(station.State, Is.EqualTo(RedEdenStationState.ReadyToCollect));
            Assert.That(station.PendingOutput, Is.Not.Null);
            Assert.That(station.PendingOutput.Id, Is.EqualTo(recipe.OutputItem.Id));
            Assert.That(station.PendingOutput.Stack, Is.EqualTo(recipe.OutputAmount));
        }

        [UnityTest]
        public IEnumerator PressingEOpensTheStationOwnedInterface()
        {
            GameObject player = new GameObject("RED EDEN Play Mode Player");
            player.tag = "Player";
            testObjects.Add(player);
            RedEdenCraftingStation station = CreateStation("E Interaction Fabricator");
            SetField(station, "stationId", "playmode-interaction-" + Guid.NewGuid().ToString("N"));
            station.useDistance = -1f;
            yield return null;

            testKeyboard = InputSystem.AddDevice<Keyboard>();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "could not find the inventory window 'Inventory'"));
            input.Press(testKeyboard.eKey);
            yield return null;

            Component ui = null;
            foreach (MonoBehaviour behaviour in station.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour.GetType().FullName == "RedEden.Crafting.RedEdenCraftingUI")
                    ui = behaviour;
            Assert.That(ui, Is.Not.Null);
            PropertyInfo isOpen = ui.GetType().GetProperty("IsOpen", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(isOpen, Is.Not.Null);
            Assert.That((bool)isOpen.GetValue(ui), Is.True);

            station.CloseInterface();
            input.Release(testKeyboard.eKey);
            testKeyboard = null;
            yield return null;
        }

        [Test]
        public void MissingRecipeRetainsSavedJobWithoutReconsumingMaterials()
        {
            RedEdenCraftingStation station = CreateStation("Missing Recipe Fabricator");
            SetField(station, "stationId", "playmode-missing-recipe-" + Guid.NewGuid().ToString("N"));
            Dictionary<string, object> data = CreateStationData(station);
            data["activeRecipeId"] = "missing-recipe-id";
            data["remainingSeconds"] = 5f;
            data["durationSeconds"] = 10f;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "could not resolve saved recipe ID 'missing-recipe-id'"));
            station.ReadSaveData(data, 1);

            Assert.That(station.State, Is.EqualTo(RedEdenStationState.Crafting));
            Assert.That(station.ActiveRecipe, Is.Null);
            Assert.That(station.RemainingSeconds, Is.EqualTo(5f));
            station.enabled = false;
        }

        [Test]
        public void MissingOutputDefinitionKeepsPendingOutputReference()
        {
            RedEdenCraftingStation station = CreateStation("Missing Output Fabricator");
            SetField(station, "stationId", "playmode-missing-output-" + Guid.NewGuid().ToString("N"));
            Dictionary<string, object> data = CreateStationData(station);
            data["pendingOutputItemId"] = "missing-output-item-id";
            data["pendingOutputAmount"] = 2;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "could not resolve saved output item ID 'missing-output-item-id'"));
            station.ReadSaveData(data, 1);

            Assert.That(station.State, Is.EqualTo(RedEdenStationState.ReadyToCollect));
            Assert.That(station.PendingOutput, Is.Null);
            Dictionary<string, object> preserved = new Dictionary<string, object>();
            station.WriteSaveData(preserved);
            Assert.That(preserved["pendingOutputItemId"], Is.EqualTo("missing-output-item-id"));
            Assert.That(preserved["pendingOutputAmount"], Is.EqualTo(2));
            station.enabled = false;
        }

        [Test]
        public void FailedSceneSerializationLeavesPreviousSaveUntouched()
        {
            const string key = "RED_EDEN_TEST_FAILED_SAVE";
            string sceneKey = key + "." + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            PlayerPrefs.SetString(key + ".UI", "previous-ui");
            PlayerPrefs.SetString(sceneKey, "previous-scene");
            PlayerPrefs.Save();

            GameObject providerObject = new GameObject("Failing save provider");
            testObjects.Add(providerObject);
            providerObject.AddComponent<FailingSaveProvider>();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "Save aborted before changing the previous save data"));
            InventoryManager.Save(key);

            Assert.That(PlayerPrefs.GetString(key + ".UI"), Is.EqualTo("previous-ui"));
            Assert.That(PlayerPrefs.GetString(sceneKey), Is.EqualTo("previous-scene"));
            PlayerPrefs.DeleteKey(key + ".UI");
            PlayerPrefs.DeleteKey(sceneKey);
            PlayerPrefs.Save();
        }

        private RedEdenCraftingStation CreateStation(string name)
        {
            GameObject gameObject = new GameObject(name);
            testObjects.Add(gameObject);
            RedEdenCraftingStation station = gameObject.AddComponent<RedEdenCraftingStation>();
            SetField(station, "database", database);
            return station;
        }

        private void CreateRuntimeDatabase()
        {
            database = CreateAsset<RedEdenCraftingDatabase>();
            Item resourceItem = CreateAsset<Item>();
            resourceItem.Id = "playmode-resource-item";
            resourceItem.Name = "Test Resource";
            Item outputItem = CreateAsset<Item>();
            outputItem.Id = "playmode-output-item";
            outputItem.Name = "Test Output";

            RedEdenResourceDefinition resource = CreateAsset<RedEdenResourceDefinition>();
            SetField(resource, "id", "playmode-resource");
            SetField(resource, "displayName", "Test Resource");
            SetField(resource, "inventoryItem", resourceItem);
            RedEdenRecipeIngredient ingredient = new RedEdenRecipeIngredient();
            SetField(ingredient, "resource", resource);
            SetField(ingredient, "amount", 2);

            RedEdenCraftingRecipe recipe = CreateAsset<RedEdenCraftingRecipe>();
            SetField(recipe, "id", "playmode-recipe");
            SetField(recipe, "displayName", "Test Recipe");
            SetField(recipe, "durationSeconds", 0.5f);
            SetField(recipe, "ingredients", new List<RedEdenRecipeIngredient> { ingredient });
            SetField(recipe, "outputItem", outputItem);
            SetField(recipe, "outputAmount", 3);

            SetField(database, "resources", new List<RedEdenResourceDefinition> { resource });
            SetField(database, "recipes", new List<RedEdenCraftingRecipe> { recipe });
        }

        private void CreateInventoryManager()
        {
            ItemDatabase inventoryDatabase = CreateAsset<ItemDatabase>();
            inventoryDatabase.settings.Add(CreateAsset<Default>());
            inventoryDatabase.settings.Add(CreateAsset<SavingLoading>());
            inventoryDatabase.settings.Add(CreateAsset<UI>());
            inventoryDatabase.settings.Add(CreateAsset<Notifications>());
            inventoryDatabase.settings.Add(CreateAsset<DevionGames.InventorySystem.Configuration.Input>());
            ((Default)inventoryDatabase.settings.Find(setting => setting is Default)).debugMessages = false;
            ((SavingLoading)inventoryDatabase.settings.Find(setting => setting is SavingLoading)).autoSave = false;

            GameObject managerObject = new GameObject("RED EDEN Play Mode Inventory Manager");
            managerObject.SetActive(false);
            testObjects.Add(managerObject);
            InventoryManager manager = managerObject.AddComponent<InventoryManager>();
            SetField(manager, "m_Database", inventoryDatabase);
            SetField(manager, "m_ChildDatabases", new ItemDatabase[0]);
            manager.dontDestroyOnLoad = false;
            manager.onDataLoaded = new UnityEvent();
            manager.onDataSaved = new UnityEvent();
            managerObject.SetActive(true);
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            testAssets.Add(asset);
            return asset;
        }

        private static Dictionary<string, object> CreateStationData(RedEdenCraftingStation station)
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            station.WriteSaveData(data);
            return data;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing serialized field " + fieldName);
            field.SetValue(target, value);
        }

        private static void ResetInventoryManagerStatics()
        {
            Type managerType = typeof(InventoryManager);
            foreach (string fieldName in new[]
            {
                "m_Current", "m_DefaultSettings", "m_SavingLoading", "m_UI",
                "m_Notifications", "m_Input", "m_PrefabCache"
            })
            {
                FieldInfo field = managerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
                if (field != null)
                    field.SetValue(null, null);
            }
        }

        private void CapturePlayerPreference(string key)
        {
            if (!savedPreferenceValues.ContainsKey(key))
                savedPreferenceValues.Add(key, PlayerPrefs.GetString(key));
        }

        private sealed class FailingSaveProvider : MonoBehaviour, ISceneSaveDataProvider
        {
            public string SaveDataId => "failing-provider";
            public int SaveDataVersion => 1;

            public void WriteSaveData(Dictionary<string, object> data)
            {
                throw new InvalidOperationException("Intentional test serialization failure.");
            }

            public void ReadSaveData(Dictionary<string, object> data, int version)
            {
            }
        }

    }
}
