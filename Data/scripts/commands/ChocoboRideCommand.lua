--[[

ChocoboRideCommand Script

Handles mounting and dismounting the Chocobo and Goobbue

--]]

function onEventStarted(player, actor, triggerName, isGoobbue)

	if (player:GetState() == 0) then
				
		worldMaster = GetWorldMaster();		
		
		if (isGoobbue ~= true) then
			local result = player:TryMountPersonalChocobo();
			if (result ~= 0) then
				player:EndEvent();
				return;
			end
			player:SendGameMessage(player, worldMaster, 26001, 0x20);
		else
			player:ChangeMusic(98);
			player:SendGameMessage(player, worldMaster, 26019, 0x20);
			player:SetMountState(2);
		end
		
		if (isGoobbue == true) then
			player:ChangeSpeed(0.0, 5.0, 10.0, 10.0);
			player:ChangeState(15);
		end
	else
		worldMaster = GetWorldMaster();
		
		if (player.rentalExpireTime ~= 0) then
			player:SendGameMessage(player, worldMaster, 26004, 0x20); --You dismount.
		else
			if (player:GetMountState() == 1) then
				player:SendGameMessage(player, worldMaster, 26003, 0x20); --You dismount X.
			else
				player:SendGameMessage(player, worldMaster, 26021, 0x20); --You dismount your Gobbue.
			end
		end
		
		player:EndChocoboRide();
	end
	
	player:EndEvent();
	
end
