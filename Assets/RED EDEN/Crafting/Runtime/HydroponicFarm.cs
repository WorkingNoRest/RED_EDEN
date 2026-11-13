using DevionGames.InventorySystem;
using DevionGames;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedEden.Crafting
{
    public enum FarmCropType
    {
        Potato = 0,
        Wheat = 1,
        Lettuce = 2
    }

    /// <summary>
    /// Hydroponic Farm M01 (agricultural silo).
    /// Produces Pure Oxygen Cylinders on a timer and one selectable crop (Potato / Wheat / Lettuce)
    /// on its own timer. Output is buffered inside the farm; the player collects it with E.
    /// Everything (items, amounts, durations, storage caps) is controlled from the Inspector.
    /// Place this file next to your other RED EDEN crafting scripts so it shares their assembly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HydroponicFarm : Trigger, ISceneSaveDataProvider
    {
        private const int CurrentSaveDataVersion = 1;
        private const int CropCount = 3;

        [SerializeField, HideInInspector] private string farmId;

        [Header("Farm")]
        [SerializeField] private bool operational = true;
        [SerializeField] private string inventoryWindow = "Inventory";
        [Tooltip("Which crop the farm is growing right now.")]
        [SerializeField] private FarmCropType activeCrop = FarmCropType.Potato;
        [Tooltip("Lets the player cycle the crop from the screen while in range.")]
        [SerializeField] private bool allowPlayerCropSwitch;
        [SerializeField] private KeyCode cycleCropKey = KeyCode.F;
        [Tooltip("Max units of the ACTIVE crop kept inside the farm. Growth pauses when full.")]
        [SerializeField, Min(1)] private int maxStoredFood = 30;

        [Header("Pure Oxygen Cylinder")]
        [SerializeField] private Item oxygenItem;
        [SerializeField, Min(1)] private int oxygenAmount = 1;
        [Tooltip("Seconds between each oxygen production cycle.")]
        [SerializeField, Min(1f)] private float oxygenIntervalSeconds = 60f;
        [SerializeField, Min(1)] private int maxStoredOxygen = 10;

        [Header("Potato")]
        [SerializeField] private Item potatoItem;
        [SerializeField, Min(1)] private int potatoAmount = 3;
        [SerializeField, Min(1f)] private float potatoSeconds = 120f;
        [SerializeField] private Color potatoTint = new Color(0.25f, 0.5f, 0.2f);

        [Header("Wheat")]
        [SerializeField] private Item wheatItem;
        [SerializeField, Min(1)] private int wheatAmount = 4;
        [SerializeField, Min(1f)] private float wheatSeconds = 150f;
        [SerializeField] private Color wheatTint = new Color(0.85f, 0.7f, 0.25f);

        [Header("Lettuce")]
        [SerializeField] private Item lettuceItem;
        [SerializeField, Min(1)] private int lettuceAmount = 2;
        [SerializeField, Min(1f)] private float lettuceSeconds = 90f;
        [SerializeField] private Color lettuceTint = new Color(0.45f, 0.8f, 0.3f);

        [Header("Plant Visuals")]
        [Tooltip("Plant meshes that grow upward with crop progress (no colliders on them).")]
        [SerializeField] private Transform[] plantRoots;
        [SerializeField, Range(0.05f, 1f)] private float minPlantScale = 0.25f;

        private struct CropData
        {
            public string name;
            public Item item;
            public int amount;
            public float seconds;
            public Color tint;
        }

        private readonly int[] storedFood = new int[CropCount];
        private int storedOxygen;
        private float oxygenElapsed;
        private float cropElapsed;

        private string message = string.Empty;
        private float messageTimer;

        private Vector3[] plantBasePositions;
        private Vector3[] plantBaseScales;
        private float[] plantMinY;
        private Renderer[] plantRenderers;
        private MaterialPropertyBlock block;
        private float lastGrowth = -1f;
        private FarmCropType lastTintCrop = (FarmCropType)(-1);

        // ---------------------------------------------------------------- public read API (used by the screen)
        public string SaveDataId => farmId;
        public int SaveDataVersion => CurrentSaveDataVersion;
        public bool Operational => operational;
        public bool PlayerInRange => InRange;
        public bool AllowPlayerCropSwitch => allowPlayerCropSwitch;
        public KeyCode CycleCropKey => cycleCropKey;
        public FarmCropType ActiveCrop => activeCrop;
        public string CropName => activeCrop.ToString().ToUpperInvariant();

        public bool CropConfigured { get { CropData c = GetCrop(activeCrop); return c.item != null && c.seconds > 0f; } }
        public int CropAmountPerCycle => GetCrop(activeCrop).amount;
        public int StoredCrop => storedFood[(int)activeCrop];
        public int MaxStoredCrop => maxStoredFood;
        public bool CropStorageFull => StoredCrop >= maxStoredFood;
        public float CropProgress => !CropConfigured ? 0f : CropStorageFull ? 1f : Mathf.Clamp01(cropElapsed / GetCrop(activeCrop).seconds);
        public float CropSecondsLeft => !CropConfigured || CropStorageFull ? 0f : Mathf.Max(0f, GetCrop(activeCrop).seconds - cropElapsed);

        public bool OxygenConfigured => oxygenItem != null && oxygenIntervalSeconds > 0f;
        public int OxygenAmountPerCycle => oxygenAmount;
        public int StoredOxygen => storedOxygen;
        public int MaxStoredOxygen => maxStoredOxygen;
        public bool OxygenStorageFull => storedOxygen >= maxStoredOxygen;
        public float OxygenProgress => !OxygenConfigured ? 0f : OxygenStorageFull ? 1f : Mathf.Clamp01(oxygenElapsed / oxygenIntervalSeconds);
        public float OxygenSecondsLeft => !OxygenConfigured || OxygenStorageFull ? 0f : Mathf.Max(0f, oxygenIntervalSeconds - oxygenElapsed);

        public string Message => messageTimer > 0f ? message : string.Empty;

        // ---------------------------------------------------------------- lifecycle
        private void Awake()
        {
            block = new MaterialPropertyBlock();

            int count = plantRoots != null ? plantRoots.Length : 0;
            plantBasePositions = new Vector3[count];
            plantBaseScales = new Vector3[count];
            plantMinY = new float[count];
            var renderers = new List<Renderer>();
            for (int i = 0; i < count; i++)
            {
                Transform t = plantRoots[i];
                if (t == null) continue;
                plantBasePositions[i] = t.localPosition;
                plantBaseScales[i] = t.localScale;
                MeshFilter mf = t.GetComponentInChildren<MeshFilter>();
                plantMinY[i] = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.min.y : 0f;
                renderers.AddRange(t.GetComponentsInChildren<Renderer>(true));
            }
            plantRenderers = renderers.ToArray();
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

            if (allowPlayerCropSwitch && InRange && UnityTools.GetKeyDown(cycleCropKey))
                CycleCrop();

            Tick(Time.deltaTime);

            if (messageTimer > 0f)
                messageTimer = Mathf.Max(0f, messageTimer - Time.deltaTime);

            UpdatePlantVisuals();
        }

        public override bool Use()
        {
            if (!base.Use())
                return false;

            try
            {
                if (!operational)
                {
                    SetMessage("FARM OFFLINE");
                    return false;
                }
                int moved = CollectAll(out string result);
                SetMessage(result);
                return moved > 0;
            }
            finally
            {
                StopUse();
            }
        }

        // ---------------------------------------------------------------- production
        private void Tick(float dt)
        {
            if (!operational)
                return;

            if (OxygenConfigured)
            {
                if (storedOxygen >= maxStoredOxygen)
                {
                    oxygenElapsed = 0f;
                }
                else
                {
                    oxygenElapsed += dt;
                    while (oxygenElapsed >= oxygenIntervalSeconds && storedOxygen < maxStoredOxygen)
                    {
                        oxygenElapsed -= oxygenIntervalSeconds;
                        storedOxygen = Mathf.Min(maxStoredOxygen, storedOxygen + oxygenAmount);
                    }
                    if (storedOxygen >= maxStoredOxygen)
                        oxygenElapsed = 0f;
                }
            }

            CropData crop = GetCrop(activeCrop);
            if (crop.item != null && crop.seconds > 0f)
            {
                int index = (int)activeCrop;
                if (storedFood[index] >= maxStoredFood)
                {
                    cropElapsed = 0f;
                }
                else
                {
                    cropElapsed += dt;
                    while (cropElapsed >= crop.seconds && storedFood[index] < maxStoredFood)
                    {
                        cropElapsed -= crop.seconds;
                        storedFood[index] = Mathf.Min(maxStoredFood, storedFood[index] + crop.amount);
                    }
                    if (storedFood[index] >= maxStoredFood)
                        cropElapsed = 0f;
                }
            }
        }

        public void SetCrop(FarmCropType crop)
        {
            if (!Enum.IsDefined(typeof(FarmCropType), crop))
            {
                Debug.LogError("RED EDEN hydroponic farm cannot use an unsupported crop type.", this);
                return;
            }
            if (crop == activeCrop)
                return;
            activeCrop = crop;
            cropElapsed = 0f;
            lastGrowth = -1f;
            SetMessage("CROP SET: " + CropName);
        }

        public void CycleCrop()
        {
            SetCrop((FarmCropType)(((int)activeCrop + 1) % CropCount));
        }

        public void SetOperational(bool value)
        {
            operational = value;
        }

        // ---------------------------------------------------------------- collecting
        public int CollectAll(out string result)
        {
            ItemContainer inventory = RedEdenInventoryBridge.FindInventory(inventoryWindow);
            if (inventory == null)
            {
                result = "INVENTORY NOT FOUND";
                return 0;
            }

            int total = 0;
            bool full = false;
            total += Transfer(inventory, oxygenItem, ref storedOxygen, ref full);
            for (int i = 0; i < CropCount; i++)
                total += Transfer(inventory, GetCrop((FarmCropType)i).item, ref storedFood[i], ref full);

            if (total == 0)
                result = full ? "INVENTORY FULL" : "NOTHING READY YET";
            else
                result = "COLLECTED x" + total + (full ? " - INVENTORY FULL" : string.Empty);
            return total;
        }

        private static int Transfer(ItemContainer inventory, Item item, ref int stored, ref bool full)
        {
            if (item == null || stored <= 0)
                return 0;

            int low = 0;
            int high = stored;
            while (low < high)
            {
                int candidate = low + (int)(((long)high - low + 1L) / 2L);
                if (RedEdenInventoryBridge.CanReceive(inventory, item, candidate))
                    low = candidate;
                else
                    high = candidate - 1;
            }

            if (low == 0 || !RedEdenInventoryBridge.TryAdd(inventory, item, low))
            {
                full = true;
                return 0;
            }

            stored -= low;
            if (stored > 0)
                full = true;
            return low;
        }

        private CropData GetCrop(FarmCropType type)
        {
            switch (type)
            {
                case FarmCropType.Wheat:
                    return new CropData { name = "Wheat", item = wheatItem, amount = wheatAmount, seconds = wheatSeconds, tint = wheatTint };
                case FarmCropType.Lettuce:
                    return new CropData { name = "Lettuce", item = lettuceItem, amount = lettuceAmount, seconds = lettuceSeconds, tint = lettuceTint };
                default:
                    return new CropData { name = "Potato", item = potatoItem, amount = potatoAmount, seconds = potatoSeconds, tint = potatoTint };
            }
        }

        private void SetMessage(string text)
        {
            message = text;
            messageTimer = 4f;
        }

        // ---------------------------------------------------------------- plant visuals
        private void UpdatePlantVisuals()
        {
            if (plantRoots == null || plantRoots.Length == 0)
                return;

            if (lastTintCrop != activeCrop)
            {
                lastTintCrop = activeCrop;
                Color tint = GetCrop(activeCrop).tint;
                foreach (Renderer r in plantRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", tint);
                    block.SetColor("_Color", tint);
                    r.SetPropertyBlock(block);
                }
            }

            float growth = CropProgress;
            if (Mathf.Abs(growth - lastGrowth) < 0.002f)
                return;
            lastGrowth = growth;

            float s = Mathf.Lerp(minPlantScale, 1f, growth);
            for (int i = 0; i < plantRoots.Length; i++)
            {
                Transform t = plantRoots[i];
                if (t == null) continue;

                // Scale upward from the base of the mesh so plants grow out of the trays.
                Vector3 scale = plantBaseScales[i];
                scale.y *= s;
                t.localScale = scale;

                Vector3 pos = plantBasePositions[i];
                pos.y += plantMinY[i] * plantBaseScales[i].y * (1f - s);
                t.localPosition = pos;
            }
        }

        protected override void OnDisable()
        {
            if (plantRoots != null && plantBasePositions != null)
            {
                for (int i = 0; i < plantRoots.Length && i < plantBasePositions.Length; i++)
                {
                    if (plantRoots[i] == null) continue;
                    plantRoots[i].localPosition = plantBasePositions[i];
                    plantRoots[i].localScale = plantBaseScales[i];
                }
            }
            lastGrowth = -1f;
            base.OnDisable();
        }

        // ---------------------------------------------------------------- save / load
        public void WriteSaveData(Dictionary<string, object> data)
        {
            if (string.IsNullOrWhiteSpace(farmId))
                throw new InvalidOperationException("RED EDEN hydroponic farm is missing its stable farm ID.");

            data.Add("farmId", farmId);
            data.Add("activeCrop", activeCrop.ToString());
            data.Add("operational", operational);
            data.Add("oxygenElapsed", oxygenElapsed);
            data.Add("cropElapsed", cropElapsed);
            data.Add("oxygenStored", storedOxygen);
            for (int i = 0; i < CropCount; i++)
                data.Add("food_" + ((FarmCropType)i), storedFood[i]);
        }

        public void ReadSaveData(Dictionary<string, object> data, int version)
        {
            if (version < 1 || version > CurrentSaveDataVersion)
            {
                Debug.LogWarning("RED EDEN hydroponic farm '" + farmId + "' does not support save schema version " + version + ".", this);
                return;
            }
            if (!string.Equals(farmId, ReadString(data, "farmId"), StringComparison.Ordinal))
            {
                Debug.LogError("RED EDEN hydroponic farm save ID does not match the receiving farm.", this);
                return;
            }

            if (Enum.TryParse(ReadString(data, "activeCrop"), out FarmCropType savedCrop) &&
                Enum.IsDefined(typeof(FarmCropType), savedCrop))
                activeCrop = savedCrop;
            else
                Debug.LogWarning("RED EDEN hydroponic farm '" + farmId +
                    "' save data has an unsupported crop type; keeping the configured crop.", this);
            operational = ReadBool(data, "operational", operational);
            oxygenElapsed = Mathf.Max(0f, ReadFloat(data, "oxygenElapsed", 0f));
            cropElapsed = Mathf.Max(0f, ReadFloat(data, "cropElapsed", 0f));
            storedOxygen = Mathf.Clamp(ReadInt(data, "oxygenStored", 0), 0, maxStoredOxygen);
            for (int i = 0; i < CropCount; i++)
                storedFood[i] = Mathf.Clamp(ReadInt(data, "food_" + ((FarmCropType)i), 0), 0, maxStoredFood);
            lastGrowth = -1f;
            lastTintCrop = (FarmCropType)(-1);
        }

        [ContextMenu("Generate New Stable Farm ID")]
        private void GenerateFarmId()
        {
            farmId = Guid.NewGuid().ToString("N");
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
            maxStoredFood = Mathf.Max(1, maxStoredFood);
            maxStoredOxygen = Mathf.Max(1, maxStoredOxygen);
            oxygenAmount = Mathf.Max(1, oxygenAmount);
            potatoAmount = Mathf.Max(1, potatoAmount);
            wheatAmount = Mathf.Max(1, wheatAmount);
            lettuceAmount = Mathf.Max(1, lettuceAmount);
            if (string.IsNullOrWhiteSpace(farmId) && !Application.isPlaying)
                GenerateFarmId();
        }
    }
}
