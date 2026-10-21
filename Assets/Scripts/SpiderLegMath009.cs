using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct SpiderLegPose009
    {
        internal float3 Position;
        internal float SoleY;
        internal quaternion Rotation;
    }

    internal struct SpiderLegBox009
    {
        internal float3 Center, Size;
        internal quaternion Rotation;
    }

    internal static class SpiderLegMath009
    {
        internal const int Count = 8;
        internal static float3 Hip(int index)
        { return new float3((index & 1) == 0 ? -.29f : .29f, .75f, ((index >> 1) - 1.5f) * .2112f); }
        static float Fan(int row, float outer, float inner)
        { return row == 0 ? -outer : row == 1 ? -inner : row == 2 ? inner : outer; }
        internal static SpiderLegBox009 Box(int index, int section)
        {
            float side = (index & 1) == 0 ? -1f : 1f;
            int row = index >> 1;
            float3 hip = Hip(index);
            bool outer = row == 0 || row == 3;
            float3 knee = new float3(side * (outer ? .54f : .585f), .78f, Fan(row, .30f, .105f));
            float3 toe = new float3(side * (outer ? .505f : .595f), .025f, Fan(row, .30f, .105f));
            if (section == 2)
                return new SpiderLegBox009 { Center = toe, Size = new float3(.12f, .05f, .13f), Rotation = quaternion.identity };
            float3 from = section == 0 ? hip : knee;
            float3 to = section == 0 ? knee : toe + new float3(0f, .045f, 0f);
            float thickness = section == 0 ? .12f : .10f;
            float3 delta = to - from;
            return new SpiderLegBox009 { Center = (from + to) * .5f,
                Size = new float3(thickness, thickness, math.length(delta)),
                Rotation = quaternion.LookRotationSafe(delta, math.up()) };
        }
        internal static SpiderLegPose009 Pose(int index, float gait, float weight, bool flying)
        {
            if (!math.isfinite(gait)) gait = 0f;
            if (!math.isfinite(weight)) weight = 0f;
            weight = math.saturate(weight);
            float phase = math.fmod(gait, 2f * math.PI) + (((index >> 1) % 2 == (index & 1)) ? 0f : math.PI);
            float sine = math.sin(phase), positive = math.max(0f, sine);
            float lift = .055f * positive * positive * weight + (flying ? .06f : 0f);
            return new SpiderLegPose009 { Position = Hip(index) + new float3(0f, lift, 0f), SoleY = lift,
                Rotation = quaternion.RotateY(sine * weight * math.radians(4f)) };
        }
    }

    [BurstCompile]
    internal struct SpiderLegPoseJob009 : IJob
    {
        internal float Gait, Weight;
        internal bool Flying;
        [WriteOnly] internal NativeArray<SpiderLegPose009> Poses;
        [WriteOnly] internal NativeArray<float> MinimumSole;
        public void Execute()
        {
            float lowest = float.PositiveInfinity;
            for (int i = 0; i < SpiderLegMath009.Count; i++)
            {
                SpiderLegPose009 pose = SpiderLegMath009.Pose(i, Gait, Weight, Flying);
                Poses[i] = pose;
                lowest = math.min(lowest, pose.SoleY);
            }
            MinimumSole[0] = lowest;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure unscaled actor-local metres, +Y up/+Z forward; no UnityEngine.Object access.
// Array Formats: Eight sequential32B pose records and onefloat sole result, persistent arrays lent by one presentation lease.
// Dependencies/Pointers: Burst/Collections/Jobs/Mathematics; caller schedules/completes before transform adaptation or disposal; no simulation ownership.
