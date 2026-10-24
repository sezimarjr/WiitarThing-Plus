using System.Linq;
using Xunit;

namespace NintrollerLib.Tests
{
    public class TiltGestureDetectorTests
    {
        // In the simulator, raising the neck rotates gravity so that (rest x now).z is negative
        private const int NeckUp = -1;

        private static TiltGestureDetector NewDetector() => new TiltGestureDetector();

        // ---------- Should trigger ----------

        [Theory]
        [InlineData(0)]
        [InlineData(30)]   // diagonal, like the user plays
        [InlineData(60)]
        [InlineData(-30)]
        [InlineData(85)]   // nearly vertical already
        public void QuickFlick_FromAnyPlayingPose_Triggers(double playingPose)
        {
            var sim = new AccelSimulator(playingPose);
            sim.Hold(2000);
            double flickStart = sim.Now;
            sim.Move(playingPose + 35, 200).Hold(500);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(1, run.Presses);
            Assert.InRange(run.PressTimes[0], flickStart, flickStart + 200 + 300);
        }

        [Theory]
        [InlineData(30, 900, 25)]   // a firm but unhurried lift: ~33 degrees/s on average
        [InlineData(35, 1000, 25)]
        [InlineData(20, 700, 15)]   // smaller lift with the lowest angle setting
        public void DeliberateLift_NotABrusqueFlick_Triggers(double liftDegrees, double ms, float triggerAngle)
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(30 + liftDegrees, ms).Hold(600);

            var detector = NewDetector();
            detector.TriggerAngleDegrees = triggerAngle;
            var run = DetectorRun.Of(detector, sim);

            Assert.True(run.Presses == 1, "presses: " + run.Presses + ", max deviation " + run.MaxDeviation);
        }

        [Fact]
        public void PostureShiftOverAFewSeconds_DoesNotTrigger_EvenAtTheLowestAngle()
        {
            // Settling into a new posture: 30 degrees over 3 s (about 10 degrees/s)
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(60, 3000).Hold(2000).Move(30, 3000).Hold(2000);

            var detector = NewDetector();
            detector.TriggerAngleDegrees = 15;
            var run = DetectorRun.Of(detector, sim);

            Assert.True(run.Presses == 0, "presses: " + run.Presses + ", max deviation " + run.MaxDeviation);
        }

        [Fact]
        public void RequiringABrusqueMove_IgnoresADeliberateLift()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(60, 900).Hold(600);

            var detector = NewDetector();
            detector.MinimumSpeedDegreesPerSecond = 80;
            var run = DetectorRun.Of(detector, sim);

