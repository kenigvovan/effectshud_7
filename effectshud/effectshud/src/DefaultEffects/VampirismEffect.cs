using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.Vampirism)]
    public class VampirismEffect: Effect
    {
        public float percentHealPerDamage = 0.10f;
        public VampirismEffect()
        {
        }
        public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {
            var c = 2;
            //ource.
            //if(targetEntity != null)
        }
    }
}
