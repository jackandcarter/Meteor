function onEventStarted(player, caller, commandRequest, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8)

    player:TryChangeToCurrentClassJob();

    player:EndEvent();
end
