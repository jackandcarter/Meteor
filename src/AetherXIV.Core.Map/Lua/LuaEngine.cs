using AetherXIV.Core.Map.actors.director;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.packets.receive.events;
using AetherXIV.Core.Map.packets.send;
using AetherXIV.Core.Map.packets.send.events;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using MoonSharp.Interpreter.Loaders;
using System;
using System.Collections.Generic;
using System.IO;
using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.area;
using System.Threading;
using AetherXIV.Core.Map.actors.chara.ai;
using AetherXIV.Core.Map.actors.chara.ai.controllers;

namespace AetherXIV.Core.Map.lua
{
    class LuaEngine
    {
        public const string FILEPATH_PLAYER = "./scripts/player.lua";
        public const string FILEPATH_ZONE = "./scripts/unique/{0}/zone.lua";
        public const string FILEPATH_CONTENT = "./scripts/content/{0}.lua";
        public const string FILEPATH_COMMANDS = "./scripts/commands/{0}.lua";
        public const string FILEPATH_DIRECTORS = "./scripts/directors/{0}.lua";
        public const string FILEPATH_NPCS = "./scripts/unique/{0}/{1}/{2}.lua";
        public const string FILEPATH_QUEST = "./scripts/quests/{0}/{1}.lua";

        private static LuaEngine mThisEngine;
        private Dictionary<Coroutine, ulong> mSleepingOnTime = new Dictionary<Coroutine, ulong>();
        private Dictionary<string, List<Coroutine>> mSleepingOnSignal = new Dictionary<string, List<Coroutine>>();
        private Dictionary<uint, Coroutine> mSleepingOnPlayerEvent = new Dictionary<uint, Coroutine>();

        private Timer luaTimer;


        private LuaEngine()
        {
            UserData.RegistrationPolicy = InteropRegistrationPolicy.Automatic;

            luaTimer = new Timer(new TimerCallback(PulseSleepingOnTime),
                           null, TimeSpan.Zero, TimeSpan.FromMilliseconds(50));
        }

        public static LuaEngine GetInstance()
        {
            if (mThisEngine == null)
                mThisEngine = new LuaEngine();

            return mThisEngine;
        }

        public void AddWaitCoroutine(Coroutine coroutine, float seconds)
        {
            ulong time = Utils.MilisUnixTimeStampUTC() + (ulong)(seconds * 1000);
            mSleepingOnTime.Add(coroutine, time);
            DevDiagnostics.Trace(
                "lua.wait.register",
                "waitType", "_WAIT_TIME",
                "seconds", seconds,
                "wakeTime", time,
                "coroutine", coroutine.GetHashCode(),
                "timeWaiters", mSleepingOnTime.Count);
        }

        public void AddWaitSignalCoroutine(Coroutine coroutine, string signal)
        {
            if (!mSleepingOnSignal.ContainsKey(signal))
                mSleepingOnSignal.Add(signal, new List<Coroutine>());
            mSleepingOnSignal[signal].Add(coroutine);
            DevDiagnostics.Trace(
                "lua.wait.register",
                "waitType", "_WAIT_SIGNAL",
                "signal", signal,
                "coroutine", coroutine.GetHashCode(),
                "signalWaiters", mSleepingOnSignal[signal].Count);
        }

        public void AddWaitEventCoroutine(Player player, Coroutine coroutine)
        {
            if (!mSleepingOnPlayerEvent.ContainsKey(player.actorId))
                mSleepingOnPlayerEvent.Add(player.actorId, coroutine);
            DevDiagnostics.Trace(
                "lua.wait.register",
                "player", player.customDisplayName,
                "actor", String.Format("0x{0:X}", player.actorId),
                "waitType", "_WAIT_EVENT",
                "coroutine", coroutine.GetHashCode(),
                "eventWaiters", mSleepingOnPlayerEvent.Count);
        }

        public void PulseSleepingOnTime(object state)
        {
            ulong currentTime = Utils.MilisUnixTimeStampUTC();
            List<Coroutine> mToAwake = new List<Coroutine>();

            foreach (KeyValuePair<Coroutine, ulong> entry in mSleepingOnTime)
            {
                if (entry.Value <= currentTime)
                    mToAwake.Add(entry.Key);
            }

            foreach (Coroutine key in mToAwake)
            {
                mSleepingOnTime.Remove(key);
                DevDiagnostics.Trace(
                    "lua.time.resume",
                    "coroutine", key.GetHashCode(),
                    "remainingTimeWaiters", mSleepingOnTime.Count);
                DynValue value = key.Resume();
                ResolveResume(null, key, value);
            }
        }

