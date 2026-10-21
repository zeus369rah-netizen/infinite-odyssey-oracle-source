using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure value catalog/schema mapping; no Unity Object access, PlayerPrefs, jobs or native ownership.
    // Runtime adapters own persistence and apply these bounded budgets on the main thread.
    public readonly struct GraphicsPresetSpec
    {
        public readonly string Name;
        public readonly float SceneScale, ShadowDistance, ParticleFactor, ArenaFarClip;
        public readonly int Cascades, ShadowResolution, AdditionalShadowResolution, AdditionalLights, ParticleQuality;
        public readonly bool Bloom, Opaque, LocalShadows, PortalDetail;
        public GraphicsPresetSpec(string name, float scale, float shadows, int cascades, int shadowResolution,
            int additionalShadowResolution, int lights, bool effects, int particles, float particleFactor, float farClip)
        {
            Name = name; SceneScale = scale; ShadowDistance = shadows; Cascades = cascades;
            ShadowResolution = shadowResolution; AdditionalShadowResolution = additionalShadowResolution;
            AdditionalLights = lights; Bloom = Opaque = LocalShadows = PortalDetail = effects;
            ParticleQuality = particles; ParticleFactor = particleFactor; ArenaFarClip = farClip;
        }
    }

    /// <summary>Pure preset/migration policy; one block remains one metre in the separate distance stage.</summary>
    public static class GraphicsPresetCatalog
    {
        public const int SchemaVersion = 2, Count = 5, DefaultPreset = 0;
        public const float DefaultVerticalFieldOfView = 79f;
        public static readonly string[] Names = { "Ultra", "Medium", "Low", "Very Low", "Ultra (Beta)" };
        public static int Clamp(int preset) => Math.Max(0, Math.Min(Count - 1, preset));
        public static GraphicsPresetSpec Get(int preset)
        {
            switch (Clamp(preset))
            {
                case 0: return new GraphicsPresetSpec("Ultra", 1f, 40f, 4, 2048, 2048, 8, true, 2, 1f, 150f);
                case 1: return new GraphicsPresetSpec("Medium", .85f, 25f, 2, 1024, 1024, 8, true, 1, .65f, 100f);
                case 2: return new GraphicsPresetSpec("Low", .65f, 12f, 1, 512, 512, 8, false, 0, .35f, 75f);
                case 4: return new GraphicsPresetSpec("Ultra (Beta)", 1f, 40f, 4, 2048, 2048, 8, true, 2, 1f, 150f);
                default: return new GraphicsPresetSpec("Very Low", .5f, 0f, 1, 256, 256, 8, false, 0, .15f, 60f);
            }
        }

        public static int MigratePreset(int stored, bool hasStoredPreset, int schema)
        {
            if (!hasStoredPreset) return DefaultPreset;
            if (schema >= SchemaVersion) return Clamp(stored);
            // Legacy0 Low /1 Balanced /2 High retain their nearest new meaning.
            int legacy = Math.Max(0, Math.Min(2, stored));
            return legacy == 0 ? 2 : legacy == 1 ? 1 : 0;
        }

        public static bool ResolveAdaptiveDefault(bool storedEnabled, int schema) => schema >= SchemaVersion && storedEnabled;
        public static float ParticleFactor(int preset, int requestedQuality)
        {
            int quality = Math.Max(0, Math.Min(2, requestedQuality));
            float requested = quality == 0 ? .35f : quality == 1 ? .65f : 1f;
            return Math.Min(requested, Get(preset).ParticleFactor);
        }
    }
}
