using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace moreStrafts.Patches
{
    // Vanilla round spawns use CurrentSpawnPoints[(take + PlayerId) % count]. Maps have ~4
    // four-player spawns (some only the two 1v1 spawns), and PlayerIds go sparse when people
    // leave, so with more players several land on the exact same point. Instead, give each
    // connected player a distinct slot by rank, borrow 1v1 spawns when the 4-player set is too
    // small, and nudge anyone who still has to share a point off to the side.
    // Runs on the host (spawning is server-side).
    [HarmonyPatch(typeof(PlayerManager), "SpawnPlayer", typeof(int), typeof(int))]
    static class RoundSpawnPatch
    {
        static readonly AccessTools.FieldRef<PlayerManager, ClientInstance> ClientScript =
            AccessTools.FieldRefAccess<PlayerManager, ClientInstance>("ClientScript");
        static readonly MethodInfo SpawnAt = AccessTools.Method(typeof(PlayerManager), "SpawnPlayer",
            new[] { typeof(int), typeof(int), typeof(Vector3), typeof(Quaternion) });

        const float MinSpawnSeparation = 2.5f;
        const float ShareOffset = 1.5f;

        static readonly Vector3[] OffsetDirections =
        {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            new Vector3(1f, 0f, 1f).normalized, new Vector3(-1f, 0f, 1f).normalized,
            new Vector3(1f, 0f, -1f).normalized, new Vector3(-1f, 0f, -1f).normalized,
        };

        static bool Prefix(PlayerManager __instance, int suitIndex, int cigIndex)
        {
            Vector3 position;
            Quaternion rotation;
            try
            {
                if (SteamLobby.Instance == null || SteamLobby.Instance.players.Count <= 2) return true;
                var client = ClientScript(__instance);
                if (client == null || client.nonSteamworksTransport || ScoreManager.Instance == null) return true;

                var playerIds = ClientInstance.playerInstances.Keys.OrderBy(id => id).ToList();
                var fourPlayer = Collect("Spawnpoints4Player");
                if (playerIds.Count <= 4 && fourPlayer.Count >= 4 && IsBalanced2v2()) return true;

                var pool = BuildPool(fourPlayer, Collect("Spawnpoints"), playerIds.Count);
                if (pool.Count == 0) return true;

                int rank = playerIds.IndexOf(client.PlayerId);
                if (rank < 0) rank = playerIds.Count;
                int slot = Mod(ScoreManager.Instance.sync___get_value_TakeIndex() + rank, pool.Count);
                int lap = rank / pool.Count;

                var point = pool[slot].transform;
                position = lap == 0 ? point.position : Nudge(point.position, lap);
                rotation = Quaternion.Euler(0f, point.eulerAngles.y, 0f);
                Log.Info($"spawn player {client.PlayerId} (rank {rank}/{playerIds.Count}) at slot {slot}/{pool.Count}, lap {lap}");
            }
            catch (Exception e)
            {
                Log.Error($"RoundSpawnPatch, using vanilla spawn: {e}");
                return true;
            }

            SpawnAt.Invoke(__instance, new object[] { suitIndex, cigIndex, position, rotation });
            return false;
        }

        static List<SpawnPoint> Collect(string tag)
        {
            var root = GameObject.FindGameObjectWithTag(tag);
            if (root == null) return new List<SpawnPoint>();
            var all = root.GetComponentsInChildren<SpawnPoint>(true);
            var active = all.Where(p => p.gameObject.activeInHierarchy).ToList();
            return active.Count > 0 ? active : all.ToList();
        }

        static List<SpawnPoint> BuildPool(List<SpawnPoint> fourPlayer, List<SpawnPoint> oneVsOne, int players)
        {
            var pool = new List<SpawnPoint>(fourPlayer);
            if (pool.Count >= players) return pool;
            foreach (var point in oneVsOne)
            {
                var p = point.transform.position;
                if (pool.All(other => Vector3.Distance(other.transform.position, p) >= MinSpawnSeparation))
                    pool.Add(point);
            }
            return pool;
        }

        static bool IsBalanced2v2()
        {
            var teams = GameManager.Instance.GetConnectedTeams();
            return teams.Length == 2
                && ScoreManager.Instance.GetPlayerIdsForTeam(teams[0]).Count == 2
                && ScoreManager.Instance.GetPlayerIdsForTeam(teams[1]).Count == 2;
        }

        // Find open ground next to a shared spawn point: no wall in the way, floor underneath.
        static Vector3 Nudge(Vector3 spawn, int lap)
        {
            var origin = spawn + Vector3.up;
            float distance = ShareOffset * ((lap + 1) / 2);
            for (int i = 0; i < OffsetDirections.Length; i++)
            {
                var dir = OffsetDirections[(lap - 1 + i) % OffsetDirections.Length];
                if (Physics.Raycast(origin, dir, distance + 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var above = origin + dir * distance;
                if (Physics.Raycast(above, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                    return hit.point;
            }
            return spawn;
        }

        static int Mod(int value, int n) => ((value % n) + n) % n;
    }
}
