using System;
using UnityEngine;

namespace RedEden.ARES
{
    [Serializable]
    public sealed class ARESRequiredFile
    {
        public string displayName = "Speech recognition model";
        public string fileName = "ggml-small.bin";
        public string url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin";
        [Tooltip("A file smaller than this is treated as missing/corrupt and re-downloaded.")]
        public long minimumBytes = 400_000_000L;
    }

    [CreateAssetMenu(menuName = "RED EDEN/ARES/Provisioning Configuration", fileName = "ARESProvisioningConfiguration")]
    public sealed class ARESProvisioningConfiguration : ScriptableObject
    {
        [Tooltip("Endpoint + model tag are read from here (single source of truth).")]
        [SerializeField] private ARESLocalBridgeConfiguration bridgeConfiguration;
        [Tooltip("Start StreamingAssets/ARES/ollama/ollama.exe (or 'ollama' from PATH) when no server answers.")]
        [SerializeField] private bool useBundledOllama = true;
        [SerializeField, Min(5)] private int serverStartTimeoutSeconds = 40;
        [SerializeField] private string keepAlive = "30m";
        [SerializeField] private ARESRequiredFile[] requiredFiles = { new ARESRequiredFile() };

        public ARESLocalBridgeConfiguration BridgeConfiguration => bridgeConfiguration;
        public bool UseBundledOllama => useBundledOllama;
        public int ServerStartTimeoutSeconds => Mathf.Max(5, serverStartTimeoutSeconds);
        public string KeepAlive => string.IsNullOrWhiteSpace(keepAlive) ? "30m" : keepAlive.Trim();
        public ARESRequiredFile[] RequiredFiles => requiredFiles ?? Array.Empty<ARESRequiredFile>();
    }
}
