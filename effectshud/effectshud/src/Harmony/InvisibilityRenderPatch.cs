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
    }
}
