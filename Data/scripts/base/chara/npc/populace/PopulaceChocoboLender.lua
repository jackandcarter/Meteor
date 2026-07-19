--[[

PopulaceChocoboLender Script

Functions:

eventTalkWelcome(player) - Start Text
eventAskMainMenu(player, curLevel, hasFundsForRent, isPresentChocoboIssuance, isSummonMyChocobo, isChangeBarding, currentChocoboWare) - Shows the main menu
eventTalkMyChocobo(player) - Starts the cutscene for getting a chocobo
eventSetChocoboName(true) - Opens the set name dialog
eventAfterChocoboName(player) - Called if player done naming chocobo, shows cutscene, returns state and waits to teleport outside city.
eventCancelChocoboName(player) - Called if player cancels naming chocobo, returns state. 
eventTalkStepBreak(player) - Finishes talkTurn and says a goodbye

Notes:

* Rent price and time seems to be hardcoded into the client. Price is always 800gil and time is 10m.
* The func eventSetChocoboName *requires* the actor with id `1080101` to be present in the client instance or it will crash (thanks Jorge for finding that).
* Special spawn codes must be sent for getting your chocobo or renting for it to work properly.

--]]

require ("global")

local rentalPrice = 800;
local rentalTime = 10;

local gcIssuances = {
	[1500006] = 2001004,
	[1500061] = 2001005,
	[1000840] = 2001006
};

local startAppearances = {
	[1500006] = CHOCOBO_LIMSA1,
	[1500061] = CHOCOBO_GRIDANIA1,
	[1000840] = CHOCOBO_ULDAH1
};

local cityExits = {
	[1500006] = {133, -6.032, 46.356, 132.572, 3.034},
	[1500061] = {150, 333.271, 5.889, -943.275, 0.794},
	[1000840] = {170, -26.088, 181.846, -79.438, 2.579}
};

function init(npc)
	return false, false, 0, 0;	
end

function onEventStarted(player, npc, triggerName)	
	local actorClassId = npc:GetActorClassId();
	local curLevel = player:GetHighestLevel();
	local hasIssuance = player:CanPresentChocoboIssuance(actorClassId);
	local hasChocobo = player.hasChocobo;
	local hasFunds = player:CanRentChocobo();

	callClientFunction(player, "eventTalkWelcome", player);
	
	local menuChoice = callClientFunction(player, "eventAskMainMenu", player, curLevel, hasFunds, hasIssuance, hasChocobo, hasChocobo, 0);

	if (menuChoice == 1) then -- Issuance option
	
		callClientFunction(player, "eventTalkMyChocobo", player);
		local nameResponse = callClientFunction(player, "eventSetChocoboName", true);

		if (nameResponse == "") then -- Cancel Chocobo naming
			callClientFunction(player, "eventCancelChocoboName", player);
			callClientFunction(player, "eventTalkStepBreak", player);
			player:EndEvent();
			return;
		else
			local issueResult = player:TryIssuePersonalChocobo(actorClassId, nameResponse);
			if (issueResult == 0) then
				callClientFunction(player, "eventAfterChocoboName", player);
				local mountResult = player:TryStartStablemasterChocoboRide(actorClassId);
				if (mountResult == 0) then
					GetWorldManager():DoZoneChange(player, cityExits[actorClassId][1], nil, 0, SPAWN_CHOCOBO_GET, cityExits[actorClassId][2], cityExits[actorClassId][3], cityExits[actorClassId][4], cityExits[actorClassId][5]);
				end
			else
				callClientFunction(player, "eventTalkStepBreak", player);
			end
		end
				
	elseif(menuChoice == 2) then -- Summon Bird
		local mountResult = player:TryStartStablemasterChocoboRide(actorClassId);
		if (mountResult == 0) then
			GetWorldManager():DoZoneChange(player, cityExits[actorClassId][1], nil, 0, SPAWN_NO_ANIM, cityExits[actorClassId][2], cityExits[actorClassId][3], cityExits[actorClassId][4], cityExits[actorClassId][5]);
		end
	elseif(menuChoice == 3) then -- Change Barding
		callClientFunction(player, "eventTalkStepBreak", player);
	elseif(menuChoice == 5) then -- Rent Bird
		local rentalResult = player:TryStartChocoboRental(actorClassId);
		if (rentalResult == 0) then
			GetWorldManager():DoZoneChange(player, cityExits[actorClassId][1], nil, 0, SPAWN_CHOCOBO_RENTAL, cityExits[actorClassId][2], cityExits[actorClassId][3], cityExits[actorClassId][4], cityExits[actorClassId][5]);
		end
	else
		callClientFunction(player, "eventTalkStepBreak", player);
	end

	player:EndEvent();
end
