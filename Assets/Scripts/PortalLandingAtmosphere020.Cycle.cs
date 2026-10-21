using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class PortalLandingAtmosphere020
    {
        static readonly int CycleDaylightId020 = Shader.PropertyToID("_CycleDaylight020");
        static readonly int CycleNightId020 = Shader.PropertyToID("_CycleNight020");
        static readonly int CycleStormId020 = Shader.PropertyToID("_CycleStorm020");
        static readonly int CycleTwilightId020 = Shader.PropertyToID("_CycleTwilight020");
        static readonly int CycleAuroraId020 = Shader.PropertyToID("_CycleAurora020");

        double presentationElapsedSeconds020;
        float presentationShaderClock020;
        uint worldRevision020;
        Transform admittedRoot020;
        bool presentationHeld020 = true;
        bool cycleViewReady020;
        CampaignCycleView020 cycleView020;

        public bool TryReadPresentationClock020(out double elapsedSeconds, out uint generation, out uint revision, out bool held)
        {
            elapsedSeconds = 0d;
            generation = 0;
            revision = 0;
            held = true;
            if (!Bound020) return false;
            elapsedSeconds = presentationElapsedSeconds020;
            generation = this.generation;
            revision = worldRevision020;
            held = presentationHeld020;
            return true;
        }

        public bool TryReadCampaignCycle020(out CampaignCycleView020 view)
        {
            view = default;
            if (!cycleViewReady020 || !Bound020 || cycleView020.Generation != generation || cycleView020.Revision != worldRevision020)
                return false;
            view = cycleView020;
            return true;
        }

        void RefreshCampaignCycleView020()
        {
            cycleViewReady020 = bound && CampaignCycleRules020.TryView(presentationElapsedSeconds020, generation, worldRevision020, out cycleView020);
            if (!cycleViewReady020) cycleView020 = default;
            else ApplyCycleLighting020();
        }

        void ApplyCampaignCycleUniforms020()
        {
            if (sky == null) return;
            float daylight = cycleViewReady020 ? cycleView020.Daylight : 0f;
            float night = cycleViewReady020 && cycleView020.Night == 1 ? 1f : 0f;
            float storm = cycleViewReady020 && CampaignCycleRules020.AllowsBloodstorm(cycleView020) ? 1f : 0f;
            float twilight = cycleViewReady020 ? cycleView020.Twilight : 0f;
            float aurora = cycleViewReady020 ? cycleView020.Aurora : 0f;
            if (sky.HasProperty(CycleDaylightId020)) sky.SetFloat(CycleDaylightId020, daylight);
            if (sky.HasProperty(CycleNightId020)) sky.SetFloat(CycleNightId020, night);
            if (sky.HasProperty(CycleStormId020)) sky.SetFloat(CycleStormId020, storm);
            if (sky.HasProperty(CycleTwilightId020)) sky.SetFloat(CycleTwilightId020, twilight);
            if (sky.HasProperty(CycleAuroraId020)) sky.SetFloat(CycleAuroraId020, aurora);
        }

        void RetireCampaignCycle020()
        {
            cycleViewReady020 = false;
            cycleView020 = default;
            admittedRoot020 = null;
            worldRevision020 = 0;
            presentationElapsedSeconds020 = 0d;
            presentationShaderClock020 = 0f;
            presentationHeld020 = true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Atmosphere's accepted session/World/root/generation/revision lease; one unwrapped cycle-seconds value and one cached modulo-4096 animation projection.
// Array Formats: One retained blittable numeric cycle view, one scalar shader projection, and five cached shader property IDs; no second clock integrator.
// Dependencies/Pointers: The atmosphere tick advances cycle time under gameplay pause/focus/capture holds and freezes only shader motion under reduced motion; the same projection drives sky, surface, weather, and borrowed renderers until resumed. No Light, RenderSettings, camera, weather, save, or network owner is added.
