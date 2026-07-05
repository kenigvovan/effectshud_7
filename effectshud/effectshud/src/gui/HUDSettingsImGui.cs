using System.Numerics;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using VSImGui.API;

namespace effectshud.src.gui
{
    public class HUDSettingsImGui : ImGuiDialogWindow
    {
        private Config _config;
        private ICoreClientAPI _api;

        public bool IsOpened => Opened;

        public HUDSettingsImGui(ICoreClientAPI api)
            : base(api, Lang.Get("effectshud:hud-settings-title"), "HUDSettingsDialog", true, ImGuiWindowFlags.AlwaysAutoResize)
        {
            _api = api;
            _config = api.ModLoader.GetModSystem<effectshud>().config;
        }

        protected override CallbackGUIStatus Draw(float deltaSeconds)
        {
            var status = base.Draw(deltaSeconds);
            return status == CallbackGUIStatus.Closed
                ? CallbackGUIStatus.Closed
                : CallbackGUIStatus.GrabMouse;
        }

        protected override bool OnDraw()
        {
            bool changed = false;

            bool editMode = HUDEffectsImGui.EditMode;
            if (ImGui.Checkbox(Lang.Get("effectshud:hud-settings-move-mode"), ref editMode))
                HUDEffectsImGui.EditMode = editMode;

            bool horizontal = _config.HUD_HORIZONTAL;
            if (ImGui.Checkbox(Lang.Get("effectshud:hud-settings-horizontal"), ref horizontal))
            {
                _config.HUD_HORIZONTAL = horizontal;
                changed = true;
            }

            int iconSize = (int)_config.EFFECT_ICON_SIZE;
            if (ImGui.SliderInt(Lang.Get("effectshud:hud-settings-icon-size"), ref iconSize, 16, 128))
            {
                _config.EFFECT_ICON_SIZE = iconSize;
                changed = true;
            }

            float bgAlpha = _config.HUD_BG_ALPHA;
            if (ImGui.SliderFloat(Lang.Get("effectshud:hud-settings-bg-alpha"), ref bgAlpha, 0f, 1f))
            {
                _config.HUD_BG_ALPHA = bgAlpha;
                changed = true;
            }

            float iconAlpha = _config.HUD_ICON_ALPHA;
            if (ImGui.SliderFloat(Lang.Get("effectshud:hud-settings-icon-alpha"), ref iconAlpha, 0f, 1f))
            {
                _config.HUD_ICON_ALPHA = iconAlpha;
                changed = true;
            }

            bool showTimer = _config.HUD_SHOW_TIMER;
            if (ImGui.Checkbox(Lang.Get("effectshud:hud-settings-show-timer"), ref showTimer))
            {
                _config.HUD_SHOW_TIMER = showTimer;
                changed = true;
            }

            float timerScale = _config.HUD_TIMER_SCALE;
            if (ImGui.SliderFloat(Lang.Get("effectshud:hud-settings-timer-scale"), ref timerScale, 0.5f, 3f))
            {
                _config.HUD_TIMER_SCALE = timerScale;
                changed = true;
            }

            bool reverseOrder = _config.HUD_REVERSE_ORDER;
            if (ImGui.Checkbox(Lang.Get("effectshud:hud-settings-reverse-order"), ref reverseOrder))
            {
                _config.HUD_REVERSE_ORDER = reverseOrder;
                changed = true;
            }

            bool growUp = _config.HUD_GROW_UP;
            if (ImGui.Checkbox(Lang.Get("effectshud:hud-settings-grow-up"), ref growUp))
            {
                _config.HUD_GROW_UP = growUp;
                _config.HUD_X = -1f;
                _config.HUD_Y = -1f;
                changed = true;
            }

            bool growLeft = _config.HUD_GROW_LEFT;
            if (ImGui.Checkbox(Lang.Get("effectshud:hud-settings-grow-left"), ref growLeft))
            {
                _config.HUD_GROW_LEFT = growLeft;
                _config.HUD_X = -1f;
                _config.HUD_Y = -1f;
                changed = true;
            }

            ImGui.Separator();

            ImGui.Text(Lang.Get("effectshud:hud-settings-filter"));
            if (ImGui.RadioButton($"{Lang.Get("effectshud:hud-settings-filter-all")}##filter0", _config.HUD_FILTER == 0)) { _config.HUD_FILTER = 0; changed = true; }
            ImGui.SameLine();
            if (ImGui.RadioButton($"{Lang.Get("effectshud:hud-settings-filter-positive")}##filter1", _config.HUD_FILTER == 1)) { _config.HUD_FILTER = 1; changed = true; }
            ImGui.SameLine();
            if (ImGui.RadioButton($"{Lang.Get("effectshud:hud-settings-filter-negative")}##filter2", _config.HUD_FILTER == 2)) { _config.HUD_FILTER = 2; changed = true; }

            ImGui.Text(Lang.Get("effectshud:hud-settings-sort"));
            if (ImGui.RadioButton($"{Lang.Get("effectshud:hud-settings-sort-default")}##sort0", _config.HUD_SORT == 0)) { _config.HUD_SORT = 0; changed = true; }
            ImGui.SameLine();
            if (ImGui.RadioButton($"{Lang.Get("effectshud:hud-settings-sort-time-asc")}##sort1", _config.HUD_SORT == 1)) { _config.HUD_SORT = 1; changed = true; }
            ImGui.SameLine();
            if (ImGui.RadioButton($"{Lang.Get("effectshud:hud-settings-sort-time-desc")}##sort2", _config.HUD_SORT == 2)) { _config.HUD_SORT = 2; changed = true; }

            ImGui.Separator();

            if (ImGui.Button(Lang.Get("effectshud:hud-settings-reset-position")))
            {
                _config.HUD_X = -1f;
                _config.HUD_Y = -1f;
                changed = true;
            }

            if (changed)
                _api.StoreModConfig(_config, "effectshud.json");

            return true;
        }
    }
}
