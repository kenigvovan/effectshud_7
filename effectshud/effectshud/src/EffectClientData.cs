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
        // Optional numeric magnitude a consumer mod can surface in its own UI (e.g. damage/tick, DR fraction).
        // 0 = "no meaningful number" (the default for effects that don't override Effect.DisplayMagnitude).
        public float magnitude;
    }
}
