using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using NintrollerLib;

namespace WiitarThing.Windows
{
    /// <summary>
    /// Live tilt (Star Power) calibration for a connected guitar: shows the current angle and tilt,
    /// and captures the resting and tilted poses (also possible with the Wiimote's 1 and 2 buttons).
    /// </summary>
    public partial class TiltCalibrationWindow : Window
    {
        // Latest guitar values, written on the input thread and read by the UI timer
        private sealed class Snapshot
        {
            public float Angle;
            public float Tilt;
            public GuitarTiltCalibration Calibration;
            public bool RestingHeld;
            public bool TiltedHeld;
            public DateTime Time;

            // Automatic mode preview
            public bool GestureReady;
            public bool GestureTriggered;
            public float GestureDeviation;
        }

        private const int AccelerometerZero = 127;
        private const double GestureFlashMs = 700; // keep "detected" on screen long enough to see it

        private const float StarPowerPoint = 0.5f;
        private const double PlayingPoseAngle = -25; // how the guitar picture is drawn at 0 tilt
        private const double MinimumRangeDegrees = 8;

        private static readonly Color StarPowerColor = Color.FromRgb(0x2E, 0xB8, 0xF5);
        private static readonly Color StarPowerLightColor = Color.FromRgb(0x9B, 0xE3, 0xFF);

        private readonly Nintroller device;
        private readonly DispatcherTimer uiTimer;
        private volatile Snapshot latest;
        private bool restingCaptured;
        private bool tiltedCaptured;

        // A detector of its own, fed the same reports, so the automatic mode can be tried here before saving
        private readonly TiltGestureDetector previewGesture = new TiltGestureDetector();
        private volatile int previewGestureAngle;
        private DateTime lastGestureUtc = DateTime.MinValue;

        /// <summary>
        /// The calibration the guitar had when the window opened, to put back on cancel.
        /// </summary>
        public GuitarTiltCalibration InitialCalibration { get; private set; }
        public bool HasInitialCalibration { get; private set; }

        /// <summary>
        /// The calibration to keep when the window was closed with Save.
        /// </summary>
        public GuitarTiltCalibration Calibration { get; private set; }

        /// <summary>
        /// Whether tilting past the Star Power point should also press Select.
        /// </summary>
        public bool TiltPressesSelect { get; private set; }

        /// <summary>
        /// How far (percent of the calibrated range) the guitar must be tilted before Select is pressed.
        /// </summary>
        public int PressPercent { get; private set; }

        /// <summary>
        /// True for the automatic mode (quick flick from the learned pose), false for the calibrated one.
        /// </summary>
        public bool AutoDetect { get; private set; }

        /// <summary>
        /// Degrees of quick rotation that count as a flick in the automatic mode.
        /// </summary>
        public int GestureAngle { get; private set; }

        private bool AutoMode => TiltPressesSelect && AutoDetect;

        public TiltCalibrationWindow(Nintroller device, string deviceName, Property settings)
        {
            InitializeComponent();

            this.device = device;
            deviceNameText.Text = deviceName;
            TiltPressesSelect = settings.tiltPressesSelect;
            PressPercent = settings.tiltPressPercent;
            AutoDetect = settings.tiltAutoDetect;
            GestureAngle = settings.tiltGestureAngle;
            previewGestureAngle = GestureAngle;

            pressSelectCheck.IsChecked = TiltPressesSelect;
            pressPercentSlider.Value = PressPercent;
            gestureAngleSlider.Value = GestureAngle;
            (AutoDetect ? autoModeRadio : calibratedModeRadio).IsChecked = true;
            UpdateModeControls();
            UpdatePointLabel();

            device.StateUpdate += Device_StateUpdate;

            uiTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(30) };
            uiTimer.Tick += UpdateView;
            uiTimer.Start();

            UpdateView(null, EventArgs.Empty);
        }

        private void Device_StateUpdate(object sender, NintrollerStateEventArgs e)
        {
            if (e.controllerType != ControllerType.Guitar || !(e.state is Guitar))
            {
                return;
            }

            var guitar = (Guitar)e.state;
            var accelerometer = guitar.wiimote.accelerometer;

            previewGesture.TriggerAngleDegrees = previewGestureAngle;
            previewGesture.Update(
                SessionMonitor.NowMs,
                accelerometer.rawX - AccelerometerZero,
                accelerometer.rawY - AccelerometerZero,
                accelerometer.rawZ - AccelerometerZero);

            var snapshot = new Snapshot
            {
                Angle = guitar.TiltAngle,
                Tilt = guitar.TiltHigh,
                Calibration = guitar.TiltCalibration,
                RestingHeld = guitar.TiltCalibratingResting,
                TiltedHeld = guitar.TiltCalibratingTilted,
                Time = DateTime.UtcNow,
                GestureReady = previewGesture.IsReady,
                GestureTriggered = previewGesture.IsTriggered,
                GestureDeviation = previewGesture.DeviationDegrees
            };

            if (!HasInitialCalibration)
            {
                InitialCalibration = snapshot.Calibration;
                HasInitialCalibration = true;
            }

            latest = snapshot;
        }

