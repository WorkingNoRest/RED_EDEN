using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RedEden.ARES
{
    public enum ARESVitalMetric
    {
        Oxygen,
        Health,
        Hunger,
        Thirst,
        Radiation,
        Stamina
    }

    public enum ARESAlertDirection
    {
        Below,
        Above
    }

    public interface IARESGameSettingsProvider
    {
        string ResponseLanguage { get; }
    }

    public interface IARESSurvivalVitalsSource
    {
        bool TryGetValue(ARESVitalMetric metric, out float value);
    }

    public interface IARESMapMarkerSink
    {
        void ShowResourceMarker(ARESResourceDefinition resource, Vector3 position);
    }

    public interface IARESVoiceInput
    {
        ARESVoiceState State { get; }
        void BeginCapture(Action<string> onTranscript, Action<string> onError);
        void EndCapture();
        void CancelCapture();
    }

    public enum ARESVoiceState
    {
        Idle,
        Listening,
        Transcribing
    }

    public interface IARESSpeechRecognizer
    {
        bool IsReady { get; }
        IEnumerator Transcribe(float[] samples, int sampleRate, Action<string> onText, Action<string> onError);
    }

    public interface IARESVoiceOutput
    {
        void Speak(string text, string language);
    }

    public sealed class ARESResourceScanResult
    {
        public ARESResourceDefinition Resource { get; }
        public Vector3 Position { get; }
        public string FormattedCoordinates => string.Format(CultureInfo.InvariantCulture,
            "X: {0:F1} m, Y: {1:F1} m, Z: {2:F1} m", Position.x, Position.y, Position.z);

        public ARESResourceScanResult(ARESResourceDefinition resource, Vector3 position)
        {
            Resource = resource;
            Position = position;
        }
    }

    public sealed class ARESInteractionRequest
    {
        public string Language { get; }

        public ARESInteractionRequest(string language)
        {
            Language = language;
        }
    }

    public sealed class ARESTextInputRequest
    {
        public string Language { get; }
        public ARESTextInputRequest(string language)
        {
            Language = language;
        }
    }

    public sealed class ARESResponse
    {
        public string Text { get; }
        public bool IsError { get; }

        public ARESResponse(string text, bool isError)
        {
            Text = text;
            IsError = isError;
        }
    }

    public sealed class ARESVitalAlert
    {
        public ARESVitalMetric Metric { get; }
        public float CurrentValue { get; }
        public float Threshold { get; }
        public string Message { get; }

        public ARESVitalAlert(ARESVitalMetric metric, float currentValue, float threshold, string message)
        {
            Metric = metric;
            CurrentValue = currentValue;
            Threshold = threshold;
            Message = message;
        }
    }

    public static class ARESEventBus
    {
        public static event Action<ARESInteractionRequest> InteractionRequested;
        public static event Action<ARESTextInputRequest> TextInputRequested;
        public static event Action<ARESResponse> ResponseReceived;
        public static event Action<ARESVoiceState> VoiceStateChanged;
        public static event Action<IReadOnlyList<ARESResourceScanResult>> ScanCompleted;
        public static event Action<ARESResourceScanResult> ResourceLocated;
        public static event Action<ARESVitalAlert> VitalAlertRaised;

        public static void RequestInteraction(ARESInteractionRequest request)
        {
            InteractionRequested?.Invoke(request);
        }

        public static void RequestTextInput(ARESTextInputRequest request)
        {
            TextInputRequested?.Invoke(request);
        }

        public static void PublishResponse(ARESResponse response)
        {
            ResponseReceived?.Invoke(response);
        }

        public static void PublishVoiceState(ARESVoiceState state)
        {
            VoiceStateChanged?.Invoke(state);
        }

        public static void PublishScanCompleted(IReadOnlyList<ARESResourceScanResult> results)
        {
            ScanCompleted?.Invoke(results);
        }

        public static void PublishResourceLocated(ARESResourceScanResult result)
        {
            ResourceLocated?.Invoke(result);
        }

        public static void PublishVitalAlert(ARESVitalAlert alert)
        {
            VitalAlertRaised?.Invoke(alert);
        }
    }
}
