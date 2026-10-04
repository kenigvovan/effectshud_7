using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using effectshud.src.gui;

namespace effectshud.src
{
    [HarmonyPatch]
    public class HudOffsetPatch
    {
        public static void Postfix_Map_OnGuiClosed(GuiDialogWorldMap __instance)
        {
            updateOffset();
        }

        public static void Postfix_Map_OnGuiOpened(GuiDialogWorldMap __instance)
        {
            updateOffset();
        }

        public static void Postfix_CoordsHUD_OnGuiClosed(HudElementCoordinates __instance)
        {
            updateOffset();
        }

        public static void Postfix_CoordsHUD_OnGuiOpened(HudElementCoordinates __instance)
        {
            effectshud.ClientSideApi.Event.RegisterCallback((dt =>
            {
                updateOffset();
            }), 1 * 1000);
        }

        public static void updateOffset()
        {
            double startPointMap = -1;
            double startPointCoords = -1;
            lock (effectshud.ClientSideApi.OpenedGuis)
            {
                foreach (var it in effectshud.ClientSideApi.OpenedGuis)
                {
                    if ((it as GuiDialog).DebugName.Equals("GuiDialogWorldMap"))
                    {
                        if ((it as GuiDialog).SingleComposer.Bounds.Alignment == EnumDialogArea.RightTop)
                        {
                            startPointMap = (it as GuiDialog).SingleComposer.Bounds.absInnerHeight;
                            continue;
                        }
                    }
                    if ((it as GuiDialog).DebugName.Equals("HudElementCoordinates"))
                    {
                        if ((it as GuiDialog).SingleComposer.Bounds.Alignment == EnumDialogArea.RightTop)
                        {
                            startPointCoords = (it as GuiDialog).SingleComposer.Bounds.absInnerHeight;
                            continue;
                        }
                    }
                }

                if (startPointCoords != -1 && startPointMap != -1)
                {
                    HUDEffectsImGui.glOffset = (int)(startPointCoords + startPointMap) + 32;
                }
                else if (startPointCoords != -1)
                {
                    HUDEffectsImGui.glOffset = (int)(startPointCoords) + 32;
                }
                else if (startPointMap != -1)
                {
                    HUDEffectsImGui.glOffset = (int)(startPointMap) + 32;
                }
                else
                {
                    HUDEffectsImGui.glOffset = 64;
                }
            }
        }
    }
}
