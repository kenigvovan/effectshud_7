using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace effectshud.src.gui.elements
{
    public class GuiElementEffectCell : GuiElementTextBase
    {
        public int QuantityTextLines
        {
            get
            {
                return textUtil.GetQuantityTextLines(Font, text, Bounds.InnerWidth, EnumLinebreakBehavior.Default);
            }
        }
        public GuiElementEffectCell(ICoreClientAPI capi, string text, CairoFont font, ElementBounds bounds, EffectClientData effectClientData) : base(capi, text, font, bounds)
        {
            orientation = font.Orientation;
            textTexture = new LoadedTexture(capi);
            imageTexture = new LoadedTexture(capi);
            deltaSum = 0;
            this.effectClientData = effectClientData;
            this.IsExpired = false;
        }
        public override void ComposeTextElements(Context ctx, ImageSurface surface)
        {
            RecomposeTextAndImage(false);
            RecomposeText(false);
        }
        public void AutoHeight()
        {
            Bounds.fixedHeight = GetMultilineTextHeight() / RuntimeEnv.GUIScale;
            Bounds.CalcWorldBounds();
            autoHeight = true;
        }
        public void RecomposeText(bool async = false, double deltaSum = 1)
        {
            effectClientData.duration -= deltaSum;
            if (effectClientData.duration < 0)
            {
                this.IsExpired = true;
                return;
            }
            int durationInt = (int)effectClientData.duration;
            text = (durationInt / 60 > 0
                                ? (durationInt / 60).ToString()
                                : "0")
                        + ":" + (durationInt % 60 < 10
                                ? "0" + durationInt % 60
                                : (durationInt % 60).ToString());
            if (autoHeight)
            {
                AutoHeight();
            }
            ImageSurface surface;
            Context ctx;
            surface = new ImageSurface(0, (int)64, (int)64);
            ctx = genContext(surface);
            DrawMultilineTextAt(ctx, 0.0, 0.0, orientation);
            generateTexture(surface, ref textTexture, true);
            ctx.Dispose();
            surface.Dispose();
        }
        public void RecomposeTextAndImage(bool async = false)
        {
            Context ctx;
            IAsset asset = api.Assets.TryGet(string.Format("effectshud:textures/effects/{0}.png", effectClientData.typeId));
            if(asset == null)
            {
                return;
            }
            using (ImageSurface imageSurface = getImageSurfaceFromAsset(api, asset.Location, 255))
            {
                SurfacePattern pattern = getPattern(api, asset.Location, true, 150, 1f);
                ctx = genContext(imageSurface);
                ctx.SetSourceSurface(imageSurface, 0, 0);
                ctx.Fill();
                
                if (effectClientData.tier > 0)
                {
                    var assetTier = api.Assets.TryGet("effectshud:textures/effects/tier" + effectClientData.tier + (effectClientData.positive ? "p" : "n") + ".png");
                    if (assetTier != null)
                    {
                        
                        var gemSurface = GuiElement.getImageSurfaceFromAsset(api, api.Assets.TryGet("effectshud:textures/effects/tier" + effectClientData.tier + (effectClientData.positive ? "p" : "n") + ".png").Location, 255);
                        ctx.SetSourceSurface(gemSurface, (int)0, (int)0);
                        ctx.Paint();
                    }
                }
                generateTexture(imageSurface, ref imageTexture, false);
                ctx.Dispose();
                imageSurface.Dispose();
            }
        }
        public override void RenderInteractiveElements(float deltaTime)
        {
            deltaSum += deltaTime;
            if (deltaSum > 0.9)
            {
                RecomposeText(false, deltaSum);
                deltaSum = 0;
            }
            Bounds.absInnerHeight = 64;
                Bounds.absInnerWidth = 64;
            api.Render.Render2DTexturePremultipliedAlpha(imageTexture.TextureId,
                                                                (int)Bounds.renderX,
                                                                (int)Bounds.renderY - 20,
                                                                (int)Bounds.InnerWidth,
                                                                (float)((int)Bounds.InnerHeight * 0.8), 50f, null);
            api.Render.Render2DTexturePremultipliedAlpha(textTexture.TextureId,
                                                                (float)((int)Bounds.renderX + effectshud.Instance.config.EFFECT_ICON_SIZE * 0.5), 
                                                                (float)((int)Bounds.renderY + effectshud.Instance.config.EFFECT_ICON_SIZE),
                                                                (int)Bounds.InnerWidth,
                                                                (int)Bounds.InnerHeight, 50f, new Vec4f(0.3f, 0.1f, 0.4f, 1));
        }
        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseDownOnElement(api, args);
            Action onClick = OnClick;
            if (onClick == null)
            {
                return;
            }
            onClick();
        }
        public void SetNewTextAsync(string text, bool autoHeight = false, bool forceRedraw = false)
        {
            SetNewText(text, autoHeight, forceRedraw, true);
        }
        public void SetNewText(string text, bool autoHeight = false, bool forceRedraw = false, bool async = false)
        {
            if (this.text != text || forceRedraw)
            {
                this.text = text;
                Bounds.CalcWorldBounds();
                if (autoHeight)
                {
                    AutoHeight();
                }
                RecomposeTextAndImage(async);
            }
        }
        public override void Dispose()
        {
            LoadedTexture loadedTexture = textTexture;
            if (loadedTexture == null && imageTexture == null)
            {
                return;
            }
            loadedTexture.Dispose();
            imageTexture.Dispose();
        }
        private EnumTextOrientation orientation;
        private LoadedTexture textTexture;
        private LoadedTexture imageTexture;
        public Action OnClick;
        public bool autoHeight;
        private double deltaSum;
        private EffectClientData effectClientData;
        public string TypeId => effectClientData?.typeId ?? null;
        public bool IsExpired;
    }
}
