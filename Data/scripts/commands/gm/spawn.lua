require("global");

properties = {
    permissions = 0,
    parameters = "ddds",
    description = "Spawns an actor class with its own real appearance. Optional fourth arg 'label' adds a debug name.",
}

function onTrigger(player, argc, actorClassId, width, height, labelMode)

	if (actorClassId == nil) then
		player:SendMessage(0x20, "", "No actor class id provided.");
		return;
	end	

    local pos = player:GetPos();
    local x = pos[0];
    local y = pos[1];
    local z = pos[2];
    local rot = pos[3];
    local zone = pos[4];
         
	actorClassId = tonumber(actorClassId);
	
	if (actorClassId ~= nil) then		
		zone = player:GetZone();
		local actor = nil;
		local w = tonumber(width) or 0;
        local h = tonumber(height) or 0;
        local spawned = {};
        local useLabel = labelMode == "label" or labelMode == "debug";
        printf("%f %f %f", x, y, z);
        --local x, y, z = player.GetPos();
        for i = 0, w do
            for j = 0, h do
				local uniqueId = string.format("gm_spawn_%d_%d_%d", actorClassId, i, j);
                zone:DespawnActor(uniqueId);
				actor = zone:SpawnActor(actorClassId, uniqueId, pos[0] + ((i - (w / 2)) * 3), pos[1], pos[2] + ((j - (h / 2)) * 3), pos[3], 0, 0, true);
                if (actor ~= nil) then
                    table.insert(spawned, uniqueId);
                    if (useLabel) then
                        actor.SetCustomDisplayName(string.format("spawn %d", actorClassId));
                    end
                end
			end
		end

		if (actor == nil) then
			player:SendMessage(0x20, "", "This actor class id cannot be spawned.");
        else
            player:SendMessage(0x20, "[spawn] ", string.format("Spawned %d actor(s). Clear with !spawnclear %d %d %d. First id: %s", #spawned, actorClassId, w, h, spawned[1]));
		end
	end
end;
