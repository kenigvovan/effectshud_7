using Vintagestory.API.Common;

namespace effectshud.src.DefaultEffects
{
    public class SafeFallEffect: Effect
    {

        public SafeFallEffect()
        {
            effectTypeId = "safefall";
        }
        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if(dmgSource.Type == EnumDamageType.Gravity)
            {
                damage = 0;
            }
        }
    }
}
