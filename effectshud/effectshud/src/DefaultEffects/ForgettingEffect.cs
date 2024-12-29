﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace effectshud.src.DefaultEffects
{
    public class ForgettingEffect: Effect
    {
        public ForgettingEffect()
        {
            this.effectTypeId = "forgetting";
        }
        public override void OnStart()
        {           
            //entity.Properties.Client.Renderer.
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
