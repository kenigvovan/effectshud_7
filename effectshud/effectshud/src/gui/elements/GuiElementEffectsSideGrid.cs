using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;

namespace effectshud.src.gui.elements
{
    public class GuiElementEffectsSideGrid : GuiElement
    {
        private List<GuiElementEffectCell> effectsCells;
        private CairoFont font;
        public GuiElementEffectsSideGrid(ICoreClientAPI capi, ElementBounds bounds) : base(capi, bounds)
        {
            effectsCells = new List<GuiElementEffectCell>();
            font = CairoFont.WhiteSmallText().WithColor(new double[] { 0.8, 0.8, 0.8, 1 }).WithStroke(new double[] { 0.7, 0.7, 0.7, 0.8 }, 1.2).WithSlant(FontSlant.Italic);
        }
        public override void ComposeElements(Context ctxStatic, ImageSurface surface)
        {
            base.ComposeElements(ctxStatic, surface);
            foreach(var it in effectsCells)
            {
                it.ComposeElements(ctxStatic, surface);
            }
        }
        public override void RenderInteractiveElements(float deltaTime)
        {
            base.RenderInteractiveElements(deltaTime);
            foreach(var it in effectsCells)
            {
                it.RenderInteractiveElements(deltaTime);
            }
        }
        public void RemoveEffectCell(string typeId)
        {
            foreach(var it in effectsCells.ToArray())
            {
                if(it.TypeId != null && it.TypeId == typeId)
                {
                    this.effectsCells.Remove(it);
                    it.Dispose();
                }
            }
        }

        public void AddEffectCell(EffectClientData ecd)
        {
            var bounds = this.Bounds.FlatCopy().WithFixedSize(128, 64);
            bounds.absFixedY += 72 * this.effectsCells.Count;
            bounds.fixedY = bounds.absFixedY;
            var cc = new GuiElementEffectCell(this.api, "no", this.font, bounds, ecd);
            effectsCells.Add(cc);
        }
        public override void Dispose()
        {
            base.Dispose();
            foreach(var it in effectsCells)
            {
                it.Dispose();
            }
        }
    }
}
