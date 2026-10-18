using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.Combat
{
    public enum GridDistanceMetric : uint { Chebyshev = 0, Manhattan = 1 }

    [Flags]
    public enum TargetAvailabilityStatus : uint
    {
        Normal = 0,
        Dead = 1,
        Untargetable = 2,
        Unloaded = 4
    }

    public enum TowerTargetQueryStatus : uint
    {
        NoTarget = 0,
        Selected = 1,
        InvalidInput = 2,
        Unloaded = 3,
        StaleSnapshot = 4,
        InvalidSnapshot = 5
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TargetCandidate
    {
        public int2 GridPosition;
        public ulong StableId;
        public uint Generation;
        public float Health;
        public uint Status;
        public uint Active;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TowerGridTargetState : IComponentData
    {
        public int2 GridPosition;
        public int Range;
        public uint DistanceMetric;
        public ulong TargetId;
        public uint TargetGeneration;
        public uint QueryStatus;
        public ulong TargetDistance;
        public double NextAcceptedShotAt;
        public uint HasTarget;
        public uint FireEligible;
        public uint AcceptedShotSequence;
        public uint SelectionSnapshotGeneration;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: signed integer XZ cells; double deadline uses simulation seconds; no metre conversion.
// Array Formats: TargetCandidate32B(int2@0,ulong@8,uint@16,float@20,uint@24,uint@28); tower component64B.
// Dependencies/Pointers: IComponentData plus unmanaged numeric fields; no UnityObject/native-container ownership.
// Active1/Normal0/positive generation admit a row; HasTarget1 disambiguates valid StableId0.
