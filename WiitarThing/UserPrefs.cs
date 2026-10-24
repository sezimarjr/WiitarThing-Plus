using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace WiitarThing
{
    public class UserPrefs
    {
        private static UserPrefs _instance;
        public static UserPrefs Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + @"\prefs.config"))
                    {
                        DataPath = AppDomain.CurrentDomain.BaseDirectory + @"\prefs.config";
                        LoadPrefsOrDefaults();
                    }
                    else if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WiitarThing_prefs.config"))
                    {
                        DataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WiitarThing_prefs.config";
                        LoadPrefsOrDefaults();
                    }
                    else
                    {
                        _instance = new UserPrefs();
                        _instance.devicePrefs = new List<Property>();
                        _instance.defaultProfile = new Profile();
                        // we could, but just in case lets not
                        //_instance.greedyMode = Environment.OSVersion.Version.Major < 10; 
                        //_instance.toshibaMode = !Shared.Windows.NativeImports.BluetoothEnableDiscovery(IntPtr.Zero, true);
                        DataPath = AppDomain.CurrentDomain.BaseDirectory + @"\prefs.config";
                        
                        if (!SavePrefs())
                        {
                            DataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WiitarThing_prefs.config";
                            SavePrefs();
                        }
                    }
                }

                return _instance;
            }
        }

        public static string DataPath { get; protected set; }
        public static bool AutoStart
        {
            get { return Instance.autoStartup; }
            set
            {
                try
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                    if (value)
                    {
                        if (key.GetValue("WiinUSoft") == null)
                        {
                            key.SetValue("WiinUSoft", (new Uri(System.Reflection.Assembly.GetEntryAssembly().CodeBase)).LocalPath);
                        }
                    }
                    else
                    {
                        key.DeleteValue("WiinUSoft", false);
                    }
                }
                catch
                {
                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

                    if (value)
                    {
                        if (!File.Exists(Path.Combine(dir, "WiinUSoft.lnk")))
                        {
                            MainWindow.Instance.CreateShortcut(dir);
                        }
                    }
                    else
                    {
                        if (File.Exists(Path.Combine(dir, "WiinUSoft.lnk")))
                        {
                            File.Delete(Path.Combine(dir, "WiinUSoft.lnk"));
                        }
                    }
                }

                Instance.autoStartup = value;
            }
        }

        public List<Property> devicePrefs;
        public Profile defaultProfile;
        public Property defaultProperty;
        public bool autoStartup;
        public bool startMinimized;
        public bool greedyMode;
        public bool toshibaMode;
        public bool autoRefresh = true;

        public UserPrefs()
        { }

        public static bool LoadPrefs()
        {
            bool successful = false;
            XmlSerializer X = new XmlSerializer(typeof(UserPrefs));
            
            try
            {
                if (File.Exists(DataPath))
                {
                    using (FileStream stream = File.OpenRead(DataPath))
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        _instance = X.Deserialize(reader) as UserPrefs;
                        reader.Close();
                        stream.Close();
                    }

                    successful = true;

                    if (_instance != null && _instance.devicePrefs != null)
                        _instance.defaultProperty = _instance.devicePrefs.Find((p) => p.hid.ToLower().Equals("all"));
                }
            }
            catch (Exception e) 
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            return successful;
        }

        // A missing or unreadable prefs file must never leave Instance null, or the app crashes on every start
        private static void LoadPrefsOrDefaults()
        {
            if (!LoadPrefs() || _instance == null)
            {
                try
                {
                    // Keep the broken file around instead of silently overwriting it on the next save
                    File.Copy(DataPath, DataPath + ".broken", true);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e.Message);
                }

                _instance = new UserPrefs();
            }

            if (_instance.devicePrefs == null)
                _instance.devicePrefs = new List<Property>();

            if (_instance.defaultProfile == null)
                _instance.defaultProfile = new Profile();
        }

        public static bool SavePrefs()
        {
            bool successful = false;
            XmlSerializer X = new XmlSerializer(typeof(UserPrefs));
            string tempPath = DataPath + ".tmp";

            try
            {
                // Write a complete copy first and then swap it in, so a crash or power loss
                // mid-save can't leave a truncated prefs file behind
                using (FileStream stream = File.Create(tempPath))
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    X.Serialize(writer, _instance);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(DataPath))
                {
                    File.Replace(tempPath, DataPath, null);
                }
                else
                {
                    File.Move(tempPath, DataPath);
                }

                successful = true;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);

                try
                {
                    File.Delete(tempPath);
                }
                catch { }
            }

            return successful;
        }

        public Property GetDevicePref(string hid)
        {
            foreach (var pref in devicePrefs)
            {
                if (pref.hid == hid)
                {
                    return pref;
                }
            }

            if (defaultProperty == null)
            {
                return null;
            }

            // Hand out a copy for this device: sharing the default object meant editing one
            // device's properties changed (and saved over) the defaults for all of them
            return new Property(defaultProperty) { hid = hid };
        }

        /// <summary>
        /// Sets the default properties for devices without their own, and stores them so they survive a restart.
        /// </summary>
        public void SetDefaultProperty(Property property)
        {
            if (defaultProperty != null)
            {
                devicePrefs.Remove(defaultProperty);
            }

            devicePrefs.RemoveAll(p => p.hid.ToLower().Equals("all"));

            defaultProperty = property;
            if (property != null)
            {
                devicePrefs.Add(property);
            }
        }

        public void AddDevicePref(Property property)
        {
            foreach (var pref in devicePrefs)
            {
                if (pref.hid == property.hid)
                {
                    pref.name            = property.name;
                    pref.autoConnect     = property.autoConnect;
                    pref.profile         = property.profile;
                    pref.connType        = property.connType;
                    pref.autoNum         = property.autoNum;
                    pref.rumbleIntensity = property.rumbleIntensity;
                    pref.useRumble       = property.useRumble;
                    pref.calPref         = property.calPref;
                    pref.calString       = property.calString;
                    pref.pointerMode     = property.pointerMode;
                    pref.tiltCalibration = property.tiltCalibration;
                    pref.tiltPressesSelect = property.tiltPressesSelect;
                    pref.tiltPressPercent = property.tiltPressPercent;
                    pref.tiltAutoDetect = property.tiltAutoDetect;
                    pref.tiltGestureAngle = property.tiltGestureAngle;
                    pref.tiltGestureSpeed = property.tiltGestureSpeed;

                    return;
                }
            }

            devicePrefs.Add(property);
        }

        public void UpdateDeviceIcon(string path, string icon)
        {
            var prop = devicePrefs.FindIndex((p) => p.hid == path);

            if (prop >= 0)
            {
                devicePrefs[prop].lastIcon = icon;
                SavePrefs();
            }
        }

        public string GetDeviceIcon(string path)
        {
            var prop = devicePrefs.FindIndex((p) => p.hid == path);

            if (prop >= 0)
            {
                return devicePrefs[prop].lastIcon;
            }

            return "";
        }
    }

    public class Property
    {
        public enum ProfHolderType
        {
            XInput = 0,
            DInput = 1
        }

        public enum CalibrationPreference
        {
            Raw     = -2,
            Minimal = -1,
            Default = 0,
            Defalut = 0,
            More    = 1,
            Extra   = 2,
            Custom  = 3
        }

        public enum PointerOffScreenMode
        {
            Center = 0,
            SnapX  = 1,
            SnapY  = 2,
            SnapXY = 3
        }

        public string hid = "";
        public string name = "";
        public string lastIcon = "";
        public bool autoConnect = false;
        public bool useRumble = true;
        public int autoNum = 0;
        public int rumbleIntensity = 2;
        public ProfHolderType connType;
        public string profile = "";
        public CalibrationPreference calPref;
        public string calString = ""; // not the best solution for saving the custom config but makes it easy
        public PointerOffScreenMode pointerMode = PointerOffScreenMode.Center;
        public NintrollerLib.GuitarTiltCalibration tiltCalibration;
        public bool tiltPressesSelect = true;
        public int tiltPressPercent = 80;
        // Automatic mode: Star Power on a quick flick from the learned playing pose, no calibration needed
        public bool tiltAutoDetect = false;
        public int tiltGestureAngle = 25;
        public int tiltGestureSpeed = 45;

        public Property()
        {
            hid = "";
            connType = ProfHolderType.XInput;
            calPref = CalibrationPreference.Default;
            pointerMode = PointerOffScreenMode.Center;
        }

        public Property(string ID)
        {
            hid = ID;
            connType = ProfHolderType.XInput;
            calPref = CalibrationPreference.Default;
            pointerMode = PointerOffScreenMode.Center;
        }

        public Property(Property copy)
        {
            hid = copy.hid;
            name = copy.name;
            autoConnect = copy.autoConnect;
            autoNum = copy.autoNum;
            useRumble = copy.useRumble;
            rumbleIntensity = copy.rumbleIntensity;
            connType = copy.connType;
            profile = copy.profile;
            calPref = copy.calPref;
            calString = copy.calString;
            pointerMode = copy.pointerMode;
            tiltCalibration = copy.tiltCalibration;
            tiltPressesSelect = copy.tiltPressesSelect;
            tiltPressPercent = copy.tiltPressPercent;
            tiltAutoDetect = copy.tiltAutoDetect;
            tiltGestureAngle = copy.tiltGestureAngle;
            tiltGestureSpeed = copy.tiltGestureSpeed;
        }
    }

    public class Profile
    {
        public enum HolderType
        {
            XInput = 0,
            DInput = 1
        }

        public NintrollerLib.ControllerType profileType;
        public HolderType connType;
        public List<string> controllerMapKeys;
        public List<string> controllerMapValues;

        public Profile()
        {
            profileType = NintrollerLib.ControllerType.Wiimote;
            controllerMapKeys = new List<string>();
            controllerMapValues = new List<string>();
            connType = HolderType.XInput;
        }

        public Profile(NintrollerLib.ControllerType type)
        {
            profileType = type;
            controllerMapKeys = new List<string>();
            controllerMapValues = new List<string>();
            connType = HolderType.XInput;
        }
    }

}
