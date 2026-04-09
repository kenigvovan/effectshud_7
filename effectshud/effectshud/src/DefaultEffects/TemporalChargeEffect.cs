using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.CanTemporalCharge)]
    public class TemporalChargeEffect: Effect
    {
        public float statChangeValue = 0.15f;
        public TemporalChargeEffect()
        {
        }
        public TemporalChargeEffect(int minutes = 1, float statChangeValue = 0.1f, int tier = 1, bool infinite = false) : base(tier, infinite)
        {
            this.statChangeValue = statChangeValue;
            SetExpiryInRealMinutes(minutes);
        }
        public override void OnStart()
        {
            entity.Stats.Set("cantemporalcharge", "effectshudtemporalcharge", statChangeValue * tier);
        }

        public override void OnExpire()
        {
            entity.Stats.Set("cantemporalcharge", "effectshudtemporalcharge", 0);
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
            entity.Stats.Set("cantemporalcharge", "effectshudtemporalcharge", statChangeValue * tier);
            this.ExpireTick = otherEffect.ExpireTick;
            this.TickCounter = otherEffect.TickCounter;
        }
    }
}
