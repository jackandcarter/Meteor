require("global");

properties = {
    permissions = 0,
    parameters = "dddd",
    description = "spawns a server actor beside a safe appearance preview: !previewpair <serverActorClassId> <appearanceId> [shellActorClassId] [spacing]",
}

function onTrigger(player, argc, serverActorClassId, appearanceId, shellActorClassId, spacing)
    local messageID = MESSAGE_TYPE_SYSTEM_ERROR;
    local sender = "[previewpair] ";

    local serverId = tonumber(serverActorClassId) or 0;
    local appearance = tonumber(appearanceId) or 0;
    local shell = tonumber(shellActorClassId) or 2104001;
    local step = tonumber(spacing) or 5;

    if serverId == 0 or appearance == 0 then
        player:SendMessage(messageID, sender, "Use !previewpair <serverActorClassId> <appearanceId> [safeShellActorClassId] [spacing].");
        return;
    end

    local pos = player:GetPos();
    local zone = player:GetZone();
    local serverUniqueId = string.format("appearance_pair_server_%d_%d", serverId, appearance);
    local previewUniqueId = string.format("appearance_pair_preview_%d_%d", serverId, appearance);

    zone:DespawnActor(serverUniqueId);
    zone:DespawnActor(previewUniqueId);

    local baseX = pos[0] + 3;
    local baseZ = pos[2] + 3;
    local serverActor = zone:SpawnActor(serverId, serverUniqueId, baseX, pos[1], baseZ, pos[3], 0, 0, true);
    local previewActor = zone:SpawnActor(shell, previewUniqueId, baseX + step, pos[1], baseZ, pos[3], 0, 0, true);

    if serverActor ~= nil then
        serverActor.SetCustomDisplayName(string.format("srv %d", serverId));
    end

    if previewActor ~= nil then
        previewActor.ChangeNpcAppearance(appearance);
        previewActor.SetCustomDisplayName(string.format("app %d", appearance));
    end

    if serverActor == nil and previewActor == nil then
        player:SendMessage(messageID, sender, string.format("Could not spawn server actor %d or shell actor %d.", serverId, shell));
    elseif serverActor == nil then
        player:SendMessage(messageID, sender, string.format("Server actor %d could not spawn; previewed appearance %d with shell %d.", serverId, appearance, shell));
    elseif previewActor == nil then
        player:SendMessage(messageID, sender, string.format("Spawned server actor %d; shell actor %d could not spawn.", serverId, shell));
    else
        player:SendMessage(messageID, sender, string.format("Spawned srv %d beside app %d. Use !previewpairclear %d %d to clear.", serverId, appearance, serverId, appearance));
    end
end;
