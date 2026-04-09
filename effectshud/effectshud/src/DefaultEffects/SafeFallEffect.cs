using Vintagestory.API.Common;
using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.SafeFall)]
    public class SafeFallEffect: Effect
    {
        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if(dmgSource.Type == EnumDamageType.Gravity)
            {
                damage = 0;
            }
        }
    }
}
