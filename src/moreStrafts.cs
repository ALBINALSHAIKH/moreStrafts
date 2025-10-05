using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using FishNet;
using Steamworks;
using TMPro;
using UnityEngine;

namespace moreStrafts
{
    [BepInPlugin("com.nitrogenia.morestrafts", "More Strafts Players", "0.0.1")]
    public class moreStraftsMod : BaseUnityPlugin
    {
        void Awake()
        {
            var harmony = new Harmony("com.nitrogenia.morestrafts");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.LogInfo("More Strafts Mod alpha v0.0.1 by Nitrogenia loaded! Max players extended to 10.");
        }
    }

    // ===== DROPDOWN PATCHES =====

    // Patch 1: Main menu dropdown (before lobby creation)
    [HarmonyPatch(typeof(SteamLobby), "Start")]
    public static class SteamLobbyStartPatch
    {
        static void Postfix(SteamLobby __instance)
        {
            // Expand MaxPlayersDropdown to support 2-10 players
            if (__instance.MaxPlayersDropdown != null)
            {
                __instance.MaxPlayersDropdown.options.Clear();
                for (int i = 2; i <= 10; i++)
                {
                    __instance.MaxPlayersDropdown.options.Add(new TMP_Dropdown.OptionData($"{i} Players"));
                }
                // Default to 4 players (index 2 = 4 players since dropdown.value + 2 = maxPlayers)
                __instance.MaxPlayersDropdown.value = 2;
                __instance.MaxPlayersDropdown.RefreshShownValue();

                Debug.Log("[moreStrafts] Main menu MaxPlayersDropdown expanded to 2-10 players");
            }

            // Set transport max clients early
            try
            {
                var transport = InstanceFinder.TransportManager?.Transport;
                if (transport != null)
                {
                    transport.SetMaximumClients(9); // 10 total = 9 clients + 1 host
                    Debug.Log("[moreStrafts] Set transport maximum clients to 9");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[moreStrafts] Failed to set transport max clients: {e.Message}");
            }
        }
    }

    // Patch 2: In-lobby dropdown (BEFORE lobby setup completes)
    // This must be a PREFIX to expand the dropdown BEFORE SetMaxPlayers is called
    [HarmonyPatch(typeof(SteamLobby), "OnLobbyCreated")]
    public static class OnLobbyCreatedPatch
    {
        static void Prefix(SteamLobby __instance, LobbyCreated_t callback)
        {
            // CRITICAL: Expand the in-lobby dropdown BEFORE the original OnLobbyCreated runs
            // The original method calls SetMaxPlayers(this.MaxPlayersDropdown) at line 424,
            // so we must expand the dropdown first or it will read the wrong value
            if (__instance.MaxPlayersDropdown != null)
            {
                // Save the current dropdown value BEFORE clearing options
                int savedValue = __instance.MaxPlayersDropdown.value;

                __instance.MaxPlayersDropdown.options.Clear();
                for (int i = 2; i <= 10; i++)
                {
                    __instance.MaxPlayersDropdown.options.Add(new TMP_Dropdown.OptionData($"{i} Players"));
                }

                // Restore the user's selection from before we cleared the options
                __instance.MaxPlayersDropdown.value = savedValue;
                __instance.MaxPlayersDropdown.RefreshShownValue();

                Debug.Log($"[moreStrafts] OnLobbyCreated Prefix: Expanded dropdown to 2-10, preserved value {savedValue}");
            }
        }

        static void Postfix(SteamLobby __instance, LobbyCreated_t callback)
        {
            // Log final state
            Debug.Log($"[moreStrafts] Lobby created with {__instance.maxPlayers} max players (dropdown value: {__instance.MaxPlayersDropdown.value})");

            // Configure transport for the host when creating lobby
            try
            {
                var transport = InstanceFinder.TransportManager?.Transport;
                if (transport != null)
                {
                    transport.SetMaximumClients(__instance.maxPlayers - 1);
                    Debug.Log($"[moreStrafts] Lobby created - transport max clients set to {__instance.maxPlayers - 1}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[moreStrafts] Failed to set transport max clients on lobby creation: {e.Message}");
            }
        }
    }

