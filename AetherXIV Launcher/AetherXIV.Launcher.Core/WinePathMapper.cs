namespace AetherXIV.Launcher.Core;

public static class WinePathMapper
{
    public static string ToWindowsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));

        string fullPath = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows())
            return fullPath;

        string normalized = fullPath.Replace('/', '\\');
        return normalized.StartsWith('\\')
            ? $"Z:{normalized}"
            : normalized;
    }
}
