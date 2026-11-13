using DevionGames.InventorySystem;
using UnityEngine;

namespace RedEden.Crafting
{
    public enum MartianResourceType
    {
        Regolith,
        Ice,
        Mineral,
        MetalOre,
        Organic,
        Manufactured
    }

    [CreateAssetMenu(menuName = "RED EDEN/Crafting/Resource Definition")]
    public sealed class RedEdenResourceDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private MartianResourceType type;
        [SerializeField] private Item inventoryItem;
        [SerializeField, Range(0f, 1f)] private float purity = 1f;
        [SerializeField, Range(0f, 1f)] private float contamination;
        [SerializeField, Min(0f)] private float densityKilogramsPerCubicMeter;
        [SerializeField, Min(0f)] private float thermalResistance;
        [SerializeField, Min(0f)] private float materialStrengthMegapascals;
        [SerializeField, Min(1)] private int harvestAmount = 1;
        [SerializeField] private string extractionMethod;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public MartianResourceType Type => type;
        public Item InventoryItem => inventoryItem;
        public float Purity => purity;
        public float Contamination => contamination;
        public float DensityKilogramsPerCubicMeter => densityKilogramsPerCubicMeter;
        public float ThermalResistance => thermalResistance;
        public float MaterialStrengthMegapascals => materialStrengthMegapascals;
        public int HarvestAmount => Mathf.Max(1, harvestAmount);
        public string ExtractionMethod => extractionMethod;

        private void OnValidate()
        {
            harvestAmount = Mathf.Max(1, harvestAmount);
            purity = Mathf.Clamp01(purity);
            contamination = Mathf.Clamp01(contamination);
            densityKilogramsPerCubicMeter = Mathf.Max(0f, densityKilogramsPerCubicMeter);
            thermalResistance = Mathf.Max(0f, thermalResistance);
            materialStrengthMegapascals = Mathf.Max(0f, materialStrengthMegapascals);
            if (string.IsNullOrWhiteSpace(id))
                id = name;
        }
    }
}
