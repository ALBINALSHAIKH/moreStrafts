using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace moreStrafts.Patches
{
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

                var secondaryObjects = SecondaryObjects(__instance);
                var pointsTexts = PointsTexts(__instance);
                var primaryMesh = PrimaryMesh(__instance);

                var teamIds = ScoreManager.Instance.TeamIdToPlayerIds.Keys.ToArray();
                int activeTeams = teamIds.Count(teamId =>
                    ScoreManager.Instance.TeamIdToPlayerIds[teamId].Any(playerId => ClientInstance.playerInstances.ContainsKey(playerId)));

                int secondaryActive = Mathf.Clamp(activeTeams - 2, 0, secondaryObjects.Length);
                for (int i = 0; i < secondaryObjects.Length; i++)
                    secondaryObjects[i].gameObject.SetActive(i < secondaryActive);

                foreach (var text in pointsTexts) text.text = "";

                var primaryMaterials = primaryMesh.materials;
                int displayedTeams = Mathf.Min(activeTeams, 2 + secondaryObjects.Length);
                for (int teamNumber = 0; teamNumber < displayedTeams && teamNumber < teamIds.Length; teamNumber++)
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
