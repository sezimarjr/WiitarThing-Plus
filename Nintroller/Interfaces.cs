using System;
using System.Collections.Generic;

namespace NintrollerLib
{
    public interface INintrollerState : IEnumerable<KeyValuePair<string, float>>
    {
        void Update(byte[] data);
        float GetValue(string input);
        void SetCalibration(Calibrations.CalibrationPreset preset);
        void SetCalibration(INintrollerState from);
        void SetCalibration(string calibrationString);
        string GetCalibrationString();
        bool CalibrationEmpty { get; }

#if DEBUG
        bool DebugViewActive { get; set; }
#endif
    }

    /// <summary>
    /// A state whose tilt calibration can be read and replaced (applied through the interface so the
    /// boxed state held by <see cref="Nintroller"/> is changed in place).
    /// </summary>
    public interface IGuitarTiltCalibratable
    {
        GuitarTiltCalibration TiltCalibration { get; set; }
        void CaptureRestingPose();
        void CaptureTiltedPose();
    }

    /// <summary>
    /// Which tilt pose to capture from the next report, see <see cref="Nintroller.CaptureGuitarTiltPose"/>.
    /// </summary>
    public enum GuitarTiltPose
    {
        Resting,
        Tilted
    }

    public interface INintrollerParsable
    {
        void Parse(byte[] input, int offset = 0);
    }

    public interface INintrollerNormalizable
    {
        void Normalize();
    }

    public interface INintrollerBounds
    {
        bool InBounds(float x, float y = 0);
    }
}
