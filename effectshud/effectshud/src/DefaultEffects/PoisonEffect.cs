using Vintagestory.API.Common;
using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.Poison, positive: false)]
    public class PoisonEffect : Effect
    {
        // Base damage per tick; total = hpPerTick * tier, applied at use time (OnTick). Never pre-multiply by tier
        // here: a mutating Tier setter re-scaled hpPerTick on every deserialize (poison grew stronger each save/load)
        // and OnStack writes the tier field directly, which bypassed any setter-based rescale.
        public float hpPerTick = 0.05f;
        public PoisonEffect()
        {
        }
        public PoisonEffect(int ticks = 20, float hpPerTick = 0.05f, int tier = 1, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInTicks(ticks);
            this.hpPerTick = hpPerTick;
        }
        public override void OnTick()
        {
            entity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Poison
            }, hpPerTick * tier);
        }
    }
}
