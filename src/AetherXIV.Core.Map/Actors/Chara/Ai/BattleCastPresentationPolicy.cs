namespace AetherXIV.Core.Map.actors.chara.ai
{
    static class BattleCastPresentationPolicy
    {
        // Official 1.23b captures use chant 0x60 for player White Magic
        // (castType 3) and 0xE0 for the observed NPC castType 12 action.
        // Other captured NPC casts use the legacy 0xF0 value. Keep that as
        // the fallback until another cast type is constrained by a capture.
        public static byte GetChantId(bool isPlayer, byte castType)
        {
            if (isPlayer && castType == 3)
                return 0x60;

            if (!isPlayer && castType == 12)
                return 0xE0;

            return 0xF0;
        }
    }
}
