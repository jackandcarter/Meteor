require ("global")

local CARPENTER_QUESTS = {
    { id = 110300, name = "Wdk200" },
    { id = 110301, name = "Wdk300" },
    { id = 110302, name = "Wdk306" }
}

local function getAvailableQuest(player)
    for _, quest in ipairs(CARPENTER_QUESTS) do
        if player:CanAcceptClassQuest(quest.id) then
            return quest, GetStaticActor(quest.name)
        end
    end

    return nil, nil
end

function onSpawn(player, npc)
    local quest = getAvailableQuest(player)
    if quest ~= nil then
        npc:SetQuestGraphic(player, 0x2)
    else
        npc:SetQuestGraphic(player, 0x0)
    end
end

function onEventStarted(player, npc)
    local defaultFst = GetStaticActor("DftFst")
    local questEntry, questActor = getAvailableQuest(player)

    if questEntry == nil or questActor == nil then
        callClientFunction(player, "delegateEvent", player, defaultFst, "defaultTalkWithAnaidjaa_001", nil, nil, nil)
        player:EndEvent()
        return
    end

    local _, result = callClientFunction(player, "switchEvent", defaultFst, questActor, nil, nil, 1, 1, 0x3f1)
    if result == 2 then
        local accepted = callClientFunction(player, "delegateEvent", player, questActor, "processEventStart")
        if accepted == 1 and player:CanAcceptClassQuest(questEntry.id) then
            player:AddQuest(questEntry.id)
            npc:SetQuestGraphic(player, 0x0)
        end
    else
        callClientFunction(player, "delegateEvent", player, defaultFst, "defaultTalkWithAnaidjaa_001", nil, nil, nil)
    end

    player:EndEvent()
end
