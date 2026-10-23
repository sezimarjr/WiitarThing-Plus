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
        }

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

        public TiltCalibrationWindow(Nintroller device, string deviceName, bool tiltPressesSelect, int pressPercent)
        {
            InitializeComponent();

            this.device = device;
            deviceNameText.Text = deviceName;
            TiltPressesSelect = tiltPressesSelect;
            PressPercent = pressPercent;
            pressSelectCheck.IsChecked = tiltPressesSelect;
            pressPercentSlider.Value = pressPercent;
            pressPercentSlider.IsEnabled = tiltPressesSelect;
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
            var snapshot = new Snapshot
            {
                Angle = guitar.TiltAngle,
                Tilt = guitar.TiltHigh,
                Calibration = guitar.TiltCalibration,
                RestingHeld = guitar.TiltCalibratingResting,
                TiltedHeld = guitar.TiltCalibratingTilted,
                Time = DateTime.UtcNow
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

            double degrees = (s.Angle - s.Calibration.Neutral) * 180 / Math.PI;
            double tilt = Math.Max(0, Math.Min(1, s.Tilt));
            bool starPower = tilt >= StarPowerThreshold;

            angleText.Text = string.Format("{0:0}°", degrees);
            percentText.Text = string.Format("{0:0}%", tilt * 100);

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
                liveStatusText.Text = TiltPressesSelect ? "⚡ Especial ativado! (botão Select apertado)" : "⚡ Especial ativado!";
            }
            else
            {
                meterStopStart.Color = (Color)FindResource("AntemSky");
                meterStopEnd.Color = (Color)FindResource("AntemBlue");
                percentText.Foreground = textBrush;
                liveStatusText.Foreground = textBrush;
                liveStatusText.FontWeight = FontWeights.Normal;
                liveStatusText.Text = "Levante o braço da guitarra para soltar o especial";
            }

            double rangeDegrees = (s.Calibration.Tilted - s.Calibration.Neutral) * 180 / Math.PI;
            rangeText.Text = string.Format("O especial chega a 100% com {0:0}° de inclinação a partir da posição de jogo.", rangeDegrees);
            warningBox.Visibility = restingCaptured && tiltedCaptured && Math.Abs(rangeDegrees) < MinimumRangeDegrees
                ? Visibility.Visible
                : Visibility.Collapsed;
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

        private void pressSelectCheck_Changed(object sender, RoutedEventArgs e)
        {
            TiltPressesSelect = pressSelectCheck.IsChecked == true;
            if (pressPercentSlider != null)
                pressPercentSlider.IsEnabled = TiltPressesSelect;
            UpdatePointLabel();
        }

        private void pressPercentSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Also fires while the window is still being built, before the other controls exist
            if (pressPercentText == null)
                return;

            PressPercent = (int)Math.Round(pressPercentSlider.Value);
            pressPercentText.Text = PressPercent + "%";
            UpdatePointLabel();
        }

        // Where the guitar has to get to for Star Power: the chosen point when tilt presses Select,
        // otherwise the halfway point most games use
        private float StarPowerThreshold => TiltPressesSelect ? PressPercent / 100f : StarPowerPoint;

        private void UpdatePointLabel()
        {
            if (pointLabel == null)
                return;

            pointLabel.Text = TiltPressesSelect
                ? string.Format("▲ aqui aperta o especial ({0}%)", PressPercent)
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
