# Umbra SDK Sample

This is the first reference plugin for Umbra API 2.0. It demonstrates the
plugin lifecycle, scoped logging, configuration-directory ownership, and the
manifest shape used by the in-process host. It also draws a working SDK window
through Umbra's managed render bridge using the standard card, badge, toggle,
icon, artwork, typography, and primary-button components.

The sample registers `/umbra-sample` through the scoped command manager and
requests local chat output through the capability-gated chat service. Command
dispatch is active now. The game's own chat window remains unavailable until
the exact 1.23b native function and ABI have been verified;
`IUmbraChat.Availability` and each delivery result report that state.

Build it with:

```shell
dotnet build Aether.Umbra.SamplePlugin.csproj -c Release
```

Copy the contents of `bin/Release/net10.0` into one directory beneath the Umbra
plugin directory. The output includes `umbra-plugin.json`; Umbra discovers that
manifest and loads the entry assembly in its own collectible context.

`Update` and `Draw` run on the DX9 render thread. Keep both callbacks short and
move file, network, and other blocking work to plugin-owned background tasks.
Only use the drawing methods during `Draw`, always pair `BeginWindow` with
`EndWindow` in a `finally` block, and stop background work when the context's
shutdown token is cancelled.

The current API exposes framework UI, plugin command registration/dispatch,
and the stable chat transport boundary. Client object state, keybinds,
notifications, resource replacement, and appearance modification remain
research-backed future services; plugins must not assume raw client pointers
or Direct3D/ImGui access.
