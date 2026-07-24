require("global")
require("quest")

MAN0U1_SEQ_INTRO = 0;
MAN0U1_SEQ_CAMP = 5;
MAN0U1_SEQ_RETURN = 10;

MAN0U1_FLAG_CAMP_ATTUNED = 0;

MAN0U1_MOMODI = 1000841;
MAN0U1_MOMODI_DISPLAY_ID = 1500014;
MAN0U1_MARKER_MOMODI = 11001001;
MAN0U1_MARKER_CAMP_BLACK_BRUSH = 11001002;

function isObjectivesComplete(player, quest)
	return false;
end

function onStateChange(player, quest, sequence)
	if (sequence == MAN0U1_SEQ_INTRO) then
		quest:SetENpc(MAN0U1_MOMODI, QFLAG_TALK);
	elseif (sequence == MAN0U1_SEQ_RETURN) then
		quest:SetENpc(MAN0U1_MOMODI, QFLAG_TALK);
	end
end

function onTalk(player, quest, npc)
	local sequence = quest:GetSequence();
	if (npc:GetActorClassId() ~= MAN0U1_MOMODI) then
		player:EndEvent();
		return;
	end

	if (sequence == MAN0U1_SEQ_INTRO) then
		local pos = player:GetPos();
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010");
		quest:NewNpcLsMsg(1);
		quest:StartSequence(MAN0U1_SEQ_CAMP);
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 175, nil, 0, 15, pos[0], pos[1], pos[2], pos[3]);
		return;
	elseif (sequence == MAN0U1_SEQ_CAMP) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_2");
	elseif (sequence == MAN0U1_SEQ_RETURN) then
		-- The next historical beat starts here. Keep it replayable until the
		-- remainder of Court in the Sands is implemented in this core.
		callClientFunction(player, "delegateEvent", player, quest, "processEvent015");
	end

	player:EndEvent();
end

function onNpcLS(player, quest, from, msgStep)
	if (from ~= 1 or quest:GetSequence() ~= MAN0U1_SEQ_CAMP) then
		player:EndEvent();
		return;
	end

	if (quest:GetData():GetFlag(MAN0U1_FLAG_CAMP_ATTUNED)) then
		-- Momodi's post-attunement guildleve call. The client quest function
		-- supplies the authentic presentation and dialogue.
		callClientFunction(player, "delegateEvent", player, quest, "processEvent013");
		quest:EndOfNpcLsMsgs();
		quest:StartSequenceForNpcLs(MAN0U1_SEQ_RETURN);
	else
		-- The pearl is granted during Momodi's briefing and can be tried before
		-- leaving. Retail answers with her Camp Black Brush reminder.
		callClientFunction(player, "delegateEvent", player, quest, "processEvent010_2");
		quest:EndOfNpcLsMsgs();
	end

	player:EndEvent();
end

function getJournalMapMarkerList(player, quest)
	local sequence = quest:GetSequence();
	local markers = {};

	if (sequence == MAN0U1_SEQ_INTRO or sequence == MAN0U1_SEQ_RETURN) then
		table.insert(markers, MAN0U1_MARKER_MOMODI);
	elseif (sequence == MAN0U1_SEQ_CAMP) then
		table.insert(markers, MAN0U1_MARKER_CAMP_BLACK_BRUSH);
	end

	return unpack(markers);
end
