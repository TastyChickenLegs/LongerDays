using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace LongerDays
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class LongerDaysPlugin : BaseUnityPlugin
    {
        internal const string ModName = "LongerDays";
        internal const string ModVersion = "1.0.8";
        internal const string Author = "TastyChickenLegs";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        private static LongerDaysPlugin context;
        public static ConfigEntry<bool> isOn;

        // public static ConfigEntry<int> startingDays;
        public static ConfigEntry<bool> modEnabled;

        public static ConfigEntry<float> dayRate;
        internal static string ConnectionError = "";
        public static long totalSecondsVal;

        private readonly Harmony _harmony = new(ModGUID);
        public static long vanillaDayLengthSec = 1800;

        public static readonly ManualLogSource LongerDaysLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
        { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            context = this;
            _serverConfigLocked = config("", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            context = this;
            isOn = config("", "IsOn", true, "Behaviour is currently on or not");
            modEnabled = config("General", "Enabled", true, "Enable this mod");
            dayRate = context.config("General", "DayRate", 0.5f,
                new ConfigDescription("Speed at which day progresses.  50% is twice as long day.",
                new AcceptableValueRange<float>(0f, 1f), null, new ConfigurationManagerAttributes { ShowRangeAsPercent = true, DispName = "Day Rate" }));

            // startingDays = context.config("General", "StartingDays", 1, "Starting days");
            if (!modEnabled.Value)
                return;

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                LongerDaysLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                LongerDaysLogger.LogError($"There was an issue loading your {ConfigFileName}");
                LongerDaysLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        //private class ConfigurationManagerAttributes
        //{
        //    public bool? Browsable = false;
        //}

        private class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;

            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", KeyboardShortcut.AllKeyCodes);
        }

        #endregion ConfigOptions

        [HarmonyPatch(typeof(EnvMan), "Awake")]
        public static class EnvMan_Awake_Patch
        {
            private static void Postfix(ref long ___m_dayLengthSec)
            {
                if (!modEnabled.Value)
                    return;

                //set the day length according to the base speed of 1800 seconds and config settings

                ___m_dayLengthSec = (long)(Mathf.Round(vanillaDayLengthSec / dayRate.Value));
            }
        }

        [HarmonyPatch(typeof(EnvMan), "FixedUpdate")]
        public static class EnvMan_Update_Patch
        {
            private static void Prefix(ref long ___m_dayLengthSec)
            {
                //set the day lenght according to the base speed and config settings
                ___m_dayLengthSec = (long)(Mathf.Round(vanillaDayLengthSec / dayRate.Value));
            }
        }
    }
}