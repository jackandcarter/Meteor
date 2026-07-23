require("global")
require("modifiers")
require("ally")

local TEXT_PROTECT_ESCORTEE = 51005
local TEXT_BOUND_BY_DUTY = 50011
local TEXT_TIME_REMAINING = 25018
local ESCORT_RING_CLASS = 1290003
local ESCORT_LIMIT_TICKS = 30 * 60 * 2
local ARRIVAL_X = -756.77
local ARRIVAL_Z = -1092.33
local ARRIVAL_RADIUS = 20.0
local FOLLOW_DISTANCE = 5.0
local MOVE_STEP = 3.4
local WAYPOINT_RADIUS = 4.0
-- The moving ContentPrivateAreaRange is the retail escort leash. Do not add a
-- second, smaller invisible leash: it can strand Powle just inside the visible
-- duty circle while the client correctly tells the player to remain within it.
local ESCORT_BOUNDARY_RADIUS = 28.0
local HOLD_RADIUS = 28.0
local ENGAGE_RADIUS = 18.0
local BARK_INTERVAL_TICKS = 40

-- The original quest-sheet say ids for these field barks have not been
-- recovered. Keep the attested retail wording and speaker while using the
-- ordinary chat delivery until those localized ids can be proven.
local BARKS = {
	dutyStart = "Powle: Let's go! Off to the Twelveswood!",
	guidance = "Sansa: This is even more fun than I thought it would be.",
	waveCall = "Powle: What was that? Did you see something? Do something! You are an adventurer, aren't you?",
	waveClear = "Sansa: You did it! You did it!",
	waveResume = "Powle: Now back to the march!",
	nearGoal = "Powle: It's just up ahead!",
	arrivalPowle = "Powle: We're here! And it's all thanks to you.",
	arrivalSansa = "Sansa: Look! The stump is just over there!"
}

-- White Wolf Gate to Lifemend Stump. Powle leads this walkable route while
-- Sansa trails him; the player protects them and keeps inside the moving duty
-- boundary. These breadcrumbs preserve the recorded ground height and bends.
local ROUTE = {
	{ -194.73, 3.54, -1021.33 }, { -188.77, 3.68, -1003.75 },
	{ -187.11, 3.99, -984.60 }, { -186.58, 6.13, -966.65 },
	{ -188.28, 5.42, -947.26 }, { -193.27, 4.04, -928.21 },
	{ -195.48, 3.72, -909.25 }, { -185.13, 3.42, -892.53 },
	{ -187.31, 3.57, -872.56 }, { -185.66, 4.64, -852.29 },
	{ -187.28, 4.33, -835.64 }, { -203.65, 4.44, -824.99 },
	{ -217.06, 4.43, -808.32 }, { -234.92, 5.59, -810.88 },
	{ -248.56, 4.39, -824.51 }, { -263.54, 4.89, -839.57 },
	{ -282.43, 4.20, -835.31 }, { -299.81, 4.57, -832.41 },
	{ -319.60, 7.55, -839.92 }, { -336.40, 5.20, -852.87 },
	{ -352.59, 4.00, -864.27 }, { -369.09, 4.70, -876.71 },
	{ -380.83, 4.18, -894.44 }, { -396.64, 5.19, -902.42 },
	{ -415.75, 4.09, -899.02 }, { -433.36, 1.55, -900.18 },
	{ -454.00, -0.69, -895.00 }, { -473.40, 3.41, -893.16 },
	{ -492.48, 4.73, -897.51 }, { -510.17, 7.33, -904.99 },
	{ -527.29, 5.18, -917.99 }, { -543.59, 4.00, -928.30 },
	{ -558.86, 5.26, -940.53 }, { -570.57, 4.30, -952.23 },
	{ -581.78, 4.75, -968.29 }, { -600.16, 4.16, -963.35 },
	{ -616.74, 3.72, -956.32 }, { -637.37, 3.98, -953.99 },
	{ -646.07, 3.75, -970.95 }, { -639.33, 4.02, -990.88 },
	{ -633.32, 7.12, -1011.26 }, { -638.98, 11.53, -1029.27 },
	{ -638.20, 19.00, -1049.53 }, { -635.46, 21.42, -1067.89 },
	{ -640.57, 23.03, -1085.74 }, { -655.57, 22.68, -1100.03 },
	{ -676.77, 21.01, -1096.99 }, { -697.41, 22.81, -1091.66 },
	{ -714.05, 22.86, -1084.68 }, { -734.59, 20.01, -1088.34 },
	{ -755.16, 22.64, -1092.80 }, { -756.77, 22.77, -1092.33 }
}

local function distance(ax, az, bx, bz)
	local dx = ax - bx
	local dz = az - bz
	return math.sqrt(dx * dx + dz * dz)
end

local function livePosition(actor)
	return actor ~= nil and
		not (actor.positionX == 0 and actor.positionY == 0 and actor.positionZ == 0)
end

local function stateKey(owner, name)
	return tostring(owner.actorId) .. ":" .. name
