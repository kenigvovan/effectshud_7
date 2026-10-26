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
        /// <summary>Optional numeric magnitude for a consumer mod's UI (e.g. damage per tick, a DR/reflect fraction).
        /// Synced to the client in <see cref="EffectClientData.magnitude"/>. Default 0 = "no meaningful number".
        /// A METHOD (not a property) on purpose: effects are (de)serialized by Newtonsoft with public members, and a
        /// method is ignored — a get-only property would be written to the effect's saved JSON.</summary>
        public virtual float DisplayMagnitude() => 0f;

        public virtual void OnStart() { }
        
        /// <summary>Called on the ACTIVE effect when another one of the same type is applied. A higher tier replaces
        /// tier and timing; the same tier refreshes timing but never shortens it; a lower tier is ignored.
        /// Subclasses that hold tier-dependent state should call base and then re-apply that state.</summary>
        public virtual void OnStack(Effect otherEffect)
        {
            if(this.tier > otherEffect.tier)
            {
                return;
            }
            if(this.tier == otherEffect.tier)
            {
                // Refresh, but never SHORTEN: keep whichever has more time left. Otherwise a short re-application
                // (e.g. a 2s invisibility from a blink) would cut an already-running long one (e.g. a 40s stealth).
                if (otherEffect.RemainingSeconds() > this.RemainingSeconds())
                {
                    CopyTimingFrom(otherEffect);
                }
                return;
            }
            this.tier = otherEffect.tier;
            CopyTimingFrom(otherEffect);
        }

        /// <summary>Real seconds left, for both tick-based and game-time-based expiry. +Infinity for infinite effects.</summary>
        public double RemainingSeconds()
        {
            if (infinite) return double.PositiveInfinity;
            if (double.IsPositiveInfinity(ExpireTimestampInDays))
            {
                return (ExpireTick - TickCounter) * effectshud.Instance.config.TICK_EVERY_SECONDS;
            }
            var cal = entity?.World?.Calendar ?? effectshud.ServerSideApi?.World?.Calendar;
            if (cal == null) return 0;
            return (ExpireTimestampInDays - cal.TotalDays) * 86400.0 / (cal.SpeedOfTime * cal.CalendarSpeedMul);
        }

        protected void CopyTimingFrom(Effect otherEffect)
        {
            this.ExpireTick = otherEffect.ExpireTick;
            this.TickCounter = otherEffect.TickCounter;
            this.ExpireTimestampInDays = otherEffect.ExpireTimestampInDays;
            this.infinite = otherEffect.infinite;
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

        public void SetExpiryNever()
        {
            infinite = true;
            ExpireTimestampInDays = double.PositiveInfinity;
            ExpireTick = Int32.MaxValue;
        }

        public void SetExpiryImmediately()
        {
            infinite = false;
            ExpireTimestampInDays = 0;
        }
        
        /// <summary>Removes the effect on death (if <see cref="removedAfterDeath"/>), calling <see cref="OnExpire"/>
        /// so stat modifiers are cleared. Returns true if the effect was removed.</summary>
        public virtual bool OnDeath()
        {
            EBEffectsAffected ebea = entity.GetBehavior<EBEffectsAffected>();
            if (ebea == null)
            {
                return false;
            }
            if (this.removedAfterDeath)
            {
                OnExpire();
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

        /// <summary>Called on the ATTACKER's effects after a target with effects support takes <paramref name="damage"/>
        /// (final amount, after the target's own effects) caused by this effect's bearer. Server side only.</summary>
        public virtual void OnDealtDamage(float damage, Entity target, DamageSource dmgSource)
        {
        }
    }
}
