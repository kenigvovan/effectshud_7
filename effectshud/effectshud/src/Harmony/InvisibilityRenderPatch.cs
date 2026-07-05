using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace effectshud.src
{
    [HarmonyPatch]
    public class InvisibilityRenderPatch
    {
        public static bool Prefix_GetFullEntityPacket(ClientSystemEntities __instance, Entity entity)
        {
            EBEffectsAffected ebef = entity.GetBehavior<EBEffectsAffected>();
            if(ebef != null)
            {
                ebef.SendAllEffectsToClient();
            }
            return true;
        }

        public static bool Prefix_DoRender3DOpaqueBatched(EntityShapeRenderer __instance)
        {
            var uid = (__instance.entity as EntityPlayer)?.PlayerUID;
            if (uid != null && effectshud.invisiblePlayers.ContainsKey(uid))
            {
                return false;
            }
            return true;
        }

        public static bool Prefix_DoRender2D(EntityShapeRenderer __instance)
        {
            var uid = (__instance.entity as EntityPlayer)?.PlayerUID;
            if (uid != null && effectshud.invisiblePlayers.ContainsKey(uid))
            {
                return false;
            }
            return true;
        }

        // Held items (weapon/tool in hand) are drawn by RenderHeldItem, called from DoRender3DOpaque — a separate
        // path from the batched avatar mesh (which already includes worn armor/clothing and is hidden above). Without
        // this they'd hang in the air on an invisible player. Hide them too.
        public static bool Prefix_RenderHeldItem(EntityShapeRenderer __instance)
        {
            var uid = (__instance.entity as EntityPlayer)?.PlayerUID;
            if (uid != null && effectshud.invisiblePlayers.ContainsKey(uid))
            {
                return false;
            }
            return true;
        }

        // The over-head name tag is drawn by EntityBehaviorNameTag.OnRenderFrame, separately from DoRender2D,
        // so it would otherwise hang in the air over an invisible player. Hide it too. ___entity injects the
        // behavior's protected 'entity' field.
        public static bool Prefix_NameTag_OnRenderFrame(Entity ___entity)
        {
            var uid = (___entity as EntityPlayer)?.PlayerUID;
            if (uid != null && effectshud.invisiblePlayers.ContainsKey(uid))
            {
                return false;
            }
            return true;
        }
    }
}
