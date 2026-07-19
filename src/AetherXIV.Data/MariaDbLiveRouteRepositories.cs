using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbCharacterCreationRepository : ICharacterCreationRepository
{
    private const int DuplicateKey = 1062;

    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterCreationRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<CharacterReservationResult> ReserveAsync(
        CharacterReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await LockAccountAsync(connection, transaction, request.AccountId, cancellationToken).ConfigureAwait(false);

        ushort activeCount = await GetActiveCharacterCountAsync(
            connection,
            transaction,
            request.AccountId,
            cancellationToken).ConfigureAwait(false);
        if (activeCount >= 8 || request.Slot != activeCount)
            return new CharacterReservationResult(CharacterReservationStatus.SlotUnavailable);

        CharacterReservationRequest canonicalRequest = request with { Slot = activeCount };

        if (await IsNameUnavailableAsync(connection, transaction, canonicalRequest, cancellationToken).ConfigureAwait(false))
            return new CharacterReservationResult(CharacterReservationStatus.NameUnavailable);

        CharacterReservationRecord? existing =
            await GetAccountReservationForUpdateAsync(connection, transaction, request.AccountId, cancellationToken).ConfigureAwait(false);

        try
        {
            CharacterReservationRecord reservation = existing is null
                ? await InsertReservationAsync(connection, transaction, canonicalRequest, cancellationToken).ConfigureAwait(false)
                : await UpdateReservationAsync(connection, transaction, existing.CharacterId, canonicalRequest, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new CharacterReservationResult(CharacterReservationStatus.Reserved, reservation);
        }
        catch (MySqlException ex) when (ex.Number == DuplicateKey)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new CharacterReservationResult(CharacterReservationStatus.NameUnavailable);
        }
    }

    public async Task<CharacterReservationRecord?> GetReservationAsync(
        AccountId accountId,
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, account_id, world_id, slot, name
FROM characters
WHERE account_id = @account_id
  AND character_id = @character_id
  AND creation_state = @reserved_state;
""";
        command.Parameters.AddWithValue("@account_id", accountId.Value);
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@reserved_state", CharacterCreationStates.Reserved);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadReservation(reader)
            : null;
    }

    public async Task<CharacterReservationRecord?> GetReservationForAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, account_id, world_id, slot, name
FROM characters
WHERE account_id = @account_id
  AND creation_state = @reserved_state
ORDER BY character_id DESC
LIMIT 1;
""";
        command.Parameters.AddWithValue("@account_id", accountId.Value);
        command.Parameters.AddWithValue("@reserved_state", CharacterCreationStates.Reserved);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadReservation(reader)
            : null;
    }

    public async Task<CharacterCreationResult> CreateAsync(
        CharacterCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await LockAccountAsync(connection, transaction, request.AccountId, cancellationToken).ConfigureAwait(false);

        uint characterId = request.ReservedCharacterId?.Value ?? 0;
        WorldId worldId = request.WorldId;
        ushort slot = request.Slot;
        string name = request.Name;
        if (!CharacterCreationPayloadParser.TryParse(request.AppearancePayload.Span, out CharacterCreationPayloadInfo creationInfo))
            throw new InvalidDataException("Character creation payload does not contain the required profile fields.");

        if (request.ReservedCharacterId is CharacterId reservedCharacterId)
        {
            CharacterReservationRecord reservation = await GetReservationForUpdateAsync(
                    connection,
                    transaction,
                    request.AccountId,
                    reservedCharacterId,
                    cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Character reservation {reservedCharacterId.Value} for account {request.AccountId.Value} was not found.");

            characterId = reservation.CharacterId.Value;
            worldId = reservation.WorldId;
            slot = reservation.Slot;
            name = reservation.Name;
            await ActivateReservedCharacterAsync(connection, transaction, request, reservation, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            characterId = await InsertActiveCharacterAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
        }

        await InsertAppearanceAsync(connection, transaction, characterId, name, request.AppearancePayload, creationInfo, cancellationToken).ConfigureAwait(false);
        await InsertClassStateAsync(connection, transaction, characterId, request.StartingClass, cancellationToken).ConfigureAwait(false);
        await InsertMapStateAsync(connection, transaction, characterId, request, cancellationToken).ConfigureAwait(false);
        await InsertProgressionStateAsync(connection, transaction, characterId, request.StartingTown, cancellationToken).ConfigureAwait(false);
        await InsertProfileAsync(connection, transaction, characterId, creationInfo, cancellationToken).ConfigureAwait(false);
        await InsertLoginStateAsync(connection, transaction, characterId, cancellationToken).ConfigureAwait(false);
        await InsertResourceStateAsync(
            connection,
            transaction,
            characterId,
            request.StartingClass,
            creationInfo.Tribe,
            level: 1,
            cancellationToken).ConfigureAwait(false);
        await InsertDefaultHotbarAsync(
            connection,
            transaction,
            characterId,
            request.StartingClass,
            cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        CharacterRecord character = new(
            new CharacterId(characterId),
            request.AccountId,
            worldId,
            name,
            request.StartingZoneId,
            request.PositionX,
            request.PositionY,
            request.PositionZ,
            request.Rotation,
            slot);

        return new CharacterCreationResult(character);
    }

    public async Task<CharacterRenameStatus> RenameAsync(
        AccountId accountId,
        CharacterId characterId,
        WorldId worldId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);

        await using (MySqlCommand availability = connection.CreateCommand())
        {
            availability.Transaction = transaction;
            availability.CommandText = """
SELECT 1
FROM characters
WHERE world_id = @world_id
  AND name = @name
  AND character_id <> @character_id
  AND (creation_state = @active_state OR account_id <> @account_id)
LIMIT 1;
""";
            availability.Parameters.AddWithValue("@world_id", worldId.Value);
            availability.Parameters.AddWithValue("@name", newName.Trim());
            availability.Parameters.AddWithValue("@character_id", characterId.Value);
            availability.Parameters.AddWithValue("@account_id", accountId.Value);
            availability.Parameters.AddWithValue("@active_state", CharacterCreationStates.Active);
            object? unavailable = await availability.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (unavailable is not null && unavailable is not DBNull)
                return CharacterRenameStatus.NameUnavailable;
        }

        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
UPDATE characters
SET name = @name
WHERE character_id = @character_id
  AND account_id = @account_id
  AND world_id = @world_id
  AND creation_state = @active_state;
""";
        command.Parameters.AddWithValue("@name", newName.Trim());
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@account_id", accountId.Value);
        command.Parameters.AddWithValue("@world_id", worldId.Value);
        command.Parameters.AddWithValue("@active_state", CharacterCreationStates.Active);
        try
        {
            int changed = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return changed == 1 ? CharacterRenameStatus.Renamed : CharacterRenameStatus.NotFound;
        }
        catch (MySqlException ex) when (ex.Number == DuplicateKey)
        {
            return CharacterRenameStatus.NameUnavailable;
        }
    }

    public async Task<CharacterDeleteStatus> DeleteAsync(
        AccountId accountId,
        CharacterId characterId,
        string expectedName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedName);
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await LockAccountAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);

        ushort? deletedSlot;
        await using (MySqlCommand find = connection.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = """
SELECT slot
FROM characters
WHERE character_id = @character_id
  AND account_id = @account_id
  AND name = @name
  AND creation_state = @active_state
FOR UPDATE;
""";
            find.Parameters.AddWithValue("@character_id", characterId.Value);
            find.Parameters.AddWithValue("@account_id", accountId.Value);
            find.Parameters.AddWithValue("@name", expectedName);
            find.Parameters.AddWithValue("@active_state", CharacterCreationStates.Active);
            object? value = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            deletedSlot = value is null || value is DBNull ? null : Convert.ToUInt16(value);
        }

        if (deletedSlot is null)
            return CharacterDeleteStatus.NotFound;

        await using (MySqlCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
UPDATE characters
SET creation_state = @deleted_state,
    deleted_at = UTC_TIMESTAMP()
WHERE character_id = @character_id
  AND account_id = @account_id
  AND name = @name
  AND creation_state = @active_state;
""";
            command.Parameters.AddWithValue("@deleted_state", CharacterCreationStates.Deleted);
            command.Parameters.AddWithValue("@character_id", characterId.Value);
            command.Parameters.AddWithValue("@account_id", accountId.Value);
            command.Parameters.AddWithValue("@name", expectedName);
            command.Parameters.AddWithValue("@active_state", CharacterCreationStates.Active);
            if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                return CharacterDeleteStatus.NotFound;
        }

        await using (MySqlCommand compact = connection.CreateCommand())
        {
            compact.Transaction = transaction;
            compact.CommandText = """
UPDATE characters
SET slot = slot + 1024
WHERE account_id = @account_id
  AND creation_state = @active_state
  AND slot > @deleted_slot;

UPDATE characters
SET slot = slot - 1025
WHERE account_id = @account_id
  AND creation_state = @active_state
  AND slot >= 1024;

UPDATE characters AS reservation
SET reservation.slot = (
  SELECT active.active_count
  FROM (
    SELECT COUNT(*) AS active_count
    FROM characters
    WHERE account_id = @account_id
      AND creation_state = @active_state
  ) AS active
)
WHERE reservation.account_id = @account_id
  AND reservation.creation_state = @reserved_state;

DELETE FROM world_login_handoffs
WHERE character_id = @character_id;
""";
            compact.Parameters.AddWithValue("@account_id", accountId.Value);
            compact.Parameters.AddWithValue("@active_state", CharacterCreationStates.Active);
            compact.Parameters.AddWithValue("@reserved_state", CharacterCreationStates.Reserved);
            compact.Parameters.AddWithValue("@deleted_slot", deletedSlot.Value);
            compact.Parameters.AddWithValue("@character_id", characterId.Value);
            await compact.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return CharacterDeleteStatus.Deleted;
    }

    private static async Task<bool> IsNameUnavailableAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterReservationRequest request,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
SELECT 1
FROM characters
WHERE world_id = @world_id
  AND name = @name
  AND (creation_state = @active_state OR account_id <> @account_id)
LIMIT 1;
""";
        command.Parameters.AddWithValue("@world_id", request.WorldId.Value);
        command.Parameters.AddWithValue("@name", request.Name);
        command.Parameters.AddWithValue("@account_id", request.AccountId.Value);
        command.Parameters.AddWithValue("@active_state", CharacterCreationStates.Active);
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is not null && value is not DBNull;
    }

    private static async Task LockAccountAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AccountId accountId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
SELECT account_id
FROM accounts
WHERE account_id = @account_id
FOR UPDATE;
""";
        command.Parameters.AddWithValue("@account_id", accountId.Value);
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (value is null || value is DBNull)
            throw new InvalidOperationException($"Account {accountId.Value} was not found.");
    }

    private static async Task<ushort> GetActiveCharacterCountAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AccountId accountId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
SELECT COUNT(*)
FROM characters
WHERE account_id = @account_id
  AND creation_state = @active_state;
""";
        command.Parameters.AddWithValue("@account_id", accountId.Value);
        command.Parameters.AddWithValue("@active_state", CharacterCreationStates.Active);
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return checked((ushort)Convert.ToUInt32(value));
    }

    private static async Task<CharacterReservationRecord?> GetAccountReservationForUpdateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AccountId accountId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
SELECT character_id, account_id, world_id, slot, name
FROM characters
WHERE account_id = @account_id
  AND creation_state = @reserved_state
ORDER BY character_id DESC
LIMIT 1
FOR UPDATE;
""";
        command.Parameters.AddWithValue("@account_id", accountId.Value);
        command.Parameters.AddWithValue("@reserved_state", CharacterCreationStates.Reserved);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadReservation(reader)
            : null;
    }

    private static async Task<CharacterReservationRecord?> GetReservationForUpdateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AccountId accountId,
        CharacterId characterId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
SELECT character_id, account_id, world_id, slot, name
FROM characters
WHERE account_id = @account_id
  AND character_id = @character_id
  AND creation_state = @reserved_state
FOR UPDATE;
""";
        command.Parameters.AddWithValue("@account_id", accountId.Value);
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@reserved_state", CharacterCreationStates.Reserved);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadReservation(reader)
            : null;
    }

    private static async Task<CharacterReservationRecord> InsertReservationAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterReservationRequest request,
        CancellationToken cancellationToken)
    {
        uint characterId;
        await using (MySqlCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
INSERT INTO characters (account_id, world_id, slot, name, current_zone_id, position_x, position_y, position_z, rotation, creation_state)
VALUES (@account_id, @world_id, @slot, @name, 0, 0, 0, 0, 0, @reserved_state);
SELECT LAST_INSERT_ID();
""";
            command.Parameters.AddWithValue("@account_id", request.AccountId.Value);
            command.Parameters.AddWithValue("@world_id", request.WorldId.Value);
            command.Parameters.AddWithValue("@slot", request.Slot);
            command.Parameters.AddWithValue("@name", request.Name);
            command.Parameters.AddWithValue("@reserved_state", CharacterCreationStates.Reserved);
            characterId = Convert.ToUInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        }

        return new CharacterReservationRecord(new CharacterId(characterId), request.AccountId, request.WorldId, request.Slot, request.Name);
    }

    private static async Task<CharacterReservationRecord> UpdateReservationAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterId characterId,
        CharacterReservationRequest request,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
