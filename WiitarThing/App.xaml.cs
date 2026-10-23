using Microsoft.Shell;
using System;
using System.Collections.Generic;
using System.Windows;

namespace WiitarThing
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstanceApp
    {
        internal const string PROFILE_FILTER = "WiitarThing Profile|*.wsp";

        internal static bool WantsMonitor(IEnumerable<string> args)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--monitor", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            // Launching with --monitor while already running turns the monitor on here, so it can be
            // started without disconnecting the controllers
            if (WantsMonitor(args))
            {
                SessionMonitor.Start();
                ((MainWindow)this.MainWindow).ShowMonitorState();
                return true;
            }

            // show the original instance
            if (this.MainWindow.WindowState == WindowState.Minimized)
            {
                ((MainWindow)this.MainWindow).ShowWindow();
            }

            this.MainWindow.Activate();

            MessageBox.Show("WiitarThing was already Running so the previous instance was brought into focus.",
                "WiitarThing Already Running", MessageBoxButton.OK, MessageBoxImage.Information);

            return true;
        }
    }
}
