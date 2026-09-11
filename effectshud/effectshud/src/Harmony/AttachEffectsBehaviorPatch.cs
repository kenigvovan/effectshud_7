using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace effectshud.src
{
    /// <summary>Postfix on <see cref="Entity.Initialize"/> that attaches the <see cref="EBEffectsAffected"/>
    /// behavior at runtime to every living mob (any <see cref="EntityAgent"/> with a health behavior), so effects
    /// apply/tick/expire on mobs too — not just players (players still get it via the player.json patch).
    /// Server-only: in singleplayer the patch is process-global, so we must skip client-side entity copies.</summary>
    public class AttachEffectsBehaviorPatch
    {
        public static void Postfix_Initialize(Entity __instance)
        {
            var entity = __instance;
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (!(entity is EntityAgent) || entity is EntityPlayer) return;
            if (entity.GetBehavior<EntityBehaviorHealth>() == null) return;
            if (entity.GetBehavior<EBEffectsAffected>() != null) return;

            var beh = new EBEffectsAffected(entity);
            entity.SidedProperties.Behaviors.Add(beh);
            // Entity.Initialize already ran, so this behavior was never initialized by the entity — do it manually.
            beh.Initialize(entity.Properties, new JsonObject(new JObject()));
        }
    }
}
