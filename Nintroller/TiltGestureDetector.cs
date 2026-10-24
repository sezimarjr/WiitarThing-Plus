using System;
using System.Collections.Generic;

namespace NintrollerLib
{
    /// <summary>
    /// Detects a Star Power "flick": the guitar resting in any playing pose, then quickly rotated away from it.
    /// The resting pose is learned on its own while the guitar is still, so no calibration is needed.
    /// Feed it every accelerometer report; <see cref="IsTriggered"/> acts as the Star Power button.
    /// </summary>
    /// <remarks>
    /// The accelerometer measures gravity plus motion. Only reports close to 1 g are trusted for direction,
    /// so strum jolts that add or remove a lot of acceleration are skipped, and a flick must stay past the
    /// angle for a moment so the short jolts that remain can't trigger it. The resting pose follows the
    /// guitar only while it moves slowly, so changing posture during a song doesn't trigger anything.
    /// </remarks>
    public sealed class TiltGestureDetector
    {
        private const double GapResetMs = 250;        // longer without reports: start over (reconnect, dropout)
        private const double SmoothingMs = 25;        // smoothing of the current direction against sensor noise
        private const double WarmupMs = 500;          // stillness needed before the first resting pose is trusted
        private const double WarmupFollowMs = 200;
        private const double RestFollowMs = 1500;     // how quickly the resting pose follows slow posture changes
        private const double GravityFollowMs = 1000;
        // Sensor noise alone reads as ~15-20 degrees/s over short windows; a flick turns at 150+ degrees/s
        private const double SpeedWindowMs = 150;
        private const double CalmDegreesPerSecond = 45;
        private const double MagnitudeTolerance = 0.25;
        private const double HoldMs = 80;             // a flick must stay past the angle this long (strum jolts don't)
        private const double MaxPressMs = 300;        // the button is let go after this, so the game sees one press
        private const double AdoptAfterMs = 5000;     // staying in a new pose this long makes it the resting pose
        private const double AdoptCalmMs = 1000;

        private struct Direction
        {
            public double X, Y, Z;
        }

        private struct HistoryEntry
        {
            public double TimeMs;
            public Direction Direction;
        }

        private readonly Queue<HistoryEntry> history = new Queue<HistoryEntry>();

        private double? lastTimeMs;
        private bool hasCurrent;
        private Direction current;
        private double gravity;
        private Direction rest;
        private bool hasRestEstimate;
        private double warmupCalmMs;
        private bool armed = true;
        private double? aboveSinceMs;
        private double pressedAtMs;
        private double disarmedAtMs;
        private double adoptCalmMs;

        /// <summary>How far (degrees) the guitar must quickly rotate from its resting pose to trigger.</summary>
        public float TriggerAngleDegrees { get; set; } = 25f;

        /// <summary>The guitar must come back within this many degrees of the resting pose before it can trigger again.</summary>
        public float ReleaseAngleDegrees { get; set; } = 12f;

        /// <summary>0 = any direction triggers; +1 / -1 = only rotations with that sign around the Wiimote's Z axis.</summary>
        public int UpDirection { get; set; }

        /// <summary>
        /// Turning faster than this (degrees/s) counts as a deliberate move; slower is treated as a posture change
        /// that the resting pose follows.
        /// </summary>
        public float MinimumSpeedDegreesPerSecond { get; set; } = 45f;

        /// <summary>How fast (degrees/s) the guitar is turning right now.</summary>
        public float SpeedDegreesPerSecond { get; private set; }

        /// <summary>True once a resting pose has been learned.</summary>
        public bool IsReady { get; private set; }

        /// <summary>True while the Star Power "button" is pressed.</summary>
        public bool IsTriggered { get; private set; }

        /// <summary>Current angle (degrees) between the guitar and its learned resting pose.</summary>
        public float DeviationDegrees { get; private set; }

        /// <summary>Sign (+1 / -1) of the current rotation from the resting pose around the Wiimote's Z axis, 0 if none.</summary>
        public int RotationSign { get; private set; }

        /// <summary>Forget the resting pose, e.g. after a reconnect.</summary>
        public void Reset()
        {
            history.Clear();
            lastTimeMs = null;
            hasCurrent = false;
            gravity = 0;
            hasRestEstimate = false;
            warmupCalmMs = 0;
            armed = true;
            aboveSinceMs = null;
            adoptCalmMs = 0;
            IsReady = false;
            IsTriggered = false;
            DeviationDegrees = 0;
            RotationSign = 0;
        }

