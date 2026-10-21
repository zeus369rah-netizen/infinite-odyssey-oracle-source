using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle
{
    public static class CoopHubSpawnLayout
    {
        public const int CandidateCount=16,Capacity=8;
        public const float MinimumSeparation=1.65f;
        public static bool Candidate(int index,out float x,out float z)
        {x=z=0;if(index<0||index>=CandidateCount)return false;x=((index&3)-1.5f)*1.8f;z=-10f+(index>>2)*1.8f;return true;}
        public static int OrderedCandidate(int slot,int attempt)=>slot<1||slot>=Capacity||attempt<0||attempt>=CandidateCount?-1:((slot-1)+attempt*5)&15;
        public static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static bool Separated(float ax,float az,float bx,float bz)
        {if(!Finite(ax)||!Finite(az)||!Finite(bx)||!Finite(bz))return false;float x=ax-bx,z=az-bz;return x*x+z*z>=MinimumSeparation*MinimumSeparation;}
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct CoopHubSpawnGrant
    {
        public ulong ClientId;
        public uint Request,Epoch,Revision;
        public int Slot,Candidate;
        public float X,Y,Z,Yaw;
        public byte State; //0 empty,1 offered,2 acknowledged,3 first pose accepted.
    }
    internal sealed class CoopHubSpawnSlots
    {
        readonly CoopHubSpawnGrant[] grants=new CoopHubSpawnGrant[8];
        uint epoch,nextRevision;
        public void Reset(uint nextEpoch){epoch=nextEpoch;nextRevision=0;Array.Clear(grants,0,grants.Length);}
        public bool TryGet(ulong client,out CoopHubSpawnGrant grant)
        {for(int i=1;i<8;i++)if(grants[i].State!=0&&grants[i].ClientId==client){grant=grants[i];return true;}grant=default;return false;}
        public bool SeparatedFromReservations(float x,float z,ulong except)
        {for(int i=1;i<8;i++)if(grants[i].State!=0&&grants[i].ClientId!=except&&!CoopHubSpawnLayout.Separated(x,z,grants[i].X,grants[i].Z))return false;return true;}
        public bool TryOffer(ulong client,int slot,uint request,int candidate,float x,float y,float z,out CoopHubSpawnGrant grant)
        {
            grant=default;
            if(client==0||slot<1||slot>7||request==0||epoch==0||candidate<0||candidate>=16||!CoopHubSpawnLayout.Finite(x)||!CoopHubSpawnLayout.Finite(y)||!CoopHubSpawnLayout.Finite(z))return false;
            var previous=grants[slot];
            if(previous.State!=0){if(previous.ClientId==client&&previous.Request==request&&previous.Epoch==epoch){grant=previous;return true;}return false;}
            if(TryGet(client,out _)||!SeparatedFromReservations(x,z,client))return false;
            uint revision=++nextRevision;if(revision==0)revision=++nextRevision;
            grant=new CoopHubSpawnGrant{ClientId=client,Slot=slot,Request=request,Epoch=epoch,Revision=revision,Candidate=candidate,X=x,Y=y,Z=z,Yaw=0,State=1};
            grants[slot]=grant;return true;
        }
        public bool Acknowledge(ulong client,uint request,uint offeredEpoch,uint revision)
        {if(!TryGet(client,out var g)||g.Request!=request||g.Epoch!=offeredEpoch||g.Revision!=revision)return false;if(g.State<2){g.State=2;grants[g.Slot]=g;}return true;}
        public bool AcceptPose(ulong client,float x,float y,float z)
        {
            if(!CoopHubSpawnLayout.Finite(x)||!CoopHubSpawnLayout.Finite(y)||!CoopHubSpawnLayout.Finite(z))return false;
            if(!TryGet(client,out var g)||g.State<2)return false;if(g.State==3)return true;
            float dx=x-g.X,dy=y-g.Y,dz=z-g.Z;
            if(!CoopHubSpawnLayout.Finite(x)||!CoopHubSpawnLayout.Finite(y)||!CoopHubSpawnLayout.Finite(z)||dx*dx+dy*dy+dz*dz>1f)return false;
            g.State=3;grants[g.Slot]=g;return true;
        }
        public bool Placed(ulong client)=>TryGet(client,out var g)&&g.State==3;
        public void Release(ulong client){for(int i=1;i<8;i++)if(grants[i].State!=0&&grants[i].ClientId==client)grants[i]=default;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure refuge XZ metres, finite supported feet supplied by the main-thread geometry adapter.
// Array Formats: Fixed16 candidate permutation and eight sequential blittable48-byte grant records; no Object APIs, native allocation or hot managed allocation.
// Dependencies/Pointers: RoomConnection owns lifetime/epoch and NGO sender identity. Duplicate offers are stable; stale/mismatched acknowledgements cannot publish a first pose. This numeric core does not claim geometry support by itself.
