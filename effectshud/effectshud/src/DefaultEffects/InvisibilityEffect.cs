using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace effectshud.src.DefaultEffects
{
    /// <summary>
    /// Invisibility. The render side (hiding the player model from others) is handled by
    /// <see cref="InvisibilityRenderPatch"/>. This adds the gameplay side, server-authoritative:
    /// <list type="bullet">
    /// <item>mobs stop noticing the bearer (<c>animalSeekingRange</c> dropped to ~0 while active),</item>
    /// <item>mobs that were specifically targeting the bearer drop that aggro when it's applied,</item>
    /// <item>attacking (melee) reveals the bearer — but only when <see cref="BreakOnAttack"/> is true.</item>
    /// </list>
    /// <see cref="BreakOnAttack"/> defaults to true and can be overridden per-application by the caller
    /// (e.g. a "greater invisibility" that survives attacks sets it false). Spell/projectile damage from the
    /// bearer doesn't route through DidAttack, so the caller breaks it explicitly for those.
    /// </summary>
    [EffectRegistration(EffectTypeIds.Invisibility)]
    public class InvisibilityEffect : Effect
    {
        private const string StatCode = "effectshud_invisibility";
        private const float AggroDropRadius = 20f;

        /// <summary>If true, the bearer attacking in melee ends the invisibility. Set per-application.</summary>
        public bool BreakOnAttack = true;

        private bool IsServer => entity?.Api?.Side == EnumAppSide.Server;

        public override void OnStart()
        {
            base.OnStart();
            if (!IsServer || entity == null) return;
            entity.Stats.Set("animalSeekingRange", StatCode, -1f); // mobs effectively can't notice you
            DropAggroOnSelf();
        }

        public override void OnExpire() => ClearStat();
        public override void OnLeave() => ClearStat();
        public override bool OnDeath() { ClearStat(); return base.OnDeath(); }

        public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {
            if (BreakOnAttack) SetExpiryImmediately(); // removed on the next effects tick
        }

        /// <summary>Taking damage reveals the bearer — break invisibility on any real (non-heal) hit. Gated on
        /// <see cref="BreakOnAttack"/> so a "greater invisibility" that survives attacking also survives being hit.</summary>
        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if (BreakOnAttack && damage > 0f && dmgSource?.Type != EnumDamageType.Heal)
                SetExpiryImmediately(); // removed on the next effects tick
        }

        private void ClearStat()
        {
            if (IsServer) entity?.Stats.Remove("animalSeekingRange", StatCode);
        }

        /// <summary>Stops only the combat tasks of nearby mobs that are currently targeting THIS bearer —
        /// leaves their other behaviour, and their aggro on other players/targets, untouched.</summary>
        private void DropAggroOnSelf()
        {
            var around = entity.World.GetEntitiesAround(entity.Pos.XYZ, AggroDropRadius, AggroDropRadius,
                e => e != entity && e.Alive && e is EntityAgent);
            foreach (var e in around)
            {
                var mgr = e.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
                if (mgr == null) continue;

                var seek = mgr.GetTask<AiTaskSeekEntity>();
                if (seek != null && seek.TargetEntity == entity) mgr.StopTask<AiTaskSeekEntity>();

                var melee = mgr.GetTask<AiTaskMeleeAttack>();
                if (melee != null && melee.TargetEntity == entity) mgr.StopTask<AiTaskMeleeAttack>();
            }
        }
    }
}
