namespace AetherXIV.Core.Map
{
    /// <summary>
    /// Ownership rules for terminal client-session transitions.
    /// The 1.23b client closes its World connection after either packet; World
    /// then requests the normal Map session end.  Sending an unsolicited Map
    /// end-confirm first makes World force-close the socket and the client
    /// presents that expected return as game-server error 30002.
    /// </summary>
    static class PlayerSessionTransitionPolicy
    {
        public const ushort LogoutOpcode = 0x000E;
        public const ushort QuitOpcode = 0x0011;

        public static bool ClientOwnsWorldDisconnect(ushort opcode)
        {
            return opcode == LogoutOpcode || opcode == QuitOpcode;
        }
    }
}
