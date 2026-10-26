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
            SetMaxOxygen(tier + 1);
        }
        public override void OnStack(Effect otherEffect)
        {
            base.OnStack(otherEffect);
            SetMaxOxygen(tier + 1);
        }
        public override void OnExpire()
        {
            SetMaxOxygen(1);
        }

        // Always derived from the world's base lung capacity: multiplying the current MaxOxygen compounded the
        // bonus on every tier upgrade.
        private void SetMaxOxygen(int multiplier)
        {
            EntityBehaviorBreathe ebb = this.entity.GetBehavior<EntityBehaviorBreathe>();
            if (ebb == null) return;
            ebb.MaxOxygen = (float)this.entity.World.Config.GetAsInt("lungCapacity", 40000) * multiplier;
        }
    }
}
