﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace effectshud.src.DefaultEffects
{
    public class ThornsEffect : Effect
    {
        public float thornDamage = 0.1f;
        public ThornsEffect()
        {
            effectTypeId = "thorns";
        }
        public ThornsEffect(int secondsDuration = 60, float hpPerAttack = 0.09f, int tier = 1, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInRealSeconds(secondsDuration);
            this.thornDamage = hpPerAttack * tier;
            effectTypeId = "thorns";
        }
        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            //add new damage type 
            if (dmgSource.SourceEntity != null)
            {
                if (dmgSource.Source != EnumDamageSource.Unknown && dmgSource.Type != EnumDamageType.PiercingAttack && dmgSource.Type != EnumDamageType.Heal)
                {
                    dmgSource.SourceEntity.ReceiveDamage(new DamageSource()
                    {
                        Source = EnumDamageSource.Unknown,
                        Type = EnumDamageType.PiercingAttack
                    }, thornDamage);
                }
            }
        }
        public override void OnDeath()
        {
            EBEffectsAffected ebea = entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null)
            {
                return;
            }
            ebea.activeEffects.Remove(this.effectTypeId);
        }
    }
}

