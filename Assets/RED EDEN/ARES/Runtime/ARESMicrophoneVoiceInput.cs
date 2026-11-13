using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RedEden.ARES
{
    /// <summary>
    /// Supports the existing offline capture flow and low-latency PCM streaming for Live sessions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ARESMicrophoneVoiceInput : MonoBehaviour, IARESVoiceInput, IARESStreamingVoiceInput
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
        private Coroutine streamStartRoutine;
        private string activeDevice;
        private AudioClip activeClip;
        private Action<byte[]> onPcm;
        private Action<string> streamError;
        private readonly ARESPcm16Resampler resampler = new ARESPcm16Resampler();
        private readonly List<short> convertedSamples = new List<short>();
        private readonly Queue<short> pendingPcm = new Queue<short>();
        private float[] streamReadBuffer = Array.Empty<float>();
        private float[] streamFirstReadBuffer = Array.Empty<float>();
        private float[] streamWrapBuffer = Array.Empty<float>();
        private int streamReadPosition;
        private bool streaming;

        public ARESVoiceState State { get; private set; }
        public float CurrentLevel { get; private set; }
        public bool IsStreaming => streaming;

        private void Awake()
        {
            recognizer = recognizerProvider as IARESSpeechRecognizer;
        }

        private void OnDisable()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            if (streamStartRoutine != null)
                StopCoroutine(streamStartRoutine);
            streamStartRoutine = null;
            if (State == ARESVoiceState.Listening && Microphone.IsRecording(activeDevice))
                Microphone.End(activeDevice);
            streaming = false;
            onPcm = null;
            streamError = null;
            ReleaseClip();
            if (State != ARESVoiceState.Idle)
                SetState(ARESVoiceState.Idle);
        }

        private void Update()
        {
            if (streaming)
                ReadPcmStream();
        }

        public void BeginPcmStream(Action<byte[]> onPcmChunk, Action<string> onError)
        {
            if (State != ARESVoiceState.Idle || streamStartRoutine != null)
            {
                onError?.Invoke("ARES microphone is already busy.");
                return;
            }
            if (onPcmChunk == null)
            {
                onError?.Invoke("ARES PCM stream callback is missing.");
                return;
            }
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                onError?.Invoke("No microphone was detected.");
                return;
            }

            activeDevice = string.IsNullOrWhiteSpace(microphoneDevice) ? null : microphoneDevice;
            onPcm = onPcmChunk;
            streamError = onError;
            CurrentLevel = 0f;
            resampler.Reset();
            convertedSamples.Clear();
            pendingPcm.Clear();
            SetState(ARESVoiceState.Listening);
            streamStartRoutine = StartCoroutine(StartPcmStream());
        }

        public void EndPcmStream()
        {
            if (streamStartRoutine != null)
            {
                StopCoroutine(streamStartRoutine);
                streamStartRoutine = null;
            }

            if (Microphone.IsRecording(activeDevice))
                Microphone.End(activeDevice);
            streaming = false;
            resampler.Flush(convertedSamples);
            EnqueueConvertedSamples();
            EmitPcmChunks(true);
            ReleaseClip();
            onPcm = null;
            streamError = null;
            resampler.Reset();
            convertedSamples.Clear();
            pendingPcm.Clear();
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
                onError?.Invoke("Offline transcription is not part of this package. Use Gemini Live PCM streaming.");
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
            if (streaming || streamStartRoutine != null)
            {
                CancelPcmStream();
                return;
            }
            if (State == ARESVoiceState.Listening)
                cancelRequested = true;
        }

        private IEnumerator StartPcmStream()
        {
            activeClip = Microphone.Start(activeDevice, true, 1, sampleRate);
            if (activeClip == null)
            {
                FailPcmStream("ARES could not open the microphone.");
                yield break;
            }

            float waitStart = Time.realtimeSinceStartup;
            while (Microphone.GetPosition(activeDevice) <= 0)
            {
                if (Time.realtimeSinceStartup - waitStart > 3f)
                {
                    FailPcmStream("The microphone did not start recording.");
                    yield break;
                }
                yield return null;
            }

            streamReadPosition = Microphone.GetPosition(activeDevice);
            streaming = true;
            streamStartRoutine = null;
        }

        private void ReadPcmStream()
        {
            if (activeClip == null || !Microphone.IsRecording(activeDevice))
            {
                FailPcmStream("ARES microphone stream stopped unexpectedly.");
                return;
            }

            int position = Microphone.GetPosition(activeDevice);
            if (position < 0 || position >= activeClip.samples)
                return;
            int available = (position - streamReadPosition + activeClip.samples) % activeClip.samples;
            if (available > activeClip.samples / 2)
            {
                FailPcmStream("ARES microphone stream overran its capture buffer.");
                return;
            }

            int framesPerRead = Mathf.Max(1, activeClip.frequency / 50);
            int channels = Mathf.Max(1, activeClip.channels);
            while (available > 0)
            {
                int frames = Mathf.Min(available, framesPerRead);
                int firstFrames = Mathf.Min(frames, activeClip.samples - streamReadPosition);
                int firstLength = firstFrames * channels;
                int totalLength = frames * channels;
                EnsureBuffer(ref streamReadBuffer, totalLength);
                EnsureBuffer(ref streamFirstReadBuffer, firstLength);
                if (!activeClip.GetData(streamFirstReadBuffer, streamReadPosition))
                {
                    FailPcmStream("ARES could not read microphone samples.");
                    return;
                }
                Array.Copy(streamFirstReadBuffer, streamReadBuffer, firstLength);

                if (firstFrames < frames)
                {
                    int remainingFrames = frames - firstFrames;
                    int remainingLength = remainingFrames * channels;
                    EnsureBuffer(ref streamWrapBuffer, remainingLength);
                    if (!activeClip.GetData(streamWrapBuffer, 0))
                    {
                        FailPcmStream("ARES could not read wrapped microphone samples.");
                        return;
                    }
                    Array.Copy(streamWrapBuffer, 0, streamReadBuffer, firstLength, remainingLength);
                }

                convertedSamples.Clear();
                resampler.Convert(streamReadBuffer, channels, activeClip.frequency, convertedSamples);
                EnqueueConvertedSamples();
                EmitPcmChunks(false);
                CurrentLevel = ComputeRms(streamReadBuffer);
                streamReadPosition = (streamReadPosition + frames) % activeClip.samples;
                available -= frames;
            }
        }

        private void EnqueueConvertedSamples()
        {
            for (int i = 0; i < convertedSamples.Count; i++)
                pendingPcm.Enqueue(convertedSamples[i]);
            convertedSamples.Clear();
        }

        private void EmitPcmChunks(bool includePartial)
        {
            const int samplesPerChunk = 320;
            while (pendingPcm.Count >= samplesPerChunk || (includePartial && pendingPcm.Count > 0))
            {
                int sampleCount = Mathf.Min(samplesPerChunk, pendingPcm.Count);
                byte[] pcm = new byte[sampleCount * 2];
                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = pendingPcm.Dequeue();
                    pcm[i * 2] = (byte)(sample & 0xff);
                    pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xff);
                }
                onPcm?.Invoke(pcm);
            }
        }

        private void FailPcmStream(string message)
        {
            if (Microphone.IsRecording(activeDevice))
                Microphone.End(activeDevice);
            streaming = false;
            streamStartRoutine = null;
            ReleaseClip();
            resampler.Reset();
            convertedSamples.Clear();
            pendingPcm.Clear();
            Action<string> error = streamError;
            onPcm = null;
            streamError = null;
            if (State != ARESVoiceState.Idle)
                SetState(ARESVoiceState.Idle);
            error?.Invoke(message);
        }

        private void CancelPcmStream()
        {
            if (streamStartRoutine != null)
                StopCoroutine(streamStartRoutine);
            streamStartRoutine = null;
            if (Microphone.IsRecording(activeDevice))
                Microphone.End(activeDevice);
            streaming = false;
            ReleaseClip();
            onPcm = null;
            streamError = null;
            resampler.Reset();
            convertedSamples.Clear();
            pendingPcm.Clear();
            if (State != ARESVoiceState.Idle)
                SetState(ARESVoiceState.Idle);
        }

        private static void EnsureBuffer(ref float[] buffer, int requiredLength)
        {
            if (buffer.Length != requiredLength)
                buffer = new float[requiredLength];
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
