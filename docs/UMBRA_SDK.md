# Umbra 2.0 Plugin SDK

Umbra is AetherXIV Launcher's in-game framework for the supported Final Fantasy
XIV 1.23b client. It provides a versioned managed plugin API, native DirectX 9
render integration, plugin isolation, repository-backed installs, safe mode,
diagnostics, and a loopback-only development bridge.

This document describes the implementation currently in this repository:

- Umbra API: `2.0`
- Framework implementation: `0.1.0`
- Plugin target framework: `.NET 10`
- Recognized client: Final Fantasy XIV 1.23b build `2012.09.19.0001`, x86

Unknown client hashes are not granted client-memory adapters. Plugins must check
service availability instead of assuming that chat or appearance bindings work.

## SDK projects

| Project | Purpose |
|---|---|
| `Aether.Umbra.PluginApi` | Stable contracts referenced by third-party plugins |
| `Aether.Umbra.Framework` | Runtime, services, plugin manager, repositories, and dev tools |
| `Aether.Umbra.Bootstrap` | Native x86 DirectX 9/Win32 bootstrap loaded into the client |
| `Aether.Umbra.SamplePlugin` | Buildable API 2.0 example plugin |

Plugin projects should reference only `Aether.Umbra.PluginApi`. Do not reference
the Framework assembly or native bootstrap from third-party plugin code.

## Create a plugin

Create a .NET class library targeting `net10.0` and reference the API project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>Example.Umbra.Plugin</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Aether.Umbra.PluginApi/Aether.Umbra.PluginApi.csproj" />
    <None Include="umbra-plugin.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

Implement `IUmbraPlugin`:

```csharp
using Aether.Umbra.PluginApi;

public sealed class ExamplePlugin : IUmbraPlugin
{
    private IUmbraPluginContext? context;
    private bool windowOpen = true;

    public string Name => "Example Plugin";

    public void Initialize(IUmbraPluginContext context)
    {
        this.context = context;
        Directory.CreateDirectory(context.ConfigDirectory);
        context.Logger.Info("initialized");
    }

    public void Update(TimeSpan delta) { }

    public void Draw(IUmbraDrawContext draw)
    {
        if (!windowOpen)
            return;

        bool visible = draw.BeginWindow("Example###ExamplePlugin", ref windowOpen);
        try
        {
            if (visible)
                draw.Text("Hello from Umbra.", UmbraTextTone.Accent);
        }
        finally
        {
            draw.EndWindow();
        }
    }

    public void Dispose()
    {
        context?.Logger.Info("disposed");
        context = null;
    }
}
```

Lifecycle callbacks are:

- `Initialize`: acquire services, create configuration storage, and register
  commands.
- `Update`: perform short non-render work.
- `Draw`: emit UI during the render callback. Keep it fast and balanced.
- `Dispose`: release registrations and resources during reload, disable,
  quarantine, or shutdown.

## Plugin manifest

Place `umbra-plugin.json` beside the plugin assembly:

```json
{
  "id": "com.example.umbra.plugin",
  "name": "Example Plugin",
  "version": "1.0.0",
  "api_version": "2.0",
  "entry": "Example.Umbra.Plugin.dll",
  "entry_type": "ExamplePlugin",
  "minimum_framework_version": "0.1.0",
  "enabled": false,
  "capabilities": ["commands.register", "chat.print"]
}
```

| Field | Required | Meaning |
|---|---|---|
| `id` | Yes | Stable, globally unique plugin ID; reverse-domain form is recommended |
| `name` | Yes | Public display name |
| `version` | Yes | Plugin version |
| `api_version` | Yes | Umbra API compatibility requested by the plugin |
| `entry` | Yes | Relative path to the managed entry assembly |
| `entry_type` | No | Fully qualified plugin type; required when multiple public implementations exist |
| `minimum_framework_version` | Yes | Oldest Framework implementation accepted |
| `enabled` | Yes | Whether the runtime should load the plugin automatically |
| `capabilities` | No | Privileged services requested by the plugin |

Absolute entry paths and `..` traversal are rejected. API compatibility requires
the same major version and a requested minor no newer than the runtime. Framework
compatibility requires the installed version to meet the declared minimum.

## Capabilities and services

