require("global")
require("modifiers")

function init()
	return "/Director/Quest/QuestDirectorMan0g101"
end

function onEventStarted(player, director, triggerName)
	local quest = player:GetQuest("Man0g1")
	if quest == nil then
		player:EndEvent()
		return
	end

	callClientFunction(player, "delegateEvent", player, quest, "questBaseRewardSeting")
	player:EndEvent()

	waitForSignal(player:GetZone():GetPlayerSignal(player, "escortComplete"))
	if quest:GetSequence() ~= 65 then
		director:EndDirector()
		return
	end
	wait(2)
	kickEventContinue(player, director, "noticeEvent", "noticeEvent")
	callClientFunction(player, "delegateEvent", player, quest, "processEvent180")
	quest:StartSequence(70)
	player:SendGameMessage(GetWorldMaster(), 50012, 0x20)
	player:EndEvent()
	player:SetMod(modifiersGlobal.MinimumHpLock, 0)
	player:GetZone():ContentFinished()
	director:EndDirector()
	GetWorldManager():DoZoneChange(player, 150, "PrivateAreaMasterPast", 0, 15,
		-770.197, 23, -1086.209, 0.0)
end

function main()
end
