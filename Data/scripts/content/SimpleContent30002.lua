
require ("modifiers")

function onCreate(starterPlayer, contentArea, director)
	
	yshtola = contentArea:SpawnActor(2290001, "yshtola", -8, 16.35, 6, 0.5);
	stahlmann = contentArea:SpawnActor(2290002, "stahlmann", 0, 16.35, 22, 3);
	
	mob1 = contentArea:SpawnActor(2205403, "mob1", -3.02+3, 17.35, 14.24, -2.81, 0, 0, true);
	mob2 = contentArea:SpawnActor(2205403, "mob2", -3.02, 17.35, 14.24, -2.81, 0, 0, true);
	mob3 = contentArea:SpawnActor(2205403, "mob3", -3.02-3, 17.35, 14.24, -2.81, 0, 0, true);
	mob1:SetMod(modifiersGlobal.MinimumHpLock, 1);
	mob2:SetMod(modifiersGlobal.MinimumHpLock, 1);
	mob3:SetMod(modifiersGlobal.MinimumHpLock, 1);
	
	director:AddMember(starterPlayer);
	director:AddMember(director);
	director:AddMember(yshtola);
	director:AddMember(stahlmann);
	director:AddMember(mob1);
	director:AddMember(mob2);
	director:AddMember(mob3);
	
	director:StartContentGroup();
	
end

function onDestroy()

	

end