UPDATE characters
SET world_id = @world_id,
    slot = @slot,
    name = @name
WHERE character_id = @character_id
  AND account_id = @account_id
  AND creation_state = @reserved_state;
""";
        command.Parameters.AddWithValue("@world_id", request.WorldId.Value);
        command.Parameters.AddWithValue("@slot", request.Slot);
        command.Parameters.AddWithValue("@name", request.Name);
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@account_id", request.AccountId.Value);
        command.Parameters.AddWithValue("@reserved_state", CharacterCreationStates.Reserved);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return new CharacterReservationRecord(characterId, request.AccountId, request.WorldId, request.Slot, request.Name);
    }

    private static async Task<uint> InsertActiveCharacterAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterCreateRequest request,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO characters (account_id, world_id, slot, name, current_zone_id, position_x, position_y, position_z, rotation, creation_state)
VALUES (@account_id, @world_id, @slot, @name, @zone_id, @x, @y, @z, @rotation, @active_state);
SELECT LAST_INSERT_ID();
""";
        command.Parameters.AddWithValue("@account_id", request.AccountId.Value);
        command.Parameters.AddWithValue("@world_id", request.WorldId.Value);
        command.Parameters.AddWithValue("@slot", request.Slot);
        command.Parameters.AddWithValue("@name", request.Name);
        command.Parameters.AddWithValue("@zone_id", request.StartingZoneId.Value);
        command.Parameters.AddWithValue("@x", request.PositionX);
        command.Parameters.AddWithValue("@y", request.PositionY);
        command.Parameters.AddWithValue("@z", request.PositionZ);
        command.Parameters.AddWithValue("@rotation", request.Rotation);
        command.Parameters.AddWithValue("@active_state", CharacterCreationStates.Active);
        return Convert.ToUInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private static async Task ActivateReservedCharacterAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterCreateRequest request,
        CharacterReservationRecord reservation,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
UPDATE characters
SET current_zone_id = @zone_id,
    position_x = @x,
    position_y = @y,
    position_z = @z,
    rotation = @rotation,
    creation_state = @active_state
WHERE character_id = @character_id
  AND account_id = @account_id
  AND creation_state = @reserved_state;
