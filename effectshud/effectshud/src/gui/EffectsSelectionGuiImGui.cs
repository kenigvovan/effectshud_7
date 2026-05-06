using System.Linq;
using System.Numerics;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using VSImGui.API;

namespace effectshud.src.gui
{
    public class EffectsSelectionGuiImGui : ImGuiDialogWindow
    {
        private string[] _effectCodes = System.Array.Empty<string>();
        private string[] _effectNames = System.Array.Empty<string>();
        private int _selectedEffectIndex = 0;
        private string _playerName = "";
        private int _effectTier = 1;
        private int _effectDuration = 5;

        public EffectsSelectionGuiImGui(ICoreClientAPI api)
            : base(api,
                Lang.Get("effectshud:effects-selection-gui-title-bar"),
                "EffectsSelectionDialog",
                true,
                ImGuiWindowFlags.AlwaysAutoResize)
        {
            RefreshEffectsList();
        }

        public bool IsOpened => Opened;

        protected override CallbackGUIStatus Draw(float deltaSeconds)
        {
            var status = base.Draw(deltaSeconds);
            return status == CallbackGUIStatus.Closed
                ? CallbackGUIStatus.Closed
                : CallbackGUIStatus.GrabMouse;
        }

        public void RefreshEffectsList()
        {
            _effectCodes = ClientApi.ModLoader.GetModSystem<effectshud>().effectsPosNeg.Keys.ToArray();
            _effectNames = _effectCodes
                .Select(code => Lang.Get("effectshud:" + code))
                .ToArray();
        }

        protected override bool OnDraw()
        {
            ImGui.Text(Lang.Get("effectshud:gui-type-effect") ?? "Effect:");
            if (_effectCodes.Length > 0)
            {
                ImGui.SetNextItemWidth(200);
                ImGui.Combo("##effect", ref _selectedEffectIndex, _effectNames, _effectNames.Length);
            }
            else
            {
                ImGui.TextDisabled("No effects registered");
            }

            ImGui.Separator();

            ImGui.Text(Lang.Get("effectshud:gui-type-playername") ?? "Player name:");
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("##playerName", ref _playerName, 64);

            ImGui.Text(Lang.Get("effectshud:gui-type-tier") ?? "Tier:");
            ImGui.SetNextItemWidth(200);
            ImGui.SliderInt("##tier", ref _effectTier, 1, 10);

            ImGui.Text(Lang.Get("effectshud:gui-type-duration-minutes") ?? "Duration (minutes):");
            ImGui.SetNextItemWidth(200);
            ImGui.SliderInt("##duration", ref _effectDuration, 1, 120, "%d min");

            ImGui.Separator();

            bool canApply = _playerName.Length > 0 && _effectCodes.Length > 0;

            if (!canApply) ImGui.BeginDisabled();
            if (ImGui.Button(Lang.Get("effectshud:gui-apply") ?? "Apply", new Vector2(95, 0)))
            {
                ApplyEffect();
            }
            if (!canApply) ImGui.EndDisabled();

            ImGui.SameLine();

            if (ImGui.Button("Close", new Vector2(95, 0)))
            {
                return false;
            }

            if (!canApply && _playerName.Length == 0)
            {
                ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), "Enter player name");
            }

            return true;
        }

        private void ApplyEffect()
        {
            if (_selectedEffectIndex < 0 || _selectedEffectIndex >= _effectCodes.Length) return;
            if (_playerName.Length == 0) return;

            string code = _effectCodes[_selectedEffectIndex];
            ClientApi.SendChatMessage(string.Format("/ef {0} {1} {2} {3}",
                code, _effectDuration, _effectTier, _playerName));
        }
    }
}
