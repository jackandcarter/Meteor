using System.Buffers.Binary;
using System.Text;
using AetherXIV.Core;
using AetherXIV.Core.Common;

namespace AetherXIV.Data;

public readonly record struct CharacterCreationPayloadInfo(
    byte Tribe,
    byte Guardian,
    byte BirthMonth,
    byte BirthDay,
    byte StartingClass,
    byte InitialTown);

public static class CharacterCreationPayloadParser
{
    private const int MinimumPayloadSize = 0x49;

    public static bool TryParse(ReadOnlySpan<byte> payload, out CharacterCreationPayloadInfo info)
    {
        info = default;
        if (payload.Length < MinimumPayloadSize)
            return false;

        byte tribe = payload[0x08];
        if (!PlayableCharacterIdentity.IsValidTribe(tribe))
            return false;

        info = new CharacterCreationPayloadInfo(
            tribe,
            payload[0x27],
            payload[0x28],
            payload[0x29],
            checked((byte)BinaryPrimitives.ReadUInt16LittleEndian(payload[0x2A..])),
            payload[0x48]);
        return true;
    }
}

public static class LobbyAppearancePayloadProfileParser
{
    public static bool TryParse(ReadOnlySpan<byte> payload, out CharacterCreationPayloadInfo info)
    {
        info = default;
        int encodedLength = payload.IndexOf((byte)0);
        if (encodedLength < 0)
            encodedLength = payload.Length;
        if (encodedLength == 0)
            return false;

        try
        {
            string encoded = Encoding.ASCII.GetString(payload[..encodedLength])
                .Replace('-', '+')
                .Replace('_', '/');
            int padding = encoded.Length % 4;
            if (padding != 0)
                encoded = encoded.PadRight(encoded.Length + (4 - padding), '=');

            byte[] raw = Convert.FromBase64String(encoded);
            using MemoryStream stream = new(raw, writable: false);
            using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);

            reader.ReadUInt32();
            reader.ReadUInt32();
            ReadLengthPrefixed(reader);
            reader.ReadUInt32();
            reader.ReadUInt32();

            for (int index = 0; index < 6; index++)
                reader.ReadUInt32();
            for (int index = 0; index < 20; index++)
                reader.ReadUInt32();

            reader.BaseStream.Seek(8, SeekOrigin.Current);
            reader.ReadUInt32();
            reader.ReadUInt32();
            byte startingClass = reader.ReadByte();
            reader.ReadUInt16();
            reader.ReadByte();
            reader.ReadUInt16();
            byte tribe = reader.ReadByte();
            if (!PlayableCharacterIdentity.IsValidTribe(tribe))
                return false;
            reader.ReadUInt32();
            ReadLengthPrefixed(reader);
            ReadLengthPrefixed(reader);
            byte guardian = reader.ReadByte();
            byte birthMonth = reader.ReadByte();
            byte birthDay = reader.ReadByte();
            reader.ReadUInt16();
            reader.ReadUInt32();
            reader.ReadUInt32();
            reader.BaseStream.Seek(0x10, SeekOrigin.Current);
            byte initialTown = checked((byte)reader.ReadUInt32());

            info = new CharacterCreationPayloadInfo(
                tribe,
                guardian,
                birthMonth,
                birthDay,
                startingClass,
                initialTown);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or EndOfStreamException or IOException or OverflowException)
        {
            return false;
        }
    }

    private static void ReadLengthPrefixed(BinaryReader reader)
    {
        uint length = reader.ReadUInt32();
        long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        if (length > remaining)
            throw new EndOfStreamException("Length-prefixed lobby value exceeds the remaining payload.");

        reader.BaseStream.Seek(length, SeekOrigin.Current);
    }
}

