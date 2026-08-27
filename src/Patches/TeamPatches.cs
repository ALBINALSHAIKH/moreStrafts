using System;
using System.Linq;
using FishNet;
using HarmonyLib;

namespace moreStrafts.Patches
{
    static class Teams
    {
        static bool IsServer =>
            InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.IsServer;

        /// <summary>Player ids that are actually connected right now, in a stable order.</summary>
        static int[] ConnectedPlayerIds() =>
            ClientInstance.playerInstances.Keys.OrderBy(playerId => playerId).ToArray();

        static bool HasTeam(int playerId) =>
            ScoreManager.Instance.PlayerIdToTeamId.TryGetValue(playerId, out _);

        /// <summary>
        /// Lowest team id with nobody connected on it. Reusing a live team's id would silently pair
        /// a player up with someone who picked that team by hand.
        /// </summary>
        static int FirstFreeTeamId()
        {
            for (int teamId = 0; ; teamId++)
            {
                bool taken = ScoreManager.Instance.GetPlayerIdsForTeam(teamId)
                    .Any(playerId => ClientInstance.playerInstances.ContainsKey(playerId));
                if (!taken) return teamId;
            }
        }

        /// <summary>Free-for-all: wipe the teams and put every player on their own.</summary>
        public static void AssignUniqueTeams(string context)
        {
            try
            {
                if (!IsServer || ScoreManager.Instance == null) return;

                var playerIds = ConnectedPlayerIds();
                ScoreManager.Instance.ResetTeams();
                foreach (int playerId in playerIds)
                    ScoreManager.Instance.SetTeamId(playerId, playerId);
                Log.Info($"{context}: unique FFA team per player [{string.Join(",", playerIds)}]");
            }
            catch (Exception e)
            {
                Log.Error($"Teams.AssignUniqueTeams({context}): {e}");
            }
        }

        /// <summary>
        /// Give a team to every connected player who has none, and to nobody else. Players who
        /// picked a team from the dropdown keep it.
        /// </summary>
        public static void AssignMissingTeams(string context)
        {
            try
            {
                if (!IsServer || ScoreManager.Instance == null) return;

                var teamless = ConnectedPlayerIds().Where(playerId => !HasTeam(playerId)).ToArray();
                if (teamless.Length == 0) return;

                foreach (int playerId in teamless)
                    ScoreManager.Instance.SetTeamId(playerId, FirstFreeTeamId());
                Log.Info($"{context}: gave a team to [{string.Join(",", teamless)}]");
            }
            catch (Exception e)
            {
                Log.Error($"Teams.AssignMissingTeams({context}): {e}");
            }
        }
    }

    /// <summary>
    /// ScoreManager.Default2v2 hardcodes players 0-3 into teams 0 and 1 and says nothing about
    /// anyone else, so above four players it produces a shape nobody picked: the first four paired
    /// up, the rest on their own. It also runs again on every join, re-pairing those first four and
    /// discarding whatever they had chosen from the team dropdown.
    ///
    /// Above four players the game has no opinion about how teams should look - any split works,
    /// including everyone solo - so the mod does not invent one either. Everybody starts on their
    /// own team and groups up through the dropdown, and picks already made are left alone. At four
    /// players or fewer the vanilla 2v2 default is exactly right, so it runs untouched.
    /// </summary>
    [HarmonyPatch(typeof(ScoreManager), "Default2v2")]
    static class DefaultTeamsPatch
    {
        static bool Prefix()
        {
            try
            {
                if (SteamLobby.Instance == null || SteamLobby.Instance.maxPlayers <= 4) return true;
                Teams.AssignMissingTeams("Default2v2");
                return false;
            }
            catch (Exception e)
            {
                Log.Error($"DefaultTeamsPatch: {e}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(SteamLobby), "SetGamemode", typeof(int))]
    static class GamemodeFfaPatch
    {
        // value 0 is free-for-all, anything else is teams. Vanilla has just reset the teams and, for
        // teams mode, applied Default2v2 - which DefaultTeamsPatch already handles.
        static void Postfix(int value)
        {
            if (value == 0) Teams.AssignUniqueTeams("SetGamemode");
        }
    }

    [HarmonyPatch(typeof(GameManager), "StartGame")]
    static class StartGameTeamsPatch
    {
        static void Postfix(GameManager __instance)
        {
            try
            {
                if (GameFields.PlayingTeams(__instance)) Teams.AssignMissingTeams("StartGame");
                else Teams.AssignUniqueTeams("StartGame");
            }
            catch (Exception e)
            {
                Log.Error($"StartGameTeamsPatch: {e}");
            }
        }
    }
}
