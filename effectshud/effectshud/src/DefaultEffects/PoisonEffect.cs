using Vintagestory.API.Common;
using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.Poison, positive: false)]
    public class PoisonEffect : Effect
    {
        public float hpPerTick = 0.05f;
        public override int Tier
        {
            get => tier;
            set
            {
                hpPerTick = hpPerTick * value;
                tier = value;
            }
        }
        public PoisonEffect()
        {
        }
        public PoisonEffect(int ticks = 20, float hpPerTick = 0.05f, int tier = 1, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInTicks(ticks);
            this.hpPerTick = hpPerTick * tier;
        }
        public override void OnTick()
        {
            entity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Poison
            }, hpPerTick);
        }
        public override void OnStack(Effect otherEffect)
        {
            base.OnStack(otherEffect);
        }
    }
}
