using Vintagestory.API.Common.Entities;

namespace effectshud.src
{
    public static class EntityExtentions
    {
        public static bool EntityHasEffect(this Entity entity, string effectId)
        {
            EBEffectsAffected ebea = entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null)
            {
                return false;
            }
            return ebea.HasEffect(effectId);
        }

        public static bool TryGetEntityEffect(this Entity entity, string effectId, out Effect effect)
        {
            EBEffectsAffected ebea = entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null)
            {
                effect = null;
                return false;
            }
            return ebea.TryGetEffect(effectId, out effect);
        }
    }
}
