using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace effectshud.src.gui
{
    /// <summary>
    /// The on-screen effects HUD. Drawn immediate-mode in <see cref="OnRenderGUI"/> (no composer): timers change
    /// every frame, so icons and text are blitted as textures instead of recomposing a retained GUI.
    /// Positions are in framebuffer pixels (same units as <c>HUD_X/HUD_Y</c> in the config).
    /// </summary>
    public class HUDEffects : HudElement
    {
        public static int glOffset = 64;
        public static bool EditMode = false;

        private const float Padding = 4f;
        private const float SpacingY = 4f;
        private const double BaseFontSize = 12;
        private const int MaxCachedTexts = 256;
        private const float BlinkBelowSeconds = 10f;

        private readonly Config _config;
        private readonly Dictionary<string, (int texId, bool premultiplied)> _iconCache = new();
        private readonly List<LoadedTexture> _ownedTextures = new(); // rasterized SVGs we must dispose
        private readonly Dictionary<string, LoadedTexture> _timerTexts = new();
        private readonly Dictionary<string, LoadedTexture> _tierTexts = new();
        private readonly Dictionary<string, LoadedTexture> _tooltipTexts = new();
        private LoadedTexture _editLabel;
        private float _cachedTimerScale = -1f;

        // Last drawn bounding box + layout, used by the settings dialog to drag the HUD around.
        public float BoxX { get; private set; }
        public float BoxY { get; private set; }
        public float BoxW { get; private set; }
        public float BoxH { get; private set; }
        private float _slotW, _slotH;
        private bool _growLeft, _growUp;

        public HUDEffects(ICoreClientAPI capi) : base(capi)
        {
            _config = capi.ModLoader.GetModSystem<effectshud>().config;
        }

        public override string ToggleKeyCombinationCode => null;
        public override bool Focusable => false;
        public override bool ShouldReceiveKeyboardEvents() => false;
        public override bool ShouldReceiveMouseEvents() => false;

        public bool BoxContains(double x, double y)
            => x >= BoxX && x <= BoxX + BoxW && y >= BoxY && y <= BoxY + BoxH;

        /// <summary>Move the HUD so its bounding box's top-left lands at (boxX, boxY). Does not save the config.</summary>
        public void MoveBoxTo(float boxX, float boxY)
        {
            _config.HUD_X = _growLeft ? boxX + BoxW - _slotW : boxX;
            _config.HUD_Y = _growUp ? boxY + BoxH - _slotH : boxY;
            BoxX = boxX;
            BoxY = boxY;
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            var ebef = capi.World?.Player?.Entity?.GetBehavior<EBEffectsAffected>();
            var effectsDict = ebef?.onlyClientsActiveEffects;
            bool hasEffects = effectsDict != null && effectsDict.Count > 0;
            if (!hasEffects && !EditMode) return;

            if (_cachedTimerScale != _config.HUD_TIMER_SCALE)
            {
                ClearTextCache();
                _cachedTimerScale = _config.HUD_TIMER_SCALE;
            }

            float iconSize = (float)_config.EFFECT_ICON_SIZE;
            float frameW = capi.Render.FrameWidth;
            float frameH = capi.Render.FrameHeight;

            _growLeft = _config.HUD_GROW_LEFT && _config.HUD_HORIZONTAL;
            _growUp = _config.HUD_GROW_UP && !_config.HUD_HORIZONTAL;

            // Visible list (filter/sort/reverse independently)
            List<EffectClientData> visibleList = new List<EffectClientData>();
            if (hasEffects)
            {
                IEnumerable<EffectClientData> visible = effectsDict.Values
                    .Where(e => (e.duration > 0 || e.infinite) && EffectDisplayUtil.ShouldRender(e.typeId));
                if (_config.HUD_FILTER == 1) visible = visible.Where(e => e.positive);
                else if (_config.HUD_FILTER == 2) visible = visible.Where(e => !e.positive);
                if (_config.HUD_SORT == 1) visible = visible.OrderBy(e => e.infinite ? double.MaxValue : e.duration);
                else if (_config.HUD_SORT == 2) visible = visible.OrderByDescending(e => e.infinite ? double.MinValue : e.duration);
                if (_config.HUD_REVERSE_ORDER) visible = visible.Reverse();
                visibleList = visible.ToList();
            }
            int N = visibleList.Count;

            // Slot dimensions
            float colWidth = iconSize;
            float lineHeight = 0f;
            if (_config.HUD_SHOW_TIMER)
            {
                lineHeight = GetTimerText("0:00").Height;
                foreach (var ecd in visibleList)
                {
                    if (ecd.infinite) continue;
                    float w = GetTimerText(EffectDisplayUtil.FormatTime(ecd.duration)).Width;
                    if (w > colWidth) colWidth = w;
                }
            }
            float itemHeight = iconSize + (_config.HUD_SHOW_TIMER ? SpacingY + lineHeight : 0);
            _slotW = colWidth;
            _slotH = itemHeight;

            // Step vector between slots (depends on direction)
            float stepX = _config.HUD_HORIZONTAL ? (colWidth + Padding) * (_growLeft ? -1f : 1f) : 0f;
            float stepY = _config.HUD_HORIZONTAL ? 0f : (itemHeight + SpacingY) * (_growUp ? -1f : 1f);

            // Anchor = absolute screen position of icon[0]
            float anchorX, anchorY;
            if (_config.HUD_X >= 0 && _config.HUD_Y >= 0)
            {
                anchorX = _config.HUD_X;
                anchorY = _config.HUD_Y;
            }
            else if (_config.HUD_HORIZONTAL)
            {
                anchorX = frameW / 2f;
                anchorY = frameH * 0.02f;
            }
            else
            {
                anchorX = frameW - colWidth - glOffset;
                anchorY = _growUp ? frameH * 0.8f : frameH * 0.2f;
            }

            // Bounding box (covers all slots)
            int boxN = Math.Max(N, 1);
            float boxW = _config.HUD_HORIZONTAL ? boxN * colWidth + (boxN - 1) * Padding : colWidth;
            float boxH = _config.HUD_HORIZONTAL ? itemHeight : boxN * itemHeight + (boxN - 1) * SpacingY;
            if (N == 0 && EditMode)
            {
                boxW = Math.Max(boxW, 60f);
                boxH = Math.Max(boxH, 40f);
            }
            BoxW = boxW;
            BoxH = boxH;
            BoxX = _growLeft ? anchorX + _slotW - boxW : anchorX;
            BoxY = _growUp ? anchorY + _slotH - boxH : anchorY;

            if (EditMode)
            {
                capi.Render.RenderRectangle(BoxX, BoxY, 50, BoxW, BoxH, ColorUtil.ToRgba(100, 255, 230, 50));
                if (N == 0)
                {
                    _editLabel ??= capi.Gui.TextTexture.GenTextTexture("HUD", TimerFont(new double[] { 1, 0.9, 0.2, 1 }));
                    capi.Render.Render2DLoadedTexture(_editLabel, BoxX + 4, BoxY + 4, 51);
                }
            }
            else if (_config.HUD_BG_ALPHA > 0f && N > 0)
            {
                int a = (int)(GameMath.Clamp(_config.HUD_BG_ALPHA, 0f, 1f) * 255);
                capi.Render.RenderRectangle(BoxX, BoxY, 50, BoxW, BoxH, ColorUtil.ToRgba(a, 0, 0, 0));
            }

            float baseAlpha = GameMath.Clamp(_config.HUD_ICON_ALPHA, 0f, 1f);
            // Blink phase for effects about to expire: alpha oscillates between 35% and 100% of the icon alpha.
            float blink = 0.35f + 0.65f * (0.5f + 0.5f * (float)Math.Cos(capi.ElapsedMilliseconds / 1000.0 * Math.PI * 3));
            bool canHover = !EditMode && !capi.Input.MouseGrabbed;
            int mouseX = capi.Input.MouseX, mouseY = capi.Input.MouseY;
            EffectClientData hovered = null;

            for (int i = 0; i < N; i++)
            {
                var ecd = visibleList[i];
                var (texId, premultiplied) = GetOrLoadIcon(ecd.typeId);
                if (texId == 0) continue;

                float slotX = anchorX + i * stepX;
                float slotY = anchorY + i * stepY;
                float iconX = slotX + (colWidth - iconSize) / 2f;

                float iconAlpha = baseAlpha;
                if (_config.HUD_BLINK_EXPIRING && !ecd.infinite && ecd.duration < BlinkBelowSeconds) iconAlpha *= blink;

                if (premultiplied)
                    capi.Render.Render2DTexturePremultipliedAlpha(texId, iconX, slotY, iconSize, iconSize, 52, new Vec4f(iconAlpha, iconAlpha, iconAlpha, iconAlpha));
                else
                    capi.Render.Render2DTexture(texId, iconX, slotY, iconSize, iconSize, 52, new Vec4f(1f, 1f, 1f, iconAlpha));

                if (_config.HUD_POSNEG_BORDER)
                {
                    int borderCol = ecd.positive ? ColorUtil.ToRgba(220, 80, 200, 80) : ColorUtil.ToRgba(220, 220, 60, 60);
                    RenderBorder(iconX, slotY, iconSize, iconSize, 2f, borderCol);
                }

                if (canHover && mouseX >= iconX && mouseX <= iconX + iconSize && mouseY >= slotY && mouseY <= slotY + iconSize)
                    hovered = ecd;

                if (ecd.tier > 1)
                {
                    var tierTex = GetTierText(EffectDisplayUtil.ToRoman(ecd.tier));
                    capi.Render.Render2DLoadedTexture(tierTex, iconX + iconSize - tierTex.Width - 2f, slotY + 1f, 53);
                }

                if (_config.HUD_SHOW_TIMER && !ecd.infinite)
                {
                    var timeTex = GetTimerText(EffectDisplayUtil.FormatTime(ecd.duration));
                    capi.Render.Render2DLoadedTexture(timeTex, slotX + (colWidth - timeTex.Width) / 2f, slotY + iconSize + SpacingY, 53);
                }
            }

            if (hovered != null) RenderTooltip(hovered, mouseX, mouseY, frameW, frameH);
        }

        private void RenderBorder(float x, float y, float w, float h, float t, int color)
        {
            capi.Render.RenderRectangle(x, y, 53, w, t, color);
            capi.Render.RenderRectangle(x, y + h - t, 53, w, t, color);
            capi.Render.RenderRectangle(x, y + t, 53, t, h - 2 * t, color);
            capi.Render.RenderRectangle(x + w - t, y + t, 53, t, h - 2 * t, color);
        }

        private void RenderTooltip(EffectClientData ecd, int mouseX, int mouseY, float frameW, float frameH)
        {
            string text = EffectDisplayUtil.EffectName(ecd.typeId);
            if (ecd.tier > 1) text += " " + EffectDisplayUtil.ToRoman(ecd.tier);

            if (!_tooltipTexts.TryGetValue(text, out var tex))
            {
                if (_tooltipTexts.Count >= MaxCachedTexts)
                {
                    foreach (var t in _tooltipTexts.Values) t.Dispose();
                    _tooltipTexts.Clear();
                }
                var bg = new TextBackground { FillColor = GuiStyle.DialogStrongBgColor, Padding = 5, Radius = 3 };
                tex = capi.Gui.TextTexture.GenTextTexture(text, CairoFont.WhiteSmallText(), bg);
                _tooltipTexts[text] = tex;
            }

            float x = Math.Min(mouseX + 16f, frameW - tex.Width - 2f);
            float y = Math.Min(mouseY + 16f, frameH - tex.Height - 2f);
            capi.Render.Render2DLoadedTexture(tex, x, y, 60);
        }

        private CairoFont TimerFont(double[] color)
        {
            return CairoFont.WhiteSmallText()
                .WithFontSize((float)(BaseFontSize * Math.Max(0.5f, _config.HUD_TIMER_SCALE)))
                .WithColor(color)
                .WithStroke(new double[] { 0, 0, 0, 0.9 }, 2);
        }

        private LoadedTexture GetTimerText(string text)
            => GetText(_timerTexts, text, new double[] { 0.9, 0.85, 1, 1 });

        private LoadedTexture GetTierText(string text)
            => GetText(_tierTexts, text, new double[] { 1, 0.85, 0.3, 1 });

        private LoadedTexture GetText(Dictionary<string, LoadedTexture> cache, string text, double[] color)
        {
            if (cache.TryGetValue(text, out var tex)) return tex;
            if (cache.Count >= MaxCachedTexts)
            {
                foreach (var t in cache.Values) t.Dispose();
                cache.Clear();
            }
            tex = capi.Gui.TextTexture.GenTextTexture(text, TimerFont(color));
            cache[text] = tex;
            return tex;
        }

        private void ClearTextCache()
        {
            foreach (var t in _timerTexts.Values) t.Dispose();
            _timerTexts.Clear();
            foreach (var t in _tierTexts.Values) t.Dispose();
            _tierTexts.Clear();
            foreach (var t in _tooltipTexts.Values) t.Dispose();
            _tooltipTexts.Clear();
            _editLabel?.Dispose();
            _editLabel = null;
        }

        private (int texId, bool premultiplied) GetOrLoadIcon(string typeId)
        {
            if (_iconCache.TryGetValue(typeId, out var cached))
                return cached;

            var result = (0, false);
            try
            {
                // A consumer mod may register a custom icon (any domain) via RegisterClientEffectData/RegisterEffect;
                // otherwise fall back to the effectshud-domain convention. .svg is rasterized, .png loaded directly.
                var location = EffectDisplayUtil.ResolveIcon(typeId);
                if (location != null && location.Path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    // Keep the LoadedTexture alive (disposed in Dispose) — otherwise its finalizer logs a leak warning.
                    var lt = capi.Gui.LoadSvgWithPadding(location, 64, 64, 2);
                    if (lt != null) { result = (lt.TextureId, true); _ownedTextures.Add(lt); }
                }
                else if (location != null)
                {
                    result = (capi.Render.GetOrLoadTexture(location), false);
                }
            }
            catch { result = (0, false); }

            _iconCache[typeId] = result;
            return result;
        }

        public override void Dispose()
        {
            ClearTextCache();
            _iconCache.Clear();
            foreach (var t in _ownedTextures) t?.Dispose();
            _ownedTextures.Clear();
            base.Dispose();
        }
    }
}
