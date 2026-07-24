using AetherXIV.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.dataobjects.chara;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.packets.WorldPackets.Send.Group;
using AetherXIV.Core.Map.packets.WorldPackets.Send;
using AetherXIV.Core.Map.utils;
using AetherXIV.Core.Map.actors.group;
using AetherXIV.Core.Map.actors.chara.player;
using AetherXIV.Core.Map.actors.director;
using AetherXIV.Core.Map.actors.chara.npc;
using AetherXIV.Core.Map.actors.chara.ai;
using AetherXIV.Core.Map.actors.chara.ai.controllers;
using AetherXIV.Core.Map.actors.chara.ai.utils;
using AetherXIV.Core.Map.actors.chara.ai.state;
using AetherXIV.Core.Map.actors.chara;
using AetherXIV.Core.Map.actors.area;
using AetherXIV.Core.Map.packets.send;
using AetherXIV.Core.Map.packets.send.actor;
using AetherXIV.Core.Map.packets.send.events;
using AetherXIV.Core.Map.packets.send.actor.inventory;
using AetherXIV.Core.Map.packets.send.player;
using AetherXIV.Core.Map.packets.send.actor.battle;
using AetherXIV.Core.Map.packets.receive.events;
using static AetherXIV.Core.Map.LuaUtils;
using AetherXIV.Core.Map.packets.send.actor.events;

namespace AetherXIV.Core.Map.Actors
{
    class PlayerBaseStatProfile
    {
        public readonly byte classId;
        public readonly byte tribe;
        public readonly short level;
        public readonly short hp;
        public readonly short mp;
        public readonly short strength;
        public readonly short vitality;
        public readonly short dexterity;
        public readonly short intelligence;
        public readonly short mind;
        public readonly short piety;
        public readonly string source;

        public PlayerBaseStatProfile(byte classId, byte tribe, short level, short hp, short mp, short strength, short vitality, short dexterity, short intelligence, short mind, short piety, string source)
        {
            this.classId = classId;
            this.tribe = tribe;
            this.level = level;
            this.hp = hp;
            this.mp = mp;
            this.strength = strength;
            this.vitality = vitality;
            this.dexterity = dexterity;
            this.intelligence = intelligence;
            this.mind = mind;
            this.piety = piety;
            this.source = source;
        }
    }

    class PlayerClassAttributeAllocation
    {
        public readonly byte classId;
        public readonly short pointsRemaining;
        public readonly short strength;
        public readonly short vitality;
        public readonly short dexterity;
        public readonly short intelligence;
        public readonly short mind;
        public readonly short piety;

        public PlayerClassAttributeAllocation(byte classId, short pointsRemaining, short strength, short vitality, short dexterity, short intelligence, short mind, short piety)
        {
            this.classId = classId;
            this.pointsRemaining = pointsRemaining;
            this.strength = strength;
            this.vitality = vitality;
            this.dexterity = dexterity;
            this.intelligence = intelligence;
            this.mind = mind;
            this.piety = piety;
        }

        public short SpentPoints()
        {
            return (short)(strength + vitality + dexterity + intelligence + mind + piety);
        }
    }

    class PlayerAttributePointState
    {
        public readonly short available;
        public readonly short limit;
        public readonly short inSTR;
        public readonly short inVIT;
        public readonly short inDEX;
        public readonly short inINT;
        public readonly short inMIN;
        public readonly short inPIE;

        public PlayerAttributePointState(short available, short limit, PlayerClassAttributeAllocation allocation)
        {
            this.available = available;
            this.limit = limit;
            inSTR = allocation.strength;
            inVIT = allocation.vitality;
            inDEX = allocation.dexterity;
            inINT = allocation.intelligence;
            inMIN = allocation.mind;
            inPIE = allocation.piety;
        }
    }

    class Player : Character
    {
        public const int JOBID_MNK = 15;
        public const int JOBID_PLD = 16;
        public const int JOBID_WAR = 17;
        public const int JOBID_BRD = 18;
        public const int JOBID_DRG = 19;
        public const int JOBID_BLM = 26;
        public const int JOBID_WHM = 27;

        public const int TIMER_TOTORAK = 0;
        public const int TIMER_DZEMAEL = 1;
        public const int TIMER_BOWL_OF_EMBERS_HARD = 2;
        public const int TIMER_BOWL_OF_EMBERS = 3;
        public const int TIMER_THORNMARCH = 4;
        public const int TIMER_AURUMVALE = 5;
        public const int TIMER_CUTTERSCRY = 6;
        public const int TIMER_BATTLE_ALEPORT = 7;
        public const int TIMER_BATTLE_HYRSTMILL = 8;
        public const int TIMER_BATTLE_GOLDENBAZAAR = 9;
        public const int TIMER_HOWLING_EYE_HARD = 10;
        public const int TIMER_HOWLING_EYE = 11;
        public const int TIMER_CASTRUM_TOWER = 12;
        public const int TIMER_BOWL_OF_EMBERS_EXTREME = 13;
        public const int TIMER_RIVENROAD = 14;
        public const int TIMER_RIVENROAD_HARD = 15;
        public const int TIMER_BEHEST = 16;
        public const int TIMER_COMPANYBEHEST = 17;
        public const int TIMER_RETURN = 18;
        public const int TIMER_SKIRMISH = 19;

        public const int NPCLS_GONE = 0;
        public const int NPCLS_INACTIVE = 1;
        public const int NPCLS_ACTIVE = 2;
        public const int NPCLS_ALERT = 3;

        public const int SLOT_MAINHAND = 0;
        public const int SLOT_OFFHAND = 1;
        public const int SLOT_THROWINGWEAPON = 4;
        public const int SLOT_PACK = 5;
        public const int SLOT_POUCH = 6;
        public const int SLOT_HEAD = 8;
        public const int SLOT_UNDERSHIRT = 9;
        public const int SLOT_BODY = 10;
        public const int SLOT_UNDERGARMENT = 11;
        public const int SLOT_LEGS = 12;
        public const int SLOT_HANDS = 13;
        public const int SLOT_BOOTS = 14;
        public const int SLOT_WAIST = 15;
        public const int SLOT_NECK = 16;
        public const int SLOT_EARS = 17;
        public const int SLOT_WRISTS = 19;
        public const int SLOT_RIGHTFINGER = 21;
        public const int SLOT_LEFTFINGER = 22;

        public static int[] MAXEXP = {570, 700, 880, 1100, 1500, 1800, 2300, 3200, 4300, 5000,                   //Level <= 10
                                     5900, 6800, 7700, 8700, 9700, 11000, 12000, 13000, 15000, 16000,            //Level <= 20
                                     20000, 22000, 23000, 25000, 27000, 29000, 31000, 33000, 35000, 38000,       //Level <= 30
                                     45000, 47000, 50000, 53000, 56000, 59000, 62000, 65000, 68000, 71000,       //Level <= 40
                                     74000, 78000, 81000, 85000, 89000, 92000, 96000, 100000, 100000, 110000};   //Level <= 50

        //Event Related
        public uint currentEventOwner = 0;
        public string currentEventName = "";
        public byte currentEventType = 0;
        public Coroutine currentEventRunning;

        //Player Info
        public uint destinationZone;
        public ushort destinationSpawnType;
        public uint[] timers = new uint[20];
        public uint currentTitle;
        public uint playTime;
        public uint lastPlayTimeUpdate;
        public bool isGM = false;
        public bool isZoneChanging = true;
        private bool hasExpectedZoneChangePosition;
        private uint expectedZoneChangeZone;
        private float expectedZoneChangeX;
        private float expectedZoneChangeY;
        private float expectedZoneChangeZ;
        private float expectedZoneChangeRotation;
        private uint rejectedZoneChangePositionCount;

        //Trading
        private Player otherTrader = null;
        private ReferencedItemPackage myOfferings;
        private bool isTradeAccepted = false;

        //GC Related
        public byte gcCurrent;
        public byte gcRankLimsa;
        public byte gcRankGridania;
        public byte gcRankUldah;

        //Mount Related
        public bool hasChocobo;
        public bool hasGoobbue;
        public string chocoboName;
        public byte mountState = 0;
        public byte chocoboAppearance;
        public byte rentalChocoboAppearance = ChocoboPolicy.RentalAppearance;
        public uint rentalExpireTime = 0;
        public byte rentalMinLeft = 0;
        public ChocoboRideKind chocoboRideKind = ChocoboRideKind.None;

        public uint achievementPoints;

        //Property Array Request Stuff
        private int lastPosition = 0;
        private int lastStep = 0;

        //Quest Actors (MUST MATCH playerWork.questScenario/questGuildleve)
        public Quest[] questScenario = new Quest[16];
        public uint[] questGuildleve = new uint[8];

        //Aetheryte
        public uint homepoint = 0;
        public byte homepointInn = 0;

        //Nameplate Stuff
        public uint currentLSPlate = 0;
        public byte repairType = 0;

        //Retainer
        RetainerMeetingRelationGroup retainerMeetingGroup = null;
        public Retainer currentSpawnedRetainer = null;
        public bool sentRetainerSpawn = false;

        private List<Director> ownedDirectors = new List<Director>();
        private Director loginInitDirector = null;
        private Actor deferredContentKickOwner = null;
        private string deferredContentKickEventName = null;
        private object[] deferredContentKickParameters = null;

        List<ushort> hotbarSlotsToUpdate = new List<ushort>();

        public PlayerWork playerWork = new PlayerWork();
        private readonly Dictionary<byte, PlayerClassAttributeAllocation> classAttributeAllocations = new Dictionary<byte, PlayerClassAttributeAllocation>();
        private readonly Dictionary<string, PlayerBaseStatProfile> baseStatProfiles = new Dictionary<string, PlayerBaseStatProfile>();
        private readonly HashSet<string> missingBaseStatProfiles = new HashSet<string>();

        public Session playerSession;

        public Player(Session cp, uint actorID) : base(actorID)
        {
            playerSession = cp;
            actorName = String.Format("_pc{0:00000000}", actorID);
            className = "Player";

            moveSpeeds[0] = SetActorSpeedPacket.DEFAULT_STOP;
            moveSpeeds[1] = SetActorSpeedPacket.DEFAULT_WALK;
            moveSpeeds[2] = SetActorSpeedPacket.DEFAULT_RUN;
            moveSpeeds[3] = SetActorSpeedPacket.DEFAULT_ACTIVE;

            itemPackages[ItemPackage.NORMAL] = new ItemPackage(this, ItemPackage.MAXSIZE_NORMAL, ItemPackage.NORMAL);
            itemPackages[ItemPackage.KEYITEMS] = new ItemPackage(this, ItemPackage.MAXSIZE_KEYITEMS, ItemPackage.KEYITEMS);
            itemPackages[ItemPackage.CURRENCY_CRYSTALS] = new ItemPackage(this, ItemPackage.MAXSIZE_CURRANCY, ItemPackage.CURRENCY_CRYSTALS);
            itemPackages[ItemPackage.MELDREQUEST] = new ItemPackage(this, ItemPackage.MAXSIZE_MELDREQUEST, ItemPackage.MELDREQUEST);
            itemPackages[ItemPackage.BAZAAR] = new ItemPackage(this, ItemPackage.MAXSIZE_BAZAAR, ItemPackage.BAZAAR);
            itemPackages[ItemPackage.LOOT] = new ItemPackage(this, ItemPackage.MAXSIZE_LOOT, ItemPackage.LOOT);
            equipment = new ReferencedItemPackage(this, ItemPackage.MAXSIZE_EQUIPMENT, ItemPackage.EQUIPMENT);

            //Set the Skill level caps of all FFXIV (classes)skills to 50
            for (int i = 0; i < charaWork.battleSave.skillLevelCap.Length; i++)
            {
                if (i != CLASSID_PUG &&
                    i != CLASSID_MRD &&
                    i != CLASSID_GLA &&
                    i != CLASSID_MRD &&
                    i != CLASSID_ARC &&
                    i != CLASSID_LNC &&
                    i != CLASSID_THM &&
                    i != CLASSID_CNJ &&
                    i != CLASSID_CRP &&
                    i != CLASSID_BSM &&
                    i != CLASSID_ARM &&
                    i != CLASSID_GSM &&
                    i != CLASSID_LTW &&
                    i != CLASSID_WVR &&
                    i != CLASSID_ALC &&
                    i != CLASSID_CUL &&
                    i != CLASSID_MIN &&
                    i != CLASSID_BTN &&
                    i != CLASSID_FSH)
                    charaWork.battleSave.skillLevelCap[i] = 0xFF;
                else
                    charaWork.battleSave.skillLevelCap[i] = 50;

            }

            charaWork.property[0] = 1;
            charaWork.property[1] = 1;
            charaWork.property[2] = 1;
            charaWork.property[4] = 1;

            charaWork.command[0] =  0xA0F00000 | 21001;
            charaWork.command[1] =  0xA0F00000 | 21001;

            charaWork.command[2] =  0xA0F00000 | 21002;
            charaWork.command[3] =  0xA0F00000 | 12004;
            charaWork.command[4] =  0xA0F00000 | 21005;
            charaWork.command[5] =  0xA0F00000 | 21006;
            charaWork.command[6] =  0xA0F00000 | 21007;
            charaWork.command[7] =  0xA0F00000 | 12009;
            charaWork.command[8] =  0xA0F00000 | 12010;
            charaWork.command[9] =  0xA0F00000 | 12005;
            charaWork.command[10] = 0xA0F00000 | 12007;
            charaWork.command[11] = 0xA0F00000 | 12011;
            charaWork.command[12] = 0xA0F00000 | 22012;
            charaWork.command[13] = 0xA0F00000 | 22013;
            charaWork.command[14] = 0xA0F00000 | 29497;
            charaWork.command[15] = 0xA0F00000 | 22015;            

            charaWork.commandAcquired[27150 - 26000] = true;

            playerWork.questScenarioComplete[110001 - 110001] = true;
            playerWork.questGuildleveComplete[120050 - 120001] = true;

            for (int i = 0; i < charaWork.additionalCommandAcquired.Length; i++ )
                charaWork.additionalCommandAcquired[i] = true;
            
            for (int i = 0; i < charaWork.commandCategory.Length; i++)
                charaWork.commandCategory[i] = 1;

            charaWork.battleTemp.generalParameter[3] = 1;

            charaWork.eventSave.bazaarTax = 5;
            charaWork.battleSave.potencial = 6.6f;

            charaWork.battleSave.negotiationFlag[0] = true;

            charaWork.commandCategory[0] = 1;
            charaWork.commandCategory[1] = 1;

            charaWork.parameterSave.commandSlot_compatibility[0] = true;
            charaWork.parameterSave.commandSlot_compatibility[1] = true;

            charaWork.commandBorder = 0x20;

            charaWork.parameterTemp.tp = 0;

            Database.LoadPlayerCharacter(this);
            lastPlayTimeUpdate = Utils.UnixTimeStampUTC();

            this.aiContainer = new AIContainer(this, new PlayerController(this), null, new TargetFind(this));
            allegiance = CharacterTargetingAllegiance.Player;
            RecalculateStats("login");
        }

        public List<SubPacket> Create0x132Packets()
        {
            List<SubPacket> packets = new List<SubPacket>();
            packets.Add(_0x132Packet.BuildPacket(actorId, 0xB, "commandForced"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0xA, "commandDefault"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x6, "commandWeak"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x4, "commandContent"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x6, "commandJudgeMode"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x100, "commandRequest"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x100, "widgetCreate"));
            packets.Add(_0x132Packet.BuildPacket(actorId, 0x100, "macroRequest"));
            return packets;
        }

        /*        
         * PLAYER ARGS:
         * Unknown - Bool 
         * Unknown - Bool
         * Is Init Director - Bool
         * Unknown - Bool
         * Unknown - Number
         * Unknown - Bool
         * Timer Array - 20 Number
        */

        public override SubPacket CreateScriptBindPacket(Player requestPlayer)
        {
            List<LuaParam> lParams;
            if (IsMyPlayer(requestPlayer.actorId))
            {
                if (loginInitDirector != null)
                    lParams = LuaUtils.CreateLuaParamList("/Chara/Player/Player_work", false, false, true, loginInitDirector, true, 0, false, timers, true);
                else
                    lParams = LuaUtils.CreateLuaParamList("/Chara/Player/Player_work", true, false, false, true, 0, false, timers, true);
            }
            else
                lParams = LuaUtils.CreateLuaParamList("/Chara/Player/Player_work", false, false, false, false, false, true);

            ActorInstantiatePacket.BuildPacket(actorId, actorName, className, lParams).DebugPrintSubPacket();


            return ActorInstantiatePacket.BuildPacket(actorId, actorName, className, lParams);
        }

        public override List<SubPacket> GetSpawnPackets(Player requestPlayer, ushort spawnType)
        {
            List<SubPacket> subpackets = new List<SubPacket>();
            subpackets.Add(CreateAddActorPacket(8));
            if (IsMyPlayer(requestPlayer.actorId))
                subpackets.AddRange(Create0x132Packets());
            subpackets.Add(CreateSpeedPacket());
            subpackets.Add(CreateSpawnPositonPacket(this, spawnType));
            subpackets.Add(CreateAppearancePacket());
            subpackets.Add(CreateNamePacket());
            subpackets.Add(_0xFPacket.BuildPacket(actorId));
            subpackets.Add(CreateStatePacket());
            subpackets.Add(CreateSubStatePacket());
            subpackets.Add(CreateInitStatusPacket());
            subpackets.Add(CreateSetActorIconPacket());
            subpackets.Add(CreateIsZoneingPacket());
            subpackets.AddRange(CreatePlayerRelatedPackets(requestPlayer.actorId));
            subpackets.Add(CreateScriptBindPacket(requestPlayer));
            return subpackets;
        }

        public List<SubPacket> CreatePlayerRelatedPackets(uint requestingPlayerActorId)
        {
            List<SubPacket> subpackets = new List<SubPacket>();

            if (gcCurrent != 0)
                subpackets.Add(SetGrandCompanyPacket.BuildPacket(actorId, gcCurrent, gcRankLimsa, gcRankGridania, gcRankUldah));

            if (currentTitle != 0)
                subpackets.Add(SetPlayerTitlePacket.BuildPacket(actorId, currentTitle));

            if (currentJob != 0)
                subpackets.Add(SetCurrentJobPacket.BuildPacket(actorId, currentJob));

            if (IsMyPlayer(requestingPlayerActorId))
            {
                subpackets.Add(SetSpecialEventWorkPacket.BuildPacket(actorId));

                if (hasChocobo && chocoboName != null && !chocoboName.Equals(""))
                {
                    subpackets.Add(SetChocoboNamePacket.BuildPacket(actorId, chocoboName));
                    subpackets.Add(SetHasChocoboPacket.BuildPacket(actorId, hasChocobo));
                }

                if (hasGoobbue)
                    subpackets.Add(SetHasGoobbuePacket.BuildPacket(actorId, hasGoobbue));

                subpackets.Add(SetAchievementPointsPacket.BuildPacket(actorId, achievementPoints));

                subpackets.Add(Database.GetLatestAchievements(this));
                subpackets.Add(Database.GetAchievementsPacket(this));
            }

            if (mountState == 1)
                subpackets.Add(SetCurrentMountChocoboPacket.BuildPacket(actorId, GetRideChocoboAppearance(), rentalExpireTime, rentalMinLeft));
            else if (mountState == 2)
                subpackets.Add(SetCurrentMountGoobbuePacket.BuildPacket(actorId, 1));

            //Inn Packets (Dream, Cutscenes, Armoire)   
            if (zone.isInn)
            {
                SetCutsceneBookPacket cutsceneBookPacket = new SetCutsceneBookPacket();
                for (int i = 0; i < 2048; i++)
                    cutsceneBookPacket.cutsceneFlags[i] = true;
                QueuePacket(cutsceneBookPacket.BuildPacket(actorId, "<Path Companion>", 11, 1, 1));
                QueuePacket(SetPlayerDreamPacket.BuildPacket(actorId, 0x16, GetInnCode()));
            }

            return subpackets;
        }

