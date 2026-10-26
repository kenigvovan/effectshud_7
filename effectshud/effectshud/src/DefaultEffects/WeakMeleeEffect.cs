using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.WeakMelee, positive: false)]
    public class WeakMeleeEffect : Effect
    {
        public float statChangeValue = -0.25f;
        public WeakMeleeEffect()
        {
        }
        public WeakMeleeEffect(int tier = 1, float statChangeValue = -0.25f, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInRealMinutes(1 * tier);
            this.statChangeValue = statChangeValue;
        }
        public override void OnStart()
        {
            entity.Stats.Set("meleeWeaponsDamage", "effectshudweakmelee", statChangeValue * tier);
        }
        public override void OnStack(Effect otherEffect)
        {
            base.OnStack(otherEffect);
            entity.Stats.Set("meleeWeaponsDamage", "effectshudweakmelee", statChangeValue * tier);
        }
        public override void OnExpire()
        {
            entity.Stats.Set("meleeWeaponsDamage", "effectshudweakmelee", 0);
        }
    }
}