""";
        command.Parameters.AddWithValue("@zone_id", request.StartingZoneId.Value);
        command.Parameters.AddWithValue("@x", request.PositionX);
        command.Parameters.AddWithValue("@y", request.PositionY);
        command.Parameters.AddWithValue("@z", request.PositionZ);
        command.Parameters.AddWithValue("@rotation", request.Rotation);
        command.Parameters.AddWithValue("@active_state", CharacterCreationStates.Active);
        command.Parameters.AddWithValue("@character_id", reservation.CharacterId.Value);
        command.Parameters.AddWithValue("@account_id", request.AccountId.Value);
        command.Parameters.AddWithValue("@reserved_state", CharacterCreationStates.Reserved);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static CharacterReservationRecord ReadReservation(MySqlDataReader reader)
    {
        return new CharacterReservationRecord(
            new CharacterId(reader.GetUInt32("character_id")),
            new AccountId(reader.GetUInt32("account_id")),
            new WorldId(reader.GetUInt32("world_id")),
            reader.GetUInt16("slot"),
            reader.GetString("name"));
    }

    private static async Task InsertAppearanceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint characterId,
        string characterName,
        ReadOnlyMemory<byte> payload,
        CharacterCreationPayloadInfo creationInfo,
        CancellationToken cancellationToken)
    {
        CharacterAppearancePayloadParser.TryParseCreationPayload(
            new CharacterId(characterId),
            payload.Span,
            out CharacterAppearanceRecord? appearance);
        if (appearance is not null)
            appearance = CharacterStartingEquipment.Apply(appearance, creationInfo.StartingClass);

        byte[] lobbyPayload = appearance is null
            ? payload.ToArray()
            : LobbyAppearancePayloadBuilder.Build(characterName, appearance, creationInfo);

        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_appearance (
    character_id, payload_json, model_id, tribe, size, hair_style, hair_highlight_color,
    hair_variation, face_type, characteristics, characteristics_color, face_eyebrows,
    face_iris_size, face_eye_shape, face_nose, face_features, face_mouth, ears,
    hair_color, skin_color, eye_color, voice, main_hand, off_hand, sp_main_hand,
    sp_off_hand, throwing, pack, pouch, head, body, legs, hands, feet, waist, neck,
    left_ear, right_ear, left_wrist, right_wrist, left_index, right_index, left_finger, right_finger)
VALUES (
    @character_id, JSON_OBJECT('encoding', 'base64', 'payload', @payload), @model_id, @tribe,
    @size, @hair_style, @hair_highlight_color, @hair_variation, @face_type,
    @characteristics, @characteristics_color, @face_eyebrows, @face_iris_size,
    @face_eye_shape, @face_nose, @face_features, @face_mouth, @ears, @hair_color,
    @skin_color, @eye_color, @voice, @main_hand, @off_hand, @sp_main_hand, @sp_off_hand,
    @throwing, @pack, @pouch, @head, @body, @legs, @hands, @feet, @waist, @neck,
    @left_ear, @right_ear, @left_wrist, @right_wrist, @left_index, @right_index, @left_finger, @right_finger);
""";
        command.Parameters.AddWithValue("@character_id", characterId);
        command.Parameters.AddWithValue("@payload", Convert.ToBase64String(lobbyPayload));
        AddAppearanceParameters(command, appearance);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddAppearanceParameters(MySqlCommand command, CharacterAppearanceRecord? appearance)
    {
        command.Parameters.AddWithValue("@model_id", ToDbValue(appearance?.ModelId));
        command.Parameters.AddWithValue("@tribe", ToDbValue(appearance?.Tribe));
        command.Parameters.AddWithValue("@size", ToDbValue(appearance?.Size));
        command.Parameters.AddWithValue("@hair_style", ToDbValue(appearance?.HairStyle));
        command.Parameters.AddWithValue("@hair_highlight_color", ToDbValue(appearance?.HairHighlightColor));
        command.Parameters.AddWithValue("@hair_variation", ToDbValue(appearance?.HairVariation));
        command.Parameters.AddWithValue("@face_type", ToDbValue(appearance?.FaceType));
        command.Parameters.AddWithValue("@characteristics", ToDbValue(appearance?.Characteristics));
        command.Parameters.AddWithValue("@characteristics_color", ToDbValue(appearance?.CharacteristicsColor));
        command.Parameters.AddWithValue("@face_eyebrows", ToDbValue(appearance?.FaceEyebrows));
        command.Parameters.AddWithValue("@face_iris_size", ToDbValue(appearance?.FaceIrisSize));
        command.Parameters.AddWithValue("@face_eye_shape", ToDbValue(appearance?.FaceEyeShape));
        command.Parameters.AddWithValue("@face_nose", ToDbValue(appearance?.FaceNose));
        command.Parameters.AddWithValue("@face_features", ToDbValue(appearance?.FaceFeatures));
        command.Parameters.AddWithValue("@face_mouth", ToDbValue(appearance?.FaceMouth));
        command.Parameters.AddWithValue("@ears", ToDbValue(appearance?.Ears));
        command.Parameters.AddWithValue("@hair_color", ToDbValue(appearance?.HairColor));
        command.Parameters.AddWithValue("@skin_color", ToDbValue(appearance?.SkinColor));
        command.Parameters.AddWithValue("@eye_color", ToDbValue(appearance?.EyeColor));
        command.Parameters.AddWithValue("@voice", ToDbValue(appearance?.Voice));
        command.Parameters.AddWithValue("@main_hand", ToDbValue(appearance?.MainHand));
        command.Parameters.AddWithValue("@off_hand", ToDbValue(appearance?.OffHand));
        command.Parameters.AddWithValue("@sp_main_hand", ToDbValue(appearance?.SpMainHand));
        command.Parameters.AddWithValue("@sp_off_hand", ToDbValue(appearance?.SpOffHand));
        command.Parameters.AddWithValue("@throwing", ToDbValue(appearance?.Throwing));
        command.Parameters.AddWithValue("@pack", ToDbValue(appearance?.Pack));
        command.Parameters.AddWithValue("@pouch", ToDbValue(appearance?.Pouch));
        command.Parameters.AddWithValue("@head", ToDbValue(appearance?.Head));
        command.Parameters.AddWithValue("@body", ToDbValue(appearance?.Body));
        command.Parameters.AddWithValue("@legs", ToDbValue(appearance?.Legs));
        command.Parameters.AddWithValue("@hands", ToDbValue(appearance?.Hands));
        command.Parameters.AddWithValue("@feet", ToDbValue(appearance?.Feet));
        command.Parameters.AddWithValue("@waist", ToDbValue(appearance?.Waist));
        command.Parameters.AddWithValue("@neck", ToDbValue(appearance?.Neck));
        command.Parameters.AddWithValue("@left_ear", ToDbValue(appearance?.LeftEar));
        command.Parameters.AddWithValue("@right_ear", ToDbValue(appearance?.RightEar));
        command.Parameters.AddWithValue("@left_wrist", ToDbValue(appearance?.LeftWrist));
        command.Parameters.AddWithValue("@right_wrist", ToDbValue(appearance?.RightWrist));
        command.Parameters.AddWithValue("@left_index", ToDbValue(appearance?.LeftIndex));
        command.Parameters.AddWithValue("@right_index", ToDbValue(appearance?.RightIndex));
        command.Parameters.AddWithValue("@left_finger", ToDbValue(appearance?.LeftFinger));
        command.Parameters.AddWithValue("@right_finger", ToDbValue(appearance?.RightFinger));
    }

    private static object ToDbValue<T>(T? value)
        where T : struct
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static async Task InsertClassStateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint characterId,
        byte startingClass,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_class_state (character_id, class_id, level, experience, is_current)