end

local function emitBark(owner, bark)
	owner:SendMessage(0x20, "", bark)
end

local function failEscort(owner, area)
	area:SetScriptState(stateKey(owner, "done"), 1)
	owner:GetQuest("Man0g1"):StartSequence(60)
	owner:SetMod(modifiersGlobal.MinimumHpLock, 0)
	owner:SendGameMessage(GetWorldMaster(), 50012, 0x20)
	area:ContentFinished()
	GetWorldManager():DoZoneChange(owner, 155, nil, 0, 15,
		-194.73, 3.54, -1021.33, -1.642)
	sendSignal(area:GetPlayerSignal(owner, "escortComplete"))
end

function onCreate(player, area, director)
	local powle = GetWorldManager().SpawnBattleNpcById(34, area)
	local sansa = GetWorldManager().SpawnBattleNpcById(35, area)
	local enemies = {}
	for spawnId = 36, 41 do
		table.insert(enemies, GetWorldManager().SpawnBattleNpcById(spawnId, area))
	end
	local ring = area:SpawnActor(ESCORT_RING_CLASS, "escortAreaRange",
		-194.73, 3.54, -1021.33, 0)

	powle:ChangeState(0)
	sansa:ChangeState(0)
	for _, enemy in ipairs(enemies) do
		enemy:ChangeState(2)
	end

	player:SetMod(modifiersGlobal.MinimumHpLock, 1)
	director:AddMember(player)
	director:AddMember(director)
	director:AddMember(powle)
	director:AddMember(sansa)
	director:AddMember(ring)
	for _, enemy in ipairs(enemies) do
		director:AddMember(enemy)
	end

	area:SetScriptState(stateKey(player, "started"), 0)
	area:SetScriptState(stateKey(player, "done"), 0)
	area:SetScriptState(stateKey(player, "startTick"), 0)
	area:SetScriptState(stateKey(player, "powleId"), powle.actorId)
	area:SetScriptState(stateKey(player, "sansaId"), sansa.actorId)
	area:SetScriptState(stateKey(player, "ringId"), ring.actorId)
	area:SetScriptState(stateKey(player, "routeIndex"), 2)
	area:SetScriptState(stateKey(player, "contested"), 0)
	area:SetScriptState(stateKey(player, "lastBarkTick"), 0)
	area:SetScriptState(stateKey(player, "nearGoal"), 0)
end

function onZoneIn(player, area, director)
	player:ChangeMusic(37)
end

