# Single image serving all three AetherXIV services (lobby/world/map).
# AETHERXIV_SERVICE (or the first CLI arg) selects which one runs at container
# start; see src/AetherXIV.Server.Host for the selection logic.

ARG DOTNET_SDK_TAG=10.0
ARG DOTNET_RUNTIME_TAG=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/AetherXIV.Core/AetherXIV.Core.csproj src/AetherXIV.Core/
COPY src/AetherXIV.Protocol/AetherXIV.Protocol.csproj src/AetherXIV.Protocol/
COPY src/AetherXIV.Data/AetherXIV.Data.csproj src/AetherXIV.Data/
COPY src/AetherXIV.Scripting/AetherXIV.Scripting.csproj src/AetherXIV.Scripting/
COPY src/AetherXIV.Server.Hosting/AetherXIV.Server.Hosting.csproj src/AetherXIV.Server.Hosting/
COPY src/AetherXIV.Lobby/AetherXIV.Lobby.csproj src/AetherXIV.Lobby/
COPY src/AetherXIV.World/AetherXIV.World.csproj src/AetherXIV.World/
COPY src/AetherXIV.Map/AetherXIV.Map.csproj src/AetherXIV.Map/
COPY src/AetherXIV.Compatibility/AetherXIV.Compatibility.csproj src/AetherXIV.Compatibility/
COPY src/EchoGate.Next.Core/EchoGate.Next.Core.csproj src/EchoGate.Next.Core/
COPY src/AetherXIV.Server.Host/AetherXIV.Server.Host.csproj src/AetherXIV.Server.Host/

# Restoring the exe pulls in every project it references.
RUN dotnet restore src/AetherXIV.Server.Host/AetherXIV.Server.Host.csproj

COPY src/ src/
RUN dotnet publish src/AetherXIV.Server.Host/AetherXIV.Server.Host.csproj \
    -c Release \
    -o /app \
    --no-restore

# Plain TCP services, no ASP.NET Core needed.
FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_RUNTIME_TAG}
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "AetherXIV.Server.Host.dll"]
