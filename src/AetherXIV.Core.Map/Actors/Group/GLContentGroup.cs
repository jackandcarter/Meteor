using AetherXIV.Core.Map.actors.director;

namespace AetherXIV.Core.Map.actors.group
{
    class GLContentGroup : ContentGroup
    {
        public GLContentGroup(ulong groupIndex, Director director, uint[] initialMembers)
            : base(groupIndex, director, initialMembers)
        {
        }

        public override uint GetTypeId()
        {
            return Group.ContentGroup_GuildleveGroup;
        }
    }
}
