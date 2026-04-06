using Vintagestory.API.Common;

namespace effectshud.src.DefaultEffects
{
    public class FireDamageImmuneEffect: Effect
    {
        public FireDamageImmuneEffect()
        {
            this.effectTypeId = "firedamageimmune";
        }
        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if(dmgSource.Type == EnumDamageType.Fire)
            {
                damage = 0;
            }
        }
    }
}
