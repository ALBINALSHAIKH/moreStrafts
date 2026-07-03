using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

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
}
