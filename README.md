# Meteor
Project Meteor for XIV 1.x MacOS and Linux

## Solution layout
- `Lobby Server/`: Login and character selection service.
- `World Server/`: World-level routing and chat/zone session coordination.
- `Map Server/`: Zone/map simulation and gameplay state.
- `Common Class Lib/`: Shared libraries used by all servers.
- `Data/`: Runtime configuration, SQL, and web assets.

## High-level flow
1. Client connects to the lobby server to authenticate and select a character.
2. Lobby responds with the selected world server address/port.
3. Client connects to the world server, which manages sessions and hands off zone work to map servers.

## Startup order
Start the servers in this order so downstream dependencies are available:
1. **Lobby Server** (`Lobby Server/Program.cs`) starts the login/character selection host and listens on the lobby socket configured by `server_ip`/`server_port` in `lobby_config.ini`.
2. **World Server** (`World Server/Program.cs`) loads world data, connects to map servers, and listens for client world connections via its configured socket.
3. **Map Server** (`Map Server/Program.cs`) loads zone data and opens its configured socket for world connections.

The socket listeners are created in each server's `Server.StartServer()` implementation (`Lobby Server/Server.cs`, `World Server/Server.cs`, `Map Server/Server.cs`), where the server binds and listens before accepting connections.

## Config
Configuration files live in `Data/*_config.ini`:
- `Data/lobby_config.ini`
- `Data/world_config.ini`
- `Data/map_config.ini`

Common INI keys:
- **General**
  - `server_ip`: bind IP address for the server socket.
  - `server_port`: TCP port for the server socket.
  - `showtimestamp`: toggles timestamped logging.
- **Database**
  - `worldid`: world identifier for DB lookups.
  - `host`: database host.
  - `port`: database port.
  - `database`: schema name.
  - `username`: database user.
  - `password`: database password.

Default ports used when `server_port` is omitted are `54994` for the lobby server and `54992` for the world server; the map server defaults to `1989` unless configured otherwise.

## Client connection
When a player selects a character, the lobby server builds a `SelectCharacterConfirmPacket` in `Lobby Server/PacketProcessor.cs` that includes the world server address and port, which tells the client to reconnect to the world server after character selection completes.

## Build & run
The solution targets .NET Framework 4.7.2 and uses `packages.config` for NuGet restore, so the tooling differs slightly per OS. Make sure MariaDB/MySQL is reachable with credentials configured in the `Data/*_config.ini` files before starting the servers.

### Linux (Ubuntu, etc.)
1. Install Mono + MSBuild + NuGet (package names vary by distro; common ones are `mono-complete`, `msbuild`, and `nuget`).
2. Restore NuGet packages:
   ```
   nuget restore Meteor.sln
   ```
3. Build:
   ```
   msbuild Meteor.sln /p:Configuration=Release
   ```
4. Copy or verify the config files are in each output directory (they are marked to copy during build):
   - `Lobby Server/bin/Release/Data/lobby_config.ini`
   - `World Server/bin/Release/Data/world_config.ini`
   - `Map Server/bin/Release/Data/map_config.ini`
5. Run servers in order (from their output folders):
   ```
   mono "Lobby Server/bin/Release/Lobby Server.exe"
   mono "World Server/bin/Release/World Server.exe"
   mono "Map Server/bin/Release/Map Server.exe"
   ```

### macOS (Intel or Apple Silicon)
1. Install Mono and NuGet (e.g., via Homebrew).
2. Restore packages:
   ```
   nuget restore Meteor.sln
   ```
3. Build:
   ```
   msbuild Meteor.sln /p:Configuration=Release
   ```
4. Run servers in order:
   ```
   mono "Lobby Server/bin/Release/Lobby Server.exe"
   mono "World Server/bin/Release/World Server.exe"
   mono "Map Server/bin/Release/Map Server.exe"
   ```

### Windows
1. Install Visual Studio (with .NET Framework 4.7.2 developer pack) or MSBuild + NuGet.
2. Restore packages:
   ```
   nuget restore Meteor.sln
   ```
3. Build (Visual Studio or command line):
   ```
   msbuild Meteor.sln /p:Configuration=Release
   ```
4. Run servers in order from their output directories:
   ```
   "Lobby Server\bin\Release\Lobby Server.exe"
   "World Server\bin\Release\World Server.exe"
   "Map Server\bin\Release\Map Server.exe"
   ```
