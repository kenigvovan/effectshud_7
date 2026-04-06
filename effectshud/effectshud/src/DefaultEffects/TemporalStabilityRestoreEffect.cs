using Vintagestory.GameContent;

namespace effectshud.src.DefaultEffects
{
    public class TemporalStabilityRestoreEffect: Effect
    {
        public TemporalStabilityRestoreEffect()
        {
            effectTypeId = "temporalstabilityrestore";
        }
        public TemporalStabilityRestoreEffect(int tier = 1)
        {
            this.effectTypeId = "temporalstabilityrestore";
            this.tier = tier;
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
