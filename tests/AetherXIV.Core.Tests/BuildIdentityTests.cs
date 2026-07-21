namespace AetherXIV.Core.Tests;

public sealed class BuildIdentityTests
{
    [Fact]
    public void SharedBuildIdentityMatchesRepositoryAndMsBuildMetadata()
    {
        string root = FindRepositoryRoot();
        string buildNumber = File.ReadAllText(Path.Combine(root, "build-number.txt")).Trim();
        string props = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "build-release.yml"));

        Assert.Equal(AetherXivBuildInfo.BuildNumber.ToString(), buildNumber);
        Assert.Contains($"<AetherXivBuildNumber>{buildNumber}</AetherXivBuildNumber>", props, StringComparison.Ordinal);
        Assert.Contains($"<InformationalVersion>2.0.0+build.{buildNumber}</InformationalVersion>", props, StringComparison.Ordinal);
        Assert.Contains($"BUILD_NUMBER: {buildNumber}", workflow, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AetherXIV.sln")) &&
                File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                File.Exists(Path.Combine(directory.FullName, "build-number.txt")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "AetherXIV.Core")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the AetherXIV repository root.");
    }
}
