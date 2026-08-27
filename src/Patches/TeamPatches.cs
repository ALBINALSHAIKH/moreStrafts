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

        /// <summary>
        /// Free-for-all: every player is their own team. Vanilla gets here through
        /// ScoreManager.GetTeamId's lazy fallback, but only for players it happens to look up.
        /// </summary>
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
        /// Teams mode. ScoreManager.Default2v2 hardcodes players 0-3 into teams 0 and 1 and leaves
        /// everyone else out of PlayerIdToTeamId entirely; GetTeamId then lazily drops each of them
        /// into a solo team of their own, so a 10-player "Teams" match silently becomes 2v2 plus six
        /// one-player teams. Split every connected player across the two teams instead. The first
        /// half goes to team 0 and the rest to team 1, which reproduces Default2v2 exactly at four
        /// players.
        /// </summary>
        public static void AssignBalancedTeams(string context)
        {
            try
            {
                if (!IsServer || ScoreManager.Instance == null) return;

                var playerIds = ConnectedPlayerIds();
                if (playerIds.Length == 0) return;

                ScoreManager.Instance.ResetTeams();
                int half = (playerIds.Length + 1) / 2;
                for (int i = 0; i < playerIds.Length; i++)
                    ScoreManager.Instance.SetTeamId(playerIds[i], i < half ? 0 : 1);
                Log.Info($"{context}: balanced teams {half}v{playerIds.Length - half} [{string.Join(",", playerIds)}]");
            }
            catch (Exception e)
            {
                Log.Error($"Teams.AssignBalancedTeams({context}): {e}");
            }
        }

        /// <summary>
        /// Players who joined after the gamemode was chosen have no team at all. Drop each one on
        /// the smaller side without touching anybody who already picked a team by hand.
        /// </summary>
        public static void FillMissingTeams(string context)
        {
            try
            {
                if (!IsServer || ScoreManager.Instance == null) return;

                var unassigned = ConnectedPlayerIds()
                    .Where(playerId => !ScoreManager.Instance.PlayerIdToTeamId.TryGetValue(playerId, out _))
                    .ToArray();
                if (unassigned.Length == 0) return;

                foreach (int playerId in unassigned)
                {
                    int teamId = ConnectedCount(0) <= ConnectedCount(1) ? 0 : 1;
                    ScoreManager.Instance.SetTeamId(playerId, teamId);
                }
                Log.Info($"{context}: assigned teamless players [{string.Join(",", unassigned)}]");
            }
            catch (Exception e)
            {
                Log.Error($"Teams.FillMissingTeams({context}): {e}");
            }
        }

        /// <summary>
        /// Team membership survives disconnects, so counting the raw list would keep balancing
        /// against players who already left.
        /// </summary>
        static int ConnectedCount(int teamId) =>
            ScoreManager.Instance.GetPlayerIdsForTeam(teamId)
                .Count(playerId => ClientInstance.playerInstances.ContainsKey(playerId));
    }

    [HarmonyPatch(typeof(SteamLobby), "SetGamemode", typeof(int))]
    static class GamemodeTeamsPatch
    {
        // value 0 is free-for-all, anything else is teams. Vanilla has just cleared the teams and
        // (for teams mode) applied Default2v2, so rebuilding the assignment here clobbers nothing.
        static void Postfix(int value)
        {
            if (value == 0) Teams.AssignUniqueTeams("SetGamemode");
            else Teams.AssignBalancedTeams("SetGamemode");
        }
    }

    [HarmonyPatch(typeof(GameManager), "StartGame")]
    static class StartGameTeamsPatch
    {
        static void Postfix(GameManager __instance)
        {
            try
            {
                if (GameFields.PlayingTeams(__instance)) Teams.FillMissingTeams("StartGame");
                else Teams.AssignUniqueTeams("StartGame");
            }
            catch (Exception e)
            {
                Log.Error($"StartGameTeamsPatch: {e}");
            }
        }
    }
}
