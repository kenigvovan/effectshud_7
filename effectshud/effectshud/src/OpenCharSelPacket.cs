using ProtoBuf;

namespace effectshud.src
{
    /// <summary>
    /// Server -> client signal telling the owning player's client to open the
    /// vanilla character creation/selection dialog right now (used by the Forgetting effect).
    /// </summary>
    [ProtoContract]
    public class OpenCharSelPacket
    {
    }
}
