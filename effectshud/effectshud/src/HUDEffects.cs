using System.Linq;
using effectshud.src.gui.elements;
using Vintagestory.API.Client;

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
            this.ComposeGuis();
        }
        
        public override void OnOwnPlayerDataReceived()
        {
            this.ComposeGuis();       
        }
        public GuiElementEffectsSideGrid CellsGrid => (GuiElementEffectsSideGrid)this.Composers["effectshud2"]?.GetElement("cellsgrid") ?? null;
        public void ComposeGuis()
        {
            IRenderAPI render = this.capi.Render;
            ElementBounds bounds1 = new ElementBounds()
            {
                Alignment = EnumDialogArea.RightMiddle,
                BothSizing = ElementSizing.Fixed,
                fixedWidth = HUDWidth,
                fixedHeight = HUDHeight
            };
            GuiComposer Compo;
            GuiComposer Compo2;

            Compo = this.capi.Gui.CreateCompo("effectshud", bounds1);
            Compo2 = this.capi.Gui.CreateCompo("effectshud2", bounds1);
            
            EBEffectsAffected ebef = capi.World?.Player?.Entity?.GetBehavior<EBEffectsAffected>() ?? null;
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
            var innerBounds = bounds1.CopyOffsetedSibling(5, 5);

            var gefsg = new GuiElementEffectsSideGrid(this.capi, innerBounds);
            foreach (var it in ebef.onlyClientsActiveEffects.Values.ToArray())
            {
                gefsg.AddEffectCell(it);
            }
            Compo2.AddInteractiveElement(gefsg, "cellsgrid");

            effectshud.redrawEffectPictures = false;
            Compo.Compose();
            Compo2.Compose();
            
            this.Composers["effectshud"] = Compo;
            this.Composers["effectshud2"] = Compo2;
        }
        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
