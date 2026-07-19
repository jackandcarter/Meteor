namespace AetherXIV.Launcher.Core;

public enum FfxivClientLanguage
{
    Japanese = 0,
    English = 1,
    German = 2,
    French = 3
}

public enum FfxivDisplayMode
{
    Windowed = 0,
    Fullscreen = 1
}

public enum FfxivShadowMapQuality
{
    Lowest = 0,
    Low = 1,
    Standard = 2,
    High = 3,
    Highest = 4
}

public enum FfxivFrameRateLimit
{
    Fps60 = 0,
    Fps30 = 1,
    Fps20 = 2,
    Fps15 = 3,
    Fps10 = 4
}

public sealed record FfxivSystemConfig(
    FfxivDisplayMode DisplayMode,
    int ResolutionWidth,
    int ResolutionHeight,
    FfxivShadowMapQuality ShadowMapQuality,
    int TextureQualityIndex,
    int BackgroundQualityIndex,
    FfxivFrameRateLimit FrameRateLimit)
{
    public const uint Magic = 0x20120419;

    private const int MagicOffset = 0x00;
    private const int DisplayModeOffset = 0x10;
    private const int ResolutionWidthOffset = 0x14;
    private const int ResolutionHeightOffset = 0x18;
    private const int ShadowMapQualityOffset = 0x20;
    private const int TextureQualityOffset = 0x3c;
    private const int BackgroundQualityOffset = 0x40;
    private const int FrameRateLimitOffset = 0x44;

    public static FfxivSystemConfig Default => new(
        FfxivDisplayMode.Windowed,
        1280,
        720,
        FfxivShadowMapQuality.Highest,
        TextureQualityIndex: 1,
        BackgroundQualityIndex: 2,
        FfxivFrameRateLimit.Fps60);

    public static FfxivSystemConfig Read(byte[] bytes)
    {
        if (!HasValidMagic(bytes))
            return Default;

        return new FfxivSystemConfig(
            ReadEnum(bytes, DisplayModeOffset, FfxivDisplayMode.Windowed),
            ReadPositiveInt(bytes, ResolutionWidthOffset, Default.ResolutionWidth),
            ReadPositiveInt(bytes, ResolutionHeightOffset, Default.ResolutionHeight),
            ReadEnum(bytes, ShadowMapQualityOffset, FfxivShadowMapQuality.Highest),
            ReadBoundedInt(bytes, TextureQualityOffset, Default.TextureQualityIndex, min: 0, max: 2),
            ReadBoundedInt(bytes, BackgroundQualityOffset, Default.BackgroundQualityIndex, min: 0, max: 3),
            ReadEnum(bytes, FrameRateLimitOffset, FfxivFrameRateLimit.Fps60));
    }

    public void WriteTo(byte[] bytes)
    {
        if (bytes.Length != FfxivClientSettingsStore.SystemConfigLength)
            throw new ArgumentException("FFXIV system config has an unexpected size.", nameof(bytes));

        WriteInt(bytes, MagicOffset, unchecked((int)Magic));
        WriteInt(bytes, DisplayModeOffset, (int)DisplayMode);
        WriteInt(bytes, ResolutionWidthOffset, ResolutionWidth);
        WriteInt(bytes, ResolutionHeightOffset, ResolutionHeight);
        WriteInt(bytes, ShadowMapQualityOffset, (int)ShadowMapQuality);
        WriteInt(bytes, TextureQualityOffset, TextureQualityIndex);
        WriteInt(bytes, BackgroundQualityOffset, BackgroundQualityIndex);
        WriteInt(bytes, FrameRateLimitOffset, (int)FrameRateLimit);
    }

    public static bool HasValidMagic(byte[] bytes)
    {
        return bytes.Length == FfxivClientSettingsStore.SystemConfigLength
            && BitConverter.ToUInt32(bytes, MagicOffset) == Magic;
    }

    private static TEnum ReadEnum<TEnum>(byte[] bytes, int offset, TEnum fallback)
        where TEnum : struct, Enum
    {
        int value = BitConverter.ToInt32(bytes, offset);
        return Enum.IsDefined(typeof(TEnum), value) ? (TEnum)(object)value : fallback;
    }

    private static int ReadPositiveInt(byte[] bytes, int offset, int fallback)
    {
        int value = BitConverter.ToInt32(bytes, offset);
        return value > 0 ? value : fallback;
    }

    private static int ReadBoundedInt(byte[] bytes, int offset, int fallback, int min, int max)
    {
        int value = BitConverter.ToInt32(bytes, offset);
        return value >= min && value <= max ? value : fallback;
    }

    private static void WriteInt(byte[] bytes, int offset, int value)
    {
        BitConverter.GetBytes(value).CopyTo(bytes, offset);
    }
}

