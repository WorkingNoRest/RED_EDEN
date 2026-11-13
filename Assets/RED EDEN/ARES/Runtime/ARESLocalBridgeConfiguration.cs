using UnityEngine;

namespace RedEden.ARES
{
    [CreateAssetMenu(menuName = "RED EDEN/ARES/Local LLM Configuration", fileName = "ARESLocalBridgeConfiguration")]
    public sealed class ARESLocalBridgeConfiguration : ScriptableObject
    {
        [SerializeField] private string endpoint = "http://localhost:11434/api/generate";
        [SerializeField] private string model = "qwen2.5:3b";
        [SerializeField, Min(1)] private int timeoutSeconds = 90;

        public string Endpoint => endpoint;
        public string Model => model;
        public int TimeoutSeconds => Mathf.Max(1, timeoutSeconds);

        private void OnValidate()
        {
            endpoint = string.IsNullOrWhiteSpace(endpoint)
                ? "http://localhost:11434/api/generate"
                : endpoint.Trim();
            model = string.IsNullOrWhiteSpace(model) ? "qwen2.5:3b" : model.Trim();
            timeoutSeconds = Mathf.Max(1, timeoutSeconds);
        }
    }
}
