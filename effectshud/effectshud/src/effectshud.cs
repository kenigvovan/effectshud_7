using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using effectshud.src.gui;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace effectshud.src
{
    public class effectshud: ModSystem
    {
        public static effectshud Instance { get; private set; }
        public static ICoreClientAPI ClientSideApi { get; private set; }
        public static ICoreServerAPI ServerSideApi { get; private set; }
        public const string harmonyID = "effectshud.Patches";
        public static ConcurrentDictionary<string, byte> invisiblePlayers;
        public static EffectsSelectionGuiImGui effectsSelectionGuiImGui { get; set; }
        public HUDEffectsImGui effectsHUDImGui;

        public Harmony harmonyInstance;
        public Dictionary<string, Type> effects;
        internal IClientNetworkChannel clientChannel;
        public Dictionary<string, bool> effectsPosNeg;
        public Dictionary<string, bool> effectsShouldBeRendered;
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
            invisiblePlayers = new ConcurrentDictionary<string, byte>();
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

            harmonyInstance.Patch(typeof(Vintagestory.GameContent.GuiDialogWorldMap).GetMethod("OnGuiClosed"), postfix: new HarmonyMethod(typeof(HudOffsetPatch).GetMethod("Postfix_Map_OnGuiClosed")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.GuiDialogWorldMap).GetMethod("OnGuiOpened"), postfix: new HarmonyMethod(typeof(HudOffsetPatch).GetMethod("Postfix_Map_OnGuiOpened")));

            harmonyInstance.Patch(typeof(Vintagestory.Client.NoObf.HudElementCoordinates).GetMethod("OnGuiClosed"), postfix: new HarmonyMethod(typeof(HudOffsetPatch).GetMethod("Postfix_CoordsHUD_OnGuiClosed")));
            harmonyInstance.Patch(typeof(Vintagestory.Client.NoObf.HudElementCoordinates).GetMethod("OnGuiOpened"), postfix: new HarmonyMethod(typeof(HudOffsetPatch).GetMethod("Postfix_CoordsHUD_OnGuiOpened")));

            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityShapeRenderer).GetMethod("DoRender3DOpaqueBatched"), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_DoRender3DOpaqueBatched")));
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityShapeRenderer).GetMethod("DoRender2D"), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_DoRender2D")));
            harmonyInstance.Patch(typeof(Vintagestory.Server.ServerPackets).GetMethod("GetFullEntityPacket"), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_GetFullEntityPacket")));
            //harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntitySkinnableShapeRenderer).GetMethod("TesselateShape"), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_TesselateShape")));
           
            api.RegisterEntityBehaviorClass("affectedByEffects", typeof(EBEffectsAffected));
            clientChannel = api.Network.RegisterChannel("effectshud");
            clientChannel.RegisterMessageType(typeof(EffectsSyncPacket));
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
                                if (it.typeId.Equals(EffectTypeIds.Invisibility))
                                    invisiblePlayers.TryAdd(packet.playerUID, 0);

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
                            if (packet.typeIdsToRemove.Contains(EffectTypeIds.Invisibility))
                                invisiblePlayers.TryRemove(packet.playerUID, out _);

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
        }
        public static bool RegisterClientEffectData(string typeId, bool positive = true, bool shouldBeRendered = true)
        {
            Instance.effectsPosNeg.Add(typeId, positive);
            Instance.effectsShouldBeRendered.Add(typeId, shouldBeRendered);
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
            // harmonyInstance.Patch(typeof(Vintagestory.API.Common.EntityAgent).GetMethod("ReceiveDamage"), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_On_ReceiveDamage")));
            serverChannel = ServerSideApi.Network.RegisterChannel("effectshud");
            serverChannel.RegisterMessageType(typeof(EffectsSyncPacket));

            base.StartServerSide(api);

            ServerSideApi.ChatCommands.Create("ef").HandleWith(addDefaultEffect)
               .RequiresPlayer().RequiresPrivilege(Privilege.controlserver).IgnoreAdditionalArgs();

            api.RegisterEntityBehaviorClass("affectedByEffects", typeof(EBEffectsAffected));
            //api.Event.PlayerDisconnect += onPlayerLeft;
            ServerSideApi.Event.PlayerNowPlaying += (serverPlayer) =>
            {
                ServerSideApi.Event.RegisterCallback((dt =>
                {
                    EBEffectsAffected ebea = serverPlayer.Entity.GetBehavior<EBEffectsAffected>();
                    if (ebea == null)
                    {
                        return;
                    }
                    ebea.SendAllEffectsToClient();
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
            serverChannel = null;

            invisiblePlayers?.Clear();
            invisiblePlayers = null;
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
