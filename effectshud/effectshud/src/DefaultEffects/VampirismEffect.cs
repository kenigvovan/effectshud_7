using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace effectshud.src.DefaultEffects
{
    /// <summary>Heals the bearer for a share of the damage it deals: damage * percentHealPerDamage * tier.</summary>
    [EffectRegistration(EffectTypeIds.Vampirism)]
    public class VampirismEffect: Effect
    {
        public float percentHealPerDamage = 0.10f;
        public VampirismEffect()
        {
        }
        public VampirismEffect(int minutes = 1, float percentHealPerDamage = 0.10f, int tier = 1, bool infinite = false) : base(tier, infinite)
        {
            SetExpiryInRealMinutes(minutes);
            this.percentHealPerDamage = percentHealPerDamage;
        }

        /// <summary>Lifesteal in percent, shown on the character tab.</summary>
        public override float DisplayMagnitude() => percentHealPerDamage * tier * 100f;

        public override void OnDealtDamage(float damage, Entity target, DamageSource dmgSource)
        {
            if (entity == null || !entity.Alive) return;
            float heal = damage * percentHealPerDamage * tier;
            if (heal <= 0f) return;
            entity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Heal
            }, heal);
        }
    }
}
