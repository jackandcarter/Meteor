using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

internal sealed class UmbraCommandService(UmbraRuntimeLog log) : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, CommandEntry> commands = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    internal IUmbraCommandManager CreateScope(string pluginId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return new ScopedCommandManager(this, pluginId);
    }

    internal IReadOnlyList<UmbraCommandInfo> GetCommands()
    {
        lock (gate)
        {
            return commands.Values
                .Select(entry => entry.Info)
                .OrderBy(info => info.Command, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    internal IDisposable Register(
        string pluginId,
        UmbraCommandRegistration registration,
        UmbraCommandHandler handler)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(handler);

        string command = NormalizeCommand(registration.Command);
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Umbra command owner must not be empty.", nameof(pluginId));

        CommandEntry entry = new(
            Guid.NewGuid(),
            new UmbraCommandInfo(command, registration.HelpMessage?.Trim() ?? "", registration.ShowInHelp, pluginId),
            handler);

        lock (gate)
        {
            if (commands.TryGetValue(command, out CommandEntry? existing))
            {
                throw new InvalidOperationException(
                    $"Umbra command {command} is already registered by {existing.Info.PluginId}.");
            }

            commands.Add(command, entry);
        }

        log.Info($"umbra_command_registered command={command} plugin={pluginId}");
        return new Registration(this, pluginId, command, entry.Id);
    }

    internal bool Unregister(string pluginId, string command)
    {
        string normalized = NormalizeCommand(command);
        bool removed;
        lock (gate)
        {
            removed = commands.TryGetValue(normalized, out CommandEntry? entry)
                && string.Equals(entry.Info.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)
                && commands.Remove(normalized);
        }

        if (removed)
            log.Info($"umbra_command_unregistered command={normalized} plugin={pluginId}");
        return removed;
    }

    internal UmbraCommandDispatchResult Dispatch(string content)
    {
        if (!TryParse(content, out UmbraCommandInvocation? invocation, out string? error))
            return new UmbraCommandDispatchResult(UmbraCommandDispatchStatus.Invalid, "", error);
        UmbraCommandInvocation parsed = invocation!;

        CommandEntry? entry;
        lock (gate)
            commands.TryGetValue(parsed.Command, out entry);

        if (entry is null)
            return new UmbraCommandDispatchResult(UmbraCommandDispatchStatus.NotFound, parsed.Command);

        try
        {
            entry.Handler(parsed);
            log.Info($"umbra_command_dispatched command={parsed.Command} plugin={entry.Info.PluginId}");
            return new UmbraCommandDispatchResult(UmbraCommandDispatchStatus.Dispatched, parsed.Command);
        }
        catch (Exception ex)
        {
            log.Error($"umbra_command_dispatch_failed command={parsed.Command} plugin={entry.Info.PluginId}", ex);
            return new UmbraCommandDispatchResult(UmbraCommandDispatchStatus.Failed, parsed.Command, ex.Message);
        }
    }

    internal void Release(string pluginId)
    {
        string[] owned;
        lock (gate)
        {
            owned = commands
                .Where(pair => string.Equals(pair.Value.Info.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .ToArray();
            foreach (string command in owned)
                commands.Remove(command);
        }

        foreach (string command in owned)
            log.Info($"umbra_command_released command={command} plugin={pluginId}");
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        lock (gate)
            commands.Clear();
    }

    private void Unregister(string pluginId, string command, Guid registrationId)
    {
        bool removed;
        lock (gate)
        {
            removed = commands.TryGetValue(command, out CommandEntry? entry)
                && entry.Id == registrationId
                && string.Equals(entry.Info.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)
                && commands.Remove(command);
        }

        if (removed)
            log.Info($"umbra_command_unregistered command={command} plugin={pluginId}");
    }

    private static string NormalizeCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Umbra command must not be empty.", nameof(command));

        string normalized = command.Trim();
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;
        if (normalized.Length is < 2 or > 64)
            throw new ArgumentException("Umbra command must contain 1 to 63 characters after '/'.", nameof(command));
        if (normalized.AsSpan(1).IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-".AsSpan()) >= 0)
            throw new ArgumentException("Umbra commands may only contain letters, digits, underscore, and hyphen.", nameof(command));

        return normalized.ToLowerInvariant();
    }

    private static bool TryParse(
        string content,
        out UmbraCommandInvocation? invocation,
        out string? error)
    {
        invocation = null;
        error = null;
        if (string.IsNullOrWhiteSpace(content))
        {
            error = "Command input is empty.";
            return false;
        }

        string raw = content.Trim();
        int separator = raw.IndexOfAny([' ', '\t', '\r', '\n']);
        string commandText = separator < 0 ? raw : raw[..separator];
        string arguments = separator < 0 ? "" : raw[(separator + 1)..].TrimStart();

        try
        {
            string command = NormalizeCommand(commandText);
            invocation = new UmbraCommandInvocation(command, arguments, raw);
            return true;
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private sealed record CommandEntry(Guid Id, UmbraCommandInfo Info, UmbraCommandHandler Handler);

    private sealed class ScopedCommandManager(UmbraCommandService owner, string pluginId) : IUmbraCommandManager
    {
        public IReadOnlyList<UmbraCommandInfo> Commands => owner.GetCommands();

        public IDisposable Register(UmbraCommandRegistration registration, UmbraCommandHandler handler) =>
            owner.Register(pluginId, registration, handler);

        public bool Unregister(string command) => owner.Unregister(pluginId, command);

        public UmbraCommandDispatchResult Dispatch(string content) => owner.Dispatch(content);
    }

    private sealed class Registration(
        UmbraCommandService owner,
        string pluginId,
        string command,
        Guid registrationId) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
                owner.Unregister(pluginId, command, registrationId);
        }
    }
}
