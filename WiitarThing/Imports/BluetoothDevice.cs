using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Collections.Generic;

namespace WiitarThing
{
    using static NativeImports;

    public class BluetoothDevice
    {
        private BluetoothRadio radio;
        private BLUETOOTH_DEVICE_INFO info;

        public string Name => info.szName;
        public ulong Address => info.Address;
        public uint DeviceClass => info.ulClassofDevice;
        public bool Connected => info.fConnected;
        public bool Remembered => info.fRemembered;
        public bool Authenticated => info.fAuthenticated;
        public DateTime LastSeen { get; }
        public DateTime LastUsed { get; }

        internal BluetoothDevice(BluetoothRadio radio, BLUETOOTH_DEVICE_INFO info)
        {
            this.radio = radio;
            this.info = info;

            LastSeen = ToDateTime(info.stLastSeen);
            LastUsed = ToDateTime(info.stLastUsed);
        }

        // Windows reports all-zero times for devices that were never seen/used (e.g. a Wiimote
        // being synced for the first time), which DateTime can't represent
        private static DateTime ToDateTime(SYSTEMTIME time)
        {
            try
            {
                return new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second, time.Milliseconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Authenticates this device with the given password.
        /// </summary>
        public uint Authenticate(string password)
        {
            return radio.AuthenticateDevice(ref info, password);
        }

        /// <summary>
        /// Removes this device from the system.
        /// </summary>
        public uint Remove()
        {
            return NativeImports.BluetoothRemoveDevice(in info.Address);
        }

        /// <summary>
        /// Gets an array of all enabled services on this device.
        /// </summary>
        public uint EnumerateInstalledServices(out Guid[] serviceGuids)
        {
            return radio.EnumerateInstalledServices(in info, out serviceGuids);
        }

        /// <summary>
        /// Sets the state of a service on this device.
        /// </summary>
        public uint SetServiceState(Guid serviceGuid, bool enable)
        {
            return radio.SetServiceState(in info, serviceGuid, enable);
        }
    }
}