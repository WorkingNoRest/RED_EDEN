using System;
using System.Collections;
using UnityEngine;

namespace RedEden.ARES
{
    /// <summary>
    /// Press V once: starts listening. It stops automatically after you finish speaking
    /// (silence detection), or earlier if V is pressed again. The audio is transcribed offline.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ARESMicrophoneVoiceInput : MonoBehaviour, IARESVoiceInput
    {
        [SerializeField] private MonoBehaviour recognizerProvider;
        [SerializeField] private string microphoneDevice = "";
        [SerializeField, Range(8000, 48000)] private int sampleRate = 16000;
        [SerializeField, Min(3f)] private float maxRecordingSeconds = 20f;
        [SerializeField, Min(0.3f)] private float silenceSecondsToStop = 1.3f;
        [SerializeField, Min(1f)] private float noSpeechTimeoutSeconds = 7f;
        [SerializeField, Min(0.1f)] private float calibrationSeconds = 0.4f;
        [SerializeField, Min(0.001f)] private float minimumSpeechLevel = 0.012f;
        [SerializeField, Min(1.5f)] private float noiseMultiplier = 3f;
        [SerializeField, Min(0.1f)] private float minimumSpeechSeconds = 0.35f;
        [SerializeField, Min(0f)] private float paddingSeconds = 0.3f;

        private IARESSpeechRecognizer recognizer;
        private bool finishRequested;
        private bool cancelRequested;
        private Coroutine routine;
        private string activeDevice;
        private AudioClip activeClip;

        public ARESVoiceState State { get; private set; }
        public float CurrentLevel { get; private set; }

        private void Awake()
        {
            recognizer = recognizerProvider as IARESSpeechRecognizer;
            if (recognizer == null)
                Debug.LogError("ARES voice input needs a component implementing IARESSpeechRecognizer.", this);
        }

        private void OnDisable()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            if (State == ARESVoiceState.Listening && Microphone.IsRecording(activeDevice))
                Microphone.End(activeDevice);
            ReleaseClip();
            if (State != ARESVoiceState.Idle)
                SetState(ARESVoiceState.Idle);
        }

        public void BeginCapture(Action<string> onTranscript, Action<string> onError)
        {
            if (State != ARESVoiceState.Idle)
            {
                onError?.Invoke("ARES is already busy with a voice request.");
                return;
            }
            if (recognizer == null)
            {
                onError?.Invoke("ARES speech recognizer is not assigned.");
                return;
            }
            if (!recognizer.IsReady)
            {
                onError?.Invoke("ARES speech model is still loading. Please try again in a moment.");
                return;
            }
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                onError?.Invoke("No microphone was detected.");
                return;
            }

