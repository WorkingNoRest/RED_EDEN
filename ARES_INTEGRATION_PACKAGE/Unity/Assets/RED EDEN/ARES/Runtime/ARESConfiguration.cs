using System;
using UnityEngine;

namespace RedEden.ARES
{
    [CreateAssetMenu(menuName = "RED EDEN/ARES/System Configuration", fileName = "ARESSystemConfiguration")]
    public sealed class ARESSystemConfiguration : ScriptableObject
    {
        public const string PlayerName = "آدم مهران";
        [SerializeField] private string defaultResponseLanguage = "English";
        [SerializeField, TextArea(2, 6)] private string persona =
            "You are ARES, a calm, professional Mars mission companion. Be concise, safety-aware, and transparent about uncertainty.";
        [SerializeField, TextArea(2, 4)] private string noArchiveRecordMessage =
            "The local NASA Mars archives contain no relevant records for that query. I cannot verify an answer from the available local sources.";
        [SerializeField, TextArea(2, 4)] private string noArchiveRecordMessagePersian =
            "در بایگانی محلی ناسا اطلاعات مرتبطی برای این پرسش پیدا نشد؛ بنابراین نمی‌توانم پاسخی را بر پایه منابع موجود تأیید کنم.";
        [SerializeField, Min(1)] private int maximumContextRecords = 3;
        [SerializeField, Min(1)] private int minimumMatchScore = 5;

        public string DefaultResponseLanguage => defaultResponseLanguage;
        public string Persona => persona;
        public int MaximumContextRecords => Mathf.Max(1, maximumContextRecords);
        public int MinimumMatchScore => Mathf.Max(5, minimumMatchScore);

        public string GetNoArchiveRecordMessage(string language)
        {
            bool isPersian = !string.IsNullOrWhiteSpace(language) &&
                (language.StartsWith("fa", StringComparison.OrdinalIgnoreCase) ||
                 language.Equals("Persian", StringComparison.OrdinalIgnoreCase) ||
                 language.Equals("Farsi", StringComparison.OrdinalIgnoreCase));
            return isPersian
                ? noArchiveRecordMessagePersian
                : noArchiveRecordMessage;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(defaultResponseLanguage))
                defaultResponseLanguage = "English";
            maximumContextRecords = Mathf.Max(1, maximumContextRecords);
            minimumMatchScore = Mathf.Max(5, minimumMatchScore);
        }
    }

    [Serializable]
    public sealed class ARESVitalThreshold
    {
        [SerializeField] private ARESVitalMetric metric;
        [SerializeField] private ARESAlertDirection direction;
        [SerializeField] private float threshold;
        [SerializeField, Min(0f)] private float resetHysteresis = 5f;
        [SerializeField, TextArea(1, 3)] private string alertMessage;

        public ARESVitalMetric Metric => metric;
        public ARESAlertDirection Direction => direction;
        public float Threshold => threshold;
        public float ResetHysteresis => Mathf.Max(0f, resetHysteresis);
        public string AlertMessage => alertMessage;
    }

}
