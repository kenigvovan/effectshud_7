using ProtoBuf;

namespace effectshud.src
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class EffectClientData
    {
        public string typeId;
        public double duration;
        public int tier;
        public bool infinite;
        public bool positive;
    }
}
