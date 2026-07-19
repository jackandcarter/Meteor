using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Aether.Umbra.Framework;

public sealed class UmbraReadOnlyMemory(UmbraRuntimeLog log)
{
    public const int MaxPeekBytes = 4096;
    public const int MaxScanBytes = 1024 * 1024;
    public const int MaxScanMatches = 128;

    public UmbraMemoryPeekResult Peek(nuint address, int size)
    {
        if (address == 0)
            return UmbraMemoryPeekResult.Failed(address, size, "address is zero");

        if (size <= 0 || size > MaxPeekBytes)
            return UmbraMemoryPeekResult.Failed(address, size, $"size must be 1..{MaxPeekBytes}");

        byte[] bytes = new byte[size];
        if (!ReadProcessMemory(GetCurrentProcess(), address, bytes, bytes.Length, out nuint read) || read == 0)
        {
            string error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            log.Warning($"umbra_memory_peek_failed address=0x{address:X} size={size} error={error}");
            return UmbraMemoryPeekResult.Failed(address, size, error);
        }

        if ((int)read != bytes.Length)
            Array.Resize(ref bytes, (int)read);

        log.Info($"umbra_memory_peek address=0x{address:X} size={size} read={read}");
        return UmbraMemoryPeekResult.Ok(address, size, bytes);
    }

    public UmbraMemoryScanResult Scan(nuint start, int size, UmbraBytePattern pattern)
    {
        if (start == 0)
            return UmbraMemoryScanResult.Failed(start, size, "start address is zero");

        if (size <= 0 || size > MaxScanBytes)
            return UmbraMemoryScanResult.Failed(start, size, $"size must be 1..{MaxScanBytes}");

        if (pattern.Length == 0 || pattern.Length > 64)
            return UmbraMemoryScanResult.Failed(start, size, "pattern length must be 1..64");

        UmbraMemoryPeekResult peek = Peek(start, size);
        if (!peek.Success || peek.Bytes.Length == 0)
            return UmbraMemoryScanResult.Failed(start, size, peek.Error ?? "memory read failed");

        List<string> matches = new();
        byte[] data = peek.Bytes;
        for (int index = 0; index <= data.Length - pattern.Length; index++)
        {
            if (!pattern.Matches(data, index))
                continue;

            matches.Add($"0x{(start + (uint)index):X}");
            if (matches.Count >= MaxScanMatches)
                break;
        }

        log.Info($"umbra_memory_scan start=0x{start:X} size={size} pattern_bytes={pattern.Length} matches={matches.Count}");
        return new UmbraMemoryScanResult(true, $"0x{start:X}", size, matches, null);
    }

    public object ProcessStatus()
    {
        Process process = Process.GetCurrentProcess();
        return new
        {
            process_id = Environment.ProcessId,
            process_name = process.ProcessName,
            module_count = process.Modules.Count,
            main_module = TryGetMainModule(process)
        };
    }

    private static object? TryGetMainModule(Process process)
    {
        try
        {
            ProcessModule? module = process.MainModule;
            if (module is null)
                return null;

            return new
            {
                name = module.ModuleName,
                base_address = $"0x{module.BaseAddress.ToInt64():X}",
                size = module.ModuleMemorySize
            };
        }
        catch
        {
            return null;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        nint process,
        nuint baseAddress,
        [Out] byte[] buffer,
        int size,
        out nuint bytesRead);
}

public sealed record UmbraMemoryPeekResult(
    bool Success,
    string Address,
    int RequestedSize,
    int ReadSize,
    string Hex,
    byte[] Bytes,
    string? Error)
{
    public static UmbraMemoryPeekResult Ok(nuint address, int requestedSize, byte[] bytes)
    {
        return new(true, $"0x{address:X}", requestedSize, bytes.Length, Convert.ToHexString(bytes), bytes, null);
    }

    public static UmbraMemoryPeekResult Failed(nuint address, int requestedSize, string error)
    {
        return new(false, $"0x{address:X}", requestedSize, 0, "", Array.Empty<byte>(), error);
    }
}

public sealed record UmbraMemoryScanResult(
    bool Success,
    string Start,
    int Size,
    IReadOnlyList<string> Matches,
    string? Error)
{
    public static UmbraMemoryScanResult Failed(nuint start, int size, string error)
    {
        return new(false, $"0x{start:X}", size, Array.Empty<string>(), error);
    }
}

public sealed class UmbraBytePattern
{
    private readonly byte?[] bytes;

    private UmbraBytePattern(byte?[] bytes)
    {
        this.bytes = bytes;
    }

    public int Length => bytes.Length;

    public static bool TryParse(string? value, out UmbraBytePattern pattern, out string? error)
    {
        pattern = new UmbraBytePattern(Array.Empty<byte?>());
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "pattern is required";
            return false;
        }

        string[] parts = value.Split([' ', '\t', '-', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<byte?> parsed = new();
        foreach (string part in parts)
        {
            if (part is "?" or "??")
            {
                parsed.Add(null);
                continue;
            }

            if (!byte.TryParse(part, System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                error = $"invalid pattern byte: {part}";
                return false;
            }

            parsed.Add(b);
        }

        pattern = new UmbraBytePattern(parsed.ToArray());
        return true;
    }

    public bool Matches(byte[] data, int offset)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            byte? expected = bytes[index];
            if (expected.HasValue && data[offset + index] != expected.Value)
                return false;
        }

        return true;
    }
}
