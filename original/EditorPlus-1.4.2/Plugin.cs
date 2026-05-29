using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EditorPlus
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        public static new ManualLogSource Logger;
        private Harmony _harmony;

        void Awake()
        {
            Instance = this;
            Logger = base.Logger;
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            _harmony.PatchAll();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _harmony?.UnpatchSelf();
            if (_overlayRoot) Destroy(_overlayRoot);
            if (_bundle) _bundle.Unload(false);
            _bundleStream?.Dispose();
        }
    }
}

