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
        private Config _config;

        public HUDEffectsImGui(ICoreClientAPI api) : base(api)
        {
            _config = api.ModLoader.GetModSystem<effectshud>().config;
        }

        protected override bool OnDraw()
        {
            var ebef = ClientApi.World?.Player?.Entity?.GetBehavior<EBEffectsAffected>();
            var effectsDict = ebef?.onlyClientsActiveEffects;
            bool hasEffects = effectsDict != null && effectsDict.Count > 0;

            if (!hasEffects && !EditMode) return true;

            float iconSize = (float)_config.EFFECT_ICON_SIZE;
            float padding = 4f;
            var viewport = ImGui.GetMainViewport();

            bool growLeft = _config.HUD_GROW_LEFT;
            bool growUp = _config.HUD_GROW_UP;

            // Materialize visible effects with filter/sort/reverse
            List<EffectClientData> visibleList = new List<EffectClientData>();
            if (hasEffects)
            {
                IEnumerable<EffectClientData> visible = effectsDict.Values
                    .Where(e => e.duration > 0 || e.infinite);

                if (_config.HUD_FILTER == 1)
                    visible = visible.Where(e => e.positive);
                else if (_config.HUD_FILTER == 2)
                    visible = visible.Where(e => !e.positive);

                if (_config.HUD_SORT == 1)
                    visible = visible.OrderBy(e => e.infinite ? double.MaxValue : e.duration);
                else if (_config.HUD_SORT == 2)
                    visible = visible.OrderByDescending(e => e.infinite ? double.MinValue : e.duration);

                bool reverseRender = _config.HUD_REVERSE_ORDER;
                if (growLeft && _config.HUD_HORIZONTAL) reverseRender = !reverseRender;
                if (growUp && !_config.HUD_HORIZONTAL) reverseRender = !reverseRender;

                if (reverseRender)
                    visible = visible.Reverse();

                visibleList = visible.ToList();
            }

            int N = visibleList.Count;

            // Compute exact window size
            float lineHeight = ImGui.GetTextLineHeight();
            float spacingY = ImGui.GetStyle().ItemSpacing.Y;

            float colWidth = iconSize;
            if (_config.HUD_SHOW_TIMER)
            {
                foreach (var ecd in visibleList)
                {
                    int totalSec = Math.Max(0, (int)ecd.duration);
                    string timeText = ecd.infinite ? "∞" : $"{totalSec / 60}:{totalSec % 60:D2}";
                    float w = ImGui.CalcTextSize(timeText).X;
                    if (w > colWidth) colWidth = w;
                }
            }

            float itemHeight = iconSize + (_config.HUD_SHOW_TIMER ? spacingY + lineHeight : 0);

            float totalWidth, totalHeight;
            if (_config.HUD_HORIZONTAL)
            {
                totalWidth = N > 0 ? N * colWidth + (N - 1) * padding : colWidth;
                totalHeight = itemHeight;
            }
            else
            {
                totalWidth = colWidth;
                totalHeight = N > 0 ? N * itemHeight + (N - 1) * spacingY : itemHeight;
            }

            // Edit mode placeholder when no effects
            if (N == 0 && EditMode)
            {
                totalWidth = Math.Max(totalWidth, 50f);
                totalHeight = Math.Max(totalHeight, 30f);
            }

            if (!EditMode)
            {
                float posX, posY;
                if (_config.HUD_X >= 0 && _config.HUD_Y >= 0)
                {
                    posX = growLeft ? _config.HUD_X - totalWidth : _config.HUD_X;
                    posY = growUp ? _config.HUD_Y - totalHeight : _config.HUD_Y;
                }
                else if (_config.HUD_HORIZONTAL)
                {
                    float anchorX = viewport.WorkSize.X / 2f;
                    posX = growLeft ? anchorX - totalWidth : anchorX;
                    posY = viewport.WorkSize.Y * 0.02f;
                }
                else
                {
                    posX = viewport.WorkSize.X - totalWidth - glOffset;
                    float anchorY = growUp ? viewport.WorkSize.Y * 0.8f : viewport.WorkSize.Y * 0.2f;
                    posY = growUp ? anchorY - totalHeight : anchorY;
                }
                ImGui.SetNextWindowPos(new Vector2(posX, posY), ImGuiCond.Always);
            }
            ImGui.SetNextWindowSize(new Vector2(totalWidth, totalHeight), ImGuiCond.Always);

            ImGui.SetNextWindowBgAlpha(EditMode ? 0.4f : _config.HUD_BG_ALPHA);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, EditMode ? 1f : 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

            var flags = ImGuiWindowFlags.NoTitleBar
                      | ImGuiWindowFlags.NoResize
                      | ImGuiWindowFlags.NoScrollbar
                      | ImGuiWindowFlags.NoSavedSettings
                      | ImGuiWindowFlags.NoFocusOnAppearing;

            if (!EditMode)
                flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;

            if (!ImGui.Begin("##effectshud_hud", flags))
            {
                ImGui.PopStyleVar(2);
                ImGui.End();
                return true;
            }
            ImGui.PopStyleVar(2);

            if (EditMode && N == 0)
            {
                ImGui.TextColored(new Vector4(1f, 0.9f, 0.2f, 1f), "HUD");
            }

            if (N > 0)
            {
                var tint = new Vector4(1f, 1f, 1f, _config.HUD_ICON_ALPHA);
                var textColor = new Vector4(0.9f, 0.85f, 1f, 1f);

                for (int i = 0; i < N; i++)
                {
                    var ecd = visibleList[i];
                    int texId = GetOrLoadTexture(ecd.typeId);
                    if (texId == 0) continue;

                    float colX = _config.HUD_HORIZONTAL ? i * (colWidth + padding) : 0f;
                    float colY = _config.HUD_HORIZONTAL ? 0f : i * (itemHeight + spacingY);

                    float iconOffsetX = (colWidth - iconSize) / 2f;
                    ImGui.SetCursorPos(new Vector2(colX + iconOffsetX, colY));
                    ImGui.Image((nint)texId, new Vector2(iconSize, iconSize), Vector2.Zero, Vector2.One, tint, Vector4.Zero);

                    if (_config.HUD_SHOW_TIMER)
                    {
                        int totalSec = Math.Max(0, (int)ecd.duration);
                        string timeText = ecd.infinite ? "∞" : $"{totalSec / 60}:{totalSec % 60:D2}";
                        float textWidth = ImGui.CalcTextSize(timeText).X;
                        float textOffsetX = (colWidth - textWidth) / 2f;
                        ImGui.SetCursorPos(new Vector2(colX + textOffsetX, colY + iconSize + spacingY));
                        ImGui.TextColored(textColor, timeText);
                    }
                }
            }

            if (EditMode)
            {
                var pos = ImGui.GetWindowPos();
                var size = ImGui.GetWindowSize();
                float newX = pos.X + (growLeft ? size.X : 0f);
                float newY = pos.Y + (growUp ? size.Y : 0f);
                if (newX != _config.HUD_X || newY != _config.HUD_Y)
                {
                    _config.HUD_X = newX;
                    _config.HUD_Y = newY;
                    ClientApi.StoreModConfig(_config, "effectshud.json");
                }
            }

            ImGui.End();
            return true;
        }

        private int GetOrLoadTexture(string typeId)
        {
            if (_textureCache.TryGetValue(typeId, out int cached))
                return cached;

            try
            {
                var location = new Vintagestory.API.Common.AssetLocation($"effectshud:textures/effects/{typeId}.png");
                int texId = ClientApi.Render.GetOrLoadTexture(location);
                _textureCache[typeId] = texId;
                return texId;
            }
            catch
            {
                _textureCache[typeId] = 0;
                return 0;
            }
        }

        protected override void Dispose(bool disposing)
        {
            _textureCache.Clear();
            base.Dispose(disposing);
        }
    }
}
