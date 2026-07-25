using BepInEx;
using HarmonyLib;

namespace Expelliarmus
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("PEAK.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;

        private void Awake()
        {
            Logger.LogInfo(PluginInfo.Name + " v" + PluginInfo.Version + " loading...");

            harmony = new Harmony(PluginInfo.GUID);
            ExpelliarmusBehaviour.Initialize(Logger);

            Logger.LogInfo(PluginInfo.Name + " loaded successfully!");
            Logger.LogInfo("  Right click while aiming at a teammate's held item = steal it into your hand.");
        }

        private void OnDestroy()
        {
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
            Logger.LogInfo(PluginInfo.Name + " unloaded");
        }
    }

    public static class PluginInfo
    {
        public const string GUID = "com.dawwnforu.expelliarmus";
        public const string Name = "Expelliarmus";
        public const string Version = "1.0.3";
    }
}
