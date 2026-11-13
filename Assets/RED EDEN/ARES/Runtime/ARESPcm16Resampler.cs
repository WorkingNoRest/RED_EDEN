using System;
using System.Collections.Generic;

namespace RedEden.ARES
{
    public sealed class ARESPcm16Resampler
    {
        public const int OutputSampleRate = 16000;

        private readonly List<float> bufferedSamples = new List<float>();
        private int sourceSampleRate;
        private int firstBufferedSample;
        private int totalInputSamples;
        private double nextOutputSample;
        private float lastInputSample;

        public void Convert(float[] interleavedSamples, int channels, int sampleRate, List<short> output)
        {
            if (interleavedSamples == null)
                throw new ArgumentNullException(nameof(interleavedSamples));
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (channels < 1 || interleavedSamples.Length % channels != 0)
                throw new ArgumentException("Interleaved audio must contain complete frames.", nameof(interleavedSamples));
            if (sampleRate < 1)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));

            if (sourceSampleRate != sampleRate)
                Reset(sampleRate);

            int frames = interleavedSamples.Length / channels;
            for (int frame = 0; frame < frames; frame++)
            {
                float mono = 0f;
                int offset = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                    mono += interleavedSamples[offset + channel];
                mono /= channels;
                if (float.IsNaN(mono) || float.IsInfinity(mono))
                    mono = 0f;
                mono = Math.Max(-1f, Math.Min(1f, mono));
                bufferedSamples.Add(mono);
                lastInputSample = mono;
                totalInputSamples++;
            }

            ConvertAvailable(output);
        }

        public void Flush(List<short> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (totalInputSamples == 0)
                return;

            bufferedSamples.Add(lastInputSample);
            totalInputSamples++;
            ConvertAvailable(output);
            bufferedSamples.Clear();
            firstBufferedSample = totalInputSamples;
        }

        public void Reset()
        {
            Reset(0);
        }

        private void Reset(int sampleRate)
        {
            sourceSampleRate = sampleRate;
            bufferedSamples.Clear();
            firstBufferedSample = 0;
            totalInputSamples = 0;
            nextOutputSample = 0d;
            lastInputSample = 0f;
        }

        private void ConvertAvailable(List<short> output)
        {
            double step = sourceSampleRate / (double)OutputSampleRate;
            while (nextOutputSample + 1d < totalInputSamples)
            {
                int leftSample = (int)nextOutputSample;
                int localIndex = leftSample - firstBufferedSample;
                float left = bufferedSamples[localIndex];
                float right = bufferedSamples[localIndex + 1];
                float fraction = (float)(nextOutputSample - leftSample);
                output.Add(ToPcm16(left + (right - left) * fraction));
                nextOutputSample += step;
            }

            int samplesToDiscard = Math.Max(0, (int)nextOutputSample - firstBufferedSample);
            if (samplesToDiscard > 0)
            {
                bufferedSamples.RemoveRange(0, samplesToDiscard);
                firstBufferedSample += samplesToDiscard;
            }
        }

        private static short ToPcm16(float sample)
        {
            if (sample <= -1f)
                return short.MinValue;
            if (sample >= 1f)
                return short.MaxValue;
            return (short)Math.Round(sample * short.MaxValue);
        }
    }
}
