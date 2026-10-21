using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Fixed room/admission scalars and canonical lane mapping, no Unity/native
    // ownership. Room's main thread owns eight records and pending deadlines.
    // Four towers are shared; this module assigns no per-lane tower entitlement.
    public static class CoopPartyCapacity
    {
        public const int MaximumPlayers=2, MaximumLanes=8, MaximumTowers=4;
        public const int PoseBytes=36, PoseEntryBytes=44, PoseHeaderBytes=3;
        public const int MaximumPoseSnapshotBytes=PoseHeaderBytes+MaximumPlayers*PoseEntryBytes;
        public const int AllLaneMask=255;
        public const string RoomType="odyssey-oracle-coop-v13-core020";
        public const string WireProtocol="oracle-020-p2-d3003-n5-f2-h1-c12-a1-sp1-id1-tl3-er1-sr1-lv8-ls1-bi1-w40-xp25-d15-xb2-co4-rc1-tb7-lb1-fl1";
        public static bool MatchesProtocol(byte[] data)
        {
            if(data==null||data.Length!=WireProtocol.Length)return false;
            for(int i=0;i<data.Length;i++)if(data[i]!=(byte)WireProtocol[i])return false;
            return true;
        }
        public static int LaneForAdmissionSlot(int slot)
        {
            switch(slot){case 0:return 0;case 1:return 4;case 2:return 2;case 3:return 6;
                case 4:return 1;case 5:return 5;case 6:return 3;case 7:return 7;default:return -1;}
        }
        public static int MaskForCount(int count)
        {int mask=0;for(int i=0;i<Math.Min(MaximumPlayers,Math.Max(0,count));i++)mask|=1<<LaneForAdmissionSlot(i);return mask;}
        public static int LaneCount(int mask)
        {int count=0;for(int i=0;i<MaximumLanes;i++)count+=(mask>>i)&1;return count;}
        public static bool ValidLaneMask(int mask)=>mask>0&&(mask&~AllLaneMask)==0;
        public static bool LaneActive(int mask,int lane)=>lane>=0&&lane<MaximumLanes&&(mask&(1<<lane))!=0;
        public static bool ValidPoseHeader(int members,int count,int remaining)=>members>=1&&members<=MaximumPlayers&&
            count>=0&&count<=members&&remaining>=count*PoseEntryBytes;
        public static bool Newer(uint incoming,uint previous)=>unchecked((int)(incoming-previous))>0;
    }
    public struct CoopMemberSlot
    {
        public ulong Id; public int Slot;
        public CoopMemberSlot(ulong id,int slot){Id=id;Slot=slot;}
    }
    public sealed class CoopAdmissionRoster
    {
        readonly ulong[] ids=new ulong[CoopPartyCapacity.MaximumPlayers];
        readonly double[] deadlines=new double[CoopPartyCapacity.MaximumPlayers];
        int occupied,pending;
        public int Count=>CoopPartyCapacity.LaneCount(occupied);
        public int PendingCount=>CoopPartyCapacity.LaneCount(pending);
        public bool TryGetSlot(ulong id,out int slot)
        {for(int i=0;i<ids.Length;i++)if((occupied&(1<<i))!=0&&ids[i]==id){slot=i;return true;}slot=-1;return false;}
        public bool ReserveHost()
        {
            if(TryGetSlot(0,out int slot))return slot==0;
            if((occupied&1)!=0)return false;
            ids[0]=0;occupied|=1;return true;
        }
        public bool Reserve(ulong id,double now,double lifetime,out int slot)
        {
            slot=-1;
            if(id==0||double.IsNaN(now)||double.IsInfinity(now)||now<0||double.IsNaN(lifetime)||double.IsInfinity(lifetime)||lifetime<=0||lifetime>60)return false;
            Expire(now);
            if(TryGetSlot(id,out slot))return true; // Repeated approval cannot extend a pending lease.
            for(int i=1;i<ids.Length;i++)if((occupied&(1<<i))==0)
            {ids[i]=id;occupied|=1<<i;pending|=1<<i;deadlines[i]=now+lifetime;slot=i;return true;}
            return false;
        }
        public bool Confirm(ulong id)
        {if(!TryGetSlot(id,out int slot))return false;pending&=~(1<<slot);deadlines[slot]=0;return true;}
        public bool Remove(ulong id)
        {if(!TryGetSlot(id,out int slot))return false;occupied&=~(1<<slot);pending&=~(1<<slot);ids[slot]=0;deadlines[slot]=0;return true;}
        public bool Expire(double now)
        {
            if(double.IsNaN(now)||double.IsInfinity(now)||now<0)return false;
            bool changed=false;
            for(int i=1;i<ids.Length;i++)if((pending&(1<<i))!=0&&now>=deadlines[i])
            {occupied&=~(1<<i);pending&=~(1<<i);ids[i]=0;deadlines[i]=0;changed=true;}
            return changed;
        }
        public void Clear(){occupied=pending=0;Array.Clear(ids,0,ids.Length);Array.Clear(deadlines,0,deadlines.Length);}
        public bool ApplyHostSnapshot(CoopMemberSlot[] members,int count)
        {
            if(members==null||count<1||count>ids.Length||members.Length<count)return false;
            int mask=0;bool host=false;
            for(int i=0;i<count;i++)
            {
                var m=members[i];
                if(m.Slot<0||m.Slot>=ids.Length||(mask&(1<<m.Slot))!=0||(m.Id==0)!=(m.Slot==0))return false;
                for(int j=0;j<i;j++)if(members[j].Id==m.Id)return false;
                mask|=1<<m.Slot;host|=m.Id==0;
            }
            if(!host)return false;
            Clear();for(int i=0;i<count;i++)ids[members[i].Slot]=members[i].Id;occupied=mask;return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: authenticated existing NGO room and Y-up lab world; no new physics or gameplay authority.
// Array Formats: fixed256-byte LabVersion2 frame, word224 LiveStatus only on State; fixed64-byte arena impact event.
// Dependencies/Pointers: exact room tl2/ls1/bi1 capability gate and v2 lab channel reject older peers; requests and Impact require zero LiveStatus; root native/two-peer acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world and accepted015 campaign/source identity, original arena positions retained.
// Array Formats: Eight milestones/40waves/905logical rewards within4096native slots, fixed128B XP channel and existing bounded pools.
// Dependencies/Pointers: Root composes actual current pool/audio owners with reviewed campaign/XP hunks; source/native/browser/performance/public acceptance remain separate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted parent/wave/owner/run identities and original source positions; literal claim-time catch-up children.
// Array Formats: Existing4096source64 ledger,905entitlement32 sidecars/dirty words,4096readonly projection, exact128BVersion2 frames.
// Dependencies/Pointers: Root composes structural clearance and these guarded owner hooks; base-onlyForge, no doublebonus, native/peer/carry/visual/performance acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up/XZ, exact1m terrain/.25m build roots and locked active-road context.
// Array Formats: Bounded native semantic/preview/body stores, exact64B versioned command and existing2peer/4tower snapshots.
// Dependencies/Pointers: Root integrates exact guarded slices; host economy/readiness/LOS and World geometry remain canonical, prepared/source compile is not native or browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing authenticated service/NGO room boundary; no gameplay/root changes.
// Array Formats: Existing fixed DTOs and same-length ASCII WireProtocol; one central0.1.1 RoomType for create/join.
// Dependencies/Pointers: Existing approval compares exact bytes before remote reservation, party capabilities share the same constant; no new protocol owner. Native/service rejection and same-build join pending.