VALUES (@character_id, @class_id, 1, 0, 1);
""";
        command.Parameters.AddWithValue("@character_id", characterId);
        command.Parameters.AddWithValue("@class_id", startingClass);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertMapStateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint characterId,
        CharacterCreateRequest request,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO map_character_state (character_id, zone_id, private_area_name, private_area_level, position_x, position_y, position_z, rotation)
VALUES (@character_id, @zone_id, NULL, 0, @x, @y, @z, @rotation);
""";
        command.Parameters.AddWithValue("@character_id", characterId);
        command.Parameters.AddWithValue("@zone_id", request.StartingZoneId.Value);
        command.Parameters.AddWithValue("@x", request.PositionX);
        command.Parameters.AddWithValue("@y", request.PositionY);
        command.Parameters.AddWithValue("@z", request.PositionZ);
        command.Parameters.AddWithValue("@rotation", request.Rotation);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertProgressionStateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint characterId,
        byte initialTown,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_progression_state (character_id, initial_town, play_time_seconds, home_point, home_point_inn)
VALUES (@character_id, @initial_town, 0, 0, 0)
ON DUPLICATE KEY UPDATE
  initial_town = VALUES(initial_town);
""";
        command.Parameters.AddWithValue("@character_id", characterId);
        command.Parameters.AddWithValue("@initial_town", initialTown);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertProfileAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint characterId,
        CharacterCreationPayloadInfo creationInfo,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_profile (character_id, guardian, birth_month, birth_day)
VALUES (@character_id, @guardian, @birth_month, @birth_day)
ON DUPLICATE KEY UPDATE
  guardian = VALUES(guardian),
  birth_month = VALUES(birth_month),
  birth_day = VALUES(birth_day);
""";
        command.Parameters.AddWithValue("@character_id", characterId);
        command.Parameters.AddWithValue("@guardian", creationInfo.Guardian);
        command.Parameters.AddWithValue("@birth_month", creationInfo.BirthMonth);
        command.Parameters.AddWithValue("@birth_day", creationInfo.BirthDay);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertLoginStateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint characterId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_login_state (character_id)
VALUES (@character_id)
ON DUPLICATE KEY UPDATE character_id = VALUES(character_id);
""";
        command.Parameters.AddWithValue("@character_id", characterId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertResourceStateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint characterId,
        byte classJob,
        byte tribe,
        ushort level,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_resource_state (character_id, current_hp, current_mp, current_tp)
SELECT
  @character_id,
  LEAST(65535, ROUND((base_hp + vitality * hp_vitality_factor) * hp_multiplier)),
  LEAST(65535, ROUND((base_mp + piety * mp_piety_factor) * mp_multiplier)),
  0
FROM player_base_stat_profiles
WHERE class_job IN (@class_job, 0)
  AND tribe IN (@tribe, 0)
  AND level = @level
ORDER BY (class_job = @class_job) DESC, (tribe = @tribe) DESC
LIMIT 1
ON DUPLICATE KEY UPDATE
  current_hp = VALUES(current_hp),
  current_mp = VALUES(current_mp),
  current_tp = VALUES(current_tp);
""";
        command.Parameters.AddWithValue("@character_id", characterId);
        command.Parameters.AddWithValue("@class_job", classJob);
        command.Parameters.AddWithValue("@tribe", tribe);
        command.Parameters.AddWithValue("@level", level);
        int affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new InvalidOperationException(
                $"No player base stat profile exists for class {classJob}, tribe {tribe}, level {level}.");
        }
    }

    private static async Task InsertDefaultHotbarAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        uint characterId,
        byte startingClass,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_hotbar_slots (character_id, class_id, slot_index, command_id, recast_end)
SELECT @character_id,
       @class_id,
       ROW_NUMBER() OVER (ORDER BY command_id DESC) - 1,
       command_id,
       0
FROM battle_commands
WHERE class_job = @class_id
  AND level = 1
