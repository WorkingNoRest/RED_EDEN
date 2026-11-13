using DevionGames.InventorySystem;
using DevionGames;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedEden.Crafting
{
    public enum RedEdenStationState
    {
        Idle,
        Crafting,
        ReadyToCollect,
        Disabled
    }

    [DisallowMultipleComponent]
    public sealed class RedEdenCraftingStation : Trigger, ISceneSaveDataProvider
    {
        private const int CurrentSaveDataVersion = 1;

        [SerializeField, HideInInspector] private string stationId;
        [SerializeField] private RedEdenCraftingDatabase database;
        [SerializeField] private string stationType = "Fabricator";
        [SerializeField] private string inventoryWindow = "Inventory";
        [SerializeField] private bool operational = true;

        private RedEdenCraftingRecipe activeRecipe;
        private string activeRecipeId;
        private float craftDurationSeconds;
        private float craftTimeRemaining;
        private Item pendingOutput;
        private string pendingOutputItemId;
        private int pendingOutputAmount;
        private bool outputCollected;
        private RedEdenCraftingUI ui;

        public string SaveDataId => stationId;
        public int SaveDataVersion => CurrentSaveDataVersion;
        public RedEdenStationState State => !operational
            ? RedEdenStationState.Disabled
            : !string.IsNullOrEmpty(pendingOutputItemId)
                ? RedEdenStationState.ReadyToCollect
                : !string.IsNullOrEmpty(activeRecipeId)
                    ? RedEdenStationState.Crafting
                    : RedEdenStationState.Idle;
        public float Progress => !string.IsNullOrEmpty(activeRecipeId) && craftDurationSeconds > 0f
            ? Mathf.Clamp01(1f - craftTimeRemaining / craftDurationSeconds)
            : State == RedEdenStationState.ReadyToCollect ? 1f : 0f;
        public float RemainingSeconds => craftTimeRemaining;
        public RedEdenCraftingRecipe ActiveRecipe => activeRecipe;
        public Item PendingOutput => pendingOutput;
        public string StationType => stationType;
        public string InventoryWindow => inventoryWindow;
        public RedEdenCraftingDatabase Database => database;
        public string StationId => stationId;
        public bool OutputCollected => outputCollected;

        protected override void Start()
        {
            key = KeyCode.E;
            triggerType = TriggerInputType.Key;
            base.Start();
        }

        protected override void Update()
        {
            if (InRange && UnityTools.GetKeyDown(key) && State == RedEdenStationState.ReadyToCollect)
            {
                if (ui == null || !ui.IsOpen)
                {
                    ui = RedEdenCraftingUI.Open(this);
                }
                else if (!TryCollect(out string reason))
                {
                    ui.ShowMessage(reason);
                }
                else
                {
                    ui.ShowMessage("OUTPUT TRANSFERRED TO INVENTORY");
                }
                return;
            }

            base.Update();
            if (operational && !string.IsNullOrEmpty(activeRecipeId))
            {
                if (activeRecipe == null && database != null)
                    activeRecipe = database.FindRecipe(activeRecipeId);

                if (activeRecipe == null)
                {
                    Debug.LogWarning("RED EDEN station '" + stationId + "' cannot resume recipe '" +
                        activeRecipeId + "' because its definition is missing.", this);
                }
                else
                {
                    craftTimeRemaining = Mathf.Max(0f, craftTimeRemaining - Time.deltaTime);
                    if (craftTimeRemaining <= 0f)
                    {
                        CreatePendingOutput(activeRecipe.OutputItem, activeRecipe.OutputAmount);
                        activeRecipe = null;
                        activeRecipeId = null;
                        craftDurationSeconds = 0f;
                        ui?.ShowMessage("FABRICATION COMPLETE — OUTPUT READY");
                    }
                }
            }

            if (ui != null && ui.IsOpen)
                ui.Refresh();
        }

        public override bool Use()
        {
            if (!base.Use())
                return false;

            if (database == null)
            {
                Debug.LogError("RED EDEN crafting station has no crafting database.", this);
                StopUse();
                return false;
            }

            ui = RedEdenCraftingUI.Open(this);
            return true;
        }

        public bool CanCraft(RedEdenCraftingRecipe recipe, out string reason)
        {
            if (!operational)
            {
                reason = "Station is disabled.";
                return false;
            }
            if (State != RedEdenStationState.Idle)
            {
                reason = State == RedEdenStationState.ReadyToCollect
                    ? "Collect the finished item first."
                    : "Station is already crafting.";
                return false;
            }
            if (recipe == null)
            {
                reason = "Select a valid recipe.";
                return false;
            }
            if (database == null || !database.ContainsRecipe(recipe))
            {
                reason = "Recipe is not registered in this station's crafting database.";
                return false;
            }
            if (!recipe.IsValid(out reason))
                return false;
            if (string.IsNullOrWhiteSpace(recipe.StationType) ||
                !string.Equals(recipe.StationType, stationType, System.StringComparison.OrdinalIgnoreCase))
            {
                reason = "This recipe requires a different station.";
                return false;
            }
            foreach (RedEdenRecipeIngredient ingredient in recipe.Ingredients)
            {
                if (!database.ContainsResource(ingredient.Resource))
                {
                    reason = "A recipe resource is not registered in this station's crafting database.";
                    return false;
                }
            }

            ItemContainer inventory = RedEdenInventoryBridge.FindInventory(inventoryWindow);
            if (inventory == null)
            {
                reason = "Player inventory is unavailable.";
                return false;
            }
            if (!RedEdenInventoryBridge.HasIngredients(inventory, recipe))
            {
                reason = "Missing ingredients.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool StartCrafting(RedEdenCraftingRecipe recipe, out string reason)
        {
            if (!CanCraft(recipe, out reason))
                return false;

            ItemContainer inventory = RedEdenInventoryBridge.FindInventory(inventoryWindow);
            if (inventory == null || !RedEdenInventoryBridge.TryConsumeIngredients(inventory, recipe))
            {
                reason = "Ingredients could not be reserved.";
                return false;
            }

            activeRecipe = recipe;
            activeRecipeId = recipe.Id;
            craftDurationSeconds = recipe.DurationSeconds;
            craftTimeRemaining = recipe.DurationSeconds;
            outputCollected = false;
            return true;
        }

        public bool TryCollect(out string reason)
        {
            if (State != RedEdenStationState.ReadyToCollect)
            {
                reason = "No finished item is waiting.";
                return false;
            }
            if (pendingOutput == null)
            {
                reason = "The saved output definition is unavailable; the printer output was retained.";
                Debug.LogError("RED EDEN station '" + stationId + "' cannot resolve pending output item '" +
                    pendingOutputItemId + "'.", this);
                return false;
            }

            ItemContainer inventory = RedEdenInventoryBridge.FindInventory(inventoryWindow);
            if (inventory == null)
            {
                reason = "Player inventory is unavailable.";
                return false;
            }
            if (!RedEdenInventoryBridge.TryAdd(inventory, pendingOutput, pendingOutput.Stack))
            {
                reason = "Inventory is full. The item remains in the printer.";
                return false;
            }

            Destroy(pendingOutput);
            pendingOutput = null;
            pendingOutputItemId = null;
            pendingOutputAmount = 0;
            outputCollected = true;
            reason = null;
            return true;
        }

        public void SetOperational(bool value)
        {
            operational = value;
        }

        public void CloseInterface()
        {
            if (ui != null)
                ui.Close();
        }

        protected override void OnWentOutOfRange()
        {
            CloseInterface();
            base.OnWentOutOfRange();
        }

        protected override void OnDestroy()
        {
            if (ui != null)
                ui.Close();
            if (pendingOutput != null && Application.isPlaying)
            {
                Debug.LogWarning("A finished RED EDEN crafting output was left uncollected when its station was destroyed.", this);
                Destroy(pendingOutput);
            }
            base.OnDestroy();
        }

        public void WriteSaveData(Dictionary<string, object> data)
        {
            if (string.IsNullOrWhiteSpace(stationId))
                throw new InvalidOperationException("RED EDEN crafting station is missing its stable station ID.");

            data.Add("stationId", stationId);
            data.Add("state", State.ToString());
            data.Add("operational", operational);
            data.Add("activeRecipeId", activeRecipeId ?? string.Empty);
            data.Add("remainingSeconds", craftTimeRemaining);
            data.Add("durationSeconds", craftDurationSeconds);
            data.Add("progress", Progress);
            data.Add("pendingOutputItemId", pendingOutput != null ? pendingOutput.Id : pendingOutputItemId ?? string.Empty);
            data.Add("pendingOutputAmount", pendingOutput != null ? pendingOutput.Stack : pendingOutputAmount);
            data.Add("outputCollected", outputCollected);
        }

        public void ReadSaveData(Dictionary<string, object> data, int version)
        {
            if (version < 1 || version > CurrentSaveDataVersion)
            {
                Debug.LogWarning("RED EDEN station '" + stationId + "' does not support save schema version " +
                    version + ".", this);
                return;
            }
            if (!string.Equals(stationId, ReadString(data, "stationId"), StringComparison.Ordinal))
            {
                Debug.LogError("RED EDEN station save ID does not match the receiving station.", this);
                return;
            }

            if (pendingOutput != null)
                Destroy(pendingOutput);
            pendingOutput = null;
            activeRecipe = null;

            operational = ReadBool(data, "operational", operational);
            activeRecipeId = ReadString(data, "activeRecipeId");
            craftDurationSeconds = Mathf.Max(0f, ReadFloat(data, "durationSeconds", 0f));
            craftTimeRemaining = Mathf.Clamp(ReadFloat(data, "remainingSeconds", 0f), 0f, craftDurationSeconds);
            outputCollected = ReadBool(data, "outputCollected", false);
            pendingOutputItemId = ReadString(data, "pendingOutputItemId");
            pendingOutputAmount = Mathf.Max(0, ReadInt(data, "pendingOutputAmount", 0));

            if (!string.IsNullOrEmpty(activeRecipeId) && !string.IsNullOrEmpty(pendingOutputItemId))
            {
                Debug.LogError("RED EDEN station '" + stationId +
                    "' save data contains both an active job and a pending output; retaining the output and discarding the conflicting job.", this);
                activeRecipeId = null;
                craftDurationSeconds = 0f;
                craftTimeRemaining = 0f;
            }

            if (!string.IsNullOrEmpty(activeRecipeId))
            {
                activeRecipe = database != null ? database.FindRecipe(activeRecipeId) : null;
                if (activeRecipe == null)
                    Debug.LogWarning("RED EDEN station '" + stationId + "' could not resolve saved recipe ID '" +
                        activeRecipeId + "'. The saved job remains reserved and will not consume materials again.", this);
            }

            if (outputCollected)
            {
                pendingOutputItemId = null;
                pendingOutputAmount = 0;
                return;
            }

            if (!string.IsNullOrEmpty(pendingOutputItemId))
            {
                Item outputDefinition = database != null ? database.FindItemDefinition(pendingOutputItemId) : null;
                if (outputDefinition == null)
                {
                    Debug.LogWarning("RED EDEN station '" + stationId + "' could not resolve saved output item ID '" +
                        pendingOutputItemId + "'. The output reference remains available for recovery.", this);
                    return;
                }
                CreatePendingOutput(outputDefinition, pendingOutputAmount);
            }
        }

        [ContextMenu("Generate New Stable Station ID")]
        private void GenerateStationId()
        {
            stationId = Guid.NewGuid().ToString("N");
        }

        private void CreatePendingOutput(Item definition, int amount)
        {
            pendingOutput = Instantiate(definition);
            pendingOutput.Stack = Mathf.Max(1, amount);
            pendingOutputItemId = definition.Id;
            pendingOutputAmount = pendingOutput.Stack;
            outputCollected = false;
        }

        private static string ReadString(Dictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out object value) ? value as string : string.Empty;
        }

        private static bool ReadBool(Dictionary<string, object> data, string key, bool fallback)
        {
            return data.TryGetValue(key, out object value) ? Convert.ToBoolean(value) : fallback;
        }

        private static int ReadInt(Dictionary<string, object> data, string key, int fallback)
        {
            return data.TryGetValue(key, out object value) ? Convert.ToInt32(value) : fallback;
        }

        private static float ReadFloat(Dictionary<string, object> data, string key, float fallback)
        {
            return data.TryGetValue(key, out object value) ? Convert.ToSingle(value) : fallback;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stationId) && !Application.isPlaying)
                GenerateStationId();
        }
    }
}
