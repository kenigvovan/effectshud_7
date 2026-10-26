using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.MiningSlow, positive: false)]
    public class MiningSlowEffect : Effect
    {
        public float statChangeValue = -0.25f;
        public MiningSlowEffect()
        {
        }
        public MiningSlowEffect(int tier = 1, float statChangeValue = -0.25f, bool infinite = false):base(tier, infinite)
        {
            SetExpiryInRealMinutes(1);
            this.statChangeValue = statChangeValue;
        }
        public override void OnStart()
        {
            entity.Stats.Set("miningSpeedMul", "effectshudminingslow", statChangeValue * tier, true);
        }
        public override void OnStack(Effect otherEffect)
        {
            base.OnStack(otherEffect);
            entity.Stats.Set("miningSpeedMul", "effectshudminingslow", statChangeValue * tier, true);
        }
        public override void OnExpire()
        {
            entity.Stats.Set("miningSpeedMul", "effectshudminingslow", 0);
        }
    }
}
