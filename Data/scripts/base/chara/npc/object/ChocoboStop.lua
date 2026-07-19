require ("global")

-- Exact 2012.09.19.0001 actor-init tuple observed for ChocoboStop. The client
-- class only initializes its dummy work value and delegates both push channels
-- to the server.
function init(npc)
	return false, false, 0, 1, "TEST";
end

function onSpawn(player, npc)
	-- Official on-foot captures instantiate both conditions disabled. Enable
	-- them only for a mounted player; the ordinary mounted-event guard keeps
	-- every unrelated interaction unavailable.
	if (player:GetMountState() ~= 0) then
		player:SetEventStatus(npc, "pushDefault", true, 0x2);
		player:SetEventStatus(npc, "_!pushRequest", true, 0x2);
	end
end

function onEventStarted(player, npc, triggerName)
	if (triggerName ~= "pushDefault" and triggerName ~= "_!pushRequest") then
		player:EndEvent();
		return;
	end

	-- ChocoboStop does not dismount the player. The Map boundary policy rejects
	-- the non-ride destination and keeps the active rental/personal ride intact.
	player:EndEvent();
end
