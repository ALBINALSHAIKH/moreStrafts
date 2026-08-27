using System;
using FishNet;
using HarmonyLib;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace moreStrafts.Patches
{
    [HarmonyPatch(typeof(SteamLobby), "OnGetLobbyData")]
    static class QuickLoadLobbyDataPatch
    {
        static void Postfix(LobbyDataUpdate_t result)
        {
            try
            {
                if (SteamLobby.Instance == null || result.m_ulSteamIDLobby != SteamLobby.Instance.CurrentLobbyID) return;
                ModState.QuickLoad = SteamMatchmaking.GetLobbyData(new CSteamID(result.m_ulSteamIDLobby), "QuickLoad") == "1";
            }
            catch (Exception e)
            {
                Log.Error($"QuickLoadLobbyDataPatch: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(SteamLobby), "OnLobbyEntered")]
    static class QuickLoadTogglePatch
    {
        static void Postfix(SteamLobby __instance, LobbyEnter_t callback)
        {
            try
            {
                var lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
                ModState.QuickLoad = SteamMatchmaking.GetLobbyData(lobbyId, "QuickLoad") == "1";
                CreateToggle(__instance, lobbyId);
            }
            catch (Exception e)
            {
                Log.Error($"QuickLoadTogglePatch: {e}");
            }
        }

        static void CreateToggle(SteamLobby lobby, CSteamID lobbyId)
        {
            if (lobby.enemyOutlineToggle == null) return;
            var template = lobby.enemyOutlineToggle.transform.parent as RectTransform;
            if (template == null || template.parent == null) return;

            var existing = template.parent.Find("EnableQuickLoad");
            if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);

            var row = UnityEngine.Object.Instantiate(template, template.parent);
            row.name = "EnableQuickLoad";
            row.localScale = Vector3.one;
            row.localPosition += new Vector3(0f, -60f, 0f);

            var toggle = row.GetComponentInChildren<Toggle>();
            if (toggle == null)
            {
                Log.Warn("QuickLoad toggle row has no Toggle component");
                return;
            }

            for (int i = 0; i < toggle.onValueChanged.GetPersistentEventCount(); i++)
                toggle.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);
            toggle.onValueChanged.RemoveAllListeners();

            bool isHost = InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.IsServer;
            toggle.SetIsOnWithoutNotify(ModState.QuickLoad);
            toggle.interactable = isHost;
            if (isHost)
            {
                toggle.onValueChanged.AddListener(isOn =>
                    SteamMatchmaking.SetLobbyData(lobbyId, "QuickLoad", isOn ? "1" : "0"));
            }

            var label = row.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = "  Enable Quick Load";

            Log.Info($"QuickLoad toggle created (host={isHost}, on={ModState.QuickLoad})");
        }
    }
}
