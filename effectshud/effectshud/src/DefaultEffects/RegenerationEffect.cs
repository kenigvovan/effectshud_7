using Vintagestory.API.Common;
using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.Regeneration)]
    public class RegenerationEffect: Effect
    {
        public float hpPerTick = 0.1f;
        public RegenerationEffect()
        {
        }
        public RegenerationEffect(int ticks = 20, float hpPerTick = 0.08f, int tier = 1, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInTicks(ticks);
            this.hpPerTick = hpPerTick * tier;
        }
        public override void OnTick()
        {           
            entity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Heal
            }, hpPerTick * tier);
        }
        public override void OnStack(Effect otherEffect)
        {
            base.OnStack(otherEffect);
        }
       
    }
}
