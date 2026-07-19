require("global");

properties = {
    permissions = 0,
    parameters = "dddd",
    description = "spawns a labeled line of safe appearance previews: !previewrange <startAppearanceId> <count> [shellActorClassId] [spacing]",
}

function onTrigger(player, argc, startAppearanceId, count, shellActorClassId, spacing)
    local messageID = MESSAGE_TYPE_SYSTEM_ERROR;
    local sender = "[previewrange] ";

    local startId = tonumber(startAppearanceId) or 0;
    local total = tonumber(count) or 0;
    local shell = tonumber(shellActorClassId) or 2104001;
    local step = tonumber(spacing) or 4;

    if startId == 0 or total <= 0 then
        player:SendMessage(messageID, sender, "Use !previewrange <startAppearanceId> <count> [safeShellActorClassId] [spacing].");
        return;
    end

    if total > 30 then
        player:SendMessage(messageID, sender, "Count capped at 30 to keep the client stable.");
        total = 30;
    end

    local pos = player:GetPos();
    local zone = player:GetZone();
    local columns = 10;
    local spawned = 0;

    for index = 0, total - 1 do
        local appearance = startId + index;
        local uniqueId = string.format("appearance_preview_range_%d", appearance);
        zone:DespawnActor(uniqueId);

        local column = index % columns;
        local row = math.floor(index / columns);
        local x = pos[0] + 3 + (column * step);
        local z = pos[2] + 3 + (row * step);

        local actor = zone:SpawnActor(shell, uniqueId, x, pos[1], z, pos[3], 0, 0, true);
        if actor ~= nil then
            actor.ChangeNpcAppearance(appearance);
            actor.SetCustomDisplayName(string.format("app %d", appearance));
            spawned = spawned + 1;
        end
    end

    player:SendMessage(messageID, sender, string.format("Spawned %d preview appearances from %d. Use !previewclear %d %d to clear.", spawned, startId, startId, total));
end;
