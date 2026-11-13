using UnityEngine;

namespace RedEden.ARES
{
    [DisallowMultipleComponent]
    public sealed class ARESInterfaceController : MonoBehaviour
    {
        [SerializeField] private ARESGeminiLiveClient liveClient;
        [SerializeField] private Vector2 margin = new Vector2(16f, 16f);

        private void OnGUI()
        {
            if (liveClient == null)
                return;

            string status;
            switch (liveClient.State)
            {
                case ARESLiveConnectionState.Connecting:
                    status = "ARES: Connecting...";
                    break;
                case ARESLiveConnectionState.Listening:
                    status = "ARES: Listening - release V when finished";
                    break;
                case ARESLiveConnectionState.Speaking:
                    status = "ARES: Speaking - hold V to interrupt";
                    break;
                case ARESLiveConnectionState.Error:
                    status = "ARES: " + (liveClient.LastError ?? "Connection error");
                    break;
                case ARESLiveConnectionState.Connected:
                    status = "ARES: Connected - hold V to speak";
                    break;
                default:
                    status = "ARES: Disconnected - hold V to connect";
                    break;
            }

            float width = Mathf.Min(460f, Mathf.Max(220f, Screen.width - margin.x * 2f));
            GUILayout.BeginArea(new Rect(margin.x, margin.y, width, 54f), GUI.skin.box);
            GUILayout.Label(status);
            GUILayout.EndArea();
        }
    }
}
