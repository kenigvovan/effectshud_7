using ProtoBuf;

namespace effectshud.src
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SerializedEffect
    {
        public string typeId;
        public string data;
    }
}
