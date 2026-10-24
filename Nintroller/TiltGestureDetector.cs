using System;

namespace NintrollerLib
{
    /// <summary>
    /// Detects a Star Power "flick": the guitar resting in any playing pose, then quickly rotated away from it.
    /// The resting pose is learned on its own while the guitar is still, so no calibration is needed.
    /// Feed it every accelerometer report; <see cref="IsTriggered"/> acts as the Star Power button.
    /// </summary>
    public sealed class TiltGestureDetector
    {
        /// <summary>How far (degrees) the guitar must quickly rotate from its resting pose to trigger.</summary>
        public float TriggerAngleDegrees { get; set; } = 25f;

        /// <summary>The guitar must come back within this many degrees of the resting pose before it can trigger again.</summary>
        public float ReleaseAngleDegrees { get; set; } = 12f;

        /// <summary>0 = any direction triggers; +1 / -1 = only rotations with that sign around the Wiimote's Z axis.</summary>
        public int UpDirection { get; set; }

        /// <summary>True once a resting pose has been learned.</summary>
        public bool IsReady { get; private set; }

        /// <summary>True while the Star Power "button" is pressed.</summary>
        public bool IsTriggered { get; private set; }

        /// <summary>Current angle (degrees) between the guitar and its learned resting pose.</summary>
        public float DeviationDegrees { get; private set; }

        /// <summary>Forget the resting pose, e.g. after a reconnect.</summary>
        public void Reset()
        {
        }

        /// <summary>
        /// Feeds one accelerometer report. <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/> are the
        /// accelerometer readings with their zero point removed (any consistent unit). Returns <see cref="IsTriggered"/>.
        /// </summary>
        public bool Update(double timeMs, float x, float y, float z)
        {
            return IsTriggered;
        }
    }
}
