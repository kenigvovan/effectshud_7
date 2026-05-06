using System.Collections.Generic;
using ProtoBuf;

namespace effectshud.src
{
    [ProtoContract]
    public class EffectsSyncPacket
    {
        [ProtoMember(1)]
        public string playerUID;

        /// <summary>Only the effects that were added or updated (delta).</summary>
        [ProtoMember(2)]
        public List<EffectClientData> effectsToAddOrUpdate;

        /// <summary>TypeIds of effects to remove (delta).</summary>
        [ProtoMember(3)]
        public HashSet<string> typeIdsToRemove;
    }
}
