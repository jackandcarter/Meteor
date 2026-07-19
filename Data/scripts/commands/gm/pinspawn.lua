require("global");

properties = {
    permissions = 0,
    parameters = "ss",
    description = "captures a provisional battle NPC spawn audit pin at your current position",
}

function onTrigger(player)
    if player then
        player:SendMessage(MESSAGE_TYPE_SYSTEM_ERROR, "[pinspawn] ", "Use !pinspawn \"Enemy Name\" \"source note\", or !pinspawn for prompt mode.");
    end
end;