public static class CharacterStartingEquipment
{
    private static readonly IReadOnlyDictionary<byte, uint[]> EquipmentByClass = new Dictionary<byte, uint[]>
    {
        [2] = [60818432, 60818432, 0, 0, 0, 0, 0, 0, 10656, 10560, 1024, 25824, 6144],
        [3] = [79692890, 0, 0, 0, 0, 0, 0, 0, 31776, 4448, 1024, 25824, 6144],
        [4] = [147850310, 0, 0, 0, 0, 0, 0, 23713, 0, 10016, 5472, 1152, 6144],
        [7] = [210764860, 236979210, 0, 0, 0, 231736320, 0, 0, 9888, 9984, 1024, 25824, 6144],
        [8] = [168823858, 0, 0, 0, 0, 0, 0, 0, 13920, 7200, 1024, 10656, 6144],
        [22] = [294650980, 0, 0, 0, 0, 0, 0, 0, 7744, 5472, 1024, 5504, 4096],
        [23] = [347079700, 0, 0, 0, 0, 0, 0, 0, 4448, 2240, 1024, 4416, 4096],
        [29] = [705692672, 0, 0, 0, 0, 0, 0, 0, 0, 10016, 10656, 9632, 2048],
        [30] = [721421372, 0, 0, 0, 0, 0, 0, 0, 0, 2241, 2336, 2304, 2048],
        [31] = [737149962, 0, 0, 0, 0, 0, 0, 0, 32992, 2240, 1024, 2272, 2048],
        [32] = [752878592, 0, 0, 0, 0, 0, 0, 0, 2368, 3424, 1024, 10656, 2048],
        [33] = [768607252, 0, 0, 0, 0, 0, 0, 4448, 4449, 1792, 1024, 21888, 2048],
        [34] = [784335922, 0, 0, 0, 0, 0, 0, 0, 5505, 5473, 1024, 5505, 2048],
        [35] = [800064522, 0, 0, 0, 0, 0, 0, 20509, 5504, 2241, 1024, 1152, 2048],
        [36] = [815793192, 0, 0, 0, 0, 0, 0, 5632, 34848, 1792, 1024, 25825, 2048],
        [39] = [862979092, 0, 0, 0, 0, 0, 0, 0, 1184, 2242, 6464, 6528, 14336],
        [40] = [878707732, 0, 0, 0, 0, 0, 0, 6304, 6624, 6560, 1024, 1152, 14336],
        [41] = [894436372, 0, 0, 0, 0, 0, 0, 6400, 1184, 9984, 1024, 6529, 14336]
    };

    public static CharacterAppearanceRecord Apply(CharacterAppearanceRecord appearance, byte startingClass)
    {
        if (!EquipmentByClass.TryGetValue(startingClass, out uint[]? equipment))
            return appearance;

        uint body = equipment[8] == 0 ? GetUndershirtForTribe(appearance.Tribe) : equipment[8];
        return appearance with
        {
            MainHand = equipment[0],
            OffHand = equipment[1],
            Head = equipment[7],
            Body = body,
            Legs = equipment[9],
            Hands = equipment[10],
            Feet = equipment[11],
            Waist = equipment[12]
        };
    }

    private static uint GetUndershirtForTribe(byte tribe)
    {
        return tribe switch
        {
            1 => 1184,
            2 => 1186,
            3 => 1187,
            4 => 1184,
            5 => 1024,
            6 => 1187,
            7 => 1505,
            8 => 1184,
            9 => 1185,
            10 => 1504,
            11 => 1505,
            12 => 1216,
            13 => 1186,
            14 => 1184,
            15 => 1186,
            _ => 0
        };
    }
}

public static class LobbyAppearancePayloadBuilder
{
    private const int PayloadSize = 0x190;
    private const int RawPayloadSize = 0xF5;

    public static byte[] Build(
        string characterName,
        CharacterAppearanceRecord appearance,
        CharacterCreationPayloadInfo creationInfo,
        ushort currentLevel = 1) =>
        Build(characterName, appearance, creationInfo, creationInfo.StartingClass, currentLevel);