        public override List<SubPacket> GetInitPackets()
        {
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("/_init", this);

            propPacketUtil.AddProperty("charaWork.eventSave.bazaarTax");
            propPacketUtil.AddProperty("charaWork.battleSave.potencial");

            //Properties
            for (int i = 0; i < charaWork.property.Length; i++)
            {
                if (charaWork.property[i] != 0)
                    propPacketUtil.AddProperty(String.Format("charaWork.property[{0}]", i));
            }

            //Parameters
            propPacketUtil.AddProperty("charaWork.parameterSave.hp[0]");
            propPacketUtil.AddProperty("charaWork.parameterSave.hpMax[0]");
            propPacketUtil.AddProperty("charaWork.parameterSave.mp");
            propPacketUtil.AddProperty("charaWork.parameterSave.mpMax");
            propPacketUtil.AddProperty("charaWork.parameterTemp.tp");
            propPacketUtil.AddProperty("charaWork.parameterSave.state_mainSkill[0]");
            propPacketUtil.AddProperty("charaWork.parameterSave.state_mainSkillLevel");

            //Status Times
            for (int i = 0; i < charaWork.statusShownTime.Length; i++)
            {
                if (charaWork.statusShownTime[i] != 0)
                    propPacketUtil.AddProperty(String.Format("charaWork.statusShownTime[{0}]", i));
            }

            //General Parameters
            for (int i = 3; i < charaWork.battleTemp.generalParameter.Length; i++)
            {
                if (charaWork.battleTemp.generalParameter[i] != 0)
                    propPacketUtil.AddProperty(String.Format("charaWork.battleTemp.generalParameter[{0}]", i));
            }

            propPacketUtil.AddProperty("charaWork.battleTemp.castGauge_speed[0]");
            propPacketUtil.AddProperty("charaWork.battleTemp.castGauge_speed[1]");

            //Battle Save Skillpoint
            propPacketUtil.AddProperty(String.Format("charaWork.battleSave.skillPoint[{0}]", charaWork.parameterSave.state_mainSkill[0] - 1));

            //Commands
            propPacketUtil.AddProperty("charaWork.commandBorder");

            propPacketUtil.AddProperty("charaWork.battleSave.negotiationFlag[0]");

            for (int i = 0; i < charaWork.command.Length; i++)
            {
                if (charaWork.command[i] != 0)
                {
                    propPacketUtil.AddProperty(String.Format("charaWork.command[{0}]", i));
                    //Recast Timers
                    if (i >= charaWork.commandBorder)
                    {
                        propPacketUtil.AddProperty(String.Format("charaWork.parameterTemp.maxCommandRecastTime[{0}]", i - charaWork.commandBorder));
                        propPacketUtil.AddProperty(String.Format("charaWork.parameterSave.commandSlot_recastTime[{0}]", i - charaWork.commandBorder));
                    }
                }
            }

            for (int i = 0; i < charaWork.commandCategory.Length; i++)
            {
                charaWork.commandCategory[i] = 1;
                if (charaWork.commandCategory[i] != 0)
                    propPacketUtil.AddProperty(String.Format("charaWork.commandCategory[{0}]", i));
            }

            for (int i = 0; i < charaWork.commandAcquired.Length; i++)
            {
                if (charaWork.commandAcquired[i] != false)
                    propPacketUtil.AddProperty(String.Format("charaWork.commandAcquired[{0}]", i));
            }

            for (int i = 0; i < charaWork.additionalCommandAcquired.Length; i++)
            {
                if (charaWork.additionalCommandAcquired[i] != false)
                    propPacketUtil.AddProperty(String.Format("charaWork.additionalCommandAcquired[{0}]", i));
            }

            for (int i = 0; i < charaWork.parameterSave.commandSlot_compatibility.Length; i++)
            {
                charaWork.parameterSave.commandSlot_compatibility[i] = true;
                if (charaWork.parameterSave.commandSlot_compatibility[i])
                    propPacketUtil.AddProperty(String.Format("charaWork.parameterSave.commandSlot_compatibility[{0}]", i));
            }

            for (int i = 0; i < charaWork.parameterSave.commandSlot_recastTime.Length; i++)
            {
                if (charaWork.parameterSave.commandSlot_recastTime[i] != 0)
                    propPacketUtil.AddProperty(String.Format("charaWork.parameterSave.commandSlot_recastTime[{0}]", i));
            }

            //System
            propPacketUtil.AddProperty("charaWork.parameterTemp.forceControl_float_forClientSelf[0]");
            propPacketUtil.AddProperty("charaWork.parameterTemp.forceControl_float_forClientSelf[1]");
            propPacketUtil.AddProperty("charaWork.parameterTemp.forceControl_int16_forClientSelf[0]");
            propPacketUtil.AddProperty("charaWork.parameterTemp.forceControl_int16_forClientSelf[1]");

            charaWork.parameterTemp.otherClassAbilityCount[0] = 4;
            charaWork.parameterTemp.otherClassAbilityCount[1] = 5;
            charaWork.parameterTemp.giftCount[1] = 5;

            propPacketUtil.AddProperty("charaWork.parameterTemp.otherClassAbilityCount[0]");
            propPacketUtil.AddProperty("charaWork.parameterTemp.otherClassAbilityCount[1]");
            propPacketUtil.AddProperty("charaWork.parameterTemp.giftCount[1]");

            propPacketUtil.AddProperty("charaWork.depictionJudge");

            //Scenario
            for (int i = 0; i < playerWork.questScenario.Length; i++)
            {
                if (playerWork.questScenario[i] != 0)
                    propPacketUtil.AddProperty(String.Format("playerWork.questScenario[{0}]", i));
            }

            //Guildleve - Local
            for (int i = 0; i < playerWork.questGuildleve.Length; i++)
            {
                if (playerWork.questGuildleve[i] != 0)
                    propPacketUtil.AddProperty(String.Format("playerWork.questGuildleve[{0}]", i));
            }

            //Guildleve - Regional
            for (int i = 0; i < work.guildleveId.Length; i++)
            {
                if (work.guildleveId[i] != 0)
                    propPacketUtil.AddProperty(String.Format("work.guildleveId[{0}]", i));
                if (work.guildleveDone[i] != false)
                    propPacketUtil.AddProperty(String.Format("work.guildleveDone[{0}]", i));
                if (work.guildleveChecked[i] != false)
                    propPacketUtil.AddProperty(String.Format("work.guildleveChecked[{0}]", i));
            }

            //Bazaar
            CheckBazaarFlags(true);
            if (charaWork.eventSave.repairType != 0)
                propPacketUtil.AddProperty("charaWork.eventSave.repairType");
            if (charaWork.eventTemp.bazaarRetail)
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarRetail");
            if (charaWork.eventTemp.bazaarRepair)
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarRepair");
            if (charaWork.eventTemp.bazaarMateria)
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarMateria");

            //NPC Linkshell            
            for (int i = 0; i < playerWork.npcLinkshellChatCalling.Length; i++)
            {
                if (playerWork.npcLinkshellChatCalling[i] != false)
                    propPacketUtil.AddProperty(String.Format("playerWork.npcLinkshellChatCalling[{0}]", i));
                if (playerWork.npcLinkshellChatExtra[i] != false)
                    propPacketUtil.AddProperty(String.Format("playerWork.npcLinkshellChatExtra[{0}]", i));
            }

            propPacketUtil.AddProperty("playerWork.restBonusExpRate");

            //Profile
            propPacketUtil.AddProperty("playerWork.tribe");
            propPacketUtil.AddProperty("playerWork.guardian");
            propPacketUtil.AddProperty("playerWork.birthdayMonth");
            propPacketUtil.AddProperty("playerWork.birthdayDay");
            propPacketUtil.AddProperty("playerWork.initialTown");

            return propPacketUtil.Done();
        }

        public void SendSeamlessZoneInPackets()
        {
            QueuePacket(SetMusicPacket.BuildPacket(actorId, zone.bgmDay, SetMusicPacket.EFFECT_FADEIN));
            QueuePacket(SetWeatherPacket.BuildPacket(actorId, SetWeatherPacket.WEATHER_CLEAR, 1));
            QueuePacket(SetMapPacket.BuildPacket(actorId, zone.regionId, zone.actorId));
        }

        public void SendZoneInPackets(
            WorldManager world,
            ushort spawnType,
            ZoneInventoryRefreshMode inventoryRefreshMode = ZoneInventoryRefreshMode.Full)
        {
            QueuePacket(SetActorIsZoningPacket.BuildPacket(actorId, false));
            QueuePacket(SetDalamudPacket.BuildPacket(actorId, 0));

            //Music Packets
            if (currentMainState == SetActorStatePacket.MAIN_STATE_MOUNTED)
            {
                if (rentalExpireTime != 0)
                    QueuePacket(SetMusicPacket.BuildPacket(actorId, 64, SetMusicPacket.EFFECT_FADEIN)); //Rental
                else
                {
                    if (mountState == 1)
                        QueuePacket(SetMusicPacket.BuildPacket(actorId, 83, SetMusicPacket.EFFECT_FADEIN)); //Mount
                    else
                        QueuePacket(SetMusicPacket.BuildPacket(actorId, 98, 0x01)); //Goobbue
                }
            }
            else
                QueuePacket(SetMusicPacket.BuildPacket(actorId, zone.bgmDay, 0x01)); //Zone

            QueuePacket(SetWeatherPacket.BuildPacket(actorId, SetWeatherPacket.WEATHER_CLEAR, 1));

            QueuePacket(SetMapPacket.BuildPacket(actorId, zone.regionId, zone.actorId));

            List<SubPacket> selfSpawnPackets = GetSpawnPackets(this, spawnType);
            QueuePackets(selfSpawnPackets);

            #region Inventory & Equipment
            QueuePacket(InventoryBeginChangePacket.BuildPacket(actorId, true));
            bool resendItemDefinitions = ZoneInventoryRefreshPolicy.ShouldResendItemDefinitions(inventoryRefreshMode);
            ushort[] zoneInPackages =
            {
                ItemPackage.NORMAL,
                ItemPackage.CURRENCY_CRYSTALS,
                ItemPackage.KEYITEMS,
                ItemPackage.BAZAAR,
                ItemPackage.MELDREQUEST,
                ItemPackage.LOOT
            };
            foreach (ushort packageCode in zoneInPackages)
            {
                if (resendItemDefinitions)
                    itemPackages[packageCode].SendFullPackage(this);
                else
                    itemPackages[packageCode].SendPackageEnvelope(this);
            }
            equipment.SendUpdate(this);
            playerSession.QueuePacket(InventoryEndChangePacket.BuildPacket(actorId));
            #endregion

            playerSession.QueuePacket(GetInitPackets());

            List<SubPacket> areaMasterSpawn = zone.GetSpawnPackets();
            List<SubPacket> debugSpawn = world.GetDebugActor().GetSpawnPackets();
            List<SubPacket> worldMasterSpawn = world.GetActor().GetSpawnPackets();

            playerSession.QueuePacket(areaMasterSpawn);
            playerSession.QueuePacket(debugSpawn);
            playerSession.QueuePacket(worldMasterSpawn);

            int weatherDirectorPackets = 0;
            if (zone.GetWeatherDirector() != null)
            {
                List<SubPacket> weatherDirectorSpawn = zone.GetWeatherDirector().GetSpawnPackets();
                weatherDirectorPackets = weatherDirectorSpawn.Count;
                playerSession.QueuePacket(weatherDirectorSpawn);
            }

            int ownedDirectorSpawnPackets = 0;
            int ownedDirectorInitPackets = 0;
            int ownedDirectorEventStatusPackets = 0;
            IEnumerable<Director> zoneInDirectors = ownedDirectors
                .Where(director => director.zoneId == zoneId && !director.IsDeleted());
            if (zone is PrivateAreaContent contentArea)
            {
                Director activeContentDirector = contentArea.GetContentDirector();
                zoneInDirectors = ownedDirectors.Where(director => director == activeContentDirector);
            }
            Director[] sentDirectors = zoneInDirectors.ToArray();
            foreach (Director director in sentDirectors)
            {
                List<SubPacket> directorSpawnPackets = director.GetSpawnPackets();
                List<SubPacket> directorInitPackets = director.GetInitPackets();
                List<SubPacket> directorEventStatusPackets = director.GetSetEventStatusPackets();
                ownedDirectorSpawnPackets += directorSpawnPackets.Count;
                ownedDirectorInitPackets += directorInitPackets.Count;
                ownedDirectorEventStatusPackets += directorEventStatusPackets.Count;
                QueuePackets(directorSpawnPackets);
                QueuePackets(directorInitPackets);
                QueuePackets(directorEventStatusPackets);
            }

            if (currentContentGroup != null)
            {
                currentContentGroup.SendGroupPackets(playerSession);
                currentContentGroup.StartAfterZoneIn();
            }

            if (currentParty != null)
                currentParty.SendGroupPackets(playerSession);

            DevDiagnostics.Trace(
                "zone.in.packets",
                "player", customDisplayName,
                "zone", zoneId,
                "zoneActor", zone == null ? "0x0" : String.Format("0x{0:X}", zone.actorId),
                "areaKind", zone == null ? "" : zone.GetType().Name,
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "spawnType", spawnType,
                "inventoryRefreshMode", inventoryRefreshMode.ToString(),
                "inventoryItemDefinitionsResent", resendItemDefinitions,
                "selfSpawnPackets", selfSpawnPackets.Count,
                "areaMasterPackets", areaMasterSpawn.Count,
                "debugPackets", debugSpawn.Count,
                "worldPackets", worldMasterSpawn.Count,
                "weatherDirectorPackets", weatherDirectorPackets,
                "ownedDirectorCount", ownedDirectors.Count,
                "zoneInDirectorCount", sentDirectors.Length,
                "ownedDirectorSpawnPackets", ownedDirectorSpawnPackets,
                "ownedDirectorInitPackets", ownedDirectorInitPackets,
                "ownedDirectorEventStatusPackets", ownedDirectorEventStatusPackets,
                "hasContentGroup", currentContentGroup != null,
                "hasParty", currentParty != null);
        }

        /// <summary>
        /// Commits the actors instantiated by a zone bootstrap using the
        /// retail 0x0006, chunked 0x0008, 0x0007 keep-list sequence.
        /// </summary>
        public void SendZoneInstanceSnapshot(WorldManager world)
        {
            if (world == null || zone == null)
                return;

            List<uint> actorIds = new List<uint>();
            HashSet<uint> seenActorIds = new HashSet<uint>();
            Action<uint> addActorId = id =>
            {
                if (id != 0 && seenActorIds.Add(id))
                    actorIds.Add(id);
            };

            addActorId(actorId);
            addActorId(zone.actorId);
            addActorId(world.GetDebugActor().actorId);
            addActorId(world.GetActor().actorId);

            Director weatherDirector = zone.GetWeatherDirector();
            if (weatherDirector != null)
                addActorId(weatherDirector.actorId);

            IEnumerable<Director> zoneInDirectors = ownedDirectors
                .Where(director => director.zoneId == zoneId && !director.IsDeleted());
            if (zone is PrivateAreaContent contentArea)
            {
                Director activeContentDirector = contentArea.GetContentDirector();
                zoneInDirectors = ownedDirectors.Where(director => director == activeContentDirector);
            }

            foreach (Director director in zoneInDirectors)
                addActorId(director.actorId);

            foreach (Actor actor in playerSession.actorInstanceList)
            {
                if (actor != null)
                    addActorId(actor.actorId);
            }

            QueuePacket(ServerZoneInstanceBeginPacket.BuildPacket(actorId));
            for (int offset = 0; offset < actorIds.Count; offset += ServerZoneInstanceActorsPacket.MAXIMUM_ACTORS)
            {
                int count = Math.Min(ServerZoneInstanceActorsPacket.MAXIMUM_ACTORS, actorIds.Count - offset);
                QueuePacket(ServerZoneInstanceActorsPacket.BuildPacket(actorId, actorIds.GetRange(offset, count)));
            }
            QueuePacket(ServerZoneInstanceEndPacket.BuildPacket(actorId));

            DevDiagnostics.Trace(
                "zone.instance.snapshot",
                "player", customDisplayName,
                "zone", zoneId,
                "zoneActor", String.Format("0x{0:X}", zone.actorId),
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "actorCount", actorIds.Count,
                "chunkCount", (actorIds.Count + ServerZoneInstanceActorsPacket.MAXIMUM_ACTORS - 1) / ServerZoneInstanceActorsPacket.MAXIMUM_ACTORS,
                "actorIds", String.Join(",", actorIds.Select(id => String.Format("0x{0:X8}", id))));
        }

        private void SendRemoveInventoryPackets(List<ushort> slots)
        {
            int currentIndex = 0;

            while (true)
            {
                if (slots.Count - currentIndex >= 64)
                    QueuePacket(InventoryRemoveX64Packet.BuildPacket(actorId, slots, ref currentIndex));
                else if (slots.Count - currentIndex >= 32)
                    QueuePacket(InventoryRemoveX32Packet.BuildPacket(actorId, slots, ref currentIndex));
                else if (slots.Count - currentIndex >= 16)
                    QueuePacket(InventoryRemoveX16Packet.BuildPacket(actorId, slots, ref currentIndex));
                else if (slots.Count - currentIndex >= 8)
                    QueuePacket(InventoryRemoveX08Packet.BuildPacket(actorId, slots, ref currentIndex));
                else if (slots.Count - currentIndex == 1)
                    QueuePacket(InventoryRemoveX01Packet.BuildPacket(actorId, slots[currentIndex]));
                else
                    break;
            }

        }

        public bool IsMyPlayer(uint otherActorId)
        {
            return actorId == otherActorId;
        }

        public void QueuePacket(SubPacket packet)

        {
            playerSession.QueuePacket(packet);
        }

        public void QueuePackets(List<SubPacket> packets)
        {
            playerSession.QueuePacket(packets);
        }

        public void SendPacket(string path)
        {
            try
            {
                BasePacket packet = new BasePacket(path);

                packet.ReplaceActorID(actorId);
                var packets = packet.GetSubpackets();
                QueuePackets(packets);
            }
            catch (Exception e)
            {
                this.SendMessage(SendMessagePacket.MESSAGE_TYPE_SYSTEM_ERROR, "[SendPacket]", "Unable to send packet.");
                this.SendMessage(SendMessagePacket.MESSAGE_TYPE_SYSTEM_ERROR, "[SendPacket]", e.Message);
            }
        }

        public void BroadcastPackets(List<SubPacket> packets, bool sendToSelf)
        {
            foreach (SubPacket packet in packets)
            {
                if (sendToSelf)
                {

                    SubPacket clonedPacket = new SubPacket(packet, actorId);
                    QueuePacket(clonedPacket);
                }

                foreach (Actor a in playerSession.actorInstanceList)
                {
                    if (a is Player)
                    {
                        Player p = (Player)a;

                        if (p.Equals(this))
                            continue;

                        SubPacket clonedPacket = new SubPacket(packet, a.actorId);
                        p.QueuePacket(clonedPacket);
                    }
                }
            }
        }

        public void BroadcastPacket(SubPacket packet, bool sendToSelf)
        {
            if (sendToSelf)
            {
                SubPacket clonedPacket = new SubPacket(packet, actorId);
                QueuePacket(clonedPacket);
            }

            foreach (Actor a in playerSession.actorInstanceList)
            {
                if (a is Player)
                {
                    Player p = (Player)a;

                    if (p.Equals(this))
                        continue;

                    SubPacket clonedPacket = new SubPacket(packet, a.actorId);
                    p.QueuePacket(clonedPacket);
                }
            }
        }

        public void ChangeAnimation(uint animId)
        {
            Actor a = zone.FindActorInArea(currentTarget);
            if (a is Npc)
                ((Npc)a).animationId = animId;
        }

        public void SetDCFlag(bool flag)
        {
            if (flag)
            {
                BroadcastPacket(SetActorIconPacket.BuildPacket(actorId, SetActorIconPacket.DISCONNECTING), true);
            }
            else
            {
                if (isGM)
                    BroadcastPacket(SetActorIconPacket.BuildPacket(actorId, SetActorIconPacket.ISGM), true);
                else
                    BroadcastPacket(SetActorIconPacket.BuildPacket(actorId, 0), true);
            }
        }

