using UnityEngine;

namespace RedEden.ARES
{
    [CreateAssetMenu(menuName = "RED EDEN/ARES/Gemini Live Configuration", fileName = "ARES_GeminiLiveConfiguration")]
    public sealed class ARESGeminiLiveConfiguration : ScriptableObject
    {
        [SerializeField] private string backendBaseUrl = "https://red-eden.onrender.com";
        [SerializeField] private string defaultLanguage = "Arabic";

        public string BackendBaseUrl => backendBaseUrl;
        public string DefaultLanguage => string.IsNullOrWhiteSpace(defaultLanguage) ? "Arabic" : defaultLanguage;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(backendBaseUrl))
                backendBaseUrl = "https://red-eden.onrender.com";
            else
                backendBaseUrl = backendBaseUrl.Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(defaultLanguage))
                defaultLanguage = "Arabic";
            else
                defaultLanguage = defaultLanguage.Trim();
        }
    }
}
