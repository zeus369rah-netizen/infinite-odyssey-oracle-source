using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.Audio
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct AudioBlocker32
    {
        public float3 Minimum; public uint Active;
        public float3 Maximum; public uint Revision;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct AudioHit16 { public ulong Identity; public float Distance; public uint Reserved; }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct AudioQuery80
    {
        public float3 Listener; public int Slot;
        public float3 Emitter; public int HitCount;
        public ulong ListenerCell, EmitterCell;
        public uint Generation, GeometryRevision, ProfileRevision, OwnerRevision;
        public uint Flags; public float Length; public ulong Reserved;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct AudioResult48
    {
        public float Gain, Cutoff, Exposure; public uint Status;
        public int Slot; public uint Generation, GeometryRevision, ProfileRevision;
        public uint OwnerRevision, Reserved0, Reserved1, Reserved2;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct AudioProfile32
    {
        public float BlockedGain, BlockedCutoff, Attack, Release;
        public float Strength, SpeechGain; public uint SpatializerRequested, Reserved;
    }

    [BurstCompile]
    public static class AudioAcousticMath015
    {
        public const int Voices = 8, Queries = 2, HitsPerRay = 16, TowerBoxes = 8, Blockers = 4096;
        public const uint QueryValid = 1, QuerySaturated = 2, RegistryUnknown = 4;
        public const uint Unknown = 0, Open = 1, Blocked = 2;
        public const float Range = 30f, EndpointPadding = .12f, OpenCutoff = 22000f;

        public static bool Finite(float value) => math.isfinite(value);
        public static bool Finite(float3 value) => math.all(math.isfinite(value));
        public static bool ValidBox(AudioBlocker32 value) => value.Active == 1 && Finite(value.Minimum) &&
            Finite(value.Maximum) && math.all(value.Maximum > value.Minimum);
        public static bool Contains(AudioBlocker32 value, float3 point) =>
            math.all(point >= value.Minimum - .03f) && math.all(point <= value.Maximum + .03f);
        public static float Approach(float current, float target, float dt, float seconds)
        {
            if (!Finite(target)) return Finite(current) ? current : 0;
            if (!Finite(current) || !Finite(dt) || !Finite(seconds) || seconds <= 0) return target;
            return math.lerp(current, target, 1f - math.exp(-math.clamp(dt, 0, .1f) / seconds));
        }
        public static uint Next(uint value) { unchecked { value++; return value == 0 ? 1u : value; } }
        public static AudioProfile32 Profile(int kind, float customStrength, bool spatializer)
        {
            float strength = Finite(customStrength) ? math.saturate(customStrength) : .7f;
            return new AudioProfile32 {
                BlockedGain = kind == 1 ? .55f : kind == 2 ? .65f : .6f,
                BlockedCutoff = kind == 1 ? 1800f : kind == 2 ? 3200f : 2400f,
                Attack = .055f, Release = .22f, Strength = kind == 3 ? strength : kind == 1 ? .9f : .7f,
                SpeechGain = kind == 2 ? .35f : .7f, SpatializerRequested = spatializer ? 1u : 0u
            };
        }
        // Audio-local refresh cells only: signed16-bit/axis at0.5m, distinct from chunk/save keys.
        public static bool TryCell(float3 point, out ulong value)
        {
            value = 0; if (!Finite(point)) return false;
            float3 grid = math.floor(point * 2f);
            if (math.any(grid < -32768f) || math.any(grid > 32767f)) return false;
            uint3 cell = (uint3)((int3)grid + 32768);
            for (int bit = 0; bit < 16; bit++)
            {
                value |= ((ulong)((cell.x >> bit) & 1u)) << (3 * bit);
                value |= ((ulong)((cell.y >> bit) & 1u)) << (3 * bit + 1);
                value |= ((ulong)((cell.z >> bit) & 1u)) << (3 * bit + 2);
            }
            return true;
        }
        public static bool Intersects(AudioBlocker32 box, float3 from, float3 to)
        {
            if (!ValidBox(box) || !Finite(from) || !Finite(to) || Contains(box, from) || Contains(box, to)) return false;
            float3 direction = to - from; float enter = 0, exit = 1;
            for (int axis = 0; axis < 3; axis++)
            {
                float component = direction[axis];
                if (math.abs(component) < .000001f) { if (from[axis] < box.Minimum[axis] || from[axis] > box.Maximum[axis]) return false; continue; }
                float a = (box.Minimum[axis] - from[axis]) / component, b = (box.Maximum[axis] - from[axis]) / component;
                enter = math.max(enter, math.min(a, b)); exit = math.min(exit, math.max(a, b));
                if (enter > exit) return false;
            }
            return exit > 0 && enter < 1;
        }
        public static AudioResult48 Header(AudioQuery80 query, AudioProfile32 profile)
        {
            float strength = Finite(profile.Strength) ? math.saturate(profile.Strength) : .7f;
            return new AudioResult48 { Slot = query.Slot, Generation = query.Generation,
                GeometryRevision = query.GeometryRevision, ProfileRevision = query.ProfileRevision,
                OwnerRevision = query.OwnerRevision, Status = Unknown,
                Gain = math.lerp(1f, .78f, strength), Cutoff = math.lerp(OpenCutoff, 6000f, strength) };
        }
        public static bool ValidProfile(AudioProfile32 value) => Finite(value.BlockedGain) && value.BlockedGain >= 0 && value.BlockedGain <= 1 &&
            Finite(value.BlockedCutoff) && value.BlockedCutoff >= 20 && value.BlockedCutoff <= OpenCutoff && Finite(value.Strength) && value.Strength >= 0 && value.Strength <= 1 &&
            Finite(value.Attack) && value.Attack > 0 && value.Attack <= 10 && Finite(value.Release) && value.Release > 0 && value.Release <= 10 &&
            Finite(value.SpeechGain) && value.SpeechGain >= 0 && value.SpeechGain <= 1;
        public static bool ValidQuery(AudioQuery80 value) => value.Flags == QueryValid && value.Slot >= 0 && value.Slot < Voices &&
            value.Generation != 0 && value.GeometryRevision != 0 && value.ProfileRevision != 0 && value.OwnerRevision != 0 &&
            value.HitCount >= 0 && value.HitCount < HitsPerRay && Finite(value.Listener) && Finite(value.Emitter) && Finite(value.Length) &&
            value.Length > .01f && value.Length <= Range && math.abs(math.length(value.Emitter - value.Listener) - value.Length) <= .001f;
        public static bool ValidResult(AudioResult48 value) => value.Status <= Blocked && Finite(value.Gain) && value.Gain >= 0 && value.Gain <= 1 &&
            Finite(value.Cutoff) && value.Cutoff >= 20 && value.Cutoff <= OpenCutoff;
        public static bool Matches(AudioResult48 result, int slot, uint generation, uint geometry, uint profile, uint owner) =>
            slot >= 0 && slot < Voices && result.Slot == slot && generation != 0 && result.Generation == generation &&
            result.GeometryRevision == geometry && result.ProfileRevision == profile && result.OwnerRevision == owner;
    }

    [BurstCompile]
    public struct AudioAcousticJob015 : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AudioQuery80> Queries;
        [ReadOnly] public NativeArray<AudioHit16> Hits;
        [ReadOnly] public NativeParallelHashMap<ulong, AudioBlocker32> Blockers;
        [ReadOnly] public NativeArray<AudioBlocker32> Towers;
        public AudioProfile32 Profile;
        [WriteOnly] public NativeArray<AudioResult48> Results;

        public void Execute(int index)
        {
            AudioQuery80 query = Queries[index]; AudioResult48 result = AudioAcousticMath015.Header(query, Profile);
            bool known = AudioAcousticMath015.ValidQuery(query) && AudioAcousticMath015.ValidProfile(Profile);
            if (!known) { Results[index] = result; return; }
            bool blocked = false;
            for (int hit = 0; hit < query.HitCount; hit++)
            {
                AudioHit16 value = Hits[index * AudioAcousticMath015.HitsPerRay + hit];
                if (value.Identity == 0 || !AudioAcousticMath015.Finite(value.Distance) || value.Distance < 0 || value.Distance > query.Length)
                { Results[index] = result; return; }
                // Physics has already hit the actual surface. A room-wide mesh AABB may contain
                // both endpoints; that must never discard its genuine wall intersection.
                if (Blockers.TryGetValue(value.Identity, out AudioBlocker32 box) && box.Active == 1) blocked = true;
            }
            for (int tower = 0; tower < Towers.Length; tower++)
                if (AudioAcousticMath015.Intersects(Towers[tower], query.Listener, query.Emitter)) blocked = true;
            result.Status = blocked ? AudioAcousticMath015.Blocked : AudioAcousticMath015.Open;
            result.Exposure = blocked ? 1f : 0f;
            result.Gain = blocked ? math.lerp(1f, Profile.BlockedGain, Profile.Strength) : 1f;
            result.Cutoff = blocked ? math.lerp(AudioAcousticMath015.OpenCutoff, Profile.BlockedCutoff, Profile.Strength) : AudioAcousticMath015.OpenCutoff;
            Results[index] = result;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Unity World metres/Y-up/+Z; audio-only0.5m Morton refresh cells.
// Array Formats:32B blocker/profile,16B hit,80B query,48B result;2queries×16hits/8towerboxes.
// Dependencies/Pointers: Pure Burst value math; borrowed immutable native inputs through one reader fence; no Object/Physics/AudioSource in jobs.
