using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using StatusEffectFilter.ConfigManagerEntry;

namespace StatusEffectFilter
{
    /* Special thanks to RedSeiko for some of the code in this mod.
     You'll find most of their code I used in the ConfigManagerEntry folder.
     I felt their version was a bit overly complex so it's modified a bit to be more "out-of-the-box" BepInEx configurations without the custom attributes and such.
     I have also added some features and caching that weren't really present before. */
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class StatusEffectFilterPlugin : BaseUnityPlugin
    {
        internal const string ModName = "StatusEffectFilter";
        internal const string ModVersion = "1.0.2";
        internal const string Author = "Azumatt";
        private const string ModGUID = $"{Author}.{ModName}";
        private static string ConfigFileName = $"{ModGUID}.cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource StatusEffectFilterLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        public void Awake()
        {
            ExcludedStatusEffects = config("HUD", "ExcludedStatusEffects", "LocalizedExample=On,$tokenized_example=Off", new ConfigDescription("List of status effects to exclude from HUD. You can use localized names or tokenized for the status effect. Make sure to use =On to enable and =Off to disable. Default values are examples of how to do this directly in the configuration file. The configuration manager will allow you to select these much faster!", null, new ConfigurationManagerAttributes { CustomDrawer = ToggleStringListConfigEntry.Drawer }));
            ExcludedStatusEffects.SettingChanged += (_, _) => ToggleStringListConfigEntry.ToggledStringValues();
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
            ExcludedStatusEffects.SettingChanged -= (_, _) => ToggleStringListConfigEntry.ToggledStringValues();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName)
            {
                IncludeSubdirectories = true,
                SynchronizingObject = ThreadingHelper.SynchronizingObject,
                EnableRaisingEvents = true
            };
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
        }


        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                StatusEffectFilterLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch (Exception ex)
            {
                StatusEffectFilterLogger.LogError($"Error loading {ConfigFileName}: {ex.Message}");
            }
        }


        #region ConfigOptions

        internal static ConfigEntry<string> ExcludedStatusEffects = null!;

        internal ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
            return configEntry;
        }

        internal ConfigEntry<T> config<T>(string group, string name, T value, string description)
        {
            return config(group, name, value, new ConfigDescription(description));
        }

        internal class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
            [UsedImplicitly] public bool? HideDefaultButton = null!;
        }

        #endregion
    }

    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Start))]
    static class FejdStartupStartPatch
    {
        static void Postfix(FejdStartup __instance)
        {
            Setup();
        }

        private static void Setup()
        {
            ToggleStringListConfigEntry.AutoCompleteLabel = new AutoCompleteBox(GetStatusEffectNames);

            AutoCompleteBox.InitializeCacheIfNeeded();
        }

        private static IEnumerable<string> GetStatusEffectNames()
        {
            if (ObjectDB.m_instance == null) return new[] { "" };
            StatusEffectSpriteManager.Instance.Initialize();
            return ObjectDB.m_instance.m_StatusEffects
                .Where(statusEffect => statusEffect.m_icon != null && statusEffect.m_name is { Length: > 0 } && !string.IsNullOrEmpty(statusEffect.m_name))
                .SelectMany(statusEffect => new[] { statusEffect.m_name, Localization.instance.Localize(statusEffect.m_name) })
                .Distinct();
        }
    }
}