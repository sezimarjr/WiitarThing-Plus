using System.Linq;
using Xunit;

namespace NintrollerLib.Tests
{
    /// <summary>
    /// Replays real accelerometer recordings from a Les Paul played in Clone Hero: 7 Star Power flicks in each
    /// 10 s recording, with strumming in between. These flicks are quick "nods" of about 20-30 degrees at
    /// 180-275 degrees/s that stay past 15 degrees for only 50-200 ms and briefly read up to 1.4 g.
    /// </summary>
    public class RealRecordingTests
    {
        [Fact]
        public void RealFlicks_AreCaught()
        {
            int caught = TuningSweep.RealRecordings
                .Select(Recording.Load)
                .Sum(samples => Recording.CountPresses(new TiltGestureDetector(), samples));

            // 21 flicks in total; a couple of them are very small
            Assert.True(caught >= 18, "caught " + caught + " of 21 real flicks");
        }

        [Theory]
        [InlineData("especial_real_1.csv")]
        [InlineData("especial_real_2.csv")]
        [InlineData("especial_real_3.csv")]
        public void RealRecording_NeverPressesMoreThanOncePerFlick(string recording)
        {
            int presses = Recording.CountPresses(new TiltGestureDetector(), Recording.Load(recording));

            Assert.InRange(presses, 0, TuningSweep.FlicksPerRecording);
        }

        [Fact]
        public void RealRecordings_EveryPressIsAtAFlick()
        {
            // The strumming between flicks must not press: every press has to land near one of the real flicks
            foreach (string name in TuningSweep.RealRecordings)
            {
                var samples = Recording.Load(name);
                var peaks = FlickPeaks(samples);

                var detector = new TiltGestureDetector();
                bool was = false;
                foreach (var s in samples)
                {
                    bool now = detector.Update(s.TimeMs, s.X, s.Y, s.Z);
                    if (now && !was)
                    {
                        double nearest = peaks.Min(p => System.Math.Abs(p - s.TimeMs));
                        Assert.True(nearest <= 400, name + ": pressed at " + s.TimeMs + " ms, " + nearest + " ms from the nearest flick");
                    }

                    was = now;
                }
            }
        }

        // Peak of each flick: the largest rotation from the pose 350 ms earlier, flicks at least 700 ms apart
        private static double[] FlickPeaks(AccelSimulator.Sample[] samples)
        {
            var peaks = new System.Collections.Generic.List<int>();
            double[] rotation = new double[samples.Length];
            for (int i = 35; i < samples.Length; i++)
                rotation[i] = Angle(samples[i - 35], samples[i]);

            for (int i = 1; i < samples.Length - 1; i++)
            {
                if (rotation[i] < 14 || rotation[i] < rotation[i - 1] || rotation[i] < rotation[i + 1])
                    continue;

                if (peaks.Count > 0 && samples[i].TimeMs - samples[peaks[peaks.Count - 1]].TimeMs < 700)
                {
                    if (rotation[i] > rotation[peaks[peaks.Count - 1]])
                        peaks[peaks.Count - 1] = i;
                }
                else
                {
                    peaks.Add(i);
                }
            }

            return peaks.Select(i => samples[i].TimeMs).ToArray();
        }

        private static double Angle(AccelSimulator.Sample a, AccelSimulator.Sample b)
        {
            double dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            double la = System.Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
            double lb = System.Math.Sqrt(b.X * b.X + b.Y * b.Y + b.Z * b.Z);
            return System.Math.Acos(System.Math.Max(-1, System.Math.Min(1, dot / (la * lb)))) * 180 / System.Math.PI;
        }
    }
}
