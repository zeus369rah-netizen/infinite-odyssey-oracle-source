using System;
using System.Runtime.InteropServices;
using OdysseyOracle.Foundation.Pools015;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct XpEntitlement32
    {
        public ulong Owner0,Owner1;
        public uint ParentId,OwnerMask,Packed,Revision;
    }
    public static class XpCatchupOrbs015
    {
        public const int ParentCapacity=905,Tiers=4,MaximumSourceId=ParentCapacity*Tiers;
        public static int ParentId(int sourceId)=>sourceId>=1&&sourceId<=MaximumSourceId?(sourceId-1)%ParentCapacity+1:0;
        public static int Tier(int sourceId)=>sourceId>=1&&sourceId<=MaximumSourceId?(sourceId-1)/ParentCapacity:-1;
        public static int ChildId(int parentId,int tier)=>parentId>=1&&parentId<=ParentCapacity&&tier>=1&&tier<Tiers?parentId+tier*ParentCapacity:0;
        public static bool InStage(int sourceId,int stage)=>CampaignStagePacing.ValidStage(stage)&&ParentId(sourceId)>0&&ParentId(sourceId)<=CampaignStagePacing.LastRewardId(stage);
        public static uint Resolved(in XpEntitlement32 value)=>value.Packed&3u;
        public static uint TierMask(in XpEntitlement32 value,int slot)=>slot>=0&&slot<2?(value.Packed>>(2+slot*3))&7u:0u;
        public static bool Empty(in XpEntitlement32 value)=>value.ParentId==0&&value.Owner0==0&&value.Owner1==0&&value.OwnerMask==0&&value.Packed==0&&value.Revision==0;
        public static bool Valid(in XpEntitlement32 value)
        {
            uint resolved=Resolved(value),a=TierMask(value,0),b=TierMask(value,1);
            return value.ParentId>=1&&value.ParentId<=ParentCapacity&&value.OwnerMask>0&&value.OwnerMask<=3&&
                (value.OwnerMask!=3||value.Owner0!=value.Owner1)&&value.Packed<=255&&resolved!=0&&(resolved&~value.OwnerMask)==0&&
                Prefix(a)&&Prefix(b)&&((resolved&1)!=0||a==0)&&((resolved&2)!=0||b==0)&&
                value.Revision==((resolved&1)!=0?1u:0u)+((resolved&2)!=0?1u:0u);
        }
        public static bool Matches(in XpEntitlement32 value,in XpSource64 parent)=>Valid(value)&&parent.SourceId==value.ParentId&&
            parent.EligibleMask==value.OwnerMask&&parent.Owner0==value.Owner0&&parent.Owner1==value.Owner1&&
            (Resolved(value)&~parent.ClaimedMask)==0;
        public static bool Monotonic(in XpEntitlement32 previous,in XpEntitlement32 next)
        {
            if(!Valid(next))return false;if(Empty(previous))return true;
            if(!Valid(previous)||previous.ParentId!=next.ParentId||previous.OwnerMask!=next.OwnerMask||previous.Owner0!=next.Owner0||previous.Owner1!=next.Owner1||
                next.Revision<previous.Revision||(previous.Packed&~next.Packed)!=0)return false;
            for(int slot=0;slot<2;slot++)if((Resolved(previous)&(1u<<slot))!=0&&TierMask(previous,slot)!=TierMask(next,slot))return false;
            return previous.Revision!=next.Revision||previous.Packed==next.Packed;
        }
        public static uint AckBit(in XpEntitlement32 value,ulong authenticatedOwner,uint revision)=>!Valid(value)||value.Revision!=revision?0u:
            (value.OwnerMask&1)!=0&&value.Owner0==authenticatedOwner?1u:(value.OwnerMask&2)!=0&&value.Owner1==authenticatedOwner?2u:0u;
        public static bool SourceIdentity(int sourceId,int wave,int baseXp)
        {
            int parent=ParentId(sourceId);if(parent==0)return false;
            int first=1;
            for(int current=1;current<=CampaignStagePacing.TotalWaves;current++)
            {
                int quota=CampaignStagePacing.AdmissionsForWave(current);
                if(parent<first+quota)return wave==current&&baseXp==CampaignStagePacing.BaseXpForAdmission(current,parent-first);
                first+=quota;
            }
            return false;
        }
        public static bool TryResolve(in XpSource64 parent,in XpEntitlement32 previous,int slot,ulong owner,int acceptedLevel,out XpEntitlement32 next)
        {
            next=default;
            if(!ParentValid(parent)||slot<0||slot>1||!CampaignBalance015.ValidLevel(acceptedLevel))return false;
            uint bit=1u<<slot;
            if((parent.EligibleMask&bit)==0||(parent.ClaimedMask&bit)==0||owner!=(slot==0?parent.Owner0:parent.Owner1))return false;
            if(!Empty(previous)&&(!Matches(previous,parent)||(Resolved(previous)&bit)!=0))return false;
            next=Empty(previous)?new XpEntitlement32{ParentId=parent.SourceId,OwnerMask=parent.EligibleMask,Owner0=parent.Owner0,Owner1=parent.Owner1}:previous;
            int gap=Math.Min(3,Math.Max(0,parent.OriginWave-acceptedLevel));uint tiers=(1u<<gap)-1u;
            next.Packed|=bit|(tiers<<(2+slot*3));next.Revision++;
            return Valid(next)&&Monotonic(previous,next);
        }
        public static bool TryChild(in XpSource64 parent,int tier,out XpSource64 child)
        {
            child=default;int id=ChildId((int)parent.SourceId,tier);
            if(id==0||!ParentValid(parent))return false;
            child=parent;child.SourceId=(uint)id;child.ClaimedMask=0;return true;
        }
        public static uint EffectiveMask(in XpSource64 source,in XpSource64 parent,in XpEntitlement32 entitlement)
        {
            if(!PoolXpMath015.SourceValid(source,CampaignStagePacing.MaximumBaseXp)||!SourceIdentity((int)source.SourceId,source.OriginWave,source.BaseXp))return 0;
            int tier=Tier((int)source.SourceId);if(tier==0)return source.EligibleMask;
            if(!IsChildOf(source,parent)||!Matches(entitlement,parent))return 0;
            uint result=0;
            for(int slot=0;slot<2;slot++)
            {
                uint bit=1u<<slot;
                if((parent.ClaimedMask&bit)!=0&&(Resolved(entitlement)&bit)!=0&&(TierMask(entitlement,slot)&(1u<<(tier-1)))!=0)result|=bit;
            }
            return result&source.EligibleMask;
        }
        public static bool IsChildOf(in XpSource64 child,in XpSource64 parent)=>ParentValid(parent)&&Tier((int)child.SourceId)>0&&ParentId((int)child.SourceId)==parent.SourceId&&
            child.BaseXp==parent.BaseXp&&child.OriginWave==parent.OriginWave&&child.RunEpoch==parent.RunEpoch&&child.RewardGeneration==parent.RewardGeneration&&
            child.Owner0==parent.Owner0&&child.Owner1==parent.Owner1&&child.EligibleMask==parent.EligibleMask&&child.X==parent.X&&child.Y==parent.Y&&child.Z==parent.Z;
        public static bool TryQuote(in XpSource64 source,in XpSource64 parent,in XpEntitlement32 entitlement,int slot,ulong owner,int level,int epoch,int generation,out XpClaimQuote48 quote)
        {
            quote=default;
            if(slot<0||slot>1||!CampaignBalance015.ValidLevel(level)||source.RunEpoch!=epoch||source.RewardGeneration!=generation||
                !PoolXpMath015.SourceValid(source,CampaignStagePacing.MaximumBaseXp))return false;
            uint bit=1u<<slot;
            if((EffectiveMask(source,parent,entitlement)&bit)==0||(source.ClaimedMask&bit)!=0||owner!=(slot==0?source.Owner0:source.Owner1))return false;
            quote=new XpClaimQuote48{SourceId=source.SourceId,RecipientBit=bit,ExpectedClaimedMask=source.ClaimedMask,RunEpoch=epoch,RewardGeneration=generation,
                AcceptedLevel=level,BaseXp=source.BaseXp,BonusXp=0,TotalXp=source.BaseXp,OriginWave=source.OriginWave,AuthenticatedOwner=owner};return true;
        }
        public static bool TryMark(ref XpSource64 source,in XpSource64 parent,in XpEntitlement32 entitlement,in XpClaimQuote48 quote)
        {
            int slot=quote.RecipientBit==1?0:quote.RecipientBit==2?1:-1;
            if(source.ClaimedMask!=quote.ExpectedClaimedMask||!TryQuote(source,parent,entitlement,slot,quote.AuthenticatedOwner,quote.AcceptedLevel,quote.RunEpoch,quote.RewardGeneration,out var expected)||
                quote.SourceId!=expected.SourceId||quote.BaseXp!=expected.BaseXp||quote.TotalXp!=expected.TotalXp||quote.BonusXp!=0||quote.OriginWave!=expected.OriginWave)return false;
            source.ClaimedMask|=quote.RecipientBit;return true;
        }
        public static bool ParentValid(in XpSource64 parent)=>parent.SourceId>=1&&parent.SourceId<=ParentCapacity&&PoolXpMath015.SourceValid(parent,CampaignStagePacing.MaximumBaseXp)&&
            SourceIdentity((int)parent.SourceId,parent.OriginWave,parent.BaseXp);
        static bool Prefix(uint value)=>value==0||value==1||value==3||value==7;
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted immutable parent/wave/owner identity and original arena point; host claim-time level freezes tier eligibility.
// Array Formats: Native-compatible entitlement32;905parents+three905child bands=3620source IDs within4096; per-source two-owner claimed bits.
// Dependencies/Pointers: Sole Director owns transactions/context/reach/LOS and base-onlyForge. Pure code grants no XP, allocates no objects and never multiplies a child bonus.