public sealed record FfxivClientSettings(FfxivClientLanguage Language, FfxivSystemConfig SystemConfig)
{
    public FfxivClientSettings(FfxivClientLanguage language)
        : this(language, FfxivSystemConfig.Default)
    {
    }

    public static FfxivClientSettings Default => new(FfxivClientLanguage.English, FfxivSystemConfig.Default);
}

public sealed record FfxivConfigStorageTarget(
    string HostDocumentsPath,
    string WindowsDocumentsPath,
    string HostConfigDirectoryPath);

public sealed record FfxivSettingsSaveResult(
    string ConfigDirectoryPath,
    string LanguageConfigPath,
    string SystemConfigPath,
    bool CreatedSystemConfig,
    bool RepairedSystemConfig,
    IReadOnlyList<string> BackupPaths);

public static class FfxivClientSettingsStore
{
    public const int SystemConfigLength = 0x2ac;

    private const uint DefaultSystemConfigVirtualAddress = 0x45c600;
    private const string ConfigDirectoryName = "FINAL FANTASY XIV";
    private const string MyGamesDirectoryName = "My Games";
    private const string LanguageConfigFileName = "config.lng";
    private const string SystemConfigFileName = "config.sys";

    public static FfxivClientSettings LoadOrDefault(string configDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(configDirectoryPath))
            return FfxivClientSettings.Default;

        FfxivClientLanguage language = FfxivClientLanguage.English;
        string languagePath = Path.Combine(configDirectoryPath, LanguageConfigFileName);
        if (File.Exists(languagePath))
        {
            byte[] bytes = File.ReadAllBytes(languagePath);
            if (bytes.Length >= 8)
            {
                int value = BitConverter.ToInt32(bytes, 4);
                if (Enum.IsDefined(typeof(FfxivClientLanguage), value))
                    language = (FfxivClientLanguage)value;
            }
        }

        string systemPath = Path.Combine(configDirectoryPath, SystemConfigFileName);
        FfxivSystemConfig systemConfig = FfxivSystemConfig.Default;
        if (File.Exists(systemPath))
            systemConfig = FfxivSystemConfig.Read(File.ReadAllBytes(systemPath));

