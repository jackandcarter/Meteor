using System;

namespace AetherXIV.Core.Lobby.DataObjects
{ 
    class Character
    {
        public uint id;
        public ushort slot;
        public ushort serverId;
        public string name;
        public ushort state;
        public string charaInfo;
        public bool isLegacy;
        public bool doRename;
        public uint currentZoneId;

        public byte guardian;
        public byte birthMonth;
        public byte birthDay;

        public uint currentClass = 3;
        public uint currentJob = 0;
        public int currentLevel = 1;

        public byte initialTown;
        public byte tribe;

        public static CharaInfo EncodedToCharacter(String charaInfo)
        {
            charaInfo.Replace("+", "-");
            charaInfo.Replace("/", "_");
            byte[] data = System.Convert.FromBase64String(charaInfo);            

            Program.Log.Debug("------------Base64 printout------------------");
            Program.Log.Debug(Common.Utils.ByteArrayToHex(data));
            Program.Log.Debug("------------Base64 printout------------------");

            CharaInfo chara = new CharaInfo();

            return chara;
        }
    }
}
