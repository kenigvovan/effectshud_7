using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Vintagestory.API.Client;
using VSImGui.API;

namespace effectshud.src.gui
{
    public class HUDEffectsImGui : ImGuiDialogBase
    {
        public static int glOffset = 64;
        public static bool EditMode = false;
        protected override bool OnClose() => false;
        private readonly Dictionary<string, int> _textureCache = new();
        private readonly List<Vintagestory.API.Client.LoadedTexture> _ownedTextures = new(); // rasterized SVGs we must dispose
        private Config _config;
        private bool _wasEditMode = false;
        private int _lastN = -1;
        private bool _loggedDrawError = false;

        public HUDEffectsImGui(ICoreClientAPI api) : base(api)
        {
            _config = api.ModLoader.GetModSystem<effectshud>().config;
        }

        protected override bool OnDraw()
        {
            var ebef = ClientApi.World?.Player?.Entity?.GetBehavior<EBEffectsAffected>();
            var effectsDict = ebef?.onlyClientsActiveEffects;
            bool hasEffects = effectsDict != null && effectsDict.Count > 0;

            if (!hasEffects && !EditMode)
            {
                _wasEditMode = false;
                return true;
            }

            // Contain any draw failure to THIS dialog. Original VSImGui aborts the whole ImGui frame if an OnDraw
            // throws, which would blank every other HUD (hotbar/resource bar) too. Catch + log once so we both keep
            // the rest of the HUD alive and learn the real cause from the log.
            try
            {
            float iconSize = (float)_config.EFFECT_ICON_SIZE;
            float padding = 4f;
            var viewport = ImGui.GetMainViewport();

            bool growLeft = _config.HUD_GROW_LEFT && _config.HUD_HORIZONTAL;
            bool growUp = _config.HUD_GROW_UP && !_config.HUD_HORIZONTAL;

            // Materialize visible list (filter/sort/reverse independently)
            List<EffectClientData> visibleList = new List<EffectClientData>();
            if (hasEffects)
            {
                IEnumerable<EffectClientData> visible = effectsDict.Values
                    .Where(e => e.duration > 0 || e.infinite);
                if (_config.HUD_FILTER == 1) visible = visible.Where(e => e.positive);
                else if (_config.HUD_FILTER == 2) visible = visible.Where(e => !e.positive);
                if (_config.HUD_SORT == 1) visible = visible.OrderBy(e => e.infinite ? double.MaxValue : e.duration);
                else if (_config.HUD_SORT == 2) visible = visible.OrderByDescending(e => e.infinite ? double.MinValue : e.duration);
                if (_config.HUD_REVERSE_ORDER) visible = visible.Reverse();
                visibleList = visible.ToList();
            }
            int N = visibleList.Count;

            // Compute slot dimensions
            float fontScale = Math.Max(0.5f, _config.HUD_TIMER_SCALE);
            float baseFontSize = ImGui.GetFontSize();
            float scaledFontSize = baseFontSize * fontScale;
            float lineHeight = ImGui.GetTextLineHeight() * fontScale;
            float spacingY = ImGui.GetStyle().ItemSpacing.Y;
            float colWidth = iconSize;
            if (_config.HUD_SHOW_TIMER)
            {
                foreach (var ecd in visibleList)
                {
                    if (ecd.infinite) continue; // infinite effects show no countdown — and we must NOT measure an empty string: ImGui.NET CalcTextSize("")/AddText("") corrupts the draw list and blanks the whole HUD
                    int totalSec = Math.Max(0, (int)ecd.duration);
                    string timeText = $"{totalSec / 60}:{totalSec % 60:D2}";
                    float w = ImGui.CalcTextSize(timeText).X * fontScale;
                    if (w > colWidth) colWidth = w;
                }
            }
            float itemHeight = iconSize + (_config.HUD_SHOW_TIMER ? spacingY + lineHeight : 0);
            float slotW = colWidth;
            float slotH = itemHeight;

            // Step vector between slots (depends on direction)
            float stepX = _config.HUD_HORIZONTAL ? (colWidth + padding) * (growLeft ? -1f : 1f) : 0f;
            float stepY = _config.HUD_HORIZONTAL ? 0f : (itemHeight + spacingY) * (growUp ? -1f : 1f);

            // Anchor = absolute screen position of icon[0]
            float anchorX, anchorY;
            if (_config.HUD_X >= 0 && _config.HUD_Y >= 0)
            {
                anchorX = _config.HUD_X;
                anchorY = _config.HUD_Y;
            }
            else if (_config.HUD_HORIZONTAL)
            {
                anchorX = viewport.WorkSize.X / 2f;
                anchorY = viewport.WorkSize.Y * 0.02f;
            }
            else
            {
                anchorX = viewport.WorkSize.X - colWidth - glOffset;
                anchorY = growUp ? viewport.WorkSize.Y * 0.8f : viewport.WorkSize.Y * 0.2f;
            }

            // Bounding box (covers all slots)
            int boxN = Math.Max(N, 1);
            float boxW = _config.HUD_HORIZONTAL ? boxN * colWidth + (boxN - 1) * padding : colWidth;
            float boxH = _config.HUD_HORIZONTAL ? itemHeight : boxN * itemHeight + (boxN - 1) * spacingY;
            if (N == 0 && EditMode)
            {
                boxW = Math.Max(boxW, 60f);
                boxH = Math.Max(boxH, 40f);
            }
            float boxX = growLeft ? anchorX + slotW - boxW : anchorX;
            float boxY = growUp ? anchorY + slotH - boxH : anchorY;

            // Edit mode: draggable handle window matching the bounding box
            if (EditMode)
            {
                bool nChanged = _lastN != N;
                if (!_wasEditMode || nChanged)
                    ImGui.SetNextWindowPos(new Vector2(boxX, boxY), ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(boxW, boxH), ImGuiCond.Always);
                ImGui.SetNextWindowBgAlpha(0.4f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

                var editFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize
                              | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings
                              | ImGuiWindowFlags.NoFocusOnAppearing;

                if (ImGui.Begin("##effectshud_edit", editFlags))
                {
                    if (N == 0)
                        ImGui.TextColored(new Vector4(1f, 0.9f, 0.2f, 1f), "HUD");

                    var winPos = ImGui.GetWindowPos();
                    float newAnchorX = growLeft ? (winPos.X + boxW - slotW) : winPos.X;
                    float newAnchorY = growUp ? (winPos.Y + boxH - slotH) : winPos.Y;
                    if (newAnchorX != _config.HUD_X || newAnchorY != _config.HUD_Y)
                    {
                        _config.HUD_X = newAnchorX;
                        _config.HUD_Y = newAnchorY;
                        anchorX = newAnchorX;
                        anchorY = newAnchorY;
                        ClientApi.StoreModConfig(_config, "effectshud.json");
                    }
                }
                ImGui.End();
                ImGui.PopStyleVar(2);
            }

            _wasEditMode = EditMode;
            _lastN = N;

            // Draw icons via background draw list (under ImGui windows, on top of game scene)
            if (N > 0)
            {
                var drawList = ImGui.GetBackgroundDrawList();
                var tint = new Vector4(1f, 1f, 1f, _config.HUD_ICON_ALPHA);
                uint tintCol = ImGui.ColorConvertFloat4ToU32(tint);
                var textColor = new Vector4(0.9f, 0.85f, 1f, 1f);
                uint textCol = ImGui.ColorConvertFloat4ToU32(textColor);

                if (_config.HUD_BG_ALPHA > 0f && !EditMode)
                {
                    var bgColor = new Vector4(0f, 0f, 0f, _config.HUD_BG_ALPHA);
                    uint bgCol = ImGui.ColorConvertFloat4ToU32(bgColor);
                    drawList.AddRectFilled(new Vector2(boxX, boxY), new Vector2(boxX + boxW, boxY + boxH), bgCol);
                }

                for (int i = 0; i < N; i++)
                {
                    var ecd = visibleList[i];
                    int texId = GetOrLoadTexture(ecd.typeId);
                    if (texId == 0) continue;

                    float slotX = anchorX + i * stepX;
                    float slotY = anchorY + i * stepY;

                    float iconOffsetX = (colWidth - iconSize) / 2f;
                    var iconMin = new Vector2(slotX + iconOffsetX, slotY);
                    var iconMax = iconMin + new Vector2(iconSize, iconSize);
                    drawList.AddImage((nint)texId, iconMin, iconMax, Vector2.Zero, Vector2.One, tintCol);

                    if (ecd.tier > 1)
                    {
                        string tierText = ToRoman(ecd.tier);
                        Vector2 tierSize = ImGui.CalcTextSize(tierText) * fontScale;
                        var tierPos = new Vector2(iconMax.X - tierSize.X - 2f, iconMin.Y + 1f);
                        var tierFont = ImGui.GetFont();
                        uint tierShadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.9f));
                        uint tierCol = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.3f, 1f));
                        drawList.AddText(tierFont, scaledFontSize, tierPos + new Vector2(-1, 0), tierShadow, tierText);
                        drawList.AddText(tierFont, scaledFontSize, tierPos + new Vector2(1, 0), tierShadow, tierText);
                        drawList.AddText(tierFont, scaledFontSize, tierPos + new Vector2(0, -1), tierShadow, tierText);
                        drawList.AddText(tierFont, scaledFontSize, tierPos + new Vector2(0, 1), tierShadow, tierText);
                        drawList.AddText(tierFont, scaledFontSize, tierPos, tierCol, tierText);
                    }

                    if (_config.HUD_SHOW_TIMER && !ecd.infinite) // infinite = no countdown; also avoids the empty-string ImGui.NET bug that blanks the HUD
                    {
                        int totalSec = Math.Max(0, (int)ecd.duration);
                        string timeText = $"{totalSec / 60}:{totalSec % 60:D2}";
                        float textWidth = ImGui.CalcTextSize(timeText).X * fontScale;
                        float textOffsetX = (colWidth - textWidth) / 2f;
                        var textPos = new Vector2(slotX + textOffsetX, slotY + iconSize + spacingY);
                        var font = ImGui.GetFont();
                        uint shadowCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.85f));
                        drawList.AddText(font, scaledFontSize, textPos + new Vector2(-1, 0), shadowCol, timeText);
                        drawList.AddText(font, scaledFontSize, textPos + new Vector2(1, 0), shadowCol, timeText);
                        drawList.AddText(font, scaledFontSize, textPos + new Vector2(0, -1), shadowCol, timeText);
                        drawList.AddText(font, scaledFontSize, textPos + new Vector2(0, 1), shadowCol, timeText);
                        drawList.AddText(font, scaledFontSize, textPos, textCol, timeText);
                    }
                }
            }
            }
            catch (Exception ex)
            {
                if (!_loggedDrawError)
                {
                    _loggedDrawError = true;
                    ClientApi.Logger.Warning("[effectshud] effects HUD draw failed (suppressed so other HUDs keep rendering): {0}", ex);
                }
            }

            return true;
        }

        private static readonly (int value, string numeral)[] _romanMap = new[]
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
            (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
            (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        };

        private static string ToRoman(int n)
        {
            if (n <= 0) return n.ToString();
            var sb = new System.Text.StringBuilder();
            foreach (var (value, numeral) in _romanMap)
            {
                while (n >= value) { sb.Append(numeral); n -= value; }
            }
            return sb.ToString();
        }

        private int GetOrLoadTexture(string typeId)
        {
            if (_textureCache.TryGetValue(typeId, out int cached))
                return cached;

            int texId = 0;
            try
            {
                // A consumer mod may register a custom icon (any domain) via RegisterClientEffectData/RegisterEffect;
                // otherwise fall back to the effectshud-domain convention. .svg is rasterized, .png loaded directly.
                var icons = effectshud.Instance?.effectIcons;
                var location = (icons != null && icons.TryGetValue(typeId, out var custom) && custom != null)
                    ? custom
                    : new Vintagestory.API.Common.AssetLocation($"effectshud:textures/effects/{typeId}.png");

                if (location.Path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    // Keep the LoadedTexture alive (disposed in Dispose) — otherwise its finalizer logs a leak warning.
                    var lt = ClientApi.Gui.LoadSvgWithPadding(location, 64, 64, 2);
                    if (lt != null) { texId = lt.TextureId; _ownedTextures.Add(lt); }
                }
                else
                {
                    texId = ClientApi.Render.GetOrLoadTexture(location);
                }
            }
            catch { texId = 0; }

            _textureCache[typeId] = texId;
            return texId;
        }

        protected override void Dispose(bool disposing)
        {
            _textureCache.Clear();
            foreach (var t in _ownedTextures) t?.Dispose();
            _ownedTextures.Clear();
            base.Dispose(disposing);
        }
    }
}
