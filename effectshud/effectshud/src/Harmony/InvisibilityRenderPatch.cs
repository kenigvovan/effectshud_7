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
                ebef.SendActiveEffectsToClient(null);
            }
            return true;
        }

        public static bool Prefix_DoRender3DOpaqueBatched(EntityShapeRenderer __instance)
        {
            if (effectshud.invisiblePlayers.ContainsKey((__instance.entity as EntityPlayer)?.PlayerUID))
            {
                return false;
            }
            return true;
        }

        public static bool Prefix_DoRender2D(EntityShapeRenderer __instance)
        {
            if (effectshud.invisiblePlayers.ContainsKey((__instance.entity as EntityPlayer)?.PlayerUID))
            {
                return false;
            }
            return true;
        }
    }
}