function onUpdate(tick, area)
	local owner = nil
	for player in area:GetPlayers() do
		owner = owner or player
	end
	if owner == nil then return end

	if area:GetScriptState(stateKey(owner, "done")) == 1 then return end
	local powleId = area:GetScriptState(stateKey(owner, "powleId"))
	local sansaId = area:GetScriptState(stateKey(owner, "sansaId"))
	if powleId == 0 or sansaId == 0 then return end

	local powle = nil
	local sansa = nil
	-- Area actor collections are CLR iterables, not 1-based Lua arrays.
	for ally in area:GetAllies() do
		if ally.actorId == powleId then powle = ally end
		if ally.actorId == sansaId then sansa = ally end
	end
	if powle == nil or sansa == nil or not powle:IsAlive() or not sansa:IsAlive() then
		if area:GetScriptState(stateKey(owner, "started")) == 1 then
			failEscort(owner, area)
		end
		return
	end
	if not livePosition(owner) or not livePosition(powle) or not livePosition(sansa) then return end

	-- Retail's escort boundary is an invisible ContentPrivateAreaRange actor
	-- whose caution/exit circles are rendered on the minimap. It follows Powle
	-- continuously, independently of combat holds.
	local ringId = area:GetScriptState(stateKey(owner, "ringId"))
	if ringId ~= 0 then
		local ring = area:FindActorInArea(ringId)
		if ring ~= nil then
			ring:MoveTo(powle.positionX, powle.positionY, powle.positionZ, 0, 2)
		end
	end

	if area:GetScriptState(stateKey(owner, "started")) == 0 then
		area:SetScriptState(stateKey(owner, "started"), 1)
		area:SetScriptState(stateKey(owner, "startTick"), tick)
		owner:SendGameMessage(GetWorldMaster(), TEXT_PROTECT_ESCORTEE, 0x20, powle.actorId)
		owner:SendGameMessage(GetWorldMaster(), TEXT_BOUND_BY_DUTY, 0x20)
		owner:SendGameMessage(GetWorldMaster(), TEXT_TIME_REMAINING, 0x20, 30)
		emitBark(owner, BARKS.dutyStart)
		area:SetScriptState(stateKey(owner, "lastBarkTick"), tick)
		-- Deliver the departure line before the first movement packet. Content
		-- updates resume on the next tick, after the client has entered the duty.
		return
	end

	local startTick = area:GetScriptState(stateKey(owner, "startTick"))
	if tick - startTick >= ESCORT_LIMIT_TICKS then
		failEscort(owner, area)
		return
	end

	if distance(owner.positionX, owner.positionZ, ARRIVAL_X, ARRIVAL_Z) <= ARRIVAL_RADIUS and
		distance(powle.positionX, powle.positionZ, ARRIVAL_X, ARRIVAL_Z) <= ARRIVAL_RADIUS * 1.5 and
		distance(sansa.positionX, sansa.positionZ, ARRIVAL_X, ARRIVAL_Z) <= ARRIVAL_RADIUS * 1.5 then
		area:SetScriptState(stateKey(owner, "done"), 1)
		emitBark(owner, BARKS.arrivalPowle)
		emitBark(owner, BARKS.arrivalSansa)
		sendSignal(area:GetPlayerSignal(owner, "escortComplete"))
		return
	end

	local contested = false
	for enemy in area:GetMonsters() do
		-- This core's monster roster also contains allies and the invisible
		-- ContentPrivateAreaRange marker. The marker rides Powle, so treating
		-- it as an enemy would hold the escort forever at distance zero.
		if enemy.actorId ~= powleId and enemy.actorId ~= sansaId and
			enemy.actorId ~= ringId and
			enemy:IsAlive() and livePosition(enemy) then
			local near = math.min(
				distance(enemy.positionX, enemy.positionZ, owner.positionX, owner.positionZ),
				distance(enemy.positionX, enemy.positionZ, powle.positionX, powle.positionZ))
			if enemy:IsEngaged() or near <= HOLD_RADIUS then contested = true end
			if near <= ENGAGE_RADIUS and not enemy:IsEngaged() then
				local engagedKey = stateKey(owner, "engaged:" .. tostring(enemy.actorId))
				if area:GetScriptState(engagedKey) == 0 then
					area:SetScriptState(engagedKey, 1)
					emitBark(owner, BARKS.waveCall)
				end
				allyGlobal.EngageTarget(enemy, owner)
			end
		end
	end
	local wasContested = area:GetScriptState(stateKey(owner, "contested")) == 1
	if contested then
		area:SetScriptState(stateKey(owner, "contested"), 1)
	elseif wasContested then
		area:SetScriptState(stateKey(owner, "contested"), 0)
		emitBark(owner, BARKS.waveClear)
		emitBark(owner, BARKS.waveResume)
	end
	if contested then return end

	if area:GetScriptState(stateKey(owner, "nearGoal")) == 0 and
		distance(owner.positionX, owner.positionZ, ARRIVAL_X, ARRIVAL_Z) <= 40.0 then
		area:SetScriptState(stateKey(owner, "nearGoal"), 1)
		emitBark(owner, BARKS.nearGoal)
	end

	local lastBarkTick = area:GetScriptState(stateKey(owner, "lastBarkTick"))
	if lastBarkTick == 0 or tick - lastBarkTick >= BARK_INTERVAL_TICKS then
		area:SetScriptState(stateKey(owner, "lastBarkTick"), tick)
		emitBark(owner, BARKS.guidance)
	end

	-- Retail escort direction: Powle leads toward Lifemend Stump and waits
	-- when the player falls behind. Advancing across already-reached crumbs
	-- prevents a delayed update from making him double back.
	local routeIndex = area:GetScriptState(stateKey(owner, "routeIndex"))
	if routeIndex < 2 then routeIndex = 2 end
	while routeIndex <= #ROUTE and
		distance(powle.positionX, powle.positionZ,
			ROUTE[routeIndex][1], ROUTE[routeIndex][3]) <= WAYPOINT_RADIUS do
		routeIndex = routeIndex + 1
	end
	area:SetScriptState(stateKey(owner, "routeIndex"), routeIndex)

	local toPlayer = distance(powle.positionX, powle.positionZ,
		owner.positionX, owner.positionZ)
	if routeIndex <= #ROUTE and toPlayer <= ESCORT_BOUNDARY_RADIUS then
		local waypoint = ROUTE[routeIndex]
		local toWaypoint = distance(powle.positionX, powle.positionZ,
			waypoint[1], waypoint[3])
		if toWaypoint > 0 then
			local dx = (waypoint[1] - powle.positionX) / toWaypoint
			local dz = (waypoint[3] - powle.positionZ) / toWaypoint
			local step = math.min(MOVE_STEP, toWaypoint)
			powle:MoveTo(powle.positionX + dx * step, waypoint[2],
				powle.positionZ + dz * step, math.atan(dx, dz), 2)
		end
	end

	local toPowle = distance(sansa.positionX, sansa.positionZ, powle.positionX, powle.positionZ)
	if toPowle > FOLLOW_DISTANCE then
		local dx = (powle.positionX - sansa.positionX) / toPowle
		local dz = (powle.positionZ - sansa.positionZ) / toPowle
		local step = math.min(MOVE_STEP, toPowle - FOLLOW_DISTANCE * 0.5)
		sansa:MoveTo(sansa.positionX + dx * step, powle.positionY,
			sansa.positionZ + dz * step, math.atan(dx, dz), 2)
	end
end

function onDestroy()
end
