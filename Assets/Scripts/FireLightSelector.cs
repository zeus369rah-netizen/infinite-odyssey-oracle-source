#if UNITY_5_3_OR_NEWER
using Unity.Burst;
#endif

namespace OdysseyOracle
{
    public struct FireLightCandidate
    {
        public float X, Y, Z;
        public int StableId;
        public byte Enabled;
    }

    // A constrained value-type reader permits NativeArray-backed production data
    // and ordinary arrays in standalone checks, without boxing or engine access.
    public interface IFireLightSelectionData
    {
        int Count { get; }
        FireLightCandidate Candidate(int index);
        int Previous(int slot);
        int Selected(int slot);
        void SetSelected(int slot, int index);
    }

    public static class FireLightSelector
    {
        public const int MaximumEmitters = 128, MaximumLights = 4;

#if UNITY_5_3_OR_NEWER
        [BurstCompile]
#endif
        public static int Select<T>(ref T data, float x, float y, float z,
            int budget = MaximumLights, float maximumDistance = 16f, float retention = .82f)
            where T : struct, IFireLightSelectionData
        {
            for (int slot = 0; slot < MaximumLights; slot++) data.SetSelected(slot, -1);
            if (budget < 0) budget = 0;
            if (budget > MaximumLights) budget = MaximumLights;
            if (!(maximumDistance >= 0f && maximumDistance < float.PositiveInfinity)) return 0;
            if (!(retention >= .5f && retention <= 1f)) retention = .82f;
            float limit = maximumDistance * maximumDistance;
            int count = data.Count < MaximumEmitters ? data.Count : MaximumEmitters, selected = 0;
            for (int slot = 0; slot < budget; slot++)
            {
                int best = -1, bestId = int.MaxValue;
                float bestScore = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    FireLightCandidate entry = data.Candidate(i);
                    if (entry.Enabled == 0) continue;
                    bool used = false;
                    for (int previous = 0; previous < slot; previous++) if (data.Selected(previous) == i) used = true;
                    if (used) continue;
                    float dx = entry.X - x, dy = entry.Y - y, dz = entry.Z - z;
                    float distance = dx * dx + dy * dy + dz * dz;
                    if (!(distance <= limit)) continue; // Also rejects NaN.
                    bool retained = false;
                    for (int previous = 0; previous < MaximumLights; previous++) if (data.Previous(previous) == i) retained = true;
                    float score = retained ? distance * retention : distance;
                    if (score > bestScore || score == bestScore && entry.StableId >= bestId) continue;
                    best = i; bestId = entry.StableId; bestScore = score;
                }
                if (best < 0) break;
                data.SetSelected(slot, best); selected++;
            }
            return selected;
        }

        public static float Flicker(int tick, int group)
        {
            unchecked
            {
                uint value = (uint)tick * 747796405u ^ (uint)group * 2891336453u;
                value ^= value >> 16; value *= 2246822519u; value ^= value >> 13;
                return .88f + (value & 65535u) * (.24f / 65535f);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Candidate coordinates are world metres; IDs index a caller-owned fixed registry.
// Pure scalar selection/flicker loops access constrained struct readers only. No
// Transform, Light, allocation, native ownership or worker-thread assumption here.
