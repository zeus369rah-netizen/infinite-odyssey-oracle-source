using OdysseyOracle.Foundation.Pools015;

namespace OdysseyOracle
{
    public static class XpLedgerMath015
    {
        public static bool TryReserve(int sourceId,int wave,int ordinal,int epoch,int generation,
            ulong owner0,ulong owner1,uint eligible,out XpSource64 source)
        {
            source=default;
            if(!CampaignBalance015.ValidWave(wave)||epoch<=0||generation<=0||eligible==0||eligible>3||eligible==3&&owner0==owner1)return false;
            int stage=(wave-1)/5+1,local=(wave-1)%5,expected=CampaignStagePacing.FirstRewardId(stage);
            for(int j=0;j<local;j++)expected+=CampaignStagePacing.AdmissionsForWave(CampaignStagePacing.GlobalWave(stage,j));
            if(ordinal<0||ordinal>=CampaignStagePacing.AdmissionsForWave(wave)||sourceId!=expected+ordinal)return false;
            source=new XpSource64{SourceId=(uint)sourceId,BaseXp=CampaignStagePacing.BaseXpForAdmission(wave,ordinal),
                OriginWave=wave,RunEpoch=epoch,RewardGeneration=generation,Owner0=owner0,Owner1=owner1,EligibleMask=eligible};
            return true;
        }
        public static bool Publish(ref XpSource64 source,in XpAdmissionTicket015 ticket,float x,float y,float z)
        {
            if(!ticket.Valid||source.Flags!=0||source.SourceId!=ticket.SourceId||source.RunEpoch!=ticket.RunEpoch||
                source.RewardGeneration!=ticket.RewardGeneration||source.BaseXp!=ticket.BaseXp||source.OriginWave!=ticket.OriginWave||
                !PoolXpMath015.Position(x,y,z))return false;
            source.X=x;source.Y=y;source.Z=z;source.Flags=PoolXpMath015.Issued;
            return PoolXpMath015.SourceValid(source,CampaignStagePacing.MaximumBaseXp);
        }
        public static bool SameImmutableValue(in XpSource64 a,in XpSource64 b)=>a.SourceId==b.SourceId&&a.BaseXp==b.BaseXp&&
            a.OriginWave==b.OriginWave&&a.X==b.X&&a.Y==b.Y&&a.Z==b.Z&&a.Owner0==b.Owner0&&a.Owner1==b.Owner1&&a.EligibleMask==b.EligibleMask;
        public static bool AdvanceProgress(int level,int xp,int points,int amount,out int nextLevel,out int nextXp,out int nextPoints)
        {
            nextLevel=level;nextXp=xp;nextPoints=points;
            if(!CampaignBalance015.ValidLevel(level)||xp<0||points<0||points>level-1||amount<1||amount>CampaignStagePacing.MaximumClaimXp||
                (level==CampaignBalance015.MaximumLevel?xp!=0:xp>=CampaignBalance015.XpThreshold(level)))return false;
            long total=(long)xp+amount;
            while(nextLevel<CampaignBalance015.MaximumLevel&&total>=CampaignBalance015.XpThreshold(nextLevel))
            {total-=CampaignBalance015.XpThreshold(nextLevel);nextLevel++;nextPoints++;}
            nextXp=nextLevel==CampaignBalance015.MaximumLevel?0:(int)total;return true;
        }
    }
    public readonly struct XpAdmissionTicket015
    {
        public readonly int SourceId,RunEpoch,RewardGeneration,OriginWave,BaseXp;
        public readonly uint PoolGeneration;
        public XpAdmissionTicket015(int sourceId,int runEpoch,int rewardGeneration,int originWave,int baseXp,uint poolGeneration)
        {SourceId=sourceId;RunEpoch=runEpoch;RewardGeneration=rewardGeneration;OriginWave=originWave;BaseXp=baseXp;PoolGeneration=poolGeneration;}
        public bool Valid=>SourceId>=1&&SourceId<=4096&&RunEpoch>0&&RewardGeneration>0&&CampaignBalance015.ValidWave(OriginWave)&&
            BaseXp>=1&&BaseXp<=CampaignStagePacing.MaximumBaseXp&&PoolGeneration>0;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted scalar source prefix/global wave/epoch/pool generation and original arena point; no Unity Object use.
// Array Formats: Exact64B logical source and one immutable value ticket; bounded5wave prefix and50level scalar loops.
// Dependencies/Pointers: Source owner performs actual authority/reach/LOS checks before these pure commit helpers; no allocation, grant callback, transport or competing ledger.
