namespace AetherXIV.Core.Map.packets.send.group
{
    class GroupMember
    {
        public uint actorId;
        public int localizedName;
        public uint unknown2;
        public bool flag1;
        public bool isOnline;
        public string name;

        public GroupMember(uint actorId, int localizedName, uint unknown2, bool flag1, bool isOnline, string name)
        {
            this.actorId = actorId;
            this.localizedName = localizedName;
            this.unknown2 = unknown2;
            this.flag1 = flag1;
            this.isOnline = isOnline;
            this.name = name == null ? "" : name;
        }

        public static GroupMember ForActor(
            uint actorId,
            uint displayNameId,
            string customDisplayName,
            bool isRecipient,
            bool isPlayer)
        {
            return new GroupMember(
                actorId,
                isPlayer ? -1 : unchecked((int)displayNameId),
                0,
                !isRecipient,
                true,
                isPlayer ? customDisplayName : "");
        }
    }
}
