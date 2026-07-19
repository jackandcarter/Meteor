require ("global")
require ("quests/man/man0u0")

local function completeMan0u0NpcTutorial(player, quest)
	if (quest == nil or quest:GetQuestId() ~= 110009) then
		return;
	end

	if (quest:GetQuestFlag(MAN0U0_FLAG_TUTORIAL3_DONE) ~= true or quest:GetQuestFlag(MAN0U0_FLAG_MINITUT_DONE1) == true) then
		return;
	end

	quest:SetQuestFlag(MAN0U0_FLAG_MINITUT_DONE1, true);
	quest:SaveData();

	ascilia = GetWorldManager():GetActorInWorldByUniqueId("ascilia");
	if (ascilia ~= nil) then
		ascilia:SetQuestGraphic(player, 0x0);
	end

	fretfulFarmhand = GetWorldManager():GetActorInWorldByUniqueId("fretful_farmhand");
	if (fretfulFarmhand ~= nil) then
		fretfulFarmhand:SetQuestGraphic(player, 0x2);
	end
end

function onEventStarted(player, actor, trigger, questId, mapCode)

	quest = player:GetQuest(questId);
	
	if (quest == nil) then	
		player:EndEvent();
		return;
	end
	
	if (mapCode == nil) then	
		player:SendDataPacket("requestedData", "qtdata", quest:GetQuestId(), quest:GetPhase());
		completeMan0u0NpcTutorial(player, quest);
		player:EndEvent();
	else
		player:SendDataPacket("requestedData", "qtmap", quest:GetQuestId());
		player:EndEvent();
	end
	
end
