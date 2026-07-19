using System.Net;

namespace AetherXIV.Server.Hosting;

public sealed record LegacyConnectionContext(
    Guid ConnectionId,
    string ServiceName,
    EndPoint? RemoteEndPoint,
    DateTimeOffset ConnectedAtUtc);
