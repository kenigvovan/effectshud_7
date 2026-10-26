using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.MiningSpeed)]
    public class MiningSpeedEffect : Effect
    {
        public float statChangeValue = 0.25f;
        public MiningSpeedEffect()
        {
        }
        public MiningSpeedEffect(int tier = 1, float statChangeValue = 0.25f, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInRealMinutes(1 * tier);
            this.statChangeValue = statChangeValue;
        }
        public override void OnStart()
        {
            entity.Stats.Set("miningSpeedMul", "effectshudminingspeed", statChangeValue * tier);
        }
        public override void OnStack(Effect otherEffect)
        {
            base.OnStack(otherEffect);
            entity.Stats.Set("miningSpeedMul", "effectshudminingspeed", statChangeValue * tier);
        }
        public override void OnExpire()
        {
            entity.Stats.Set("miningSpeedMul", "effectshudminingspeed", 0);
        }
    }
}
