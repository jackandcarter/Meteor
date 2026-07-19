namespace AetherXIV.Core.Map.Tests;

public sealed class PlayerSessionTransitionPolicyTests
{
    [Theory]
    [InlineData(PlayerSessionTransitionPolicy.LogoutOpcode)]
    [InlineData(PlayerSessionTransitionPolicy.QuitOpcode)]
    public void TerminalClientPacketsOwnTheirWorldDisconnect(ushort opcode)
    {
        Assert.True(PlayerSessionTransitionPolicy.ClientOwnsWorldDisconnect(opcode));
    }

    [Fact]
    public void UnrelatedOpcodeDoesNotClaimTerminalDisconnectOwnership()
    {
        Assert.False(PlayerSessionTransitionPolicy.ClientOwnsWorldDisconnect(0x0001));
    }
}