            routine = StartCoroutine(CaptureRoutine(onTranscript, onError));
        }

        /// <summary>Stop recording now and transcribe what was captured.</summary>
        public void EndCapture()
        {
            if (State == ARESVoiceState.Listening)
                finishRequested = true;
        }

        public void CancelCapture()
        {
            if (State == ARESVoiceState.Listening)
                cancelRequested = true;
        }

        private IEnumerator CaptureRoutine(Action<string> onTranscript, Action<string> onError)
        {
            finishRequested = false;
            cancelRequested = false;
            CurrentLevel = 0f;
            activeDevice = string.IsNullOrWhiteSpace(microphoneDevice) ? null : microphoneDevice;
            SetState(ARESVoiceState.Listening);

            int clipSeconds = Mathf.CeilToInt(maxRecordingSeconds);
            AudioClip clip = Microphone.Start(activeDevice, false, clipSeconds, sampleRate);
            activeClip = clip;
            if (clip == null)
            {
                Fail(onError, "ARES could not open the microphone.");
                yield break;
            }

            float waitStart = Time.realtimeSinceStartup;
            while (Microphone.GetPosition(activeDevice) <= 0)
            {
                if (Time.realtimeSinceStartup - waitStart > 3f)
                {
                    Microphone.End(activeDevice);
                    ReleaseClip();
                    Fail(onError, "The microphone did not start recording.");
                    yield break;
                }
                yield return null;
            }

            int rate = clip.frequency;
            int window = Mathf.Max(1, rate / 20); // 50 ms analysis windows
            float[] buffer = new float[window];
            int read = 0;
            int lastPosition = 0;
            float noiseSum = 0f;
            int noiseCount = 0;
            float threshold = minimumSpeechLevel;
            bool calibrated = false;
            int firstVoiceSample = -1;
            int lastVoiceSample = 0;
            float lastVoiceTime = 0f;
            float speechTime = 0f;

            while (true)
            {
                if (cancelRequested)
                    break;

                int position = Microphone.IsRecording(activeDevice)
                    ? Microphone.GetPosition(activeDevice)
                    : clip.samples;
                lastPosition = Mathf.Max(lastPosition, position);

                while (position - read >= window)
                {
                    clip.GetData(buffer, read);
                    float rms = ComputeRms(buffer);
                    CurrentLevel = rms;
                    read += window;
                    float now = read / (float)rate;

                    if (now <= calibrationSeconds)
                    {
                        noiseSum += rms;
                        noiseCount++;
                        continue;
                    }
                    if (!calibrated)
                    {
                        calibrated = true;
                        float noiseFloor = noiseCount > 0 ? noiseSum / noiseCount : 0f;
                        threshold = Mathf.Max(minimumSpeechLevel, noiseFloor * noiseMultiplier);
                    }

                    if (rms > threshold)
                    {
                        if (firstVoiceSample < 0)
                            firstVoiceSample = read - window;
                        lastVoiceSample = read;
                        lastVoiceTime = now;
                        speechTime += window / (float)rate;
                    }
                }

                float elapsed = read / (float)rate;
                if (finishRequested)
                    break;
                if (firstVoiceSample >= 0 && speechTime >= minimumSpeechSeconds &&
                    elapsed - lastVoiceTime >= silenceSecondsToStop)
                    break;
                if (firstVoiceSample < 0 && elapsed >= noSpeechTimeoutSeconds)
                    break;
                if (read + window > clip.samples)
                    break;

                yield return null;
            }

            int endSample = Mathf.Min(clip.samples, Mathf.Max(read, lastPosition));
            if (Microphone.IsRecording(activeDevice))
                Microphone.End(activeDevice);

            if (cancelRequested)
            {
                ReleaseClip();
                SetState(ARESVoiceState.Idle);
                yield break;
            }
            if (firstVoiceSample < 0 || speechTime < minimumSpeechSeconds)
            {
                ReleaseClip();
                Fail(onError, "ARES did not hear anything. Press V and speak again.");
                yield break;
            }

            int padding = Mathf.RoundToInt(paddingSeconds * rate);
            int start = Mathf.Max(0, firstVoiceSample - padding);
            int end = Mathf.Min(endSample, lastVoiceSample + padding);
            int length = end - start;
            if (length <= 0 || start + length > clip.samples)
            {
                ReleaseClip();
                Fail(onError, "ARES captured an invalid audio range.");
                yield break;
            }

            float[] samples = new float[length];
            clip.GetData(samples, start);
            ReleaseClip();
            NormalizeQuietAudio(samples);

            SetState(ARESVoiceState.Transcribing);
            string text = null;
            string error = null;
            yield return recognizer.Transcribe(samples, rate, t => text = t, e => error = e);
            SetState(ARESVoiceState.Idle);
            routine = null;

            if (!string.IsNullOrWhiteSpace(error))
            {
                onError?.Invoke(error);
                yield break;
            }

            text = text == null ? string.Empty : text.Trim();
            if (IsNonSpeechTranscript(text))
            {
                onError?.Invoke("ARES could not understand that. Press V and try again.");
                yield break;
            }

            onTranscript?.Invoke(text);
        }

        private void Fail(Action<string> onError, string message)
        {
            SetState(ARESVoiceState.Idle);
            routine = null;
            onError?.Invoke(message);
        }

        private void ReleaseClip()
        {
            if (activeClip == null)
                return;
            Destroy(activeClip);
            activeClip = null;
        }

        private void SetState(ARESVoiceState state)
        {
            State = state;
            ARESEventBus.PublishVoiceState(state);
        }

        private static float ComputeRms(float[] data)
        {
            double sum = 0d;
            for (int i = 0; i < data.Length; i++)
                sum += data[i] * data[i];
            return Mathf.Sqrt((float)(sum / data.Length));
        }

        private static void NormalizeQuietAudio(float[] samples)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            if (peak < 0.001f || peak >= 0.5f)
                return;

            float gain = 0.8f / peak;
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= gain;
        }

        // Whisper emits markers like [BLANK_AUDIO] or (silence) for non-speech audio.
        private static bool IsNonSpeechTranscript(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;
            string t = text.Trim();
            return (t.StartsWith("[") && t.EndsWith("]")) ||
                   (t.StartsWith("(") && t.EndsWith(")")) ||
                   t.IndexOf("BLANK_AUDIO", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