ORDER BY command_id DESC;
""";
        command.Parameters.AddWithValue("@character_id", characterId);
        command.Parameters.AddWithValue("@class_id", startingClass);
        int inserted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (inserted == 0)
        {
            throw new InvalidOperationException(
                $"No level-one battle commands exist for supported starting class {startingClass}.");
        }
    }
}

public sealed class MariaDbCharacterAppearancePayloadRepository : ICharacterAppearanceRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterAppearancePayloadRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<ReadOnlyMemory<byte>?> GetLobbyPayloadAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT JSON_UNQUOTE(JSON_EXTRACT(payload_json, '$.payload')) AS payload
FROM character_appearance
WHERE character_id = @character_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        object? raw = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (raw is null || raw is DBNull)
            return null;

        return Convert.FromBase64String(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture) ?? String.Empty);
    }

    public async Task<CharacterAppearanceRecord?> GetAppearanceAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, model_id, tribe, size, hair_style, hair_highlight_color, hair_variation,
       face_type, characteristics, characteristics_color, face_eyebrows, face_iris_size,
       face_eye_shape, face_nose, face_features, face_mouth, ears, hair_color, skin_color,
       eye_color, voice, main_hand, off_hand, sp_main_hand, sp_off_hand, throwing, pack,
       pouch, head, body, legs, hands, feet, waist, neck, left_ear, right_ear, left_index,
       right_index, left_finger, right_finger, left_wrist, right_wrist
FROM character_appearance
WHERE character_id = @character_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        if (IsNull(reader, "model_id"))
            return null;

        return ReadAppearance(reader);
    }

    private static CharacterAppearanceRecord ReadAppearance(MySqlDataReader reader)
    {
        return new CharacterAppearanceRecord(
            new CharacterId(reader.GetUInt32("character_id")),
            reader.GetUInt32("model_id"),
            reader.GetByte("tribe"),
            reader.GetUInt32("size"),
            reader.GetUInt32("hair_style"),
            reader.GetUInt32("hair_highlight_color"),
            reader.GetUInt32("hair_variation"),
            reader.GetByte("face_type"),
            reader.GetByte("characteristics"),
            reader.GetByte("characteristics_color"),
            reader.GetByte("face_eyebrows"),
            reader.GetByte("face_iris_size"),
            reader.GetByte("face_eye_shape"),
            reader.GetByte("face_nose"),
            reader.GetByte("face_features"),
            reader.GetByte("face_mouth"),
            reader.GetByte("ears"),
            reader.GetUInt32("hair_color"),
            reader.GetUInt32("skin_color"),
            reader.GetUInt32("eye_color"),
            reader.GetUInt32("voice"),
            reader.GetUInt32("main_hand"),
            reader.GetUInt32("off_hand"),
            reader.GetUInt32("sp_main_hand"),
            reader.GetUInt32("sp_off_hand"),
            reader.GetUInt32("throwing"),
            reader.GetUInt32("pack"),
            reader.GetUInt32("pouch"),
            reader.GetUInt32("head"),
            reader.GetUInt32("body"),
            reader.GetUInt32("legs"),
            reader.GetUInt32("hands"),
            reader.GetUInt32("feet"),
            reader.GetUInt32("waist"),
            reader.GetUInt32("neck"),
            reader.GetUInt32("left_ear"),
            reader.GetUInt32("right_ear"),
            ReadUInt32OrZero(reader, "left_wrist"),
            ReadUInt32OrZero(reader, "right_wrist"),
            reader.GetUInt32("left_index"),
            reader.GetUInt32("right_index"),
            reader.GetUInt32("left_finger"),
            reader.GetUInt32("right_finger"));
    }

    private static uint ReadUInt32OrZero(MySqlDataReader reader, string column)
    {
        return reader.IsDBNull(reader.GetOrdinal(column)) ? 0 : reader.GetUInt32(column);
    }

    private static bool IsNull(MySqlDataReader reader, string column)
    {
        return reader.IsDBNull(reader.GetOrdinal(column));
    }
}

public sealed class MariaDbCharacterLoadoutRepository : ICharacterLoadoutRepository, ICharacterLoadoutMutationRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterLoadoutRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CharacterClassStateRecord>> ListClassStatesAsync(CharacterId characterId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, class_id, level, experience, is_current
FROM character_class_state
WHERE character_id = @character_id
ORDER BY class_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        List<CharacterClassStateRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CharacterClassStateRecord(
                new CharacterId(reader.GetUInt32("character_id")),
                reader.GetByte("class_id"),
                reader.GetUInt16("level"),
                reader.GetUInt32("experience"),
                reader.GetBoolean("is_current")));
        }

        return rows;
    }

    public async Task<IReadOnlyList<CharacterEquipmentSlotRecord>> ListEquipmentAsync(CharacterId characterId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, slot_id, item_id, dye_id, server_item_id, inventory_container_id, inventory_slot_id
FROM character_equipment_slots
WHERE character_id = @character_id
ORDER BY slot_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        List<CharacterEquipmentSlotRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CharacterEquipmentSlotRecord(
                new CharacterId(reader.GetUInt32("character_id")),
                reader.GetUInt16("slot_id"),
                reader.GetUInt32("item_id"),
                reader.GetUInt32("dye_id"),
                reader.GetUInt32("server_item_id"),
                reader.GetByte("inventory_container_id"),
                reader.GetUInt16("inventory_slot_id")));
        }

        return rows;
    }

    public async Task<IReadOnlyList<CharacterInventoryItemRecord>> ListInventoryAsync(CharacterId characterId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, container_id, slot_id, item_id, quantity, server_item_id, quality
FROM character_inventory_items
WHERE character_id = @character_id
ORDER BY container_id, slot_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        List<CharacterInventoryItemRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CharacterInventoryItemRecord(
                new CharacterId(reader.GetUInt32("character_id")),
                reader.GetByte("container_id"),
                reader.GetUInt16("slot_id"),
                reader.GetUInt32("item_id"),
                reader.GetUInt16("quantity"),
                reader.GetUInt32("server_item_id"),
                reader.GetByte("quality")));
        }

        return rows;
    }

    public async Task SaveClassStatesAsync(
        IReadOnlyList<CharacterClassStateRecord> states,
        CancellationToken cancellationToken = default)
    {
        if (states.Count == 0)
            return;

        CharacterId characterId = states[0].CharacterId;
        if (states.Any(state => state.CharacterId != characterId))
            throw new ArgumentException("Class-state batches must belong to one character.", nameof(states));
        if (states.Count(state => state.IsCurrent) != 1)
            throw new ArgumentException("Class-state batches must contain exactly one current class.", nameof(states));

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (MySqlCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "UPDATE character_class_state SET is_current = 0 WHERE character_id = @character_id;";
            clear.Parameters.AddWithValue("@character_id", characterId.Value);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (CharacterClassStateRecord state in states.OrderBy(state => state.ClassId))
        {
            await using MySqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
INSERT INTO character_class_state (character_id, class_id, level, experience, is_current)
VALUES (@character_id, @class_id, @level, @experience, @is_current)
ON DUPLICATE KEY UPDATE level = VALUES(level), experience = VALUES(experience), is_current = VALUES(is_current);
""";
            command.Parameters.AddWithValue("@character_id", state.CharacterId.Value);
            command.Parameters.AddWithValue("@class_id", state.ClassId);
            command.Parameters.AddWithValue("@level", state.Level);
            command.Parameters.AddWithValue("@experience", state.Experience);
            command.Parameters.AddWithValue("@is_current", state.IsCurrent);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveInventoryItemsAsync(
        IReadOnlyList<CharacterInventoryItemRecord> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            return;

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (CharacterInventoryItemRecord item in items)
            await UpsertInventoryItemAsync(connection, transaction, item, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveEquipmentSlotsAsync(
        IReadOnlyList<CharacterEquipmentSlotRecord> slots,
        CancellationToken cancellationToken = default)
    {
        if (slots.Count == 0)
            return;

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (CharacterEquipmentSlotRecord slot in slots)
            await UpsertEquipmentSlotAsync(connection, transaction, slot, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertInventoryItemAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterInventoryItemRecord item,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_inventory_items (character_id, container_id, slot_id, item_id, quantity, server_item_id, quality)
VALUES (@character_id, @container_id, @slot_id, @item_id, @quantity, @server_item_id, @quality)
ON DUPLICATE KEY UPDATE item_id = VALUES(item_id), quantity = VALUES(quantity),
    server_item_id = VALUES(server_item_id), quality = VALUES(quality);
""";
        command.Parameters.AddWithValue("@character_id", item.CharacterId.Value);
        command.Parameters.AddWithValue("@container_id", item.ContainerId);
        command.Parameters.AddWithValue("@slot_id", item.SlotId);
        command.Parameters.AddWithValue("@item_id", item.ItemId);
        command.Parameters.AddWithValue("@quantity", item.Quantity);
        command.Parameters.AddWithValue("@server_item_id", item.ServerItemId);
        command.Parameters.AddWithValue("@quality", item.Quality);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertEquipmentSlotAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterEquipmentSlotRecord slot,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_equipment_slots (character_id, slot_id, item_id, dye_id, server_item_id, inventory_container_id, inventory_slot_id)
VALUES (@character_id, @slot_id, @item_id, @dye_id, @server_item_id, @inventory_container_id, @inventory_slot_id)
ON DUPLICATE KEY UPDATE item_id = VALUES(item_id), dye_id = VALUES(dye_id),
    server_item_id = VALUES(server_item_id),
    inventory_container_id = VALUES(inventory_container_id),
    inventory_slot_id = VALUES(inventory_slot_id);
""";
        command.Parameters.AddWithValue("@character_id", slot.CharacterId.Value);
        command.Parameters.AddWithValue("@slot_id", slot.SlotId);
        command.Parameters.AddWithValue("@item_id", slot.ItemId);
        command.Parameters.AddWithValue("@dye_id", slot.DyeId);
        command.Parameters.AddWithValue("@server_item_id", slot.ServerItemId);
        command.Parameters.AddWithValue("@inventory_container_id", slot.InventoryContainerId);
        command.Parameters.AddWithValue("@inventory_slot_id", slot.InventorySlotId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class MariaDbItemVisualRepository : IItemVisualRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbItemVisualRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<ItemVisualRecord?> GetAsync(uint itemId, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<uint, ItemVisualRecord> rows = await ListAsync([itemId], cancellationToken).ConfigureAwait(false);
        return rows.TryGetValue(itemId, out ItemVisualRecord? row) ? row : null;
    }

    public async Task<IReadOnlyDictionary<uint, ItemVisualRecord>> ListAsync(
        IEnumerable<uint> itemIds,
        CancellationToken cancellationToken = default)
    {
        uint[] ids = itemIds
            .Where(itemId => itemId != 0)
            .Distinct()
            .Order()
            .ToArray();
        if (ids.Length == 0)
            return new Dictionary<uint, ItemVisualRecord>();

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        string[] parameterNames = new string[ids.Length];
        for (int index = 0; index < ids.Length; index++)
        {
            string parameterName = "@item_id_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            parameterNames[index] = parameterName;
            command.Parameters.AddWithValue(parameterName, ids[index]);
        }

        command.CommandText = $"""
SELECT iv.item_id, iv.weapon_id, iv.equipment_id, iv.variant_id, iv.color_id,
       iv.off_hand_weapon_id, iv.off_hand_equipment_id, iv.off_hand_variant_id,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM item_visuals iv
LEFT JOIN provenance_refs p ON p.provenance_id = iv.provenance_id
WHERE iv.item_id IN ({String.Join(", ", parameterNames)})
ORDER BY iv.item_id;
""";

        Dictionary<uint, ItemVisualRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ItemVisualRecord row = ReadItemVisual(reader);
            rows[row.ItemId] = row;
        }

        return rows;
    }

    private static ItemVisualRecord ReadItemVisual(MySqlDataReader reader)
    {
        uint itemId = reader.GetUInt32("item_id");
        return new ItemVisualRecord(
            itemId,
            reader.GetUInt32("weapon_id"),
            reader.GetUInt32("equipment_id"),
            reader.GetUInt32("variant_id"),
            reader.GetUInt32("color_id"),
            reader.GetUInt32("off_hand_weapon_id"),
            reader.GetUInt32("off_hand_equipment_id"),
            reader.GetUInt32("off_hand_variant_id"),
            ReadProvenance(reader, itemId));
    }

    private static ProvenanceRef ReadProvenance(MySqlDataReader reader, uint itemId)
    {
        if (reader.IsDBNull(reader.GetOrdinal("evidence_status")))
            return new ProvenanceRef(EvidenceStatus.Unknown, "database", $"item_visuals:{itemId}", String.Empty);

        string rawStatus = reader.GetString("evidence_status");
        EvidenceStatus status = Enum.TryParse(rawStatus, ignoreCase: false, out EvidenceStatus parsed)
            ? parsed
            : EvidenceStatus.Unknown;
        return new ProvenanceRef(
            status,
            reader.GetString("source_type"),
            reader.GetString("source_ref"),
            reader.GetString("notes"));
    }
}

public sealed class MariaDbPlayerStatRepository : IPlayerStatRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbPlayerStatRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<PlayerStatRecord>> ListStatsAsync(CharacterId characterId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, stat_id, stat_value, source
FROM player_stats
WHERE character_id = @character_id
ORDER BY stat_id, source;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        List<PlayerStatRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new PlayerStatRecord(
                new CharacterId(reader.GetUInt32("character_id")),
                reader.GetUInt16("stat_id"),
                reader.GetInt32("stat_value"),
                reader.GetString("source")));
        }

        return rows;
    }
}