        public void CleanupAndSave()
        {
            playerSession.LockUpdates(true);

            // Rental state is intentionally session-scoped in 1.x. Logging out
            // (including a disconnect) ends the ride immediately.
            if (GetMountState() != 0 || IsChocoboRentalActive())
                ChocoboService.EndRide(this, false);

            //Remove actor from zone and main server list
            if (zone != null)
            {
                zone.RemoveActorFromZone(this);
            }
            else
            {
                DevDiagnostics.Trace(
                    "player.cleanup.missingZone",
                    "player", customDisplayName,
                    "zone", zoneId,
                    "privateArea", privateArea ?? "",
                    "privateAreaType", privateAreaType,
                    "destinationZone", destinationZone,
                    "spawnType", destinationSpawnType,
                    "x", positionX,
                    "y", positionY,
                    "z", positionZ,
                    "rot", rotation);
            }

            // Keep pending zone-in state if the client disconnected before confirming position.
            bool preservePendingZoneChange = IsInZoneChange() && (this.destinationZone != 0 || this.destinationSpawnType != 0);
            if (preservePendingZoneChange)
            {
                DevDiagnostics.Trace(
                    "zone.change.disconnect.pending",
                    "player", customDisplayName,
                    "zone", zoneId,
                    "privateArea", privateArea ?? "",
                    "privateAreaType", privateAreaType,
                    "destinationZone", destinationZone,
                    "spawnType", destinationSpawnType,
                    "x", positionX,
                    "y", positionY,
                    "z", positionZ,
                    "rot", rotation);
            }
            else
            {
                //Set Destination to 0
                this.destinationZone = 0;
                this.destinationSpawnType = 0;
            }

            //Clean up parties
            RemoveFromCurrentPartyAndCleanup();

            //Save Player
            Database.SavePlayerPlayTime(this);
            if (preservePendingZoneChange)
            {
                DevDiagnostics.Trace(
                    "zone.change.disconnect.positionSaveSkipped",
                    "player", customDisplayName,
                    "zone", zoneId,
                    "privateArea", privateArea ?? "",
                    "privateAreaType", privateAreaType,
                    "destinationZone", destinationZone,
                    "spawnType", destinationSpawnType,
                    "x", positionX,
                    "y", positionY,
                    "z", positionZ,
                    "rot", rotation);
            }
            else
            {
                Database.SavePlayerPosition(this);
            }
            Database.SavePlayerStatusEffects(this);
        }

        public void CleanupAndSave(uint destinationZone, ushort spawnType, float destinationX, float destinationY, float destinationZ, float destinationRot)
        {
            playerSession.LockUpdates(true);

            //Remove actor from zone and main server list
            if (zone != null)
            {
                zone.RemoveActorFromZone(this);
            }
            else
            {
                DevDiagnostics.Trace(
                    "player.cleanupForZoneChange.missingZone",
                    "player", customDisplayName,
                    "zone", zoneId,
                    "privateArea", privateArea ?? "",
                    "privateAreaType", privateAreaType,
                    "destinationZone", destinationZone,
                    "spawnType", destinationSpawnType,
                    "x", positionX,
                    "y", positionY,
                    "z", positionZ,
                    "rot", rotation);
            }

            //Clean up parties
            RemoveFromCurrentPartyAndCleanup();

            //Set destination
            this.destinationZone = destinationZone;
            this.destinationSpawnType = spawnType;
            this.positionX = destinationX;
            this.positionY = destinationY;
            this.positionZ = destinationZ;
            this.rotation = destinationRot;

            this.statusEffects.RemoveStatusEffectsByFlags((uint)StatusEffectFlags.LoseOnZoning);

            //Save Player
            Database.SavePlayerPlayTime(this);
            Database.SavePlayerPosition(this);
            Database.SavePlayerStatusEffects(this);
        }

        public new Area GetZone()
        {
            return zone;
        }

        public void SendMessage(uint logType, string sender, string message)
        {
            QueuePacket(SendMessagePacket.BuildPacket(actorId, logType, sender, message));
        }

        //Only use at logout since it's intensive
        private byte GetInnCode()
        {
            if (zone.isInn)
            {
                Vector3 position = new Vector3(positionX, 0, positionZ);
                if (Utils.Distance(position, new Vector3(0, 0, 0)) <= 20f)
                    return 3;
                else if (Utils.Distance(position, new Vector3(160, 0, 160)) <= 20f)
                    return 2;
                else if (Utils.Distance(position, new Vector3(-160, 0, -160)) <= 20f)
                    return 1;
            }
            return 0;
        }

        public void SetSleeping()
        {
            playerSession.LockUpdates(true);
            switch(GetInnCode())
            {
                case 1:
                    positionX = -162.42f;
                    positionY = 0f;
                    positionZ = -154.21f;
                    rotation = 1.56f;
                    break;
                case 2:
                    positionX = 157.55f;
                    positionY = 0f;
                    positionZ = 165.05f;
                    rotation = 1.53f;
                    break;
                case 3:
                    positionX = -2.65f;
                    positionY = 0f;
                    positionZ = 3.94f;
                    rotation = 1.52f;
                    break;
            }
        }

        public void Logout()
        {
            EndClientSession("logout", LogoutPacket.BuildPacket(actorId));
        }

        public void QuitGame()
        {
            EndClientSession("quit", QuitPacket.BuildPacket(actorId));
        }

        private void EndClientSession(string reason, SubPacket clientTransitionPacket)
        {
            DevDiagnostics.Trace(
                "player.logout.request",
                "player", actorId,
                "playerName", customDisplayName,
                "reason", reason,
                "session", playerSession.id,
                "zone", zoneId);

            QueuePacket(clientTransitionPacket);

            // Stop accepting client gameplay immediately while the client owns
            // the terminal World-socket transition.
            Server.GetServer().BeginSessionEnd(playerSession.id);

            DevDiagnostics.Trace(
                "player.logout.packet",
                "player", actorId,
                "playerName", customDisplayName,
                "reason", reason,
                "session", playerSession.id,
                "opcode", String.Format("0x{0:X4}", clientTransitionPacket.gameMessage.opcode));

            statusEffects.RemoveStatusEffectsByFlags((uint)StatusEffectFlags.LoseOnLogout);
            CleanupAndSave();

            DevDiagnostics.Trace(
                "player.logout.cleanup",
                "player", actorId,
                "playerName", customDisplayName,
                "reason", reason,
                "session", playerSession.id);

            if (PlayerSessionTransitionPolicy.ClientOwnsWorldDisconnect(
                clientTransitionPacket.gameMessage.opcode))
            {
                DevDiagnostics.Trace(
                    "player.logout.awaitDisconnect",
                    "player", actorId,
                    "playerName", customDisplayName,
                    "reason", reason,
                    "session", playerSession.id);
                return;
            }

            // Reserved for a future transition opcode whose protocol explicitly
            // requires Map to initiate teardown. Logout and Quit never use this
            // path: after the client disconnects, World requests session end and
            // Map returns the normal confirmation through PacketProcessor.
            playerSession.QueuePacket(SessionEndConfirmPacket.BuildPacket(playerSession, 0));

            DevDiagnostics.Trace(
                "player.logout.sessionEnd",
                "player", actorId,
                "playerName", customDisplayName,
                "reason", reason,
                "session", playerSession.id,
                "destinationZone", 0);

        }

        public uint GetPlayTime(bool doUpdate)
        {
            if (doUpdate)
            {
                uint curTime = Utils.UnixTimeStampUTC();
                playTime += curTime - lastPlayTimeUpdate;
                lastPlayTimeUpdate = curTime;
            }

            return playTime;
        }

        public void SavePlayTime()
        {
            Database.SavePlayerPlayTime(this);
        }

        public void ChangeMusic(ushort musicId)
        {
            QueuePacket(SetMusicPacket.BuildPacket(actorId, musicId, 1));
        }

        public void ChangeMusic(ushort musicId, ushort trackMode)
        {
            QueuePacket(SetMusicPacket.BuildPacket(actorId, musicId, trackMode));
        }

        public void SendMountAppearance()
        {
            if (mountState == 1)
                BroadcastPacket(SetCurrentMountChocoboPacket.BuildPacket(actorId, GetRideChocoboAppearance(), rentalExpireTime, rentalMinLeft), true);
            else if (mountState == 2)
                BroadcastPacket(SetCurrentMountGoobbuePacket.BuildPacket(actorId, 1), true);
        }

        public void SetMountState(byte mountState)
        {
            this.mountState = mountState;
            SendMountAppearance();
        }

        public byte GetMountState()
        {
            return mountState;
        }

        public byte GetRideChocoboAppearance()
        {
            return chocoboRideKind == ChocoboRideKind.Rental
                ? rentalChocoboAppearance
                : chocoboAppearance;
        }

        public void DoEmote(uint targettedActor, uint animId, uint descId)
        {
            BroadcastPacket(ActorDoEmotePacket.BuildPacket(actorId, targettedActor, animId, descId), true);
        }

