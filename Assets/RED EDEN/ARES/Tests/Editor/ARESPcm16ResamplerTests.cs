using System.Collections.Generic;
using NUnit.Framework;

namespace RedEden.ARES.Tests
{
    public sealed class ARESPcm16ResamplerTests
    {
        [Test]
        public void ConvertsStereo48KhzChunksToMono16KhzPcm()
        {
            ARESPcm16Resampler resampler = new ARESPcm16Resampler();
            float[] stereo = new float[960 * 2];
            for (int frame = 0; frame < 960; frame++)
            {
                stereo[frame * 2] = 0.25f;
                stereo[frame * 2 + 1] = 0.75f;
            }
            List<short> output = new List<short>();

            resampler.Convert(stereo, 2, 48000, output);

            Assert.That(output, Has.Count.EqualTo(320));
            Assert.That(output, Has.All.EqualTo(16384));
        }

        [Test]
        public void FlushEmitsTheFinalPartialFrameWithoutChangingPcmPolarity()
        {
            ARESPcm16Resampler resampler = new ARESPcm16Resampler();
            List<short> output = new List<short>();

            resampler.Convert(new[] { 0f, 1f, -1f }, 1, 16000, output);
            Assert.That(output, Has.Count.EqualTo(2));
            resampler.Flush(output);

            Assert.That(output, Has.Count.EqualTo(3));
            Assert.That(output[0], Is.EqualTo(0));
            Assert.That(output[1], Is.EqualTo(short.MaxValue));
            Assert.That(output[2], Is.EqualTo(short.MinValue));
        }

        [Test]
        public void ChangingCaptureRateStartsANewResamplingSequence()
        {
            ARESPcm16Resampler resampler = new ARESPcm16Resampler();
            List<short> output = new List<short>();

            resampler.Convert(new float[960], 1, 48000, output);
            resampler.Convert(new float[320], 1, 16000, output);
            resampler.Flush(output);

            Assert.That(output, Has.Count.EqualTo(640));
        }
    }
}
