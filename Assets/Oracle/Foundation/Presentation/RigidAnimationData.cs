using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.Presentation
{
    // One affine column-vector transform. Never reinterpret a row-major float array.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RigidInstanceMatrix64
    {
        public const int Stride = 64;
        public float4 C0;
        public float4 C1;
        public float4 C2;
        public float4 C3;

        public static RigidInstanceMatrix64 FromMatrix(float4x4 value)
        {
            return new RigidInstanceMatrix64 { C0 = value.c0, C1 = value.c1, C2 = value.c2, C3 = value.c3 };
        }
    }

    // Four explicit float4 fields: this buffer is 64B, separate from the 64B matrix buffer.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct AnimationGpuPacket64
    {
        public const int Stride = 64;
        public float4 PivotHealth; // xyz: local staff pivot; w: normalized health.
        public float4 TipSupport; // xyz: local staff tip / tower base anchor; w: validated world support Y.
        public float4 AxisProgress; // xyz: world slam axis; w: caller normalized progress, 0 idle/start, 1 contact.
        public float4 ClockRecoil; // x: stir sample seconds; y: accepted fire OR cracking-damage event elapsed seconds (-1 inactive); z: amplitude; w: duration.
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RigidAnimationConfig32
    {
        public const int Stride = 32;
        // Frequency is radians/second. All timing/amplitudes are author/caller configuration, not game balance defaults.
        public float4 FormulaConfig; // stir frequency, stir amplitude radians, slam angle radians, rapid blend end (0,1].
        public float4 RecoilConfig; // XZ stretch ratio, crack noise scale, crack contrast >0, crack emission cap >=0.
    }

    // CPU-side validation metadata only: no uint identities are encoded into floats or uploaded implicitly.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RigidAnimationStamp32
    {
        public const int Stride = 32;
        public uint EntityIndex;
        public uint EntityGeneration;
        public uint StateRevision;
        public uint TriggerSequence;
        public uint ScopeGeneration;
        public uint SupportRevision;
        public uint SupportOriginEpoch;
        public uint Flags;

        public bool EqualsExpected(in RigidAnimationStamp32 expected)
        {
            return EntityIndex == expected.EntityIndex && EntityGeneration == expected.EntityGeneration
                && StateRevision == expected.StateRevision && TriggerSequence == expected.TriggerSequence
                && ScopeGeneration == expected.ScopeGeneration && SupportRevision == expected.SupportRevision
                && SupportOriginEpoch == expected.SupportOriginEpoch && Flags == expected.Flags;
        }
    }

    public enum RigidPartKind : uint
    {
        AnchoredBody = 0,
        Staff = 1,
        Tower = 2
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Y-up metres; local authored pivot/tip/base -> affine column-vector render-origin-relative world; radians.
// Array Formats: Matrix64 = four float4 columns; Packet64 = four float4 fields; separate Config32 and CPU-only Stamp32.
// Dependencies/Pointers: Unity.Mathematics blittable values only; caller owns fixed buffers, entity generations, support revisions and upload/read fences.
