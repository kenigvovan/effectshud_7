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
            entity.Stats.Set("miningSpeedMul", "effectshudminingslow", statChangeValue * tier, true);
            this.ExpireTick = otherEffect.ExpireTick;
            this.TickCounter = otherEffect.TickCounter;
        }
        public override void OnExpire()
        {
            entity.Stats.Set("miningSpeedMul", "effectshudminingslow", 0);
        }
        public override bool OnDeath()
        {
            entity.Stats.Set("miningSpeedMul", "effectshudminingslow", 0);
            EBEffectsAffected ebea = entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null)
            {
                return false;
            }
            if (this.removedAfterDeath)
            {
                ebea.activeEffects.Remove(this.effectTypeId);
                ebea.needUpdate = true;
                return true;
            }
            return false;
        }
    }
}