            Assert.Equal(0, run.Presses);
        }

        [Fact]
        public void ReportsHowFastTheGuitarIsTurning()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(70, 300).Hold(300);

            var detector = NewDetector();
            float maxSpeed = 0;
            foreach (var s in sim.Samples)
            {
                detector.Update(s.TimeMs, s.X, s.Y, s.Z);
                maxSpeed = System.Math.Max(maxSpeed, detector.SpeedDegreesPerSecond);
            }

            // 40 degrees eased over 300 ms peaks at about 210 degrees/s
            Assert.InRange(maxSpeed, 120, 260);
        }

        [Fact]
        public void QuickFlick_WithGuitarFaceTilted_Triggers()
        {
            var sim = new AccelSimulator(30, faceTiltDegrees: 20);
            sim.Hold(2000).Move(65, 200).Hold(500);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(1, run.Presses);
        }

        [Fact]
        public void QuickFlick_IsDetectedSoonAfterPassingTheAngle()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000);
            double flickStart = sim.Now;
            sim.Move(70, 250).Hold(500);

            var run = DetectorRun.Of(NewDetector(), sim);

            // The eased move passes 25 degrees from rest about 145 ms into the flick
            Assert.Equal(1, run.Presses);
            Assert.True(run.PressTimes[0] - (flickStart + 145) <= 120,
                "pressed " + (run.PressTimes[0] - flickStart) + " ms after the flick started");
        }

        [Fact]
        public void QuickFlick_DuringHardStrumming_StillTriggers()
        {
            var sim = new AccelSimulator(30, seed: 7);
            sim.Hold(1500).Strumming(0.4, 30, 150, 1500);
            sim.Move(70, 200);
            sim.Strumming(0.4, 30, 150, 600);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(1, run.Presses);
        }

        [Fact]
        public void TwoFlicks_TriggerTwice()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(70, 200).Hold(400).Move(30, 300).Hold(2500).Move(70, 200).Hold(400);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(2, run.Presses);
        }

        // ---------- Should NOT trigger ----------

        [Fact]
        public void Resting_NeverTriggers()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(15000);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(0, run.Presses);
            Assert.True(run.MaxDeviation < 8, "max deviation at rest " + run.MaxDeviation);
        }

        [Fact]
        public void SlowPostureChange_DoesNotTrigger()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(75, 8000).Hold(2000).Move(10, 10000).Hold(2000);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(0, run.Presses);
        }

        [Fact]
        public void SmallQuickMove_DoesNotTrigger()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(42, 150).Hold(1000).Move(30, 150).Hold(1000);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(0, run.Presses);
        }

        [Theory]
        [InlineData(0.4, 30)]
        [InlineData(0.6, 40)]
        [InlineData(1.0, 30)]
        [InlineData(1.5, 20)]
        public void HardStrumming_DoesNotTrigger(double joltG, double joltMs)
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var sim = new AccelSimulator(30, seed: seed);
                sim.Hold(2000).Strumming(joltG, joltMs, 120, 15000);

                var run = DetectorRun.Of(NewDetector(), sim);

                Assert.True(run.Presses == 0, "seed " + seed + " pressed " + run.Presses + " times");
            }
        }

        [Theory]
        [InlineData(1.5, 0, 0)]    // sideways, along the guitar's face
        [InlineData(0, 0, 1.5)]    // into the guitar's face
        [InlineData(1.2, 0, 1.2)]
        public void HardBump_LongerThanAStrum_DoesNotTrigger(double gx, double gy, double gz)
        {
            // Hitting furniture or a sudden body movement: much more than 1 g for longer than the flick hold time.
            // Only the check that readings stay close to 1 g keeps this from looking like a quick tilt.
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Bump(gx, gy, gz, 150).Hold(1000);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(0, run.Presses);
        }

        [Fact]
        public void SwayingToTheMusic_DoesNotTrigger()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Sway(8, 1.5, 15000);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(0, run.Presses);
        }

        [Fact]
        public void RightAfterConnecting_DoesNotTriggerUntilARestingPoseIsKnown()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(200).Move(70, 200).Hold(500);

            var detector = NewDetector();
            var run = DetectorRun.Of(detector, sim);

            Assert.Equal(0, run.Presses);
        }

        [Fact]
        public void BecomesReadyAfterBeingStillForAMoment()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(3000);

            var detector = NewDetector();
            Assert.False(detector.IsReady);

            var run = DetectorRun.Of(detector, sim);

            Assert.True(detector.IsReady);
            Assert.InRange(run.FirstReadyTime.Value, 300, 1500);
        }

        [Fact]
        public void FlickHoldAndReturn_TriggersOnlyOnce()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(70, 200).Hold(2000).Move(30, 250).Hold(2000);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(1, run.Presses);
        }

        [Fact]
        public void ButtonIsReleased_SoTheGameSeesAPress()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(70, 200).Hold(2000);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(1, run.Presses);
            Assert.Single(run.ReleaseTimes);
            double held = run.ReleaseTimes[0] - run.PressTimes[0];
            Assert.InRange(held, 50, 600);
        }

        [Fact]
        public void AfterABluetoothGap_DoesNotTriggerOnTheNewPose()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Gap(600).SetPose(70).Hold(2000);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(0, run.Presses);
        }

        [Fact]
        public void StaysWorkingAfterLongPlay()
        {
            var sim = new AccelSimulator(30, seed: 3);
            sim.Hold(2000);
            for (int i = 0; i < 5; i++)
            {
                sim.Strumming(0.5, 30, 130, 6000).Sway(6, 1, 3000);
                sim.Move(sim.PoseDegrees + 40, 200).Hold(400).Move(sim.PoseDegrees - 40, 300).Hold(1500);
            }

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(5, run.Presses);
        }

        // ---------- Direction ----------

        [Fact]
        public void WithUpDirectionSet_LoweringTheNeckDoesNotTrigger()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(-10, 200).Hold(500);

            var detector = NewDetector();
            detector.UpDirection = NeckUp;
            var run = DetectorRun.Of(detector, sim);

            Assert.Equal(0, run.Presses);
        }

        [Fact]
        public void WithUpDirectionSet_RaisingTheNeckTriggers()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(70, 200).Hold(500);

            var detector = NewDetector();
            detector.UpDirection = NeckUp;
            var run = DetectorRun.Of(detector, sim);

            Assert.Equal(1, run.Presses);
        }

        [Fact]
        public void WithAnyDirection_LoweringTheNeckAlsoTriggers()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(-10, 200).Hold(500);

            var run = DetectorRun.Of(NewDetector(), sim);

            Assert.Equal(1, run.Presses);
        }

        // ---------- Settings ----------

        [Fact]
        public void HigherTriggerAngle_IgnoresAMediumFlick()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000).Move(60, 200).Hold(500);

            var detector = NewDetector();
            detector.TriggerAngleDegrees = 40;
            var run = DetectorRun.Of(detector, sim);

            Assert.Equal(0, run.Presses);
        }

        [Fact]
        public void Reset_ForgetsTheRestingPose()
        {
            var sim = new AccelSimulator(30);
            sim.Hold(2000);

            var detector = NewDetector();
            DetectorRun.Of(detector, sim);
            Assert.True(detector.IsReady);

            detector.Reset();

            Assert.False(detector.IsReady);
            Assert.False(detector.IsTriggered);
        }
    }
}
