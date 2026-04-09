using Vintagestory.API.Common;
using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.FireDamageImmune)]
    public class FireDamageImmuneEffect: Effect
    {
        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if(dmgSource.Type == EnumDamageType.Fire)
            {
                damage = 0;
            }
        }
    }
}