| Capability | Service | Access |
|---|---|---|
| `commands.register` | `IUmbraCommandManager` | Register and dispatch plugin slash commands |
| `chat.print` | `IUmbraChat` | Print tagged plugin text through a verified adapter |
| `chat.submit` | `IUmbraChat` | Submit chat input through a verified adapter |
| `client.appearance.read` | `IUmbraActorAppearanceService` | Read immutable observed appearance snapshots |

`GetService<T>()` returns `null` when the required capability was not declared.
Chat can be partially granted: print without submit, or submit without print.

The values `ui.draw` and `configuration` are accepted as descriptive manifest
capabilities, but drawing and the plugin config directory are supplied through
the base lifecycle rather than a gated service.

## Plugin context

`IUmbraPluginContext` exposes plugin, API, and framework identity; a sanitized
plugin-specific `ConfigDirectory`; declared capabilities; a shutdown token;
scoped logging; and capability-aware service lookup. Treat context and service
objects as runtime-owned and do not retain them after `Dispose`.

## Drawing API

`IUmbraDrawContext` supplies frame timing, viewport dimensions, render-thread
state, content widths, device generation, and plugin-manager state.

Available primitives include:

- Windows and sizing: `BeginWindow`, `EndWindow`, `SetNextWindowSize`.
- Layout: `SameLine`, `Separator`, `Spacing`, `BeginChild`, `BeginPanel`, and
  `EndChild`.
- Text and input: `Text`, `InputText`, `Checkbox`, and `Toggle`.
- Actions and visuals: styled `Button`, `Icon`, `Badge`, and `Artwork`.
- Framework action: `RequestPluginManagerOpen`.

Always balance begin/end calls, including early-return and exception paths.
Umbra performs render-state recovery after every plugin callback as a safety net.

The third-party draw budget is 4 milliseconds. Umbra tracks last and peak draw
time and counts over-budget frames. It logs the first slow draw and periodic
reminders thereafter.

## Commands

```csharp
IUmbraCommandManager? commands = context.GetService<IUmbraCommandManager>();
IDisposable? registration = commands?.Register(
    new UmbraCommandRegistration("/example", "Shows the example plugin."),
    invocation => context.Logger.Info(invocation.Arguments));
```

Commands are lowercase and contain 1 to 63 letters, digits, underscores, or
hyphens after `/`. Duplicate commands are rejected. Dispose registrations when
done; Umbra also releases all commands owned by an unloaded plugin.

## Chat

```csharp
IUmbraChat? chat = context.GetService<IUmbraChat>();
if (chat?.Availability.CanPrint == true)
    chat.Print("Ready.", "Example", UmbraChatTone.System);
```

Delivery can be delivered, unavailable, denied, rejected, or failed. The legacy
chat buffer accepts at most 511 UTF-8 bytes. Current builds deliberately report
native chat as unavailable until its client binding is verified.

## Actor appearance observations

`IUmbraActorAppearanceService` is read-only. It exposes immutable snapshots with
actor/model identity, revision, timestamp, source, and the legacy 28-value
appearance table. `UmbraGraphicId` decodes compatible equipment slots into
weapon, equipment, variant, and color components. Plugins cannot publish or
mutate snapshots, and unverified builds receive no active adapter.

## Discovery, loading, and isolation

Umbra discovers `umbra-plugin.json` or `plugin.json` directly in the configured
plugin folder and one directory below it. Each plugin loads in a collectible
assembly load context so it can be unloaded or reloaded.

Safe mode loads system plugins only. A third-party plugin is quarantined after
three consecutive callback failures. Runtime status includes load state, errors,
callback duration, peak draw time, and slow-draw count.

The built-in Plugin Manager supports discovery, installed plugins, updates,
repositories, enable/disable, reload, install, and recoverable uninstall.
Uninstall moves a plugin into `Cache/PluginTrash` instead of permanently
deleting it.

## Repositories and package security

Repository and download URLs must use HTTPS; HTTP is allowed only for loopback
development. Repository responses are cached for use when a later fetch fails.

The Repositories tab accepts an HTTPS URL for a JSON plugin index. GitHub Pages,
GitHub Releases, or any other static HTTPS host can be used; Umbra does not clone
or build a source repository on the client. A custom index may use a Dalamud-style
top-level array, while the supported AetherXIV service uses a named envelope:

