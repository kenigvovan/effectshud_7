namespace effectshud.src.DefaultEffects
{
    public class WalkSlowEffect : Effect
    {
        public float statChangeValue = -0.25f;
        public WalkSlowEffect()
        {
            effectTypeId = "walkslow";
        }
        public WalkSlowEffect(int tier = 1, float statChangeValue = -0.25f, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInRealMinutes(1 * tier);

            effectTypeId = "walkslow";
        }
        public override void OnStart()
        {
            entity.Stats.Set("walkspeed", "effectshudwalkslow", statChangeValue * tier);
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
            entity.Stats.Set("walkspeed", "effectshudwalkslow", statChangeValue * tier);
            this.ExpireTick = otherEffect.ExpireTick;
            this.TickCounter = otherEffect.TickCounter;
        }
        public override void OnExpire()
        {
            entity.Stats.Set("walkspeed", "effectshudwalkslow", 0);
        }
        public override bool OnDeath()
        {
            entity.Stats.Set("walkspeed", "effectshudwalkslow", 0);
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
