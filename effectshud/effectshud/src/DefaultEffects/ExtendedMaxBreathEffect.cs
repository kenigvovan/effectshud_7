using Vintagestory.GameContent;
using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.ExtendedMaxBreath)]
    public class ExtendedMaxBreathEffect: Effect
    {
        public float hpPerTick = 0.05f;
        public ExtendedMaxBreathEffect()
        {
        }
        public ExtendedMaxBreathEffect(int ticks = 20, int tier = 1, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInTicks(ticks);
        }
        public override void OnStart()
        {
            if (this.entity.HasBehavior<EntityBehaviorBreathe>())
            {
                EntityBehaviorBreathe ebb = this.entity.GetBehavior<EntityBehaviorBreathe>();
                ebb.MaxOxygen = ebb.MaxOxygen * (tier + 1);
            }
        }
        public override void OnStack(Effect otherEffect)
        {
            if (this.tier > otherEffect.Tier)
            {
                return;
            }
            if (this.tier == otherEffect.Tier)
            {
                this.ExpireTick = otherEffect.ExpireTick;
                this.TickCounter = otherEffect.TickCounter;
                return;
            }
            this.tier = otherEffect.Tier;
            if (this.entity.HasBehavior<EntityBehaviorBreathe>())
            {
                EntityBehaviorBreathe ebb = this.entity.GetBehavior<EntityBehaviorBreathe>();
                ebb.MaxOxygen = ebb.MaxOxygen * (tier + 1);
            }
            this.ExpireTick = otherEffect.ExpireTick;
            this.TickCounter = otherEffect.TickCounter;
        }
        public override void OnExpire()
        {
            if (this.entity.HasBehavior<EntityBehaviorBreathe>())
            {
                EntityBehaviorBreathe ebb = this.entity.GetBehavior<EntityBehaviorBreathe>();
                ebb.MaxOxygen = (float)this.entity.World.Config.GetAsInt("lungCapacity", 40000);
            }
        }
    }
}
