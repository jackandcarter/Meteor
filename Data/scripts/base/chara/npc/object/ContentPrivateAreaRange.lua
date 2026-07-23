-- The retail client owns the range marker and uses its exit/caution push
-- conditions to draw the moving duty boundary. The server has no dialogue or
-- interaction to run for this invisible actor, but it must acknowledge the
-- client's generated events so they do not surface as missing-script errors.

function init(npc)
	return false, false, 0, 0
end

function onEventStarted(player, npc, triggerName)
	player:EndEvent()
end