        public void SendGameMessage(Actor sourceActor, Actor textIdOwner, ushort textId, byte log, params object[] msgParams)
        {
            TraceGameMessage("source", sourceActor, textIdOwner, textId, log, "", 0, false, msgParams);

            if (msgParams == null || msgParams.Length == 0)
            {
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, sourceActor.actorId, textIdOwner.actorId, textId, log));
            }
            else
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, sourceActor.actorId, textIdOwner.actorId, textId, log, LuaUtils.CreateLuaParamList(msgParams)));
        }

        public void SendGameMessage(Actor textIdOwner, ushort textId, byte log, params object[] msgParams)
        {
            TraceGameMessage("default", null, textIdOwner, textId, log, "", 0, false, msgParams);

            if (msgParams == null || msgParams.Length == 0)
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, log));
            else
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, log, LuaUtils.CreateLuaParamList(msgParams)));
        }

        public void SendGameMessageCustomSender(Actor textIdOwner, ushort textId, byte log, string customSender, params object[] msgParams)
        {
            TraceGameMessage("customSender", null, textIdOwner, textId, log, customSender, 0, false, msgParams);

            if (msgParams == null || msgParams.Length == 0)
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, customSender, log));
            else
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, customSender, log, LuaUtils.CreateLuaParamList(msgParams)));
        }

        public void SendGameMessageDisplayIDSender(Actor textIdOwner, ushort textId, byte log, uint displayId, params object[] msgParams)
        {
            TraceGameMessage("displayIdSender", null, textIdOwner, textId, log, "", displayId, true, msgParams);

            if (msgParams == null || msgParams.Length == 0)
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, displayId, log));
            else
                QueuePacket(GameMessagePacket.BuildPacket(Server.GetWorldManager().GetActor().actorId, textIdOwner.actorId, textId, displayId, log, LuaUtils.CreateLuaParamList(msgParams)));
        }

        private static string FormatTraceActorId(Actor actor)
        {
            return actor == null ? "0x0" : String.Format("0x{0:X}", actor.actorId);
        }

        private static string FormatTraceActorName(Actor actor)
        {
            return actor == null ? "" : actor.actorName;
        }

        private static string FormatTraceMessageParams(object[] msgParams)
        {
            if (msgParams == null || msgParams.Length == 0)
                return "";

            string[] parts = new string[msgParams.Length];
            for (int i = 0; i < msgParams.Length; i++)
                parts[i] = msgParams[i] == null ? "nil" : msgParams[i].ToString();

            return String.Join(", ", parts);
        }

        private void TraceGameMessage(string mode, Actor sourceActor, Actor textIdOwner, ushort textId, byte log, string customSender, uint displayId, bool hasDisplayId, object[] msgParams)
        {
            if (!DevDiagnostics.Enabled)
                return;

            DevDiagnostics.Trace(
                "game.message",
                "player", customDisplayName,
                "mode", mode,
                "source", FormatTraceActorId(sourceActor),
                "sourceName", FormatTraceActorName(sourceActor),
                "textOwner", FormatTraceActorId(textIdOwner),
                "textOwnerName", FormatTraceActorName(textIdOwner),
                "textId", textId,
                "log", log,
                "customSender", customSender ?? "",
                "displayId", hasDisplayId ? displayId.ToString() : "",
                "params", FormatTraceMessageParams(msgParams));
        }

        public void BroadcastWorldMessage(ushort worldMasterId, params object[] msgParams)
        {
            //SubPacket worldMasterMessage = 
            //zone.BroadcastPacketAroundActor(this, worldMasterMessage);
        }

        public void GraphicChange(uint slot, uint graphicId)
        {
            appearanceIds[slot] = graphicId;           
        }

        public void GraphicChange(uint slot, uint weapId, uint equipId, uint variantId, uint colorId)
        {

            uint mixedVariantId;

            if (weapId == 0)
                mixedVariantId = ((variantId & 0x1F) << 5) | colorId;
            else
                mixedVariantId = variantId;

            uint graphicId =
                    (weapId & 0x3FF)  << 20 |
                    (equipId & 0x3FF) << 10 |
                    (mixedVariantId & 0x3FF);

            appearanceIds[slot] = graphicId;            
            
        }

        public void GraphicChange(int slot, InventoryItem invItem)
        {
            if (invItem == null)
                appearanceIds[slot] = 0;
            else
            {
                ItemData item = Server.GetItemGamedata(invItem.itemId);

                if (item is EquipmentItem)
                {
                    EquipmentItem eqItem = (EquipmentItem)item;

                    uint mixedVariantId;

                    if (eqItem.graphicsWeaponId == 0)
                        mixedVariantId = ((eqItem.graphicsVariantId & 0x1F) << 5) | eqItem.graphicsColorId;
                    else
                        mixedVariantId = eqItem.graphicsVariantId;

                    uint graphicId =
                            (eqItem.graphicsWeaponId & 0x3FF) << 20 |
                            (eqItem.graphicsEquipmentId & 0x3FF) << 10 |
                            (mixedVariantId & 0x3FF);

                    appearanceIds[slot] = graphicId;
                }

                //Handle offhand
                if (slot == MAINHAND && item is WeaponItem)
                {
                    WeaponItem wpItem = (WeaponItem)item;

                    uint graphicId =
                            (wpItem.graphicsOffhandWeaponId & 0x3FF) << 20 |
                            (wpItem.graphicsOffhandEquipmentId & 0x3FF) << 10 |
                            (wpItem.graphicsOffhandVariantId & 0x3FF);

                    if (graphicId != 0)
                        appearanceIds[SetActorAppearancePacket.OFFHAND] = graphicId;
                }

                //Handle ALC offhand special case
                if (slot == OFFHAND && item is WeaponItem && item.IsAlchemistWeapon())
                {
                    WeaponItem wpItem = (WeaponItem)item;

                    uint graphicId =
                            ((wpItem.graphicsWeaponId + 1) & 0x3FF) << 20 |
                            (wpItem.graphicsEquipmentId & 0x3FF) << 10 |
                            (wpItem.graphicsVariantId & 0x3FF);

                    if (graphicId != 0)
                        appearanceIds[SetActorAppearancePacket.SPOFFHAND] = graphicId;
                }
            }

            Database.SavePlayerAppearance(this);
            BroadcastPacket(CreateAppearancePacket(), true);
        }

        public void SendAppearance()
        {
            BroadcastPacket(CreateAppearancePacket(), true);
        }

        public void SendCharaExpInfo()
        {
            if (lastStep == 0)
            {
                int maxLength;
                if ((sizeof(short) * charaWork.battleSave.skillLevel.Length)-lastPosition < 0x5E)
                    maxLength = (sizeof(short) * charaWork.battleSave.skillLevel.Length) - lastPosition;
                else
                    maxLength = 0x5E;

                byte[] skillLevelBuffer = new byte[maxLength];
                Buffer.BlockCopy(charaWork.battleSave.skillLevel, 0, skillLevelBuffer, 0, skillLevelBuffer.Length);
                SetActorPropetyPacket charaInfo1 = new SetActorPropetyPacket("charaWork/exp");

                charaInfo1.SetIsArrayMode(true);
                if (maxLength == 0x5E)
                {
                    charaInfo1.AddBuffer(Utils.MurmurHash2("charaWork.battleSave.skillLevel", 0), skillLevelBuffer, 0, skillLevelBuffer.Length, 0x0);
                    lastPosition += maxLength;
                }
                else
                {
                    charaInfo1.AddBuffer(Utils.MurmurHash2("charaWork.battleSave.skillLevel", 0), skillLevelBuffer, 0, skillLevelBuffer.Length, 0x3);
                    lastPosition = 0;
                    lastStep++;
                }

                charaInfo1.AddTarget();

                QueuePacket(charaInfo1.BuildPacket(actorId));
            }
            else if (lastStep == 1)
            {
                int maxLength;
                if ((sizeof(short) * charaWork.battleSave.skillLevelCap.Length) - lastPosition < 0x5E)
                    maxLength = (sizeof(short) * charaWork.battleSave.skillLevelCap.Length) - lastPosition;
                else
                    maxLength = 0x5E;

                byte[] skillCapBuffer = new byte[maxLength];
                Buffer.BlockCopy(charaWork.battleSave.skillLevelCap, lastPosition, skillCapBuffer, 0, skillCapBuffer.Length);
                SetActorPropetyPacket charaInfo1 = new SetActorPropetyPacket("charaWork/exp");

                
                if (maxLength == 0x5E)
                {
                    charaInfo1.SetIsArrayMode(true);
                    charaInfo1.AddBuffer(Utils.MurmurHash2("charaWork.battleSave.skillLevelCap", 0), skillCapBuffer, 0, skillCapBuffer.Length, 0x1);
                    lastPosition += maxLength;
                }
                else
                {
                    charaInfo1.SetIsArrayMode(false);
                    charaInfo1.AddBuffer(Utils.MurmurHash2("charaWork.battleSave.skillLevelCap", 0), skillCapBuffer, 0, skillCapBuffer.Length, 0x3);
                    lastStep = 0;
                    lastPosition = 0;
                }

                charaInfo1.AddTarget();

                QueuePacket(charaInfo1.BuildPacket(actorId));
            }
           
        }

        public int GetHighestLevel()
        {
            int max = 0;
            foreach (short level in charaWork.battleSave.skillLevel)
            {
                if (level > max)
                    max = level;
            }
            return max;
        }

        public InventoryItem[] GetGearset(ushort classId)
        {
            return Database.GetEquipment(this, classId);
        }

        public void PrepareClassChange(byte classId)
        {
            SendCharaExpInfo();
        }

        public void DoClassChange(byte classId)
        {
            //load hotbars
            //Calculate stats
            //Calculate hp/mp

            //Get Potenciel ??????

            //Set HP/MP/TP PARAMS

            //Set mainskill and level

            //Set Parameters

            //Set current EXP

            //Set Hotbar Commands 1
            //Set Hotbar Commands 2
            //Set Hotbar Commands 3

            //Check if bonus point available... set

            //Remove buffs that fall off when changing class
            CommandResultContainer resultContainer = new CommandResultContainer();
            statusEffects.RemoveStatusEffectsByFlags((uint)StatusEffectFlags.LoseOnClassChange, resultContainer);
            resultContainer.CombineLists();
            DoBattleAction(0, 0x7c000062, resultContainer.GetList());

            if (currentJob != 0 && ConvertJobIdToClassId((byte)currentJob) != classId)
                SetCurrentJob(0);

            //If new class, init abilties and level
            if (charaWork.battleSave.skillLevel[classId - 1] <= 0)
            {
                UpdateClassLevel(classId, 1);
                EquipAbilitiesAtLevel(classId, 1);
            }

            //Set rested EXP
            charaWork.parameterSave.state_mainSkill[0] = classId;
            charaWork.parameterSave.state_mainSkillLevel = charaWork.battleSave.skillLevel[classId-1];
            playerWork.restBonusExpRate = 0.0f;
            for(int i = charaWork.commandBorder; i < charaWork.command.Length; i++)
            {
                charaWork.command[i] = 0;
                charaWork.commandCategory[i] = 0;
            }

            //If new class, init abilties and level
            if (charaWork.battleSave.skillLevel[classId - 1] <= 0)
            {
                UpdateClassLevel(classId, 1);
                EquipAbilitiesAtLevel(classId, 1);
            }

            ActorPropertyPacketUtil propertyBuilder = new ActorPropertyPacketUtil("charaWork/stateForAll", this);

            propertyBuilder.AddProperty("charaWork.parameterSave.state_mainSkill[0]");
            propertyBuilder.AddProperty("charaWork.parameterSave.state_mainSkillLevel");
            propertyBuilder.NewTarget("playerWork/expBonus");
            propertyBuilder.AddProperty("playerWork.restBonusExpRate");
            propertyBuilder.NewTarget("charaWork/battleStateForSelf");
            propertyBuilder.AddProperty(String.Format("charaWork.battleSave.skillPoint[{0}]", classId - 1));
            Database.LoadHotbar(this);

            var time = Utils.UnixTimeStampUTC();
            for(int i = charaWork.commandBorder; i < charaWork.command.Length; i++)
            {
                if(charaWork.command[i] != 0)
                {
                    charaWork.parameterSave.commandSlot_recastTime[i - charaWork.commandBorder] = time + charaWork.parameterTemp.maxCommandRecastTime[i - charaWork.commandBorder];
                }
            }

            UpdateHotbar();

            List<SubPacket> packets = propertyBuilder.Done();

            foreach (SubPacket packet in packets)
                BroadcastPacket(packet, true);

            Database.SavePlayerCurrentClass(this);
            RecalculateStats();
        }

        public void UpdateClassLevel(byte classId, short level)
        {
            Database.PlayerCharacterUpdateClassLevel(this, classId, level);
            charaWork.battleSave.skillLevel[classId - 1] = level;
            ActorPropertyPacketUtil propertyBuilder = new ActorPropertyPacketUtil("charaWork/stateForAll", this);
            propertyBuilder.AddProperty(String.Format("charaWork.battleSave.skillLevel[{0}]", classId-1));
            List<SubPacket> packets = propertyBuilder.Done();
            QueuePackets(packets);
        }

        public void SetRepairRequest(byte type)
        {
            charaWork.eventSave.repairType = type;
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("charaWork/bazaar", this);
            propPacketUtil.AddProperty("charaWork.eventSave.repairType");
            QueuePackets(propPacketUtil.Done());
        }

        public void CheckBazaarFlags(bool noUpdate = false)
        {
            bool isDealing = false, isRepairing = false, seekingItem = false;
            lock (GetItemPackage(ItemPackage.BAZAAR))
            {
                foreach (InventoryItem item in GetItemPackage(ItemPackage.BAZAAR).GetRawList())
                {
                    if (item == null)
                        break;

                    if (item.GetBazaarMode() == InventoryItem.MODE_SELL_SINGLE || item.GetBazaarMode() == InventoryItem.MODE_SELL_PSTACK || item.GetBazaarMode() == InventoryItem.MODE_SELL_FSTACK)
                        isDealing = true;
                    if (item.GetBazaarMode() == InventoryItem.MODE_SEEK_REPAIR)
                        isRepairing = true;
                    if (item.GetBazaarMode() == InventoryItem.MODE_SEEK_ITEM)
                        isDealing = true;

                    if (isDealing && isRepairing && seekingItem)
                        break;
                }
            }

            bool doUpdate = false;

            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("charaWork/bazaar", this);
            if (charaWork.eventTemp.bazaarRetail != isDealing)
            {
                charaWork.eventTemp.bazaarRetail = isDealing;
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarRetail");
                doUpdate = true;
            }

            if (charaWork.eventTemp.bazaarRepair != isRepairing)
            {
                charaWork.eventTemp.bazaarRepair = isRepairing;
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarRepair");
                doUpdate = true;
            }

            if (charaWork.eventTemp.bazaarMateria != (GetItemPackage(ItemPackage.MELDREQUEST).GetCount() != 0))
            {
                charaWork.eventTemp.bazaarMateria = GetItemPackage(ItemPackage.MELDREQUEST).GetCount() != 0;
                propPacketUtil.AddProperty("charaWork.eventTemp.bazaarMateria");
                doUpdate = true;
            }
            
            if (!noUpdate && doUpdate)            
                BroadcastPackets(propPacketUtil.Done(), true);            
        }        

        private const uint GilCatalogId = 1000001;

        public int GetCurrentGil()
        {
            InventoryItem gil = GetItemPackage(ItemPackage.CURRENCY_CRYSTALS).GetItemByCatelogId(GilCatalogId);
            return gil != null ? gil.quantity : 0;
        }

        public int AddGil(int amount)
        {
            if (amount <= 0)
                return ItemPackage.ERROR_SYSTEM;

            return GetItemPackage(ItemPackage.CURRENCY_CRYSTALS).AddItem(GilCatalogId, amount, 1);
        }

        public Actor GetActorInInstance(uint actorId)
        {
            foreach (Actor a in playerSession.actorInstanceList)
            {
                if (a.actorId == actorId)
                    return a;
            }

            return null;
        }

        public void SetZoneChanging(bool flag)
        {
            isZoneChanging = flag;

            if (!flag)
                ClearExpectedZoneChangePosition();
        }

        public bool IsInZoneChange()
        {
            return isZoneChanging;
        }

        public void PrepareZoneChangePositionValidation(uint expectedZone, float expectedX, float expectedY, float expectedZ, float expectedRotation)
        {
            expectedZoneChangeZone = expectedZone;
            expectedZoneChangeX = expectedX;
            expectedZoneChangeY = expectedY;
            expectedZoneChangeZ = expectedZ;
            expectedZoneChangeRotation = expectedRotation;
            rejectedZoneChangePositionCount = 0;
            hasExpectedZoneChangePosition = true;
            SetZoneChanging(true);
        }

        public bool IsZoneChangePositionAcceptable(float x, float y, float z)
        {
            bool accepted = ZoneTransitionPositionPolicy.IsDestinationConsistent(
                hasExpectedZoneChangePosition,
                zoneId,
                expectedZoneChangeZone,
                expectedZoneChangeX,
                expectedZoneChangeY,
                expectedZoneChangeZ,
                x,
                y,
                z);

            if (accepted)
                return true;

            rejectedZoneChangePositionCount++;

            // A 1.x client commonly repeats its old position while unloading.
            // Keep the trace useful without logging every duplicate packet.
            if (rejectedZoneChangePositionCount == 1 || rejectedZoneChangePositionCount % 10 == 0)
            {
                DevDiagnostics.Trace(
                    "zone.change.position.ignoredStale",
                    "player", customDisplayName,
                    "zone", zoneId,
                    "expectedZone", expectedZoneChangeZone,
                    "expectedX", expectedZoneChangeX,
                    "expectedY", expectedZoneChangeY,
                    "expectedZ", expectedZoneChangeZ,
                    "expectedRot", expectedZoneChangeRotation,
                    "receivedX", x,
                    "receivedY", y,
                    "receivedZ", z,
                    "rejectedCount", rejectedZoneChangePositionCount);
            }

            return false;
        }

        public void MarkZoneChangePending(ushort spawnType)
        {
            destinationZone = 0;
            destinationSpawnType = spawnType;
            PrepareZoneChangePositionValidation(zoneId, positionX, positionY, positionZ, rotation);

            DevDiagnostics.Trace(
                "zone.change.pending",
                "player", customDisplayName,
                "zone", zoneId,
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "spawnType", spawnType,
                "x", positionX,
                "y", positionY,
                "z", positionZ,
                "rot", rotation);

            Database.SavePlayerPosition(this);
        }

        public void CompleteZoneChange()
        {
            uint completedDestinationZone = destinationZone;
            ushort completedSpawnType = destinationSpawnType;
            uint completedRejectedPositionCount = rejectedZoneChangePositionCount;

            destinationZone = 0;
            destinationSpawnType = 0;
            SetZoneChanging(false);

            DevDiagnostics.Trace(
                "zone.change.complete",
                "player", customDisplayName,
                "zone", zoneId,
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "destinationZone", completedDestinationZone,
                "spawnType", completedSpawnType,
                "ignoredStalePositions", completedRejectedPositionCount,
                "x", positionX,
                "y", positionY,
                "z", positionZ,
                "rot", rotation);

            Database.SavePlayerPosition(this);

            // ClientZoneInComplete (0x0007) is the primary release signal, but
            // retail traces show that acknowledgement can trail the actor
            // bundle substantially and a resident-geometry reload may not
            // deliver it through every proxy path. An accepted destination
            // position is equally strong evidence that the replacement scene
            // and its director actor are live. Release the one-shot notice
            // here as a fallback so processEventTu_001 can lift the tutorial
            // input mask instead of leaving both the linkpearl and NPC inert.
            ReleaseDeferredContentKickEvent();
        }

        private void ClearExpectedZoneChangePosition()
        {
            hasExpectedZoneChangePosition = false;
            expectedZoneChangeZone = 0;
            expectedZoneChangeX = 0;
            expectedZoneChangeY = 0;
            expectedZoneChangeZ = 0;
            expectedZoneChangeRotation = 0;
            rejectedZoneChangePositionCount = 0;
        }

        public ReferencedItemPackage GetEquipment()
        {
            return equipment;
        }     

        public byte GetInitialTown()
        {
            return playerWork.initialTown;
        }

        public uint GetHomePoint()
        {
            return homepoint;
        }

        public byte GetHomePointInn()
        {
            return homepointInn;
        }

        public void SetHomePoint(uint aetheryteId)
        {            
            homepoint = aetheryteId;
            Database.SavePlayerHomePoints(this);
        }

        public void SetHomePointInn(byte townId)
        {
            homepointInn = townId;
            Database.SavePlayerHomePoints(this);
        }

        public bool HasAetheryteNodeUnlocked(uint aetheryteId)
        {
            if (aetheryteId != 0)
                return true;
            else
                return false;
        }

        public int GetFreeQuestSlot()
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] == null)
                    return i;
            }

            return -1;
        }

        public int GetFreeGuildleveSlot()
        {
            // The client journal reserves work.guildleveId[0..7] for regional
            // leves. Local leves mirror their compact IDs into [8..15].
            for (int i = 0; i < playerWork.questGuildleve.Length; i++)
            {
                if (work.guildleveId[i] == 0)
                    return i;
            }

            return -1;
        }

        public int GetFreeLocalGuildleveSlot()
        {
            for (int i = 0; i < playerWork.questGuildleve.Length; i++)
            {
                if (playerWork.questGuildleve[i] == 0)
                    return i;
            }

            return -1;
        }

        //For Lua calls, cause MoonSharp goes retard with uint
        public void AddQuest(int id, bool isSilent = false)
        {
            AddQuest((uint)id, isSilent);
        }       
        public void CompleteQuest(int id)
        {
            CompleteQuest((uint)id);
        }
        public bool HasQuest(int id)
        {
            return HasQuest((uint)id);
        }
        public Quest GetQuest(int id)
        {
            return GetQuest((uint)id);
        }
        public bool IsQuestCompleted(int id)
        {
            return IsQuestCompleted((uint)id);
        }
        public bool CanAcceptQuest(int id)
        {
            return CanAcceptQuest((uint)id);
        }
        public bool CanAcceptClassQuest(int id)
        {
            return CanAcceptClassQuest((uint)id);
        }
        //For Lua calls, cause MoonSharp goes retard with uint

        public bool AddGuildleve(uint id)
        {
            if (id == 0 || id > ushort.MaxValue)
            {
                TraceGuildleveAcceptance("regional", id, -1, "invalid-id");
                return false;
            }

            if (HasGuildleve(id))
            {
                TraceGuildleveAcceptance("regional", id, -1, "already-present");
                return false;
            }

            int freeSlot = GetFreeGuildleveSlot();

            if (freeSlot == -1)
            {
                TraceGuildleveAcceptance("regional", id, -1, "journal-full");
                return false;
            }

            if (!Database.SaveGuildleve(this, id, freeSlot))
            {
                TraceGuildleveAcceptance("regional", id, freeSlot, "persist-failed");
                return false;
            }

            work.guildleveId[freeSlot] = (ushort)id;
            SendGameMessage(Server.GetWorldManager().GetActor(), 50152, 0x20, (object)id);
            SendGuildleveClientUpdate(freeSlot);
            TraceGuildleveAcceptance("regional", id, freeSlot, "accepted");
            return true;
        }

        public bool AddLocalGuildleve(uint id)
        {
            const uint localGuildleveBase = 120000;
            uint compactId = id - localGuildleveBase;

            if (id <= localGuildleveBase || compactId > ushort.MaxValue)
            {
                TraceGuildleveAcceptance("local", id, -1, "invalid-id");
                return false;
            }

            if (HasLocalGuildleve(id))
            {
                TraceGuildleveAcceptance("local", id, -1, "already-present");
                return false;
            }

            int freeSlot = GetFreeLocalGuildleveSlot();
            if (freeSlot == -1)
            {
                TraceGuildleveAcceptance("local", id, -1, "journal-full");
                return false;
            }

            if (!Database.SaveLocalGuildleve(this, id, freeSlot))
            {
                TraceGuildleveAcceptance("local", id, freeSlot, "persist-failed");
                return false;
            }

            uint actorId = 0xA0F00000 | id;
            int workSlot = freeSlot + playerWork.questGuildleve.Length;
            playerWork.questGuildleve[freeSlot] = actorId;
            questGuildleve[freeSlot] = actorId;
            work.guildleveId[workSlot] = checked((ushort)compactId);

            SendGameMessage(Server.GetWorldManager().GetActor(), 50152, 0x20, (object)id);

            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("playerWork/journal", this);
            propPacketUtil.AddProperty(String.Format("playerWork.questGuildleve[{0}]", freeSlot));
            propPacketUtil.NewTarget("work/guildleve");
            propPacketUtil.AddProperty(String.Format("work.guildleveId[{0}]", workSlot));
            QueuePackets(propPacketUtil.Done());

            TraceGuildleveAcceptance("local", id, freeSlot, "accepted");
            return true;
        }

        public void MarkGuildleve(uint id, bool abandoned, bool completed)
        {
            if (HasGuildleve(id))
            {
                for (int i = 0; i < playerWork.questGuildleve.Length; i++)
                {
                    if (work.guildleveId[i] == id)
                    {
                        work.guildleveChecked[i] = completed;
                        work.guildleveDone[i] = abandoned;
                        Database.MarkGuildleve(this, id, abandoned, completed);
                        SendGuildleveMarkClientUpdate(i);
                    }
                }
            }
        }

        public void RemoveGuildleve(uint id)
        {
            if (HasGuildleve(id))
            {
                for (int i = 0; i < playerWork.questGuildleve.Length; i++)
                {
                    if (work.guildleveId[i] == id)
                    {
                        Database.RemoveGuildleve(this, id);
                        work.guildleveId[i] = 0;
                        SendGuildleveClientUpdate(i);
                        break;
                    }
                }
            }
        }

        public void AddQuest(uint id, bool isSilent = false)
        {
            Actor actor = Server.GetStaticActors((0xA0F00000 | id));
            AddQuest(actor.actorName, isSilent);
        }

        public void AddQuest(string name, bool isSilent = false)
        {
            Actor actor = Server.GetStaticActors(name);

            if (actor == null)
                return;

            uint id = actor.actorId;

            int freeSlot = GetFreeQuestSlot();

            if (freeSlot == -1)
                return;

            playerWork.questScenario[freeSlot] = id;
            questScenario[freeSlot] = new Quest(this, playerWork.questScenario[freeSlot], name, null, 0, 0);
            Database.SaveQuest(this, questScenario[freeSlot]);
            SendQuestClientUpdate(freeSlot);

            if (!isSilent)
            {
                SendGameMessage(Server.GetWorldManager().GetActor(), 25224, 0x20, (object)questScenario[freeSlot].GetQuestId());
                questScenario[freeSlot].NextPhase(0);
            }
        }        

        public void CompleteQuest(uint id)
        {
            Actor actor = Server.GetStaticActors((0xA0F00000 | id));
            CompleteQuest(actor.actorName);
        }

        public void CompleteQuest(string name)
        {
            Actor actor = Server.GetStaticActors(name);

            if (actor == null)
                return;

            uint id = actor.actorId;
            if (HasQuest(id))
            {
                Database.CompleteQuest(playerSession.GetActor(), id);
                SendGameMessage(Server.GetWorldManager().GetActor(), 25086, 0x20, (object)GetQuest(id).GetQuestId());
                RemoveQuest(id);
            }
        }

        //TODO: Add checks for you being in an instance or main scenario
        public void AbandonQuest(uint id)
        {
            Quest quest = GetQuest(id);
            RemoveQuestByQuestId(id);
            quest.DoAbandon();       
        }

        public void RemoveQuestByQuestId(uint id)
        {
            RemoveQuest((0xA0F00000 | id));
        }

        public void RemoveQuest(uint id)
        {
            if (HasQuest(id))
            {
                for (int i = 0; i < questScenario.Length; i++)
                {
                    if (questScenario[i] != null && questScenario[i].actorId == id)
                    {
                        Database.RemoveQuest(this, questScenario[i].actorId);
                        questScenario[i] = null;
                        playerWork.questScenario[i] = 0;
                        SendQuestClientUpdate(i);
                        break;
                    }
                }
            }
        }

        public void ReplaceQuest(uint oldId, uint newId)
        {
            if (HasQuest(oldId))
            {
                for (int i = 0; i < questScenario.Length; i++)
                {
                    if (questScenario[i] != null && questScenario[i].GetQuestId() == oldId)
                    {
                        Actor actor = Server.GetStaticActors((0xA0F00000 | newId));
                        playerWork.questScenario[i] = (0xA0F00000 | newId);
                        questScenario[i] = new Quest(this, playerWork.questScenario[i], actor.actorName, null, 0, 0);
                        Database.SaveQuest(this, questScenario[i]);
                        SendQuestClientUpdate(i);
                        break;
                    }
                }
            }
        }

        public bool CanAcceptQuest(string name)
        {
            if (!IsQuestCompleted(name) && !HasQuest(name))
                return true;
            else
                return false;
        }

        public bool CanAcceptQuest(uint id)
        {
            Actor actor = Server.GetStaticActors((0xA0F00000 | id));
            return actor != null && CanAcceptQuest(actor.actorName);
        }

        public bool CanAcceptClassQuest(uint id)
        {
            return ClassQuestProgressionPolicy.TryGet(id, out ClassQuestRequirement requirement)
                && CanAcceptQuest(id)
                && ClassQuestProgressionPolicy.MeetsRequirements(
                    requirement,
                    GetCurrentClassOrJob(),
                    GetClassLevel,
                    IsQuestCompleted);
        }

        public bool IsQuestCompleted(string questName)
        {
            Actor actor = Server.GetStaticActors(questName);
            return IsQuestCompleted(actor.actorId);
        }

        public bool IsQuestCompleted(uint questId)
        {
            return Database.IsQuestCompleted(this, 0xFFFFF & questId);
        }

        public Quest GetQuest(uint id)
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] != null && questScenario[i].actorId == (0xA0F00000 | id))
                    return questScenario[i];
            }

            return null;
        }

        public Quest GetQuest(string name)
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] != null && questScenario[i].actorName.ToLower().Equals(name.ToLower()))
                    return questScenario[i];
            }

            return null;
        }

        public bool HasQuest(string name)
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] != null && questScenario[i].actorName.ToLower().Equals(name.ToLower()))
                    return true;
            }

            return false;
        }

        public bool HasQuest(uint id)
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] != null && questScenario[i].actorId == (0xA0F00000 | id))
                    return true;
            }

            return false;
        }

        public bool HasGuildleve(uint id)
        {
            for (int i = 0; i < playerWork.questGuildleve.Length; i++)
            {
                if (work.guildleveId[i] == id)
                    return true;
            }

            return false;
        }

        public bool HasLocalGuildleve(uint id)
        {
            uint actorId = 0xA0F00000 | id;
            for (int i = 0; i < playerWork.questGuildleve.Length; i++)
            {
                if (playerWork.questGuildleve[i] == actorId)
                    return true;
            }

            return false;
        }

        private void TraceGuildleveAcceptance(string kind, uint id, int slot, string status)
        {
            if (!DevDiagnostics.Enabled)
                return;

            DevDiagnostics.Trace(
                "guildleve.accept",
                "playerActorId", actorId,
                "player", customDisplayName,
                "kind", kind,
                "guildleveId", id,
                "slot", slot,
                "status", status);
        }

        public int GetQuestSlot(uint id)
        {
            for (int i = 0; i < questScenario.Length; i++)
            {
                if (questScenario[i] != null && questScenario[i].actorId == (0xA0F00000 | id))
                    return i;
            }

            return -1;
        }

        public void SetNpcLS(uint npcLSId, uint state)
        {
            if (npcLSId >= (uint)playerWork.npcLinkshellChatCalling.Length ||
                npcLSId >= (uint)playerWork.npcLinkshellChatExtra.Length)
            {
                Program.Log.Error("Ignoring invalid NPC linkshell id {0} for player {1}.", npcLSId, actorId);
                return;
            }

            if (state > NPCLS_ALERT)
            {
                Program.Log.Error("Ignoring invalid NPC linkshell state {0} for player {1}.", state, actorId);
                return;
            }

            bool wasOwned = playerWork.npcLinkshellChatCalling[npcLSId] ||
                            playerWork.npcLinkshellChatExtra[npcLSId];
            bool isCalling, isExtra;
            isCalling = isExtra = false;

            switch (state)
            {
                case NPCLS_INACTIVE:

                    if (playerWork.npcLinkshellChatExtra[npcLSId] == true && playerWork.npcLinkshellChatCalling[npcLSId] == false)
                    {
                        TraceNpcLinkshellState(npcLSId, state, playerWork.npcLinkshellChatCalling[npcLSId], playerWork.npcLinkshellChatExtra[npcLSId], true);
                        return;
                    }

                    isExtra = true;
                    break;
                case NPCLS_ACTIVE:

                    if (playerWork.npcLinkshellChatExtra[npcLSId] == false && playerWork.npcLinkshellChatCalling[npcLSId] == true)
                    {
                        TraceNpcLinkshellState(npcLSId, state, playerWork.npcLinkshellChatCalling[npcLSId], playerWork.npcLinkshellChatExtra[npcLSId], true);
                        return;
                    }

                    isCalling = true;
                    break;
                case NPCLS_ALERT:

                    if (playerWork.npcLinkshellChatExtra[npcLSId] == true && playerWork.npcLinkshellChatCalling[npcLSId] == true)
                    {
                        TraceNpcLinkshellState(npcLSId, state, playerWork.npcLinkshellChatCalling[npcLSId], playerWork.npcLinkshellChatExtra[npcLSId], true);
                        return;
                    }

                    isExtra = isCalling = true;
                    break;
            }

            playerWork.npcLinkshellChatExtra[npcLSId] = isExtra;
            playerWork.npcLinkshellChatCalling[npcLSId] = isCalling;

            Database.SaveNpcLS(this, npcLSId, isCalling, isExtra);

            TraceNpcLinkshellState(npcLSId, state, isCalling, isExtra, false);

            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("playerWork/npcLinkshellChat", this);
            propPacketUtil.AddProperty(String.Format("playerWork.npcLinkshellChatExtra[{0}]", npcLSId));
            propPacketUtil.AddProperty(String.Format("playerWork.npcLinkshellChatCalling[{0}]", npcLSId));
            QueuePackets(propPacketUtil.Done());

            // The client must learn that this NPC linkshell now belongs to the
            // player before it is asked to open or display a pending message.
            // Quest-facing identifiers are one-based; playerWork is zero-based.
            if (!wasOwned && (isCalling || isExtra))
                SendGameMessage(Server.GetWorldManager().GetActor(), 25118, 0x20, (object)(npcLSId + 1));
        }

        private void TraceNpcLinkshellState(uint npcLSId, uint state, bool isCalling, bool isExtra, bool unchanged)
        {
            if (!DevDiagnostics.Enabled)
                return;

            DevDiagnostics.Trace(
                "npcLinkshell.state",
                "player", customDisplayName,
                "npcLsId", npcLSId,
                "state", state,
                "isCalling", isCalling,
                "isExtra", isExtra,
                "unchanged", unchanged);
        }

        private void SendQuestClientUpdate(int slot)
        {
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("playerWork/journal", this);
            propPacketUtil.AddProperty(String.Format("playerWork.questScenario[{0}]", slot));
            QueuePackets(propPacketUtil.Done());
        }

        private void SendGuildleveClientUpdate(int slot)
        {
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("work/guildleve", this);
            propPacketUtil.AddProperty(String.Format("work.guildleveId[{0}]", slot));
            QueuePackets(propPacketUtil.Done());
        }

        private void SendGuildleveMarkClientUpdate(int slot)
        {
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("work/guildleve", this);
            propPacketUtil.AddProperty(String.Format("work.guildleveDone[{0}]", slot));
            propPacketUtil.AddProperty(String.Format("work.guildleveChecked[{0}]", slot));
            QueuePackets(propPacketUtil.Done());
        }

        public void SendStartCastbar(uint commandId, uint endTime)
        {
            playerWork.castCommandClient = commandId;
            playerWork.castEndClient = endTime;
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("playerWork/castState", this);
            propPacketUtil.AddProperty("playerWork.castEndClient");
            propPacketUtil.AddProperty("playerWork.castCommandClient");
            QueuePackets(propPacketUtil.Done());
        }

        public void SendEndCastbar()
        {
            playerWork.castCommandClient = 0;
            playerWork.castEndClient = 0;
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("playerWork/castState", this);
            propPacketUtil.AddProperty("playerWork.castCommandClient");
            QueuePackets(propPacketUtil.Done());
        }

        public void SetLoginDirector(Director director)
        {
            if (ownedDirectors.Contains(director))
                loginInitDirector = director;
        }

        public void AddDirector(Director director, bool spawnImmediatly = false)
        {            
            if (!ownedDirectors.Contains(director))
            {
                ownedDirectors.Add(director);
                director.AddMember(this);                
            }
        }

        public void SendDirectorPackets(Director director)
        {
            QueuePackets(director.GetSpawnPackets());
            QueuePackets(director.GetInitPackets());
            QueuePackets(director.GetSetEventStatusPackets());
        }

        public void RemoveDirector(Director director)
        {
            if (ownedDirectors.Contains(director))
            {
                QueuePacket(RemoveActorPacket.BuildPacket(director.actorId));
                ownedDirectors.Remove(director);
                if (loginInitDirector == director)
                    loginInitDirector = null;
                director.RemoveMember(this);
            }
        }
        
        public GuildleveDirector GetGuildleveDirector()
        {
            foreach (Director d in ownedDirectors)
            {
                if (d is GuildleveDirector)
                    return (GuildleveDirector)d;
            }

            return null;
        }

        public Director GetDirector(string directorName)
        {
            foreach (Director d in ownedDirectors)
            {
                if (d.GetScriptPath().Equals(directorName))                
                    return d;                
            }

            // The unchanged player.lua asks for OpeningDirector during every opening-zone
            // login. If native login recovery has already reconstructed the successor
            // SimpleContent director, that lookup must be treated as satisfied; otherwise
            // Lua starts and kicks a second public director over the active battle.
            if (directorName.Equals("OpeningDirector", StringComparison.Ordinal)
                && zone is AetherXIV.Core.Map.actors.area.PrivateAreaContent contentArea
                && privateArea != null
                && privateArea.StartsWith("SimpleContent", StringComparison.Ordinal))
            {
                Director contentDirector = contentArea.GetContentDirector();
                DevDiagnostics.Trace(
                    "director.openingContentAlias",
                    "player", customDisplayName,
                    "zone", zoneId,
                    "privateArea", privateArea,
                    "requestedDirector", directorName,
                    "resolvedDirector", contentDirector == null ? "" : contentDirector.GetScriptPath());
                return contentDirector;
            }

            return null;
        }

        public Director GetDirector(uint id)
        {
            foreach (Director d in ownedDirectors)
            {
                if (d.actorId == id)
                    return d;
            }

            return null;
        }

        public void ExaminePlayer(Actor examinee)
        {
            Player toBeExamined;
            if (examinee is Player)
                toBeExamined = (Player)examinee;
            else
                return;

            QueuePacket(InventoryBeginChangePacket.BuildPacket(toBeExamined.actorId, true));
            toBeExamined.GetEquipment().SendUpdateAsItemPackage(this, ItemPackage.MAXSIZE_EQUIPMENT_OTHERPLAYER, ItemPackage.EQUIPMENT_OTHERPLAYER);
            QueuePacket(InventoryEndChangePacket.BuildPacket(toBeExamined.actorId));
        }        

        public void SendDataPacket(params object[] parameters)
        {
            List<LuaParam> lParams = LuaUtils.CreateLuaParamList(parameters);
            DevDiagnostics.Trace(
                "event.data",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "paramCount", lParams.Count,
                "params", LuaUtils.DumpParams(lParams));
            SubPacket spacket = GenericDataPacket.BuildPacket(actorId, lParams);
            spacket.DebugPrintSubPacket();
            QueuePacket(spacket);
        }

        public void StartEvent(Actor owner, EventStartPacket start)
        {
            bool isRideCommand = owner != null && owner.GetName() == "ChocoboRideCommand";
            uint ownerActorClassId = owner is Npc ? ((Npc)owner).GetActorClassId() : 0;
            bool isChocoboStop = owner != null && ChocoboStopPolicy.CanStartWhileMounted(
                ownerActorClassId,
                owner.GetClassName(),
                start.eventName);
            if (GetMountState() != 0 && !isRideCommand && !isChocoboStop)
            {
                DevDiagnostics.Trace(
                    "event.start.blocked",
                    "reason", "mounted",
                    "player", customDisplayName,
                    "owner", String.Format("0x{0:X}", start.ownerActorID),
                    "eventName", start.eventName);
                QueuePacket(EndEventPacket.BuildPacket(actorId, start.ownerActorID, start.eventName, start.eventType));
                SendGameMessage(Server.GetWorldManager().GetActor(), 32553, 0x20);
                return;
            }

            currentEventOwner = start.ownerActorID;
            currentEventName = start.eventName;
            currentEventType = start.eventType;
            DevDiagnostics.Trace(
                "event.start",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", start.ownerActorID),
                "ownerName", owner == null ? "(none)" : owner.GetName(),
                "trigger", String.Format("0x{0:X}", start.triggerActorID),
                "eventName", start.eventName,
                "eventType", start.eventType,
                "params", LuaUtils.DumpParams(start.luaParams));

            Npc questNpc = owner as Npc;
            if (questNpc != null)
            {
                foreach (Quest quest in questScenario)
                {
                    if (quest != null && quest.TryHandleNpcEvent(this, questNpc, start))
                        return;
                }
            }

            LuaEngine.GetInstance().EventStarted(this, owner, start);
        }

        public void RefreshQuestENpcs()
        {
            foreach (Quest quest in questScenario)
            {
                if (quest != null)
                    quest.UpdateENPCs(true);
            }

            DisableRetiredGridaniaOpeningTrigger();
        }

        private void DisableRetiredGridaniaOpeningTrigger()
        {
            // The unnamed public copy is Man0g0's adventurers' guild push
            // trigger. Once that quest has been replaced by Man0g1 it must
            // remain present for the map layout but its push event is off.
            if (zone == null || HasQuest(110005))
                return;

            foreach (Npc npc in zone.GetAllActors<Npc>())
            {
                if (npc.GetActorClassId() != 1099046 || !String.IsNullOrEmpty(npc.GetUniqueId()))
                    continue;

                if (npc.eventConditions != null && npc.eventConditions.pushWithCircleEventConditions != null)
                {
                    foreach (var condition in npc.eventConditions.pushWithCircleEventConditions)
                        SetEventStatus(npc, condition.conditionName, false, 2);
                }

                npc.SetQuestGraphic(this, 0);
            }
        }

        public void StartNpcLinkshellEvent(EventStartPacket start)
        {
            currentEventOwner = start.ownerActorID;
            currentEventName = start.eventName;
            currentEventType = start.eventType;

            uint? npcLsHint = null;
            if (start.luaParams != null)
            {
                foreach (LuaParam parameter in start.luaParams)
                {
                    if (parameter.value is byte byteValue)
                        npcLsHint = byteValue;
                    else if (parameter.value is ushort ushortValue)
                        npcLsHint = ushortValue;
                    else if (parameter.value is short shortValue && shortValue >= 0)
                        npcLsHint = (uint)shortValue;
                    else if (parameter.value is int intValue && intValue >= 0)
                        npcLsHint = (uint)intValue;
                    else if (parameter.value is uint uintValue)
                        npcLsHint = uintValue;

                    if (npcLsHint.HasValue)
                        break;
                }
            }

            Quest pendingQuest = null;
            foreach (Quest quest in questScenario)
            {
                if (quest == null || quest.GetNpcLsFrom() == 0)
                    continue;

                uint from = quest.GetNpcLsFrom();
                if (!npcLsHint.HasValue || from == npcLsHint.Value || from == npcLsHint.Value + 1)
                {
                    pendingQuest = quest;
                    break;
                }

                if (pendingQuest == null)
                    pendingQuest = quest;
            }

            DevDiagnostics.Trace(
                "npcLinkshell.event",
                "player", customDisplayName,
                "owner", String.Format("0x{0:X}", start.ownerActorID),
                "eventName", start.eventName,
                "hint", npcLsHint.HasValue ? npcLsHint.Value.ToString() : "",
                "quest", pendingQuest == null ? "" : pendingQuest.GetName(),
                "from", pendingQuest == null ? 0 : pendingQuest.GetNpcLsFrom(),
                "messageStep", pendingQuest == null ? 0 : pendingQuest.GetNpcLsMessageStep());

            if (pendingQuest == null)
            {
                EndEvent();
                return;
            }

            pendingQuest.OnNpcLs(this, pendingQuest.GetNpcLsFrom(), pendingQuest.GetNpcLsMessageStep());
        }

        public void UpdateEvent(EventUpdatePacket update)
        {
            DevDiagnostics.Trace(
                "event.update",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", currentEventOwner),
                "eventName", currentEventName,
                "eventType", currentEventType,
                "params", LuaUtils.DumpParams(update.luaParams));
            LuaEngine.GetInstance().OnEventUpdate(this, update.luaParams);
        }

        public void KickEvent(Actor actor, string eventName, params object[] parameters)
        {
            if (actor == null)
                return;

            List<LuaParam> lParams = LuaUtils.CreateLuaParamList(parameters);
            DevDiagnostics.Trace(
                "event.kick",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", actor.actorId),
                "ownerName", actor.GetName(),
                "eventName", eventName,
                "eventType", 5,
                "params", LuaUtils.DumpParams(lParams));
            SubPacket spacket = KickEventPacket.BuildPacket(actorId, actor.actorId, eventName, 5, lParams);
            spacket.DebugPrintSubPacket();
            QueuePacket(spacket);
        }

        /// <summary>
        /// Parks a content-director event until the client acknowledges the
        /// same-zone actor reload. A KickEvent sent before that acknowledgement
        /// is discarded because DeleteAllActors has invalidated its owner.
        /// </summary>
        public void DeferContentKickEvent(Actor actor, string eventName, params object[] parameters)
        {
            if (actor == null || String.IsNullOrEmpty(eventName))
                return;

            deferredContentKickOwner = actor;
            deferredContentKickEventName = eventName;
            deferredContentKickParameters = parameters == null ? new object[0] : (object[])parameters.Clone();
            DevDiagnostics.Trace(
                "event.kick.content.deferred",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", actor.actorId),
                "ownerName", actor.GetName(),
                "eventName", eventName);
        }

        public void ReleaseDeferredContentKickEvent()
        {
            Actor owner = deferredContentKickOwner;
            string eventName = deferredContentKickEventName;
            object[] parameters = deferredContentKickParameters;

            deferredContentKickOwner = null;
            deferredContentKickEventName = null;
            deferredContentKickParameters = null;

            if (owner == null || String.IsNullOrEmpty(eventName))
                return;

            DevDiagnostics.Trace(
                "event.kick.content.release",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", owner.actorId),
                "ownerName", owner.GetName(),
                "eventName", eventName);
            KickEvent(owner, eventName, parameters ?? new object[0]);
        }

        public void ClearDeferredContentKickEvent()
        {
            deferredContentKickOwner = null;
            deferredContentKickEventName = null;
            deferredContentKickParameters = null;
        }

        public void KickEventSpecial(Actor actor, uint unknown, string eventName, params object[] parameters)
        {
            if (actor == null)
                return;

            List<LuaParam> lParams = LuaUtils.CreateLuaParamList(parameters);
            DevDiagnostics.Trace(
                "event.kickSpecial",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", actor.actorId),
                "ownerName", actor.GetName(),
                "eventName", eventName,
                "eventType", 0,
                "unknown", String.Format("0x{0:X}", unknown),
                "params", LuaUtils.DumpParams(lParams));
            SubPacket spacket = KickEventPacket.BuildPacket(actorId, actor.actorId, eventName, 0, lParams);
            spacket.DebugPrintSubPacket();
            QueuePacket(spacket);
        }

        public void SetEventStatus(Actor actor, string conditionName, bool enabled, byte type)
        {
            if (actor == null)
                return;

            DevDiagnostics.Trace(
                "event.status",
                "player", customDisplayName,
                "playerActor", String.Format("0x{0:X}", actorId),
                "actor", String.Format("0x{0:X}", actor.actorId),
                "actorName", actor.GetName(),
                "condition", conditionName ?? "",
                "enabled", enabled,
                "type", type,
                "result", "queued");

            QueuePacket(SetEventStatusPacket.BuildPacket(actor.actorId, enabled, type, conditionName));
        }       

        public void RunEventFunction(string functionName, params object[] parameters)
        {
            List<LuaParam> lParams = LuaUtils.CreateLuaParamList(parameters);
            DevDiagnostics.Trace(
                "event.runFunction",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", currentEventOwner),
                "eventName", currentEventName,
                "eventType", currentEventType,
                "function", functionName,
                "params", LuaUtils.DumpParams(lParams));
            SubPacket spacket = RunEventFunctionPacket.BuildPacket(actorId, currentEventOwner, currentEventName, currentEventType, functionName, lParams);
            spacket.DebugPrintSubPacket();
            QueuePacket(spacket);
        }

        public void EndEvent()
        {
            DevDiagnostics.Trace(
                "event.end",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "owner", String.Format("0x{0:X}", currentEventOwner),
                "eventName", currentEventName,
                "eventType", currentEventType);
            SubPacket p = EndEventPacket.BuildPacket(actorId, currentEventOwner, currentEventName, currentEventType);
            p.DebugPrintSubPacket();
            QueuePacket(p);

            currentEventOwner = 0;
            currentEventName = "";
            currentEventType = 0;
            currentEventRunning = null;
        }

        public void BroadcastCountdown(byte countdownLength, ulong syncTime)
        {
            BroadcastPacket(StartCountdownPacket.BuildPacket(actorId, countdownLength, syncTime, "Go!"), true);
        }
        
        public void SendInstanceUpdate(bool force = false)
        {
            Server.GetWorldManager().SeamlessCheck(this);

            //Update Instance
            List<Actor> aroundMe = new List<Actor>();

            if (zone != null)
                aroundMe.AddRange(zone.GetActorsAroundActor(this, 50));
            if (zone2 != null)
                aroundMe.AddRange(zone2.GetActorsAroundActor(this, 50));

            DevDiagnostics.Trace(
                "session.instance.update",
                "player", customDisplayName,
                "force", force,
                "updatesLocked", playerSession.isUpdatesLocked,
                "zone", zoneId,
                "zoneActor", zone == null ? "0x0" : String.Format("0x{0:X}", zone.actorId),
                "areaKind", zone == null ? "" : zone.GetType().Name,
                "privateArea", privateArea ?? "",
                "privateAreaType", privateAreaType,
                "zoneActorCount", zone == null ? 0 : zone.GetActorCount(),
                "nearbyActorCount", aroundMe.Count,
                "instanceActorCountBefore", playerSession.actorInstanceList.Count);
            playerSession.UpdateInstance(aroundMe, force);
            DevDiagnostics.Trace(
                "session.instance.update.done",
                "player", customDisplayName,
                "force", force,
                "instanceActorCountAfter", playerSession.actorInstanceList.Count);
        }

        public string GetPrivateAreaName()
        {
            return privateArea ?? "";
        }

        public bool IsInParty()
        {
            return currentParty != null;
        }

        public bool IsPartyLeader()
        {
            if (IsInParty())
            {
                Party party = (Party)currentParty;
                return party.GetLeader() == actorId;
            }
            else
                return false;
        }

        public void PartyOustPlayer(uint actorId)
        {
            SubPacket oustPacket = PartyModifyPacket.BuildPacket(playerSession, 1, actorId);
            QueuePacket(oustPacket);
        }

        public void PartyOustPlayer(string name)
        {
            SubPacket oustPacket = PartyModifyPacket.BuildPacket(playerSession, 1, name);
            QueuePacket(oustPacket);
        }

        public void PartyLeave()
        {
            SubPacket leavePacket = PartyLeavePacket.BuildPacket(playerSession, false);
            QueuePacket(leavePacket);
        }

        public void PartyDisband()
        {
            SubPacket disbandPacket = PartyLeavePacket.BuildPacket(playerSession, true);
            QueuePacket(disbandPacket);
        }

        public void PartyPromote(uint actorId)
        {
            SubPacket promotePacket = PartyModifyPacket.BuildPacket(playerSession, 0, actorId);
            QueuePacket(promotePacket);
        }

        public void PartyPromote(string name)
        {
            SubPacket promotePacket = PartyModifyPacket.BuildPacket(playerSession, 0, name);
            QueuePacket(promotePacket);
        }

        //A party member list packet came, set the party
        public void SetParty(Party group)
        {
            if (group is Party && currentParty != group)
            {
                RemoveFromCurrentPartyAndCleanup();
                currentParty = group;
            }
        }

        //Removes the player from the party and cleans it up if needed
        public void RemoveFromCurrentPartyAndCleanup()
        {
            if (currentParty == null)
                return;

            Party partyGroup = (Party) currentParty;
            partyGroup.SendDeletePacket(playerSession);

            DevDiagnostics.Trace(
                "party.player.leave",
                "player", customDisplayName,
                "actor", String.Format("0x{0:X}", actorId),
                "group", partyGroup.groupIndex,
                "membersBefore", partyGroup.members.Count);

            partyGroup.RemoveMember(actorId);

            bool hasSessionMember = false;
            for (int i = 0; i < partyGroup.members.Count; i++)
            {
                if (Server.GetServer().GetSession(partyGroup.members[i]) != null)
                {
                    hasSessionMember = true;
                    break;
                }
            }

            if (!hasSessionMember)
                Server.GetWorldManager().NoMembersInParty(partyGroup);

            currentParty = null;
        }
        
        public void ChangeChocoboAppearance(byte appearanceId)
        {
            Database.ChangePlayerChocoboAppearance(this, appearanceId);
            chocoboAppearance = appearanceId;
        }
        
        public bool IsChocoboRentalActive()
        {
            return rentalExpireTime != 0;
        }

        public int GetNpcRepairQuoteResult(LuaUtils.ItemRefParam reference)
        {
            return (int)RepairService.QuoteItem(this, reference).result;
        }

        public int GetNpcRepairCandidateCount()
        {
            return RepairService.GetCandidateCount(this);
        }

        public LuaUtils.ItemRefParam GetNpcRepairCandidate(int candidateIndex)
        {
            return RepairService.GetCandidate(this, candidateIndex);
        }

        public int GetNpcRepairFee(LuaUtils.ItemRefParam reference)
        {
            return RepairService.QuoteItem(this, reference).fee;
        }

        public uint GetNpcRepairItemId(LuaUtils.ItemRefParam reference)
        {
            return RepairService.QuoteItem(this, reference).itemId;
        }

        public int TryNpcRepair(LuaUtils.ItemRefParam reference, uint expectedItemId, int expectedFee)
        {
            return (int)RepairService.TryRepairItem(this, reference, expectedItemId, expectedFee);
        }

        public bool CanRentChocobo()
        {
            return ChocoboPolicy.IsRentalLevelEligible(GetHighestLevel()) && GetCurrentGil() >= ChocoboPolicy.RentalPrice;
        }

        public int GetCurrentGrandCompanyRank()
        {
            return ChocoboPolicy.GetRank(this, gcCurrent);
        }

        public int TryPurchaseChocoboIssuance(uint companyShopActorClassId, uint itemId, int price)
        {
            return (int)GrandCompanyShopService.TryPurchaseChocoboIssuance(
                this,
                companyShopActorClassId,
                itemId,
                price);
        }

        public bool CanPresentChocoboIssuance(uint stablemasterActorClassId)
        {
            if (!ChocoboPolicy.TryGetStablemaster(stablemasterActorClassId, out StablemasterPolicy stablemaster))
                return false;
            return !hasChocobo
                && gcCurrent == stablemaster.grandCompany
                && ChocoboPolicy.IsPrivateThirdClassOrHigher(
                    ChocoboPolicy.GetRank(this, stablemaster.grandCompany))
                && HasItem(stablemaster.issuanceItemId);
        }

        public int TryIssuePersonalChocobo(uint stablemasterActorClassId, string name)
        {
            return (int)ChocoboService.TryIssuePersonal(this, stablemasterActorClassId, name);
        }

        public int TryStartChocoboRental(uint stablemasterActorClassId)
        {
            return (int)ChocoboService.TryStartRental(this, stablemasterActorClassId);
        }

        public int TryStartStablemasterChocoboRide(uint stablemasterActorClassId)
        {
            if (!ChocoboPolicy.TryGetStablemaster(stablemasterActorClassId, out StablemasterPolicy unused))
                return (int)ChocoboResult.InvalidStablemaster;
            return (int)ChocoboService.TryMountPersonal(this, true);
        }

        public int TryMountPersonalChocobo()
        {
            return (int)ChocoboService.TryMountPersonal(this, false);
        }

        public void EndChocoboRide()
        {
            ChocoboService.EndRide(this);
        }

        public Retainer SpawnMyRetainer(Npc bell, int retainerIndex)
        {
            Retainer retainer = Database.LoadRetainer(this, retainerIndex);

            float distance = (float)Math.Sqrt(((positionX - bell.positionX) * (positionX - bell.positionX)) + ((positionZ - bell.positionZ) * (positionZ - bell.positionZ)));
            float posX = bell.positionX - ((-1.0f * (bell.positionX - positionX)) / distance);
            float posZ = bell.positionZ - ((-1.0f * (bell.positionZ - positionZ)) / distance);

            retainer.positionX = posX;
            retainer.positionY = positionY;
            retainer.positionZ = posZ;
            retainer.rotation = (float)Math.Atan2(positionX - posX, positionZ - posZ);

            retainerMeetingGroup = new RetainerMeetingRelationGroup(5555, this, retainer);
            retainerMeetingGroup.SendGroupPackets(playerSession);

            currentSpawnedRetainer = retainer;
            sentRetainerSpawn = false;

            return retainer;
        }

        public void DespawnMyRetainer()
        {
            if (currentSpawnedRetainer != null)
            {
                currentSpawnedRetainer = null;
                retainerMeetingGroup.SendDeletePacket(playerSession);
                retainerMeetingGroup = null;
            }
        }
        
        public override void Update(DateTime tick)
        {
            
            // Chocobo Rental Expirey
            if (rentalExpireTime != 0)
            {
                uint tickUTC = Utils.UnixTimeStampUTC(tick);

                //Rental has expired, dismount
                if (rentalExpireTime <= tickUTC)
                {
                    ChocoboService.EndRide(this);
                }
                else
                {
                    rentalMinLeft = (byte) ((rentalExpireTime - tickUTC) /60);
                }
            }

            aiContainer.Update(tick);
            statusEffects.Update(tick);            
        }

        public override void PostUpdate(DateTime tick, List<SubPacket> packets = null)
        {
            // todo: is this correct?
            if (this.playerSession == null)
            {
                DevDiagnostics.Trace(
                    "player.postUpdate.missingSession",
                    "player", customDisplayName,
                    "actor", String.Format("0x{0:X}", actorId),
                    "updateFlags", updateFlags.ToString());
                return;
            }

            if (this.playerSession.isUpdatesLocked)
                return;           

            // todo: should probably add another flag for battleTemp since all this uses reflection
            packets = new List<SubPacket>();

            // we only want the latest update for the player
            if ((updateFlags & ActorUpdateFlags.Position) != 0)
            {
                if (positionUpdates != null && positionUpdates.Count > 1)
                {
                    var latestPosition = positionUpdates[positionUpdates.Count - 1];
                    positionUpdates.Clear();
                    positionUpdates.Add(latestPosition);
                }
            }

            if ((updateFlags & ActorUpdateFlags.HpTpMp) != 0)
            {
                var propPacketUtil = new ActorPropertyPacketUtil("charaWork/stateAtQuicklyForAll", this);

                // todo: should this be using job as index?
                propPacketUtil.AddProperty("charaWork.parameterSave.hp[0]");
                propPacketUtil.AddProperty("charaWork.parameterSave.hpMax[0]");
                propPacketUtil.AddProperty("charaWork.parameterSave.state_mainSkill[0]");
                propPacketUtil.AddProperty("charaWork.parameterSave.state_mainSkillLevel");

                packets.AddRange(propPacketUtil.Done());
            }


            if ((updateFlags & ActorUpdateFlags.Stats) != 0)
            {
                var propPacketUtil = new ActorPropertyPacketUtil("charaWork/battleParameter", this);

                for (uint i = 0; i < 35; i++)
                {
                    if (GetMod(i) != charaWork.battleTemp.generalParameter[i])
                    {
                        charaWork.battleTemp.generalParameter[i] = (short)GetMod(i);
                        propPacketUtil.AddProperty(String.Format("charaWork.battleTemp.generalParameter[{0}]", i));
                    }
                }

                QueuePackets(propPacketUtil.Done());
            }

            if ((updateFlags & ActorUpdateFlags.Hotbar) != 0)
            {
                UpdateHotbar(hotbarSlotsToUpdate);
                hotbarSlotsToUpdate.Clear();

                updateFlags ^= ActorUpdateFlags.Hotbar;
            }
            
            base.PostUpdate(tick, packets);
        }

        public override void Die(DateTime tick, CommandResultContainer actionContainer = null)
        {
            // todo: death timer
            aiContainer.InternalDie(tick, 60);
        }

        //Update commands and recast timers for the entire hotbar
        public void UpdateHotbar()
        {
            for (ushort i = charaWork.commandBorder; i < charaWork.commandBorder + 30; i++)
            {
                hotbarSlotsToUpdate.Add(i);
            }
            updateFlags |= ActorUpdateFlags.Hotbar;
        }

        //Updates the hotbar and recast timers for only certain hotbar slots
        public void UpdateHotbar(List<ushort> slotsToUpdate)
        {
            UpdateHotbarCommands(slotsToUpdate);
            UpdateRecastTimers(slotsToUpdate);
        }

        //Update command ids for the passed in hotbar slots
        public void UpdateHotbarCommands(List<ushort> slotsToUpdate)
        {
            ActorPropertyPacketUtil propPacketUtil = new ActorPropertyPacketUtil("charaWork/command", this);
            foreach (ushort slot in slotsToUpdate)
            {
                propPacketUtil.AddProperty(String.Format("charaWork.command[{0}]", slot));
                propPacketUtil.AddProperty(String.Format("charaWork.commandCategory[{0}]", slot));
            }

            propPacketUtil.NewTarget("charaWork/commandDetailForSelf");
            //Enable or disable slots based on whether there is an ability in that slot
            foreach (ushort slot in slotsToUpdate)
            {
                charaWork.parameterSave.commandSlot_compatibility[slot - charaWork.commandBorder] = charaWork.command[slot] != 0;
                propPacketUtil.AddProperty(String.Format("charaWork.parameterSave.commandSlot_compatibility[{0}]", slot - charaWork.commandBorder));
            }

            QueuePackets(propPacketUtil.Done());
            //QueuePackets(compatibiltyUtil.Done());
        }

        //Update recast timers for the passed in hotbar slots
        public void UpdateRecastTimers(List<ushort> slotsToUpdate)
        {
            ActorPropertyPacketUtil recastPacketUtil = new ActorPropertyPacketUtil("charaWork/commandDetailForSelf", this);

            foreach (ushort slot in slotsToUpdate)
            {
                recastPacketUtil.AddProperty(String.Format("charaWork.parameterTemp.maxCommandRecastTime[{0}]", slot - charaWork.commandBorder));
                recastPacketUtil.AddProperty(String.Format("charaWork.parameterSave.commandSlot_recastTime[{0}]", slot - charaWork.commandBorder));
            }

            QueuePackets(recastPacketUtil.Done());
        }

        //Find the first open slot in classId's hotbar and equip an ability there.
        public void EquipAbilityInFirstOpenSlot(byte classId, uint commandId, bool printMessage = true)
        {
            //Find first open slot on class's hotbar slot, then call EquipAbility with that slot.
            ushort hotbarSlot = 0;

            //If the class we're equipping for is the current class, we can just look at charawork.command
            if(classId == charaWork.parameterSave.state_mainSkill[0])
                hotbarSlot = FindFirstCommandSlotById(0);
            //Otherwise, we need to check the database.
            else
                hotbarSlot = (ushort) (Database.FindFirstCommandSlot(this, classId) + charaWork.commandBorder);

            EquipAbility(classId, commandId, hotbarSlot, printMessage);
        }

        //Add commandId to classId's hotbar at hotbarSlot.
        //If classId is not the current class, do it in the database
        //hotbarSlot starts at 32
        public void EquipAbility(byte classId, uint commandId, ushort hotbarSlot, bool printMessage = true)
        {
            var ability = Server.GetWorldManager().GetBattleCommand(commandId);
            uint trueCommandId = 0xA0F00000 | commandId;
            ushort lowHotbarSlot = (ushort)(hotbarSlot - charaWork.commandBorder);
            ushort maxRecastTime = (ushort)(ability != null ? ability.maxRecastTimeSeconds : 5);
            uint recastEnd = Utils.UnixTimeStampUTC() + maxRecastTime;
            
            Database.EquipAbility(this, classId, (ushort) (hotbarSlot - charaWork.commandBorder), commandId, recastEnd);
            //If the class we're equipping for is the current class (need to find out if state_mainSkill is supposed to change when you're a job)
            //then equip the ability in charawork.commands and save in databse, otherwise just save in database
            if (classId == GetCurrentClassOrJob())
            {
                charaWork.command[hotbarSlot] = trueCommandId;
                charaWork.commandCategory[hotbarSlot] = 1;
                charaWork.parameterTemp.maxCommandRecastTime[lowHotbarSlot] = maxRecastTime;
                charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot] = recastEnd;

                hotbarSlotsToUpdate.Add(hotbarSlot);
                updateFlags |= ActorUpdateFlags.Hotbar;
            }


            if(printMessage)
                SendGameMessage(Server.GetWorldManager().GetActor(), 30603, 0x20, 0, commandId);
        }

        //Doesn't take a classId because the only way to swap abilities is through the ability equip widget oe /eaction, which only apply to current class
        //hotbarSlot 1 and 2 are 32-indexed.
        public void SwapAbilities(ushort hotbarSlot1, ushort hotbarSlot2)
        {
            //0 indexed hotbar slots for saving to database and recast timers
            uint lowHotbarSlot1 = (ushort)(hotbarSlot1 - charaWork.commandBorder);
            uint lowHotbarSlot2 = (ushort)(hotbarSlot2 - charaWork.commandBorder);
            
            //Store information about first command
            uint commandId = charaWork.command[hotbarSlot1];
            uint recastEnd = charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot1];
            ushort recastMax = charaWork.parameterTemp.maxCommandRecastTime[lowHotbarSlot1];

            //Move second command's info to first hotbar slot
            charaWork.command[hotbarSlot1] = charaWork.command[hotbarSlot2];
            charaWork.parameterTemp.maxCommandRecastTime[lowHotbarSlot1] = charaWork.parameterTemp.maxCommandRecastTime[lowHotbarSlot2];
            charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot1] = charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot2];

            //Move first command's info to second slot
            charaWork.command[hotbarSlot2] = commandId;
            charaWork.parameterTemp.maxCommandRecastTime[lowHotbarSlot2] = recastMax;
            charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot2] = recastEnd;

            //Save changes to both slots
            Database.EquipAbility(this, GetCurrentClassOrJob(), (ushort)(lowHotbarSlot1), 0xA0F00000 ^ charaWork.command[hotbarSlot1], charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot1]);
            Database.EquipAbility(this, GetCurrentClassOrJob(), (ushort)(lowHotbarSlot2), 0xA0F00000 ^ charaWork.command[hotbarSlot2], charaWork.parameterSave.commandSlot_recastTime[lowHotbarSlot2]);

            //Update slots on client
            hotbarSlotsToUpdate.Add(hotbarSlot1);
            hotbarSlotsToUpdate.Add(hotbarSlot2);
            updateFlags |= ActorUpdateFlags.Hotbar;
        }

        public void UnequipAbility(ushort hotbarSlot, bool printMessage = true)
        {
            ushort trueHotbarSlot = (ushort)(hotbarSlot + charaWork.commandBorder - 1);
            uint commandId = charaWork.command[trueHotbarSlot];
            Database.UnequipAbility(this,  hotbarSlot);
            charaWork.command[trueHotbarSlot] = 0;
            hotbarSlotsToUpdate.Add(trueHotbarSlot);

            if (printMessage && commandId != 0)
                SendGameMessage(Server.GetWorldManager().GetActor(), 30604, 0x20, 0, 0xA0F00000 ^ commandId);

            updateFlags |= ActorUpdateFlags.Hotbar;
        }

        //Finds the first hotbar slot with a given commandId.
        //If the returned value is outside the hotbar, it indicates it wasn't found.
        public ushort FindFirstCommandSlotById(uint commandId)
        {
            if(commandId != 0)
                commandId |= 0xA0F00000;

            ushort firstSlot = (ushort)(charaWork.commandBorder + 30);

            for (ushort i = charaWork.commandBorder; i < charaWork.commandBorder + 30; i++)
            {
                if (charaWork.command[i] == commandId)
                {
                    firstSlot = i;
                    break;
                }
            }

            return firstSlot;
        }
        
        private void UpdateHotbarTimer(uint commandId, uint recastTimeMs)
        {
            ushort slot = FindFirstCommandSlotById(commandId);
            charaWork.parameterSave.commandSlot_recastTime[slot - charaWork.commandBorder] = Utils.UnixTimeStampUTC(DateTime.Now.AddMilliseconds(recastTimeMs));
            var slots = new List<ushort>();
            slots.Add(slot);
            UpdateRecastTimers(slots);
        }

        private uint GetHotbarTimer(uint commandId)
        {
            ushort slot = FindFirstCommandSlotById(commandId);
            return charaWork.parameterSave.commandSlot_recastTime[slot - charaWork.commandBorder];
        }

        public override void Cast(uint spellId, uint targetId = 0)
        {
            if (aiContainer.CanChangeState())
                aiContainer.Cast(zone.FindActorInArea<Character>(targetId == 0 ? currentTarget : targetId), spellId);
            else if (aiContainer.IsCurrentState<MagicState>())
                // You are already casting.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32536, 0x20);
            else
                // Please wait a moment and try again.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32535, 0x20);
        }

        public override void Ability(uint abilityId, uint targetId = 0)
        {
            if (aiContainer.CanChangeState())
                aiContainer.Ability(zone.FindActorInArea<Character>(targetId == 0 ? currentTarget : targetId), abilityId);
            else
                // Please wait a moment and try again.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32535, 0x20);
        }

        public override void WeaponSkill(uint skillId, uint targetId = 0)
        {
            if (aiContainer.CanChangeState())
                aiContainer.WeaponSkill(zone.FindActorInArea<Character>(targetId == 0 ? currentTarget : targetId), skillId);
            else
                // Please wait a moment and try again.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32535, 0x20);
        }

        public override bool IsValidTarget(Character target, ValidTarget validTarget)
        {
            if (target == null)
            {
                // Target does not exist.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32511, 0x20);
                return false;
            }

            if (target.isMovingToSpawn)
            {
                // A player may resume the closed Gridania tutorial encounter
                // after an older server process or reconnect left a wolf on
                // the generic world-mob return path. Clear the stale path;
                // the attack/retaliation flow will establish combat normally.
                if (target is BattleNpc tutorialNpc &&
                    GridaniaOpeningTutorialPolicy.IsLiveContentCombat(tutorialNpc, this))
                {
                    tutorialNpc.isMovingToSpawn = false;
                    tutorialNpc.aiContainer.pathFind.Clear();
                    DevDiagnostics.Trace(
                        "battle.target.returnCancelled",
                        "player", String.Format("0x{0:X}", actorId),
                        "target", String.Format("0x{0:X}", tutorialNpc.actorId),
                        "content", GridaniaOpeningTutorialPolicy.ContentAreaName);
                }
                else
                {
                    // That command cannot be performed on the current target.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32547, 0x20);
                    return false;
                }
            }

            // enemy only
            if ((validTarget & ValidTarget.Enemy) != 0)
            {
                if (!target.CanBeAttackedBy(this))
                {
                    // That command cannot be performed on the current target.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32547, 0x20);
                    DevDiagnostics.Trace(
                        "battle.target.blocked",
                        "reason", "target is not attackable",
                        "actor", String.Format("0x{0:X}", actorId),
                        "actorName", customDisplayName != null ? customDisplayName : actorName,
                        "target", String.Format("0x{0:X}", target.actorId),
                        "targetName", target.customDisplayName != null ? target.customDisplayName : target.actorName,
                        "targetType", target.GetType().Name);
                    return false;
                }

                // todo: this seems ambiguous
                if (target.isStatic)
                {
                    // That command cannot be performed on the current target.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32547, 0x20);
                    return false;
                }
                if (currentParty != null && target.currentParty == currentParty)
                {
                    // That command cannot be performed on a party member.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32548, 0x20);
                    return false;
                }
                // todo: pvp?
                if (target.allegiance == allegiance)
                {
                    // That command cannot be performed on an ally.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32549, 0x20);
                    return false;
                }

                bool partyEngaged = false;
                // todo: replace with confrontation status effect? (see how dsp does it)
                if (target.aiContainer.IsEngaged())
                {
                    if (currentParty != null)
                    {
                        if (target is BattleNpc)
                        {
                            var helpingActorId = ((BattleNpc)target).GetMobMod((uint)MobModifier.CallForHelp);
                            partyEngaged = this.actorId == helpingActorId || (((BattleNpc)target).GetMobMod((uint)MobModifier.FreeForAll) != 0);
                        }

                        if (!partyEngaged)
                        {
                            foreach (var memberId in ((Party)currentParty).members)
                            {
                                if (memberId == target.currentLockedTarget)
                                {
                                    partyEngaged = true;
                                    break;
                                }
                            }
                        }
                    }
                    else if (target.currentLockedTarget == actorId)
                    {
                        partyEngaged = true;
                    }
                }
                else
                {
                    partyEngaged = true;
                }

                if (!partyEngaged)
                {
                    // That target is already engaged.
                    SendGameMessage(Server.GetWorldManager().GetActor(), 32520, 0x20);
                    return false;
                }
            }

            if ((validTarget & ValidTarget.Ally) != 0 && target.allegiance != allegiance)
            {
                // That command cannot be performed on the current target.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32547, 0x20);
                return false;
            }

            // todo: isStatic seems ambiguous?
            if ((validTarget & ValidTarget.NPC) != 0 && target.isStatic)
                return true;

            // todo: why is player always zoning?
            // cant target if zoning
            if (target is Player && ((Player)target).playerSession.isUpdatesLocked)
            {
                // That command cannot be performed on the current target.
                SendGameMessage(Server.GetWorldManager().GetActor(), 32547, 0x20);
                return false;
            }

            return true;
        }

        //Do we need separate functions? they check the same things
        public override bool CanUse(Character target, BattleCommand skill, CommandResult error = null)
        {
            if (!skill.IsValidMainTarget(this, target, error) || !IsValidTarget(target, skill.mainTarget))
            {
                // error packet is set in IsValidTarget
                return false;
            }

            //Might want to do these with a BattleAction instead to be consistent with the rest of command stuff
            if (GetHotbarTimer(skill.id) > Utils.UnixTimeStampUTC())
            {
                // todo: this needs confirming
                // Please wait a moment and try again.
                error?.SetTextId(32535);
                return false;
            }

            float xzDistance = Utils.XZDistance(positionX, positionZ, target.positionX, target.positionZ);
            if (xzDistance > skill.range)
            {
                DevDiagnostics.Trace(
                    "battle.command.blocked",
                    "reason", "out_of_range",
                    "player", String.Format("0x{0:X}", actorId),
                    "playerName", customDisplayName != null ? customDisplayName : actorName,
                    "target", String.Format("0x{0:X}", target.actorId),
                    "targetName", target.customDisplayName != null ? target.customDisplayName : target.actorName,
                    "commandId", skill.id,
                    "commandName", skill.name,
                    "distance", xzDistance,
                    "range", skill.range,
                    "playerX", positionX,
                    "playerY", positionY,
                    "playerZ", positionZ,
                    "targetX", target.positionX,
                    "targetY", target.positionY,
                    "targetZ", target.positionZ,
                    "queuedPositions", positionUpdates == null ? 0 : positionUpdates.Count);

                // The target is too far away.
                error?.SetTextId(32539);
                return false;
            }

            if (xzDistance < skill.minRange)
            {
                // The target is too close.
                error?.SetTextId(32538);
                return false;
            }

            if (target.positionY - positionY > (skill.rangeHeight / 2))
            {
                // The target is too far above you.
                error?.SetTextId(32540);
                return false;
            }

            if (positionY - target.positionY > (skill.rangeHeight / 2))
            {
                // The target is too far below you.
                error?.SetTextId(32541);
                return false;
            }

            if (skill.CalculateMpCost(this) > GetMP())
            {
                // You do not have enough MP.
                error?.SetTextId(32545);
                return false;
            }

            if (skill.CalculateTpCost(this) > GetTP())
            {
                // You do not have enough TP.
                error?.SetTextId(32546);
                return false;
            }

            //Proc requirement
            if (skill.procRequirement != BattleCommandProcRequirement.None && !charaWork.battleTemp.timingCommandFlag[(int)skill.procRequirement - 1])
            {
                //Conditions for use are not met
                error?.SetTextId(32556);
                return false;
            }


            return true;
        }

        public override void OnAttack(State state, CommandResult action, ref CommandResult error)
        {
            var target = state.GetTarget();

            base.OnAttack(state, action, ref error);

            // todo: switch based on main weap (also probably move this anim assignment somewhere else)
            action.animation = 0x19001000;
            if (error == null)
            {
                // melee attack animation
                //action.animation = 0x19001000;
            }
            if (target is BattleNpc)
            {
                ((BattleNpc)target).hateContainer.UpdateHate(this, action.enmity);
            }

            EmitContentProgressSignal("playerAttack");
        }

        public override void OnCast(State state, CommandResult[] actions, BattleCommand spell, ref CommandResult[] errors)
        {
            // todo: update hotbar timers to skill's recast time (also needs to be done on class change or equip crap)
            base.OnCast(state, actions, spell, ref errors);
            // todo: should just make a thing that updates the one slot cause this is dumb as hell            
            UpdateHotbarTimer(spell.id, spell.recastTimeMs);
            //LuaEngine.GetInstance().OnSignal("spellUse");
        }

        public override void OnWeaponSkill(State state, CommandResult[] actions, BattleCommand skill, ref CommandResult[] errors)
        {
            // todo: update hotbar timers to skill's recast time (also needs to be done on class change or equip crap)
            base.OnWeaponSkill(state, actions, skill, ref errors);

            // todo: should just make a thing that updates the one slot cause this is dumb as hell
            UpdateHotbarTimer(skill.id, skill.recastTimeMs);
            // todo: this really shouldnt be called on each ws?
            lua.LuaEngine.CallLuaBattleFunction(this, "onWeaponSkill", this, state.GetTarget(), skill);
            LuaEngine.GetInstance().OnSignal("weaponskillUse");
        }

        public override void OnAbility(State state, CommandResult[] actions, BattleCommand ability, ref CommandResult[] errors)
        {
            base.OnAbility(state, actions, ability, ref errors);
            UpdateHotbarTimer(ability.id, ability.recastTimeMs);
            LuaEngine.GetInstance().OnSignal("abilityUse");
            LuaEngine.GetInstance().OnSignal("abilityUsed");
        }

        //Handles exp being added, does not handle figuring out exp bonus from buffs or skill/link chains or any of that
        //Returns CommandResults that can be sent to display the EXP gained number and level ups
        //exp should be a ushort single the exp graphic overflows after ~65k
        public List<CommandResult> AddExp(int exp, byte classId, byte bonusPercent = 0)
        {
            List<CommandResult> actionList = new List<CommandResult>();
            int originalExp = exp;
            short startingLevel = GetLevel();
            int startingClassExp = charaWork.battleSave.skillPoint[classId - 1];

            exp += (int) Math.Ceiling((exp * bonusPercent / 100.0f));
            int awardedExp = exp;

            DevDiagnostics.Trace(
                "player.exp.grant.begin",
                "player", String.Format("0x{0:X}", actorId),
                "playerName", customDisplayName != null ? customDisplayName : actorName,
                "classId", classId,
                "currentClassId", GetClass(),
                "level", startingLevel,
                "oldExp", startingClassExp,
                "baseExp", originalExp,
                "bonusPercent", bonusPercent,
                "finalExp", awardedExp);

            //You earn [exp] (+[bonusPercent]%) experience points.
            //In non-english languages there are unique messages for each language, hence the use of ClassExperienceTextIds
            actionList.Add(new CommandResult(actorId, BattleUtils.ClassExperienceTextIds[classId], 0, (ushort)exp, bonusPercent));

            bool leveled = false;
            int diff = MAXEXP[GetLevel() - 1] - charaWork.battleSave.skillPoint[classId - 1];            
            //While there is enough experience to level up, keep leveling up, unlocking skills and removing experience from exp until we don't have enough to level up
            while (exp >= diff && GetLevel() < charaWork.battleSave.skillLevelCap[classId])
            {
                //Level up
                LevelUp(classId, actionList);
                leveled = true;
                //Reduce exp based on how much exp is needed to level
                exp -= diff;
                diff = MAXEXP[GetLevel() - 1];
            }

            if(leveled)
            {
                //Set exp to current class to 0 so that exp is added correctly
                charaWork.battleSave.skillPoint[classId - 1] = 0;
                //send new level
                ActorPropertyPacketUtil levelPropertyPacket = new ActorPropertyPacketUtil("charaWork/stateForAll", this);
                levelPropertyPacket.AddProperty(String.Format("charaWork.battleSave.skillLevel[{0}]", classId - 1));
                levelPropertyPacket.AddProperty("charaWork.parameterSave.state_mainSkillLevel");
                QueuePackets(levelPropertyPacket.Done());

                Database.SetLevel(this, classId, GetLevel());
                Database.SavePlayerCurrentClass(this);
            }
            //Cap experience for level 50
            charaWork.battleSave.skillPoint[classId - 1] = Math.Min(charaWork.battleSave.skillPoint[classId - 1] + exp, MAXEXP[GetLevel() - 1]);

            ActorPropertyPacketUtil expPropertyPacket = new ActorPropertyPacketUtil("charaWork/battleStateForSelf", this);
            expPropertyPacket.AddProperty(String.Format("charaWork.battleSave.skillPoint[{0}]", classId - 1));
            
            QueuePackets(expPropertyPacket.Done());
            Database.SetExp(this, classId, charaWork.battleSave.skillPoint[classId - 1]);

            DevDiagnostics.Trace(
                "player.exp.grant.end",
                "player", String.Format("0x{0:X}", actorId),
                "playerName", customDisplayName != null ? customDisplayName : actorName,
                "classId", classId,
                "currentClassId", GetClass(),
                "oldLevel", startingLevel,
                "newLevel", GetLevel(),
                "oldExp", startingClassExp,
                "newExp", charaWork.battleSave.skillPoint[classId - 1],
                "baseExp", originalExp,
                "bonusPercent", bonusPercent,
                "finalExp", awardedExp,
                "remainderApplied", exp,
                "leveled", leveled);

            return actionList;
        }

        //Equips any abilities for the given classId at the given level. If actionList is not null, adds a "You learn Command" message
        private void EquipAbilitiesAtLevel(byte classId, short level, List<CommandResult> actionList = null)
        {
            //If there's any abilites that unlocks at this level, equip them.
            List<ushort> commandIds = Server.GetWorldManager().GetBattleCommandIdByLevel(classId, level);
            foreach (ushort commandId in commandIds)
            {
                EquipAbilityInFirstOpenSlot(classId, commandId, false);
                byte jobId = ConvertClassIdToJobId(classId);
                if (jobId != classId)
                    EquipAbilityInFirstOpenSlot(jobId, commandId, false);

                //33926: You learn [command].
                if (actionList != null)
                {
                    if (classId == GetCurrentClassOrJob() || jobId == GetCurrentClassOrJob())
                        actionList.Add(new CommandResult(actorId, 33926, 0, commandId));
                }
            }
        }

        //Increaess level of current class and equips new abilities earned at that level
        public void LevelUp(byte classId, List<CommandResult> actionList = null)
        {
            if (charaWork.battleSave.skillLevel[classId - 1] < charaWork.battleSave.skillLevelCap[classId])
            {
                short oldLevel = charaWork.battleSave.skillLevel[classId - 1];

                //Increase level
                charaWork.battleSave.skillLevel[classId - 1]++;
                charaWork.parameterSave.state_mainSkillLevel++;
                short newLevel = charaWork.battleSave.skillLevel[classId - 1];

                DevDiagnostics.Trace(
                    "player.level.up",
                    "player", String.Format("0x{0:X}", actorId),
                    "playerName", customDisplayName != null ? customDisplayName : actorName,
                    "classId", classId,
                    "currentClassId", GetClass(),
                    "oldLevel", oldLevel,
                    "newLevel", newLevel);

                //33909: You attain level [level].
                if (actionList != null)
                    actionList.Add(new CommandResult(actorId, 33909, 0, (ushort)newLevel));

                EquipAbilitiesAtLevel(classId, GetLevel(), actionList);

                if (classId == GetClass())
                    RecalculateStats("level-up");
            }
        }

        public void SetClassAttributeAllocation(PlayerClassAttributeAllocation allocation)
        {
            if (allocation == null)
                return;

            classAttributeAllocations[allocation.classId] = allocation;
        }

        private PlayerClassAttributeAllocation GetClassAttributeAllocation(byte classId)
        {
            PlayerClassAttributeAllocation allocation;
            if (classAttributeAllocations.TryGetValue(classId, out allocation))
                return allocation;

            return new PlayerClassAttributeAllocation(classId, 0, 0, 0, 0, 0, 0, 0);
        }

        public PlayerAttributePointState GetAttributePoints()
        {
            byte classId = GetAttributeAllocationClassId();
            PlayerClassAttributeAllocation allocation = GetClassAttributeAllocation(classId);
            return new PlayerAttributePointState(
                GetEarnedAttributePointsForLevel(GetLevel()),
                GetAttributePointCapForLevel(GetLevel()),
                allocation);
        }

        public bool TrySetAttributePoints(int strength, int vitality, int dexterity, int intelligence, int mind, int piety)
        {
            byte classId = GetAttributeAllocationClassId();
            short level = GetLevel();
            short earnedPoints = GetEarnedAttributePointsForLevel(level);
            short statCap = GetAttributePointCapForLevel(level);
            int[] requested = { strength, vitality, dexterity, intelligence, mind, piety };
            int spentPoints = 0;

            for (int i = 0; i < requested.Length; i++)
            {
                if (requested[i] < 0 || requested[i] > statCap)
                {
                    DevDiagnostics.Trace(
                        "stats.allocation.rejected",
                        "player", String.Format("0x{0:X}", actorId),
                        "classId", classId,
                        "level", level,
                        "reason", "stat-out-of-range",
                        "statIndex", i,
                        "requested", requested[i],
                        "statCap", statCap);
                    return false;
                }

                spentPoints += requested[i];
            }

            if (!IsDiscipleOfWarOrMagicClass(classId) || spentPoints > earnedPoints)
            {
                DevDiagnostics.Trace(
                    "stats.allocation.rejected",
                    "player", String.Format("0x{0:X}", actorId),
                    "classId", classId,
                    "level", level,
                    "reason", !IsDiscipleOfWarOrMagicClass(classId) ? "unsupported-class" : "total-over-allotment",
                    "spentPoints", spentPoints,
                    "earnedPoints", earnedPoints,
                    "statCap", statCap);
                return false;
            }

            PlayerClassAttributeAllocation allocation = new PlayerClassAttributeAllocation(
                classId,
                (short)(earnedPoints - spentPoints),
                (short)strength,
                (short)vitality,
                (short)dexterity,
                (short)intelligence,
                (short)mind,
                (short)piety);

            if (!Database.SavePlayerClassAttributeAllocation(this, allocation))
                return false;

            SetClassAttributeAllocation(allocation);
            RecalculateStats("attribute-allocation");
            DevDiagnostics.Trace(
                "stats.allocation.committed",
                "player", String.Format("0x{0:X}", actorId),
                "classId", classId,
                "level", level,
                "spentPoints", spentPoints,
                "remainingPoints", allocation.pointsRemaining,
                "statCap", statCap,
                "str", strength,
                "vit", vitality,
                "dex", dexterity,
                "int", intelligence,
                "mnd", mind,
                "pie", piety);
            return true;
        }

        public static short GetEarnedAttributePointsForLevel(short level)
        {
            if (level < 10)
                return 0;

            return (short)(level - 5);
        }

        public static short GetAttributePointCapForLevel(short level)
        {
            if (level < 10)
                return 0;

            return (short)(3 + ((level - 10) / 2));
        }

        public static bool IsDiscipleOfWarOrMagicClass(byte classId)
        {
            return classId == CLASSID_PUG ||
                   classId == CLASSID_GLA ||
                   classId == CLASSID_MRD ||
                   classId == CLASSID_ARC ||
                   classId == CLASSID_LNC ||
                   classId == CLASSID_THM ||
                   classId == CLASSID_CNJ;
        }
        
        public static byte ConvertClassIdToJobId(byte classId)
        {
            byte jobId = classId;

            switch(classId)
            {
                case CLASSID_PUG:
                case CLASSID_GLA:
                case CLASSID_MRD:
                    jobId += 13;
                    break;
                case CLASSID_ARC:
                case CLASSID_LNC:
                    jobId += 11;
                    break;
                case CLASSID_THM:
                case CLASSID_CNJ:
                    jobId += 4;
                    break;
            }

            return jobId;
        }

        public static byte ConvertJobIdToClassId(byte jobId)
        {
            switch (jobId)
            {
                case JOBID_MNK:
                    return CLASSID_PUG;
                case JOBID_PLD:
                    return CLASSID_GLA;
                case JOBID_WAR:
                    return CLASSID_MRD;
                case JOBID_BRD:
                    return CLASSID_ARC;
                case JOBID_DRG:
                    return CLASSID_LNC;
                case JOBID_BLM:
                    return CLASSID_THM;
                case JOBID_WHM:
                    return CLASSID_CNJ;
                default:
                    return jobId;
            }
        }

        public byte GetAttributeAllocationClassId()
        {
            return ConvertJobIdToClassId(GetCurrentClassOrJob());
        }

        public byte GetBaseStatClassOrJobId()
        {
            return GetCurrentClassOrJob();
        }

        public void SetCurrentJob(byte jobId)
        {
            currentJob = jobId;
            BroadcastPacket(SetCurrentJobPacket.BuildPacket(actorId, jobId), true);
            Database.SavePlayerCurrentJob(this);
            Database.LoadHotbar(this);
            SendCharaExpInfo();
            RecalculateStats("job-change");
        }

        public short GetClassLevel(byte classId)
        {
            if (classId == 0 || classId > charaWork.battleSave.skillLevel.Length)
                return 0;

            return charaWork.battleSave.skillLevel[classId - 1];
        }

        public bool TryChangeToCurrentClassJob()
        {
            byte baseClassId = charaWork.parameterSave.state_mainSkill[0];
            if (!JobProgressionPolicy.TryGetForBaseClass(baseClassId, out JobProgressionRequirement requirement))
                return false;

            if (currentJob == requirement.JobId)
            {
                SetCurrentJob(0);
                return true;
            }

            if (!HasItem(requirement.SoulCrystalItemId)
                || !JobProgressionPolicy.MeetsLevelRequirements(requirement, GetClassLevel))
                return false;

            SetCurrentJob(requirement.JobId);
            return true;
        }

        //Gets the id of the player's current job. If they aren't a job, gets the id of their class
        public byte GetCurrentClassOrJob()
        {
            if (currentJob != 0)
                return (byte) currentJob;
            return charaWork.parameterSave.state_mainSkill[0];
        }

        public void hpstuff(uint hp)
        {
            SetMaxHP(hp);
            SetHP(hp);            
            mpMaxBase = (ushort)hp;
            charaWork.parameterSave.mpMax = (short)hp;
            charaWork.parameterSave.mp = (short)hp;
            AddTP(3000);
            updateFlags |= ActorUpdateFlags.HpTpMp;
        }
        
        public void SetCombos(int comboId1 = 0, int comboId2 = 0)
        {
            SetCombos(new int[] { comboId1, comboId2 });
        }

        public void SetCombos(int[] comboIds)
        {
            Array.Copy(comboIds, playerWork.comboNextCommandId, 2);

            //If we're starting or continuing a combo chain, add the status effect and combo cost bonus
            if (comboIds[0] != 0)
            {
                StatusEffect comboEffect = new StatusEffect(this, Server.GetWorldManager().GetStatusEffect((uint) StatusEffectId.Combo));
                comboEffect.SetDuration(13);
                comboEffect.SetOverwritable(1);
                statusEffects.AddStatusEffect(comboEffect, this);
                playerWork.comboCostBonusRate = 1;
            }
            //Otherwise we're ending a combo, remove the status
            else
            {
                statusEffects.RemoveStatusEffect(statusEffects.GetStatusEffectById((uint) StatusEffectId.Combo));
                playerWork.comboCostBonusRate = 0;
            }

            ActorPropertyPacketUtil comboPropertyPacket = new ActorPropertyPacketUtil("playerWork/combo", this);
            comboPropertyPacket.AddProperty("playerWork.comboCostBonusRate");
            comboPropertyPacket.AddProperty("playerWork.comboNextCommandId[0]");
            comboPropertyPacket.AddProperty("playerWork.comboNextCommandId[1]");
            QueuePackets(comboPropertyPacket.Done());
        }

        private string GetBaseStatProfileKey(byte classOrJobId, byte tribe, short level)
        {
            return String.Format("{0}:{1}:{2}", classOrJobId, tribe, level);
        }

        private PlayerBaseStatProfile GetCurrentBaseStatProfile()
        {
            byte classOrJobId = GetBaseStatClassOrJobId();
            byte tribe = playerWork.tribe;
            short level = GetLevel();
            string key = GetBaseStatProfileKey(classOrJobId, tribe, level);

            if (baseStatProfiles.ContainsKey(key))
                return baseStatProfiles[key];

            if (missingBaseStatProfiles.Contains(key))
                return null;

            PlayerBaseStatProfile profile = Database.GetPlayerBaseStats(classOrJobId, tribe, level);
            if (profile != null)
            {
                baseStatProfiles[key] = profile;
                return profile;
            }

            missingBaseStatProfiles.Add(key);
            DevDiagnostics.Trace(
                "stats.base.missing",
                "player", String.Format("0x{0:X}", actorId),
                "playerName", customDisplayName != null ? customDisplayName : actorName,
                "classOrJobId", classOrJobId,
                "allocationClassId", GetAttributeAllocationClassId(),
                "tribe", tribe,
                "level", level);

            return null;
        }

        private void ApplyBaseStatProfile()
        {
            PlayerBaseStatProfile profile = GetCurrentBaseStatProfile();
            if (profile == null)
                return;

            AddRecalculatedMod(Modifier.Hp, profile.hp);
            AddRecalculatedMod(Modifier.Mp, profile.mp);
            AddRecalculatedMod(Modifier.Strength, profile.strength);
            AddRecalculatedMod(Modifier.Vitality, profile.vitality);
            AddRecalculatedMod(Modifier.Dexterity, profile.dexterity);
            AddRecalculatedMod(Modifier.Intelligence, profile.intelligence);
            AddRecalculatedMod(Modifier.Mind, profile.mind);
            AddRecalculatedMod(Modifier.Piety, profile.piety);

            DevDiagnostics.Trace(
                "stats.layer.base",
                "player", String.Format("0x{0:X}", actorId),
                "classOrJobId", profile.classId,
                "tribe", profile.tribe,
                "level", profile.level,
                "source", profile.source,
                "hp", profile.hp,
                "mp", profile.mp,
                "str", profile.strength,
                "vit", profile.vitality,
                "dex", profile.dexterity,
                "int", profile.intelligence,
                "mnd", profile.mind,
                "pie", profile.piety);
        }

        private void ApplyClassAttributeAllocation()
        {
            byte classId = GetAttributeAllocationClassId();
            if (!IsDiscipleOfWarOrMagicClass(classId))
                return;

            PlayerClassAttributeAllocation allocation = GetClassAttributeAllocation(classId);
            short earnedPoints = GetEarnedAttributePointsForLevel(GetLevel());
            short statCap = GetAttributePointCapForLevel(GetLevel());

            AddRecalculatedMod(Modifier.Strength, allocation.strength);
            AddRecalculatedMod(Modifier.Vitality, allocation.vitality);
            AddRecalculatedMod(Modifier.Dexterity, allocation.dexterity);
            AddRecalculatedMod(Modifier.Intelligence, allocation.intelligence);
            AddRecalculatedMod(Modifier.Mind, allocation.mind);
            AddRecalculatedMod(Modifier.Piety, allocation.piety);

            DevDiagnostics.Trace(
                "stats.layer.allocation",
                "player", String.Format("0x{0:X}", actorId),
                "classId", classId,
                "level", GetLevel(),
                "earnedPoints", earnedPoints,
                "storedRemaining", allocation.pointsRemaining,
                "storedSpent", allocation.SpentPoints(),
                "statCap", statCap,
                "str", allocation.strength,
                "vit", allocation.vitality,
                "dex", allocation.dexterity,
                "int", allocation.intelligence,
                "mnd", allocation.mind,
                "pie", allocation.piety);
        }

        private bool ApplyEquipmentParamBonus(int paramType, short value)
        {
            if (value == 0)
                return false;

            uint modifierId;
            if (!EquipmentStatPolicy.TryGetModifierId(paramType, out modifierId))
                return false;

            AddRecalculatedMod((Modifier)modifierId, value);
            return true;
        }

        private static bool HasEquipmentBenefits(InventoryItem item)
        {
            if (item == null || item.itemData == null)
                return false;

            // Non-durable equipment has no condition gate. Durable equipment is
            // always created with a modifier row at maximum condition; a missing
            // row or zero condition therefore means it provides no benefits.
            if (item.itemData.durability <= 0)
                return true;

            return item.modifiers != null && item.modifiers.durability > 0;
        }

        private int ApplyEquipmentBonusPairs(EquipmentItem itemData, bool includeHighQualityBonus)
        {
            int applied = 0;
            int[] types =
            {
                itemData.paramBonusType1,
                itemData.paramBonusType2,
                itemData.paramBonusType3,
                itemData.paramBonusType4,
                itemData.paramBonusType5,
                itemData.paramBonusType6,
                itemData.paramBonusType7,
                itemData.paramBonusType8,
                itemData.paramBonusType9,
                itemData.paramBonusType10
            };
            short[] values =
            {
                itemData.paramBonusValue1,
                itemData.paramBonusValue2,
                itemData.paramBonusValue3,
                itemData.paramBonusValue4,
                itemData.paramBonusValue5,
                itemData.paramBonusValue6,
                itemData.paramBonusValue7,
                itemData.paramBonusValue8,
                itemData.paramBonusValue9,
                itemData.paramBonusValue10
            };

            for (int i = 0; i < types.Length; i++)
            {
                int pairNumber = i + 1;
                if (!EquipmentStatPolicy.ShouldApplyBonusPair(pairNumber, includeHighQualityBonus))
                    continue;

                if (ApplyEquipmentParamBonus(types[i], values[i]))
                    applied++;
            }

            return applied;
        }

        private void ApplyMainHandToolStats(WeaponItem weapon, bool isHighQuality)
        {
            AddRecalculatedMod(Modifier.Craftsmanship,
                EquipmentStatPolicy.CalculateToolValue(weapon.craftProcessing, isHighQuality));
            AddRecalculatedMod(Modifier.MagicCraftsmanship,
                EquipmentStatPolicy.CalculateToolValue(weapon.craftMagicProcessing, isHighQuality));
            AddRecalculatedMod(Modifier.Control,
                EquipmentStatPolicy.CalculateToolValue(weapon.craftProcessControl, isHighQuality));
            AddRecalculatedMod(Modifier.Gathering,
                EquipmentStatPolicy.CalculateToolValue(weapon.harvestPotency, isHighQuality));
            AddRecalculatedMod(Modifier.Output,
                EquipmentStatPolicy.CalculateToolValue(weapon.harvestLimit, isHighQuality));
            AddRecalculatedMod(Modifier.Perception,
                EquipmentStatPolicy.CalculateToolValue(weapon.harvestRate, isHighQuality));
        }

        private void ApplyEquipmentStats()
        {
            int equippedItems = 0;
            int activeItems = 0;
            int appliedBonusPairs = 0;
            int armorDefense = 0;

            for (ushort slot = 0; slot < equipment.GetCapacity(); slot++)
            {
                InventoryItem equippedItem = equipment.GetItemAtSlot(slot);
                if (equippedItem == null)
                    continue;

                EquipmentItem itemData = equippedItem.itemData as EquipmentItem;
                if (itemData == null)
                    continue;

                equippedItems++;
                bool hasBenefits = HasEquipmentBenefits(equippedItem);
                bool atOrBelowLevel = itemData.level <= GetLevel();
                bool isHighQuality = equippedItem.quality >= 2;
                bool hasHighQualityBonus = equippedItem.modifiers != null
                    && equippedItem.modifiers.mainQuality > 1;
                int itemDefense = 0;
                int itemBonusPairs = 0;

                if (hasBenefits)
                {
                    activeItems++;

                    ArmorItem armor = itemData as ArmorItem;
                    if (armor != null)
                    {
                        itemDefense = EquipmentStatPolicy.CalculateArmorDefense(
                            armor.defense,
                            GetLevel(),
                            itemData.level,
                            isHighQuality);
                        AddRecalculatedMod(Modifier.Defense, itemDefense);
                        armorDefense += itemDefense;
                    }

                    // The 1.23b client exposes pair 3 as the append parameter,
                    // pair 4 only for HQ instances, and pairs 5-10 as ordinary
                    // bonuses. Official traces show those ordinary bonuses are
                    // absent when the item is above the player's level.
                    if (atOrBelowLevel)
                    {
                        itemBonusPairs = ApplyEquipmentBonusPairs(itemData, hasHighQualityBonus);
                        appliedBonusPairs += itemBonusPairs;
                    }

                    // Official captures show the displayed crafting/gathering
                    // trio comes from the main tool only. Above-level tool
                    // scaling is deliberately deferred until a capture proves
                    // whether the generic level-adjust curve applies here.
                    WeaponItem weapon = itemData as WeaponItem;
                    if (slot == SLOT_MAINHAND && weapon != null && atOrBelowLevel)
                        ApplyMainHandToolStats(weapon, isHighQuality);
                }

                DevDiagnostics.Trace(
                    "stats.layer.equipment.item",
                    "player", String.Format("0x{0:X}", actorId),
                    "slot", slot,
                    "itemId", equippedItem.itemId,
                    "itemLevel", itemData.level,
                    "playerLevel", GetLevel(),
                    "quality", equippedItem.quality,
                    "mainQuality", equippedItem.modifiers != null ? equippedItem.modifiers.mainQuality : 0,
                    "durability", equippedItem.modifiers != null ? equippedItem.modifiers.durability : 0,
                    "hasBenefits", hasBenefits,
                    "atOrBelowLevel", atOrBelowLevel,
                    "armorDefense", itemDefense,
                    "bonusPairs", itemBonusPairs);
            }

            DevDiagnostics.Trace(
                "stats.layer.equipment",
                "player", String.Format("0x{0:X}", actorId),
                "classId", GetClass(),
                "level", GetLevel(),
                "equippedItems", equippedItems,
                "activeItems", activeItems,
                "appliedBonusPairs", appliedBonusPairs,
                "armorDefense", armorDefense,
                "hp", GetMod(Modifier.Hp),
                "mp", GetMod(Modifier.Mp),
                "str", GetMod(Modifier.Strength),
                "vit", GetMod(Modifier.Vitality),
                "dex", GetMod(Modifier.Dexterity),
                "int", GetMod(Modifier.Intelligence),
                "mnd", GetMod(Modifier.Mind),
                "pie", GetMod(Modifier.Piety),
                "attack", GetMod(Modifier.Attack),
                "defense", GetMod(Modifier.Defense));
        }

        public override void CalculateBaseStats()
        {
            ApplyBaseStatProfile();
            ApplyClassAttributeAllocation();

            //Add weapon property mod
            var equip = GetEquipment();
            var mainHandItem = equip.GetItemAtSlot(SLOT_MAINHAND);
            var damageAttribute = 0;
            var attackDelay = 3000;
            var hitCount = 1;

            if (mainHandItem != null)
            {
                var mainHandWeapon = (Server.GetItemGamedata(mainHandItem.itemId) as WeaponItem);
                damageAttribute = mainHandWeapon.damageAttributeType1;
                attackDelay = (int) (mainHandWeapon.damageInterval * 1000);
                hitCount = mainHandWeapon.frequency;
            }

            var hasShield = equip.GetItemAtSlot(SLOT_OFFHAND) != null ? 1 : 0;
            SetMod((uint)Modifier.CanBlock, hasShield);

            SetMod((uint)Modifier.AttackType, damageAttribute);
            SetMod((uint)Modifier.Delay, attackDelay);
            SetMod((uint)Modifier.HitCount, hitCount);

            //These stats all correlate in a 3:2 fashion
            //It seems these stats don't actually increase their respective stats. The magic stats do, however
            AddRecalculatedMod(Modifier.Attack, (long)(GetMod(Modifier.Strength) * 0.667));
            AddRecalculatedMod(Modifier.Accuracy, (long)(GetMod(Modifier.Dexterity) * 0.667));
            AddRecalculatedMod(Modifier.Defense, (long)(GetMod(Modifier.Vitality) * 0.667));

            //These stats correlate in a 4:1 fashion. (Unsure if MND is accurate but it would make sense for it to be)
            AddRecalculatedMod(Modifier.AttackMagicPotency, (long)((float)GetMod(Modifier.Intelligence) * 0.25));

            AddRecalculatedMod(Modifier.MagicAccuracy, (long)((float)GetMod(Modifier.Mind) * 0.25));
            AddRecalculatedMod(Modifier.HealingMagicPotency, (long)((float)GetMod(Modifier.Mind) * 0.25));

            AddRecalculatedMod(Modifier.MagicEvasion, (long)((float)GetMod(Modifier.Piety) * 0.25));
            AddRecalculatedMod(Modifier.EnfeeblingMagicPotency, (long)((float)GetMod(Modifier.Piety) * 0.25));

            //VIT correlates to HP in a 1:1 fashion
            AddRecalculatedMod(Modifier.Hp, (long)GetMod(Modifier.Vitality));

            // Equipment is intentionally applied after the incomplete base
            // derivation above. Official gear-change captures show that primary
            // attributes granted by equipment do not cascade into the displayed
            // Attack/Accuracy/Defense values in 1.23b.
            ApplyEquipmentStats();

            CalculateTraitMods();
            base.CalculateBaseStats();
        }

        public bool HasTrait(ushort id)
        {
            BattleTrait trait = Server.GetWorldManager().GetBattleTrait(id);

            return HasTrait(trait);
        }

        public bool HasTrait(BattleTrait trait)
        {
            return (trait != null) && (trait.job == GetClass()) && (trait.level <= GetLevel());
        }

        public void CalculateTraitMods()
        {
            var traitIds = Server.GetWorldManager().GetAllBattleTraitIdsForClass((byte) GetClass());

            foreach(var traitId in traitIds)
            {
                var trait = Server.GetWorldManager().GetBattleTrait(traitId);
                if(HasTrait(trait))
                {
                    AddRecalculatedMod((Modifier)trait.modifier, trait.bonus);
                }
            }
        }

        public bool HasItemEquippedInSlot(uint itemId, ushort slot)
        {
            var equippedItem = equipment.GetItemAtSlot(slot);

            return equippedItem != null && equippedItem.itemId == itemId;
        }

        public Retainer GetSpawnedRetainer()
        {
            return currentSpawnedRetainer;
        }

        public void StartTradeTransaction(Player otherPlayer)
        {
            myOfferings = new ReferencedItemPackage(this, ItemPackage.MAXSIZE_TRADE, ItemPackage.TRADE);            
            otherTrader = otherPlayer;
            isTradeAccepted = false;
        }

        public Player GetOtherTrader()
        {
            return otherTrader;
        }

        public ReferencedItemPackage GetTradeOfferings()
        {
            return myOfferings;
        }

        public bool IsTrading()
        {
            return otherTrader != null;
        }

        public bool IsTradeAccepted()
        {
            return isTradeAccepted;
        }
        
        public void AddTradeItem(ushort slot, ItemRefParam chosenItem, int tradeQuantity)
        {
            if (!IsTrading())
                return;
            
            //Get chosen item
            InventoryItem offeredItem = itemPackages[chosenItem.itemPackage].GetItemAtSlot(chosenItem.slot);
            offeredItem.SetTradeQuantity(tradeQuantity);
            
            myOfferings.Set(slot, offeredItem);
            SendTradePackets();
        }
        
        public void RemoveTradeItem(ushort slot)
        {
            if (!IsTrading())
                return;

            InventoryItem offeredItem = myOfferings.GetItemAtSlot(slot);
            offeredItem.SetNormal();

            myOfferings.Clear(slot);
            SendTradePackets();
        }

        public void ClearTradeItems()
        {
            if (!IsTrading())
                return;

            for (ushort i = 0; i < myOfferings.GetCapacity(); i++)
            {
                InventoryItem offeredItem = myOfferings.GetItemAtSlot(i);
                if (offeredItem != null)
                    offeredItem.SetNormal();
            }

            myOfferings.ClearAll();
            SendTradePackets();
        }

        private void SendTradePackets()
        {
            //Send to self
            QueuePacket(InventoryBeginChangePacket.BuildPacket(actorId, true));
            myOfferings.SendUpdate(this);
            QueuePacket(InventoryEndChangePacket.BuildPacket(actorId));

            //Send to other trader
            otherTrader.QueuePacket(InventoryBeginChangePacket.BuildPacket(actorId, true));
            myOfferings.SendUpdateAsItemPackage(otherTrader);
            otherTrader.QueuePacket(InventoryEndChangePacket.BuildPacket(actorId));
        }

        public void AcceptTrade(bool accepted)
        {
            if (!IsTrading())
                return;
            isTradeAccepted = accepted;            
        }

        public void FinishTradeTransaction()
        {
            if (myOfferings != null)
            {
                myOfferings.ClearAll();
                for (ushort i = 0; i < myOfferings.GetCapacity(); i++)
                {
                    InventoryItem offeredItem = myOfferings.GetItemAtSlot(i);
                    if (offeredItem != null)
                        offeredItem.SetNormal();
                }

                QueuePacket(InventoryBeginChangePacket.BuildPacket(actorId, true));
                myOfferings.SendUpdate(this);
                QueuePacket(InventoryEndChangePacket.BuildPacket(actorId));
            }

            isTradeAccepted = false;
            myOfferings = null;
            otherTrader = null;
        }
        
    }
}
