using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using effectshud.src.gui;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
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
        public static EffectsSelectionGuiImGui effectsSelectionGuiImGui { get; set; }
        public HUDEffectsImGui effectsHUDImGui;
        public HUDSettingsImGui hudSettingsImGui;

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
            api.Input.RegisterHotKey("effectsghud", "Show effects hud", GlKeys.L, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("effectsghud", new ActionConsumable<KeyCombination>(this.OnHotKeySkillDialog));

            api.Input.RegisterHotKey("effectsghudgui", "Gui effects selection", GlKeys.L, HotkeyType.GUIOrOtherControls, false, false, true);
            api.Input.SetHotKeyHandler("effectsghudgui", new ActionConsumable<KeyCombination>(this.OnHotKeyEffectsSelectionGui));

            api.Input.RegisterHotKey("effectshudmove", "HUD Settings", GlKeys.L, HotkeyType.GUIOrOtherControls, true, false, false);
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

            effectsHUDImGui = new HUDEffectsImGui(ClientSideApi);
            effectsHUDImGui.Open();

            // TEMP diagnostic: ".efinvis" prints the invisibility render flag of every loaded player entity,
            // to verify the WatchedAttributes flag actually reached this client. Remove once invis sync is confirmed.
            api.ChatCommands.Create("efinvis").HandleWith((args) =>
            {
                var sb = new System.Text.StringBuilder();
                foreach (var ent in ClientSideApi.World.LoadedEntities.Values)
                {
                    if (!(ent is EntityPlayer eplr)) continue;
                    sb.AppendLine($"{eplr.GetName()} (id {ent.EntityId}): {ent.WatchedAttributes.GetBool(DefaultEffects.InvisibilityEffect.InvisibleAttr)}");
                }
                if (sb.Length == 0) sb.Append("no player entities loaded");
                return TextCommandResult.Success(sb.ToString());
            });
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
        public static TextCommandResult addDefaultEffect(TextCommandCallingArgs args)
        {
            TextCommandResult tcr = new TextCommandResult();
            tcr.Status = EnumCommandStatus.Success;

            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player.WorldData.CurrentGameMode != EnumGameMode.Creative) return tcr;
            if (args.RawArgs.Length < 4) return tcr;

            ICoreServerAPI sapi = player.Entity.Api as ICoreServerAPI;
            effectshud mod = sapi.ModLoader.GetModSystem<effectshud>();

            mod.effects.TryGetValue(args.RawArgs[0], out Type effectType);
            if (effectType == null) return tcr;

            if (!int.TryParse(args.RawArgs[1], out int durationMin)) return tcr;
            if (!int.TryParse(args.RawArgs[2], out int tier)) return tcr;

            foreach (var it in sapi.World.AllOnlinePlayers)
            {
                if (it.PlayerName.Equals(args.RawArgs[3]))
                {
                    Effect ef = (Effect)Activator.CreateInstance(effectType);
                    ef.SetExpiryInRealMinutes(durationMin);
                    ef.Tier = tier;
                    ef.positive = mod.effectsPosNeg.TryGetValue(ef.effectTypeId, out bool posneg) ? posneg : true;
                    ApplyEffectOnEntity(it.Entity, ef);
                    tcr.StatusMessage = "effectshud:effect-set-to-player-tier-duration";
                    tcr.MessageParams = new object[] { effectType.Name, it.PlayerName, tier, durationMin };
                    break;
                }
            }
            return tcr;
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

            ServerSideApi.ChatCommands.Create("ef").HandleWith(addDefaultEffect)
               .RequiresPlayer().RequiresPrivilege(Privilege.controlserver).IgnoreAdditionalArgs();

            api.RegisterEntityBehaviorClass("affectedByEffects", typeof(EBEffectsAffected));
            //api.Event.PlayerDisconnect += onPlayerLeft;
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
        public void onPlayerLeft(IServerPlayer byPlayer)
        {
            EBEffectsAffected ebea = byPlayer.Entity.GetBehavior<EBEffectsAffected>();
            if(ebea == null)
            {
                return;
            }
            ebea.serialize();
        }
        public static bool RegisterEntityEffect(string typeId, Type effectType)
        {
            Instance.effects.Add(typeId, effectType);
            return true;
        }
        public static bool ApplyEffectOnEntity(Entity entity, Effect effect)
        {
            EBEffectsAffected ebea = entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null) return false;
            return ebea.AddEffect(effect);
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
            if (effectsSelectionGuiImGui == null)
            {
                effectsSelectionGuiImGui = new EffectsSelectionGuiImGui(ClientSideApi);
            }
            if (effectsSelectionGuiImGui.IsOpened)
            {
                effectsSelectionGuiImGui.Close();
            }
            else
            {
                effectsSelectionGuiImGui.Open();
            }
            return true;
        }
        private bool OnHotKeyHUDSettings(KeyCombination comb)
        {
            if (hudSettingsImGui == null)
                hudSettingsImGui = new HUDSettingsImGui(ClientSideApi);
            if (hudSettingsImGui.IsOpened)
                hudSettingsImGui.Close();
            else
                hudSettingsImGui.Open();
            return true;
        }
        private bool OnHotKeySkillDialog(KeyCombination comb)
        {
            if (effectsHUDImGui != null)
            {
                effectsHUDImGui.Dispose();
                effectsHUDImGui = null;
            }
            else
            {
                effectsHUDImGui = new HUDEffectsImGui(ClientSideApi);
                effectsHUDImGui.Open();
            }
            return true;
        }
        public override void Dispose()
        {
            base.Dispose();
            harmonyInstance?.UnpatchAll(harmonyID);
            ClientSideApi = null;
            ServerSideApi = null;
            harmonyInstance = null;


            effects = null;

            clientChannel = null;
            effectsPosNeg = null;
            effectsShouldBeRendered = null;
            effectIcons = null;
            serverChannel = null;

            hudSettingsImGui?.Dispose();
            hudSettingsImGui = null;
            effectsSelectionGuiImGui?.Dispose();
            effectsSelectionGuiImGui = null;
            effectsHUDImGui?.Dispose();
            effectsHUDImGui = null;
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
