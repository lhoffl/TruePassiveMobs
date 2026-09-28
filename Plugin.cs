using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;
using UnityEngine;

namespace TruePassiveMobs
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.ClientMustHaveMod, VersionStrictness.Minor)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim_server.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.lhoffl.TruePassiveMobs";
        public const string PluginName = "TruePassiveMobs";
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private FileSystemWatcher _configWatcher;
        private volatile bool _configFileChanged;

        private void Awake()
        {
            Log = Logger;
            ModConfig.Bind(Config);
            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGUID);
            WatchConfigFile();
        }

        private void Update()
        {
            if (_configFileChanged)
            {
                _configFileChanged = false;
                Config.Reload();
                Log.LogInfo("Config reloaded");
            }
            ProfileSync.Update();
            ModConfig.WarnAboutUnknownNames();
        }

        private void OnDestroy()
        {
            _configWatcher?.Dispose();
            _harmony?.UnpatchSelf();
        }

        private void WatchConfigFile()
        {
            string path = Config.ConfigFilePath;
            _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
            };
            _configWatcher.Changed += (_, _) => _configFileChanged = true;
            _configWatcher.Created += (_, _) => _configFileChanged = true;
            _configWatcher.Renamed += (_, _) => _configFileChanged = true;
        }
    }
}