        return new FfxivClientSettings(language, systemConfig);
    }

    public static FfxivSettingsSaveResult Save(
        ClientInstall clientInstall,
        string configDirectoryPath,
        FfxivClientSettings settings,
        bool repairSystemConfig)
    {
        ArgumentNullException.ThrowIfNull(clientInstall);
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(configDirectoryPath))
            throw new ArgumentException("FFXIV config directory path is required.", nameof(configDirectoryPath));

        Directory.CreateDirectory(configDirectoryPath);

        List<string> backups = new();
        string languagePath = Path.Combine(configDirectoryPath, LanguageConfigFileName);
        string systemPath = Path.Combine(configDirectoryPath, SystemConfigFileName);

        BackupIfExists(languagePath, backups);
        WriteLanguageConfig(languagePath, settings.Language);

        bool systemConfigExisted = File.Exists(systemPath);
        bool systemConfigUsable = IsUsableSystemConfig(systemPath);
        byte[] systemConfigBytes = systemConfigUsable && !repairSystemConfig
            ? File.ReadAllBytes(systemPath)
            : ExtractDefaultSystemConfig(clientInstall.ConfigExecutablePath);

        settings.SystemConfig.WriteTo(systemConfigBytes);

        BackupIfExists(systemPath, backups);
        File.WriteAllBytes(systemPath, systemConfigBytes);

        bool createdSystemConfig = !systemConfigExisted;
        bool repairedSystemConfig = systemConfigExisted && (repairSystemConfig || !systemConfigUsable);

        return new FfxivSettingsSaveResult(
            configDirectoryPath,
            languagePath,
            systemPath,
            createdSystemConfig,
            repairedSystemConfig,
            backups);
    }

    public static byte[] CreateDefaultSystemConfig()
    {
        byte[] config = new byte[SystemConfigLength];
        FfxivSystemConfig.Default.WriteTo(config);

        WriteInt(config, 0x04, 0);
        WriteInt(config, 0x08, 0xd800);
        WriteInt(config, 0x0c, 0);
        WriteInt(config, 0x1c, 7);
        WriteInt(config, 0x24, 0);
        WriteInt(config, 0x28, 0);
        WriteInt(config, 0x2c, 2);
        WriteInt(config, 0x30, 0);
        WriteInt(config, 0x34, 0);
        WriteInt(config, 0x38, 0);
        WriteInt(config, 0x48, 0);
        WriteInt(config, 0x4c, 0);
        WriteInt(config, 0x50, 1);
        WriteInt(config, 0x54, 0);
        WriteInt(config, 0x58, 2);
        WriteInt(config, 0x5c, 1);

        return config;
    }

    public static bool TryLoadSystemConfig(string configDirectoryPath, out FfxivSystemConfig systemConfig)
    {
        systemConfig = FfxivSystemConfig.Default;

        if (string.IsNullOrWhiteSpace(configDirectoryPath))
            return false;

        string systemPath = Path.Combine(configDirectoryPath, SystemConfigFileName);
        if (!IsUsableSystemConfig(systemPath))
            return false;

        systemConfig = FfxivSystemConfig.Read(File.ReadAllBytes(systemPath));
        return true;
    }

    public static bool IsUsableSystemConfig(string systemConfigPath)
    {
        if (!File.Exists(systemConfigPath))
            return false;

        FileInfo info = new(systemConfigPath);
        if (info.Length != SystemConfigLength)
            return false;

        try
        {
            return FfxivSystemConfig.HasValidMagic(File.ReadAllBytes(systemConfigPath));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static FfxivConfigStorageTarget ResolveNativeWindowsTarget()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
            documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");

        string configDirectory = Path.Combine(documents, MyGamesDirectoryName, ConfigDirectoryName);
        return new FfxivConfigStorageTarget(documents, documents, configDirectory);
    }

    public static bool TryResolveWineTarget(
        WineRuntimeProfile profile,
        string managedPrefixPath,
        out FfxivConfigStorageTarget target,
        out string error)
    {
        target = new FfxivConfigStorageTarget("", "", "");
        error = "";

        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Kind == WineRuntimeKind.NativeWindows)
        {
            target = ResolveNativeWindowsTarget();
            return true;
        }

        if (profile.Kind == WineRuntimeKind.WhiskyBottle)
        {
            if (!WhiskyRuntimeEnvironment.TryCreateWineProfile(
                    profile.Command,
                    profile.BottleName ?? "",
                    out WineRuntimeProfile whiskyWineProfile,
                    out string whiskyError))
            {
                error = $"Whisky runtime resolution failed: {whiskyError}";
                return false;
            }

            return TryResolveWineTarget(whiskyWineProfile, managedPrefixPath, out target, out error);
        }

        string? prefixPath = null;
        if (profile.Kind == WineRuntimeKind.WinePrefix)
        {
            prefixPath = string.IsNullOrWhiteSpace(profile.PrefixPath)
                ? managedPrefixPath
                : profile.PrefixPath;
        }
        else if (profile.Environment.TryGetValue("WINEPREFIX", out string? environmentPrefix))
        {
            prefixPath = environmentPrefix;
        }

        if (string.IsNullOrWhiteSpace(prefixPath))
        {
            error = "This runtime does not expose a Wine prefix path, so AetherXIV Launcher cannot safely locate FFXIV config files.";
            return false;
        }

        if (!WineRuntimeConfigurator.TryCreatePrefixLocalDocuments(
                prefixPath,
                out WineUserDocumentsTarget documentsTarget,
                out error))
        {
            return false;
        }

        target = new FfxivConfigStorageTarget(
            documentsTarget.HostDocumentsPath,
            documentsTarget.WindowsDocumentsPath,
            documentsTarget.HostFfxivConfigPath);
        return true;
    }

    public static byte[] ExtractDefaultSystemConfig(string configExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(configExecutablePath) || !File.Exists(configExecutablePath))
            throw new FileNotFoundException("ffxivconfig.exe was not found.", configExecutablePath);

        byte[] executable = File.ReadAllBytes(configExecutablePath);
        int offset = MapVirtualAddressToFileOffset(executable, DefaultSystemConfigVirtualAddress);
        if (offset < 0 || offset + SystemConfigLength > executable.Length)
            throw new InvalidOperationException("Could not locate the default FFXIV system config block in ffxivconfig.exe.");

        byte[] embeddedConfigStorage = executable.AsSpan(offset, SystemConfigLength).ToArray();
        if (embeddedConfigStorage.All(value => value == 0))
            throw new InvalidOperationException("The default FFXIV system config block is empty.");

        return CreateDefaultSystemConfig();
    }

    private static void WriteLanguageConfig(string languagePath, FfxivClientLanguage language)
    {
        byte[] bytes = new byte[8];
        BitConverter.GetBytes((int)language).CopyTo(bytes, 4);
        File.WriteAllBytes(languagePath, bytes);
    }

    private static void BackupIfExists(string path, List<string> backups)
    {
        if (!File.Exists(path))
            return;

        string backupBasePath = $"{path}.bak-{DateTime.UtcNow:yyyyMMddHHmmss}";
        string backupPath = backupBasePath;
        for (int index = 2; File.Exists(backupPath); index++)
            backupPath = $"{backupBasePath}-{index}";

        File.Copy(path, backupPath, overwrite: false);
        backups.Add(backupPath);
    }

    private static void WriteInt(byte[] bytes, int offset, int value)
    {
        BitConverter.GetBytes(value).CopyTo(bytes, offset);
    }

    private static int MapVirtualAddressToFileOffset(byte[] executable, uint virtualAddress)
    {
        if (executable.Length < 0x100)
            return -1;

        int peOffset = BitConverter.ToInt32(executable, 0x3c);
        if (peOffset < 0 || peOffset + 0x18 >= executable.Length)
            return -1;

        if (BitConverter.ToUInt32(executable, peOffset) != 0x00004550)
            return -1;

        ushort sectionCount = BitConverter.ToUInt16(executable, peOffset + 0x6);
        ushort optionalHeaderSize = BitConverter.ToUInt16(executable, peOffset + 0x14);
        int optionalHeaderOffset = peOffset + 0x18;
        if (optionalHeaderOffset + optionalHeaderSize > executable.Length)
            return -1;

        ushort magic = BitConverter.ToUInt16(executable, optionalHeaderOffset);
        if (magic != 0x10b)
            return -1;

        uint imageBase = BitConverter.ToUInt32(executable, optionalHeaderOffset + 0x1c);
        if (virtualAddress < imageBase)
            return -1;

        uint rva = virtualAddress - imageBase;
        int sectionOffset = optionalHeaderOffset + optionalHeaderSize;
        for (int index = 0; index < sectionCount; index++)
        {
            int header = sectionOffset + index * 0x28;
            if (header + 0x28 > executable.Length)
                return -1;

            uint virtualSize = BitConverter.ToUInt32(executable, header + 0x8);
            uint sectionRva = BitConverter.ToUInt32(executable, header + 0xc);
            uint rawSize = BitConverter.ToUInt32(executable, header + 0x10);
            uint rawPointer = BitConverter.ToUInt32(executable, header + 0x14);
            uint mappedSize = Math.Max(virtualSize, rawSize);
            if (rva < sectionRva || rva >= sectionRva + mappedSize)
                continue;

            uint rawOffset = rawPointer + (rva - sectionRva);
            return rawOffset > int.MaxValue ? -1 : (int)rawOffset;
        }

        return -1;
    }
}
