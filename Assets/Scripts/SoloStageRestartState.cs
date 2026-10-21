using System;

namespace OdysseyOracle
{
    public readonly struct AcceptedSoloRestartProgress
    {
        public readonly int Level,Xp,Points,Mask;
        public readonly MatchForgeSnapshot Forge;
        public AcceptedSoloRestartProgress(int level,int xp,int points,int mask,MatchForgeSnapshot forge)
        {Level=level;Xp=xp;Points=points;Mask=mask;Forge=forge;}
        public bool Valid
        {
            get{
                if(!Forge.Valid||Level<1||Level>ElementProgression.MaximumLevel||Xp<0||Points<0||Mask<0||Mask>4095)return false;
                int spent=0;
                for(int element=0;element<4;element++){
                    int branch=(Mask>>(element*3))&7;
                    if(branch!=0&&branch!=1&&branch!=3&&branch!=7)return false;
                    for(int bit=0;bit<3;bit++)if((branch&(1<<bit))!=0)spent++;
                }
                return (long)Points+spent<=Level-1&&(Level==ElementProgression.MaximumLevel?Xp==0:Xp<CampaignBalance015.XpThreshold(Level));
            }
        }
    }
    public readonly struct SoloStageRestartTicket
    {
        public readonly string Nonce,SourceCampaign;
        public readonly int Stage,SourceEpoch,SourceRevision,SourceRoomOperation,Visits,Towers;
        public readonly ulong SourceClient;
        public readonly bool FirstStageCoreLoadout;
        public readonly AcceptedSoloRestartProgress Progress;
        SoloStageRestartTicket(string nonce,CampaignSnapshot accepted,CampaignPeerSnapshot local,int operation,AcceptedSoloRestartProgress progress)
        {Nonce=nonce;SourceCampaign=accepted.campaign;Stage=accepted.stage;SourceEpoch=accepted.epoch;SourceRevision=accepted.revision;SourceRoomOperation=operation;SourceClient=local.id;Visits=local.visits;Towers=local.towers;FirstStageCoreLoadout=accepted.firstStageCoreLoadout;Progress=progress;}
        public bool Valid=>CampaignQuestState.ValidNonce(Nonce)&&CampaignQuestState.ValidNonce(SourceCampaign)&&CampaignStagePacing.ValidStage(Stage)&&
            SourceEpoch>0&&SourceRevision>0&&SourceRoomOperation>0&&SourceRoomOperation<int.MaxValue-2&&Progress.Valid&&Towers==CampaignQuestState.FourCoreTowers&&
            (Visits==CampaignQuestState.RequiredVisits||Stage==1&&FirstStageCoreLoadout&&Visits>=0&&Visits<=CampaignQuestState.RequiredVisits);
        internal static bool TryCreate(string nonce,CampaignSnapshot accepted,ulong localId,int roomOperation,AcceptedSoloRestartProgress progress,out SoloStageRestartTicket ticket)
        {
            ticket=default;
            if(!CampaignQuestState.ValidNonce(nonce)||!CampaignQuestState.ValidSnapshot(accepted,2)||accepted.peers.Length!=2||
                (accepted.phase!=(int)CampaignPhase.Defense&&accepted.phase!=(int)CampaignPhase.Preparing)||!progress.Valid||roomOperation<1)return false;
            CampaignPeerSnapshot local=null;
            foreach(CampaignPeerSnapshot peer in accepted.peers)if(peer.id==localId)local=peer;
            if(local==null||!local.participating)return false;
            var candidate=new SoloStageRestartTicket(nonce,accepted,local,roomOperation,progress);
            if(!candidate.Valid)return false;ticket=candidate;return true;
        }
        public bool SuppressesReward(int stage)=>stage>=1&&stage<=Stage;
        internal bool CanReuseOwnedLossCleanup(int roomOperation,bool unexpectedLossStartedLeave,bool hasOwnedCleanupTask)
            =>Valid&&unexpectedLossStartedLeave&&hasOwnedCleanupTask&&roomOperation==SourceRoomOperation+1;
    }
    public enum SoloRestartPhase{Idle,Captured,Leaving,Connecting,Preparing,AwaitingResume,ResumeArmed,Complete,Failed}
    public sealed class SoloStageRestartGate
    {
        public SoloRestartPhase Phase{get;private set;}
        public SoloStageRestartTicket Ticket{get;private set;}
        public int Generation{get;private set;}
        public bool SeedConsumed{get;private set;}
        double deadline;
        public bool HoldsSimulation=>Phase!=SoloRestartPhase.Idle&&Phase!=SoloRestartPhase.Complete;
        public bool Begin(SoloStageRestartTicket ticket,double now)
        {
            if(!ticket.Valid||!Finite(now)||now<0||now>1e12||HoldsSimulation||Generation==int.MaxValue)return false;
            Generation++;Ticket=ticket;SeedConsumed=false;Phase=SoloRestartPhase.Captured;deadline=now+30;return true;
        }
        public bool Advance(int generation,SoloRestartPhase expected,SoloRestartPhase next,double now,double seconds)
        {
            if(generation!=Generation||Phase!=expected||!Finite(now)||now<0||now>1e12||!Finite(seconds)||seconds<=0||seconds>180||now>deadline||
                (int)next!=(int)expected+1||next==SoloRestartPhase.AwaitingResume&&!SeedConsumed)return false;
            Phase=next;deadline=now+seconds;return true;
        }
        public bool ConsumeSeed(int generation,int newRoomOperation,int admittedMembers,int admittedSlot,bool soleHost,out SoloStageRestartTicket ticket)
        {
            ticket=default;
            if(generation!=Generation||Phase!=SoloRestartPhase.Preparing||SeedConsumed||!Ticket.Valid||!soleHost||admittedMembers!=1||admittedSlot!=0||newRoomOperation<=Ticket.SourceRoomOperation)return false;
            SeedConsumed=true;ticket=Ticket;return true;
        }
        public bool ArmResume()=>Phase==SoloRestartPhase.ResumeArmed||Phase==SoloRestartPhase.AwaitingResume&&Set(SoloRestartPhase.ResumeArmed);
        public bool CompleteResume()=>Phase==SoloRestartPhase.ResumeArmed&&SeedConsumed&&Set(SoloRestartPhase.Complete);
        bool Set(SoloRestartPhase phase){Phase=phase;return true;}
        public bool TimedOut(double now)=>HoldsSimulation&&Phase!=SoloRestartPhase.Failed&&Phase!=SoloRestartPhase.AwaitingResume&&Phase!=SoloRestartPhase.ResumeArmed&&Finite(now)&&now>deadline;
        public void Fail(){Phase=SoloRestartPhase.Failed;}
        public void Cancel(){if(Generation<int.MaxValue)Generation++;Ticket=default;SeedConsumed=false;Phase=SoloRestartPhase.Idle;}
        static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
    }
    public static class AcceptedPartyDifficulty
    {
        // Formula remains modular; the actual room adapter enforces the current two-player cap.
        public static bool TryMultiplier(int authenticatedMembers,out float multiplier)
        {
            multiplier=1;
            if(authenticatedMembers<1||authenticatedMembers>2)return false;
            multiplier=ScaleForAdditionalMembers(authenticatedMembers);return true;
        }
        public static float ScaleForAdditionalMembers(int members)
        {
            if(members<1||members>8)throw new ArgumentOutOfRangeException(nameof(members));
            return (float)Math.Pow(1.1d,members-1);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: accepted campaign/room authority generations, independent of actor/world positions.
// Array Formats: immutable scalar ticket/progress; bounded pure lifecycle gate; no save, network or Unity object references.
// Dependencies/Pointers: trusted owner callbacks construct tickets; new sole admitted host consumes once; replayed-stage rewards suppressed; actual party capped2 and health/damage-only adapters own scaling.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world and accepted015 campaign/source identity, original arena positions retained.
// Array Formats: Eight milestones/40waves/905logical rewards within4096native slots, fixed128B XP channel and existing bounded pools.
// Dependencies/Pointers: Root composes actual current pool/audio owners with reviewed campaign/XP hunks; source/native/browser/performance/public acceptance remain separate.
