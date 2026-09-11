using Vintagestory.API.Common;
using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.Thorns)]
    public class ThornsEffect : Effect
    {
        public float thornDamage = 0.1f;
        public ThornsEffect()
        {
        }
        public ThornsEffect(int secondsDuration = 60, float hpPerAttack = 0.09f, int tier = 1, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInRealSeconds(secondsDuration);
            this.thornDamage = hpPerAttack * tier;
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
    }
}

