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
        internal Harmony _harmony; // Made internal so patches can access it

        void Awake()
        {
            Instance = this;
            Logger = base.Logger;
            Logger?.LogInfo($"[EditorPlus] Plugin Awake - GUID: {MyPluginInfo.PLUGIN_GUID}");
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            try
            {
                _harmony.PatchAll();
                Logger?.LogInfo("[EditorPlus] Harmony patches applied");
            }
            catch (Exception ex)
            {
                Logger?.LogWarning($"[EditorPlus] Some Harmony patches failed (this may be normal): {ex.Message}");
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Logger?.LogInfo("[EditorPlus] Scene event handlers registered");
            
            // Create copy-paste handler early to ensure it runs before other mods
            CopyPasteInputHandler.EnsureExists();
            
            // Create hold position monitor to continuously apply hold position
            HoldPositionContinuousMonitor.EnsureExists();
            
            // Initialize free camera collision monitor
            Patches.FreeCameraCollisionPatch.Initialize();
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
