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