        public void OnSignal(string signal, params object[] args)
        {
            List<Coroutine> mToAwake = new List<Coroutine>();
            int waiterCount = mSleepingOnSignal.ContainsKey(signal) ? mSleepingOnSignal[signal].Count : 0;
            DevDiagnostics.Trace(
                "lua.signal.emit",
                "signal", signal,
                "args", args == null ? 0 : args.Length,
                "waiters", waiterCount);

            if (mSleepingOnSignal.ContainsKey(signal))
            {
                mToAwake.AddRange(mSleepingOnSignal[signal]);
                mSleepingOnSignal.Remove(signal);
            }

            foreach (Coroutine key in mToAwake)
            {
                DevDiagnostics.Trace(
                    "lua.signal.resume",
                    "signal", signal,
                    "coroutine", key.GetHashCode());
                DynValue value = key.Resume(args);
                ResolveResume(null, key, value);
            }
        }

        public void OnEventUpdate(Player player, List<LuaParam> args)
        {
            if (mSleepingOnPlayerEvent.ContainsKey(player.actorId))
            {
                try
                {
                    Coroutine coroutine = mSleepingOnPlayerEvent[player.actorId];
                    mSleepingOnPlayerEvent.Remove(player.actorId);
                    DevDiagnostics.Trace(
                        "lua.resume",
                        "player", player.customDisplayName,
                        "actor", String.Format("0x{0:X}", player.actorId),
                        "source", "event.update",
                        "coroutine", coroutine.GetHashCode(),
                        "params", LuaUtils.DumpParams(args));
                    DynValue value = coroutine.Resume(LuaUtils.CreateLuaParamObjectList(args));
                    ResolveResume(player, coroutine, value);
                }
                catch (ScriptRuntimeException e)
                {
                    LuaEngine.SendError(player, String.Format("OnEventUpdated: {0}", e.DecoratedMessage));
                    player.EndEvent();
                }
            }
            else
            {
                DevDiagnostics.Trace(
                    "lua.resumeMissing",
                    "player", player.customDisplayName,
                    "actor", String.Format("0x{0:X}", player.actorId),
                    "source", "event.update",
                    "params", LuaUtils.DumpParams(args));
                player.EndEvent();
            }
        }

        /// <summary> 
        /// // todo: this is dumb, should probably make a function for each action with different default return values
        /// or just make generic function and pass default value as first arg after functionName
        /// </summary>
        public static void CallLuaBattleFunction(Character actor, string functionName, params object[] args)
        {
            // todo: should use "scripts/zones/ZONE_NAME/battlenpcs/NAME.lua" instead of scripts/unique
            string path = "";

            // todo: should we call this for players too?
            if (actor is Player)
            {
                // todo: check this is correct
                path = FILEPATH_PLAYER;
            }
            else if (actor is Npc)
            {
                // todo: this is probably unnecessary as im not sure there were pets for players
                if (!(actor.aiContainer.GetController<PetController>()?.GetPetMaster() is Player))
                    path = String.Format("./scripts/unique/{0}/{1}/{2}.lua", actor.zone.zoneName, actor is BattleNpc ? "Monster" : "PopulaceStandard", ((Npc)actor).GetUniqueId());
            }
            // dont wanna throw an error if file doesnt exist
            path = ResolveExistingPath(path);
            if (File.Exists(path))
            {
                var script = LoadGlobals();
                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleFunction [{functionName}] {e.Message}");
                }
                DynValue res = new DynValue();

                if (!script.Globals.Get(functionName).IsNil())
                {
                    res = script.Call(script.Globals.Get(functionName), args);
                }
            }
        }

