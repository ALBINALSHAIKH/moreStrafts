using System;
using System.Linq;
using FishNet;
using HarmonyLib;

namespace moreStrafts.Patches
{
    static class FfaTeams
    {
        public static void AssignUniqueTeams(string context)
        {
            try
            {
                if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServer) return;
                if (ScoreManager.Instance == null) return;

                var playerIds = ClientInstance.playerInstances.Keys.ToArray();
                ScoreManager.Instance.ResetTeams();
                foreach (int playerId in playerIds)
                    ScoreManager.Instance.SetTeamId(playerId, playerId);
                Log.Info($"{context}: unique FFA team per player [{string.Join(",", playerIds)}]");
            }
            catch (Exception e)
            {
                Log.Error($"FfaTeams({context}): {e}");
            }
        }
    }

    [HarmonyPatch(typeof(SteamLobby), "SetGamemode", typeof(int))]
    static class GamemodeFfaPatch
    {
        static void Postfix(int value)
        {
            if (value == 0) FfaTeams.AssignUniqueTeams("SetGamemode");
        }
    }

    [HarmonyPatch(typeof(GameManager), "StartGame")]
    static class StartGameFfaPatch
    {
        static void Postfix(GameManager __instance)
        {
            try
            {
                if (!GameFields.PlayingTeams(__instance)) FfaTeams.AssignUniqueTeams("StartGame");
            }
            catch (Exception e)
            {
                Log.Error($"StartGameFfaPatch: {e}");
            }
        }
    }
}
