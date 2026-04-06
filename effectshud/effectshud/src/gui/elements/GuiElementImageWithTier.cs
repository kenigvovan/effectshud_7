using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace effectshud.src.gui.elements
{
    public class GuiElementImageWithTier : GuiElementTextBase
    {
        private readonly string imageAsset;
        int tier;
        bool positive;
        public GuiElementImageWithTier(ICoreClientAPI capi, ElementBounds bounds, string imageAsset, int tier, bool positive) : base(capi, "", null, bounds)
        {
            this.imageAsset = imageAsset;
            this.tier = tier;
            this.positive = positive;
        }
        public override void ComposeElements(Context context, ImageSurface surface)
        {
            context.Save();

            IAsset asset = api.Assets.TryGet(string.Format("effectshud:textures/effects/{0}.png", imageAsset));
            using (ImageSurface imageSurface = getImageSurfaceFromAsset(api, asset.Location, 255))
            {
                SurfacePattern pattern = getPattern(api, asset.Location, true, 255, 1f);

                pattern.Filter = Filter.Best;
                context.SetSource(pattern);
                context.Rectangle(Bounds.drawX, Bounds.drawY, Bounds.OuterWidth, Bounds.OuterHeight);
                context.SetSourceSurface(imageSurface, (int)Bounds.drawX, (int)Bounds.drawY);

                context.Paint();
                if (tier > 0)
                {
                    var assetTier = api.Assets.TryGet("effectshud:textures/effects/tier" + tier + (positive ? "p" : "n") + ".png");
                    if (assetTier != null)
                    {
                        var gemSurface = getImageSurfaceFromAsset(api, api.Assets.TryGet("effectshud:textures/effects/tier" + tier + (positive ? "p" : "n") + ".png").Location, 255);
                        context.SetSourceSurface(gemSurface, (int)Bounds.drawX, (int)Bounds.drawY);
                        context.Paint();
                    }
                }
                context.FillPreserve();
                context.Restore();
                pattern.Dispose();
            }
        }

    }
}
