using System;
using System.Collections.Generic;

namespace NintrollerLib.Tests
{
    /// <summary>
    /// Generates Wiimote accelerometer reports for a guitar being played: 100 reports a second, integer counts
    /// with the zero point removed (about 26 counts per g, like the real sensor), and a little sensor noise.
    /// The guitar's pose is an angle in the plane of its face (the Wiimote's X/Y plane); raising the neck
    /// increases it. The face can also be tilted toward the Wiimote's Z axis.
    /// </summary>
    internal sealed class AccelSimulator
    {
        public const double SampleMs = 10;
        public const double CountsPerG = 26;

        public struct Sample
        {
            public double TimeMs;
            public float X, Y, Z;
        }

        private readonly Random random;
        private readonly List<Sample> samples = new List<Sample>();
        private double time;

        public AccelSimulator(double startPoseDegrees, int seed = 1, double faceTiltDegrees = 0, double noiseCounts = 1.2)
        {
            random = new Random(seed);
            PoseDegrees = startPoseDegrees;
            FaceTiltDegrees = faceTiltDegrees;
            NoiseCounts = noiseCounts;
        }

        public double PoseDegrees { get; private set; }
        public double FaceTiltDegrees { get; set; }
        public double NoiseCounts { get; set; }
        public double Now => time;
        public IReadOnlyList<Sample> Samples => samples;

        /// <summary>Keeps the current pose.</summary>
        public AccelSimulator Hold(double ms)
        {
            return Move(PoseDegrees, ms);
        }

        /// <summary>Rotates to <paramref name="toPoseDegrees"/> over <paramref name="ms"/>, easing in and out like an arm does.</summary>
        public AccelSimulator Move(double toPoseDegrees, double ms)
        {
            double from = PoseDegrees;
            int steps = Math.Max(1, (int)Math.Round(ms / SampleMs));
            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                double eased = 0.5 - 0.5 * Math.Cos(Math.PI * t);
                Emit(from + (toPoseDegrees - from) * eased, 0, 0, 0);
            }

            PoseDegrees = toPoseDegrees;
            return this;
        }

        /// <summary>Rocks the guitar back and forth around the current pose (body moving to the music).</summary>
        public AccelSimulator Sway(double amplitudeDegrees, double hz, double ms)
        {
            double center = PoseDegrees;
            int steps = (int)Math.Round(ms / SampleMs);
            for (int i = 1; i <= steps; i++)
            {
                double seconds = i * SampleMs / 1000.0;
                Emit(center + amplitudeDegrees * Math.Sin(2 * Math.PI * hz * seconds), 0, 0, 0);
            }

            return this;
        }

        /// <summary>A short jolt (e.g. a hard strum) of <paramref name="g"/> in a random direction on top of gravity.</summary>
        public AccelSimulator Jolt(double g, double ms)
        {
            double ax = random.NextDouble() * 2 - 1;
            double ay = random.NextDouble() * 2 - 1;
            double az = random.NextDouble() * 2 - 1;
            double length = Math.Sqrt(ax * ax + ay * ay + az * az);
            ax = ax / length * g;
            ay = ay / length * g;
            az = az / length * g;

            int steps = Math.Max(1, (int)Math.Round(ms / SampleMs));
            for (int i = 0; i < steps; i++)
            {
                Emit(PoseDegrees, ax, ay, az);
            }

            return this;
        }

        /// <summary>Plays with hard strums: a jolt of <paramref name="g"/> every <paramref name="everyMs"/>.</summary>
        public AccelSimulator Strumming(double g, double joltMs, double everyMs, double totalMs)
        {
            double end = time + totalMs;
            while (time < end)
            {
                Jolt(g, joltMs);
                Hold(everyMs - joltMs);
            }

            return this;
        }

        /// <summary>No reports for a while (Bluetooth dropout or reconnect).</summary>
        public AccelSimulator Gap(double ms)
        {
            time += ms;
            return this;
        }

        /// <summary>Jumps to a pose without any reports in between (e.g. after a gap).</summary>
        public AccelSimulator SetPose(double poseDegrees)
        {
            PoseDegrees = poseDegrees;
            return this;
        }

        private void Emit(double poseDegrees, double ax, double ay, double az)
        {
            time += SampleMs;

            double pose = poseDegrees * Math.PI / 180;
            double face = FaceTiltDegrees * Math.PI / 180;

            double gx = Math.Sin(pose) * Math.Cos(face) + ax;
            double gy = Math.Cos(pose) * Math.Cos(face) + ay;
            double gz = Math.Sin(face) + az;

            samples.Add(new Sample
            {
                TimeMs = time,
                X = (float)Math.Round(gx * CountsPerG + Noise()),
                Y = (float)Math.Round(gy * CountsPerG + Noise()),
                Z = (float)Math.Round(gz * CountsPerG + Noise())
            });
        }

        // Roughly normal noise (sum of uniforms), scaled to NoiseCounts standard deviation
        private double Noise()
        {
            double sum = 0;
            for (int i = 0; i < 12; i++)
                sum += random.NextDouble();
            return (sum - 6) * NoiseCounts;
        }
    }

    /// <summary>What a detector did over a simulated stream.</summary>
    internal sealed class DetectorRun
    {
        public readonly List<double> PressTimes = new List<double>();
        public readonly List<double> ReleaseTimes = new List<double>();
        public double? FirstReadyTime;
        public float MaxDeviation;

        public int Presses => PressTimes.Count;

        public static DetectorRun Of(TiltGestureDetector detector, AccelSimulator sim)
        {
            var run = new DetectorRun();
            bool was = false;
            foreach (var s in sim.Samples)
            {
                bool now = detector.Update(s.TimeMs, s.X, s.Y, s.Z);
                if (now && !was) run.PressTimes.Add(s.TimeMs);
                if (!now && was) run.ReleaseTimes.Add(s.TimeMs);
                if (detector.IsReady && run.FirstReadyTime == null) run.FirstReadyTime = s.TimeMs;
                run.MaxDeviation = Math.Max(run.MaxDeviation, detector.DeviationDegrees);
                was = now;
            }

            return run;
        }
    }
}
