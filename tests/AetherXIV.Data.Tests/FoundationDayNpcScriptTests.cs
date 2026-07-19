namespace AetherXIV.Data.Tests;

public sealed class FoundationDayNpcScriptTests
{
    [Theory]
    [InlineData("flame_lieutenant_somber_meadow.lua", "processEventSOMBER")]
    [InlineData("flame_sergeant_mimio_mio.lua", "processEventMIMIO")]
    [InlineData("flame_private_sisimuza_tetemuza.lua", "processEventSISIMUZA")]
    public void UldahFoundationDayNpcDelegatesToSpl000(string fileName, string clientFunction)
    {
        string path = Path.Combine(
            FindDataRoot(),
            "scripts",
            "unique",
            "wil0Town01",
            "PopulaceStandard",
            fileName);
        string script = File.ReadAllText(path);

        Assert.Contains("GetStaticActor(\"Spl000\")", script, StringComparison.Ordinal);
        Assert.Contains($"\"{clientFunction}\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("defaultTalkWithSomber_001", script, StringComparison.Ordinal);
    }

    private static string FindDataRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Data");
            if (Directory.Exists(Path.Combine(candidate, "scripts")))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository Data directory.");
    }
}