        public static int CallLuaStatusEffectFunction(Character actor, StatusEffect effect, string functionName, params object[] args)
        {
            // todo: this is stupid, load the actual effect name from db table
            string path = $"./scripts/effects/{effect.GetName()}.lua";

            if (File.Exists(path))
            {
                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaStatusEffectFunction [{functionName}] {e.Message}");
                }
                DynValue res = new DynValue();

                if (!script.Globals.Get(functionName).IsNil())
                {
                    res = script.Call(script.Globals.Get(functionName), args);
                    if (res != null)
                        return (int)res.Number;
                }
            }
            else
            {
                Program.Log.Error($"LuaEngine.CallLuaStatusEffectFunction [{effect.GetName()}] Unable to find script {path}");
            }
            return -1;
        }

        public static int CallLuaBattleCommandFunction(Character actor, BattleCommand command, string folder, string functionName, params object[] args)
        {
            string path = $"./scripts/commands/{folder}/{command.name}.lua";
            string requestedPath = path;

            if (File.Exists(path))
            {
                DevDiagnostics.Trace(
                    "lua.commandScript.resolve",
                    "actor", actor == null ? "0x0" : String.Format("0x{0:X}", actor.actorId),
                    "actorName", actor == null ? "" : (actor.customDisplayName != null ? actor.customDisplayName : actor.actorName),
                    "commandId", command.id,
                    "commandName", command.name,
                    "folder", folder,
                    "function", functionName,
                    "path", path,
                    "defaultUsed", false,
                    "resolved", true);
                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction [{functionName}] {e.Message}");
                }
                DynValue res = new DynValue();
                
                if (!script.Globals.Get(functionName).IsNil())
                {
                    res = script.Call(script.Globals.Get(functionName), args);
                    if (res != null)
                        return (int)res.Number;
                }
            }
            else
            {
                path = $"./scripts/commands/{folder}/default.lua";
                bool defaultExists = File.Exists(path);
                DevDiagnostics.Trace(
                    "lua.commandScript.resolve",
                    "actor", actor == null ? "0x0" : String.Format("0x{0:X}", actor.actorId),
                    "actorName", actor == null ? "" : (actor.customDisplayName != null ? actor.customDisplayName : actor.actorName),
                    "commandId", command.id,
                    "commandName", command.name,
                    "folder", folder,
                    "function", functionName,
                    "path", path,
                    "requestedPath", requestedPath,
                    "defaultUsed", true,
                    "resolved", defaultExists);
                if (!defaultExists)
                    return -1;

                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction [{functionName}] {e.Message}");
                }
                DynValue res = new DynValue();
               // DynValue r = script.Globals.Get(functionName);

                if (!script.Globals.Get(functionName).IsNil())
                {
                    res = script.Call(script.Globals.Get(functionName), args);
                    if (res != null)
                        return (int)res.Number;
                }
            }
            return -1;
        }


        public static void LoadBattleCommandScript(BattleCommand command, string folder)
        {
            string path = $"./scripts/commands/{folder}/{command.name}.lua";

            if (File.Exists(path))
            {
                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction {e.Message}");
                }
                command.script = script;
            }
            else
            {
                path = $"./scripts/commands/{folder}/default.lua";
                if (!File.Exists(path))
                {
                    DevDiagnostics.Trace(
                        "lua.commandScript.resolve",
                        "commandId", command.id,
                        "commandName", command.name,
                        "folder", folder,
                        "path", path,
                        "defaultUsed", true,
                        "resolved", false);
                    return;
                }

                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction {e.Message}");
                }

                command.script = script;
            }
        }

        public static void LoadStatusEffectScript(StatusEffect effect)
        {
            string path = $"./scripts/effects/{effect.GetName()}.lua";

            if (File.Exists(path))
            {
                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction {e.Message}");
                }
                effect.script = script;
            }
            else
            {
                path = $"./scripts/effects/default.lua";
                if (!File.Exists(path))
                {
                    DevDiagnostics.Trace(
                        "lua.effectScript.resolve",
                        "effect", effect.GetName(),
                        "path", path,
                        "defaultUsed", true,
                        "resolved", false);
                    return;
                }

                var script = LoadGlobals();

                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error($"LuaEngine.CallLuaBattleCommandFunction {e.Message}");
                }

                effect.script = script;
            }
        }


        public static string GetScriptPath(Actor target)
        {
            if (target is Player)
            {
                return String.Format(FILEPATH_PLAYER);
            }
            else if (target is Npc)
            {
                return null;
            }
            else if (target is Command)
            {
                return String.Format(FILEPATH_COMMANDS, target.GetName());
            }
            else if (target is Director)
            {
                return String.Format(FILEPATH_DIRECTORS, ((Director)target).GetScriptPath());
            }
            else if (target is PrivateAreaContent)
            {
                return String.Format(FILEPATH_CONTENT, ((PrivateAreaContent)target).GetPrivateAreaName());
            }
            else if (target is Area)
            {
                return String.Format(FILEPATH_ZONE, ((Area)target).zoneName);
            }
            else if (target is Quest)
            {
                string initial = ((Quest)target).actorName.Substring(0, 3);
                string questName = ((Quest)target).actorName;
                return String.Format(FILEPATH_QUEST, initial, questName);
            }
            else
                return "";
        }

        public static string ResolveExistingPath(string requestedPath)
        {
            if (String.IsNullOrWhiteSpace(requestedPath))
                return requestedPath;

            if (File.Exists(requestedPath) || Directory.Exists(requestedPath))
                return requestedPath;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(requestedPath);
            }
            catch
            {
                return requestedPath;
            }

            string root = Path.GetPathRoot(fullPath);
            string current = String.IsNullOrEmpty(root) ? Directory.GetCurrentDirectory() : root;
            string remainder = fullPath.Substring(current.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] parts = remainder.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                string direct = Path.Combine(current, part);
                if (File.Exists(direct) || Directory.Exists(direct))
                {
                    current = direct;
                    continue;
                }

                if (!Directory.Exists(current))
                    return requestedPath;

                string match = null;
                try
                {
                    foreach (string entry in Directory.GetFileSystemEntries(current))
                    {
                        if (String.Equals(Path.GetFileName(entry), part, StringComparison.OrdinalIgnoreCase))
                        {
                            match = entry;
                            break;
                        }
                    }
                }
                catch
                {
                    return requestedPath;
                }

                if (match == null)
                    return requestedPath;

                current = match;
            }

            return current;
        }

        private List<LuaParam> CallLuaFunctionNpcForReturn(Player player, Npc target, string funcName, bool optional, params object[] args)
        {
            object[] args2 = new object[args.Length + (player == null ? 1 : 2)];
            Array.Copy(args, 0, args2, (player == null ? 1 : 2), args.Length);
            if (player != null)
            {
                args2[0] = player;
                args2[1] = target;
            }
            else
                args2[0] = target;

            LuaScript parent = null, child = null;
            string parentRequestedPath = "./scripts/base/" + target.classPath + ".lua";
            string parentPath = ResolveExistingPath(parentRequestedPath);
            string childPath = null;
            string childRequestedPath = null;

            if (File.Exists(parentPath))
                parent = LuaEngine.LoadScript(parentPath);

            Area area = target.zone;
            if (area is PrivateArea)
            {
                childRequestedPath = String.Format("./scripts/unique/{0}/PrivateArea/{1}_{2}/{3}/{4}.lua", area.zoneName, ((PrivateArea)area).GetPrivateAreaName(), ((PrivateArea)area).GetPrivateAreaType(), target.className, target.GetUniqueId());
                childPath = ResolveExistingPath(childRequestedPath);
                if (File.Exists(childPath))
                    child = LuaEngine.LoadScript(childPath);
            }
            else
            {
                childRequestedPath = String.Format("./scripts/unique/{0}/{1}/{2}.lua", area.zoneName, target.className, target.GetUniqueId());
                childPath = ResolveExistingPath(childRequestedPath);
                if (File.Exists(childPath))
                    child = LuaEngine.LoadScript(childPath);
            }

            DevDiagnostics.Trace(
                "lua.script.resolve",
                "player", player == null ? "(none)" : player.customDisplayName,
                "actor", target.GetName(),
                "uniqueId", target.GetUniqueId(),
                "className", target.className,
                "classPath", target.classPath,
                "function", funcName,
                "parentRequestedPath", parentRequestedPath,
                "parentPath", parentPath,
                "parentExists", parent != null,
                "childRequestedPath", childRequestedPath,
                "childPath", childPath,
                "childExists", child != null,
                "resolved", parent != null || child != null);

            if (parent == null && child == null)
            {
                LuaEngine.SendError(player, String.Format("ERROR: Could not find script for actor {0}.", target.GetName()));
            }

            //Run Script
            DynValue result;

            if (child != null && child.Globals[funcName] != null)
                result = child.Call(child.Globals[funcName], args2);
            else if (parent != null && parent.Globals[funcName] != null)
                result = parent.Call(parent.Globals[funcName], args2);
            else
                return null;

            List<LuaParam> lparams = LuaUtils.CreateLuaParamList(result);
            return lparams;
        }

        private void CallLuaFunctionNpc(Player player, Npc target, string funcName, bool optional, params object[] args)
        {
            object[] args2 = new object[args.Length + (player == null ? 1 : 2)];
            Array.Copy(args, 0, args2, (player == null ? 1 : 2), args.Length);
            if (player != null)
            {
                args2[0] = player;
                args2[1] = target;
            }
            else
                args2[0] = target;

            LuaScript parent = null, child = null;
            string parentRequestedPath = "./scripts/base/" + target.classPath + ".lua";
            string parentPath = ResolveExistingPath(parentRequestedPath);
            string childPath = null;
            string childRequestedPath = null;

            if (File.Exists(parentPath))
                parent = LuaEngine.LoadScript(parentPath);

            Area area = target.zone;
            if (area is PrivateArea)
            {
                childRequestedPath = String.Format("./scripts/unique/{0}/PrivateArea/{1}_{2}/{3}/{4}.lua", area.zoneName, ((PrivateArea)area).GetPrivateAreaName(), ((PrivateArea)area).GetPrivateAreaType(), target.className, target.GetUniqueId());
                childPath = ResolveExistingPath(childRequestedPath);
                if (File.Exists(childPath))
                    child = LuaEngine.LoadScript(childPath);
            }
            else
            {
                childRequestedPath = String.Format("./scripts/unique/{0}/{1}/{2}.lua", area.zoneName, target.className, target.GetUniqueId());
                childPath = ResolveExistingPath(childRequestedPath);
                if (File.Exists(childPath))
                    child = LuaEngine.LoadScript(childPath);
            }

            DevDiagnostics.Trace(
                "lua.script.resolve",
                "player", player == null ? "(none)" : player.customDisplayName,
                "actor", target.GetName(),
                "uniqueId", target.GetUniqueId(),
                "className", target.className,
                "classPath", target.classPath,
                "function", funcName,
                "parentRequestedPath", parentRequestedPath,
                "parentPath", parentPath,
                "parentExists", parent != null,
                "childRequestedPath", childRequestedPath,
                "childPath", childPath,
                "childExists", child != null,
                "resolved", parent != null || child != null);

            if (parent == null && child == null)
            {
                LuaEngine.SendError(player, String.Format("Could not find script for actor {0}.", target.GetName()));
                if (player != null && funcName == "onEventStarted")
                    player.EndEvent();
                return;
            }

            //Run Script
            Coroutine coroutine = null;

            if (child != null && !child.Globals.Get(funcName).IsNil())
                coroutine = child.CreateCoroutine(child.Globals[funcName]).Coroutine;
            else if (parent != null && parent.Globals.Get(funcName) != null && !parent.Globals.Get(funcName).IsNil())
                coroutine = parent.CreateCoroutine(parent.Globals[funcName]).Coroutine;

            if (coroutine != null)
            {
                try
                {
                    DynValue value = coroutine.Resume(args2);
                    ResolveResume(player, coroutine, value);
                }
                catch (ScriptRuntimeException e)
                {
                    Program.Log.Error("Lua NPC function failed: player={0} actor={1} unique={2} class={3} func={4} parent={5} child={6}: {7}",
                        player != null ? player.customDisplayName : "(none)",
                        target.GetName(),
                        target.GetUniqueId(),
                        target.classPath,
                        funcName,
                        parentPath,
                        childPath,
                        e.DecoratedMessage);
                    SendError(player, e.DecoratedMessage);
                    if (player != null)
                        player.EndEvent();
                }
            }
            else if (!optional)
            {
                if (IsOptionalMonsterEvent(target, funcName))
                {
                    TraceOptionalMissingFunction(player, target, funcName, parentPath, childPath);
                    if (player != null && funcName == "onEventStarted")
                        player.EndEvent();
                    return;
                }

                LuaEngine.SendError(player, String.Format("Could not find function '{0}' for actor {1}.", funcName, target.GetName()));
                if (player != null && funcName == "onEventStarted")
                    player.EndEvent();
            }
        }

        private static bool IsOptionalMonsterEvent(Npc target, string funcName)
        {
            return funcName == "onEventStarted" && target is BattleNpc;
        }

        private static void TraceOptionalMissingFunction(Player player, Npc target, string funcName, string parentPath, string childPath)
        {
            DevDiagnostics.Trace(
                "lua.function.missing.optional",
                "player", player == null ? "(none)" : player.customDisplayName,
                "actor", target.GetName(),
                "actorId", String.Format("0x{0:X}", target.actorId),
                "uniqueId", target.GetUniqueId(),
                "className", target.className,
                "classPath", target.classPath,
                "function", funcName,
                "parentPath", parentPath,
                "childPath", childPath,
                "reason", "battle NPC event hook is optional");
        }

        public List<LuaParam> CallLuaFunctionForReturn(Player player, Actor target, string funcName, bool optional, params object[] args)
        {
            //Need a seperate case for NPCs cause that child/parent thing.
            if (target is Npc)
                return CallLuaFunctionNpcForReturn(player, (Npc)target, funcName, optional, args);

            object[] args2 = new object[args.Length + (player == null ? 1 : 2)];
            Array.Copy(args, 0, args2, (player == null ? 1 : 2), args.Length);
            if (player != null)
            {
                args2[0] = player;
                args2[1] = target;
            }
            else
                args2[0] = target;

            string luaPath = GetScriptPath(target);
            LuaScript script = LoadScript(luaPath);
            if (script != null)
            {
                if (!script.Globals.Get(funcName).IsNil())
                {
                    //Run Script
                    DynValue result = script.Call(script.Globals[funcName], args2);
                    List<LuaParam> lparams = LuaUtils.CreateLuaParamList(result);
                    return lparams;
                }
                else
                {
                    if (!optional)
                        SendError(player, String.Format("Could not find function '{0}' for actor {1}.", funcName, target.GetName()));
                }
            }
            else
            {
                if (!optional)
                    SendError(player, String.Format("Could not find script for actor {0}.", target.GetName()));
            }
            return null;
        }

        public List<LuaParam> CallLuaFunctionForReturn(string path, string funcName, bool optional, params object[] args)
        {
            string luaPath = path;
            LuaScript script = LoadScript(luaPath);
            if (script != null)
            {
                if (!script.Globals.Get(funcName).IsNil())
                {
                    //Run Script
                    DynValue result = script.Call(script.Globals[funcName], args);
                    List<LuaParam> lparams = LuaUtils.CreateLuaParamList(result);
                    return lparams;
                }
            }
            return null;
        }

        public void CallLuaFunction(Player player, Actor target, string funcName, bool optional, params object[] args)
        {
            //Need a seperate case for NPCs cause that child/parent thing.
            if (target is Npc)
            {
                CallLuaFunctionNpc(player, (Npc)target, funcName, optional, args);
                return;
            }

            object[] args2 = new object[args.Length + 2];
            Array.Copy(args, 0, args2, 2, args.Length);
            args2[0] = player;
            args2[1] = target;

            string luaPath = GetScriptPath(target);
            LuaScript script = LoadScript(luaPath);
            if (script != null)
            {
                if (!script.Globals.Get(funcName).IsNil())
                {
                    try
                    {
                        Coroutine coroutine = script.CreateCoroutine(script.Globals[funcName]).Coroutine;
                        DynValue value = coroutine.Resume(args2);
                        ResolveResume(player, coroutine, value);
                    }
                    catch(Exception e)
                    {
                        Program.Log.Error("Lua function failed: player={0} actor={1} func={2} path={3}: {4}",
                            player != null ? player.customDisplayName : "(none)",
                            target.GetName(),
                            funcName,
                            luaPath,
                            e.Message);
                        player.SendMessage(0x20, "", e.Message);
                        player.EndEvent();

                    }
                }
                else
                {
                    if (!optional)
                        SendError(player, String.Format("Could not find function '{0}' for actor {1}.", funcName, target.GetName()));
                }
            }
            else
            {
                if (!(target is Area) && !optional)
                    SendError(player, String.Format("Could not find script for actor {0}.", target.GetName()));
            }
        }

        public void EventStarted(Player player, Actor target, EventStartPacket eventStart)
        {
            List<LuaParam> lparams = new List<LuaParam>();
            lparams.AddRange(eventStart.luaParams);
            lparams.Insert(0, new LuaParam(2, eventStart.eventName));
            if (mSleepingOnPlayerEvent.ContainsKey(player.actorId))
            {
                Coroutine coroutine = mSleepingOnPlayerEvent[player.actorId];
                mSleepingOnPlayerEvent.Remove(player.actorId);

                try
                {
                    DevDiagnostics.Trace(
                        "lua.resume",
                        "player", player.customDisplayName,
                        "actor", String.Format("0x{0:X}", player.actorId),
                        "source", "event.start",
                        "eventName", eventStart.eventName,
                        "coroutine", coroutine.GetHashCode());
                    DynValue value = coroutine.Resume();
                    ResolveResume(null, coroutine, value);
                }
                catch (ScriptRuntimeException e)
                {
                    LuaEngine.SendError(player, String.Format("OnEventStarted: {0}", e.DecoratedMessage));
                    player.EndEvent();
                }
            }
            else
            {
                DevDiagnostics.Trace(
                    "lua.dispatch",
                    "player", player.customDisplayName,
                    "actor", String.Format("0x{0:X}", player.actorId),
                    "target", target.GetName(),
                    "targetActor", String.Format("0x{0:X}", target.actorId),
                    "eventName", eventStart.eventName,
                    "isDirector", target is Director);

                if (target is Director)
                    ((Director)target).OnEventStart(player, LuaUtils.CreateLuaParamObjectList(lparams));
                else
                    CallLuaFunction(player, target, "onEventStarted", false, LuaUtils.CreateLuaParamObjectList(lparams));
            }
        }

        public DynValue ResolveResume(Player player, Coroutine coroutine, DynValue value)
        {
            if (value == null || value.IsVoid())
                return value;

            if (player != null && value.String != null && value.String.Equals("_WAIT_EVENT"))
            {
                DevDiagnostics.Trace(
                    "lua.wait",
                    "player", player.customDisplayName,
                    "actor", String.Format("0x{0:X}", player.actorId),
                    "waitType", "_WAIT_EVENT",
                    "coroutine", coroutine.GetHashCode());
                GetInstance().AddWaitEventCoroutine(player, coroutine);
            }
            else if (value.Tuple != null && value.Tuple.Length >= 1 && value.Tuple[0].String != null)
            {
                switch (value.Tuple[0].String)
                {
                    case "_WAIT_TIME":
                        DevDiagnostics.Trace(
                            "lua.wait",
                            "player", player == null ? "(none)" : player.customDisplayName,
                            "waitType", "_WAIT_TIME",
                            "seconds", value.Tuple.Length > 1 ? value.Tuple[1].Number : 0,
                            "coroutine", coroutine.GetHashCode());
                        GetInstance().AddWaitCoroutine(coroutine, (float)value.Tuple[1].Number);
                        break;
                    case "_WAIT_SIGNAL":
                        DevDiagnostics.Trace(
                            "lua.wait",
                            "player", player == null ? "(none)" : player.customDisplayName,
                            "waitType", "_WAIT_SIGNAL",
                            "signal", value.Tuple.Length > 1 ? value.Tuple[1].String : "",
                            "coroutine", coroutine.GetHashCode());
                        GetInstance().AddWaitSignalCoroutine(coroutine, (string)value.Tuple[1].String);
                        break;
                    case "_WAIT_EVENT":
                        Player waitingPlayer = (Player)value.Tuple[1].UserData.Object;
                        DevDiagnostics.Trace(
                            "lua.wait",
                            "player", waitingPlayer.customDisplayName,
                            "actor", String.Format("0x{0:X}", waitingPlayer.actorId),
                            "waitType", "_WAIT_EVENT",
                            "coroutine", coroutine.GetHashCode());
                        GetInstance().AddWaitEventCoroutine(waitingPlayer, coroutine);
                        break;
                    default:
                        return value;
                }
            }

            return value;
        }

        #region RunGMCommand
        public static void RunGMCommand(Player player, String cmd, string[] param, bool help = false)
        {
            bool playerNull = player == null;

            if (playerNull)
            {
                if (param.Length >= 2 && param[1].Contains("\""))
                    player = Server.GetWorldManager().GetPCInWorld(param[1]);
                else if (param.Length > 2)
                    player = Server.GetWorldManager().GetPCInWorld(param[1] + param[2]);
            }

            if (playerNull && param.Length >= 3)
                player = Server.GetWorldManager().GetPCInWorld(param[1] + " " + param[2]);
            
            // load from scripts/commands/gm/ directory
            var path = String.Format("./scripts/commands/gm/{0}.lua", cmd.ToLower());

            // check if the file exists
            if (File.Exists(path))
            {
                // load global functions
                LuaScript script = LoadGlobals();

                // see if this script has any syntax errors
                try
                {
                    script.DoFile(path);
                }
                catch (Exception e)
                {
                    Program.Log.Error("LuaEngine.RunGMCommand: {0}.", e.Message);
                    return;
                }

                // can we run this script
                if (!script.Globals.Get("onTrigger").IsNil())
                {
                    // can i run this command
                    var permissions = 0;

                    // parameter types (string, integer, double, float)
                    var parameters = "";
                    var description = "!" + cmd + ": ";

                    // get the properties table
                    var res = script.Globals.Get("properties");

                    // make sure properties table exists
                    if (!res.IsNil())
                    {
                        try
                        {
                            // returns table if one is found
                            var table = res.Table;

                            // find each key/value pair
                            foreach (var pair in table.Pairs)
                            {
                                if (pair.Key.String == "permissions")
                                {
                                    permissions = (int)pair.Value.Number;
                                }
                                else if (pair.Key.String == "parameters")
                                {
                                    parameters = pair.Value.String;
                                }
                                else if (pair.Key.String == "description")
                                {
                                    description = pair.Value.String;
                                }
                            }
                        }
                        catch (Exception e) { LuaScript.Log.Error("LuaEngine.RunGMCommand: " + e.Message); return; }
                    }

                    // if this isnt a console command, make sure player exists
                    if (player != null)
                    {
                        if (permissions > 0 && !player.isGM)
                        {
                            Program.Log.Info("LuaEngine.RunGMCommand: {0}'s GM level is too low to use command {1}.", player.actorName, cmd);
                            return;
                        }
                        // i hate to do this, but cant think of a better way to keep !help
                        else if (help)
                        {
                            player.SendMessage(SendMessagePacket.MESSAGE_TYPE_SYSTEM_ERROR, String.Format("[Commands] [{0}]", cmd), description);
                            return;
                        }
                    }
                    else if (help)
                    {
                        LuaScript.Log.Info("[Commands] [{0}]: {1}", cmd, description);
                        return;
                    }

                    // we'll push our lua params here
                    List<object> LuaParam = new List<object>();

                    var i = playerNull ? 2 : 0;
                    for (; i < parameters.Length; ++i)
                    {
                        try
                        {
                            // convert chat parameters to command parameters
                            switch (parameters[i])
                            {
                                case 'i':
                                    LuaParam.Add(Convert.ChangeType(param[i + 1], typeof(int)));
                                    continue;
                                case 'd':
                                    LuaParam.Add(Convert.ChangeType(param[i + 1], typeof(double)));
                                    continue;
                                case 'f':
                                    LuaParam.Add(Convert.ChangeType(param[i + 1], typeof(float)));
                                    continue;
                                case 's':
                                    LuaParam.Add(param[i + 1]);
                                    continue;
                                default:
                                    LuaScript.Log.Info("LuaEngine.RunGMCommand: {0} unknown parameter {1}.", path, parameters[i]);
                                    LuaParam.Add(param[i + 1]);
                                    continue;
                            }
                        }
                        catch (Exception e)
                        {
                            if (e is IndexOutOfRangeException) break;
                            LuaParam.Add(param[i + 1]);
                        }
                    }

                    // the script can double check the player exists, we'll push them anyways
                    LuaParam.Insert(0, player);
                    // push the arg count too
                    LuaParam.Insert(1, i - (playerNull ? 2 : 0));

                    // run the script                    
                    //script.Call(script.Globals["onTrigger"], LuaParam.ToArray());

                    // gm commands dont need to be coroutines?
                    try
                    {
                        Coroutine coroutine = script.CreateCoroutine(script.Globals["onTrigger"]).Coroutine;
                        DynValue value = coroutine.Resume(LuaParam.ToArray());
                        GetInstance().ResolveResume(player, coroutine, value);
                    }
                    catch (Exception e)
                    {
                        Program.Log.Error("LuaEngine.RunGMCommand: {0} - {1}", path, e.Message);
                    }
                    return;
                }
            }
            LuaScript.Log.Error("LuaEngine.RunGMCommand: Unable to find script {0}", path);
            return;
        }
        #endregion

        public static LuaScript LoadScript(string path)
        {
            if (!File.Exists(path))
                return null;

            LuaScript script = LoadGlobals();

            try
            {
                script.DoFile(path);
            }
            catch (SyntaxErrorException e)
            {
                Program.Log.Error("{0}.", e.DecoratedMessage);
                return null;
            }
            return script;
        }

        public static LuaScript LoadGlobals(LuaScript script = null)
        {
            script = script ?? new LuaScript();

            // register and load all global functions here
            ((FileSystemScriptLoader)script.Options.ScriptLoader).ModulePaths = FileSystemScriptLoader.UnpackStringPaths("./scripts/?;./scripts/?.lua");
            script.Globals["GetWorldManager"] = (Func<WorldManager>)Server.GetWorldManager;
            script.Globals["GetStaticActor"] = (Func<string, Actor>)Server.GetStaticActors;
            script.Globals["GetStaticActorById"] = (Func<uint, Actor>)Server.GetStaticActors;
            script.Globals["GetWorldMaster"] = (Func<Actor>)Server.GetWorldManager().GetActor;
            script.Globals["GetItemGamedata"] = (Func<uint, ItemData>)Server.GetItemGamedata;
            script.Globals["GetGuildleveGamedata"] = (Func<uint, GuildleveData>)Server.GetGuildleveGamedata;
            script.Globals["GetLuaInstance"] = (Func<LuaEngine>)LuaEngine.GetInstance;

            script.Options.DebugPrint = s => { Program.Log.Debug(s); };
            return script;
        }

        public static void SendError(Player player, string message)
        {
            message = "[LuaError] " + message;
            if (player == null)
                return;
            List<SubPacket> SendError = new List<SubPacket>();
            player.SendMessage(SendMessagePacket.MESSAGE_TYPE_SYSTEM_ERROR, "", message);
            player.QueuePacket(EndEventPacket.BuildPacket(player.actorId, player.currentEventOwner, player.currentEventName, 0));
        }

    }
}
