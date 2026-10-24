using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace NintrollerLib.Tests
{
    /// <summary>
    /// A real accelerometer recording saved by WiitarThing's tilt window ("Gravar 10 s de movimento"):
    /// tempo_ms;rawX;rawY;rawZ;... with raw 0-255 readings whose zero point is about 127.
    /// </summary>
    internal static class Recording
    {
        private const int Zero = 127;

        public static AccelSimulator.Sample[] Load(string name)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", name);
            var samples = new List<AccelSimulator.Sample>();

            foreach (string line in File.ReadLines(path))
            {
                string[] parts = line.Split(';');
                if (parts.Length < 4 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double time))
                    continue;

                samples.Add(new AccelSimulator.Sample
                {
                    TimeMs = time,
                    X = int.Parse(parts[1], CultureInfo.InvariantCulture) - Zero,
                    Y = int.Parse(parts[2], CultureInfo.InvariantCulture) - Zero,
                    Z = int.Parse(parts[3], CultureInfo.InvariantCulture) - Zero
                });
            }

            return samples.ToArray();
        }

        public static int CountPresses(TiltGestureDetector detector, IEnumerable<AccelSimulator.Sample> samples)
        {
            int presses = 0;
            bool was = false;
            foreach (var s in samples)
            {
                bool now = detector.Update(s.TimeMs, s.X, s.Y, s.Z);
                if (now && !was) presses++;
                was = now;
            }

            return presses;
        }
    }
}
