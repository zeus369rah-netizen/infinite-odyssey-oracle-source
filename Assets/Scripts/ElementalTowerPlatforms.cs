using System;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // World-space metres and stable IDs only. Four SHARED bastions are between
    // the eight road axes; no pad belongs to an admission slot or player.
    // Cold arrays allocate once per arena entry. No authority, input or frame loop.
    public static class ElementalTowerPlatforms
    {
        public const int PadCount = 4, LaneCount = 8;
        public const float BastionRadius = 14f, TopHeight = .36f;
        public const float BarrierStart = 16f, BarrierEnd = 73f, BarrierWidth = 7f, BarrierHeight = 5f;
        const float Diagonal = .7071067811865475f;
        static readonly int[] admissionLanes = { 0, 4, 2, 6, 1, 5, 3, 7 };
        static readonly Vector3[] directions = { new Vector3(1,0,0), new Vector3(Diagonal,0,Diagonal),
            new Vector3(0,0,1), new Vector3(-Diagonal,0,Diagonal), new Vector3(-1,0,0),
            new Vector3(-Diagonal,0,-Diagonal), new Vector3(0,0,-1), new Vector3(Diagonal,0,-Diagonal) };

        public static Vector3[] CreateTopCenters() => new[] {
            new Vector3(12.934314f,TopHeight,5.357568f), new Vector3(-5.357568f,TopHeight,12.934314f),
            new Vector3(-12.934314f,TopHeight,-5.357568f), new Vector3(5.357568f,TopHeight,-12.934314f) };

        public static int LaneForAdmissionSlot(int slot)
        { CheckLane(slot); return admissionLanes[slot]; }
        public static Vector3 Direction(int lane)
        { CheckLane(lane); return directions[lane]; }
        public static int MaskForPlayerCount(int count)
        {
            count = Math.Max(0,Math.Min(LaneCount,count)); int mask = 0;
            for(int slot=0;slot<count;slot++)mask |= 1 << admissionLanes[slot];
            return mask;
        }
        public static int NearestLane(Vector3 feet)
        {
            if(!Finite(feet.x)||!Finite(feet.z))return -1;
            int best=0; double score=double.NegativeInfinity;
            for(int lane=0;lane<LaneCount;lane++)
            {
                double dot=(double)feet.x*directions[lane].x+(double)feet.z*directions[lane].z;
                if(dot>score){score=dot;best=lane;}
            }
            return best;
        }
        public static Vector3 ConnectorAnchor(int pad,int side)
        {
            if(pad<0||pad>=PadCount||side<0||side>1)throw new ArgumentOutOfRangeException();
            int lane=pad*2+side; Vector3 p=TopCenter(pad),d=Direction(lane);
            return d*(p.x*d.x+p.z*d.z);
        }
        public static Vector3 TopCenter(int pad)
        {
            switch(pad)
            {
                case 0:return new Vector3(12.934314f,TopHeight,5.357568f);
                case 1:return new Vector3(-5.357568f,TopHeight,12.934314f);
                case 2:return new Vector3(-12.934314f,TopHeight,-5.357568f);
                case 3:return new Vector3(5.357568f,TopHeight,-12.934314f);
                default:throw new ArgumentOutOfRangeException(nameof(pad));
            }
        }
        public static bool IsCanonicalPad(Vector3 p)
        {
            if(!Finite(p.x)||!Finite(p.y)||!Finite(p.z)||Math.Abs(p.y-TopHeight)>.025f)return false;
            for(int pad=0;pad<PadCount;pad++)
            {Vector3 c=TopCenter(pad);float x=p.x-c.x,z=p.z-c.z;if(x*x+z*z<=.04f)return true;}
            return false;
        }
        public static Vector3 PlayerSpawn(int admissionSlot)
        {
            // Preserve the first solo spawn; the other seven lie inside the clear
            // central hub, outside the core and the existing SE match forge.
            int lane=LaneForAdmissionSlot(admissionSlot);Vector3 d=Direction(lane);
            float radius=admissionSlot==0?6.7f:4.2f;
            return new Vector3(d.z*radius,.2f,-d.x*radius);
        }
        public static void CheckLane(int lane)
        {if(lane<0||lane>=LaneCount)throw new ArgumentOutOfRangeException(nameof(lane));}
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
    }

    // Pure lane lifecycle. Requested admission may shrink immediately; physical
    // geometry only shrinks after explicit host drain and the World's local check.
    public sealed class ArenaLaneState
    {
        public int AdmissionMask {get;private set;}
        public int GeometryMask {get;private set;}
        public int DrainingMask=>GeometryMask & ~AdmissionMask;
        public static int Count(int mask)
        {CheckMask(mask);int count=0;for(;mask!=0;mask&=mask-1)count++;return count;}
        public void Reset(int mask)
        {CheckMask(mask);AdmissionMask=GeometryMask=mask;}
        public void Request(int mask)
        {CheckMask(mask);AdmissionMask=mask;GeometryMask|=mask;}
        public bool CompleteDrain(int lane,bool physicallyClear)
        {
            ElementalTowerPlatforms.CheckLane(lane);int bit=1<<lane;
            if((AdmissionMask&bit)!=0||!physicallyClear)return false;
            GeometryMask&=~bit;return true;
        }
        public bool ApplyRemote(int admissionMask,int geometryMask,int locallyOccupiedMask)
        {
            CheckMask(admissionMask);CheckMask(geometryMask);CheckMask(locallyOccupiedMask);
            if((admissionMask&~geometryMask)!=0)return false;
            AdmissionMask=admissionMask;
            GeometryMask=geometryMask|(GeometryMask&locallyOccupiedMask);
            return GeometryMask==geometryMask;
        }
        public static void CheckMask(int mask)
        {if((mask&~255)!=0)throw new ArgumentOutOfRangeException(nameof(mask));}
    }
}
