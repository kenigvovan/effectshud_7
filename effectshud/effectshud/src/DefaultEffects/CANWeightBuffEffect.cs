using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.CANWeightBuff)]
    public class CANWeightBuffEffect: Effect
    {
        public float statChangeValue = 1000;
        public CANWeightBuffEffect()
        {
        }
        public CANWeightBuffEffect(int minutes = 1, float statChangeValue = 1000, int tier = 1, bool infinite = false) : base(tier, infinite)
        {
            this.statChangeValue = statChangeValue;
            SetExpiryInRealMinutes(minutes);
        }
        public override void OnStart()
        {
            entity.Stats.Set("weightmodweightbonus", "effectshudweightmodweightbonus", statChangeValue * tier);
        }

        public override void OnExpire()
        {
            entity.Stats.Set("weightmodweightbonus", "effectshudweightmodweightbonus", 0);
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
            entity.Stats.Set("weightmodweightbonus", "effectshudweightmodweightbonus", statChangeValue * tier);
            this.ExpireTick = otherEffect.ExpireTick;
            this.TickCounter = otherEffect.TickCounter;
        }
    }
}
