using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace moreStrafts.Patches
{
    [HarmonyPatch(typeof(RoundManager), "NextRoundCall")]
    static class RoundEndArraysPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int replaced = 0;
            for (int i = 0; i + 1 < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Ldc_I4_4 || codes[i + 1].opcode != OpCodes.Newarr) continue;
                var element = codes[i + 1].operand as Type;
                if (element != typeof(string) && element != typeof(int)) continue;
                codes[i].opcode = OpCodes.Ldc_I4_S;
                codes[i].operand = (sbyte)ModState.MaxPlayers;
                replaced++;
            }

            if (replaced == 2)
                Log.Info($"round-end name/score arrays extended to {ModState.MaxPlayers}");
            else
                Log.Warn($"round-end array pattern changed ({replaced} matches, expected 2); post-round screen limited to 4 players");
            return codes;
        }
    }

    /// <summary>
    /// RoundManager.InterfaceSetup drives the post-round scoreboard. It walks player ids 0..3 and
    /// writes each opponent into enemyName/enemyScore/enemyPlusOne at index
    /// <c>(playerId &lt; i) ? i - 1 : i</c>. Those arrays only have three slots, so a local player
    /// whose own id is 4 or higher lands on index 3 and the coroutine dies with an
    /// IndexOutOfRangeException halfway through - leaving PauseManager.onEndRoundScreen stuck at
    /// true and the scoreboard frozen on screen (the "stuck text" bug).
    ///
    /// Extending the arrays is not enough on its own: the slots are scene objects, so the extra
    /// players have nowhere to be drawn. Instead we skip straight to the loading screen for the
    /// players who would crash, which keeps the round flowing for them and leaves the scoreboard
    /// intact for everyone it does fit.
    /// </summary>
    [HarmonyPatch(typeof(RoundManager), "InterfaceSetup")]
    static class RoundEndInterfacePatch
    {
        static readonly AccessTools.FieldRef<RoundManager, GameObject[]> EnemyPlusOne =
            AccessTools.FieldRefAccess<RoundManager, GameObject[]>("enemyPlusOne");
        static readonly AccessTools.FieldRef<RoundManager, TextMeshProUGUI[]> EnemyName =
            AccessTools.FieldRefAccess<RoundManager, TextMeshProUGUI[]>("enemyName");
        static readonly AccessTools.FieldRef<RoundManager, TextMeshProUGUI[]> EnemyScore =
            AccessTools.FieldRefAccess<RoundManager, TextMeshProUGUI[]>("enemyScore");

        static bool loggedOverflow;

        static bool Prefix(RoundManager __instance, int playerId, ref IEnumerator __result)
        {
            if (ModState.QuickLoad)
            {
                __result = LoadingScreenOnly();
                return false;
            }

            if (FitsOnScoreboard(__instance, playerId)) return true;

            if (!loggedOverflow)
            {
                loggedOverflow = true;
                Log.Warn($"player {playerId} does not fit the post-round scoreboard; showing the loading screen instead");
            }
            __result = LoadingScreenOnly();
            return false;
        }

        /// <summary>
        /// Highest enemy slot the vanilla coroutine will touch for this player, compared against
        /// the smallest of the three slot arrays. Reading the real lengths means the guard relaxes
        /// on its own if the game ever ships more slots.
        /// </summary>
        static bool FitsOnScoreboard(RoundManager roundManager, int playerId)
        {
            try
            {
                int highestSlot = playerId >= 4 ? 3 : 2;
                int slots = Math.Min(EnemyName(roundManager).Length,
                    Math.Min(EnemyScore(roundManager).Length, EnemyPlusOne(roundManager).Length));
                return highestSlot < slots;
            }
            catch (Exception e)
            {
                Log.Error($"RoundEndInterfacePatch: {e}");
                return false;
            }
        }

        static IEnumerator LoadingScreenOnly()
        {
            if (SceneMotor.Instance != null) SceneMotor.Instance.ShowLoadingScreen();
            yield break;
        }
    }
}
