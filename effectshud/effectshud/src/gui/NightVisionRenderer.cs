using System;
using Vintagestory.API.Client;

namespace effectshud.src.gui
{
    /// <summary>
    /// Client side of the night vision effect. Drives the vanilla <c>ShaderUniforms.NightVisionStrength</c> (the
    /// same uniform the night vision device uses). The vanilla ModSystemNightVision writes it every frame in
    /// EnumRenderStage.Before at order 0 — this runs right after and only ever raises it, so a worn device and the
    /// effect don't fight.
    /// </summary>
    public class NightVisionRenderer : IRenderer
    {
        private const float StrengthTier1 = 0.6f;
        private const float StrengthMax = 0.8f; // vanilla device cap

        private readonly ICoreClientAPI capi;

        public NightVisionRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public double RenderOrder => 0.1;
        public int RenderRange => 1;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var effects = capi.World?.Player?.Entity?.GetBehavior<EBEffectsAffected>()?.onlyClientsActiveEffects;
            if (effects == null || !effects.TryGetValue(EffectTypeIds.NightVision, out var ecd)) return;
            if (!ecd.infinite && ecd.duration <= 0) return;

            float strength = Math.Min(StrengthMax, StrengthTier1 + 0.1f * (ecd.tier - 1));
            var uniforms = capi.Render.ShaderUniforms;
            if (uniforms.NightVisionStrength < strength) uniforms.NightVisionStrength = strength;
        }

        public void Dispose()
        {
        }
    }
}
