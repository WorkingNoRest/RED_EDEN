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
        [SerializeField] private ARESLocalBridge localBridge;
        [SerializeField] private ARESGeminiLiveClient geminiLiveClient;
        [SerializeField] private MonoBehaviour gameSettingsProvider;
        [SerializeField] private MonoBehaviour vitalsSourceProvider;
        [SerializeField] private MonoBehaviour voiceInputProvider;
        [SerializeField] private MonoBehaviour voiceOutputProvider;
        [SerializeField] private bool showTestInputUI;

        private IARESGameSettingsProvider gameSettings;
        private IARESSurvivalVitalsSource vitalsSource;
        private IARESVoiceInput voiceInput;
        private IARESVoiceOutput voiceOutput;
        private bool requestInProgress;
        private bool testPanelOpen;
        private ARESVoiceState voiceState;
        private string testInputText = string.Empty;
        private string lastDisplayedResponse = string.Empty;

        private void Awake()
        {
            gameSettings = gameSettingsProvider as IARESGameSettingsProvider;
            vitalsSource = vitalsSourceProvider as IARESSurvivalVitalsSource;
            voiceInput = voiceInputProvider as IARESVoiceInput;
            voiceOutput = voiceOutputProvider as IARESVoiceOutput;

            if (gameSettingsProvider != null && gameSettings == null)
                Debug.LogError("ARES game settings component does not implement IARESGameSettingsProvider.", gameSettingsProvider);
            if (vitalsSourceProvider != null && vitalsSource == null)
                Debug.LogError("ARES vitals component does not implement IARESSurvivalVitalsSource.", vitalsSourceProvider);
            if (voiceInputProvider != null && voiceInput == null)
                Debug.LogError("ARES voice input component does not implement IARESVoiceInput.", voiceInputProvider);
            if (voiceOutputProvider != null && voiceOutput == null)
                Debug.LogError("ARES voice output component does not implement IARESVoiceOutput.", voiceOutputProvider);
        }

        private void OnEnable()
        {
            ARESEventBus.VitalAlertRaised += OnVitalAlertRaised;
            ARESEventBus.ResponseReceived += OnResponseReceived;
            ARESEventBus.TextInputRequested += OnTextInputRequested;
            ARESEventBus.VoiceStateChanged += OnVoiceStateChanged;
        }

        private void OnDisable()
        {
            ARESEventBus.VitalAlertRaised -= OnVitalAlertRaised;
            ARESEventBus.ResponseReceived -= OnResponseReceived;
            ARESEventBus.TextInputRequested -= OnTextInputRequested;
            ARESEventBus.VoiceStateChanged -= OnVoiceStateChanged;
            voiceInput?.CancelCapture();
            requestInProgress = false;
        }

        private void Update()
        {
            if (WasInteractionKeyPressed())
                BeginInteraction();
        }

        public void BeginInteraction()
        {
            if (requestInProgress)
                return;
            if (voiceInput != null)
            {
                if (voiceInput.State == ARESVoiceState.Listening)
                {
                    voiceInput.EndCapture();
                    return;
                }
                if (voiceInput.State != ARESVoiceState.Idle)
                    return;
            }

            string language = GetResponseLanguage();
            if (showTestInputUI)
                testPanelOpen = true;
            ARESEventBus.RequestInteraction(new ARESInteractionRequest(language));
            if (voiceInput != null)
                voiceInput.BeginCapture(SubmitText, HandleInputError);
            else
                ARESEventBus.RequestTextInput(new ARESTextInputRequest(language));
        }

        public void SubmitText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                PublishError("ARES received an empty query.");
                return;
            }
            if (requestInProgress)
            {
                PublishError("ARES is already processing a request.");
                return;
            }

            StartCoroutine(ProcessQuery(text.Trim(), GetResponseLanguage()));
        }

        private IEnumerator ProcessQuery(string query, string language)
        {
            requestInProgress = true;
            try
            {
                if (configuration == null)
                {
                    PublishError("ARES system configuration is not assigned.");
                    yield break;
                }

                if (ragHandler == null)
                {
                    PublishError("ARES knowledge handler is not assigned; local archive retrieval is unavailable.");
                    yield break;
                }

                while (!ragHandler.LoadCompleted)
                    yield return null;

                if (!ragHandler.IsLoaded)
                {
                    PublishError("ARES NASA knowledge archive is empty or could not be loaded.");
                    yield break;
                }

                ARESRAGHandler.RetrievalResult retrieval = ragHandler.Retrieve(query, language);
                if (!retrieval.HasSupportingRecords)
                {
                    PublishResponse(configuration.GetNoArchiveRecordMessage(language), false);
                    yield break;
                }

                string errorMessage = null;
                string responseText = null;

                if (geminiLiveClient != null)
                {
                    string gameContext = ARESGameBridge.BuildRuntimeSnapshot() +
                        "\nNASA archive evidence:\n" + retrieval.SystemInstruction;
                    yield return geminiLiveClient.Ask(query, language, gameContext,
                        result => responseText = result,
                        error => errorMessage = error);
                }
                else
                {
                    if (localBridge == null)
                    {
                        PublishError("ARES local LLM bridge is not assigned.");
                        yield break;
                    }

                    yield return localBridge.GenerateResponse(retrieval.SystemInstruction, query,
                        result => responseText = result,
                        error => errorMessage = error);
                }

                if (!string.IsNullOrWhiteSpace(errorMessage))
                    PublishError(errorMessage);
                else if (!string.IsNullOrWhiteSpace(responseText))
                    PublishResponse(responseText, false);
                else
                    PublishError("ARES completed the request without a response.");
            }
            finally
            {
                requestInProgress = false;
            }
        }

        private void OnVitalAlertRaised(ARESVitalAlert alert)
        {
            if (alert != null && !string.IsNullOrWhiteSpace(alert.Message))
                PublishResponse(alert.Message, false);
        }

        private void OnResponseReceived(ARESResponse response)
        {
            if (response == null)
                return;

            lastDisplayedResponse = response.Text;
            if (voiceOutput != null)
                voiceOutput.Speak(response.Text, GetResponseLanguage());
        }

        private void OnTextInputRequested(ARESTextInputRequest request)
        {
            if (showTestInputUI)
            {
                testPanelOpen = true;
                testInputText = string.Empty;
            }
        }

        private void OnVoiceStateChanged(ARESVoiceState state)
        {
            voiceState = state;
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

        private void OnGUI()
        {
            if (!showTestInputUI || !testPanelOpen)
                return;

            float width = Mathf.Min(460f, Screen.width - 32f);
            GUILayout.BeginArea(new Rect(16f, 16f, width, 390f), GUI.skin.box);
            GUILayout.Label("ARES | آدم مهران");
            DrawVitals();
            GUILayout.Label("Voice input: " + voiceState);
            GUILayout.Label("Enter a question, then send it to the local NASA archive assistant:");
            GUI.SetNextControlName("ARESQuestion");
            testInputText = GUILayout.TextField(testInputText, 512);

            GUILayout.BeginHorizontal();
            GUI.enabled = !requestInProgress && !string.IsNullOrWhiteSpace(testInputText);
            if (GUILayout.Button(requestInProgress ? "ARES is responding..." : "Send to ARES", GUILayout.Height(30f)))
                SubmitText(testInputText);
            GUI.enabled = true;
            if (GUILayout.Button(voiceState == ARESVoiceState.Listening ? "Stop listening (V)" : "Talk to ARES (V)",
                GUILayout.Height(30f)))
                BeginInteraction();
            if (GUILayout.Button("Close", GUILayout.Height(30f)))
            {
                voiceInput?.CancelCapture();
                testPanelOpen = false;
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(lastDisplayedResponse))
            {
                GUILayout.Space(6f);
                GUILayout.Label(lastDisplayedResponse, GUI.skin.box);
            }
            GUILayout.EndArea();
        }

        private void DrawVitals()
        {
            if (vitalsSource == null)
                return;

            GUILayout.Label("PLAYER BIOMETRICS");
            DrawVital(ARESVitalMetric.Health, "Health");
            DrawVital(ARESVitalMetric.Oxygen, "Oxygen");
            DrawVital(ARESVitalMetric.Hunger, "Hunger");
            DrawVital(ARESVitalMetric.Thirst, "Thirst");
            DrawVital(ARESVitalMetric.Stamina, "Stamina");
            DrawVital(ARESVitalMetric.Radiation, "Radiation");
        }

        private void DrawVital(ARESVitalMetric metric, string label)
        {
            if (vitalsSource.TryGetValue(metric, out float value))
                GUILayout.Label(label + ": " + Mathf.RoundToInt(value) + "%");
        }
    }
}
