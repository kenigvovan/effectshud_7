using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using effectshud.src.DefaultEffects;
using effectshud.src.gui;
using HarmonyLib;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace effectshud.src
{
    public class effectshud: ModSystem
    {
        public static effectshud Instance { get; private set; }
        public const string harmonyID = "effectshud.Patches";
        public static ConcurrentDictionary<string, byte> invisiblePlayers;
        public static EffectsSelectionGui effectsSelectionGui { get; set; }

        public ICoreServerAPI sapi;
        public ICoreClientAPI capi;
        public Harmony harmonyInstance;
        public List<TrackedEffect> trackedEffects;
        public Dictionary<string, Type> effects;
        public bool showHUD = true;
        internal IClientNetworkChannel clientChannel;
        public Dictionary<string, EffectClientData> clientsActiveEffects;
        public HUDEffects effectsHUD;
        public Dictionary<string, bool> effectsPosNeg;
        public Dictionary<string, bool> effectsShouldBeRendered;
        internal IServerNetworkChannel serverChannel;
        public bool redrawEffectPictures = true;
        public Config config;
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            Instance = this;
            trackedEffects = new List<TrackedEffect>();
            if (effects == null)
            {
                effects = new Dictionary<string, Type>();
            }
            clientsActiveEffects = new Dictionary<string, EffectClientData>();
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
            capi = api;
            base.StartClientSide(api);
            //var c =
               /* Environment.SetEnvironmentVariable("TEXTURE_DEBUG_DISPOSE", "1");
            var c = Environment.GetEnvironmentVariable("CAIRO_DEBUG_DISPOSE");*/
            api.Gui.RegisterDialog((GuiDialog)new HUDEffects((ICoreClientAPI)api));
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
                var player = capi.World.PlayerByUid(packet.playerUID);
                if(player?.Entity != null)
                {
                    var ebef = player.Entity.GetBehavior<EBEffectsAffected>();
                    if(ebef != null)
                    {
                        if (packet.currentEffectsData != null)
                        {
                            
                            foreach (var it in JsonConvert.DeserializeObject<List<EffectClientData>>(packet.currentEffectsData))
                            {
                                if(it.typeId.Equals(EffectTypeIds.Invisibility))
                                {
                                    invisiblePlayers.TryAdd(packet.playerUID, 0);
                                }
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
                                    effectsHUD?.CellsGrid?.AddEffectCell(it);                                    
                                }
                            }
                        }
                        if (packet.playerUID.Equals(capi.World.Player.PlayerUID))
                        {
                            redrawEffectPictures = true;
                        }
                        if (packet.typeIdsToRemove != null)
                        {
                            if(packet.typeIdsToRemove.Contains(EffectTypeIds.Invisibility))
                            {
                                invisiblePlayers.TryRemove(packet.playerUID, out _);
                            }
                            foreach (var effToRemove in packet.typeIdsToRemove.ToArray())
                            {
                                if (ebef.onlyClientsActiveEffects.TryGetValue(effToRemove, out EffectClientData ecd))
                                {
                                    effectsHUD?.CellsGrid?.RemoveEffectCell(ecd.typeId);
                                    ebef.onlyClientsActiveEffects.Remove(effToRemove);
                                }
                            }
                        }
                    }
                }

                effectsHUD?.ComposeGuis();
                if (packet?.typeIdsToRemove?.Count > 0 && effectsHUD != null)
                {                    
                    //effectsHUD?.ComposeGuis();
                }

                //effectsHUD = new HUDEffects(capi);
                /*if (showHUD && effectsHUD != null)
                {
                    effectsHUD.ComposeGuis();
                }*/
            });

            effectsHUD = new HUDEffects(capi);
            effectsHUD.TryOpen();
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
            if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                return tcr;
            }
            //effectname minutes tier targetname
            if(args.RawArgs.Length < 4)
            {
                return tcr;
            }
            Instance.effects.TryGetValue(args.RawArgs[0], out Type effectType);
            if(effectType == null)
            {
                return tcr;
            }
            int durationMin = 0;
            try
            {
                durationMin = int.Parse(args.RawArgs[1]);
            }
            catch(FormatException e)
            {
                return tcr;
            }
            int tier = 1;
            try
            {
                tier = int.Parse(args.RawArgs[2]);
            }
            catch (FormatException e)
            {
                return tcr;
            }

            foreach(var it in Instance.sapi.World.AllOnlinePlayers)
            {
                if(it.PlayerName.Equals(args.RawArgs[3]))
                {
                    Effect ef = (Effect)Activator.CreateInstance(effectType);
                    ef.SetExpiryInRealMinutes(durationMin);
                    ef.Tier = tier;
                    if(effectshud.Instance.effectsPosNeg.TryGetValue(ef.effectTypeId, out bool posneg))
                    {
                        ef.positive = posneg;
                    }
                    else
                    {
                        ef.positive = true;
                    }
                    ApplyEffectOnEntity(it.Entity, ef);
                    tcr.StatusMessage = "effectshud:effect-set-to-player-tier-duration";
                    tcr.MessageParams = new object[] {effectType.Name, it.PlayerName, tier, durationMin }; 
                    break;
                }
            }
            return tcr;
        }
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;            
             harmonyInstance = new Harmony(harmonyID);
            harmonyInstance.Patch(typeof(Vintagestory.GameContent.EntityBehaviorTemporalStabilityAffected).GetMethod("OnGameTick"), transpiler: new HarmonyMethod(typeof(TemporalChargePatch).GetMethod("Prefix_EntityBehaviorTemporalStabilityAffected")));
            // harmonyInstance.Patch(typeof(Vintagestory.API.Common.EntityAgent).GetMethod("ReceiveDamage"), prefix: new HarmonyMethod(typeof(InvisibilityRenderPatch).GetMethod("Prefix_On_ReceiveDamage")));
            base.StartServerSide(api);

            sapi.ChatCommands.Create("ef").HandleWith(addDefaultEffect)
               .RequiresPlayer().RequiresPrivilege(Privilege.controlserver).IgnoreAdditionalArgs();

            api.RegisterEntityBehaviorClass("affectedByEffects", typeof(EBEffectsAffected));
            //RegisterEntityEffect("vampirism", typeof(VampirismEffect));
            serverChannel = sapi.Network.RegisterChannel("effectshud");
            serverChannel.RegisterMessageType(typeof(EffectsSyncPacket));
            //api.Event.PlayerDisconnect += onPlayerLeft;
            sapi.Event.PlayerNowPlaying += (serverPlayer) =>
            {
                sapi.Event.RegisterCallback((dt =>
                {
                    EBEffectsAffected ebea = serverPlayer.Entity.GetBehavior<EBEffectsAffected>();
                    if (ebea == null)
                    {
                        return;
                    }
                    ebea.SendActiveEffectsToClient(null);
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
            if(ebea == null)
            {
                return false;
            }
            return ebea.AddEffect(effect);
        }
        private bool OnHotKeyEffectsSelectionGui(KeyCombination comb)
        {
            if (effectsSelectionGui == null)
            {
                effectsSelectionGui = new EffectsSelectionGui(capi);
            }
            if (effectsSelectionGui.IsOpened())
            {
                effectsSelectionGui.TryClose();
            }
            else
                effectsSelectionGui.TryOpen();
            return true;
        }
        private bool OnHotKeySkillDialog(KeyCombination comb)
        {
            showHUD = !showHUD;
            effectsHUD = null;
            lock (capi.OpenedGuis)
            {
                foreach (var it in capi.OpenedGuis)
                {
                    if (it is HUDEffects && !showHUD)
                    {
                        (it as HUDEffects).TryClose();
                        break;
                    }
                }
                if (showHUD)
                {
                    effectsHUD = new HUDEffects(capi);
                }
            }
            HudOffsetPatch.updateOffset();
            return true;
        }
        public static bool RegisterEffect(string watchedBranch, string effectWatchedName, bool showTime, string effectDurationWatchedName, string [] domainAndPath, Vintagestory.API.Common.Func<int, bool> needToShow)
        {
            AssetLocation tmpAL;
            AssetLocation [] tmpArr = new AssetLocation [domainAndPath.Length];
            for (int i = 0; i < domainAndPath.Length; i++)
            {
                try
                {
                    tmpAL = new AssetLocation(domainAndPath[i] + ".png");
                    tmpArr[i] = tmpAL;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            Instance.trackedEffects.Add(new TrackedEffect(tmpArr, showTime, watchedBranch, effectWatchedName, effectDurationWatchedName, needToShow));
            return true;
        }
        public override void Dispose()
        {
            base.Dispose();
            harmonyInstance?.UnpatchAll(harmonyID);
            sapi = null;
            capi = null;
            harmonyInstance = null;

            trackedEffects = null;
            effects = null;

            clientChannel = null;
            clientsActiveEffects = null;
            effectsPosNeg = null;
            effectsShouldBeRendered = null;
            serverChannel = null;

            invisiblePlayers?.Clear();
            invisiblePlayers = null;
            if (effectsSelectionGui != null)
            {
                effectsSelectionGui.TryClose();
                effectsSelectionGui.Dispose();
                effectsSelectionGui = null;
            }
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
        public double Now { get { return sapi?.World.Calendar.TotalDays ?? 0; } }
    }
}
