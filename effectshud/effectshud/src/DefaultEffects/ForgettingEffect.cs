using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using effectshud.src;

namespace effectshud.src.DefaultEffects
{
    [EffectRegistration(EffectTypeIds.Forgetting, shouldBeRendered: false)]
    public class ForgettingEffect: Effect
    {
        public override void OnStart()
        {           
            if(((entity as EntityPlayer).Player as IServerPlayer).WorldData != null)
            {
                if(SerializerUtil.Deserialize<bool>(((entity as EntityPlayer).Player as IServerPlayer).WorldData.GetModdata("createCharacter"), false))
                {
                    ((entity as EntityPlayer).Player as IServerPlayer).WorldData.SetModdata("createCharacter", SerializerUtil.Serialize<bool>(false));
                }
            }
        }
    }
}
