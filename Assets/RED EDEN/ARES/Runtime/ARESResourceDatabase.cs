using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedEden.ARES
{
    [CreateAssetMenu(menuName = "RED EDEN/ARES/Resource Definition", fileName = "ARESResource")]
    public sealed class ARESResourceDefinition : ScriptableObject
    {
        [SerializeField] private string resourceId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string[] searchTerms = Array.Empty<string>();

        public string ResourceId => resourceId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public IReadOnlyList<string> SearchTerms => searchTerms;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                resourceId = name;
        }
    }

    [CreateAssetMenu(menuName = "RED EDEN/ARES/Resource Database", fileName = "ARESResourceDatabase")]
    public sealed class ARESResourceDatabase : ScriptableObject
    {
        [SerializeField] private List<ARESResourceDefinition> resources = new List<ARESResourceDefinition>();

        public IReadOnlyList<ARESResourceDefinition> Resources => resources;

        public bool Contains(ARESResourceDefinition resource)
        {
            return resource != null && resources.Contains(resource);
        }

        private void OnValidate()
        {
            if (resources == null)
                resources = new List<ARESResourceDefinition>();

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ARESResourceDefinition resource in resources)
            {
                if (resource == null)
                {
                    Debug.LogWarning("ARES resource database contains an empty resource reference.", this);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(resource.ResourceId))
                    Debug.LogWarning("ARES resource '" + resource.name + "' needs a stable resource ID.", resource);
                else if (!ids.Add(resource.ResourceId))
                    Debug.LogWarning("ARES resource database contains duplicate ID '" + resource.ResourceId + "'.", this);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class ARESScannableResource : MonoBehaviour
    {
        [SerializeField] private ARESResourceDefinition resource;

        public ARESResourceDefinition Resource => resource;
    }
}
