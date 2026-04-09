using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace effectshud.src.gui
{
    public class EffectsSelectionGui: GuiDialog
    {
        public override string ToggleKeyCombinationCode => "effectshudselection";
        string collectedStringValuePlayerName = "";
        int collectedIntValueTier = 0;
        int collectedIntValueDuration = 0;
        string collectedEffectCode = "";
        private int currentY = 20;
        public static ClientEventManager clientEventManager;
        public EffectsSelectionGui(ICoreClientAPI capi) : base(capi)
        {
            SetupDialog();

            clientEventManager = (ClientEventManager)typeof(ClientMain).GetField("eventManager").GetValue(capi.World as ClientMain);
        }
        private void SetupDialog()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds effectNameBounds = ElementBounds.FixedPos(EnumDialogArea.LeftTop, 0, 20)
              .WithFixedWidth(160)
              .WithFixedHeight(40);

            ElementBounds playerNameTextBounds = ElementBounds.FixedPos(EnumDialogArea.LeftTop, 0, 70)
              .WithFixedWidth(160)
              .WithFixedHeight(20);

            ElementBounds playerNameBounds = ElementBounds.FixedPos(EnumDialogArea.LeftTop, 0, 100)
              .WithFixedWidth(160)
              .WithFixedHeight(40);

            ElementBounds effectTierTextBounds = ElementBounds.FixedPos(EnumDialogArea.LeftTop, 0, 150)
              .WithFixedWidth(160)
              .WithFixedHeight(20);

            ElementBounds effectTierBounds = ElementBounds.FixedPos(EnumDialogArea.LeftTop, 0, 180)
              .WithFixedWidth(160)
              .WithFixedHeight(40);

            ElementBounds effectDurationTextBounds = ElementBounds.FixedPos(EnumDialogArea.LeftTop, 0, 230)
             .WithFixedWidth(180)
             .WithFixedHeight(40);

            ElementBounds effectDurationBounds = ElementBounds.FixedPos(EnumDialogArea.LeftTop, 0, 260)
              .WithFixedWidth(160)
              .WithFixedHeight(40);

            ElementBounds applyBounds = ElementBounds.FixedPos(EnumDialogArea.LeftTop, 0, 320)
              .WithFixedWidth(60)
              .WithFixedHeight(40);

            bgBounds.WithChildren(effectNameBounds, playerNameBounds, effectTierBounds, effectDurationBounds, applyBounds);

            SingleComposer = capi.Gui.CreateCompo("NewCityCreationDialog-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("effectshud:effects-selection-gui-title-bar"), () => OnTitleBarCloseClicked())
                .BeginChildElements(bgBounds);
            SingleComposer.AddDropDown(GetEffectsCodes(), GetEffectsNames(), 0, didSelectEntity, effectNameBounds, "effectnamedropdown");

            this.collectedEffectCode = GetEffectsCodes()[0];

            SingleComposer.AddStaticText(Lang.Get("effectshud:gui-type-playername"), CairoFont.WhiteSmallishText(), playerNameTextBounds);

            SingleComposer.AddTextInput(playerNameBounds, (name) => collectedStringValuePlayerName = name, null, "playerName");

            SingleComposer.AddStaticText(Lang.Get("effectshud:gui-type-tier"), CairoFont.WhiteSmallishText(), effectTierTextBounds);

            SingleComposer.AddNumberInput(effectTierBounds, (tier) =>
            {
                try
                {
                    collectedIntValueTier = int.Parse(tier);
                }
                catch (Exception e)
                {
                    collectedIntValueTier = 0;
                }
            }, null, "effectTier");

            SingleComposer.AddStaticText(Lang.Get("effectshud:gui-type-duration-minutes"), CairoFont.WhiteSmallishText(), effectDurationTextBounds);

            SingleComposer.AddNumberInput(effectDurationBounds, (duration) => {
                try
                {
                    collectedIntValueDuration = int.Parse(duration);
                }
                catch (Exception e)
                {
                    collectedIntValueDuration = 0;
                }
            }, null, "effectDuration");

            SingleComposer.AddButton(Lang.Get("effectshud:gui-apply"), onClickApplyButton, applyBounds, CairoFont.WhiteMediumText())
                .EndChildElements()
                .Compose();
        }
        public string [] GetEffectsCodes()
        {
            return effectshud.Instance.effectsPosNeg.Keys.ToArray();
        }
        public string[] GetEffectsNames()
        {
            string[] names = new string[effectshud.Instance.effectsPosNeg.Keys.Count];
            int i = 0;
            foreach (var it in effectshud.Instance.effectsPosNeg.Keys)
            {
                names[i] = Lang.Get("effectshud:" + it);
                i++;
            }
            return names;
        }
        private void didSelectEntity(string code, bool selected)
        {
            if (selected)
            {
                collectedEffectCode = code;
            }
            else
            {
                collectedEffectCode = "";
            }

        }
        public bool onClickApplyButton()
        {
            if (this.collectedStringValuePlayerName == "")
            {
                capi.ShowChatMessage("No player selected.");
            }

            if (this.collectedEffectCode != "")
            {
                clientEventManager.TriggerNewClientChatLine(GlobalConstants.CurrentChatGroup, string.Format("/ef {0} {1} {2} {3}", collectedEffectCode, collectedIntValueDuration, collectedIntValueTier, collectedStringValuePlayerName), Vintagestory.API.Common.EnumChatType.Macro, "");
                //collectedEffectCode = "";
            }
            return true;
        }
        private bool OnTitleBarCloseClicked()
        {
            TryClose();           
            return true;
        }
        public override bool OnEscapePressed()
        {
            return OnTitleBarCloseClicked();
        }
    }
}
