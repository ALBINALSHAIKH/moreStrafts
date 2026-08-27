using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace moreStrafts.Patches
{
    /// <summary>
    /// Vanilla UpdateVisuals walks every team in TeamIdToPlayerIds and indexes activeMaterials,
    /// secondaryPointObjects and pointsTexts with the loop counter, so anything past the four teams
    /// the HUD was built for throws. This replaces it with a version that clamps to the slots that
    /// actually exist.
    /// </summary>
    [HarmonyPatch(typeof(MatchPoitnsHUD), "UpdateVisuals", typeof(int), typeof(Dictionary<int, int>))]
    static class MatchPointsHudPatch
    {
        static readonly AccessTools.FieldRef<MatchPoitnsHUD, MeshRenderer[]> SecondaryObjects =
            AccessTools.FieldRefAccess<MatchPoitnsHUD, MeshRenderer[]>("secondaryPointObjects");
        static readonly AccessTools.FieldRef<MatchPoitnsHUD, TMP_Text[]> PointsTexts =
            AccessTools.FieldRefAccess<MatchPoitnsHUD, TMP_Text[]>("pointsTexts");
        static readonly AccessTools.FieldRef<MatchPoitnsHUD, MeshRenderer> PrimaryMesh =
            AccessTools.FieldRefAccess<MatchPoitnsHUD, MeshRenderer>("primaryPointMesh");
        static readonly AccessTools.FieldRef<MatchPoitnsHUD, Material[]> ActiveMaterials =
            AccessTools.FieldRefAccess<MatchPoitnsHUD, Material[]>("activeMaterials");
        static readonly AccessTools.FieldRef<MatchPoitnsHUD, Material> InactiveMaterial =
            AccessTools.FieldRefAccess<MatchPoitnsHUD, Material>("inactiveMaterial");

        static bool warned;

        static bool Prefix(MatchPoitnsHUD __instance, int winnerTeamId, Dictionary<int, int> roundScores)
        {
            try
            {
                if (ClientInstance.playerInstances.Count <= 1) return false;
                var scoreManager = ScoreManager.Instance;
                if (scoreManager == null) return false;

                var secondaryObjects = SecondaryObjects(__instance);
                var pointsTexts = PointsTexts(__instance);
                var primaryMesh = PrimaryMesh(__instance);

                var teamIds = ActiveTeamIds(scoreManager);

                int secondaryActive = Mathf.Clamp(teamIds.Length - 2, 0, secondaryObjects.Length);
                for (int i = 0; i < secondaryObjects.Length; i++)
                    secondaryObjects[i].gameObject.SetActive(i < secondaryActive);

                foreach (var text in pointsTexts) text.text = "";

                var primaryMaterials = primaryMesh.materials;
                int displayedTeams = Mathf.Min(teamIds.Length, 2 + secondaryObjects.Length);
                for (int teamNumber = 0; teamNumber < displayedTeams; teamNumber++)
                {
                    int teamId = teamIds[teamNumber];
                    roundScores.TryGetValue(teamId, out int score);
                    ApplyTeamVisual(__instance, teamNumber, score, teamId == winnerTeamId, primaryMaterials);
                }
                primaryMesh.materials = primaryMaterials;

                return false;
            }
            catch (Exception e)
            {
                if (!warned)
                {
                    warned = true;
                    Log.Error($"MatchPointsHudPatch: {e}");
                }
                return true;
            }
        }

        /// <summary>
        /// Teams whose members are still connected, lowest id first. Filtering matters because
        /// ScoreManager keeps a team around after its last player disconnects, so an unfiltered list
        /// hands HUD slots to empty teams and pushes live ones off the end. Sorting matters because
        /// SyncDictionary key order depends on how updates arrived, so without it the same team can
        /// land in a different slot - and so under a different colour - on each client.
        /// </summary>
        static int[] ActiveTeamIds(ScoreManager scoreManager) =>
            scoreManager.TeamIdToPlayerIds.Keys
                .Where(teamId => scoreManager.TeamIdToPlayerIds[teamId]
                    .Any(playerId => ClientInstance.playerInstances.ContainsKey(playerId)))
                .OrderBy(teamId => teamId)
                .ToArray();

        static void ApplyTeamVisual(MatchPoitnsHUD hud, int teamNumber, int score, bool isWinner, Material[] primaryMaterials)
        {
            var activeMaterials = ActiveMaterials(hud);
            if (teamNumber < 0 || teamNumber >= activeMaterials.Length) return;
            var material = score > 0 ? activeMaterials[teamNumber] : InactiveMaterial(hud);

            if (teamNumber < 2)
            {
                primaryMaterials[teamNumber == 0 ? 1 : 3] = material;
            }
            else
            {
                var secondaryObjects = SecondaryObjects(hud);
                int index = teamNumber - 2;
                if (index < secondaryObjects.Length)
                {
                    var materials = secondaryObjects[index].materials;
                    materials[1] = material;
                    secondaryObjects[index].materials = materials;
                }
            }

            var pointsTexts = PointsTexts(hud);
            if (((score == 2 && !isWinner) || score > 2) && teamNumber < pointsTexts.Length)
                pointsTexts[teamNumber].text = score.ToString();

            if (isWinner) primaryMaterials[2] = material;
        }
    }
}
