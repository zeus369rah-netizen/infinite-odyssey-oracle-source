using System;
using System.Globalization;

namespace OdysseyOracle
{
    public enum ReadinessHudSignal{Waiting,AllReady,Preparing,Starting,Running,Return,Complete,Failed,Syncing,Solo,Disconnected}
    public struct ReadinessHudInput
    {
        public bool Show,Connected,OfflineSolo,CampaignActive,MembershipMatches,Running,ArenaReady;
        public int Members,Ready,Level;public CampaignPhase Phase;public float RemainingSeconds;
    }
    public struct ReadinessHudView
    {
        public bool Visible,CountsKnown,AllReady;public int Members,Ready,Level,Seconds;
        public ReadinessHudSignal Signal;
    }
    public struct ReadinessHudFit{public float Scale;public bool FooterFits;}
    public static class ReadinessHudMath
    {
        public const float FooterHeight=62,Gap=8,MapTop=36,BottomReserve=300,MinimumScale=.45f;
        public static ReadinessHudView Evaluate(ReadinessHudInput input)
        {
            var v=new ReadinessHudView{Visible=input.Show,Ready=-1,Level=input.CampaignActive&&input.Level>=1&&input.Level<=8?input.Level:0,Seconds=-1};
            if(!input.Show)return v;
            if(!input.Connected){v.Members=input.OfflineSolo?1:0;v.Ready=input.OfflineSolo?0:-1;v.CountsKnown=input.OfflineSolo;v.Signal=input.OfflineSolo?ReadinessHudSignal.Solo:ReadinessHudSignal.Disconnected;return v;}
            if(input.Members<1||input.Members>2||input.Ready<0||input.Ready>input.Members||input.CampaignActive&&!input.MembershipMatches)
            {v.Members=input.Members>=1&&input.Members<=2?input.Members:0;v.Signal=ReadinessHudSignal.Syncing;return v;}
            v.Members=input.Members;v.Ready=input.Ready;v.CountsKnown=true;v.AllReady=v.Ready==v.Members;
            if(input.CampaignActive)
            {
                switch(input.Phase)
                {
                    case CampaignPhase.StageClear:v.Ready=-1;v.CountsKnown=v.AllReady=false;v.Signal=ReadinessHudSignal.Return;return v;
                    case CampaignPhase.Complete:v.Ready=-1;v.CountsKnown=v.AllReady=false;v.Signal=ReadinessHudSignal.Complete;return v;
                    case CampaignPhase.Failed:v.Ready=-1;v.CountsKnown=v.AllReady=false;v.Signal=ReadinessHudSignal.Failed;return v;
                    case CampaignPhase.Preparing:v.Signal=input.ArenaReady?ReadinessHudSignal.Starting:ReadinessHudSignal.Preparing;return v;
                    case CampaignPhase.Defense:
                        v.Signal=input.Running&&input.ArenaReady?ReadinessHudSignal.Running:ReadinessHudSignal.Starting;
                        if(v.Signal==ReadinessHudSignal.Running&&!float.IsNaN(input.RemainingSeconds)&&!float.IsInfinity(input.RemainingSeconds)&&input.RemainingSeconds>=0&&input.RemainingSeconds<=180)
                            v.Seconds=(int)Math.Ceiling(input.RemainingSeconds);
                        return v;
                }
            }
            v.Signal=v.AllReady?ReadinessHudSignal.AllReady:ReadinessHudSignal.Waiting;return v;
        }
        public static ReadinessHudFit Fit(float width,float height,float mapHeight,float requested,float availableWidth,bool allowFooter,float additionalBelow=0)
        {
            if(!Finite(width)||!Finite(height)||!Finite(mapHeight)||!Finite(requested)||!Finite(availableWidth)||width<=0||height<=0||mapHeight<=0||requested<=0)return new ReadinessHudFit{Scale=MinimumScale};
            if(!Finite(additionalBelow)||additionalBelow<0||additionalBelow>128)additionalBelow=0;
            float widthFit=Math.Max(140,availableWidth)/229f,space=height-MapTop-BottomReserve-additionalBelow;
            float withFooter=Math.Min(requested,Math.Min(widthFit,space/(mapHeight+FooterHeight+Gap)));
            bool fits=allowFooter&&withFooter>=MinimumScale&&MapTop+229f*withFooter<=width-12;
            float scale=fits?withFooter:Math.Max(MinimumScale,Math.Min(requested,Math.Min(widthFit,space/mapHeight)));
            return new ReadinessHudFit{Scale=scale,FooterFits=fits};
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
    public static class ReadinessHudLabels
    {
        public static readonly string[] Digits={"0","1","2","3","4","5","6","7","8"};
        public static readonly string[] Level=BuildLevels(),LevelSeconds=BuildSeconds();
        static readonly string[] Known={"READY 0/1","READY 1/1","READY 0/2","READY 1/2","READY 2/2"};
        static readonly string[] Unknown={"READY —","READY —/1","READY —/2"};
        public static void Warm(){_=Level.Length;_=LevelSeconds.Length;}
        public static string Ready(ReadinessHudView view)=>!view.CountsKnown||view.Members<1||view.Members>2||view.Ready<0||view.Ready>view.Members?
            Unknown[view.Members>=1&&view.Members<=2?view.Members:0]:view.Members==1?Known[view.Ready]:Known[view.Ready+2];
        public static string Status(ReadinessHudView view)
        {
            switch(view.Signal)
            {
                case ReadinessHudSignal.AllReady:return "ALL READY";
                case ReadinessHudSignal.Preparing:return "PREPARING";
                case ReadinessHudSignal.Starting:return "STARTING";
                case ReadinessHudSignal.Running:return view.Seconds>=0&&view.Seconds<=180?LevelSeconds[view.Seconds]:"IN LEVEL";
                case ReadinessHudSignal.Return:return "RETURN TO REFUGE";
                case ReadinessHudSignal.Complete:return "COMPLETE";
                case ReadinessHudSignal.Failed:return "LEVEL ENDED";
                case ReadinessHudSignal.Syncing:return "SYNCING PARTY";
                case ReadinessHudSignal.Solo:return "SOLO · PORTAL";
                case ReadinessHudSignal.Disconnected:return "NOT CONNECTED";
                default:return "WAITING";
            }
        }
        static string[] BuildLevels(){var result=new string[9];result[0]="CHALLENGES · 8 LEVELS";for(int i=1;i<=8;i++)result[i]="LEVEL "+Digits[i]+" / 8";return result;}
        static string[] BuildSeconds(){var result=new string[181];for(int i=0;i<result.Length;i++)result[i]="LEVEL · "+i.ToString(CultureInfo.InvariantCulture)+"s LEFT";return result;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted Room/Campaign scalar readiness and existing Canvas map/footer fit; no world queries or clock ownership.
// Array Formats: Pure value view/input/fit, cold0..8 level/digit and0..180 actual-level-second strings; live counts strictly1/2, unknown stays unknown.
// Dependencies/Pointers: Owner phase/ready/RemainingSeconds read-only; no fabricated start countdown, local-time interpolation, allocation or Unity APIs in Evaluate/Fit/label selection. Build/Warm caches run cold.
