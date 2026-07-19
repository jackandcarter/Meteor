using System;
using System.Collections.Generic;
using System.Linq;
using AetherXIV.Core.Map.dataobjects;

using System.IO;
using AetherXIV.Core.Map.packets.send;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.Actors;

namespace AetherXIV.Core.Map
{
    class CommandProcessor
    {
        private static Dictionary<uint, ItemData> gamedataItems = Server.GetGamedataItems();
        private readonly Dictionary<uint, SpawnPinPromptState> spawnPinPrompts = new Dictionary<uint, SpawnPinPromptState>();

        const UInt32 ITEM_GIL = 1000001;

        private enum SpawnPinPromptStep
        {
            EnemyName,
            SourceNote
        }

        private class SpawnPinPromptState
        {
            public SpawnPinPromptStep Step;
            public string EnemyName;
        }
      
        /// <summary>
        /// We only use the default options for SendMessagePacket.
        /// May as well make it less unwieldly to view
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        private void SendMessage(Session session, String message)
        {
            if (session != null)
                session.GetActor().QueuePacket(SendMessagePacket.BuildPacket(session.id, SendMessagePacket.MESSAGE_TYPE_GENERAL_INFO, "", message));
        }

        private void SendPinSpawnMessage(Player player, String message)
        {
            if (player != null)
                player.SendMessage(SendMessagePacket.MESSAGE_TYPE_SYSTEM_ERROR, "[pinspawn] ", message);
            else
                Program.Log.Info(String.Format("[pinspawn] {0}", message));
        }

        internal bool HandleChatInput(string input, Session session)
        {
            if (String.IsNullOrEmpty(input))
                return false;

            if (input.StartsWith("!"))
                return DoCommand(input, session);

            if (session != null && spawnPinPrompts.ContainsKey(session.id))
                return ContinuePinSpawnPrompt(input, session);

            return false;
        }

        internal bool DoCommand(string input, Session session)
        {
            if (!input.Any() || input.Equals("") || input.Length == 1)
                return false;

            input = input.Trim();
            input = input.StartsWith("!") ? input.Substring(1) : input;

            var split = input.Split('"')
                     .Select((str, index) => index % 2 == 0
                                           ? str.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                           : new String[] { str }
                             )
                     .SelectMany(str => str).ToArray();

            split = split.ToArray(); // Ignore case on commands

            if (split.Length > 0)
            {
                var cmd = split[0];

                // if client isnt null, take player to be the player actor
                Player player = null;
                if (session != null)
                    player = session.GetActor();

                if (cmd.Equals("help"))
                {
                    // if there's another string after this, take it as the command we want the description for
                    if (split.Length > 1)
                    {
                        LuaEngine.RunGMCommand(player, split[1], null, true);
                        return true;
                    }

                    // print out all commands
                    foreach (var str in Directory.GetFiles("./scripts/commands/gm/"))
                    {
                        var c = str.Replace(".lua", "");
                        c = c.Replace("./scripts/commands/gm/", "");

                        LuaEngine.RunGMCommand(player, c, null, true);
                    }
                    return true;
                }

                if (cmd.Equals("pinspawn", StringComparison.OrdinalIgnoreCase))
                {
                    HandlePinSpawnCommand(split, session, player);
                    return true;
                }

                LuaEngine.RunGMCommand(player, cmd.ToString(), split.ToArray());
                return true;
            }
            // Debug
            //SendMessage(client, string.Join(",", split));

            if (split.Length >= 1)
            {
            
                // TODO: reloadzones

            #region !reloaditems
            if (split[0].Equals("reloaditems"))
                {
                    Program.Log.Info(String.Format("Got request to reload item gamedata"));
                    SendMessage(session, "Reloading Item Gamedata...");
                    gamedataItems.Clear();
                    gamedataItems = Database.GetItemGamedata();
                    Program.Log.Info(String.Format("Loaded {0} items.", gamedataItems.Count));
                    SendMessage(session, String.Format("Loaded {0} items.", gamedataItems.Count));
                    return true;
                }
                #endregion

                #region !property
                else if (split[0].Equals("property"))
                {
                    if (split.Length == 4)
                    {
                       // ChangeProperty(Utils.MurmurHash2(split[1], 0), Convert.ToUInt32(split[2], 16), split[3]);
                    }
                    return true;
                }
                #endregion

                #region !property2
                else if (split[0].Equals("property2"))
                {
                    if (split.Length == 4)
                    {
                        //ChangeProperty(Convert.ToUInt32(split[1], 16), Convert.ToUInt32(split[2], 16), split[3]);
                    }
                    return true;
                }
                #endregion
            }
            return false;
        }

        private void HandlePinSpawnCommand(string[] split, Session session, Player player)
        {
            if (player == null || session == null)
            {
                Program.Log.Info("[pinspawn] This command must be run by an in-game player.");
                return;
            }

            if (split.Length == 1)
            {
                spawnPinPrompts[session.id] = new SpawnPinPromptState { Step = SpawnPinPromptStep.EnemyName };
                SendPinSpawnMessage(player, "Enemy name? Type cancel to stop.");
                return;
            }

            string enemyName = split[1];
            string sourceNote = split.Length > 2 ? String.Join(" ", split.Skip(2).ToArray()) : "";
            SavePinSpawn(player, enemyName, sourceNote);
        }

        private bool ContinuePinSpawnPrompt(string input, Session session)
        {
            Player player = session.GetActor();
            string value = input == null ? "" : input.Trim();

            if (value.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                spawnPinPrompts.Remove(session.id);
                SendPinSpawnMessage(player, "Canceled.");
                return true;
            }

            SpawnPinPromptState prompt = spawnPinPrompts[session.id];

            if (prompt.Step == SpawnPinPromptStep.EnemyName)
            {
                if (String.IsNullOrEmpty(value))
                {
                    SendPinSpawnMessage(player, "Enemy name cannot be blank. Type cancel to stop.");
                    return true;
                }

                prompt.EnemyName = value;
                prompt.Step = SpawnPinPromptStep.SourceNote;
                SendPinSpawnMessage(player, "Source note? Type skip for blank, or cancel to stop.");
                return true;
            }

            string sourceNote = value.Equals("skip", StringComparison.OrdinalIgnoreCase) ? "" : value;
            spawnPinPrompts.Remove(session.id);
            SavePinSpawn(player, prompt.EnemyName, sourceNote);
            return true;
        }

        private void SavePinSpawn(Player player, string enemyName, string sourceNote)
        {
            if (String.IsNullOrWhiteSpace(enemyName))
            {
                SendPinSpawnMessage(player, "Enemy name cannot be blank.");
                return;
            }

            uint pinId = Database.SaveBattleNpcSpawnAuditPin(player, enemyName, sourceNote);
            if (pinId == 0)
            {
                SendPinSpawnMessage(player, "Could not save pin. Is the spawn audit migration applied?");
                return;
            }

            SendPinSpawnMessage(
                player,
                String.Format(
                    "Saved provisional pin #{0}: {1} at Zone:{2} X:{3:0.000} Y:{4:0.000} Z:{5:0.000} Rot:{6:0.000}",
                    pinId,
                    enemyName.Trim(),
                    player.zoneId,
                    player.positionX,
                    player.positionY,
                    player.positionZ,
                    player.rotation));
        }
    }

}