        private void UpdateView(object sender, EventArgs e)
        {
            Snapshot s = latest;
            bool live = s != null && (DateTime.UtcNow - s.Time).TotalMilliseconds < 1000;

            captureRestingButton.IsEnabled = live;
            captureTiltedButton.IsEnabled = live;
            saveButton.IsEnabled = live;

            if (!live)
            {
                angleText.Text = "--";
                percentText.Text = "--";
                liveStatusText.Text = "Conecte a guitarra para começar";
                meterFill.Width = 0;
                guitarGlow.Opacity = 0;
                PlacePointMarker();
                return;
            }

            if (s.RestingHeld) MarkStep(1);
            if (s.TiltedHeld) MarkStep(2);

            double degrees;
            double tilt;
            bool starPower;
            string idleStatus;

            if (AutoMode)
            {
                // The meter runs from the playing pose to twice the flick angle, so the flick angle sits in the middle
                if (s.GestureTriggered)
                    lastGestureUtc = DateTime.UtcNow;

                degrees = s.GestureReady ? s.GestureDeviation : 0;
                tilt = Math.Max(0, Math.Min(1, degrees / (2.0 * GestureAngle)));
                starPower = (DateTime.UtcNow - lastGestureUtc).TotalMilliseconds < GestureFlashMs;
                idleStatus = s.GestureReady
                    ? "Faça um movimento rápido com o braço da guitarra para soltar o especial"
                    : "Deixe a guitarra parada um instante: estou aprendendo a sua posição de jogo";

                angleText.Text = s.GestureReady ? string.Format("{0:0}°", degrees) : "--";
                percentText.Text = s.GestureReady ? string.Format("{0:0}%", Math.Min(100, degrees / GestureAngle * 100)) : "...";
            }
            else
            {
                degrees = (s.Angle - s.Calibration.Neutral) * 180 / Math.PI;
                tilt = Math.Max(0, Math.Min(1, s.Tilt));
                starPower = tilt >= StarPowerThreshold;
                idleStatus = "Levante o braço da guitarra para soltar o especial";

                angleText.Text = string.Format("{0:0}°", degrees);
                percentText.Text = string.Format("{0:0}%", tilt * 100);
            }

            // Ease the picture toward the measured angle so sensor noise doesn't make it shake
            double targetRotation = PlayingPoseAngle + Math.Max(-20, Math.Min(90, degrees));
            guitarRotation.Angle += (targetRotation - guitarRotation.Angle) * 0.35;

            double trackWidth = Math.Max(0, meterTrack.ActualWidth - 4);
            meterFill.Width = trackWidth * tilt;
            PlacePointMarker();

            double glowTarget = starPower ? 1 : 0;
            guitarGlow.Opacity += (glowTarget - guitarGlow.Opacity) * 0.3;
            guitarGlow.BlurRadius = 35 * guitarGlow.Opacity;

            var textBrush = (Brush)FindResource("BrushJohnTheRevelator");
            if (starPower)
            {
                meterStopStart.Color = StarPowerLightColor;
                meterStopEnd.Color = StarPowerColor;
                percentText.Foreground = new SolidColorBrush(StarPowerColor);
                liveStatusText.Foreground = percentText.Foreground;
                liveStatusText.FontWeight = FontWeights.Bold;
                liveStatusText.Text = AutoMode ? "⚡ Especial detectado! (botão Select apertado)"
                    : TiltPressesSelect ? "⚡ Especial ativado! (botão Select apertado)" : "⚡ Especial ativado!";
            }
            else
            {
                meterStopStart.Color = (Color)FindResource("AntemSky");
                meterStopEnd.Color = (Color)FindResource("AntemBlue");
                percentText.Foreground = textBrush;
                liveStatusText.Foreground = textBrush;
                liveStatusText.FontWeight = FontWeights.Normal;
                liveStatusText.Text = idleStatus;
            }

            if (AutoMode)
            {
                rangeText.Text = string.Format(
                    "Modo automático: solta o especial com um movimento rápido de {0}° ou mais a partir da posição em que você toca. " +
                    "As posições 1 e 2 só ajustam o eixo de inclinação.", GestureAngle);
                warningBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                double rangeDegrees = (s.Calibration.Tilted - s.Calibration.Neutral) * 180 / Math.PI;
                rangeText.Text = string.Format("O especial chega a 100% com {0:0}° de inclinação a partir da posição de jogo.", rangeDegrees);
                warningBox.Visibility = restingCaptured && tiltedCaptured && Math.Abs(rangeDegrees) < MinimumRangeDegrees
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        // Puts the marker (and its label, kept inside the meter's width) at the Star Power point
        private void PlacePointMarker()
        {
            double width = meterTrack.ActualWidth;
            if (width <= 0)
                return;

            double x = 2 + (width - 4) * StarPowerThreshold;
            pointMarker.Margin = new Thickness(Math.Max(0, x - pointMarker.Width / 2), 0, 0, 0);

            double labelWidth = pointLabel.ActualWidth;
            double labelLeft = Math.Max(0, Math.Min(width - labelWidth, x - 6));
            pointLabel.Margin = new Thickness(labelLeft, 0, 0, 0);
        }

        private void MarkStep(int step)
        {
            var starPowerBrush = new SolidColorBrush(StarPowerColor);

            if (step == 1)
            {
                restingCaptured = true;
                step1Badge.Background = starPowerBrush;
                step1BadgeText.Text = "✓";
                step1Card.BorderBrush = starPowerBrush;
                step1Status.Text = "✔ Posição de jogo marcada";
            }
            else
            {
                tiltedCaptured = true;
                step2Badge.Background = starPowerBrush;
                step2BadgeText.Text = "✓";
                step2Card.BorderBrush = starPowerBrush;
                step2Status.Text = "✔ Posição do especial marcada";
            }
        }

        private void captureRestingButton_Click(object sender, RoutedEventArgs e)
        {
            device.CaptureGuitarTiltPose(GuitarTiltPose.Resting);
            MarkStep(1);
        }

        private void captureTiltedButton_Click(object sender, RoutedEventArgs e)
        {
            device.CaptureGuitarTiltPose(GuitarTiltPose.Tilted);
            MarkStep(2);
        }

        // These handlers also fire while the window is still being built, before every control exists
        private bool ControlsReady => pressPercentText != null && gestureAngleText != null && autoSettings != null && calibratedSettings != null;

        private void pressSelectCheck_Changed(object sender, RoutedEventArgs e)
        {
            TiltPressesSelect = pressSelectCheck.IsChecked == true;
            UpdateModeControls();
            UpdatePointLabel();
        }

        private void mode_Changed(object sender, RoutedEventArgs e)
        {
            AutoDetect = autoModeRadio.IsChecked == true;
            UpdateModeControls();
            UpdatePointLabel();
        }

        private void UpdateModeControls()
        {
            if (!ControlsReady)
                return;

            autoModeRadio.IsEnabled = TiltPressesSelect;
            calibratedModeRadio.IsEnabled = TiltPressesSelect;
            autoSettings.IsEnabled = TiltPressesSelect && AutoDetect;
            calibratedSettings.IsEnabled = TiltPressesSelect && !AutoDetect;
            autoSettings.Opacity = autoSettings.IsEnabled ? 1 : 0.45;
            calibratedSettings.Opacity = calibratedSettings.IsEnabled ? 1 : 0.45;
        }

        private void pressPercentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!ControlsReady)
                return;

            PressPercent = (int)Math.Round(pressPercentSlider.Value);
            pressPercentText.Text = PressPercent + "%";
            UpdatePointLabel();
        }

        private void gestureAngleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!ControlsReady)
                return;

            GestureAngle = (int)Math.Round(gestureAngleSlider.Value);
            previewGestureAngle = GestureAngle;
            gestureAngleText.Text = GestureAngle + "°";
            UpdatePointLabel();
        }

        // Where the meter's marker goes: the flick angle (drawn mid-meter) in the automatic mode, the chosen point
        // when tilt presses Select in the calibrated mode, otherwise the halfway point most games use
        private float StarPowerThreshold => AutoMode ? 0.5f : TiltPressesSelect ? PressPercent / 100f : StarPowerPoint;

        private void UpdatePointLabel()
        {
            if (pointLabel == null)
                return;

            pointLabel.Text = AutoMode ? string.Format("▲ movimento rápido de {0}°", GestureAngle)
                : TiltPressesSelect ? string.Format("▲ aqui aperta o especial ({0}%)", PressPercent)
                : "▲ especial na maioria dos jogos";
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            Snapshot s = latest;
            if (s == null)
            {
                return;
            }

            Calibration = s.Calibration;
            DialogResult = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            uiTimer.Stop();
            device.StateUpdate -= Device_StateUpdate;
        }
    }
}
