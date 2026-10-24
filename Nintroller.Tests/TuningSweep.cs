using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NintrollerLib.Tests
{
    /// <summary>
    /// Not a pass/fail test: tries combinations of detector settings against the real recordings (which should
    /// trigger on each of the player's flicks) and the simulated "should not trigger" cases, and prints a table.
    /// Run with: dotnet test --filter Category=Sweep --logger "console;verbosity=detailed"
    /// </summary>
    public class TuningSweep
    {
        private readonly ITestOutputHelper output;

        public TuningSweep(ITestOutputHelper output)
        {
            this.output = output;
        }

        // Each real recording has 7 flicks, a little over a second apart, with strumming in between
        internal static readonly string[] RealRecordings = { "especial_real_1.csv", "especial_real_2.csv", "especial_real_3.csv" };
        internal const int FlicksPerRecording = 7;

        internal static IEnumerable<KeyValuePair<string, AccelSimulator>> FalseAlarmScenarios()
        {
            foreach (var jolt in new[] { new[] { 0.4, 30 }, new[] { 0.6, 40 }, new[] { 1.0, 30 }, new[] { 1.5, 20 } })
                for (int seed = 1; seed <= 5; seed++)
                    yield return new KeyValuePair<string, AccelSimulator>("strum " + jolt[0] + "g/" + jolt[1] + "ms #" + seed,
                        new AccelSimulator(30, seed: seed).Hold(2000).Strumming(jolt[0], jolt[1], 120, 15000));

            foreach (var bump in new[] { new[] { 1.5, 0, 0 }, new[] { 0, 0, 1.5 }, new[] { 1.2, 0, 1.2 } })
                yield return new KeyValuePair<string, AccelSimulator>("bump " + string.Join(",", bump),
                    new AccelSimulator(30).Hold(2000).Bump(bump[0], bump[1], bump[2], 150).Hold(1000));

            yield return new KeyValuePair<string, AccelSimulator>("sway", new AccelSimulator(30).Hold(2000).Sway(8, 1.5, 15000));
            yield return new KeyValuePair<string, AccelSimulator>("slow posture", new AccelSimulator(30).Hold(2000).Move(75, 8000).Hold(2000).Move(10, 10000).Hold(2000));
            yield return new KeyValuePair<string, AccelSimulator>("posture 3s", new AccelSimulator(30).Hold(2000).Move(60, 3000).Hold(2000).Move(30, 3000).Hold(2000));
            yield return new KeyValuePair<string, AccelSimulator>("small move", new AccelSimulator(30).Hold(2000).Move(42, 150).Hold(1000).Move(30, 150).Hold(1000));
            yield return new KeyValuePair<string, AccelSimulator>("rest", new AccelSimulator(30).Hold(15000));
        }

        [Fact]
        [Trait("Category", "Sweep")]
        public void PrintSettingsTable()
        {
            var recordings = RealRecordings.Select(Recording.Load).ToArray();
            var falseAlarmCases = FalseAlarmScenarios().ToList();

            output.WriteLine("angle  tol  hold smooth follow | real flicks caught (of 21) | false alarms (cases)");
            foreach (float angle in new[] { 15f, 20f, 25f })
            foreach (double tolerance in new[] { 0.45 })
            foreach (double hold in new[] { 40.0, 50, 60 })
            foreach (double smoothing in new[] { 10.0, 15 })
            foreach (double follow in new[] { 400.0, 600, 1000, 1500 })
            {
                Func<TiltGestureDetector> make = () => new TiltGestureDetector
                {
                    TriggerAngleDegrees = angle,
                    MagnitudeTolerance = tolerance,
                    HoldMs = hold,
                    SmoothingMs = smoothing,
                    RestFollowMs = follow
                };

                int[] caught = recordings.Select(r => Recording.CountPresses(make(), r)).ToArray();
                var falseAlarms = falseAlarmCases
                    .Where(c => DetectorRun.Of(make(), c.Value).Presses > 0)
                    .Select(c => c.Key)
                    .ToList();

                output.WriteLine("{0,4}  {1,4}  {2,3}  {3,4}  {4,5}  | {5,2} ({6})              | {7} {8}",
                    angle, tolerance, hold, smoothing, follow, caught.Sum(), string.Join("+", caught),
                    falseAlarms.Count, string.Join("; ", falseAlarms.Take(3)));
            }
        }
    }
}
