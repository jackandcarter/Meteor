using AetherXIV.Core;
using AetherXIV.Protocol;
using System.Text;

namespace AetherXIV.Server.Hosting;

public sealed record WorldZoneChangeRouteRequest(
    uint SessionId,
    ZoneId DestinationZoneId,
    string? PrivateAreaName,
    uint PrivateAreaLevel,
    ushort SpawnType,
    float X,
    float Y,
    float Z,
    float Rotation);

public static class WorldMapRoutePackets
{
    public const int ZoneChangeRequestMinimumPayloadSize = 0x20;
    public const int ZoneChangeRequestPayloadSize = ZoneChangeRequestMinimumPayloadSize;

    public static byte[] EncodeZoneChangeRequest(WorldZoneChangeRouteRequest request)
    {
        byte[] privateAreaName = String.IsNullOrWhiteSpace(request.PrivateAreaName)
            ? []
            : Encoding.UTF8.GetBytes(request.PrivateAreaName);
        if (privateAreaName.Length > UInt16.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request), "Private area name is too long for the world-map route packet.");

        byte[] payload = new byte[ZoneChangeRequestPayloadSize + privateAreaName.Length];
        PacketBinary.WriteUInt32LittleEndian(payload, request.SessionId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x04), request.DestinationZoneId.Value);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x08), request.PrivateAreaLevel);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x0C), request.SpawnType);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(0x0E), request.X);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(0x12), request.Y);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(0x16), request.Z);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(0x1A), request.Rotation);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x1E), checked((ushort)privateAreaName.Length));
        privateAreaName.CopyTo(payload.AsSpan(ZoneChangeRequestPayloadSize));
        return payload;
    }

    public static WorldZoneChangeRouteRequest DecodeZoneChangeRequest(ReadOnlyMemory<byte> payload)
    {
        if (!TryDecodeZoneChangeRequest(payload, out WorldZoneChangeRouteRequest request))
            throw new InvalidDataException($"World zone-change request payload must be at least {ZoneChangeRequestMinimumPayloadSize} bytes.");

        return request;
    }

    public static bool TryDecodeZoneChangeRequest(
        ReadOnlyMemory<byte> payload,
        out WorldZoneChangeRouteRequest request)
    {
        request = default!;
        ReadOnlySpan<byte> span = payload.Span;
        if (span.Length < ZoneChangeRequestMinimumPayloadSize)
            return false;

        ushort privateAreaNameLength = PacketBinary.ReadUInt16LittleEndian(span[0x1E..]);
        if (span.Length < ZoneChangeRequestMinimumPayloadSize + privateAreaNameLength)
            return false;

        string? privateAreaName = privateAreaNameLength == 0
            ? null
            : Encoding.UTF8.GetString(span.Slice(ZoneChangeRequestPayloadSize, privateAreaNameLength));
        request = new WorldZoneChangeRouteRequest(
            PacketBinary.ReadUInt32LittleEndian(span),
            new ZoneId(PacketBinary.ReadUInt32LittleEndian(span[0x04..])),
            privateAreaName,
            PacketBinary.ReadUInt32LittleEndian(span[0x08..]),
            PacketBinary.ReadUInt16LittleEndian(span[0x0C..]),
            PacketBinary.ReadSingleLittleEndian(span[0x0E..]),
            PacketBinary.ReadSingleLittleEndian(span[0x12..]),
            PacketBinary.ReadSingleLittleEndian(span[0x16..]),
            PacketBinary.ReadSingleLittleEndian(span[0x1A..]));
        return true;
    }
}
