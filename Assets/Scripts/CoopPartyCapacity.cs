using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Fixed room/admission scalars and canonical lane mapping, no Unity/native
    // ownership. Room's main thread owns eight records and pending deadlines.
    // Four towers are shared; this module assigns no per-lane tower entitlement.
    public static class CoopPartyCapacity
    {
        public const int MaximumPlayers=8, MaximumTowers=4;
        public const int PoseBytes=36, PoseEntryBytes=44, PoseHeaderBytes=3;
        public const int MaximumPoseSnapshotBytes=PoseHeaderBytes+MaximumPlayers*PoseEntryBytes;
        public const int AllLaneMask=255;
        public const string WireProtocol="oracle-008-p8-d3003-n5-f2-h1-c11-a1";
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
        {int count=0;for(int i=0;i<MaximumPlayers;i++)count+=(mask>>i)&1;return count;}
        public static bool ValidLaneMask(int mask)=>mask>0&&(mask&~AllLaneMask)==0;
        public static bool LaneActive(int mask,int lane)=>lane>=0&&lane<MaximumPlayers&&(mask&(1<<lane))!=0;
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
