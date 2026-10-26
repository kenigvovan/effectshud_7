using effectshud.src.DefaultEffects;
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
        /// <summary>Writes active effects into the entity's WatchedAttributes so they survive a save — called on every
        /// add/remove/expiry/death, not just on despawn, so a server crash doesn't lose them.</summary>
        public void serialize()
        {
            if (effectsTree == null) return; // client side / not initialized
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
            // Reconcile the invisibility render flag with the actual effect state: OnStart never runs for effects
            // restored by deserialize (and never ran at all for saves from before the flag existed), and this also
            // clears a stale flag left behind if the effect vanished without OnExpire.
            bool invis = HasEffect(EffectTypeIds.Invisibility);
            if (entity.WatchedAttributes.GetBool(InvisibilityEffect.InvisibleAttr) != invis)
                entity.WatchedAttributes.SetBool(InvisibilityEffect.InvisibleAttr, invis);
            var health = entity.GetBehavior<EntityBehaviorHealth>();
            if (health != null) health.onDamaged += OnShouldEntityReceiveDamage;
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
                        if (!effect.Value.infinite
                            && (effect.Value.ExpireTimestampInDays < now || effect.Value.ExpireTick <= effect.Value.TickCounter))
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
                        serialize();
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
                serialize();
            }
            this.effectsToRemove.Clear();
            needUpdate = false;
            //base.OnEntityDeath(damageSourceForDeath);
            //remove effects which not stay after death
        }

        private EffectClientData CreateEffectClientData(Effect effect)
        {
            float remainingSeconds = effect.infinite ? 0 : (float)effect.RemainingSeconds();

            return new EffectClientData
            {
                typeId = effect.effectTypeId,
                duration = remainingSeconds,
                tier = effect.Tier,
                infinite = effect.infinite,
                positive = effect.positive,
                magnitude = effect.DisplayMagnitude()
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

        // Effect sync goes only to the bearer (for his HUD). Invisibility visibility for OTHER clients is not
        // packet-based anymore: it's a WatchedAttributes flag the engine syncs to everyone who sees the entity.
        private void SendPacket(EffectsSyncPacket packet, IServerPlayer ownerPlayer)
        {
            Mod.serverChannel.SendPacket(packet, ownerPlayer);
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
            // Sync the effect that is actually active after stacking, not the incoming one: stacking a weaker
            // effect onto a stronger one keeps the stronger one's tier and timing.
            Effect active = ApplyEffect(ef);
            serialize();
            if (ShouldSync(active))
                SendEffectToClient(active);
            return true;
        }

        public void AddEffects(IEnumerable<Effect> effects)
        {
            var activeToSync = new Dictionary<string, Effect>();
            foreach (var ef in effects)
            {
                Effect active = ApplyEffect(ef);
                if (ShouldSync(active))
                    activeToSync[active.effectTypeId] = active;
            }
            serialize();
            var toSync = activeToSync.Values.Select(CreateEffectClientData).ToList();
            if (toSync.Count == 0) return;

            var ownerPlayer = (entity as EntityPlayer)?.Player as IServerPlayer;
            if (ownerPlayer == null) return;
            SendPacket(new EffectsSyncPacket
            {
                playerUID = ownerPlayer.PlayerUID,
                effectsToAddOrUpdate = toSync
            }, ownerPlayer);
        }

        /// <summary>Removes one active effect (calling its OnExpire) and syncs the owner's HUD.</summary>
        public bool RemoveEffect(string typeId)
        {
            if (!activeEffects.TryGetValue(typeId, out Effect ef)) return false;
            ef.OnExpire();
            activeEffects.Remove(typeId);
            serialize();
            SendEffectToClient(null, new HashSet<string> { typeId });
            return true;
        }

        /// <summary>Removes all active effects (calling each OnExpire) and syncs the owner's HUD. Returns the count.</summary>
        public int RemoveAllEffects()
        {
            var removed = new HashSet<string>(activeEffects.Keys);
            foreach (var ef in activeEffects.Values.ToArray())
            {
                ef.OnExpire();
            }
            activeEffects.Clear();
            serialize();
            if (removed.Count > 0) SendEffectToClient(null, removed);
            return removed.Count;
        }

        private static bool ShouldSync(Effect ef) => ef.ExpireTick != 0 || ef.infinite;

        /// <summary>Applies or stacks the effect and returns the one that is active afterwards.</summary>
        private Effect ApplyEffect(Effect ef)
        {
            if (activeEffects.TryGetValue(ef.effectTypeId, out Effect oldEffect))
            {
                oldEffect.OnStack(ef);
                return oldEffect;
            }
            ef.entity = entity;
            activeEffects.Add(ef.effectTypeId, ef);
            ef.OnStart();
            return ef;
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

            // Let the attacker's effects react to the damage they caused (e.g. vampirism).
            if (damage > 0f && dmgSource != null && dmgSource.Type != EnumDamageType.Heal)
            {
                var attacker = dmgSource.GetCauseEntity();
                var attackerEbea = attacker != null && attacker != entity ? attacker.GetBehavior<EBEffectsAffected>() : null;
                if (attackerEbea != null)
                {
                    foreach (var it in attackerEbea.activeEffects.Values.ToArray())
                    {
                        it.OnDealtDamage(damage, entity, dmgSource);
                    }
                    attackerEbea.SendIfNeedsUpdate();
                }
            }
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
