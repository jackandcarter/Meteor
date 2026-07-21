-- ArrowReloadCommand is a client-issued command event, not a battle ability.
-- The 1.23b client owns the local reload presentation; acknowledge the event
-- so it cannot remain open or produce a missing-script Lua error.

function onEventStarted(player, command, triggerName, arg1, arg2, arg3, arg4, targetActor, arg5, arg6, arg7, arg8)
    player:EndEvent();
end;
