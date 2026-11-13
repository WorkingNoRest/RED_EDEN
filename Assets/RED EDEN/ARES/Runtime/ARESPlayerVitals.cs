using DevionGames.StatSystem;
using UnityEngine;

namespace RedEden.ARES
{
    [DisallowMultipleComponent]
    public sealed class ARESPlayerVitals : MonoBehaviour, IARESSurvivalVitalsSource
    {
        [SerializeField] private StatsHandler healthStatsHandler;
        [SerializeField] private string healthStatName = "Health";
        [SerializeField, Range(0f, 100f)] private float healthFallbackPercent = 100f;
        [SerializeField, Range(0f, 100f)] private float oxygenPercent = 100f;
        [SerializeField, Range(0f, 100f)] private float hungerPercent = 100f;
        [SerializeField, Range(0f, 100f)] private float thirstPercent = 100f;
        [SerializeField, Range(0f, 100f)] private float staminaPercent = 100f;
        [SerializeField, Range(0f, 100f)] private float radiationPercent;

        private Attribute healthAttribute;
        private bool healthLookupReported;

        public float HealthPercent => ReadHealthPercent();
        public float OxygenPercent => oxygenPercent;
        public float HungerPercent => hungerPercent;
        public float ThirstPercent => thirstPercent;
        public float StaminaPercent => staminaPercent;
        public float RadiationPercent => radiationPercent;

        private void Awake()
        {
            ResolveHealthStat();
        }

        private void Start()
        {
            ResolveHealthStat();
        }

        public bool TryGetValue(ARESVitalMetric metric, out float value)
        {
            switch (metric)
            {
                case ARESVitalMetric.Oxygen:
                    value = oxygenPercent;
                    return true;
                case ARESVitalMetric.Health:
                    value = ReadHealthPercent();
                    return true;
                case ARESVitalMetric.Hunger:
                    value = hungerPercent;
                    return true;
                case ARESVitalMetric.Thirst:
                    value = thirstPercent;
                    return true;
                case ARESVitalMetric.Stamina:
                    value = staminaPercent;
                    return true;
                case ARESVitalMetric.Radiation:
                    value = radiationPercent;
                    return true;
                default:
                    value = 0f;
                    return false;
            }
        }

        public bool SetValue(ARESVitalMetric metric, float value)
        {
            value = Mathf.Clamp(value, 0f, 100f);
            switch (metric)
            {
                case ARESVitalMetric.Oxygen:
                    oxygenPercent = value;
                    return true;
                case ARESVitalMetric.Health:
                    SetHealthPercent(value);
                    return true;
                case ARESVitalMetric.Hunger:
                    hungerPercent = value;
                    return true;
                case ARESVitalMetric.Thirst:
                    thirstPercent = value;
                    return true;
                case ARESVitalMetric.Stamina:
                    staminaPercent = value;
                    return true;
                case ARESVitalMetric.Radiation:
                    radiationPercent = value;
                    return true;
                default:
                    return false;
            }
        }

        public bool ModifyValue(ARESVitalMetric metric, float delta)
        {
            if (!TryGetValue(metric, out float currentValue))
                return false;
            return SetValue(metric, currentValue + delta);
        }

        private float ReadHealthPercent()
        {
            ResolveHealthStat();
            if (healthAttribute == null || healthAttribute.Value <= 0f)
                return healthFallbackPercent;

            return Mathf.Clamp01(healthAttribute.CurrentValue / healthAttribute.Value) * 100f;
        }

        private void SetHealthPercent(float value)
        {
            ResolveHealthStat();
            if (healthAttribute == null || healthAttribute.Value <= 0f)
            {
                healthFallbackPercent = value;
                return;
            }

            healthAttribute.CurrentValue = healthAttribute.Value * value / 100f;
        }

        private void ResolveHealthStat()
        {
            if (healthStatsHandler == null)
                healthStatsHandler = GetComponent<StatsHandler>();

            Attribute resolvedAttribute = healthStatsHandler != null &&
                !string.IsNullOrWhiteSpace(healthStatName)
                ? healthStatsHandler.GetStat(healthStatName) as Attribute
                : null;
            if (healthAttribute != resolvedAttribute)
                healthAttribute = resolvedAttribute;

            if (healthAttribute == null && !healthLookupReported)
            {
                healthLookupReported = true;
                Debug.LogWarning("ARES could not find the configured Health attribute. " +
                    "It will report the fallback health value until the existing stat is assigned.", this);
            }
        }

        private void OnValidate()
        {
            healthFallbackPercent = Mathf.Clamp(healthFallbackPercent, 0f, 100f);
            oxygenPercent = Mathf.Clamp(oxygenPercent, 0f, 100f);
            hungerPercent = Mathf.Clamp(hungerPercent, 0f, 100f);
            thirstPercent = Mathf.Clamp(thirstPercent, 0f, 100f);
            staminaPercent = Mathf.Clamp(staminaPercent, 0f, 100f);
            radiationPercent = Mathf.Clamp(radiationPercent, 0f, 100f);
        }
    }
}
