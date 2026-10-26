using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace effectshud.src.gui
{
    /// <summary>
    /// HUD settings window. Every change is applied to the config immediately and saved. While "Move HUD" is on,
    /// this dialog also drags the HUD with the mouse (the HUD itself receives no mouse events).
    /// </summary>
    public class HUDSettingsDialog : GuiDialog
    {
        private const string ComposerKey = "effectshud-settings";
        private const double LabelW = 200;
        private const double CtrlX = 210;
        private const double CtrlW = 200;
        private const double RowH = 30;

        private readonly Config _config;
        private bool _dragging;
        private double _grabDx, _grabDy;

        public HUDSettingsDialog(ICoreClientAPI capi) : base(capi)
        {
            _config = capi.ModLoader.GetModSystem<effectshud>().config;
        }

        public override string ToggleKeyCombinationCode => null;
        public override bool PrefersUngrabbedMouse => true;

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Compose();
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            HUDEffects.EditMode = false;
            _dragging = false;
        }

        private void Compose()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            var compo = capi.Gui.CreateCompo(ComposerKey, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("effectshud:hud-settings-title"), () => TryClose())
                .BeginChildElements(bgBounds);

            double y = GuiStyle.TitleBarHeight + 5;

            AddSwitchRow(compo, ref y, "move", "hud-settings-move-mode", HUDEffects.EditMode, on => HUDEffects.EditMode = on);
            AddSwitchRow(compo, ref y, "horizontal", "hud-settings-horizontal", _config.HUD_HORIZONTAL, on => { _config.HUD_HORIZONTAL = on; Save(); });
            AddSwitchRow(compo, ref y, "timer", "hud-settings-show-timer", _config.HUD_SHOW_TIMER, on => { _config.HUD_SHOW_TIMER = on; Save(); });
            AddSwitchRow(compo, ref y, "blink", "hud-settings-blink-expiring", _config.HUD_BLINK_EXPIRING, on => { _config.HUD_BLINK_EXPIRING = on; Save(); });
            AddSwitchRow(compo, ref y, "border", "hud-settings-posneg-border", _config.HUD_POSNEG_BORDER, on => { _config.HUD_POSNEG_BORDER = on; Save(); });
            AddSwitchRow(compo, ref y, "reverse", "hud-settings-reverse-order", _config.HUD_REVERSE_ORDER, on => { _config.HUD_REVERSE_ORDER = on; Save(); });
            AddSwitchRow(compo, ref y, "growup", "hud-settings-grow-up", _config.HUD_GROW_UP, on => { _config.HUD_GROW_UP = on; ResetPosition(); });
            AddSwitchRow(compo, ref y, "growleft", "hud-settings-grow-left", _config.HUD_GROW_LEFT, on => { _config.HUD_GROW_LEFT = on; ResetPosition(); });

            AddSliderRow(compo, ref y, "iconsize", "hud-settings-icon-size", v => _config.EFFECT_ICON_SIZE = v);
            AddSliderRow(compo, ref y, "bgalpha", "hud-settings-bg-alpha", v => _config.HUD_BG_ALPHA = v / 100f);
            AddSliderRow(compo, ref y, "iconalpha", "hud-settings-icon-alpha", v => _config.HUD_ICON_ALPHA = v / 100f);
            AddSliderRow(compo, ref y, "timerscale", "hud-settings-timer-scale", v => _config.HUD_TIMER_SCALE = v / 100f);

            AddDropDownRow(compo, ref y, "hud-settings-filter",
                new[] { "hud-settings-filter-all", "hud-settings-filter-positive", "hud-settings-filter-negative" },
                _config.HUD_FILTER, v => _config.HUD_FILTER = v);
            AddDropDownRow(compo, ref y, "hud-settings-sort",
                new[] { "hud-settings-sort-default", "hud-settings-sort-time-asc", "hud-settings-sort-time-desc" },
                _config.HUD_SORT, v => _config.HUD_SORT = v);

            y += 5;
            compo.AddButton(Lang.Get("effectshud:hud-settings-reset-position"), () => { ResetPosition(); return true; },
                ElementBounds.Fixed(0, y, LabelW, RowH), EnumButtonStyle.Normal, "reset");

            SingleComposer?.Dispose();
            SingleComposer = compo.EndChildElements().Compose();

            SingleComposer.GetSlider("iconsize").SetValues((int)_config.EFFECT_ICON_SIZE, 16, 128, 1, "px");
            SingleComposer.GetSlider("bgalpha").SetValues((int)Math.Round(_config.HUD_BG_ALPHA * 100), 0, 100, 1, "%");
            SingleComposer.GetSlider("iconalpha").SetValues((int)Math.Round(_config.HUD_ICON_ALPHA * 100), 0, 100, 1, "%");
            SingleComposer.GetSlider("timerscale").SetValues((int)Math.Round(_config.HUD_TIMER_SCALE * 100), 50, 300, 10, "%");
        }

        private static void AddLabel(GuiComposer compo, double y, string langKey)
        {
            compo.AddStaticText(Lang.Get("effectshud:" + langKey), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 4, LabelW, RowH - 4));
        }

        private static void AddSwitchRow(GuiComposer compo, ref double y, string key, string langKey, bool value, Action<bool> onToggle)
        {
            AddLabel(compo, y, langKey);
            compo.AddSwitch(onToggle, ElementBounds.Fixed(CtrlX, y, 25, 25), key);
            compo.GetSwitch(key).SetValue(value);
            y += RowH;
        }

        private void AddSliderRow(GuiComposer compo, ref double y, string key, string langKey, Action<int> onChange)
        {
            AddLabel(compo, y, langKey);
            compo.AddSlider(v => { onChange(v); Save(); return true; }, ElementBounds.Fixed(CtrlX, y + 5, CtrlW, 20), key);
            y += RowH;
        }

        private void AddDropDownRow(GuiComposer compo, ref double y, string langKey, string[] optionKeys, int selected, Action<int> onPick)
        {
            AddLabel(compo, y, langKey);
            string[] values = new string[optionKeys.Length];
            string[] names = new string[optionKeys.Length];
            for (int i = 0; i < optionKeys.Length; i++)
            {
                values[i] = i.ToString();
                names[i] = Lang.Get("effectshud:" + optionKeys[i]);
            }
            compo.AddDropDown(values, names, selected, (code, sel) => { onPick(int.Parse(code)); Save(); },
                ElementBounds.Fixed(CtrlX, y, CtrlW, 25));
            y += RowH;
        }

        private void ResetPosition()
        {
            _config.HUD_X = -1f;
            _config.HUD_Y = -1f;
            Save();
        }

        private void Save() => capi.StoreModConfig(_config, "effectshud.json");

        // -------- dragging the HUD (only while "Move HUD" is on) --------

        private static HUDEffects Hud => effectshud.Instance?.effectsHUD;

        public override void OnMouseDown(MouseEvent args)
        {
            var hud = Hud;
            if (HUDEffects.EditMode && hud != null && hud.BoxContains(args.X, args.Y)
                && !(SingleComposer?.Bounds.PointInside(args.X, args.Y) ?? false))
            {
                _dragging = true;
                _grabDx = args.X - hud.BoxX;
                _grabDy = args.Y - hud.BoxY;
                args.Handled = true;
                return;
            }
            base.OnMouseDown(args);
        }

        public override void OnMouseMove(MouseEvent args)
        {
            var hud = Hud;
            if (_dragging && hud != null)
            {
                hud.MoveBoxTo((float)(args.X - _grabDx), (float)(args.Y - _grabDy));
                args.Handled = true;
                return;
            }
            base.OnMouseMove(args);
        }

        public override void OnMouseUp(MouseEvent args)
        {
            if (_dragging)
            {
                _dragging = false;
                Save();
                args.Handled = true;
                return;
            }
            base.OnMouseUp(args);
        }
    }
}
