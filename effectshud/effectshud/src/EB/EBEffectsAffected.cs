using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace effectshud.src
{
    public class EBEffectsAffected : EntityBehavior
    {
        public Dictionary<string, Effect> activeEffects = new Dictionary<string, Effect>();
        public Dictionary<string, EffectClientData> onlyClientsActiveEffects = new Dictionary<string, EffectClientData>();
        HashSet<string> effectsToRemove = new HashSet<string>();
        ITreeAttribute effectsTree;
        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            Converters = new JsonConverter[] { new EffectJsonConverter() }
        };
        public bool needUpdate { get; set; } = false;
        float accum = 0;
        private effectshud _mod;
        private effectshud Mod => _mod ??= entity.Api.ModLoader.GetModSystem<effectshud>();
        public void serialize()
        {
            List<SerializedEffect> sel = new List<SerializedEffect>();
            foreach(var it in activeEffects)
            {
                sel.Add(new SerializedEffect
                {
                    typeId = it.Key,
                    data = JsonConvert.SerializeObject(it.Value, settings)
                });
            }
            effectsTree.SetString("activeEffectsData", JsonConvert.SerializeObject(sel));
            entity.WatchedAttributes.MarkPathDirty("activeEffects");
        }
        public void deserialize()
        {
            if (effectsTree.HasAttribute("activeEffectsData"))
            {
                var tmp = JsonConvert.DeserializeObject<List<SerializedEffect>>(effectsTree.GetString("activeEffectsData"));
                foreach(var it in tmp)
                {
                    if (!Mod.effects.TryGetValue(it.typeId, out Type ourType))
                        continue;

                    var tmpE = JsonConvert.DeserializeObject(it.data, ourType, settings) as Effect;
                    if (tmpE == null)
                        continue;

                    activeEffects[it.typeId] = tmpE;
                }
            }
            foreach (var it in activeEffects.Values)
            {
                it.entity = entity;
            }
        }
        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            if (entity.Api.Side == EnumAppSide.Client)
            {
                return;
            }
           
            base.Initialize(properties, attributes);
            effectsTree = entity.WatchedAttributes.GetTreeAttribute("activeEffects");
            
            if (effectsTree == null)
            {
                entity.WatchedAttributes.SetAttribute("activeEffects", effectsTree = new TreeAttribute());
                serialize();
            }
            else
            {
                deserialize();
            }
            entity.GetBehavior<EntityBehaviorHealth>().onDamaged += OnShouldEntityReceiveDamage;
            //SendAllEffectsToClient();

        }
        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            if (entity.Api.Side == EnumAppSide.Client)
            {
                return;
            }
            serialize();
        }
        public EBEffectsAffected(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "affectedByEffects";
        }
        internal double Now { get { return entity.Api.World.Calendar.TotalDays; } }
        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);
            
            if (entity.Api.Side == EnumAppSide.Server) {
                double now = Now;
                accum += deltaTime;
                if (accum > Mod.config.TICK_EVERY_SECONDS)
                {
                    accum = 0;
                    
                    foreach (var effect in activeEffects)
                    {
                        if (effect.Value.ExpireTimestampInDays < now || effect.Value.ExpireTick <= effect.Value.TickCounter)
                        {
                            effectsToRemove.Add(effect.Key);
                            effect.Value.OnExpire();
                        }
                        else
                        {
                            if (!effect.Value.infinite)
                            {
                                effect.Value.TickCounter++;
                            }                     
                            effect.Value.OnTick();
                        }
                    }
                    if (effectsToRemove.Count > 0)
                    {
                        foreach (var it in effectsToRemove)
                        {
                            activeEffects.Remove(it);
                        }
                        SendEffectToClient(null, effectsToRemove);
                        effectsToRemove.Clear();
                    }                  
                }
            }
            else
            {
                accum += deltaTime;
                if (accum > 0.5f)
                {
                    float elapsed = accum;
                    accum = 0;

                    foreach (var effect in onlyClientsActiveEffects.ToArray())
                    {
                        if (!effect.Value.infinite)
                        {
                            effect.Value.duration -= elapsed;
                            if (effect.Value.duration < 0)
                                onlyClientsActiveEffects.Remove(effect.Key);
                        }
                    }
                }
            }
        }
        
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            foreach (var it in activeEffects.Values.ToArray())
            {
                if(it.OnDeath())
                {
                    this.effectsToRemove.Add(it.effectTypeId);
                }
            }

            if (this.needUpdate)
            {
                SendEffectToClient(null, this.effectsToRemove);
            }
            this.effectsToRemove.Clear();
            needUpdate = false;
            //base.OnEntityDeath(damageSourceForDeath);
            //remove effects which not stay after death
        }

        private EffectClientData CreateEffectClientData(Effect effect)
        {
            float remainingSeconds;
            if (effect.infinite)
            {
                remainingSeconds = 0;
            }
            else if (effect.ExpireTimestampInDays == double.PositiveInfinity)
            {
                remainingSeconds = (float)((effect.ExpireTick - effect.TickCounter) * Mod.config.TICK_EVERY_SECONDS);
            }
            else
            {
                var cal = entity.Api.World.Calendar;
                double remainingDays = effect.ExpireTimestampInDays - Now;
                remainingSeconds = (float)(remainingDays * 86400.0 / (cal.SpeedOfTime * cal.CalendarSpeedMul));
            }

            return new EffectClientData
            {
                typeId = effect.effectTypeId,
                duration = remainingSeconds,
                tier = effect.Tier,
                infinite = effect.infinite,
                positive = effect.positive
            };
        }

        private void SendIfNeedsUpdate()
        {
            if (needUpdate)
            {
                SendAllEffectsToClient();
                needUpdate = false;
            }
        }

        private void SendPacket(EffectsSyncPacket packet, IServerPlayer ownerPlayer)
        {
            Mod.serverChannel.SendPacket(packet, ownerPlayer);

            // Invisibility must be mirrored to NEARBY clients too (they own the render-hide via invisiblePlayers),
            // not just the bearer. This fires on BOTH apply (effect in effectsToAddOrUpdate) and removal
            // (typeIdsToRemove) — previously only removal was broadcast, so others never hid a freshly-invisible
            // player and kept rendering him while he was invisible to himself.
            bool affectsInvisibility =
                packet.typeIdsToRemove?.Contains(EffectTypeIds.Invisibility) == true
                || packet.effectsToAddOrUpdate?.Any(e => e.typeId == EffectTypeIds.Invisibility) == true;
            if (affectsInvisibility && effectshud.ServerSideApi != null)
            {
                foreach (var it in effectshud.ServerSideApi.World.GetPlayersAround(entity.ServerPos.XYZ, 128, 128))
                {
                    if (it != ownerPlayer)
                        Mod.serverChannel.SendPacket(packet, it as IServerPlayer);
                }
            }
        }

        public void SendAllEffectsToClient()
        {
            var ownerPlayer = (entity as EntityPlayer)?.Player as IServerPlayer;
            if (ownerPlayer == null) return;

            var effectData = new List<EffectClientData>();
            foreach (var it in activeEffects.Values)
                effectData.Add(CreateEffectClientData(it));

            SendPacket(new EffectsSyncPacket
            {
                playerUID = ownerPlayer.PlayerUID,
                effectsToAddOrUpdate = effectData
            }, ownerPlayer);
        }

        /// <summary>Sends this entity's full effect list to ONE specific client. Used to catch a player up on
        /// already-active effects of others (e.g. an existing invisibility) right after they join — the normal
        /// apply-time broadcast happened before they were connected, so they'd otherwise render an invisible player.</summary>
        public void SendAllEffectsToPlayer(IServerPlayer recipient)
        {
            var ownerPlayer = (entity as EntityPlayer)?.Player as IServerPlayer;
            if (ownerPlayer == null || recipient == null) return;

            var effectData = new List<EffectClientData>();
            foreach (var it in activeEffects.Values)
                effectData.Add(CreateEffectClientData(it));

            Mod.serverChannel.SendPacket(new EffectsSyncPacket
            {
                playerUID = ownerPlayer.PlayerUID,
                effectsToAddOrUpdate = effectData
            }, recipient);
        }

        public void SendEffectToClient(Effect ef, HashSet<string> typeIdsToRemove = null)
        {
            var ownerPlayer = (entity as EntityPlayer)?.Player as IServerPlayer;
            if (ownerPlayer == null) return;

            SendPacket(new EffectsSyncPacket
            {
                playerUID = ownerPlayer.PlayerUID,
                effectsToAddOrUpdate = ef != null ? new List<EffectClientData> { CreateEffectClientData(ef) } : null,
                typeIdsToRemove = typeIdsToRemove
            }, ownerPlayer);
        }

        public bool AddEffect(Effect ef)
        {
            ApplyEffect(ef);
            if (ef.ExpireTick != 0)
                SendEffectToClient(ef);
            return true;
        }

        public void AddEffects(IEnumerable<Effect> effects)
        {
            var toSync = new List<EffectClientData>();
            foreach (var ef in effects)
            {
                ApplyEffect(ef);
                if (ef.ExpireTick != 0)
                    toSync.Add(CreateEffectClientData(ef));
            }
            if (toSync.Count == 0) return;

            var ownerPlayer = (entity as EntityPlayer)?.Player as IServerPlayer;
            if (ownerPlayer == null) return;
            SendPacket(new EffectsSyncPacket
            {
                playerUID = ownerPlayer.PlayerUID,
                effectsToAddOrUpdate = toSync
            }, ownerPlayer);
        }

        private void ApplyEffect(Effect ef)
        {
            if (activeEffects.TryGetValue(ef.effectTypeId, out Effect oldEffect))
            {
                oldEffect.OnStack(ef);
            }
            else
            {
                ef.entity = entity;
                activeEffects.Add(ef.effectTypeId, ef);
                ef.OnStart();
            }
        }
        public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {
            foreach (var it in activeEffects.Values)
            {
                it.DidAttack(source, targetEntity, ref handled);
            }
            SendIfNeedsUpdate();
        }

        public float OnShouldEntityReceiveDamage(float damage, DamageSource dmgSource)
        {
            foreach (var it in activeEffects.Values)
            {
                it.OnShouldEntityReceiveDamage(ref damage, dmgSource);
            }
            SendIfNeedsUpdate();
            return damage;
        }

        public override void OnEntityRevive()
        {
            foreach (var it in activeEffects.Values)
            {
                it.OnRevive();
            }
            SendIfNeedsUpdate();
        }

        public bool HasEffect(string effectId)
        {
            return this.activeEffects.ContainsKey(effectId);
        }

        public bool TryGetEffect(string effectId, out Effect effect)
        {
            return this.activeEffects.TryGetValue(effectId, out effect);
        }

        public int GetEffectTier(string effectId)
        {
            return this.activeEffects.TryGetValue(effectId, out Effect effect) 
                                                            ? effect.Tier 
                                                            : -1;
        }
    }
}
