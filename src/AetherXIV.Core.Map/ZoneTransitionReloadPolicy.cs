namespace AetherXIV.Core.Map
{
    enum ZoneTransitionReloadRecipe
    {
        FullMap,
        ResidentGeometry
    }

    static class ZoneTransitionReloadPolicy
    {
        public static ZoneTransitionReloadRecipe Select(uint currentZoneId, uint destinationZoneId)
        {
            return currentZoneId == destinationZoneId
                ? ZoneTransitionReloadRecipe.ResidentGeometry
                : ZoneTransitionReloadRecipe.FullMap;
        }
    }
}