```json
{
  "repository_name": "Example Umbra Repository",
  "plugins": [
    {
      "id": "example.plugin",
      "name": "Example Plugin",
      "version": "1.0.0",
      "api_version": "2.0",
      "author": "Example Developer",
      "description": "An example Umbra plugin.",
      "download_url": "https://example.invalid/releases/example.plugin-1.0.0.zip",
      "size_bytes": 12345,
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "minimum_framework_version": "0.1.0",
      "entry": "Example.Plugin.dll"
    }
  ]
}
```

Every package must contain `umbra-plugin.json` or `plugin.json` at the ZIP root.
Its identity, name, version, API version, minimum framework version, and optional
entry path must match the repository entry, and the declared assembly must exist.
Custom repositories are checksum-verified but remain unreviewed; only the managed
AetherXIV catalog receives the supported trust label.

Installable entries require identity/version fields, API and minimum framework
versions, URL, archive size, and SHA-256. Umbra verifies size and hash before
extraction, bounds archive size and expansion, and rejects rooted or traversal
archive paths. Installation validates in a staging directory before activation.
Updates preserve the previous package in `Cache/PluginBackups`, and a failed
activation restores the prior installation. Hidden and testing-only entries are
not normally installable.

## Development bridge

The optional bridge listens only on `127.0.0.1`, defaults to port `8797`, and is
disabled unless enabled through environment or control state.

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/status` | Runtime, process, bridge, and capture status |
| `GET` | `/events?limit=100` | Recent bounded development events |
| `GET` | `/logs?limit=120` | Tail the framework log |
| `POST` | `/capture/start` | Start a JSON Lines event capture |
| `POST` | `/capture/pause` | Pause the active capture |
| `POST` | `/capture/stop` | Stop the active capture |
| `POST` | `/memory/peek` | Bounded read-only process-memory probe |
| `POST` | `/scan/pattern` | Bounded read-only byte-pattern scan |

The bridge provides no memory writes. Keep it disabled for ordinary play and do
not expose or proxy it outside the local machine.

## Runtime environment

| Variable | Meaning |
|---|---|
| `AETHER_UMBRA_LOG` | Framework log path |
| `AETHER_UMBRA_PLUGIN_DIR` | Plugin discovery/install directory |
| `AETHER_UMBRA_CACHE_DIR` | Repository, config, trash, and dev cache root |
| `AETHER_UMBRA_SAFE_MODE` | `1`, `true`, or `yes` disables third-party plugins |
| `AETHER_UMBRA_REPOSITORY_URLS` | Semicolon/newline-separated repositories |
| `AETHER_UMBRA_REPOSITORIES_JSON` | Repository sources and supported/custom metadata |
| `AETHER_UMBRA_DEV_BRIDGE` | Enables the bridge initially |
| `AETHER_UMBRA_DEV_BRIDGE_PORT` | Bridge port from 1024 to 65535 |
| `AETHER_UMBRA_DEV_BRIDGE_DIR` | Bridge state/capture directory |
| `AETHER_UMBRA_DEV_BRIDGE_CONTROL` | Bridge control JSON path |

Launcher-controlled injection also supplies bootstrap/framework paths, load
delay, safe mode, repositories, and Wine managed-host preference. Plugins should
use the plugin context rather than reading Launcher variables directly.

## Build and test

```sh
dotnet build "AetherXIV Launcher/Umbra/Aether.Umbra.SamplePlugin/Aether.Umbra.SamplePlugin.csproj" -c Release
dotnet test AetherXIV.sln
```

Use `Aether.Umbra.SamplePlugin` as the current reference implementation. Package
the plugin DLL, its managed dependencies, and `umbra-plugin.json` together in a
ZIP when publishing through a repository.

## Current SDK limitations

- Native chat print and submit bindings remain unresolved.
- Appearance observations have an API/cache, but the native adapter is pending.
- Client interop is restricted to one recognized 1.23b executable hash.
- Third-party plugins receive no arbitrary memory-write, packet-mutation, or
  general unsafe-native API.
- API 2.0 is implemented, while Framework 0.1.0 remains pre-stable.
