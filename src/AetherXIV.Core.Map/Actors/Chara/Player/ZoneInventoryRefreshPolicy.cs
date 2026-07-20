namespace AetherXIV.Core.Map.actors.chara.player
{
    enum ZoneInventoryRefreshMode
    {
        Full,
        RetainKnownItemDefinitions
    }

    static class ZoneInventoryRefreshPolicy
    {
        public static bool ShouldResendItemDefinitions(ZoneInventoryRefreshMode mode)
        {
            return mode == ZoneInventoryRefreshMode.Full;
        }
    }
}
