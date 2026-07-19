--[[

BonusPointCommand Script

Functions:

operateUI(pointsAvailable, pointsLimit, str, vit, dex, int, min, pie)

--]]

require ("global")

function onEventStarted(player, actor, triggerName)
	local points = player:GetAttributePoints();
	local accepted, str, vit, dex, int, mnd, pie = callClientFunction(
		player,
		"delegateCommand",
		actor,
		"operateUI",
		points.available,
		points.limit,
		points.inSTR,
		points.inVIT,
		points.inDEX,
		points.inINT,
		points.inMIN,
		points.inPIE);

	if (accepted == true) then
		player:TrySetAttributePoints(str, vit, dex, int, mnd, pie);
	end
	
	player:endEvent();
end
