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
            // Store the BASE value; tier is applied once in OnTick. Multiplying here too made healing scale
            // with tier squared (0.08 * tier in the ctor, then * tier again per tick).
            this.hpPerTick = hpPerTick;
        }
        public override void OnTick()
        {
            entity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Heal
            }, hpPerTick * tier);
        }

    }
}
