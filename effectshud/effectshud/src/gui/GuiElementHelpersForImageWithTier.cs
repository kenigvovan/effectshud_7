using effectshud.src.gui.elements;
using Vintagestory.API.Client;

namespace effectshud.src.gui
{
    public static class GuiElementHelpersForImageWithTier
    {
        public static GuiComposer AddImageWithTier(this GuiComposer composer, ElementBounds bounds, string imageAsset, int tier = 0, bool positive = true)
        {
            if (!composer.Composed)
            {
                composer.AddStaticElement(new GuiElementImageWithTier(composer.Api, bounds, imageAsset, tier, positive), null);
            }
            return composer;
        }
    }
}
