require ("global")
require ("modifiers")

function onCreate(starterPlayer, contentArea, director)
	local yda = GetWorldManager().SpawnBattleNpcById(6, contentArea);
	local papalymo = GetWorldManager().SpawnBattleNpcById(7, contentArea);
	local mob1 = GetWorldManager().SpawnBattleNpcById(3, contentArea);
	local mob2 = GetWorldManager().SpawnBattleNpcById(4, contentArea);
	local mob3 = GetWorldManager().SpawnBattleNpcById(5, contentArea);

	-- Retail starts Yda and the wolves in active state. Papalymo remains in
	-- his caster presentation state until the battle is released.
	yda:ChangeState(2);
	mob1:ChangeState(2);
	mob2:ChangeState(2);
	mob3:ChangeState(2);

	-- No actor can die while the client is still running its targeting and
	-- weaponskill lessons. The director releases the wolves after the player
	-- uses the requested weaponskill.
	mob1:SetMod(modifiersGlobal.MinimumHpLock, 1);
	mob2:SetMod(modifiersGlobal.MinimumHpLock, 1);
	mob3:SetMod(modifiersGlobal.MinimumHpLock, 1);
	yda:SetMod(modifiersGlobal.MinimumHpLock, 1);
	papalymo:SetMod(modifiersGlobal.MinimumHpLock, 1);
	starterPlayer:SetMod(modifiersGlobal.MinimumHpLock, 1);

	local openingStoper = contentArea:SpawnActor(1090384, "openingstoper", 356.09, 3.74, -701.62, -1.41);

	director:AddMember(starterPlayer);
	director:AddMember(director);
	director:AddMember(papalymo);
	director:AddMember(yda);
	director:AddMember(mob1);
	director:AddMember(mob2);
	director:AddMember(mob3);
end

function onDestroy()
end

-- Battle engagement and retargeting are server-owned in
-- EngageContentBattleForPlayer; a polling Lua update is intentionally not
-- used because this runtime loads area scripts per invocation.
function onUpdate(tick, area)
end
