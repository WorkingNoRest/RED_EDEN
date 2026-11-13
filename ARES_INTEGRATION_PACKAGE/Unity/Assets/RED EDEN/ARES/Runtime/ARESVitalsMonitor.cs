using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RedEden.ARES
{
    [DisallowMultipleComponent]
    public sealed class ARESVitalsMonitor : MonoBehaviour
    {
        [SerializeField] private ARESVitalsConfiguration configuration;
        [SerializeField] private MonoBehaviour vitalsSource;

        private IARESSurvivalVitalsSource source;
        private readonly Dictionary<ARESVitalMetric, float> lastAlertTime =
            new Dictionary<ARESVitalMetric, float>();
        private readonly HashSet<ARESVitalMetric> currentlyAlerting = new HashSet<ARESVitalMetric>();

        private void Awake()
        {
            source = vitalsSource as IARESSurvivalVitalsSource;
            if (vitalsSource != null && source == null)
                Debug.LogError("ARES vitals component does not implement IARESSurvivalVitalsSource.", vitalsSource);
            else if (source == null)
                Debug.LogError("ARES vitals source is not assigned; proactive alerts are unavailable.", this);
        }

        private void OnEnable()
        {
            StartCoroutine(MonitorVitals());
        }

        private IEnumerator MonitorVitals()
        {
            yield return null;
            while (true)
            {
                if (configuration != null && source != null)
                {
                    ARESVitalThreshold[] thresholds = configuration.Thresholds;
                    if (thresholds == null)
                    {
                        Debug.LogError("ARES vitals configuration has no threshold list.", configuration);
                        yield break;
                    }

                    foreach (ARESVitalThreshold threshold in thresholds)
                    {
                        if (threshold == null || !source.TryGetValue(threshold.Metric, out float value))
                            continue;

                        bool outsideSafeRange = threshold.Direction == ARESAlertDirection.Below
                            ? value < threshold.Threshold
                            : value > threshold.Threshold;
                        if (!outsideSafeRange)
                        {
                            if (IsBeyondResetThreshold(threshold, value))
                                currentlyAlerting.Remove(threshold.Metric);
                            continue;
                        }

                        float previousAlert;
                        bool hasPreviousAlert = lastAlertTime.TryGetValue(threshold.Metric, out previousAlert);
                        bool cooldownElapsed = !hasPreviousAlert ||
                            Time.unscaledTime - previousAlert >= configuration.RepeatedAlertCooldownSeconds;
                        if (!currentlyAlerting.Contains(threshold.Metric) || cooldownElapsed)
                        {
                            lastAlertTime[threshold.Metric] = Time.unscaledTime;
                            currentlyAlerting.Add(threshold.Metric);
                            ARESEventBus.PublishVitalAlert(new ARESVitalAlert(threshold.Metric, value,
                                threshold.Threshold, GetAlertMessage(threshold)));
                        }
                    }
                }
                else if (configuration == null)
                {
                    Debug.LogError("ARES vitals configuration is not assigned.", this);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(configuration.PollingIntervalSeconds);
            }
        }

        private static bool IsBeyondResetThreshold(ARESVitalThreshold threshold, float value)
        {
            return threshold.Direction == ARESAlertDirection.Below
                ? value >= threshold.Threshold + threshold.ResetHysteresis
                : value <= threshold.Threshold - threshold.ResetHysteresis;
        }

        private static string GetAlertMessage(ARESVitalThreshold threshold)
        {
            return string.IsNullOrWhiteSpace(threshold.AlertMessage)
                ? threshold.Metric + " has crossed its configured safety threshold."
                : threshold.AlertMessage;
        }
    }
}
