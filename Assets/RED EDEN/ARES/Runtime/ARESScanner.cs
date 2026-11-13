using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RedEden.ARES
{
    [DisallowMultipleComponent]
    public sealed class ARESScanner : MonoBehaviour
    {
        [SerializeField] private ARESResourceDatabase resourceDatabase;
        [SerializeField, Min(0.1f)] private float scanRadius = 20f;
        [SerializeField] private LayerMask scanLayers = ~0;
        [SerializeField] private Transform scanOrigin;
        [SerializeField] private MonoBehaviour mapMarkerSink;
        [SerializeField, TextArea] private string noResourcesFoundMessage =
            "No catalogued resources were detected nearby.";

        private IARESMapMarkerSink markerSink;

        private void Awake()
        {
            markerSink = mapMarkerSink as IARESMapMarkerSink;
            if (mapMarkerSink != null && markerSink == null)
                Debug.LogError("ARES map marker component does not implement IARESMapMarkerSink.", mapMarkerSink);
        }

        public IReadOnlyList<ARESResourceScanResult> Scan()
        {
            List<ARESResourceScanResult> results = new List<ARESResourceScanResult>();
            if (resourceDatabase == null)
            {
                Debug.LogError("ARES scanner has no resource database assigned.", this);
                ARESEventBus.PublishScanCompleted(results);
                return results;
            }

            Vector3 center = scanOrigin != null ? scanOrigin.position : transform.position;
            Collider[] hits = Physics.OverlapSphere(center, Mathf.Max(0.1f, scanRadius), scanLayers,
                QueryTriggerInteraction.Collide);
            HashSet<ARESScannableResource> seen = new HashSet<ARESScannableResource>();

            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                ARESScannableResource scannable = hit.GetComponentInParent<ARESScannableResource>();
                if (scannable == null || scannable.Resource == null ||
                    !resourceDatabase.Contains(scannable.Resource) || !seen.Add(scannable))
                    continue;

                ARESResourceScanResult result = new ARESResourceScanResult(scannable.Resource,
                    scannable.transform.position);
                results.Add(result);
                markerSink?.ShowResourceMarker(result.Resource, result.Position);
                ARESEventBus.PublishResourceLocated(result);
            }

            ARESEventBus.PublishScanCompleted(results);
            if (results.Count == 0)
            {
                ARESEventBus.PublishResponse(new ARESResponse(noResourcesFoundMessage, false));
            }
            else
            {
                StringBuilder summary = new StringBuilder("Resource scan complete. Located ");
                summary.Append(results.Count).Append(" resource");
                if (results.Count != 1)
                    summary.Append('s');
                summary.AppendLine(":");

                foreach (ARESResourceScanResult result in results)
                    summary.Append("- ").Append(result.Resource.DisplayName).Append(": ")
                        .AppendLine(result.FormattedCoordinates);

                ARESEventBus.PublishResponse(new ARESResponse(summary.ToString().TrimEnd(), false));
            }
            return results;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(scanOrigin != null ? scanOrigin.position : transform.position, scanRadius);
        }
    }
}
