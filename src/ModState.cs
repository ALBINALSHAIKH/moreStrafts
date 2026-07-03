namespace moreStrafts
{
    public static class ModState
    {
        public const int MaxPlayers = 10;

        static bool quickLoad;

        public static bool QuickLoad
        {
            get => quickLoad;
            set
            {
                if (value != quickLoad) Log.Info($"QuickLoad {quickLoad} -> {value}");
                quickLoad = value;
            }
        }
    }
}