    public static byte[] Build(
        string characterName,
        CharacterAppearanceRecord appearance,
        CharacterCreationPayloadInfo creationInfo,
        byte currentClass,
        ushort currentLevel)
    {
        byte[] raw = new byte[RawPayloadSize];
        using MemoryStream rawStream = new(raw, writable: true);
        using (BinaryWriter writer = new(rawStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((uint)0x000004C0);
            writer.Write((uint)0x232327EA);
            WriteUtf8LengthPrefixed(writer, characterName + '\0');
            writer.Write((uint)0x1C);
            writer.Write((uint)0x04);
            writer.Write(appearance.ModelId);
            writer.Write(appearance.Size);
            writer.Write(ActorAppearanceConversion.BuildColorInfo(
                appearance.SkinColor,
                appearance.HairColor,
                appearance.EyeColor));
            writer.Write(ActorAppearanceConversion.BuildFaceInfo(
                appearance.Characteristics,
                appearance.CharacteristicsColor,
                appearance.FaceType,
                appearance.Ears,
                appearance.FaceMouth,
                appearance.FaceFeatures,
                appearance.FaceNose,
                appearance.FaceEyeShape,
                appearance.FaceIrisSize,
                appearance.FaceEyebrows));
            writer.Write(ActorAppearanceConversion.BuildHighlightHair(
                appearance.HairHighlightColor,
                appearance.HairVariation,
                appearance.HairStyle));
            writer.Write(appearance.Voice);
            writer.Write(appearance.MainHand);
            writer.Write(appearance.OffHand);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write(appearance.Head);
            writer.Write(appearance.Body);
            writer.Write(appearance.Legs);
            writer.Write(appearance.Hands);
            writer.Write(appearance.Feet);
            writer.Write(appearance.Waist);
            writer.Write(appearance.Neck);
            writer.Write(appearance.RightEar);
            writer.Write(appearance.LeftEar);
            writer.Write(appearance.RightIndex);
            writer.Write(appearance.LeftIndex);
            writer.Write(appearance.RightFinger);
            writer.Write(appearance.LeftFinger);

            for (int index = 0; index < 0x8; index++)
                writer.Write((byte)0);

            writer.Write((uint)1);
            writer.Write((uint)1);
            writer.Write(currentClass);
            writer.Write(currentLevel);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write(creationInfo.Tribe);
            writer.Write((uint)0xE22222AA);
            WriteUtf8LengthPrefixed(writer, "prv0Inn01\0");
            WriteUtf8LengthPrefixed(writer, "defaultTerritory\0");
            writer.Write(creationInfo.Guardian);
            writer.Write(creationInfo.BirthMonth);
            writer.Write(creationInfo.BirthDay);
            writer.Write((ushort)0x17);
            writer.Write((uint)4);
            writer.Write((uint)4);
            writer.Seek(0x10, SeekOrigin.Current);
            writer.Write((uint)creationInfo.InitialTown);
            writer.Write((uint)creationInfo.InitialTown);
        }

        byte[] payload = new byte[PayloadSize];
        string encoded = Convert.ToBase64String(raw).Replace('+', '-').Replace('/', '_');
        byte[] encodedBytes = Encoding.ASCII.GetBytes(encoded);
        if (encodedBytes.Length > payload.Length)
            throw new InvalidDataException($"Encoded lobby appearance payload exceeds {payload.Length} bytes.");

        encodedBytes.CopyTo(payload.AsSpan());
        return payload;
    }

    private static void WriteUtf8LengthPrefixed(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }
}

public readonly record struct CharacterStartingLocation(
    ZoneId ZoneId,
    float X,
    float Y,
    float Z,
    float Rotation);

public static class CharacterStartingLocations
{
    public static CharacterStartingLocation FromInitialTown(byte initialTown)
    {
        if (!TryFromInitialTown(initialTown, out CharacterStartingLocation location))
            throw new ArgumentOutOfRangeException(nameof(initialTown), initialTown, "Unsupported character starting town.");

        return location;
    }

    public static bool TryFromInitialTown(byte initialTown, out CharacterStartingLocation location)
    {
        return initialTown switch
        {
            1 => Return(new CharacterStartingLocation(new ZoneId(193), 0.016f, 10.35f, -36.91f, 0.025f), out location),
            2 => Return(new CharacterStartingLocation(new ZoneId(166), 369.5434f, 4.21f, -706.1074f, -1.26721f), out location),
            3 => Return(new CharacterStartingLocation(new ZoneId(184), 5.364327f, 196.0f, 133.6561f, -2.849384f), out location),
            _ => Return(default, out location, false)
        };
    }

    private static bool Return(CharacterStartingLocation value, out CharacterStartingLocation location, bool result = true)
    {
        location = value;
        return result;
    }
}
