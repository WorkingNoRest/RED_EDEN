using DevionGames.InventorySystem;
using DevionGames;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedEden.Crafting
{
    [DisallowMultipleComponent]
    public sealed class MartianResourceNode : Trigger, ISceneSaveDataProvider
    {
        private const int CurrentSaveDataVersion = 1;

        [SerializeField, HideInInspector] private string nodeId;
        [SerializeField] private RedEdenResourceDefinition resource;
        [SerializeField, Min(1)] private int remainingAmount = 10;
        [SerializeField, Min(1)] private int amountPerUse = 1;
        [SerializeField] private string inventoryWindow = "Inventory";
        [SerializeField] private bool supportsRespawn;
        [SerializeField, Min(0f)] private float respawnDelaySeconds = 300f;

        private int currentRemainingAmount;
        private float respawnTimeRemaining;
        private bool initialized;
        private Renderer[] renderers;
        private Collider[] colliders;

        public string SaveDataId => nodeId;
        public int SaveDataVersion => CurrentSaveDataVersion;
        public int RemainingAmount => currentRemainingAmount;
        public RedEdenResourceDefinition Resource => resource;

        private void Awake()
        {
            currentRemainingAmount = remainingAmount;
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            initialized = true;
            UpdateAvailability();
        }

        protected override void Start()
        {
            key = KeyCode.E;
            triggerType = TriggerInputType.Key;
            base.Start();
        }

        protected override void Update()
        {
            base.Update();
            if (currentRemainingAmount > 0 || !supportsRespawn)
                return;
            respawnTimeRemaining = Mathf.Max(0f, respawnTimeRemaining - Time.deltaTime);
            if (respawnTimeRemaining <= 0f)
            {
                currentRemainingAmount = remainingAmount;
                UpdateAvailability();
            }
        }

        public override bool Use()
        {
            if (!base.Use())
                return false;

            try
            {
                if (resource == null || resource.InventoryItem == null)
                {
                    Debug.LogError("RED EDEN resource node is missing a valid resource definition.", this);
                    return false;
                }
                if (currentRemainingAmount < 1)
                    return false;

                ItemContainer inventory = RedEdenInventoryBridge.FindInventory(inventoryWindow);
                if (inventory == null)
                    return false;

                int amount = Mathf.Min(Mathf.Max(1, amountPerUse), currentRemainingAmount);
                if (!RedEdenInventoryBridge.TryAdd(inventory, resource.InventoryItem, amount))
                {
                    Debug.Log("Inventory is full; the resource node has not been depleted.", this);
                    return false;
                }

                currentRemainingAmount -= amount;
                if (currentRemainingAmount == 0)
                {
                    respawnTimeRemaining = supportsRespawn ? Mathf.Max(0f, respawnDelaySeconds) : 0f;
                    UpdateAvailability();
                }
                return true;
            }
            finally
            {
                StopUse();
            }
        }

        public void WriteSaveData(Dictionary<string, object> data)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new InvalidOperationException("RED EDEN resource node is missing its stable node ID.");
            if (resource == null || string.IsNullOrWhiteSpace(resource.Id))
                throw new InvalidOperationException("RED EDEN resource node '" + nodeId + "' has no valid resource definition.");

            data.Add("nodeId", nodeId);
            data.Add("resourceId", resource.Id);
            data.Add("remainingAmount", currentRemainingAmount);
            data.Add("depleted", currentRemainingAmount <= 0);
            data.Add("supportsRespawn", supportsRespawn);
            data.Add("respawnTimeRemaining", respawnTimeRemaining);
        }

        public void ReadSaveData(Dictionary<string, object> data, int version)
        {
            if (version < 1 || version > CurrentSaveDataVersion)
            {
                Debug.LogWarning("RED EDEN resource node '" + nodeId + "' does not support save schema version " +
                    version + ".", this);
                return;
            }
            if (!string.Equals(nodeId, ReadString(data, "nodeId"), StringComparison.Ordinal))
            {
                Debug.LogError("RED EDEN resource node save ID does not match the receiving node.", this);
                return;
            }
            if (resource == null || !string.Equals(resource.Id, ReadString(data, "resourceId"), StringComparison.Ordinal))
            {
                Debug.LogError("RED EDEN resource node '" + nodeId +
                    "' references a different resource definition than its saved state.", this);
                return;
            }

            currentRemainingAmount = Mathf.Clamp(ReadInt(data, "remainingAmount", remainingAmount), 0, remainingAmount);
            if (ReadBool(data, "depleted", currentRemainingAmount <= 0))
                currentRemainingAmount = 0;
            supportsRespawn = ReadBool(data, "supportsRespawn", supportsRespawn);
            respawnTimeRemaining = supportsRespawn && currentRemainingAmount == 0
                ? Mathf.Clamp(ReadFloat(data, "respawnTimeRemaining", 0f), 0f, Mathf.Max(0f, respawnDelaySeconds))
                : 0f;
            UpdateAvailability();
        }

        [ContextMenu("Generate New Stable Node ID")]
        private void GenerateNodeId()
        {
            nodeId = Guid.NewGuid().ToString("N");
        }

        private void UpdateAvailability()
        {
            if (!initialized)
                return;
            bool available = currentRemainingAmount > 0;
            foreach (Renderer target in renderers)
                if (target != null)
                    target.enabled = available;
            foreach (Collider target in colliders)
                if (target != null)
                    target.enabled = available;
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
            remainingAmount = Mathf.Max(1, remainingAmount);
            amountPerUse = Mathf.Max(1, amountPerUse);
            respawnDelaySeconds = Mathf.Max(0f, respawnDelaySeconds);
            if (string.IsNullOrWhiteSpace(nodeId) && !Application.isPlaying)
                GenerateNodeId();
        }
    }
}
