namespace AetherXIV.ClientData;

public static class ClientDataClassifier
{
    public static bool IsCandidate(string clientRootPath, string filePath)
    {
        return Classify(clientRootPath, filePath) != ClientDataKind.UnknownCandidate
            || IsSqpackPath(clientRootPath, filePath);
    }

    public static ClientDataKind Classify(string clientRootPath, string filePath)
    {
        string relativePath = NormalizeRelativePath(clientRootPath, filePath);
        string fileName = Path.GetFileName(filePath).ToLowerInvariant();
        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension == ".gmd")
            return ClientDataKind.LooseGmd;

        if (extension == ".geb")
            return ClientDataKind.LooseGeb;

        if (fileName.EndsWith(".index", StringComparison.Ordinal)
            || fileName.EndsWith(".index2", StringComparison.Ordinal)
            || fileName.Contains(".win32.index", StringComparison.Ordinal))
            return ClientDataKind.SqpackIndex;

        if (fileName.Contains(".win32.dat", StringComparison.Ordinal)
            || (IsSqpackPath(relativePath) && extension.StartsWith(".dat", StringComparison.Ordinal)))
            return ClientDataKind.SqpackData;

        if (extension.StartsWith(".dat", StringComparison.Ordinal))
            return ClientDataKind.PackedDatResource;

        if (IsSqpackPath(relativePath))
            return ClientDataKind.SqpackOther;

        return ClientDataKind.UnknownCandidate;
    }

    public static ClientDataExtractionMode GetExtractionMode(ClientDataKind kind, bool includeStringProbes)
    {
        return kind switch
        {
            ClientDataKind.LooseGmd or ClientDataKind.LooseGeb when includeStringProbes => ClientDataExtractionMode.StringProbe,
            ClientDataKind.LooseGmd or ClientDataKind.LooseGeb => ClientDataExtractionMode.FileInventory,
            ClientDataKind.PackedDatResource => ClientDataExtractionMode.ResourceHeaderProbe,
            ClientDataKind.SqpackIndex or ClientDataKind.SqpackData or ClientDataKind.SqpackOther => ClientDataExtractionMode.ArchiveCatalogOnly,
            _ => ClientDataExtractionMode.FileInventory
        };
    }

    public static string NormalizeRelativePath(string clientRootPath, string filePath)
    {
        string relativePath = Path.GetRelativePath(clientRootPath, filePath);
        return relativePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static bool IsSqpackPath(string clientRootPath, string filePath)
    {
        return IsSqpackPath(NormalizeRelativePath(clientRootPath, filePath));
    }

    private static bool IsSqpackPath(string relativePath)
    {
        return relativePath.Contains("/sqpack/", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("sqpack/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/sqpack", StringComparison.OrdinalIgnoreCase);
    }
}
