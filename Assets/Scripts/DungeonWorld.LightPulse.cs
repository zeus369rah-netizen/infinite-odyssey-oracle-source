using System;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        private const int MaximumSpatialPulseLights = 32;
        [Serializable]
        private struct SpatialPulseDescriptor
        {
            public float BaseIntensity, Phase;
            public SpatialLightPulseKind Kind;
        }
        // Cold bounded adapters only. Retain references/bases through ordinary Editor domain reload.
        [SerializeField] private Light[] spatialPulseLights = new Light[MaximumSpatialPulseLights];
        [SerializeField] private SpatialPulseDescriptor[] spatialPulseDescriptors = new SpatialPulseDescriptor[MaximumSpatialPulseLights];
        [SerializeField] private int spatialPulseCount, spatialPulseOverflow;
        private SpatialLightPulseClock spatialPulseClock;

        internal double SpatialLightPulseTime
        {
            get
            {
                GameSession owner = GameSession.Instance;
                bool suspended = !Application.isFocused || owner != null && owner.HasStarted && owner.Paused;
                return spatialPulseClock.Sample(Time.frameCount, Time.unscaledDeltaTime, suspended);
            }
        }

        private void RegisterSpatialLightPulse(Light light, Vector3 localPosition)
        {
            if (light == null) return;
            Color color = light.color;
            SpatialLightPulseKind kind = color.r > color.g && color.g > color.b
                ? SpatialLightPulseKind.Warm
                : color.b > color.r && color.r > color.g * 2f
                    ? SpatialLightPulseKind.Violet : SpatialLightPulseKind.Steady;
            if (kind == SpatialLightPulseKind.Steady) return; // Moon/sky fill is deliberately constant.
            for (int i = 0; i < spatialPulseCount; i++) if (spatialPulseLights[i] == light) return;
            // Registration is cold. A future budget overflow stays at authored steady illumination.
            if (spatialPulseCount >= MaximumSpatialPulseLights) { spatialPulseOverflow++; return; }
            int slot = spatialPulseCount++;
            spatialPulseLights[slot] = light;
            spatialPulseDescriptors[slot] = new SpatialPulseDescriptor {
                BaseIntensity = light.intensity, Kind = kind,
                Phase = SpatialLightPulseRules.WarmPhase(Mathf.RoundToInt(localPosition.z * 100f))
            };
            RegisterRoomReactionPulseSlot(slot,light);
        }

        private void TickSpatialLightPulse()
        {
            double seconds = SpatialLightPulseTime;
            // Object writes stay in this bounded main-thread adapter; no geometry/spatial queries.
            for (int i = 0; i < spatialPulseCount; i++)
            {
                Light light = spatialPulseLights[i];
                if (light == null || !light.isActiveAndEnabled) continue;
                SpatialPulseDescriptor descriptor = spatialPulseDescriptors[i];
                light.intensity = descriptor.BaseIntensity *
                    SpatialLightPulseRules.Evaluate(descriptor.Kind, seconds, descriptor.Phase)*RoomReactionPulseGain(i);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Authored geometry-root local light anchors stay fixed; shared presentation seconds/radians.
// Array Formats: Fixed32 serialized Light references plus scalar descriptors; no hot allocations/new lights.
// Dependencies/Pointers: World owns existing lights; pure pulse rules supply intensity only. URP owns spatial
// attenuation/shadows; quality presets retain their existing shadow control. Pause/blur freezes the clock.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.
