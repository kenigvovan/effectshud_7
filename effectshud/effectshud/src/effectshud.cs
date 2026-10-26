using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using effectshud.src.gui;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace effectshud.src
{
    public class effectshud: ModSystem
    {
        public static effectshud Instance { get; private set; }
        public static ICoreClientAPI ClientSideApi { get; private set; }
        public static ICoreServerAPI ServerSideApi { get; private set; }
        public const string harmonyID = "effectshud.Patches";
        public static EffectsSelectionDialog effectsSelectionDialog { get; set; }
        public HUDEffects effectsHUD;
        public HUDSettingsDialog hudSettingsDialog;
        private NightVisionRenderer nightVisionRenderer;

        public Harmony harmonyInstance;
        public Dictionary<string, Type> effects;
        internal IClientNetworkChannel clientChannel;
        public Dictionary<string, bool> effectsPosNeg;
        public Dictionary<string, bool> effectsShouldBeRendered;
        /// <summary>Optional per-effect HUD icon override (any mod/domain). Falls back to
        /// <c>effectshud:textures/effects/&lt;typeId&gt;.png</c> when absent.</summary>
        public Dictionary<string, AssetLocation> effectIcons;
        internal IServerNetworkChannel serverChannel;
        public Config config;

        /// <summary>Consumer-contributed sections shown on the character-dialog "Effects" tab, above the effect list.
        /// See <see cref="ICharacterSheetSection"/>. Read live at render time, so registration order/timing is free.</summary>
        public List<ICharacterSheetSection> characterSheetSections;
        /// <summary>Optional per-effect display-name resolver (typeId → localized name), for consumer effect ids.
        /// The tab tries this first, then the <c>effectshud:&lt;typeId&gt;</c> lang key, then a humanized fallback.</summary>
        public Dictionary<string, Func<string>> effectDisplayNames;
        /// <summary>Optional override for the character tab's title (else <c>effectshud:charactertab-title</c>).</summary>
        public Func<string> characterTabTitle;
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            Instance = this;
            if (effects == null)
            {
                effects = new Dictionary<string, Type>();
            }
            effectsPosNeg = new Dictionary<string, bool>();
            effectsShouldBeRendered = new Dictionary<string, bool>();
            effectIcons = new Dictionary<string, AssetLocation>();
            characterSheetSections = new List<ICharacterSheetSection>();
            effectDisplayNames = new Dictionary<string, Func<string>>();
            loadConfig(api);
            ScanAndRegisterEffects();
        }
        private void ScanAndRegisterEffects()
        {
            foreach (var type in GetType().Assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<EffectRegistrationAttribute>();
                if (attr == null || !type.IsSubclassOf(typeof(Effect))) continue;

                effects[attr.TypeId] = type;
                effectsPosNeg[attr.TypeId] = attr.Positive;
                effectsShouldBeRendered[attr.TypeId] = attr.ShouldBeRendered;
            }
        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            ClientSideApi = api;
            base.StartClientSide(api);
            harmonyInstance = new Harmony(harmonyID);
            api.Input.RegisterHotKey("effectsghud", Lang.Get("effectshud:hotkey-toggle-hud"), GlKeys.L, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("effectsghud", new ActionConsumable<KeyCombination>(this.OnHotKeySkillDialog));

            api.Input.RegisterHotKey("effectsghudgui", Lang.Get("effectshud:hotkey-effects-selection"), GlKeys.L, HotkeyType.GUIOrOtherControls, false, false, true);
            api.Input.SetHotKeyHandler("effectsghudgui", new ActionConsumable<KeyCombination>(this.OnHotKeyEffectsSelectionGui));

            api.Input.RegisterHotKey("effectshudmove", Lang.Get("effectshud:hotkey-hud-settings"), GlKeys.L, HotkeyType.GUIOrOtherControls, true, false, false);
            api.Input.SetHotKeyHandler("effectshudmove", new ActionConsumable<KeyCombination>(this.OnHotKeyHUDSettings));

            harmonyInstance.Patch(typeof(Vintagestory.GameContent.GuiDialogWorldMap).GetMethod("OnGuiClosed"), postfix: new HarmonyMethod(typeof(HudOffsetPatch).GetMethod("Postfix_Map_OnGuiClosed")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.GuiDialogWorldMap).GetMethod("OnGuiOpened"), postfix: new HarmonyMethod(typeof(HudOffsetPatch).GetMethod("Postfix_Map_OnGuiOpened")));

            harmonyInstance.Patch(typeof(Vintagestory.Client.NoObf.HudElementCoordinates).GetMethod("OnGuiClosed"), postfix: new HarmonyMethod(typeof(HudOffsetPatch).GetMethod("Postfix_CoordsHUD_OnGuiClosed")));
            harmonyInstance.Patch(typeof(Vintagestory.Client.NoObf.HudElementCoordinates).GetMethod("OnGuiOpened"), postfix: new HarmonyMethod(typeof(HudOffsetPatch).GetMethod("Postfix_CoordsHUD_OnGuiOpened")));

            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityShapeRenderer).GetMethod("DoRender3DOpaqueBatched"), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_DoRender3DOpaqueBatched")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityShapeRenderer).GetMethod("DoRender2D"), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_DoRender2D")));
            // Held items render via the player renderer's RenderHeldItem override (protected) — patch that exact
            // method so held weapons/tools are hidden on invisible players too (the batched mesh patch misses them).
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityPlayerShapeRenderer).GetMethod("RenderHeldItem", BindingFlags.Instance | BindingFlags.NonPublic), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_RenderHeldItem")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityBehaviorNameTag).GetMethod("OnRenderFrame"), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_NameTag_OnRenderFrame")));
            //harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntitySkinnableShapeRenderer).GetMethod("TesselateShape"), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_TesselateShape")));
           
            api.RegisterEntityBehaviorClass("affectedByEffects", typeof(EBEffectsAffected));
            clientChannel = api.Network.RegisterChannel("effectshud");
            clientChannel.RegisterMessageType(typeof(EffectsSyncPacket));
            clientChannel.RegisterMessageType(typeof(OpenCharSelPacket));
            clientChannel.SetMessageHandler<OpenCharSelPacket>((packet) =>
            {
                var charSys = ClientSideApi.ModLoader.GetModSystem<CharacterSystem>();
                if (charSys == null) return;
                new GuiDialogCreateCharacter(ClientSideApi, charSys).PrepAndOpen();
            });
            clientChannel.SetMessageHandler<EffectsSyncPacket>((packet) =>
            {
                var player = ClientSideApi.World.PlayerByUid(packet.playerUID);
                if(player?.Entity != null)
                {
                    var ebef = player.Entity.GetBehavior<EBEffectsAffected>();
                    if(ebef != null)
                    {
                        if (packet.effectsToAddOrUpdate != null)
                        {
                            foreach (var it in packet.effectsToAddOrUpdate)
                            {
                                if (ebef.onlyClientsActiveEffects.TryGetValue(it.typeId, out EffectClientData ecd))
                                {
                                    ecd.tier = it.tier;
                                    ecd.infinite = it.infinite;
                                    ecd.duration = it.duration;
                                    ecd.typeId = it.typeId;
                                    ecd.positive = it.positive;
                                }
                                else
                                {
                                    ebef.onlyClientsActiveEffects[it.typeId] = it;
                                }
                            }
                        }
                        if (packet.typeIdsToRemove != null)
                        {
                            foreach (var effToRemove in packet.typeIdsToRemove.ToArray())
                            {
                                ebef.onlyClientsActiveEffects.Remove(effToRemove);
                            }
                        }
                    }
                }

            });

            effectsHUD = new HUDEffects(ClientSideApi);
            effectsHUD.TryOpen();

            nightVisionRenderer = new NightVisionRenderer(api);
            api.Event.RegisterRenderer(nightVisionRenderer, EnumRenderStage.Before, "effectshud-nightvision");
        }
        public static bool RegisterClientEffectData(string typeId, bool positive = true, bool shouldBeRendered = true, AssetLocation icon = null)
        {
            // Indexer (not Add) so re-registration / both-sides registration doesn't throw on a duplicate key.
            Instance.effectsPosNeg[typeId] = positive;
            Instance.effectsShouldBeRendered[typeId] = shouldBeRendered;
            if (icon != null) Instance.effectIcons[typeId] = icon;
            return true;
        }

        /// <summary>One-call registration for consumer mods: registers the effect TYPE (so it can be created and
        /// deserialized) plus its client HUD data (positive/negative, whether to render, and an optional custom icon
        /// from any domain). Call from your mod's Start on both sides. Define the effect by subclassing
        /// <see cref="effectshud.src.Effect"/> and overriding OnStart/OnExpire (set/clear your own stat key).</summary>
        public static bool RegisterEffect(string typeId, Type effectType, bool positive = true, bool shouldBeRendered = true, AssetLocation icon = null)
        {
            Instance.effects[typeId] = effectType;
            RegisterClientEffectData(typeId, positive, shouldBeRendered, icon);
            return true;
        }
        /// <summary>Register a section for the character-dialog "Effects" tab (rendered above the effect list).
        /// Call from your mod's client start. Safe to call before the tab exists — sections are read live.</summary>
        public static void RegisterCharacterSheetSection(ICharacterSheetSection section)
        {
            if (section != null) Instance?.characterSheetSections?.Add(section);
        }

        /// <summary>Register a localized display name for one effect typeId, used by the character tab's effect list
        /// (and available to any effectshud UI). The provider is a callback so it can re-localize on language change.</summary>
        public static void RegisterEffectDisplayName(string typeId, Func<string> localizedName)
        {
            if (!string.IsNullOrEmpty(typeId) && localizedName != null && Instance?.effectDisplayNames != null)
                Instance.effectDisplayNames[typeId] = localizedName;
        }

        /// <summary>Override the character tab's title (defaults to lang key <c>effectshud:charactertab-title</c>).</summary>
        public static void SetCharacterTabTitle(Func<string> title)
        {
            if (Instance != null) Instance.characterTabTitle = title;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            ServerSideApi = api;            
             harmonyInstance = new Harmony(harmonyID);
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityBehaviorTemporalStabilityAffected).GetMethod("OnGameTick"), transpiler: new HarmonyMethod(typeof(TemporalChargePatch).GetMethod("Prefix_EntityBehaviorTemporalStabilityAffected")));
            // Attach the effects behavior to all living mobs at runtime so effects work on them (server-only mechanic).
            harmonyInstance.Patch(typeof(Vintagestory.API.Common.Entities.Entity).GetMethod("Initialize"), postfix: new HarmonyMethod(typeof(AttachEffectsBehaviorPatch).GetMethod("Postfix_Initialize")));
            // harmonyInstance.Patch(typeof(Vintagestory.API.Common.EntityAgent).GetMethod("ReceiveDamage"), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_On_ReceiveDamage")));
            serverChannel = ServerSideApi.Network.RegisterChannel("effectshud");
            serverChannel.RegisterMessageType(typeof(EffectsSyncPacket));
            serverChannel.RegisterMessageType(typeof(OpenCharSelPacket));

            base.StartServerSide(api);

            EffectsCommands.Register(api);

            api.RegisterEntityBehaviorClass("affectedByEffects", typeof(EBEffectsAffected));
            ServerSideApi.Event.PlayerNowPlaying += (serverPlayer) =>
            {
                // Own-HUD catch-up only. Invisibility of OTHERS needs no catch-up: it lives in the entity's
                // WatchedAttributes, which the engine syncs to every client that sees the entity.
                ServerSideApi.Event.RegisterCallback((dt =>
                {
                    EBEffectsAffected ebea = serverPlayer.Entity?.GetBehavior<EBEffectsAffected>();
                    ebea?.SendAllEffectsToClient();
                }), 1000
                );
            };
        }
        public static bool RegisterEntityEffect(string typeId, Type effectType)
        {
            Instance.effects[typeId] = effectType;
            return true;
        }
        public static bool ApplyEffectOnEntity(Entity entity, Effect effect)
        {
            EBEffectsAffected ebea = entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null) return false;
            return ebea.AddEffect(effect);
        }

        /// <summary>Removes one effect (calling its OnExpire) and syncs the owner's HUD. False if it wasn't active.</summary>
        public static bool RemoveEffectFromEntity(Entity entity, string typeId)
        {
            EBEffectsAffected ebea = entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null) return false;
            return ebea.RemoveEffect(typeId);
        }

        public static bool ApplyEffectsOnEntity(Entity entity, IEnumerable<Effect> effects)
        {
            EBEffectsAffected ebea = entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null) return false;
            ebea.AddEffects(effects);
            return true;
        }
        private bool OnHotKeyEffectsSelectionGui(KeyCombination comb)
        {
            effectsSelectionDialog ??= new EffectsSelectionDialog(ClientSideApi);
            if (effectsSelectionDialog.IsOpened())
                effectsSelectionDialog.TryClose();
            else
                effectsSelectionDialog.TryOpen();
            return true;
        }
        private bool OnHotKeyHUDSettings(KeyCombination comb)
        {
            hudSettingsDialog ??= new HUDSettingsDialog(ClientSideApi);
            if (hudSettingsDialog.IsOpened())
                hudSettingsDialog.TryClose();
            else
                hudSettingsDialog.TryOpen();
            return true;
        }
        private bool OnHotKeySkillDialog(KeyCombination comb)
        {
            if (effectsHUD != null)
            {
                effectsHUD.TryClose();
                effectsHUD.Dispose();
                effectsHUD = null;
            }
            else
            {
                effectsHUD = new HUDEffects(ClientSideApi);
                effectsHUD.TryOpen();
            }
            return true;
        }
        public override void Dispose()
        {
            base.Dispose();
            if (nightVisionRenderer != null)
            {
                ClientSideApi?.Event.UnregisterRenderer(nightVisionRenderer, EnumRenderStage.Before);
                nightVisionRenderer = null;
            }
            harmonyInstance?.UnpatchAll(harmonyID);
            ClientSideApi = null;
            ServerSideApi = null;
            harmonyInstance = null;


            effects = null;

            clientChannel = null;
            effectsPosNeg = null;
            effectsShouldBeRendered = null;
            effectIcons = null;
            characterSheetSections = null;
            effectDisplayNames = null;
            characterTabTitle = null;
            serverChannel = null;

            hudSettingsDialog?.Dispose();
            hudSettingsDialog = null;
            effectsSelectionDialog?.Dispose();
            effectsSelectionDialog = null;
            effectsHUD?.Dispose();
            effectsHUD = null;
            Instance = null;
        }
        private void loadConfig(ICoreAPI api)
        {
            config = null;
            try
            {
                config = api.LoadModConfig<Config>("effectshud.json");
            }
            catch (Exception e)
            {
                api.Logger.Warning("EffectsHUD: Failed to load config: {0}", e.Message);
            }
            if(config == null)
            {
                config = new Config();
            }
            api.StoreModConfig<Config>(config, "effectshud.json");

        }
        public double Now { get { return ServerSideApi?.World.Calendar.TotalDays ?? 0; } }
    }
}
