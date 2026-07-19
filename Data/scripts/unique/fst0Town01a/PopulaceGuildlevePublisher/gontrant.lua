require ("global")

local function availableCards(player)
    local cardOne = 0
    local cardFour = 0

    if not player:HasGuildleve(12483) then
        cardOne = 12483
    end

    if not player:HasGuildleve(12482) then
        cardFour = 12482
    end

    return cardOne, cardFour
end

function onEventStarted(player, npc)
    local showIntroduction = true
    local allowances = 41

    while true do
        local menuChoice = callClientFunction(player, "eventTalkType", 48, showIntroduction, 718, 854, 871, true, 0, nil, allowances, 0, 0, 0)
        showIntroduction = false

        if menuChoice == nil or menuChoice == 8 then
            break
        end

        if menuChoice == 1 then
            local selectedPack = callClientFunction(player, "eventTalkPack", 201, 207)

            while selectedPack ~= nil do
                local cardOne, cardFour = availableCards(player)
                local selectedCard = callClientFunction(player, "eventTalkCard", cardOne, 0, 0, cardFour, 0, 0, 0, 0)

                if selectedCard == nil then
                    selectedPack = callClientFunction(player, "eventTalkPack", 201, 207)
                else
                    local guildleveId = 0
                    if selectedCard == 1 then
                        guildleveId = cardOne
                    elseif selectedCard == 4 then
                        guildleveId = cardFour
                    end

                    if guildleveId == 12483 then
                        local accepted = callClientFunction(player, "eventTalkDetail", 12483, 2, 1000001, 7339, 0, 1, 10, true, nil)
                        if accepted == true and player:AddGuildleve(12483) then
                            allowances = allowances - 1
                            callClientFunction(player, "eventTalkAfterOffer", nil)
                        end
                    elseif guildleveId == 12482 then
                        local accepted = callClientFunction(player, "eventTalkDetail", 12482, 2, 1000001, 6240, 1000013, 10, 10, true, nil)
                        if accepted == true and player:AddGuildleve(12482) then
                            allowances = allowances - 1
                        end
                    end
                end
            end
        end
    end

    player:EndEvent()
end
