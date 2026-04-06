namespace effectshud.src.DefaultEffects
{
    public class NightVisionEffect: Effect
    {
        public NightVisionEffect()
        {
            effectTypeId = "nightvision";
        }
        public NightVisionEffect(int ticks = 20, float hpPerTick = 0.08f, int tier = 1, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInRealMinutes(tier);
            effectTypeId = "nightvision";
        }
        public override void OnStack(Effect otherEffect)
        {
            base.OnStack(otherEffect);
        }
    }
}
