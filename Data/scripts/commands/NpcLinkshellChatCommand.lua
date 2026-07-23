require ("global")

function onEventStarted(player, command, triggerName, npcLsId)		
	-- The map runtime routes this command to the active quest's onNpcLS
	-- hook. Keep a defensive close here if script dispatch is ever used.
	player:EndEvent();
end
