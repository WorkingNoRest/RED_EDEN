using System;
using UnityEngine;

namespace RedEden.ARES
{
    [CreateAssetMenu(menuName = "RED EDEN/ARES/Vitals Configuration", fileName = "ARESVitalsConfiguration")]
    public sealed class ARESVitalsConfiguration : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float pollingIntervalSeconds = 1f;
        [SerializeField, Min(0f)] private float repeatedAlertCooldownSeconds = 30f;
        [SerializeField] private ARESVitalThreshold[] thresholds = Array.Empty<ARESVitalThreshold>();

        public float PollingIntervalSeconds => Mathf.Max(0.1f, pollingIntervalSeconds);
        public float RepeatedAlertCooldownSeconds => Mathf.Max(0f, repeatedAlertCooldownSeconds);
        public ARESVitalThreshold[] Thresholds => thresholds;

        private void OnValidate()
        {
            pollingIntervalSeconds = Mathf.Max(0.1f, pollingIntervalSeconds);
            repeatedAlertCooldownSeconds = Mathf.Max(0f, repeatedAlertCooldownSeconds);
        }
    }
}
