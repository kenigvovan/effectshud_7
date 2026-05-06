using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Vintagestory.API.Client;
using VSImGui.API;

namespace effectshud.src.gui
{
    public class HUDEffectsImGui : ImGuiDialogBase
    {
        public static int glOffset = 64;
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
            if (ebef == null) return true;

            var effects = ebef.onlyClientsActiveEffects;
            if (effects.Count == 0) return true;

            float iconSize = (float)ClientApi.ModLoader.GetModSystem<effectshud>().config.EFFECT_ICON_SIZE;
            float padding = 4f;
            float windowWidth = iconSize + padding * 2;

            // Позиция: правый край, по центру по вертикали
            var viewport = ImGui.GetMainViewport();
            float offsetX = glOffset;
            ImGui.SetNextWindowPos(
                new Vector2(viewport.WorkSize.X - windowWidth - offsetX, viewport.WorkSize.Y * 0.2f),
                ImGuiCond.Always
            );
            ImGui.SetNextWindowSize(
                new Vector2(windowWidth, viewport.WorkSize.Y * 0.6f),
                ImGuiCond.Always
            );
            ImGui.SetNextWindowBgAlpha(0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

            var flags = ImGuiWindowFlags.NoTitleBar
                      | ImGuiWindowFlags.NoResize
                      | ImGuiWindowFlags.NoScrollbar
                      | ImGuiWindowFlags.NoInputs
                      | ImGuiWindowFlags.NoMove
                      | ImGuiWindowFlags.NoSavedSettings
                      | ImGuiWindowFlags.NoFocusOnAppearing;

            if (!ImGui.Begin("##effectshud_hud", flags))
            {
                ImGui.PopStyleVar(2);
                ImGui.End();
                return true;
            }
            ImGui.PopStyleVar(2);

            foreach (var pair in effects)
            {
                EffectClientData ecd = pair.Value;
                if (ecd.duration <= 0 && !ecd.infinite) continue;

                int texId = GetOrLoadTexture(ecd.typeId);
                if (texId == 0) continue;

                ImGui.Image(texId, new Vector2(iconSize, iconSize));

                if (!ecd.infinite)
                {
                    int totalSec = Math.Max(0, (int)ecd.duration);
                    string timeText = $"{totalSec / 60}:{totalSec % 60:D2}";

                    ImGui.SetCursorPosX(padding);
                    ImGui.TextColored(new Vector4(0.9f, 0.85f, 1f, 1f), timeText);
                }
                else
                {
                    ImGui.SetCursorPosX(padding);
                    ImGui.TextColored(new Vector4(0.9f, 0.85f, 1f, 1f), "∞");
                }

                ImGui.Spacing();
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
