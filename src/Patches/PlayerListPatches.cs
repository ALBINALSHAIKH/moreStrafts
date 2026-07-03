using System;
using System.Collections.Generic;
using FishNet;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace moreStrafts.Patches
{
    static class GameFields
    {
        public static readonly AccessTools.FieldRef<PlayerListItem, GameObject> CosmeticButton =
            AccessTools.FieldRefAccess<PlayerListItem, GameObject>("cosmeticbutton");
        public static readonly AccessTools.FieldRef<PlayerListItem, PauseManager> PauseManager =
            AccessTools.FieldRefAccess<PlayerListItem, PauseManager>("pauseManager");
        public static readonly AccessTools.FieldRef<PlayerListItem, LobbyController> LobbyController =
            AccessTools.FieldRefAccess<PlayerListItem, LobbyController>("lobbyController");
        public static readonly AccessTools.FieldRef<PlayerListItem, GameManager> GameManager =
            AccessTools.FieldRefAccess<PlayerListItem, GameManager>("gameManager");
        public static readonly AccessTools.FieldRef<PlayerListItem, TMP_Dropdown> TeamDropdown =
            AccessTools.FieldRefAccess<PlayerListItem, TMP_Dropdown>("teamIdDropdown");
        public static readonly AccessTools.FieldRef<PlayerListItem, ulong> LocalSteamId =
            AccessTools.FieldRefAccess<PlayerListItem, ulong>("localSteamId");
        public static readonly AccessTools.FieldRef<GameManager, bool> PlayingTeams =
            AccessTools.FieldRefAccess<GameManager, bool>("playingTeams");
    }

    [HarmonyPatch(typeof(PlayerListItem), "Update")]
    static class PlayerListItemUpdatePatch
    {
        static bool warned;

        static bool Prefix(PlayerListItem __instance)
        {
            try
            {
                var pauseManager = GameFields.PauseManager(__instance);
                var cosmeticButton = GameFields.CosmeticButton(__instance);
                if (cosmeticButton != null && pauseManager != null)
                    cosmeticButton.transform.localScale = pauseManager.inMainMenu ? Vector3.one : Vector3.zero;

                var parent = __instance.transform.parent;
                bool onTabScreen = parent != null && parent.gameObject.name == "-- TAB SCREEN --";

                var lobbyController = GameFields.LobbyController(__instance);
                if (__instance.PlayerIdNumber > 0 && lobbyController != null)
                {
                    var slots = onTabScreen ? lobbyController.tabclientPosition : lobbyController.clientPosition;
                    __instance.transform.position = SlotPosition(slots, __instance.PlayerIdNumber - 1);
                }

                var gameManager = GameFields.GameManager(__instance);
                var teamDropdown = GameFields.TeamDropdown(__instance);
                if (gameManager != null && teamDropdown != null)
                {
                    teamDropdown.gameObject.SetActive(GameFields.PlayingTeams(gameManager));
                    bool isServer = InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.IsServer;
                    bool ownRow = GameFields.LocalSteamId(__instance) == __instance.PlayerSteamID;
                    if (isServer || ownRow) teamDropdown.interactable = !onTabScreen;
                }

                return false;
            }
            catch (Exception e)
            {
                if (!warned)
                {
                    warned = true;
                    Log.Error($"PlayerListItemUpdatePatch: {e}");
                }
                return true;
            }
        }

        static Vector3 SlotPosition(Transform[] slots, int index)
        {
            if (index < slots.Length) return slots[index].position;
            var last = slots[slots.Length - 1].position;
            return new Vector3(last.x, last.y - (index - slots.Length + 1) * 0.5f, last.z);
        }
    }

    [HarmonyPatch(typeof(PlayerListItem), "Start")]
    static class TeamDropdownPatch
    {
        static void Postfix(PlayerListItem __instance)
        {
            try
            {
                if (SteamLobby.Instance == null || SteamLobby.Instance.maxPlayers <= 4) return;
                var dropdown = GameFields.TeamDropdown(__instance);
                if (dropdown == null) return;

                int maxPlayers = SteamLobby.Instance.maxPlayers;
                var options = new List<string>();
                for (int team = 1; team <= maxPlayers; team++) options.Add($"Team {team}");
                dropdown.ClearOptions();
                dropdown.AddOptions(options);
                dropdown.SetValueWithoutNotify(ScoreManager.Instance.GetTeamId(__instance.PlayerIdNumber));
                Log.Info($"team dropdown expanded to {maxPlayers} options for player {__instance.PlayerIdNumber}");
            }
            catch (Exception e)
            {
                Log.Error($"TeamDropdownPatch: {e}");
            }
        }
    }
}
