﻿using Cairo;
using effectshud.src.gui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace effectshud.src
{
    public class HUDEffects : HudElement
    {
        public static int glOffset = 0;
        float HUDWidth = 128;
        float HUDHeight = 1000;
        float wChange = 64;
        float hChange = 64;
        float del = 20;
        float texSizeW = 64;
        float texSizeH = 64;
        public override double DrawOrder => 0.1;
        public HUDEffects(ICoreClientAPI capi) : base(capi)
        {
            capi.World.RegisterGameTickListener((float dt) => { 
                if(effectshud.showHUD) ComposeGuis(); }, 1000);
        }
        
        public override void OnOwnPlayerDataReceived()
        {
            this.ComposeGuis();       
        }
         
        public void ComposeGuis()
        {
            IRenderAPI render = this.capi.Render;
            ElementBounds bounds1 = new ElementBounds()
            {
                Alignment = EnumDialogArea.RightFixed,
                BothSizing = ElementSizing.Fixed,
                fixedWidth = HUDWidth,
                fixedHeight = HUDHeight
            };

            GuiComposer Compo = this.capi.Gui.CreateCompo("effectshud", bounds1);
            GuiComposer Compo2 = this.capi.Gui.CreateCompo("effectshud2", bounds1);
            int currentEffectCounter = 0;
            EBEffectsAffected ebef = capi.World.Player.Entity.GetBehavior<EBEffectsAffected>();
            if(ebef == null) 
            {
                return;
            }
            foreach (var it in ebef.onlyClientsActiveEffects.Values.ToArray())
            {
                if(it.duration <= 0)
                {
                    effectshud.clientsActiveEffects.Remove(it.typeId);
                }
            }
            foreach (var it in ebef.onlyClientsActiveEffects.Values)
            {

                if (effectshud.effectsShouldBeRendered.TryGetValue(it.typeId, out bool al) && al)
                {
                    effectshud.effectsPosNeg.TryGetValue(it.typeId, out bool posneg);
                    GuiElementHelpersForImageWithTier.AddImageWithTier(Compo, ElementBounds.Fixed(0, (int)((texSizeH + del) * currentEffectCounter) + glOffset, 64, 64), it.typeId, it.tier, posneg);
                    //Compo.AddImage(ElementBounds.Fixed(0, (int)((texSizeH + del) * currentEffectCounter) + glOffset, 64, 64), al[it.tier - 1]);


                    if (it.infinite)
                    {
                       Compo.AddStaticText("--:--",
                       CairoFont.WhiteSmallText().WithFontSize(12),
                       ElementBounds.Fixed(10, (int)(((texSizeH + del) * currentEffectCounter) + glOffset + ((del) * currentEffectCounter + 64))).WithFixedSize(32.0, 10.0));
                    }
                    else
                    {
                        var fontv = CairoFont.WhiteSmallText().WithColor(new double[] { 0, 0, 0, 1 }).WithStroke(new double[] { 255, 255, 255, 0.8 }, 1.8);
                        //Compo.AddStaticText("67", CairoFont.WhiteSmallText().WithFontSize(12), ElementBounds.Fixed(6, (int)(hChange + del) * currentEffectCounter + 32).WithFixedSize(32.0, 10.0));
                        Compo2.AddStaticText((it.duration / 60).ToString() + ":" + ((it.duration % 60) < 10 ? "0" + (it.duration % 60) : (it.duration % 60).ToString()),
                            fontv,
                            ElementBounds.Fixed(10, (int)(((texSizeH + del) * currentEffectCounter) + glOffset + 64)).WithFixedSize(42.0, 10.0));
                    }
                }
                currentEffectCounter++;
            }
            foreach (var it in ebef.onlyClientsActiveEffects.Values)
            {
                if (it.infinite)
                    continue;
                it.duration--;
            }
            effectshud.redrawEffectPictures = false;
            Compo.Compose();
            Compo2.Compose();
            this.Composers["effectshud"] = Compo;
            this.Composers["effectshud2"] = Compo2;
            this.TryOpen();      
        }
    }
}
