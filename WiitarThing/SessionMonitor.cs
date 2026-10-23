using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace WiitarThing
{
    /// <summary>
    /// Records Bluetooth report timing and fret behavior while playing, to tell connection problems apart from
    /// worn controller contacts. Started with the --monitor argument; writes a per-second CSV and an events log
    /// to the "monitor" folder next to the exe. The input thread only updates counters; files are written once a second.
    /// </summary>
    internal static class SessionMonitor
    {
        private const int MaxEventsPerSecond = 30;

        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static readonly ConcurrentQueue<string> pendingEvents = new ConcurrentQueue<string>();
        private static readonly List<DeviceStats> devices = new List<DeviceStats>();
        private static readonly object startLock = new object();

        private static string summaryPath;
        private static string eventsPath;
        private static Timer flushTimer;
        private static int eventsThisSecond;
        private static int droppedEvents;

        public static bool Enabled { get; private set; }

        /// <summary>
        /// Milliseconds since the monitor's clock started; used for all timing so it isn't affected by clock changes.
        /// </summary>
        internal static double NowMs => clock.Elapsed.TotalMilliseconds;

        public static void Start()
        {
            lock (startLock)
            {
                if (Enabled)
                    return;

                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "monitor");
                Directory.CreateDirectory(folder);

                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                summaryPath = Path.Combine(folder, "sessao_" + stamp + ".csv");
                eventsPath = Path.Combine(folder, "sessao_" + stamp + "_eventos.log");

                File.WriteAllText(summaryPath,
                    "hora;controle;pacotes;intervalo_medio_ms;maior_intervalo_ms;intervalos_acima_50ms;intervalos_acima_200ms;" +
                    "trastes_apertados;trastes_soltaram_e_voltaram;toques_muito_curtos;muitos_trastes_juntos;palhetadas" + Environment.NewLine);
                File.WriteAllText(eventsPath, "Monitor iniciado em " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + Environment.NewLine);

                Enabled = true;
                flushTimer = new Timer(Flush, null, 1000, 1000);
            }
        }

        public static DeviceStats ForDevice(string name)
        {
            var stats = new DeviceStats(name);
            lock (devices)
            {
                devices.Add(stats);
            }

            return stats;
        }

        /// <summary>
        /// Notes something worth looking at later. Safe from any thread; limited per second so a misbehaving
        /// controller can't flood the log.
        /// </summary>
        public static void Event(string device, string message)
        {
            if (!Enabled)
                return;

            if (Interlocked.Increment(ref eventsThisSecond) > MaxEventsPerSecond)
            {
                Interlocked.Increment(ref droppedEvents);
                return;
            }

            pendingEvents.Enqueue(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + device + ": " + message);
        }

        private static void Flush(object state)
        {
            try
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                var lines = new List<string>();

                lock (devices)
                {
                    foreach (var device in devices)
                    {
                        string line = device.TakeSecond(time);
                        if (line != null)
                            lines.Add(line);
                    }
                }

                if (lines.Count > 0)
                    File.AppendAllLines(summaryPath, lines);

                Interlocked.Exchange(ref eventsThisSecond, 0);
                var events = new List<string>();
                while (pendingEvents.TryDequeue(out string e))
                    events.Add(e);

                int dropped = Interlocked.Exchange(ref droppedEvents, 0);
                if (dropped > 0)
                    events.Add(DateTime.Now.ToString("HH:mm:ss.fff") + "  (" + dropped + " eventos a mais neste segundo foram omitidos)");

                if (events.Count > 0)
                    File.AppendAllLines(eventsPath, events);
            }
            catch (Exception ex)
            {
                WiitarDebug.Log("Session monitor flush failed: " + ex.Message, WiitarDebug.LogLevel.Warning);
            }
        }

        internal sealed class DeviceStats
        {
            // A fret released and pressed again faster than this can't be a person; it's the contact dropping out
            private const double BounceMs = 40;
            private const double ShortPressMs = 12;
            private const double GapWarnMs = 50;
            private const double GapBadMs = 200;

            private static readonly string[] FretNames = { "verde", "vermelho", "amarelo", "azul", "laranja" };

            private readonly string name;
            private readonly bool[] fretDown = new bool[5];
            private readonly double[] fretDownAt = new double[5];
            private readonly double[] fretUpAt = { -1, -1, -1, -1, -1 };

            private double lastReportMs = -1;
            private int lastFretCount;
            private bool lastUp, lastDown;

            // Per-second counters
            private int reports;
            private double gapSum;
            private double maxGap;
            private int gaps50, gaps200, presses, bounces, shortPresses, multiFret, strums;

            public DeviceStats(string name)
            {
                this.name = name;
            }

            /// <summary>
            /// Called on the input thread for every report from a connected guitar.
            /// </summary>
            public void OnReport(bool g, bool r, bool y, bool b, bool o, bool up, bool down)
            {
                double now = NowMs;

                lock (this)
                {
                    if (lastReportMs >= 0)
                    {
                        double gap = now - lastReportMs;
                        gapSum += gap;
                        if (gap > maxGap) maxGap = gap;
                        if (gap > GapWarnMs) gaps50++;
                        if (gap > GapBadMs)
                        {
                            gaps200++;
                            Event(name, string.Format(CultureInfo.InvariantCulture, "ficou {0:0} ms sem receber dados (Bluetooth travou)", gap));
                        }
                    }

                    lastReportMs = now;
                    reports++;

                    Fret(0, g, now);
                    Fret(1, r, now);
                    Fret(2, y, now);
                    Fret(3, b, now);
                    Fret(4, o, now);

                    int count = (g ? 1 : 0) + (r ? 1 : 0) + (y ? 1 : 0) + (b ? 1 : 0) + (o ? 1 : 0);
                    if (count >= 4 && lastFretCount <= 1)
                    {
                        multiFret++;
                        Event(name, count + " trastes apareceram apertados de uma vez (possível falha de leitura)");
                    }

                    lastFretCount = count;

                    if (up && !lastUp) strums++;
                    if (down && !lastDown) strums++;
                    lastUp = up;
                    lastDown = down;
                }
            }

            private void Fret(int i, bool pressed, double now)
            {
                if (pressed == fretDown[i])
                    return;

                fretDown[i] = pressed;

                if (pressed)
                {
                    presses++;
                    if (fretUpAt[i] >= 0 && now - fretUpAt[i] < BounceMs)
                    {
                        bounces++;
                        Event(name, string.Format(CultureInfo.InvariantCulture,
                            "traste {0} soltou por {1:0} ms e voltou sozinho (possível mau contato)", FretNames[i], now - fretUpAt[i]));
                    }

                    fretDownAt[i] = now;
                }
                else
                {
                    fretUpAt[i] = now;
                    if (now - fretDownAt[i] < ShortPressMs)
                    {
                        shortPresses++;
                    }
                }
            }

            /// <summary>
            /// Forget the timing state, e.g. after a disconnect, so the time spent disconnected isn't counted as a gap.
            /// </summary>
            public void Reset()
            {
                lock (this)
                {
                    lastReportMs = -1;
                    for (int i = 0; i < 5; i++)
                    {
                        fretDown[i] = false;
                        fretUpAt[i] = -1;
                    }

                    lastFretCount = 0;
                }
            }

            /// <summary>
            /// Returns this second's CSV row and starts a new second, or null when the controller isn't connected.
            /// </summary>
            public string TakeSecond(string time)
            {
                lock (this)
                {
                    bool active = lastReportMs >= 0;
                    if (!active && reports == 0)
                        return null;

                    // A whole second without a report while connected is itself the problem, so it still gets a row
                    string line = string.Format(CultureInfo.InvariantCulture,
                        "{0};{1};{2};{3:0.0};{4:0};{5};{6};{7};{8};{9};{10};{11}",
                        time, name, reports, reports > 0 ? gapSum / reports : 0, Math.Max(maxGap, active ? NowMs - lastReportMs : 0),
                        gaps50, gaps200, presses, bounces, shortPresses, multiFret, strums);

                    reports = 0;
                    gapSum = 0;
                    maxGap = 0;
                    gaps50 = gaps200 = presses = bounces = shortPresses = multiFret = strums = 0;

                    return line;
                }
            }
        }
    }
}
