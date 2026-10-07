using System;
using System.Collections;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace moreStrafts.Patches
{
    // The post-round screen has 3 enemy slots, but InterfaceSetup indexes them with
    // (playerId < i ? i - 1 : i) for i in 0..3. For a local PlayerId >= 4 that reaches
    // slot 3, throws, and kills the coroutine before it hides the screen, so it stays
    // up into the next round. Pad the arrays with a hidden 4th slot so it never throws.
    [HarmonyPatch(typeof(RoundManager), "NextRoundCall")]
    static class RoundScreenSlotsPatch
    {
        static readonly AccessTools.FieldRef<RoundManager, TextMeshProUGUI[]> EnemyName =
            AccessTools.FieldRefAccess<RoundManager, TextMeshProUGUI[]>("enemyName");
        static readonly AccessTools.FieldRef<RoundManager, TextMeshProUGUI[]> EnemyScore =
            AccessTools.FieldRefAccess<RoundManager, TextMeshProUGUI[]>("enemyScore");
        static readonly AccessTools.FieldRef<RoundManager, GameObject[]> EnemyPlusOne =
            AccessTools.FieldRefAccess<RoundManager, GameObject[]>("enemyPlusOne");

        const int Slots = 4;

        static void Prefix(RoundManager __instance)
        {
            try
            {
                ref var names = ref EnemyName(__instance);
                ref var scores = ref EnemyScore(__instance);
                ref var plusOnes = ref EnemyPlusOne(__instance);
                if (names.Length >= Slots && scores.Length >= Slots && plusOnes.Length >= Slots) return;

                names = Pad(names, () => HiddenText(__instance, "moreStrafts_EnemyName"));
                scores = Pad(scores, () => HiddenText(__instance, "moreStrafts_EnemyScore"));
                plusOnes = Pad(plusOnes, () => HiddenObject(__instance, "moreStrafts_EnemyPlusOne"));
                Log.Info($"round-end enemy slots padded to {Slots}");
            }
            catch (Exception e)
            {
                Log.Error($"RoundScreenSlotsPatch: {e}");
            }
        }

        static T[] Pad<T>(T[] array, Func<T> make)
        {
            if (array.Length >= Slots) return array;
            var padded = new T[Slots];
            Array.Copy(array, padded, array.Length);
            for (int i = array.Length; i < Slots; i++) padded[i] = make();
            return padded;
        }

        static GameObject HiddenObject(RoundManager owner, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(owner.transform, false);
            go.SetActive(false);
            return go;
        }

        static TextMeshProUGUI HiddenText(RoundManager owner, string name)
        {
            var go = HiddenObject(owner, name);
            return go.AddComponent<TextMeshProUGUI>();
        }
    }

    // Safety net: if InterfaceSetup still throws for any other reason, hide the screen
    // instead of leaving it stuck over the next round.
    [HarmonyPatch(typeof(RoundManager), "InterfaceSetup")]
    static class RoundScreenGuardPatch
    {
        static void Postfix(RoundManager __instance, ref IEnumerator __result)
        {
            __result = Guard(__instance, __result);
        }

        static IEnumerator Guard(RoundManager roundManager, IEnumerator inner)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!inner.MoveNext()) yield break;
                    current = inner.Current;
                }
                catch (Exception e)
                {
                    Log.Error($"post-round screen failed, hiding it: {e}");
                    break;
                }
                yield return current;
            }

            try
            {
                roundManager.StopCoroutine();
            }
            catch (Exception e)
            {
                Log.Error($"post-round screen cleanup: {e}");
                var container = GameObject.Find("--END ROUND SCREEN--");
                if (container != null) container.transform.localScale = Vector3.zero;
            }
            if (SceneMotor.Instance != null) SceneMotor.Instance.ShowLoadingScreen();
            if (PauseManager.Instance != null) PauseManager.Instance.onEndRoundScreen = false;
        }
    }
}