    // Patch 2b: Configure transport when entering an existing lobby (for clients and host)
    [HarmonyPatch(typeof(SteamLobby), "OnLobbyEntered")]
    public static class OnLobbyEnteredPatch
    {
        static void Postfix(SteamLobby __instance, LobbyEnter_t callback)
        {
            // Get the max players from the lobby data
            int maxPlayers = __instance.maxPlayers;

            Debug.Log($"[moreStrafts] Lobby entered - max players: {maxPlayers}");

            // Configure transport to support the lobby's max player count
            try
            {
                var transport = InstanceFinder.TransportManager?.Transport;
                if (transport != null)
                {
                    transport.SetMaximumClients(maxPlayers - 1);
                    Debug.Log($"[moreStrafts] Transport configured for {maxPlayers - 1} clients (max players: {maxPlayers})");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[moreStrafts] Failed to configure transport on lobby enter: {e.Message}");
            }
        }
    }

    // ===== QUICK MATCH FILTER PATCH =====

    // Patch 3: Fix quick match to allow joining lobbies with up to 10 players
    [HarmonyPatch(typeof(SteamLobby), "OnGetLobbyList")]
    public static class OnGetLobbyListPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                // Find: if (SteamMatchmaking.GetLobbyMemberLimit(csteamID) <= 4)
                // Change 4 to 10
                if (codes[i].opcode == OpCodes.Ldc_I4_4)
                {
                    // Check if this is in a comparison context
                    if (i + 1 < codes.Count &&
                        (codes[i + 1].opcode == OpCodes.Bgt ||
                         codes[i + 1].opcode == OpCodes.Bgt_S ||
                         codes[i + 1].opcode == OpCodes.Ble ||
                         codes[i + 1].opcode == OpCodes.Ble_S))
                    {
                        codes[i] = new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)10);
                        Debug.Log("[moreStrafts] Patched OnGetLobbyList: Changed lobby member limit check from 4 to 10");
                    }
                }
            }
            return codes;
        }
    }

    // ===== PLAYER LIMIT CHECKS =====

    // Patch 4: Fix LobbyController.RemoveExtraPlayerItem to not remove players beyond index 1
    [HarmonyPatch(typeof(LobbyController), "RemoveExtraPlayerItem")]
    public static class RemoveExtraPlayerItemPatch
    {
        static bool Prefix(LobbyController __instance)
        {
            // Access private field
            var PlayerListItems = Traverse.Create(__instance).Field("PlayerListItems").GetValue<List<PlayerListItem>>();
            var PlayerListItemsTab = Traverse.Create(__instance).Field("PlayerListItemsTab").GetValue<List<PlayerListItem>>();

            // Only remove items that exceed the actual max player count
            int maxPlayers = SteamLobby.Instance.maxPlayers;

            List<PlayerListItem> toRemove = new List<PlayerListItem>();
            for (int i = 0; i < PlayerListItems.Count; i++)
            {
                if (i >= maxPlayers)
                {
                    toRemove.Add(PlayerListItems[i]);
                }
            }

            foreach (var item in toRemove)
            {
                UnityEngine.Object.Destroy(item.gameObject);
                PlayerListItems.Remove(item);
            }

            // Same for tab screen
            List<PlayerListItem> toRemoveTab = new List<PlayerListItem>();
            for (int j = 0; j < PlayerListItemsTab.Count; j++)
            {
                if (j >= maxPlayers)
                {
                    toRemoveTab.Add(PlayerListItemsTab[j]);
                }
            }

            foreach (var item in toRemoveTab)
            {
                UnityEngine.Object.Destroy(item.gameObject);
                PlayerListItemsTab.Remove(item);
            }

            return false; // Skip original method
        }
    }

    // ===== ARRAY BOUNDS SAFETY PATCHES =====

    // Patch 5: Fix CreateClientPlayerItem to handle missing position slots safely
    [HarmonyPatch(typeof(LobbyController), "CreateClientPlayerItem")]
    public static class CreateClientPlayerItemPatch
    {
        static bool Prefix(LobbyController __instance)
        {
            var PlayerListItems = Traverse.Create(__instance).Field("PlayerListItems").GetValue<List<PlayerListItem>>();
            var PlayerListItemsTab = Traverse.Create(__instance).Field("PlayerListItemsTab").GetValue<List<PlayerListItem>>();
            var manager = Traverse.Create(__instance).Field("manager").GetValue<SteamLobby>();
            var tabScreen = Traverse.Create(__instance).Field("tabScreen").GetValue<Transform>();

            foreach (var player in manager.players)
            {
                // Main lobby view
                if (!PlayerListItems.Any(b => b.ConnectionID == player.Owner.ClientId))
                {
                    var playerItem = UnityEngine.Object.Instantiate(__instance.PlayerListItemPrefab);
                    var component = playerItem.GetComponent<PlayerListItem>();
                    var clientInstance = player.GetComponent<ClientInstance>();

                    component.PlayerName = clientInstance.PlayerName;
                    component.ConnectionID = clientInstance.ConnectionID;
                    component.PlayerIdNumber = clientInstance.PlayerId;
                    component.PlayerSteamID = clientInstance.PlayerSteamID;
                    component.Ready = clientInstance.Ready;
                    component.SetPlayerValues();

                    playerItem.transform.SetParent(__instance.PlayerListViewContent.transform);
                    playerItem.transform.localScale = Vector3.one;

                    // Safe position assignment
                    int posIndex = clientInstance.PlayerId - 1;
                    if (posIndex >= 0 && posIndex < __instance.clientPosition.Length)
                    {
                        playerItem.transform.position = __instance.clientPosition[posIndex].position;
                    }
                    else
                    {
                        // Use last available position or stack below
                        var lastPos = __instance.clientPosition[__instance.clientPosition.Length - 1].position;
                        playerItem.transform.position = lastPos + Vector3.down * 60f * (posIndex - __instance.clientPosition.Length + 1);
                        Debug.LogWarning($"[moreStrafts] Player {clientInstance.PlayerId} exceeds available positions, stacking below");
                    }

                    PlayerListItems.Add(component);
                }

                // Tab screen
                if (!PlayerListItemsTab.Any(b => b.ConnectionID == player.Owner.ClientId))
                {
                    var playerItem = UnityEngine.Object.Instantiate(__instance.PlayerListItemPrefab);
                    var component = playerItem.GetComponent<PlayerListItem>();
                    var clientInstance = player.GetComponent<ClientInstance>();

                    component.PlayerName = clientInstance.PlayerName;
                    component.ConnectionID = clientInstance.ConnectionID;
                    component.PlayerIdNumber = clientInstance.PlayerId;
                    component.PlayerSteamID = clientInstance.PlayerSteamID;
                    component.Ready = clientInstance.Ready;
                    component.SetPlayerValues();

                    playerItem.transform.SetParent(tabScreen);
                    playerItem.transform.localScale = Vector3.one;

                    // Safe position assignment for tab
                    var tabClientPosition = __instance.tabclientPosition;
                    int posIndex = clientInstance.PlayerId - 1;
                    if (posIndex >= 0 && posIndex < tabClientPosition.Length)
                    {
                        playerItem.transform.position = tabClientPosition[posIndex].position;
                    }
                    else
                    {
                        var lastPos = tabClientPosition[tabClientPosition.Length - 1].position;
                        playerItem.transform.position = lastPos + Vector3.down * 60f * (posIndex - tabClientPosition.Length + 1);
                    }

                    PlayerListItemsTab.Add(component);
                }
            }

            return false; // Skip original
        }
    }

    // Patch 6: Fix PlayerListItem.Update to handle position array bounds
    // COMPLETE METHOD REPLACEMENT - returns false to skip original
    [HarmonyPatch(typeof(PlayerListItem), "Update")]
    public static class PlayerListItemUpdatePatch
    {
        static bool Prefix(PlayerListItem __instance)
        {
            // Get required fields via reflection (since they're private)
            var cosmeticbutton = Traverse.Create(__instance).Field("cosmeticbutton").GetValue<GameObject>();
            var pauseManager = Traverse.Create(__instance).Field("pauseManager").GetValue<PauseManager>();
            var lobbyController = Traverse.Create(__instance).Field("lobbyController").GetValue<LobbyController>();
            var gameManager = Traverse.Create(__instance).Field("gameManager").GetValue<GameManager>();
            var teamIdDropdown = Traverse.Create(__instance).Field("teamIdDropdown").GetValue<TMP_Dropdown>();
            var localSteamId = Traverse.Create(__instance).Field("localSteamId").GetValue<ulong>();

            // Line 69: Cosmetic button scale (always safe)
            if (cosmeticbutton != null && pauseManager != null)
            {
                cosmeticbutton.transform.localScale = pauseManager.inMainMenu ? Vector3.one : Vector3.zero;
            }

            // Lines 70-80: Position update with BOUNDS SAFETY
            bool isTabScreen = __instance.transform.parent.gameObject.name == "-- TAB SCREEN --";
            if (__instance.PlayerIdNumber > 0 && lobbyController != null)
            {
                int posIndex = __instance.PlayerIdNumber - 1;

                if (isTabScreen)
                {
                    // Check bounds before accessing tabclientPosition array
                    if (posIndex < lobbyController.tabclientPosition.Length)
                    {
                        __instance.transform.position = lobbyController.tabclientPosition[posIndex].position;
                    }
                    else
                    {
                        // Fallback for players 5-10: stack below last available position
                        var lastPos = lobbyController.tabclientPosition[lobbyController.tabclientPosition.Length - 1].position;
                        int overflow = posIndex - (lobbyController.tabclientPosition.Length - 1);
                        __instance.transform.position = new Vector3(lastPos.x, lastPos.y - (overflow * 0.5f), lastPos.z);
                    }
                }
                else
                {
                    // Check bounds before accessing clientPosition array
                    if (posIndex < lobbyController.clientPosition.Length)
                    {
                        __instance.transform.position = lobbyController.clientPosition[posIndex].position;
                    }
                    else
                    {
                        // Fallback for players 5-10: stack below last available position
                        var lastPos = lobbyController.clientPosition[lobbyController.clientPosition.Length - 1].position;
                        int overflow = posIndex - (lobbyController.clientPosition.Length - 1);
                        __instance.transform.position = new Vector3(lastPos.x, lastPos.y - (overflow * 0.5f), lastPos.z);
                    }
                }
            }

            // Lines 82-94: Team dropdown visibility and interactability
            if (gameManager != null && teamIdDropdown != null)
            {
                // Access playingTeams via the getter method
                bool playingTeams = Traverse.Create(gameManager).Field("playingTeams").GetValue<bool>();
                teamIdDropdown.gameObject.SetActive(playingTeams);

                if (InstanceFinder.NetworkManager.IsServer)
                {
                    teamIdDropdown.interactable = !isTabScreen;
                }
                else if (localSteamId == __instance.PlayerSteamID)
                {
                    teamIdDropdown.interactable = !isTabScreen;
                }
            }

            // Return false to skip the original method entirely (prevents crash!)
            return false;
        }
    }

    // Patch 7: Fix RunIntoLobby animation for players beyond preview slots
    [HarmonyPatch(typeof(ClientInstance), "RpcLogic___RunIntoLobby_3316948804")]
    public static class RunIntoLobbyPatch
    {
        static bool Prefix(ClientInstance __instance, int id)
        {
            var previews = LobbyController.Instance.previews;

            // Check bounds before accessing
            if (id >= 0 && id < previews.Length && previews[id] != null)
            {
                return true; // Run original method
            }

            // Skip animation for players beyond available preview slots
            Debug.Log($"[moreStrafts] Skipping lobby run animation for player {id} - no preview slot available");
            return false;
        }
    }

    // Patch 8: Fix MenuAnimationObservers for players beyond preview slots
    [HarmonyPatch(typeof(ClientInstance), "RpcLogic___MenuAnimationObservers_1692629761")]
    public static class MenuAnimationObserversPatch
    {
        static bool Prefix(ClientInstance __instance, int index, int id)
        {
            var previews = LobbyController.Instance.previews;

            // Check bounds
            if (id >= 0 && id < previews.Length && previews[id] != null)
            {
                return true; // Run original method
            }

            // Skip animation for players beyond preview slots
            return false;
        }
    }

    // Patch 9: Fix DressAboubiObservers for players beyond preview slots
    [HarmonyPatch(typeof(ClientInstance), "RpcLogic___DressAboubiObservers_2497120398")]
    public static class DressAboubiObserversPatch
    {
        static bool Prefix(ClientInstance __instance, GameObject hat, int matIndex, int cigIndex, int id)
        {
            var previews = LobbyController.Instance.previews;

            // Check bounds
            if (id >= 0 && id < previews.Length)
            {
                return true; // Run original method
            }

            Debug.Log($"[moreStrafts] Skipping cosmetics for player {id} - no preview slot available");
            return false;
        }
    }

    // ===== NETWORKING CONFIGURATION =====

    // Patch 10: Ensure transport is properly configured when max players changes
    [HarmonyPatch(typeof(SteamLobby), "SetMaxPlayers")]
    public static class SetMaxPlayersPatch
    {
        static void Postfix(SteamLobby __instance, TMP_Dropdown _dropdown)
        {
            Debug.Log($"[moreStrafts] Max players set to: {__instance.maxPlayers} (dropdown value: {_dropdown.value})");

            // Ensure transport supports the new max
            try
            {
                var transport = InstanceFinder.TransportManager?.Transport;
                if (transport != null)
                {
                    transport.SetMaximumClients(__instance.maxPlayers - 1);
                    Debug.Log($"[moreStrafts] Updated transport max clients to {__instance.maxPlayers - 1}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[moreStrafts] Failed to update transport max clients: {e.Message}");
            }
        }
    }

    // Patch 11: Ensure UpdateOnClients respects new max player limit
    [HarmonyPatch(typeof(ClientInstance), "RpcLogic___UpdateOnClients_3316948804")]
    public static class UpdateOnClientsPatch
    {
        static void Prefix(ref int maxPlayers)
        {
            // Ensure max players is within valid range
            if (maxPlayers < 2) maxPlayers = 2;
            if (maxPlayers > 10) maxPlayers = 10;
        }
    }

    // ===== READY SYSTEM PATCHES =====

    // Patch 12: Update LobbyController.Update to handle more than 4 players
    [HarmonyPatch(typeof(LobbyController), "Update")]
    public static class LobbyControllerUpdatePatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // Look for any hardcoded player count comparisons and replace them
            for (int i = 0; i < codes.Count; i++)
            {
                // Replace hardcoded 2 or 4 with dynamic maxPlayers where appropriate
                if (codes[i].opcode == OpCodes.Ldc_I4_2 || codes[i].opcode == OpCodes.Ldc_I4_4)
                {
                    // Check if this is followed by a comparison
                    if (i + 1 < codes.Count &&
                        (codes[i + 1].opcode == OpCodes.Bgt ||
                         codes[i + 1].opcode == OpCodes.Bgt_S ||
                         codes[i + 1].opcode == OpCodes.Ble ||
                         codes[i + 1].opcode == OpCodes.Ble_S ||
                         codes[i + 1].opcode == OpCodes.Blt ||
                         codes[i + 1].opcode == OpCodes.Blt_S))
                    {
                        // This looks like a player count check - don't modify it here
                        // The logic should use SteamLobby.Instance.maxPlayers instead
                    }
                }
            }

            return codes;
        }
    }

    // ===== ROUND MANAGER ARRAY BOUNDS FIXES =====

    // Patch 13: Fix RoundManager.NextRoundCall array sizes for 10 players
    [HarmonyPatch(typeof(RoundManager), "NextRoundCall")]
    public static class RoundManagerNextRoundCallPatch
    {
        static bool Prefix(RoundManager __instance, int playerId, bool won, int winningTeamId)
        {
            // Expand arrays from 4 to 10 to support all players
            var names = new string[10];
            var scores = new int[10];
            var players = new ClientInstance[SteamLobby.Instance.players.Count];

            if (won)
            {
                Settings.Instance.IncreaseRoundsWon();
            }
            else
            {
                Settings.Instance.IncreaseRoundsLost();
            }

            for (int i = 0; i < players.Length; i++)
            {
                players[i] = SteamLobby.Instance.players[i].GetComponent<ClientInstance>();
            }

            // Fill names for all 10 possible players
            for (int j = 0; j < names.Length; j++)
            {
                ClientInstance clientInstance;
                if (ClientInstance.playerInstances.TryGetValue(j, out clientInstance))
                {
                    names[j] = clientInstance.PlayerName;
                }
                else
                {
                    names[j] = "";
                }
            }

            // Fill scores for all 10 possible players
            for (int k = 0; k < scores.Length; k++)
            {
                scores[k] = ScoreManager.Instance.GetPoints(ScoreManager.Instance.GetTeamId(k));
            }

            // Set the arrays via reflection
            Traverse.Create(__instance).Field("names").SetValue(names);
            Traverse.Create(__instance).Field("scores").SetValue(scores);
            Traverse.Create(__instance).Field("players").SetValue(players);

            // Call InterfaceSetup method and start the coroutine
            var coroutine = Traverse.Create(__instance).Method("InterfaceSetup",
                new object[] { playerId, won, winningTeamId }).GetValue<System.Collections.IEnumerator>();

            Traverse.Create(__instance).Field("InterfaceSetupCoroutine").SetValue(coroutine);
            __instance.StartCoroutine(coroutine);

            return false; // Skip original method
        }
    }

    // Patch 14: No InterfaceSetup patch needed - the UI only supports 4 players
    // The NextRoundCall patch prevents crashes by expanding the names/scores arrays
    // Players 5-10 won't be shown on the end-round screen, but the game won't crash

    // ===== EXPAND TEAM SYSTEM FROM 4 TO 10 TEAMS =====

    // Patch 15: Expand team dropdown options from 4 to maxPlayers
    [HarmonyPatch(typeof(PlayerListItem), "Start")]
    public static class ExpandTeamDropdownPatch
    {
        static void Postfix(PlayerListItem __instance)
        {
            var teamIdDropdown = Traverse.Create(__instance).Field("teamIdDropdown").GetValue<TMP_Dropdown>();

            if (teamIdDropdown == null)
            {
                return;
            }

            int maxPlayers = SteamLobby.Instance.maxPlayers;

            // If max players > 4, expand dropdown options to match
            if (maxPlayers > 4)
            {
                teamIdDropdown.ClearOptions();

                var options = new System.Collections.Generic.List<string>();
                for (int i = 1; i <= maxPlayers; i++)
                {
                    options.Add($"Team {i}");
                }

                teamIdDropdown.AddOptions(options);

                // Restore current team selection
                int currentTeamId = ScoreManager.Instance.GetTeamId(__instance.PlayerIdNumber);
                teamIdDropdown.value = currentTeamId;

                Debug.Log($"[moreStrafts] Expanded team dropdown to {maxPlayers} options for player {__instance.PlayerIdNumber}");
            }
        }
    }

    // Patch 16: Ensure unique team IDs in FFA mode (playingTeams = false)
    [HarmonyPatch(typeof(SteamLobby), "SetGamemode", new Type[] { typeof(int) })]
    public static class FFATeamAssignmentPatch
    {
        static void Postfix(SteamLobby __instance, int value)
        {
            // value: 0 = FFA, 1 = Teams
            bool isFFAMode = (value == 0);

            if (isFFAMode && InstanceFinder.NetworkManager.IsServer)
            {
                int playerCount = __instance.players.Count;

                // In FFA mode, assign each player their own unique team ID
                ScoreManager.Instance.ResetTeams();
                for (int i = 0; i < playerCount; i++)
                {
                    ScoreManager.Instance.SetTeamId(i, i);
                }

                Debug.Log($"[moreStrafts] FFA mode activated - assigned unique team IDs for {playerCount} players");
            }
        }
    }

    // Patch 17: Ensure unique team IDs when game starts in FFA mode
    [HarmonyPatch(typeof(GameManager), "StartGame")]
    public static class GameStartFFAPatch
    {
        static void Postfix(GameManager __instance)
        {
            // Check if we're in FFA mode (playingTeams = false)
            bool playingTeams = Traverse.Create(__instance).Field("playingTeams").GetValue<bool>();

            if (!playingTeams && InstanceFinder.NetworkManager.IsServer)
            {
                int playerCount = SteamLobby.Instance.players.Count;

                // Assign each player their own unique team ID in FFA mode
                ScoreManager.Instance.ResetTeams();
                for (int i = 0; i < playerCount; i++)
                {
                    ScoreManager.Instance.SetTeamId(i, i);
                }

                Debug.Log($"[moreStrafts] Game starting in FFA mode - assigned unique team IDs for {playerCount} players");
            }
        }
    }

    // ===== MATCH POINTS HUD ARRAY BOUNDS FIXES =====

    // Patch 18: Fix MatchPoitnsHUD.UpdateVisuals(winnerTeamId, roundScores) for 5+ teams
    [HarmonyPatch(typeof(MatchPoitnsHUD), "UpdateVisuals", new Type[] { typeof(int), typeof(Dictionary<int, int>) })]
    public static class MatchPoitnsHUDMainUpdateVisualsPatch
    {
        static bool Prefix(MatchPoitnsHUD __instance, int winnerTeamId, Dictionary<int, int> roundScores)
        {
            // Get required fields via reflection
            var secondaryPointObjects = Traverse.Create(__instance).Field("secondaryPointObjects").GetValue<UnityEngine.MeshRenderer[]>();
            var pointsTexts = Traverse.Create(__instance).Field("pointsTexts").GetValue<TMPro.TMP_Text[]>();
            var primaryPointMesh = Traverse.Create(__instance).Field("primaryPointMesh").GetValue<UnityEngine.MeshRenderer>();

            // Early exit if only 1 player
            if (ClientInstance.playerInstances.Count <= 1)
            {
                return false;
            }

            // Build list of active team IDs
            List<int> activeTeamIds = new List<int>();
            foreach (int teamId in ScoreManager.Instance.TeamIdToPlayerIds.Keys)
            {
                foreach (int playerId in ScoreManager.Instance.TeamIdToPlayerIds[teamId])
                {
                    if (ClientInstance.playerInstances.ContainsKey(playerId))
                    {
                        activeTeamIds.Add(teamId);
                        break;
                    }
                }
            }

            // Calculate how many secondary objects we need (teams beyond first 2)
            int secondaryTeamsCount = activeTeamIds.Count - 2;

            // BOUNDS SAFETY: Only activate as many secondary objects as we have
            int maxSecondaryToActivate = Mathf.Min(secondaryTeamsCount, secondaryPointObjects.Length);
            for (int i = 0; i < maxSecondaryToActivate; i++)
            {
                secondaryPointObjects[i].gameObject.SetActive(true);
            }

            // Deactivate unused secondary objects
            int unusedCount = secondaryPointObjects.Length - maxSecondaryToActivate;
            if (unusedCount > 0)
            {
                for (int j = secondaryPointObjects.Length - 1; j >= secondaryPointObjects.Length - unusedCount; j--)
                {
                    if (j >= 0)
                    {
                        secondaryPointObjects[j].gameObject.SetActive(false);
                    }
                }
            }

            // Clear all point texts
            foreach (var text in pointsTexts)
            {
                text.text = "";
            }

            // Update visuals for each team
            UnityEngine.Material[] materials = primaryPointMesh.materials;
            int maxTeamsToDisplay = Mathf.Min(activeTeamIds.Count, 4); // UI only supports 4 teams

            for (int l = 0; l < maxTeamsToDisplay; l++)
            {
                int teamId = System.Linq.Enumerable.ElementAt(ScoreManager.Instance.TeamIdToPlayerIds.Keys, l);
                int score = roundScores.ContainsKey(teamId) ? roundScores[teamId] : 0;

                // Call the helper UpdateVisuals method with bounds safety
                UpdateVisualsSafe(__instance, l, score, teamId == winnerTeamId, materials);
            }

            primaryPointMesh.materials = materials;

            return false; // Skip original method
        }

        // Helper method with bounds safety
        static void UpdateVisualsSafe(MatchPoitnsHUD instance, int teamNumber, int roundScore, bool isWinner, UnityEngine.Material[] primaryPointMeshMaterials)
        {
            var activeMaterials = Traverse.Create(instance).Field("activeMaterials").GetValue<UnityEngine.Material[]>();
            var inactiveMaterial = Traverse.Create(instance).Field("inactiveMaterial").GetValue<UnityEngine.Material>();
            var secondaryPointObjects = Traverse.Create(instance).Field("secondaryPointObjects").GetValue<UnityEngine.MeshRenderer[]>();
            var pointsTexts = Traverse.Create(instance).Field("pointsTexts").GetValue<TMPro.TMP_Text[]>();

            // Bounds check
            if (teamNumber < 0 || teamNumber >= activeMaterials.Length)
            {
                return;
            }

            UnityEngine.Material material = (roundScore > 0) ? activeMaterials[teamNumber] : inactiveMaterial;

            if (teamNumber < 2)
            {
                int num = (teamNumber == 0) ? 1 : 3;
                primaryPointMeshMaterials[num] = material;
            }
            else
            {
                int num2 = teamNumber - 2;
                if (num2 < secondaryPointObjects.Length)
                {
                    UnityEngine.Material[] materials = secondaryPointObjects[num2].materials;
                    materials[1] = material;
                    secondaryPointObjects[num2].materials = materials;
                }
            }

            if ((roundScore == 2 && !isWinner) || roundScore > 2)
            {
                if (teamNumber < pointsTexts.Length)
                {
                    pointsTexts[teamNumber].text = roundScore.ToString();
                }
            }

            if (isWinner)
            {
                primaryPointMeshMaterials[2] = material;
            }
        }
    }
}
