using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.NightVision)]
    public class NightVisionEffect: Effect
    {
        public NightVisionEffect()
        {
        }
        public NightVisionEffect(int ticks = 20, float hpPerTick = 0.08f, int tier = 1, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInRealMinutes(tier);
        }
        public override void OnStack(Effect otherEffect)
        {
            base.OnStack(otherEffect);
        }
    }
}
