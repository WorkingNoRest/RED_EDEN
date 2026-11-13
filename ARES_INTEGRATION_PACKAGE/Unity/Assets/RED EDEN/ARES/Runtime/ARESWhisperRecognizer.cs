using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Whisper; // whisper.unity package (whisper.cpp binding) - fully offline

namespace RedEden.ARES
{
    /// <summary>
    /// Offline speech recognition through whisper.unity.
    /// On the WhisperManager component: UNTICK "Init On Awake" - this script initialises it
    /// after the launcher has downloaded the model file into ARESPaths.WhisperModelsDirectory.
    /// The WhisperManager API is provided by the pinned whisper.unity 1.4 package.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ARESWhisperRecognizer : MonoBehaviour, IARESSpeechRecognizer
    {
        [SerializeField] private WhisperManager whisper;
        [SerializeField] private string modelFileName = "ggml-small.bin";
        [Tooltip("'auto' detects Arabic / Persian / English. Use 'ar', 'fa' or 'en' to force one.")]
        [SerializeField] private string language = "auto";
        [SerializeField] private bool initializeOnStart = true;

        public bool IsReady { get; private set; }

        private bool initializing;

        private IEnumerator Start()
        {
            if (initializeOnStart)
            {
                ARESModelProvisioner provisioner = FindObjectOfType<ARESModelProvisioner>();
                while (provisioner != null &&
                    provisioner.Phase != ARESProvisionPhase.Ready &&
                    provisioner.Phase != ARESProvisionPhase.Failed)
                    yield return null;
                if (provisioner != null && provisioner.Phase == ARESProvisionPhase.Failed)
                {
                    Debug.LogError("ARES speech model setup failed: " + provisioner.Error, this);
                    yield break;
                }
                yield return InitializeRoutine();
            }
        }

        public IEnumerator InitializeRoutine()
        {
            if (IsReady || initializing)
                yield break;
            if (whisper == null)
            {
                Debug.LogError("ARES Whisper recognizer has no WhisperManager assigned.", this);
                yield break;
            }

            string path = Path.Combine(ARESPaths.WhisperModelsDirectory, modelFileName);
            if (!File.Exists(path))
            {
                Debug.LogError("ARES Whisper model file not found: " + path, this);
                yield break;
            }

            initializing = true;
            whisper.ModelPath = path;
            whisper.IsModelPathInStreamingAssets = false;
            whisper.language = language;

            Task init = whisper.InitModel();
            while (!init.IsCompleted)
                yield return null;

            initializing = false;
            if (init.IsFaulted || init.IsCanceled)
            {
                Debug.LogError("ARES Whisper failed to load: " +
                    (init.Exception != null ? init.Exception.GetBaseException().Message : "initialization was cancelled."),
                    this);
                yield break;
            }
            if (!whisper.IsLoaded)
            {
                Debug.LogError("ARES WhisperManager finished initialization without loading its model.", this);
                yield break;
            }

            IsReady = true;
            Debug.Log("ARES Whisper model ready: " + modelFileName, this);
        }

        public IEnumerator Transcribe(float[] samples, int sampleRate, Action<string> onText, Action<string> onError)
        {
            if (!IsReady)
            {
                onError?.Invoke("ARES speech model is not loaded.");
                yield break;
            }

            whisper.language = language;
            Task<WhisperResult> task = whisper.GetTextAsync(samples, sampleRate, 1);
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted || task.IsCanceled)
            {
                onError?.Invoke("ARES speech recognition failed: " +
                    (task.Exception != null ? task.Exception.GetBaseException().Message : "transcription was cancelled."));
                yield break;
            }

            WhisperResult result = task.Result;
            if (result == null)
            {
                onError?.Invoke("ARES Whisper returned no transcription result.");
                yield break;
            }

            onText?.Invoke(result.Result);
        }
    }
}
