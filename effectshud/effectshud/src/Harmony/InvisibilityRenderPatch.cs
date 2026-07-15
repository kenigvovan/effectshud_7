using HarmonyLib;
using effectshud.src.DefaultEffects;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace effectshud.src
{
    /// <summary>Client-side render hiding for invisible entities. The source of truth is the
    /// <see cref="InvisibilityEffect.InvisibleAttr"/> WatchedAttributes flag, set server-side by the effect and
    /// auto-synced by the engine to every client that sees the entity — including clients that come into range
    /// later (full entity packet), so no side-channel packet bookkeeping is needed.</summary>
    [HarmonyPatch]
    public class InvisibilityRenderPatch
    {
        private static bool IsInvisible(Entity entity)
        {
            return entity != null && entity.WatchedAttributes.GetBool(InvisibilityEffect.InvisibleAttr);
        }

        public static bool Prefix_DoRender3DOpaqueBatched(EntityShapeRenderer __instance)
        {
            return !IsInvisible(__instance.entity);
        }

        public static bool Prefix_DoRender2D(EntityShapeRenderer __instance)
        {
            return !IsInvisible(__instance.entity);
        }

        // Held items (weapon/tool in hand) are drawn by RenderHeldItem, called from DoRender3DOpaque — a separate
        // path from the batched avatar mesh (which already includes worn armor/clothing and is hidden above). Without
        // this they'd hang in the air on an invisible player. Hide them too.
        public static bool Prefix_RenderHeldItem(EntityShapeRenderer __instance)
        {
            return !IsInvisible(__instance.entity);
        }

        // The over-head name tag is drawn by EntityBehaviorNameTag.OnRenderFrame, separately from DoRender2D,
        // so it would otherwise hang in the air over an invisible player. Hide it too. ___entity injects the
        // behavior's protected 'entity' field.
        public static bool Prefix_NameTag_OnRenderFrame(Entity ___entity)
        {
            return !IsInvisible(___entity);
        }
    }
}
