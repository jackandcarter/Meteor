require("global");

properties = {
    permissions = 0,
    parameters = "dd",
    description = "clears a labeled preview range: !previewclear <startAppearanceId> <count>",
}

function onTrigger(player, argc, startAppearanceId, count)
    local messageID = MESSAGE_TYPE_SYSTEM_ERROR;
    local sender = "[previewclear] ";

    local startId = tonumber(startAppearanceId) or 0;
    local total = tonumber(count) or 0;

    if startId == 0 or total <= 0 then
        player:SendMessage(messageID, sender, "Use !previewclear <startAppearanceId> <count>.");
        return;
    end

    if total > 100 then
        total = 100;
    end

    local zone = player:GetZone();
    for index = 0, total - 1 do
        zone:DespawnActor(string.format("appearance_preview_range_%d", startId + index));
    end

    player:SendMessage(messageID, sender, string.format("Cleared preview appearances from %d through %d.", startId, startId + total - 1));
end;
