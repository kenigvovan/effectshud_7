using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace effectshud.src.gui
{
    /// <summary>Admin window: pick an effect, an online player, tier and duration, then apply via <c>/ef</c>.</summary>
    public class EffectsSelectionDialog : GuiDialog
    {
        private const string ComposerKey = "effectshud-selection";
        private const double LabelW = 180;
        private const double CtrlX = 190;
        private const double CtrlW = 220;
        private const double RowH = 30;

        private string _effectCode;
        private string _playerName;
        private int _tier = 1;
        private int _duration = 5;

        public EffectsSelectionDialog(ICoreClientAPI capi) : base(capi) { }

        public override string ToggleKeyCombinationCode => null;
        public override bool PrefersUngrabbedMouse => true;

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Compose(); // refresh effect and player lists every time
        }

        private void Compose()
        {
            string[] effectCodes = capi.ModLoader.GetModSystem<effectshud>().effectsPosNeg.Keys.ToArray();
            string[] effectNames = effectCodes.Select(code => Lang.Get("effectshud:" + code)).ToArray();
            string[] players = capi.World.AllOnlinePlayers.Select(p => p.PlayerName).OrderBy(n => n).ToArray();

            if (_effectCode == null || !effectCodes.Contains(_effectCode)) _effectCode = effectCodes.FirstOrDefault();
            if (_playerName == null || !players.Contains(_playerName)) _playerName = capi.World.Player?.PlayerName ?? players.FirstOrDefault();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            var compo = capi.Gui.CreateCompo(ComposerKey, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("effectshud:effects-selection-gui-title-bar"), () => TryClose())
                .BeginChildElements(bgBounds);

            double y = GuiStyle.TitleBarHeight + 5;

            AddLabel(compo, y, "gui-type-effect");
            if (effectCodes.Length > 0)
            {
                compo.AddDropDown(effectCodes, effectNames, Array.IndexOf(effectCodes, _effectCode),
                    (code, sel) => _effectCode = code, ElementBounds.Fixed(CtrlX, y, CtrlW, 25));
            }
            else
            {
                compo.AddStaticText(Lang.Get("effectshud:gui-no-effects"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(CtrlX, y + 4, CtrlW, RowH - 4));
            }
            y += RowH;

            AddLabel(compo, y, "gui-type-playername");
            if (players.Length > 0)
            {
                compo.AddDropDown(players, players, Math.Max(0, Array.IndexOf(players, _playerName)),
                    (code, sel) => _playerName = code, ElementBounds.Fixed(CtrlX, y, CtrlW, 25));
            }
            else
            {
                compo.AddStaticText(Lang.Get("effectshud:gui-no-players"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(CtrlX, y + 4, CtrlW, RowH - 4));
            }
            y += RowH;

            AddLabel(compo, y, "gui-type-tier");
            compo.AddSlider(v => { _tier = v; return true; }, ElementBounds.Fixed(CtrlX, y + 5, CtrlW, 20), "tier");
            y += RowH;

            AddLabel(compo, y, "gui-type-duration-minutes");
            compo.AddSlider(v => { _duration = v; return true; }, ElementBounds.Fixed(CtrlX, y + 5, CtrlW, 20), "duration");
            y += RowH + 5;

            bool canApply = effectCodes.Length > 0 && players.Length > 0;
            compo.AddButton(Lang.Get("effectshud:gui-apply"), () => { SendCommand("add {0} {1} {2} {3}", _effectCode, _duration, _tier, _playerName); return true; },
                ElementBounds.Fixed(0, y, 95, RowH), EnumButtonStyle.Normal, "apply");
            compo.AddButton(Lang.Get("effectshud:gui-remove"), () => { SendCommand("remove {0} {1}", _effectCode, _playerName); return true; },
                ElementBounds.Fixed(105, y, 95, RowH), EnumButtonStyle.Normal, "remove");
            compo.AddButton(Lang.Get("effectshud:gui-clear"), () => { SendCommand("clear {0}", _playerName); return true; },
                ElementBounds.Fixed(210, y, 95, RowH), EnumButtonStyle.Normal, "clear");
            compo.AddButton(Lang.Get("effectshud:gui-close"), () => TryClose(),
                ElementBounds.Fixed(315, y, 95, RowH), EnumButtonStyle.Normal, "close");

            SingleComposer?.Dispose();
            SingleComposer = compo.EndChildElements().Compose();

            SingleComposer.GetSlider("tier").SetValues(_tier, 1, 10, 1);
            SingleComposer.GetSlider("duration").SetValues(_duration, 1, 120, 1, " min");
            SingleComposer.GetButton("apply").Enabled = canApply;
            SingleComposer.GetButton("remove").Enabled = canApply;
            SingleComposer.GetButton("clear").Enabled = players.Length > 0;
        }

        private static void AddLabel(GuiComposer compo, double y, string langKey)
        {
            compo.AddStaticText(Lang.Get("effectshud:" + langKey), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 4, LabelW, RowH - 4));
        }

        private void SendCommand(string format, params object[] args)
        {
            if (string.IsNullOrEmpty(_playerName)) return;
            capi.SendChatMessage("/ef " + string.Format(format, args));
        }
    }
}