        /// <summary>
        /// Feeds one accelerometer report. <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/> are the
        /// accelerometer readings with their zero point removed (any consistent unit). Returns <see cref="IsTriggered"/>.
        /// </summary>
        public bool Update(double timeMs, float x, float y, float z)
        {
            if (lastTimeMs.HasValue && timeMs - lastTimeMs.Value > GapResetMs)
            {
                Reset();
            }

            double dt = lastTimeMs.HasValue ? Math.Max(1, Math.Min(50, timeMs - lastTimeMs.Value)) : 10;
            lastTimeMs = timeMs;

            double magnitude = Math.Sqrt((double)x * x + (double)y * y + (double)z * z);
            if (magnitude < 1e-6)
            {
                return IsTriggered;
            }

            if (gravity <= 0)
            {
                gravity = magnitude;
            }

            // Far from 1 g means the guitar is being jolted; its direction then says nothing about the pose
            bool steadyMagnitude = Math.Abs(magnitude / gravity - 1) <= MagnitudeTolerance;
            var reading = new Direction { X = x / magnitude, Y = y / magnitude, Z = z / magnitude };

            if (!hasCurrent)
            {
                current = reading;
                hasCurrent = true;
            }
            else if (steadyMagnitude)
            {
                current = Blend(current, reading, Alpha(dt, SmoothingMs));
            }

            double speed = Speed(timeMs);
            SpeedDegreesPerSecond = speed == double.MaxValue ? 0 : (float)speed;

            // "Still" decides when the first resting pose and the gravity level can be learned;
            // "posture change" decides when the resting pose follows the guitar
            bool still = steadyMagnitude && speed < CalmDegreesPerSecond;
            bool postureChange = steadyMagnitude && speed < MinimumSpeedDegreesPerSecond;
            if (still)
            {
                gravity += (magnitude - gravity) * Alpha(dt, GravityFollowMs);
            }

            if (!IsReady)
            {
                WarmUp(still, dt);
                return IsTriggered;
            }

            double deviation = AngleDegrees(rest, current);
            DeviationDegrees = (float)deviation;
            RotationSign = Math.Sign(rest.X * current.Y - rest.Y * current.X);

            FollowRest(postureChange, dt, deviation, timeMs);
            UpdateButton(steadyMagnitude, deviation, timeMs);

            return IsTriggered;
        }

        // Learns the first resting pose once the guitar has been still for a moment
        private void WarmUp(bool calm, double dt)
        {
            if (!calm)
            {
                warmupCalmMs = 0;
                hasRestEstimate = false;
                return;
            }

            rest = hasRestEstimate ? Blend(rest, current, Alpha(dt, WarmupFollowMs)) : current;
            hasRestEstimate = true;
            warmupCalmMs += dt;

            if (warmupCalmMs >= WarmupMs)
            {
                IsReady = true;
            }
        }

        private void FollowRest(bool calm, double dt, double deviation, double timeMs)
        {
            if (armed && !IsTriggered)
            {
                // Slow posture changes move the resting pose along with the guitar
                if (calm)
                {
                    rest = Blend(rest, current, Alpha(dt, RestFollowMs));
                }

                return;
            }

            if (armed)
            {
                return;
            }

            // After a flick the resting pose stays put, so coming back down isn't seen as another flick.
            // Staying in the new pose for long enough makes it the new resting pose.
            if (deviation < ReleaseAngleDegrees)
            {
                armed = true;
                return;
            }

            adoptCalmMs = calm ? adoptCalmMs + dt : 0;
            if (timeMs - disarmedAtMs >= AdoptAfterMs && adoptCalmMs >= AdoptCalmMs)
            {
                rest = current;
                armed = true;
            }
        }

        private void UpdateButton(bool steadyMagnitude, double deviation, double timeMs)
        {
            if (IsTriggered)
            {
                if (deviation < ReleaseAngleDegrees || timeMs - pressedAtMs >= MaxPressMs)
                {
                    IsTriggered = false;
                    if (deviation >= ReleaseAngleDegrees)
                    {
                        armed = false;
                        disarmedAtMs = timeMs;
                        adoptCalmMs = 0;
                    }
                }

                return;
            }

            if (!armed)
            {
                return;
            }

            bool rightWay = UpDirection == 0 || RotationSign == UpDirection;
            if (deviation < TriggerAngleDegrees || !rightWay)
            {
                aboveSinceMs = null;
                return;
            }

            if (!steadyMagnitude)
            {
                return;
            }

            if (!aboveSinceMs.HasValue)
            {
                aboveSinceMs = timeMs;
            }
            else if (timeMs - aboveSinceMs.Value >= HoldMs)
            {
                IsTriggered = true;
                pressedAtMs = timeMs;
                aboveSinceMs = null;
            }
        }

        // Rotation speed of the smoothed direction over the last ~100 ms, in degrees per second
        private double Speed(double timeMs)
        {
            history.Enqueue(new HistoryEntry { TimeMs = timeMs, Direction = current });
            while (history.Count > 1 && timeMs - history.Peek().TimeMs > SpeedWindowMs)
            {
                history.Dequeue();
            }

            var oldest = history.Peek();
            double span = timeMs - oldest.TimeMs;
            if (span < SpeedWindowMs / 2)
            {
                return double.MaxValue;
            }

            return AngleDegrees(oldest.Direction, current) / (span / 1000);
        }

        private static double Alpha(double dt, double timeConstantMs)
        {
            return 1 - Math.Exp(-dt / timeConstantMs);
        }

        private static Direction Blend(Direction from, Direction to, double amount)
        {
            double x = from.X + (to.X - from.X) * amount;
            double y = from.Y + (to.Y - from.Y) * amount;
            double z = from.Z + (to.Z - from.Z) * amount;
            double length = Math.Sqrt(x * x + y * y + z * z);
            if (length < 1e-9)
            {
                return to;
            }

            return new Direction { X = x / length, Y = y / length, Z = z / length };
        }

        private static double AngleDegrees(Direction a, Direction b)
        {
            double dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            dot = Math.Max(-1, Math.Min(1, dot));
            return Math.Acos(dot) * 180 / Math.PI;
        }
    }
}
