require("global");

properties = {
    permissions = 0,
    parameters = "dd",
    description = "spawns a known-good shell actor and applies an appearance id for safe visual review",
}

function onTrigger(player, argc, appearanceId, shellActorClassId)
    local messageID = MESSAGE_TYPE_SYSTEM_ERROR;
    local sender = "[previewappearance] ";

    local appearance = tonumber(appearanceId) or 0;
    local shell = tonumber(shellActorClassId) or 2104001;

    if appearance == 0 then
        player:SendMessage(messageID, sender, "Use !previewappearance <appearanceId> [safeShellActorClassId].");
        return;
    end

    local pos = player:GetPos();
    local zone = player:GetZone();
    local uniqueId = string.format("appearance_preview_%d_%d", appearance, math.random(1000000, 9999999));
    local actor = zone:SpawnActor(shell, uniqueId, pos[0] + 2, pos[1], pos[2] + 2, pos[3], 0, 0, true);

    if actor == nil then
        player:SendMessage(messageID, sender, string.format("Safe shell actor class %d cannot be spawned.", shell));
        return;
    end

    actor.ChangeNpcAppearance(appearance);
    actor.SetCustomDisplayName(string.format("app %d", appearance));
    player:SendMessage(messageID, sender, string.format("Spawned shell %d with appearance %d.", shell, appearance));
end;
