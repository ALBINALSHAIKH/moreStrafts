using System;
using FishNet;
using HarmonyLib;
using Steamworks;
using TMPro;
using UnityEngine;

namespace moreStrafts.Patches
{
    static class LobbyCapacity
    {
        public static void ExpandDropdown(TMP_Dropdown? dropdown)
        {
            if (dropdown == null || dropdown.options.Count >= ModState.MaxPlayers - 1) return;
            int value = dropdown.value;
            dropdown.options.Clear();
            for (int players = 2; players <= ModState.MaxPlayers; players++)
                dropdown.options.Add(new TMP_Dropdown.OptionData($"{players} Players"));
            dropdown.value = value;
            dropdown.RefreshShownValue();
            Log.Info($"max-players dropdown expanded to 2-{ModState.MaxPlayers} (value {value})");
        }

        public static void SetTransportMaxClients(int maxClients)
        {
            var transport = InstanceFinder.TransportManager?.Transport;
            if (transport == null) return;
            transport.SetMaximumClients(maxClients);
            Log.Info($"transport max clients set to {maxClients}");
        }
    }

    [HarmonyPatch(typeof(SteamLobby), "Start")]
    static class SteamLobbyStartPatch
    {
        static void Postfix(SteamLobby __instance)
        {
            try
            {
                LobbyCapacity.ExpandDropdown(__instance.MaxPlayersDropdown);
                LobbyCapacity.SetTransportMaxClients(ModState.MaxPlayers - 1);
            }
            catch (Exception e)
            {
                Log.Error($"SteamLobbyStartPatch: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(SteamLobby), "OnLobbyCreated")]
    static class OnLobbyCreatedPatch
    {
        static void Prefix(SteamLobby __instance)
        {
            try
            {
                LobbyCapacity.ExpandDropdown(__instance.MaxPlayersDropdown);
            }
            catch (Exception e)
            {
                Log.Error($"OnLobbyCreatedPatch prefix: {e}");
            }
        }

        static void Postfix(SteamLobby __instance, LobbyCreated_t callback)
        {
            try
            {
                if (callback.m_eResult != EResult.k_EResultOK) return;
                SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "QuickLoad", "0");
                ModState.QuickLoad = false;
                LobbyCapacity.SetTransportMaxClients(__instance.maxPlayers - 1);
                Log.Info($"lobby {callback.m_ulSteamIDLobby} created with maxPlayers={__instance.maxPlayers}");
            }
            catch (Exception e)
            {
                Log.Error($"OnLobbyCreatedPatch postfix: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(SteamLobby), "SetMaxPlayers")]
    static class SetMaxPlayersPatch
    {
        static void Postfix(SteamLobby __instance)
        {
            try
            {
                LobbyCapacity.SetTransportMaxClients(__instance.maxPlayers - 1);
            }
            catch (Exception e)
            {
                Log.Error($"SetMaxPlayersPatch: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(ClientInstance), "RpcLogic___UpdateOnClients_3316948804")]
    static class UpdateOnClientsPatch
    {
        static void Prefix(ref int maxPlayers)
        {
            maxPlayers = Mathf.Clamp(maxPlayers, 2, ModState.MaxPlayers);
        }
    }
}
