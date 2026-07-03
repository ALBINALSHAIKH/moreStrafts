using HarmonyLib;

namespace moreStrafts.Patches
{
    static class PreviewSlots
    {
        public static bool Exists(int id)
        {
            var controller = LobbyController.Instance;
            bool exists = controller != null && id >= 0 && id < controller.previews.Length && controller.previews[id] != null;
            if (!exists) Log.Info($"player {id} has no lobby preview slot, skipping preview RPC");
            return exists;
        }
    }

    [HarmonyPatch(typeof(ClientInstance), "RpcLogic___RunIntoLobby_3316948804")]
    static class RunIntoLobbyPatch
    {
        static bool Prefix(int id) => PreviewSlots.Exists(id);
    }

    [HarmonyPatch(typeof(ClientInstance), "RpcLogic___MenuAnimationObservers_1692629761")]
    static class MenuAnimationObserversPatch
    {
        static bool Prefix(int id) => PreviewSlots.Exists(id);
    }

    [HarmonyPatch(typeof(ClientInstance), "RpcLogic___DressAboubiObservers_2497120398")]
    static class DressAboubiObserversPatch
    {
        static bool Prefix(int id) => PreviewSlots.Exists(id);
    }
}
