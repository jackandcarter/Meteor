require("global");

properties = {
    permissions = 0,
    parameters = "dd",
    description = "clears a side-by-side appearance preview pair: !previewpairclear <serverActorClassId> <appearanceId>",
}

function onTrigger(player, argc, serverActorClassId, appearanceId)
    local messageID = MESSAGE_TYPE_SYSTEM_ERROR;
    local sender = "[previewpairclear] ";

    local serverId = tonumber(serverActorClassId) or 0;
    local appearance = tonumber(appearanceId) or 0;

    if serverId == 0 or appearance == 0 then
        player:SendMessage(messageID, sender, "Use !previewpairclear <serverActorClassId> <appearanceId>.");
        return;
    end

    local zone = player:GetZone();
    zone:DespawnActor(string.format("appearance_pair_server_%d_%d", serverId, appearance));
    zone:DespawnActor(string.format("appearance_pair_preview_%d_%d", serverId, appearance));

    player:SendMessage(messageID, sender, string.format("Cleared preview pair for server actor %d and appearance %d.", serverId, appearance));
end;
