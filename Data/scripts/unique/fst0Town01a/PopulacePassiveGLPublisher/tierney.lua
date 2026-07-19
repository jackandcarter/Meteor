require ("global")

local function availableQuests(player)
    local first = 0
    local second = 0

    if not player:HasLocalGuildleve(120222) then
        first = 120222
    end

    if not player:HasLocalGuildleve(120202) then
        second = 120202
    end

    return first, second
end

function onEventStarted(player, npc)
    callClientFunction(player, "talkOfferWelcome", player, 38)

    while callClientFunction(player, "askOfferPack", player) ~= nil do
        while callClientFunction(player, "askOfferRank", player) ~= nil do
            while true do
                local first, second = availableQuests(player)
                local selected = callClientFunction(player, "askOfferQuest", player, 2, first, second, 0, 0, 0, 0, 0, 0, 0, 0)

                if selected == nil then
                    break
                end

                local questId = 0
                if selected == 1 then
                    questId = first
                elseif selected == 2 then
                    questId = second
                end

                if questId ~= 0 and player:AddLocalGuildleve(questId) then
                    callClientFunction(player, "talkOfferDecide", nil)
                end
            end
        end
    end

    callClientFunction(player, "finishTalkTurn", nil)
    player:EndEvent()
end
