using System;
using System.Collections;
using UnityEngine;

namespace RedEden.ARES
{
    // Kept only to resolve old scene references; offline recognition is disabled.
    [DisallowMultipleComponent]
    public sealed class ARESWhisperRecognizer : MonoBehaviour, IARESSpeechRecognizer
    {
        public bool IsReady => false;

        public IEnumerator Transcribe(float[] samples, int sampleRate, Action<string> onText, Action<string> onError)
        {
            onError?.Invoke("Offline speech recognition is not included. Use ARES Gemini Live.");
            yield break;
        }
    }
}
