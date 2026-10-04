using Vintagestory.GameContent;
using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.TemporalStabilityRestore, shouldBeRendered: false)]
    public class TemporalStabilityRestoreEffect: Effect
    {
        public TemporalStabilityRestoreEffect()
        {
        }
        public TemporalStabilityRestoreEffect(int tier = 1) : base(tier)
        {
        }
        public override void OnStart()
        {
            var ebtsa = this.entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
            if(ebtsa == null)
            {
                return;
            }
            if((ebtsa.OwnStability + tier * 0.33) >= 1)
            {
                ebtsa.OwnStability = 1;
            }
            else
            {
                ebtsa.OwnStability += tier * 0.33;
            }
        }
    }
}
