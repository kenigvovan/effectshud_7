using System;
using System.Reflection;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace effectshud.src
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public abstract class Effect
    {
        public int TickCounter = 0;
        protected int tier;
        public virtual int Tier 
        { 
            get => tier;
            set 
            {
                tier = value; 
            }
        }
        public int ExpireTick = 0;
        public double ExpireTimestampInDays = 0;
        public bool infinite = false;
        public bool positive = true;
        protected internal Entity entity; // protected so effect subclasses in OTHER mods can use it in OnStart/OnExpire/OnTick
        public string effectTypeId;
        public bool removedAfterDeath = true;
                              
        public Effect(int tier = 1, bool infinite = false, bool removedAfterDeath = true)
        {
            this.tier = tier;
            this.infinite = infinite;
            this.removedAfterDeath = removedAfterDeath;

            // Auto-set effectTypeId from attribute if not already set
            if (string.IsNullOrEmpty(effectTypeId))
            {
                var attr = GetType().GetCustomAttribute<EffectRegistrationAttribute>();
                if (attr != null)
                {
                    effectTypeId = attr.TypeId;
                }
            }
        }
        public virtual void OnStart() { }
        
        public virtual void OnStack(Effect otherEffect) 
        {
            if(this.tier > otherEffect.tier)
            {
                return;
            }
            if(this.tier == otherEffect.tier)
            {
                // Refresh, but never SHORTEN: keep whichever has more time left. Remaining = ExpireTick - TickCounter
                // (both relative to the same tick cadence). Otherwise a short re-application (e.g. a 2s invisibility
                // from a blink) would cut an already-running long one (e.g. a 40s stealth).
                if (otherEffect.ExpireTick - otherEffect.TickCounter > this.ExpireTick - this.TickCounter)
                {
                    this.ExpireTick = otherEffect.ExpireTick;
                    this.TickCounter = otherEffect.TickCounter;
                }
                return;
            }
            this.tier = otherEffect.tier;
            this.ExpireTick = otherEffect.ExpireTick;
            this.TickCounter = otherEffect.TickCounter;          
        }
       
        public virtual void OnExpire() { }
    
        
        public virtual void OnTick() { }
       
        public virtual void OnLeave() { }
        
        public virtual void OnJoin() { }
      
        public void SetExpiryInGameDays(double deltaDays)
        {
            ExpireTimestampInDays = effectshud.Instance.Now + deltaDays;
            ExpireTick = Int32.MaxValue;
        }

        public void SetExpiryInGameHours(double deltaHours)
        {
            ExpireTimestampInDays = effectshud.Instance.Now + deltaHours / 24.0;
            ExpireTick = Int32.MaxValue;
        }

        public void SetExpiryInGameMinutes(double deltaMinutes)
        {
            ExpireTimestampInDays = effectshud.Instance.Now + deltaMinutes / 24.0 / 60.0;
            ExpireTick = Int32.MaxValue;
        }

        public void SetExpiryInTicks(int deltaTicks)
        {
            ExpireTick = TickCounter + deltaTicks;
            ExpireTimestampInDays = double.PositiveInfinity;
        }

        public void SetExpiryInRealSeconds(int deltaSeconds)
        {
            SetExpiryInTicks((int)Math.Ceiling(deltaSeconds / effectshud.Instance.config.TICK_EVERY_SECONDS));
        }

        public void SetExpiryInRealMinutes(int deltaMinutes)
        {
            SetExpiryInRealSeconds(deltaMinutes * 60);
        }

        /*public void SetExpiryNever()
        {
            ExpireTimestampInDays = double.PositiveInfinity;
            ExpireTick = Int32.MaxValue;
        }*/

        public void SetExpiryImmediately()
        {
            ExpireTimestampInDays = 0;
        }
        
        public void Apply(Entity entity)
        {
            if(entity == null)
            {
                throw new Exception("Target entity for effect is null");
            }
            EBEffectsAffected ebea = entity.GetBehavior<EBEffectsAffected>();
            if(ebea == null)
            {
                return;
            }
        }
     
        public void Remove()
        {
            
           // BuffManager.RemoveBuff(entity, this)
        }
        public virtual bool OnDeath()
        {
            EBEffectsAffected ebea = entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null)
            {
                return false;
            }
            if (this.removedAfterDeath)
            {
                ebea.activeEffects.Remove(this.effectTypeId);
                ebea.needUpdate = true;
                return true;
            }
            return false;
        }

        public virtual void OnRevive()
        {

        }
        public virtual void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
        }

        public virtual void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {

        }
    }
}
