require("global");

properties = {
    permissions = 0,
    parameters = "ddd",
    description = "Clears actors spawned by !spawn: !spawnclear <actorClassId> [width] [height]",
}

function onTrigger(player, argc, actorClassId, width, height)
    local messageID = MESSAGE_TYPE_SYSTEM_ERROR;
    local sender = "[spawnclear] ";

    local classId = tonumber(actorClassId) or 0;
    local w = tonumber(width) or 0;
    local h = tonumber(height) or 0;

    if (classId == 0) then
        player:SendMessage(messageID, sender, "Use !spawnclear <actorClassId> [width] [height].");
        return;
    end

    local zone = player:GetZone();
    local total = 0;

    for i = 0, w do
        for j = 0, h do
            zone:DespawnActor(string.format("gm_spawn_%d_%d_%d", classId, i, j));
            total = total + 1;
        end
    end

    player:SendMessage(messageID, sender, string.format("Cleared %d possible actor(s) for class %d.", total, classId));
end;
