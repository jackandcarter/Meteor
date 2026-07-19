namespace Aether.Umbra.Framework;

public sealed class UmbraRuntimeLog
{
    private readonly string path;
    private readonly object gate = new();

    private UmbraRuntimeLog(string path)
    {
        this.path = path;
    }

    public static UmbraRuntimeLog Open(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        return new UmbraRuntimeLog(path);
    }

    public void Info(string message)
    {
        Write(message);
    }

    public void Warning(string message)
    {
        Write($"warning={message}");
    }

    public void Error(string message)
    {
        Write(message);
    }

    public void Error(string message, Exception exception)
    {
        Write($"{message} error={exception}");
    }

    private void Write(string message)
    {
        lock (gate)
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
    }
}
