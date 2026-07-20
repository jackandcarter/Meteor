namespace AetherXIV.Core.Map.Actors
{
    static class NpcPropertyPolicy
    {
        // Property 2 is player-owned state in the 1.23b actor work contract.
        // Replaying it on generic/tutorial NPC construction corrupts the
        // client's actor state while the opening battle roster is installed.
        private const uint ForbiddenNpcPropertyMask = 1u << 2;

        public static uint Sanitize(uint propertyFlags) =>
            propertyFlags & ~ForbiddenNpcPropertyMask;
    }
}
