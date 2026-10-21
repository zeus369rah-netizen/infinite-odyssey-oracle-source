using System;

namespace OdysseyOracle
{
    public struct CampaignCycleView020
    {
        public uint DayNumber, Generation, Revision;
        public ushort PhaseRemainingSeconds, StormRemainingSeconds;
        public byte Night, Storm;
        public float Phase01, Daylight, Twilight, Aurora;
        public float DirectionalIntensity, AmbientScale;
    }

    public static class CampaignCycleRules020
    {
        public const double CycleSeconds = 1800d;
        public const double PhaseSeconds = 900d;
        public const double ShaderClockPeriod = 4096d;
        public const double MaximumAdmittedDelta = .15d;
        public const uint DefaultSeed = 0x51c7a02du;
        const double MaximumElapsedSeconds = 1800d * 4294967295d;
        const double TwilightSeconds = 75d;
        const double AuroraFadeSeconds = 45d;

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // This is the sole bounded presentation-time integrator. Call it once from the atmosphere owner.
        public static double AdvanceElapsed(double elapsedSeconds, float unscaledDelta, bool held)
        {
            if (!Finite(elapsedSeconds) || elapsedSeconds < 0d) elapsedSeconds = 0d;
            if (held || !Finite(unscaledDelta) || unscaledDelta <= 0f) return elapsedSeconds;
            return elapsedSeconds + Math.Min(MaximumAdmittedDelta, (double)unscaledDelta);
        }

        // Existing shaders retain their established wrapped domain; cycle math reads the unwrapped source.
        public static float ProjectShaderClock(double elapsedSeconds)
        {
            if (!Finite(elapsedSeconds) || elapsedSeconds < 0d) return 0f;
            return (float)(elapsedSeconds % ShaderClockPeriod);
        }

        public static bool TryView(double elapsedSeconds, uint generation, uint revision, out CampaignCycleView020 view)
        {
            view = default;
            if (!Finite(elapsedSeconds) || elapsedSeconds < 0d || elapsedSeconds >= MaximumElapsedSeconds || generation == 0 || revision == 0)
                return false;

            double zeroBasedDay = Math.Floor(elapsedSeconds / CycleSeconds);
            uint dayNumber = (uint)zeroBasedDay + 1u;
            double cycleSecond = elapsedSeconds - zeroBasedDay * CycleSeconds;
            bool night = cycleSecond < PhaseSeconds;
            double phaseSecond = night ? cycleSecond : cycleSecond - PhaseSeconds;
            double remaining = PhaseSeconds - phaseSecond;
            int phaseRemaining = Math.Max(1, Math.Min(900, (int)Math.Ceiling(remaining)));
            bool storm = night && IsStormNight(dayNumber);
            float daylight = night ? 0f : (float)Math.Sin(Math.PI * phaseSecond / PhaseSeconds);
            double distanceToPhaseEdge = Math.Min(phaseSecond, PhaseSeconds - phaseSecond);
            float twilight = 1f - Smooth01(distanceToPhaseEdge / TwilightSeconds);
            float aurora = night ? Smooth01(Math.Min(phaseSecond, remaining) / AuroraFadeSeconds) : 0f;

            view = new CampaignCycleView020
            {
                DayNumber = dayNumber,
                Generation = generation,
                Revision = revision,
                PhaseRemainingSeconds = (ushort)phaseRemaining,
                StormRemainingSeconds = (ushort)(storm ? phaseRemaining : 0),
                Night = (byte)(night ? 1 : 0),
                Storm = (byte)(storm ? 1 : 0),
                Phase01 = (float)(phaseSecond / PhaseSeconds),
                Daylight = daylight,
                Twilight = twilight,
                Aurora = aurora,
                DirectionalIntensity = .12f + .30f * daylight,
                AmbientScale = .68f + .32f * daylight
            };
            return true;
        }

        public static bool AllowsBloodstorm(in CampaignCycleView020 view) =>
            view.DayNumber != 0 && view.Generation != 0 && view.Revision != 0 && view.Night == 1 && view.Storm == 1;

        public static float SunElevationDegrees(in CampaignCycleView020 view)
        {
            double phase = Math.Max(0d, Math.Min(1d, view.Phase01));
            return 8f + 70f * (float)Math.Sin(Math.PI * phase);
        }

        public static float KeyArcBlend(in CampaignCycleView020 view) =>
            view.Night == 1 ? 0f : 1f - Math.Max(0f, Math.Min(1f, view.Twilight));

        public static float SunAzimuthDegrees(in CampaignCycleView020 view) => -90f + 180f * Math.Max(0f, Math.Min(1f, view.Phase01));

        public static bool IsStormNight(uint dayNumber) =>
            dayNumber == 1 || dayNumber > 1 && Mix(dayNumber ^ DefaultSeed) % 3u == 0u;

        public static bool SameHudValue(in CampaignCycleView020 a, in CampaignCycleView020 b) =>
            a.DayNumber == b.DayNumber && a.Generation == b.Generation && a.Revision == b.Revision &&
            a.Night == b.Night && a.Storm == b.Storm &&
            a.PhaseRemainingSeconds == b.PhaseRemainingSeconds && a.StormRemainingSeconds == b.StormRemainingSeconds;

        static float Smooth01(double value)
        {
            if (value <= 0d) return 0f;
            if (value >= 1d) return 1f;
            float x = (float)value;
            return x * x * (3f - 2f * x);
        }

        static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            return value ^ (value >> 16);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: One unwrapped admitted-presentation-seconds scalar; modulo projections are pure numeric views only.
// Array Formats: One blittable cycle view with fixed-width day/scope/timer fields and bounded scalar weights; no heap arrays or per-frame objects.
// Dependencies/Pointers: PortalLandingAtmosphere020 is the sole clock integrator and passes its exact generation/revision. First night storms; later night selection hashes one fixed seed. Daylight intensity is a direct .12..42 target for the existing sole key Light, not a multiplier; no Light creation, RenderSettings, weather, save, or gameplay authority here.
