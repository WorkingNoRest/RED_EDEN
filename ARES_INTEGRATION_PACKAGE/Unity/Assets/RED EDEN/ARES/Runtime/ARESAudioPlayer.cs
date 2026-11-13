using System;
using System.Threading;
using UnityEngine;

namespace RedEden.ARES
{
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class ARESAudioPlayer : MonoBehaviour, IARESLiveAudioOutput
    {
        [SerializeField, Range(12000, 48000)] private int sampleRate = 24000;
        [SerializeField, Min(2)] private int bufferSeconds = 4;

        private AudioSource audioSource;
        private AudioClip streamingClip;
        private float[] sampleRing;
        private int ringMask;
        private long readPosition;
        private long writePosition;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            int capacity = Mathf.NextPowerOfTwo(Mathf.Max(1024, sampleRate * bufferSeconds));
            sampleRing = new float[capacity];
            ringMask = capacity - 1;
            streamingClip = AudioClip.Create(
                "ARES Live PCM",
                capacity,
                1,
                sampleRate,
                true,
                ReadAudio,
                SetAudioPosition);
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.clip = streamingClip;
            audioSource.Play();
        }

        private void OnDestroy()
        {
            if (audioSource != null)
                audioSource.Stop();
            if (streamingClip != null)
                Destroy(streamingClip);
        }

        public bool TryPlayPcm(byte[] pcm)
        {
            if (pcm == null || pcm.Length == 0 || (pcm.Length & 1) != 0 || sampleRing == null)
                return false;

            int count = pcm.Length / 2;
            long write = Interlocked.Read(ref writePosition);
            long read = Interlocked.Read(ref readPosition);
            if (count > sampleRing.Length - (write - read))
                return false;

            for (int i = 0; i < count; i++)
            {
                short value = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
                sampleRing[(int)((write + i) & ringMask)] = value / 32768f;
            }
            Volatile.Write(ref writePosition, write + count);
            return true;
        }

        public void Flush()
        {
            long write = Interlocked.Read(ref writePosition);
            Interlocked.Exchange(ref readPosition, write);
        }

        private void ReadAudio(float[] data)
        {
            long read = Interlocked.Read(ref readPosition);
            long write = Interlocked.Read(ref writePosition);
            int available = (int)Math.Min(data.Length, Math.Max(0, write - read));
            for (int i = 0; i < available; i++)
                data[i] = sampleRing[(int)((read + i) & ringMask)];
            for (int i = available; i < data.Length; i++)
                data[i] = 0f;
            if (available > 0)
                Interlocked.Exchange(ref readPosition, read + available);
        }

        private void SetAudioPosition(int position)
        {
        }
    }
}
