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
using UnityEngine;

namespace StatusEffectFilter
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class StatusEffectFilterPlugin : BaseUnityPlugin
    {
        internal const string ModName = "StatusEffectFilter";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource StatusEffectFilterLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            ConfigUtils.BindConfig(Config);

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        public static IEnumerable<string> GetStatusEffectNames()
        {
            if (ObjectDB.m_instance != null)
            {
                return ObjectDB.m_instance.m_StatusEffects
                    .Where(statusEffect =>
                        statusEffect.m_icon != null && statusEffect.m_name is { Length: > 0 }
                                                    && !string.IsNullOrEmpty(statusEffect.m_name))
                    .SelectMany(statusEffect =>
                        new[]
                        {
                            statusEffect.m_name,
                            Localization.instance.Localize(statusEffect.m_name)
                        }
                    ).Distinct();
            }

            return new[] { "" };
        }

        public static IEnumerable<Sprite> GetStatusEffectIcons()
        {
            if (ObjectDB.m_instance != null)
            {
                return ObjectDB.m_instance.m_StatusEffects
                    .Where(statusEffect =>
                        statusEffect.m_icon != null && statusEffect.m_name is { Length: > 0 }
                                                    && !string.IsNullOrEmpty(statusEffect.m_name))
                    .Select(statusEffect => statusEffect.m_icon);
            }

            return new List<Sprite>();
        }

        [Config(LateBind = true)]
        private static void SetupConfig(ConfigFile config)
        {
            statusEffectConfig = new ToggleStringListConfigEntry(config, "HUD", // Section
                "ExcludedStatusEffects", // Key
                "Effect1=1,Effect2=0", // Default value (format: "name=state")
                "List of status effects to exclude from HUD. Name of effect should be followed by =1 to enable or =0 to disable.",
                GetStatusEffectNames // Method to provide status effect names
            );
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
                StatusEffectFilterLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                StatusEffectFilterLogger.LogError($"There was an issue loading your {ConfigFileName}");
                StatusEffectFilterLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        internal static ToggleStringListConfigEntry statusEffectConfig;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
            //var configEntry = Config.Bind(group, name, value, description);

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description)
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

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        public class ConfigAttribute : Attribute
        {
            public bool LateBind { get; set; } = false;
        }

        public static class ConfigUtils
        {
            public static void BindConfig(ConfigFile config)
            {
                BindConfigs(config, Assembly.GetExecutingAssembly());
            }

            private static void BindConfigs(ConfigFile config, Assembly assembly)
            {
                foreach (var pair in GetBindConfigMethods(assembly))
                {
                    ConfigBinder.Bind(config, pair.Method, pair.Attribute);
                }
            }


            private static IEnumerable<MethodInfoConfigAttributePair> GetBindConfigMethods(Assembly assembly)
            {
                return assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)).SelectMany(method => GetBindConfigMethod(method));
            }

            private static IEnumerable<MethodInfoConfigAttributePair> GetBindConfigMethod(MethodInfo method)
            {
                ConfigAttribute attribute = method.GetCustomAttribute<ConfigAttribute>(inherit: false);

                if (attribute != null)
                {
                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ConfigFile))
                    {
                        yield return new MethodInfoConfigAttributePair(method, attribute);
                    }
                }
            }
        }

        public class MethodInfoConfigAttributePair
        {
            public MethodInfo Method { get; set; }
            public ConfigAttribute Attribute { get; set; }

            public MethodInfoConfigAttributePair(MethodInfo method, ConfigAttribute attribute)
            {
                Method = method;
                Attribute = attribute;
            }
        }


        [HarmonyPatch]
        public static class ConfigBinder
        {
            private static readonly Queue<Action> LateBindQueue = new();
            private static bool StartupPatched = false;

            public static void Bind(ConfigFile config, MethodInfo method, ConfigAttribute attribute)
            {
                if (!attribute.LateBind || StartupPatched)
                {
                    method.Invoke(null, new object[] { config });
                }
                else
                {
                    LateBindQueue.Enqueue(() => method.Invoke(null, new object[] { config }));
                }
            }

            [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Start))]
            public static void Postfix()
            {
                while (LateBindQueue.Count > 0)
                {
                    LateBindQueue.Dequeue()?.Invoke();
                }

                StartupPatched = true;
            }
        }

        #endregion
    }
}