require ("global")

function init(npc)
	return false, false, 0, 0;
end

function onSpawn(player, npc)
end

function onEventStarted(player, npc, triggerName)
	player:EndEvent();
end
