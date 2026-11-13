using System.IO;
using UnityEngine;

namespace RedEden.ARES
{
    /// <summary>Single source of truth for every on-disk location ARES uses.</summary>
    public static class ARESPaths
    {
        public static string KnowledgeDirectory =>
            Path.Combine(Application.streamingAssetsPath, "ARES", "Knowledge");
    }
}
