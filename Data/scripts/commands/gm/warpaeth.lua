require("global");
require("aetheryte");

properties = {
    permissions = 0,
    parameters = "s",
    description =
[[
Teleports to a known aetheryte/aetherial gate destination from Data/scripts/aetheryte.lua.
This is a GM/debug movement command only; it does not attune, unlock, advance quests, or fix missing world objects.
!warpaeth <aetheryteId|alias>
!warpaeth list
Examples:
!warpaeth 1280032
!warpaeth blackbrush
!warpaeth uldah
]],
}

local sender = "[warpaeth] ";

local aetheryteNames = {
    [1280001] = "limsa_city",
    [1280002] = "camp_bearded_rock",
    [1280003] = "camp_skull_valley",
    [1280004] = "camp_bald_knoll",
    [1280005] = "camp_bloodshore",
    [1280006] = "camp_iron_lake",
    [1280007] = "cedarwood",
    [1280008] = "widow_cliffs",
    [1280009] = "moraby_bay",
    [1280010] = "woad_whisper_canyon",
    [1280011] = "isles_of_umbra",
    [1280012] = "tiger_helm_island",
    [1280013] = "south_bloodshore",
    [1280014] = "agelyss_wise",
    [1280015] = "zelmas_run",
    [1280016] = "bronze_lake",
    [1280017] = "oakwood",
    [1280018] = "mistbeard_cove",
    [1280020] = "cassiopeia",
    [1280031] = "uldah",
    [1280032] = "camp_black_brush",
    [1280033] = "camp_drybone",
    [1280034] = "camp_horizon",
    [1280035] = "camp_bluefog",
    [1280036] = "camp_broken_water",
    [1280037] = "cactus_basin",
    [1280038] = "four_sisters",
    [1280039] = "halatali",
    [1280040] = "burning_wall",
    [1280041] = "sandgate",
    [1280042] = "nophicas_wells",
    [1280043] = "footfalls",
    [1280044] = "scorpion_keep",
    [1280045] = "hidden_gorge",
    [1280046] = "sea_of_spires",
    [1280047] = "cutters_pass",
    [1280048] = "red_labyrinth",
    [1280049] = "burnt_lizard_creek",
    [1280050] = "zanrak",
    [1280052] = "nanawa_mines",
    [1280054] = "copperbell_mines",
    [1280061] = "gridania",
    [1280062] = "camp_bentbranch",
    [1280063] = "camp_nine_ivies",
    [1280064] = "camp_crimson_bark",
    [1280065] = "camp_emerald_moss",
    [1280066] = "camp_tranquil",
    [1280067] = "humblehearth",
    [1280068] = "sorrel_haven",
    [1280069] = "five_hangs",
    [1280070] = "verdant_drop",
    [1280071] = "lynxpelt_patch",
    [1280072] = "larkscall",
    [1280073] = "treespeak",
    [1280074] = "aldersprings",
    [1280075] = "lasthold",
    [1280076] = "lichenweed",
    [1280077] = "murmur_rills",
    [1280078] = "turning_leaf",
    [1280079] = "silent_arbor",
    [1280080] = "longroot",
    [1280081] = "snakemolt",
    [1280082] = "mun_tuy_cellars",
    [1280083] = "tam_tara_deepcroft",
    [1280092] = "camp_dragonhead",
    [1280093] = "camp_crooked_fork",
    [1280094] = "camp_glory",
    [1280095] = "camp_ever_lakes",
    [1280096] = "camp_riversmeet",
    [1280097] = "boulder_downs",
    [1280098] = "prominence_point",
    [1280099] = "feathergorge",
    [1280100] = "maiden_glen",
    [1280101] = "hushed_boughs",
    [1280102] = "scarwing_fall",
    [1280103] = "weeping_vale",
    [1280104] = "clearwater",
    [1280105] = "terrigans_stand",
    [1280106] = "shepherd_peak",
    [1280107] = "fellwood",
    [1280108] = "wyrmkings_perch",
    [1280109] = "the_lance",
    [1280110] = "twinpools",
    [1280121] = "camp_brittlebark",
    [1280122] = "camp_revenants_toll",
    [1280123] = "fogfens",
    [1280124] = "singing_shards",
    [1280125] = "jagged_crest_cave",
};

local aliases = {};

local function normalize(value)
    return tostring(value or ""):lower():gsub("[^%w]", "");
end

local function addAlias(aetheryteId, ...)
    local names = {...};
    for _, name in ipairs(names) do
        aliases[normalize(name)] = aetheryteId;
    end
end

for aetheryteId, name in pairs(aetheryteNames) do
    addAlias(aetheryteId, name);
end

addAlias(1280031, "uldah", "ul_dah", "uldah_city");
addAlias(1280032, "blackbrush", "camp_blackbrush", "camp_black_brush");
addAlias(1280036, "brokenwater", "camp_brokenwater");
addAlias(1280061, "gridania", "gridania_city");
addAlias(1280122, "revenants_toll", "revenants", "revtoll");

local function send(player, message)
    player:SendMessage(MESSAGE_TYPE_SYSTEM_ERROR, sender, message);
    print(sender..message);
end

local function resolveAetheryteId(target)
    local direct = tonumber(target);
    if direct ~= nil then
        return direct;
    end

    return aliases[normalize(target)];
end

local function listKnown(player)
    send(player, "known aliases are printed to the map server terminal");
    print(sender.."known aetheryte destinations:");
    for aetheryteId, name in pairs(aetheryteNames) do
        local destination = aetheryteTeleportPositions[aetheryteId];
        if destination ~= nil then
            print(string.format(
                "%s%d %s zone=%d x=%.3f y=%.3f z=%.3f",
                sender,
                aetheryteId,
                name,
                destination[1],
                destination[2],
                destination[3],
                destination[4]));
        end
    end
end

function onTrigger(player, argc, target)
    if not player then
        print(sender.."player not found");
        return;
    end

    if target == nil or target == "" then
        send(player, "usage: !warpaeth <aetheryteId|alias> or !warpaeth list");
        return;
    end

    if normalize(target) == "list" then
        listKnown(player);
        return;
    end

    local aetheryteId = resolveAetheryteId(target);
    if aetheryteId == nil then
        send(player, string.format("unknown destination '%s'; use !warpaeth list", tostring(target)));
        return;
    end

    local destination = aetheryteTeleportPositions[aetheryteId];
    if destination == nil then
        send(player, string.format("aetheryte %d has no teleport position", aetheryteId));
        return;
    end

    local zone = destination[1];
    local x = destination[2];
    local y = destination[3];
    local z = destination[4];
    local pos = player:GetPos();
    local privateArea = player.privateArea;
    local inPublicArea = privateArea == nil or privateArea == "";
    local worldManager = GetWorldManager();

    send(player, string.format(
        "moving to %d %s zone=%d x=%.3f y=%.3f z=%.3f",
        aetheryteId,
        aetheryteNames[aetheryteId] or "unknown",
        zone,
        x,
        y,
        z));

    if pos[4] == zone and inPublicArea then
        worldManager:DoPlayerMoveInZone(player, x, y, z, pos[3], 0x00);
    else
        worldManager:DoZoneChange(player, zone, nil, 0, 0x02, x, y, z, 0.00);
    end
end