public sealed class MariaDbPlayerBaseStatProfileRepository : IPlayerBaseStatProfileRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbPlayerBaseStatProfileRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<PlayerBaseStatProfileRecord?> GetAsync(
        byte classJob,
        byte tribe,
        ushort level,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT profile.class_job, profile.tribe, profile.level, profile.base_hp, profile.base_mp,
       profile.strength, profile.dexterity, profile.vitality, profile.intelligence,
       profile.mind, profile.piety, profile.hp_vitality_factor, profile.mp_piety_factor,
       profile.hp_multiplier, profile.mp_multiplier,
       provenance.evidence_status, provenance.source_type, provenance.source_ref, provenance.notes
FROM player_base_stat_profiles profile
JOIN provenance_refs provenance ON provenance.provenance_id = profile.provenance_id
WHERE profile.class_job IN (@class_job, 0)
  AND profile.tribe IN (@tribe, 0)
  AND profile.level <= @level
ORDER BY
  (profile.class_job = @class_job) DESC,
  (profile.tribe = @tribe) DESC,
  profile.level DESC
LIMIT 1;
""";
        command.Parameters.AddWithValue("@class_job", classJob);
        command.Parameters.AddWithValue("@tribe", tribe);
        command.Parameters.AddWithValue("@level", level);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new PlayerBaseStatProfileRecord(
            reader.GetByte("class_job"),
            reader.GetByte("tribe"),
            reader.GetUInt16("level"),
            reader.GetUInt16("base_hp"),
            reader.GetUInt16("base_mp"),
            reader.GetUInt16("strength"),
            reader.GetUInt16("dexterity"),
            reader.GetUInt16("vitality"),
            reader.GetUInt16("intelligence"),
            reader.GetUInt16("mind"),
            reader.GetUInt16("piety"),
            reader.GetDecimal("hp_vitality_factor"),
            reader.GetDecimal("mp_piety_factor"),
            reader.GetDecimal("hp_multiplier"),
            reader.GetDecimal("mp_multiplier"),
            new ProvenanceRef(
                Enum.Parse<EvidenceStatus>(reader.GetString("evidence_status")),
                reader.GetString("source_type"),
                reader.GetString("source_ref"),
                reader.GetString("notes")));
    }
}

public sealed class MariaDbCharacterProfileRepository : ICharacterProfileRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterProfileRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<CharacterProfileRecord?> GetAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT profile.character_id, profile.guardian, profile.birth_month, profile.birth_day, profile.updated_at,
       JSON_UNQUOTE(JSON_EXTRACT(appearance.payload_json, '$.payload')) AS lobby_payload
FROM character_profile profile
LEFT JOIN character_appearance appearance ON appearance.character_id = profile.character_id
WHERE profile.character_id = @character_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        CharacterProfileRecord profile = new(
            new CharacterId(reader.GetUInt32("character_id")),
            reader.GetByte("guardian"),
            reader.GetByte("birth_month"),
            reader.GetByte("birth_day"),
            ReadUtc(reader, "updated_at"));
        string? encodedPayload = reader.IsDBNull(reader.GetOrdinal("lobby_payload"))
            ? null
            : reader.GetString("lobby_payload");
        await reader.DisposeAsync().ConfigureAwait(false);

        if ((profile.Guardian != 0 || profile.BirthMonth != 0 || profile.BirthDay != 0)
            || String.IsNullOrWhiteSpace(encodedPayload))
        {
            return profile;
        }

        byte[] lobbyPayload;
        try
        {
            lobbyPayload = Convert.FromBase64String(encodedPayload);
        }
        catch (FormatException)
        {
            return profile;
        }

        if (!LobbyAppearancePayloadProfileParser.TryParse(lobbyPayload, out CharacterCreationPayloadInfo recovered))
            return profile;

        await using MySqlCommand update = connection.CreateCommand();
        update.CommandText = """
UPDATE character_profile
SET guardian = @guardian, birth_month = @birth_month, birth_day = @birth_day
WHERE character_id = @character_id;
""";
        update.Parameters.AddWithValue("@guardian", recovered.Guardian);
        update.Parameters.AddWithValue("@birth_month", recovered.BirthMonth);
        update.Parameters.AddWithValue("@birth_day", recovered.BirthDay);
        update.Parameters.AddWithValue("@character_id", characterId.Value);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return profile with
        {
            Guardian = recovered.Guardian,
            BirthMonth = recovered.BirthMonth,
            BirthDay = recovered.BirthDay,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static DateTimeOffset ReadUtc(MySqlDataReader reader, string column)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(column), DateTimeKind.Utc));
    }
}

public sealed class MariaDbCharacterResourceStateRepository : ICharacterResourceStateRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterResourceStateRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<CharacterResourceStateRecord?> GetAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, current_hp, current_mp, current_tp, updated_at
FROM character_resource_state
WHERE character_id = @character_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new CharacterResourceStateRecord(
            new CharacterId(reader.GetUInt32("character_id")),
            reader.GetUInt16("current_hp"),
            reader.GetUInt16("current_mp"),
            reader.GetUInt16("current_tp"),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime("updated_at"), DateTimeKind.Utc)));
    }

    public async Task SaveAsync(
        CharacterResourceStateRecord state,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO character_resource_state (character_id, current_hp, current_mp, current_tp)
VALUES (@character_id, @current_hp, @current_mp, @current_tp)
ON DUPLICATE KEY UPDATE
  current_hp = VALUES(current_hp),
  current_mp = VALUES(current_mp),
  current_tp = VALUES(current_tp);
""";
        command.Parameters.AddWithValue("@character_id", state.CharacterId.Value);
        command.Parameters.AddWithValue("@current_hp", state.CurrentHitPoints);
        command.Parameters.AddWithValue("@current_mp", state.CurrentMagicPoints);
        command.Parameters.AddWithValue("@current_tp", state.CurrentTacticalPoints);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class MariaDbWorldHandoffTicketRepository : IWorldHandoffTicketRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbWorldHandoffTicketRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<MapHandoffTicketRecord> CreateAsync(
        CharacterId characterId,
        WorldId worldId,
        ZoneId zoneId,
        ServerEndpoint mapEndpoint,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        string ticket = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO map_handoff_tickets (ticket, character_id, world_id, zone_id, map_host, map_port, expires_at)
VALUES (@ticket, @character_id, @world_id, @zone_id, @map_host, @map_port, @expires_at);
""";
        command.Parameters.AddWithValue("@ticket", ticket);
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@world_id", worldId.Value);
        command.Parameters.AddWithValue("@zone_id", zoneId.Value);
        command.Parameters.AddWithValue("@map_host", mapEndpoint.Host);
        command.Parameters.AddWithValue("@map_port", mapEndpoint.Port);
        command.Parameters.AddWithValue("@expires_at", expiresAt.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return new MapHandoffTicketRecord(ticket, characterId, worldId, zoneId, mapEndpoint, expiresAt, null);
    }

    public async Task<MapHandoffTicketRecord?> ConsumeAsync(string ticket, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticket);

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        MapHandoffTicketRecord? record;
        await using (MySqlCommand select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
SELECT ticket, character_id, world_id, zone_id, map_host, map_port, expires_at, consumed_at
FROM map_handoff_tickets
WHERE ticket = @ticket
  AND consumed_at IS NULL
  AND expires_at > UTC_TIMESTAMP()
FOR UPDATE;
""";
            select.Parameters.AddWithValue("@ticket", ticket);
            await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            record = await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadTicket(reader) : null;
        }

        if (record is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        await using (MySqlCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE map_handoff_tickets SET consumed_at = UTC_TIMESTAMP() WHERE ticket = @ticket;";
            update.Parameters.AddWithValue("@ticket", ticket);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return record with { ConsumedAt = DateTimeOffset.UtcNow };
    }

    private static MapHandoffTicketRecord ReadTicket(MySqlDataReader reader)
    {
        return new MapHandoffTicketRecord(
            reader.GetString("ticket"),
            new CharacterId(reader.GetUInt32("character_id")),
            new WorldId(reader.GetUInt32("world_id")),
            new ZoneId(reader.GetUInt32("zone_id")),
            new ServerEndpoint(reader.GetString("map_host"), reader.GetUInt16("map_port")),
            ReadUtc(reader, "expires_at"),
            reader.IsDBNull(reader.GetOrdinal("consumed_at")) ? null : ReadUtc(reader, "consumed_at"));
    }

    private static DateTimeOffset ReadUtc(MySqlDataReader reader, string name)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(name), DateTimeKind.Utc));
    }
}

