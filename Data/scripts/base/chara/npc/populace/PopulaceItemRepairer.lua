--[[

PopulaceItemRepairer Script

Functions:

talkWelcome(player, sayWelcomeText, currentLevel?, changes 1500243 from "welcome" to "well met") - Opens the main menu
selectItem(nil, pageNumber, ?, condition1, condition2, condition3, condition4, condition5) - Select item slot.
confirmRepairItem(player, price, itemId, hq grade) - Shows the confirm box for item repair.
confirmUseFacility(player, price) - Shows confirm box for using facility. Default price is 11k?
finishTalkTurn() - Call at end to stop npc from staring at the player (eeeek)

--]]

require ("global")

function init(npc)
	return false, false, 0, 0;	
end

local function repairReference(player, reference)
	if (reference == nil or player:GetNpcRepairQuoteResult(reference) ~= 0) then
		return;
	end

	local fee = player:GetNpcRepairFee(reference);
	local itemId = player:GetNpcRepairItemId(reference);
	local confirmed = callClientFunction(player, "confirmRepairItem", player, fee, itemId, 0);
	if (confirmed == true or confirmed == 1) then
		-- The server revalidates the reference, item identity, damage,
		-- fee and gil here. Replayed/stale replies cannot mutate state.
		player:TryNpcRepair(reference, itemId, fee);
	end
end

function onEventStarted(player, npc, triggerName)
	local result = callClientFunction(player, "talkWelcome", player, true, 0, true);

	while (result == 1) do
		local currentPage = 1;
		local selectedReference = nil;
		local repairSelected = false;

		while (true) do
			local slot, page, listIndx = callClientFunction(player, "selectItem", nil, currentPage, 4, 2, 55, 55, 55, 55);

			if (slot == nil and page ~= nil) then
				currentPage = page;
			elseif (type(slot) == "number" and slot == -1 and listIndx == 2) then
				repairSelected = true;
				break;
			elseif (slot == nil or (type(slot) == "number" and slot == -1)) then
				break;
			else
				selectedReference = slot;
				break;
			end
		end

		if (repairSelected) then
			-- Snapshot references before mutating durability. Re-querying the
			-- candidate index after each repair would skip the next selected item.
			local references = {};
			local candidateCount = player:GetNpcRepairCandidateCount();
			for candidateIndex = 0, candidateCount - 1 do
				table.insert(references, player:GetNpcRepairCandidate(candidateIndex));
			end
			for _, reference in ipairs(references) do
				repairReference(player, reference);
			end
		elseif (selectedReference ~= nil) then
			repairReference(player, selectedReference);
		else
			break;
		end

		-- The retail flow returns through welcome after the confirmation batch.
		result = callClientFunction(player, "talkWelcome", player, true, 0, true);
	end

	if (result == 2) then
		callClientFunction(player, "confirmUseFacility", player);
	end
	
	callClientFunction(player, "finishTalkTurn");
	
	player:EndEvent();
end
