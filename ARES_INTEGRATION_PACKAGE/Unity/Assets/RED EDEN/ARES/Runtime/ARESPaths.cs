using System.IO;
using UnityEngine;

namespace RedEden.ARES
{
    /// <summary>Single source of truth for every on-disk location ARES uses.</summary>
    public static class ARESPaths
    {
        public static string Root => Path.Combine(Application.persistentDataPath, "ARES");
        public static string OllamaModelsDirectory => Path.Combine(Root, "ollama-models");
        public static string WhisperModelsDirectory => Path.Combine(Root, "whisper");

        // Generated knowledge shards (output of build_mars_knowledge.py) live here.
        public static string KnowledgeDirectory =>
            Path.Combine(Application.streamingAssetsPath, "ARES", "Knowledge");

        // Optional portable Ollama (ollama-windows-amd64.zip extracted here).
        public static string BundledOllamaExecutable =>
            Path.Combine(Application.streamingAssetsPath, "ARES", "ollama", "ollama.exe");
    }
}
