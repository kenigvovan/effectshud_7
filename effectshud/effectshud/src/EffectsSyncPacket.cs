using System.Collections.Generic;
using ProtoBuf;

namespace effectshud.src
{
    [ProtoContract]
    public class EffectsSyncPacket
    {
        [ProtoMember(1)]
        public string currentEffectsData;
        [ProtoMember(2)]
        public HashSet<string> typeIdsToRemove;
        [ProtoMember(3)]
        public string playerUID;
    }
}
