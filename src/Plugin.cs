using System;
using BepInEx;
using HarmonyLib;
using moreStrafts.Patches;
using UnityEngine.SceneManagement;

namespace moreStrafts
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.nitrogenia.morestrafts";
        public const string PluginName = "More Strafts Players";
        public const string PluginVersion = "0.1.0";

        static readonly Type[] PatchClasses =
        {
            typeof(SteamLobbyStartPatch),
            typeof(OnLobbyCreatedPatch),
            typeof(SetMaxPlayersPatch),
            typeof(UpdateOnClientsPatch),
            typeof(QuickLoadLobbyDataPatch),
            typeof(QuickLoadTogglePatch),
            typeof(RoundEndInterfacePatch),
            typeof(PlayerListItemUpdatePatch),
            typeof(TeamDropdownPatch),
            typeof(RunIntoLobbyPatch),
            typeof(MenuAnimationObserversPatch),
            typeof(DressAboubiObserversPatch),
            typeof(RoundEndArraysPatch),
            typeof(GamemodeTeamsPatch),
            typeof(StartGameTeamsPatch),
            typeof(MatchPointsHudPatch),
        };

        void Awake()
        {
            var fileLogging = Config.Bind("Debug", "PerInstanceLogFiles", false,
                "Write a small per-instance log file to BepInEx/moreStrafts_debug. Include these files in bug reports.");
            Log.Init(Logger, fileLogging.Value);
            SceneManager.sceneLoaded += (scene, mode) => Log.Info($"scene '{scene.name}' ({mode})");

            var harmony = new Harmony(PluginGuid);
            int applied = 0;
            foreach (var patchClass in PatchClasses)
            {
                try
                {
                    harmony.CreateClassProcessor(patchClass).Patch();
                    applied++;
                }
                catch (Exception e)
                {
                    Log.Error($"patch {patchClass.Name} failed: {e}");
                }
            }
            Log.Info($"{PluginName} {PluginVersion}: {applied}/{PatchClasses.Length} patches applied");
        }
    }
}
