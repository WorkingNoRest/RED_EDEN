using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RedEden.ARES
{
    [DisallowMultipleComponent]
    public sealed class ARESController : MonoBehaviour
    {
        [SerializeField] private ARESSystemConfiguration configuration;
        [SerializeField] private ARESRAGHandler ragHandler;
        [SerializeField] private ARESGeminiLiveClient geminiLiveClient;
        [SerializeField] private MonoBehaviour gameSettingsProvider;
        [SerializeField] private MonoBehaviour voiceInputProvider;

        private IARESGameSettingsProvider gameSettings;
        private IARESStreamingVoiceInput streamingVoiceInput;
        private bool holdingTalkKey;
        private bool liveActivityStarted;
        private string latestLiveTranscript = string.Empty;

        private void Awake()
        {
            gameSettings = gameSettingsProvider as IARESGameSettingsProvider;
            streamingVoiceInput = voiceInputProvider as IARESStreamingVoiceInput;

            if (gameSettingsProvider != null && gameSettings == null)
                Debug.LogError("ARES game settings component does not implement IARESGameSettingsProvider.", gameSettingsProvider);
            if (voiceInputProvider == null || streamingVoiceInput == null)
                Debug.LogError("ARES Live requires a microphone component that supports PCM streaming.", this);
            if (geminiLiveClient == null)
                Debug.LogError("ARES Gemini Live client is not assigned.", this);
        }

        private void OnEnable()
        {
            ARESEventBus.VitalAlertRaised += OnVitalAlertRaised;
            if (geminiLiveClient != null)
            {
                geminiLiveClient.InputTranscriptReceived += OnLiveInputTranscript;
                geminiLiveClient.StateChanged += OnLiveConnectionStateChanged;
            }
        }

        private void OnDisable()
        {
            ARESEventBus.VitalAlertRaised -= OnVitalAlertRaised;
            if (geminiLiveClient != null)
            {
                geminiLiveClient.InputTranscriptReceived -= OnLiveInputTranscript;
                geminiLiveClient.StateChanged -= OnLiveConnectionStateChanged;
            }

            EndLiveSpeech();
            if (geminiLiveClient != null)
                geminiLiveClient.Disconnect();
        }

        private void Update()
        {
            if (WasInteractionKeyPressed())
                BeginLiveSpeech();
            if (WasInteractionKeyReleased())
                EndLiveSpeech();
        }

        public void BeginInteraction()
        {
            BeginLiveSpeech();
        }

        public void SubmitText(string text)
        {
            PublishError("Text input is not supported. Hold V to speak with ARES Gemini Live.");
        }

        private void OnVitalAlertRaised(ARESVitalAlert alert)
        {
            if (alert != null && !string.IsNullOrWhiteSpace(alert.Message))
                PublishResponse(alert.Message, false);
        }

        private void BeginLiveSpeech()
        {
            if (holdingTalkKey)
                return;
            holdingTalkKey = true;
            latestLiveTranscript = string.Empty;

            if (geminiLiveClient == null)
            {
                holdingTalkKey = false;
                PublishError("ARES Gemini Live client is not assigned.");
                return;
            }
            if (streamingVoiceInput == null)
            {
                holdingTalkKey = false;
                PublishError("ARES Live requires a microphone component that supports PCM streaming.");
                return;
            }

            ARESEventBus.RequestInteraction(new ARESInteractionRequest(GetResponseLanguage()));
            if (geminiLiveClient.IsReady)
            {
                StartLiveActivity();
                return;
            }
            if (geminiLiveClient.IsConnecting)
                return;

            StartCoroutine(geminiLiveClient.Connect(
                GetResponseLanguage(),
                StartLiveActivity,
                message =>
                {
                    holdingTalkKey = false;
                    HandleInputError(message);
                }));
        }

        private void StartLiveActivity()
        {
            if (!holdingTalkKey || geminiLiveClient == null)
                return;
            if (!geminiLiveClient.BeginUserActivity())
            {
                holdingTalkKey = false;
                HandleInputError("ARES could not start a Live voice activity.");
                return;
            }

            liveActivityStarted = true;
            streamingVoiceInput.BeginPcmStream(
                chunk => geminiLiveClient.SendPcm(chunk),
                HandleLiveMicrophoneError);
        }

        private void EndLiveSpeech()
        {
            if (!holdingTalkKey)
                return;
            holdingTalkKey = false;

            if (streamingVoiceInput != null && streamingVoiceInput.IsStreaming)
                streamingVoiceInput.EndPcmStream();

            if (!liveActivityStarted)
                return;
            if (geminiLiveClient == null || !geminiLiveClient.IsReady)
            {
                liveActivityStarted = false;
                return;
            }

            AddLocalArchiveEvidence();
            geminiLiveClient.EndUserActivity();
            liveActivityStarted = false;
        }

        private void HandleLiveMicrophoneError(string message)
        {
            if (streamingVoiceInput != null && streamingVoiceInput.IsStreaming)
                streamingVoiceInput.EndPcmStream();
            if (liveActivityStarted && geminiLiveClient != null && geminiLiveClient.IsReady)
                geminiLiveClient.EndUserActivity();
            liveActivityStarted = false;
            holdingTalkKey = false;
            HandleInputError(string.IsNullOrWhiteSpace(message) ? "ARES microphone streaming failed." : message);
        }

        private void OnLiveInputTranscript(string transcript, bool interim)
        {
            if (!string.IsNullOrWhiteSpace(transcript))
                latestLiveTranscript = transcript.Trim();
        }

        private void OnLiveConnectionStateChanged(ARESLiveConnectionState state)
        {
            if (state != ARESLiveConnectionState.Error)
                return;

            holdingTalkKey = false;
            liveActivityStarted = false;
            if (streamingVoiceInput != null && streamingVoiceInput.IsStreaming)
                streamingVoiceInput.EndPcmStream();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                EndLiveSpeech();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                EndLiveSpeech();
        }

        private void AddLocalArchiveEvidence()
        {
            if (ragHandler == null || !ragHandler.LoadCompleted || !ragHandler.IsLoaded ||
                string.IsNullOrWhiteSpace(latestLiveTranscript))
                return;

            ARESRAGHandler.RetrievalResult retrieval = ragHandler.Retrieve(latestLiveTranscript, GetResponseLanguage());
            if (retrieval.HasSupportingRecords)
                geminiLiveClient.SendArchiveEvidence(retrieval.SystemInstruction);
        }

        private void HandleInputError(string message)
        {
            PublishError(string.IsNullOrWhiteSpace(message) ? "ARES voice input failed." : message);
        }

        private void PublishResponse(string text, bool isError)
        {
            string playerName = ARESSystemConfiguration.PlayerName;
            string response = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            if (!response.StartsWith(playerName, System.StringComparison.OrdinalIgnoreCase))
            {
                string language = GetResponseLanguage();
                string separator = language.StartsWith("fa", System.StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("Persian", System.StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("Farsi", System.StringComparison.OrdinalIgnoreCase)
                    ? "، "
                    : ", ";
                response = playerName + separator + response;
            }
            ARESEventBus.PublishResponse(new ARESResponse(response, isError));
        }

        private void PublishError(string message)
        {
            Debug.LogWarning(message, this);
            PublishResponse(message, true);
        }

        private string GetResponseLanguage()
        {
            if (gameSettings != null && !string.IsNullOrWhiteSpace(gameSettings.ResponseLanguage))
                return gameSettings.ResponseLanguage;
            return configuration != null && !string.IsNullOrWhiteSpace(configuration.DefaultResponseLanguage)
                ? configuration.DefaultResponseLanguage
                : "English";
        }

        private bool WasInteractionKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                Keyboard.current.vKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.V);
#else
            return false;
#endif
        }

        private bool WasInteractionKeyReleased()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                Keyboard.current.vKey.wasReleasedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyUp(KeyCode.V);
#else
            return false;
#endif
        }
    }
}