public sealed class MariaDbMapCharacterStateRepository : IMapCharacterStateRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbMapCharacterStateRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<CharacterMapStateRecord?> GetAsync(CharacterId characterId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, zone_id, private_area_name, private_area_level, position_x, position_y, position_z, rotation, updated_at
FROM map_character_state
WHERE character_id = @character_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new CharacterMapStateRecord(
            new CharacterId(reader.GetUInt32("character_id")),
            new ZoneId(reader.GetUInt32("zone_id")),
            reader.IsDBNull(reader.GetOrdinal("private_area_name")) ? null : reader.GetString("private_area_name"),
            reader.GetUInt32("private_area_level"),
            reader.GetFloat("position_x"),
            reader.GetFloat("position_y"),
            reader.GetFloat("position_z"),
            reader.GetFloat("rotation"),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime("updated_at"), DateTimeKind.Utc)));
    }

    public async Task SaveAsync(CharacterMapStateRecord state, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO map_character_state (character_id, zone_id, private_area_name, private_area_level, position_x, position_y, position_z, rotation)
VALUES (@character_id, @zone_id, @private_area_name, @private_area_level, @x, @y, @z, @rotation)
ON DUPLICATE KEY UPDATE
  zone_id = VALUES(zone_id),
  private_area_name = VALUES(private_area_name),
  private_area_level = VALUES(private_area_level),
  position_x = VALUES(position_x),
  position_y = VALUES(position_y),
  position_z = VALUES(position_z),
  rotation = VALUES(rotation);
""";
        command.Parameters.AddWithValue("@character_id", state.CharacterId.Value);
        command.Parameters.AddWithValue("@zone_id", state.ZoneId.Value);
        command.Parameters.AddWithValue("@private_area_name", (object?)state.PrivateAreaName ?? DBNull.Value);
        command.Parameters.AddWithValue("@private_area_level", state.PrivateAreaLevel);
        command.Parameters.AddWithValue("@x", state.PositionX);
        command.Parameters.AddWithValue("@y", state.PositionY);
        command.Parameters.AddWithValue("@z", state.PositionZ);
        command.Parameters.AddWithValue("@rotation", state.Rotation);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class MariaDbCharacterProgressionRepository : ICharacterProgressionRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterProgressionRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<CharacterProgressionStateRecord?> GetAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, initial_town, play_time_seconds, home_point, home_point_inn,
       rest_bonus_exp_rate, updated_at
FROM character_progression_state
WHERE character_id = @character_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new CharacterProgressionStateRecord(
            new CharacterId(reader.GetUInt32("character_id")),
            reader.GetByte("initial_town"),
            reader.GetUInt32("play_time_seconds"),
            reader.GetUInt32("home_point"),
            reader.GetByte("home_point_inn"),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime("updated_at"), DateTimeKind.Utc)),
            reader.GetDecimal("rest_bonus_exp_rate"));
    }

    public async Task SaveAsync(CharacterProgressionStateRecord state, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO character_progression_state (
  character_id, initial_town, play_time_seconds, home_point, home_point_inn,
  rest_bonus_exp_rate
)
VALUES (
  @character_id, @initial_town, @play_time_seconds, @home_point, @home_point_inn,
  @rest_bonus_exp_rate
)
ON DUPLICATE KEY UPDATE
  initial_town = VALUES(initial_town),
  play_time_seconds = VALUES(play_time_seconds),
  home_point = VALUES(home_point),
  home_point_inn = VALUES(home_point_inn),
  rest_bonus_exp_rate = VALUES(rest_bonus_exp_rate);
""";
        command.Parameters.AddWithValue("@character_id", state.CharacterId.Value);
        command.Parameters.AddWithValue("@initial_town", state.InitialTown);
        command.Parameters.AddWithValue("@play_time_seconds", state.PlayTimeSeconds);
        command.Parameters.AddWithValue("@home_point", state.HomePoint);
        command.Parameters.AddWithValue("@home_point_inn", state.HomePointInn);
        command.Parameters.AddWithValue("@rest_bonus_exp_rate", state.RestBonusExpRate);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class MariaDbCharacterQuestStateRepository : ICharacterQuestSnapshotRepository, ICharacterQuestProgressionRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterQuestStateRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CharacterQuestStateRecord>> ListAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, quest_id, quest_name, slot_index, phase, flags, quest_data_json, completed, updated_at
FROM character_quest_state
WHERE character_id = @character_id
ORDER BY completed, slot_index, quest_id, quest_name;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);

        List<CharacterQuestStateRecord> quests = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            quests.Add(new CharacterQuestStateRecord(
                new CharacterId(reader.GetUInt32("character_id")),
                reader.GetUInt32("quest_id"),
                reader.GetString("quest_name"),
                reader.GetUInt32("phase"),
                reader.GetUInt32("flags"),
                reader.GetByte("completed") != 0,
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime("updated_at"), DateTimeKind.Utc)),
                reader.IsDBNull(reader.GetOrdinal("slot_index")) ? null : reader.GetByte("slot_index"),
                reader.GetString("quest_data_json")));
        }

        return quests;
    }

    public async Task SaveAsync(CharacterQuestStateRecord quest, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO character_quest_state (character_id, quest_id, quest_name, slot_index, phase, flags, quest_data_json, completed)
VALUES (@character_id, @quest_id, @quest_name, @slot_index, @phase, @flags, @quest_data_json, @completed)
ON DUPLICATE KEY UPDATE
  slot_index = VALUES(slot_index),
  phase = VALUES(phase),
  flags = VALUES(flags),
  quest_data_json = VALUES(quest_data_json),
  completed = VALUES(completed);
""";
        command.Parameters.AddWithValue("@character_id", quest.CharacterId.Value);
        command.Parameters.AddWithValue("@quest_id", quest.QuestId);
        command.Parameters.AddWithValue("@quest_name", quest.QuestName);
        command.Parameters.AddWithValue("@slot_index", quest.SlotIndex is byte slotIndex ? slotIndex : DBNull.Value);
        command.Parameters.AddWithValue("@phase", quest.Phase);
        command.Parameters.AddWithValue("@flags", quest.Flags);
        command.Parameters.AddWithValue("@quest_data_json", quest.QuestDataJson);
        command.Parameters.AddWithValue("@completed", quest.Completed ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplaceAllAsync(
        CharacterId characterId,
        IReadOnlyList<CharacterQuestStateRecord> quests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quests);
        if (quests.Any(quest => quest.CharacterId != characterId))
            throw new ArgumentException("Quest snapshot contains a row for another character.", nameof(quests));
        if (quests.Where(quest => !quest.Completed && quest.SlotIndex.HasValue)
            .GroupBy(quest => quest.SlotIndex)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Quest snapshot assigns more than one active quest to a journal slot.", nameof(quests));
        }

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (MySqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM character_quest_state WHERE character_id = @character_id;";
                delete.Parameters.AddWithValue("@character_id", characterId.Value);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (CharacterQuestStateRecord quest in quests)
            {
                await using MySqlCommand insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
INSERT INTO character_quest_state
    (character_id, quest_id, quest_name, slot_index, phase, flags, quest_data_json, completed)
VALUES
    (@character_id, @quest_id, @quest_name, @slot_index, @phase, @flags, @quest_data_json, @completed);
""";
                insert.Parameters.AddWithValue("@character_id", quest.CharacterId.Value);
                insert.Parameters.AddWithValue("@quest_id", quest.QuestId);
                insert.Parameters.AddWithValue("@quest_name", quest.QuestName);
                insert.Parameters.AddWithValue("@slot_index", quest.Completed || !quest.SlotIndex.HasValue
                    ? DBNull.Value
                    : quest.SlotIndex.Value);
                insert.Parameters.AddWithValue("@phase", quest.Phase);
                insert.Parameters.AddWithValue("@flags", quest.Flags);
                insert.Parameters.AddWithValue("@quest_data_json", quest.QuestDataJson);
                insert.Parameters.AddWithValue("@completed", quest.Completed ? 1 : 0);
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task CommitAsync(
        CharacterQuestProgressionCommit commit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commit);
        if (commit.Progression.CharacterId != commit.CharacterId
            || commit.Quests.Any(quest => quest.CharacterId != commit.CharacterId))
            throw new ArgumentException("Progression commit contains state for another character.", nameof(commit));

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (MySqlCommand characterLock = connection.CreateCommand())
            {
                characterLock.Transaction = transaction;
                characterLock.CommandText = "SELECT character_id FROM characters WHERE character_id=@character_id FOR UPDATE;";
                characterLock.Parameters.AddWithValue("@character_id", commit.CharacterId.Value);
                if (await characterLock.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                    throw new InvalidOperationException($"Character {commit.CharacterId.Value} no longer exists.");
            }

            await using (MySqlCommand progression = connection.CreateCommand())
            {
                progression.Transaction = transaction;
                progression.CommandText = """
INSERT INTO character_progression_state
  (character_id, initial_town, play_time_seconds, home_point, home_point_inn, rest_bonus_exp_rate)
VALUES (@character_id,@initial_town,@play_time,@home_point,@home_point_inn,@rest_bonus)
ON DUPLICATE KEY UPDATE initial_town=VALUES(initial_town), play_time_seconds=VALUES(play_time_seconds),
  home_point=VALUES(home_point), home_point_inn=VALUES(home_point_inn), rest_bonus_exp_rate=VALUES(rest_bonus_exp_rate);
""";
                progression.Parameters.AddWithValue("@character_id", commit.CharacterId.Value);
                progression.Parameters.AddWithValue("@initial_town", commit.Progression.InitialTown);
                progression.Parameters.AddWithValue("@play_time", commit.Progression.PlayTimeSeconds);
                progression.Parameters.AddWithValue("@home_point", commit.Progression.HomePoint);
                progression.Parameters.AddWithValue("@home_point_inn", commit.Progression.HomePointInn);
                progression.Parameters.AddWithValue("@rest_bonus", commit.Progression.RestBonusExpRate);
                await progression.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (MySqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM character_quest_state WHERE character_id=@character_id;";
                delete.Parameters.AddWithValue("@character_id", commit.CharacterId.Value);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            foreach (CharacterQuestStateRecord quest in commit.Quests)
            {
                await using MySqlCommand insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
INSERT INTO character_quest_state
 (character_id,quest_id,quest_name,slot_index,phase,flags,quest_data_json,completed)
VALUES (@character_id,@quest_id,@quest_name,@slot_index,@phase,@flags,@quest_data_json,@completed);
""";
                insert.Parameters.AddWithValue("@character_id", commit.CharacterId.Value);
                insert.Parameters.AddWithValue("@quest_id", quest.QuestId);
                insert.Parameters.AddWithValue("@quest_name", quest.QuestName);
                insert.Parameters.AddWithValue("@slot_index", quest.Completed || !quest.SlotIndex.HasValue ? DBNull.Value : quest.SlotIndex.Value);
                insert.Parameters.AddWithValue("@phase", quest.Phase);
                insert.Parameters.AddWithValue("@flags", quest.Flags);
                insert.Parameters.AddWithValue("@quest_data_json", quest.QuestDataJson);
                insert.Parameters.AddWithValue("@completed", quest.Completed ? 1 : 0);
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}

public sealed class MariaDbTutorialCheckpointRepository : ITutorialCheckpointRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbTutorialCheckpointRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<TutorialCheckpointRecord?> GetAsync(CharacterId characterId, string directorName, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, director_name, checkpoint_name, checkpoint_state, payload_json, updated_at
FROM tutorial_checkpoints
WHERE character_id = @character_id AND director_name = @director_name;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@director_name", directorName);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new TutorialCheckpointRecord(
            new CharacterId(reader.GetUInt32("character_id")),
            reader.GetString("director_name"),
            reader.GetString("checkpoint_name"),
            Enum.Parse<TutorialCheckpointState>(reader.GetString("checkpoint_state")),
            reader.GetString("payload_json"),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime("updated_at"), DateTimeKind.Utc)));
    }

    public async Task SaveAsync(TutorialCheckpointRecord checkpoint, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO tutorial_checkpoints (character_id, director_name, checkpoint_name, checkpoint_state, payload_json)
VALUES (@character_id, @director_name, @checkpoint_name, @checkpoint_state, @payload_json)
ON DUPLICATE KEY UPDATE
  checkpoint_name = VALUES(checkpoint_name),
  checkpoint_state = VALUES(checkpoint_state),
  payload_json = VALUES(payload_json);
""";
        command.Parameters.AddWithValue("@character_id", checkpoint.CharacterId.Value);
        command.Parameters.AddWithValue("@director_name", checkpoint.DirectorName);
        command.Parameters.AddWithValue("@checkpoint_name", checkpoint.CheckpointName);
        command.Parameters.AddWithValue("@checkpoint_state", checkpoint.State.ToString());
        command.Parameters.AddWithValue("@payload_json", checkpoint.PayloadJson);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
