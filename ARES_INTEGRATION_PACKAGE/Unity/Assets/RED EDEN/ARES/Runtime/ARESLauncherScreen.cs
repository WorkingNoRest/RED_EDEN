using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedEden.ARES
{
    /// <summary>
    /// Minimal IMGUI launcher screen. Put it in a first "Launcher" scene next to ARESModelProvisioner.
    /// Replace with your own UI by reading provisioner.Phase / Progress / StatusText / Error.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ARESLauncherScreen : MonoBehaviour
    {
        [SerializeField] private ARESModelProvisioner provisioner;
        [Tooltip("Scene to load once everything is ready. Leave empty to just hide this screen.")]
        [SerializeField] private string nextSceneName = "";
        [SerializeField] private GameObject[] enableWhenReady;

        private bool finished;

        private void Update()
        {
            if (finished || provisioner == null || provisioner.Phase != ARESProvisionPhase.Ready)
                return;

            finished = true;
            foreach (GameObject target in enableWhenReady)
                if (target != null)
                    target.SetActive(true);
            if (!string.IsNullOrWhiteSpace(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
        }

        private void OnGUI()
        {
            if (finished || provisioner == null)
                return;

            float width = Mathf.Min(520f, Screen.width - 40f);
            Rect box = new Rect((Screen.width - width) / 2f, Screen.height / 2f - 90f, width, 180f);
            GUILayout.BeginArea(box, GUI.skin.box);
            GUILayout.Label("RED EDEN - ARES setup");
            GUILayout.Space(6f);
            GUILayout.Label(provisioner.StatusText);

            Rect bar = GUILayoutUtility.GetRect(width - 20f, 18f);
            GUI.Box(bar, GUIContent.none);
            Rect fill = new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * provisioner.Progress, bar.height - 4f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);

            if (provisioner.Phase == ARESProvisionPhase.Failed)
            {
                GUILayout.Space(8f);
                if (GUILayout.Button("Retry", GUILayout.Height(30f)))
                    provisioner.Retry();
            }
            else if (provisioner.Phase == ARESProvisionPhase.DownloadingModel ||
                     provisioner.Phase == ARESProvisionPhase.DownloadingSpeechModel)
            {
                GUILayout.Space(6f);
                GUILayout.Label("First launch only - an internet connection is required for this download.");
            }
            GUILayout.EndArea();
        }
    }
}
