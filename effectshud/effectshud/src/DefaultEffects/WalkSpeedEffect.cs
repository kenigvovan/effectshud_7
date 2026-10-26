using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.WalkSpeed)]
    public class WalkSpeedEffect: Effect
    {
        public float statChangeValue = 0.25f;
        public WalkSpeedEffect()
        {
        }
        public WalkSpeedEffect(int tier = 1, float statChangeValue = 0.25f, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInRealMinutes(1 * tier);
            this.statChangeValue = statChangeValue;
        }
        public override void OnStart()
        {
            entity.Stats.Set("walkspeed", "effectshudwalkspeed", statChangeValue * tier);
        }
        public override void OnStack(Effect otherEffect)
        {
            base.OnStack(otherEffect);
            entity.Stats.Set("walkspeed", "effectshudwalkspeed", statChangeValue * tier);
        }
        public override void OnExpire()
        {
            entity.Stats.Set("walkspeed", "effectshudwalkspeed", 0f);
        }
    }
}
