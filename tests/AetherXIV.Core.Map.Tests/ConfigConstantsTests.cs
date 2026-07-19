namespace AetherXIV.Core.Map.Tests;

public sealed class ConfigConstantsTests
{
    [Fact]
    public void ApplyLaunchArgsPreservesCaseSensitiveDatabaseValues()
    {
        string originalHost = ConfigConstants.DATABASE_HOST;
        string originalUser = ConfigConstants.DATABASE_USERNAME;
        string originalPassword = ConfigConstants.DATABASE_PASSWORD;

        try
        {
            ConfigConstants.ApplyLaunchArgs(
            [
                "--HOST", "MariaDB",
                "--user", "AetherUser",
                "--p", "MixedCase-Secret"
            ]);

            Assert.Equal("MariaDB", ConfigConstants.DATABASE_HOST);
            Assert.Equal("AetherUser", ConfigConstants.DATABASE_USERNAME);
            Assert.Equal("MixedCase-Secret", ConfigConstants.DATABASE_PASSWORD);
        }
        finally
        {
            ConfigConstants.DATABASE_HOST = originalHost;
            ConfigConstants.DATABASE_USERNAME = originalUser;
            ConfigConstants.DATABASE_PASSWORD = originalPassword;
        }
    }
}
