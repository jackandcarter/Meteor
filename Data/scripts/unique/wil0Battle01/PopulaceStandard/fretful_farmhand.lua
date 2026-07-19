require ("global")
require ("quests/man/man0u0")

function onSpawn(player, npc)
	man0u0Quest = player:GetQuest("Man0u0");

	if (man0u0Quest ~= nil) then
		if (man0u0Quest:GetQuestFlag(MAN0U0_FLAG_MINITUT_DONE1) == true and man0u0Quest:GetQuestFlag(MAN0U0_FLAG_MINITUT_DONE2) == false) then
			npc:SetQuestGraphic(player, 0x2);
		else
			npc:SetQuestGraphic(player, 0x0);
		end
	end	
end

function onEventStarted(player, npc, triggerName)
	man0u0Quest = player:GetQuest("Man0u0");

	if (man0u0Quest ~= nil) then
		if (triggerName == "talkDefault") then
			if (man0u0Quest:GetQuestFlag(MAN0U0_FLAG_MINITUT_DONE2) == false) then
				callClientFunction(player, "delegateEvent", player, man0u0Quest, "processTtrMini002_first", nil, nil, nil);
				npc:SetQuestGraphic(player, 0x0);
				man0u0Quest:SetQuestFlag(MAN0U0_FLAG_MINITUT_DONE2, true);
				man0u0Quest:SaveData();

				gilDiggingMistress = GetWorldManager():GetActorInWorldByUniqueId("gil-digging_mistress");
				if (gilDiggingMistress ~= nil) then
					gilDiggingMistress:SetQuestGraphic(player, 0x2);
				end

				director = player:GetDirector("OpeningDirector");
				if (director ~= nil) then
					director:onTalkEvent(player, npc);
				end
			else
				callClientFunction(player, "delegateEvent", player, man0u0Quest, "processTtrMini002", nil, nil, nil);
			end
		end
	end
	player:EndEvent();
end
