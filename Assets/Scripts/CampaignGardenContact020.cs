// PSEUDOCODE:
// Cold-validate128 actual copied roots and their stable IDs; sort root indices by Morton/PlantId into <=128 cell ranges.
// Validate the exact value lease, audio epoch and monotonic sequence before changing any pair state.
// Copy the256-pair source bank; query <=9 Morton cells/player and latch actual swept/vertical enters, including dropped enters.
// Maintain .20m/.15s exit hysteresis in Morton order; silently rebase initial/held/policy/gap/teleport samples.
// Select <=2 eligible nearest enters, consume cooldown/serial attempts once, and publish two true capsule envelopes plus scoped tokens.
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CampaignGardenContactScope020
    {
        public ulong WorldToken,Epoch;
        public uint Generation,WorldRevision,GeometryRevision,FoliageRevision;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CampaignGardenContactPlayer020
    {
        public ulong PlayerId;
        public float3 FeetWorld;
        public float Radius,Height;
        public byte Accepted;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CampaignGardenContactCell020
    {
        public uint Morton;
        public int Start,Count;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CampaignGardenContactIndex020
    {
        public CampaignGardenContactScope020 Scope;
        public int RangeCount;
        public byte Ready;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CampaignGardenContactPair020
    {
        public double ExitQuiet;
        public float DistanceSquared;
        public byte Latched,Near,Entered,Eligible;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CampaignGardenContactPlayerState020
    {
        public ulong PlayerId;
        public double NextCue;
        public float3 PreviousFeet;
        public float Radius,Height;
        public byte Valid;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CampaignGardenContactState020
    {
        public CampaignGardenContactScope020 Scope;
        public ulong Sequence,Serial;
        public double Clock;
        public uint AudioEpoch,Step;
        public byte NeedRebase,LastEvents,LastMotion;
    }
    // Field order matches the frozen nature token; the main-thread owner copies Position into Vector3.
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CampaignGardenContactCue020
    {
        public uint OwnerEpoch,Generation,WorldRevision,GeometryRevision,FoliageRevision,Step;
        public ulong Serial,Plant,Player;
        public float3 Position;
        public float Strength;
        public int Variant;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct CampaignGardenContactFrame020
    {
        public CampaignGardenContactScope020 Scope;
        public ulong Sequence;
        public uint AudioEpoch,Step;
        public float4 Brush0,Brush1; // x,z,radius,strength; zero strength is inactive.
        public float2 Vertical0,Vertical1; // actual feetY,height; never a guessed floor.
        public CampaignGardenContactCue020 First,Second;
        public int CandidateTests,PairVisits,Failure;
        public byte TokenCount,Ready,Held;
    }

    public static class CampaignGardenContactMath020
    {
        public const int PlantCount=128,PlayerCount=2,PairCount=256,CellCapacity=128,TokenCapacity=2;
        public const float PlantRadius=.30f,EnterExtra=.10f,ExitGap=.20f,ExitQuiet=.15f;
        public const float MinimumSpeed=.35f,MaximumSpeed=12f,MinimumMove=.015f,MaximumGap=.10f,MaximumMove=.75f;
        public const double PlayerCooldown=.35,PlantCooldown=.65;
        public static bool ScopeValid(in CampaignGardenContactScope020 s)=>s.WorldToken!=0&&s.Epoch!=0&&
            s.Generation!=0&&s.WorldRevision!=0&&s.GeometryRevision!=0&&s.FoliageRevision!=0;
        public static bool Same(in CampaignGardenContactScope020 a,in CampaignGardenContactScope020 b)=>
            a.WorldToken==b.WorldToken&&a.Epoch==b.Epoch&&a.Generation==b.Generation&&a.WorldRevision==b.WorldRevision&&
            a.GeometryRevision==b.GeometryRevision&&a.FoliageRevision==b.FoliageRevision;
        public static uint Morton(int x,int z)
        {uint key=0;for(int bit=0;bit<7;bit++)key|=(((uint)x>>bit)&1u)<<(2*bit)|(((uint)z>>bit)&1u)<<(2*bit+1);return key;}
        public static bool TryCell(float3 p,out int x,out int z,out uint key)
        {
            x=z=0;key=0;if(!math.all(math.isfinite(p))||p.x< -128f||p.x>=128f||p.z< -128f||p.z>=128f)return false;
            // Integer64 offset after floor preserves the half-open upper boundary under float rounding.
            x=(int)math.floor(p.x*.5f)+64;z=(int)math.floor(p.z*.5f)+64;
            if(x<0||x>=128||z<0||z>=128)return false;key=Morton(x,z);return true;
        }
        public static bool PlayerValid(in CampaignGardenContactPlayer020 p)=>p.Accepted==1&&p.PlayerId!=0&&
            math.all(math.isfinite(p.FeetWorld))&&math.isfinite(p.Radius)&&p.Radius>0f&&p.Radius<=.5f&&
            math.isfinite(p.Height)&&p.Height>=p.Radius*2f&&p.Height<=4f&&TryCell(p.FeetWorld,out _,out _,out _);
        public static bool Vertical(float y,float height,in CampaignGardenPlantContact020 p)=>
            y<p.RootWorld.y+p.Height&&y+height>p.RootWorld.y;
        public static float DistanceSquared(float3 p,float3 q)
        {float2 d=new float2(p.x-q.x,p.z-q.z);return math.lengthsq(d);}
        public static bool Sweep(float3 from,in CampaignGardenContactPlayer020 player,in CampaignGardenPlantContact020 plant,out float d2)
        {
            // Capsule-axis versus plant-stem interval: each below/overlap/above region is one bounded quadratic.
            float2 a=new float2(from.x-plant.RootWorld.x,from.z-plant.RootWorld.z);
            float2 delta=new float2(player.FeetWorld.x-from.x,player.FeetWorld.z-from.z);
            float dy=player.FeetWorld.y-from.y,low=0f,high=1f;d2=float.PositiveInfinity;
            if(!Clip(from.y+player.Height-plant.RootWorld.y,dy,ref low,ref high)||
                !Clip(plant.RootWorld.y+plant.Height-from.y,-dy,ref low,ref high))return false;
            float below=plant.RootWorld.y-(from.y+player.Height-player.Radius);
            float above=from.y+player.Radius-(plant.RootWorld.y+plant.Height);
            float start=low,end=high;
            if(Clip(below,-dy,ref start,ref end))Minimum(a,delta,below,-dy,start,end,ref d2);
            start=low;end=high;
            if(Clip(-below,dy,ref start,ref end)&&Clip(-above,-dy,ref start,ref end))Minimum(a,delta,0f,0f,start,end,ref d2);
            start=low;end=high;
            if(Clip(above,dy,ref start,ref end))Minimum(a,delta,above,dy,start,end,ref d2);
            float reach=player.Radius+plant.Radius+EnterExtra;return d2<=reach*reach;
        }
        public static bool Current(in CampaignGardenContactPlayer020 player,in CampaignGardenPlantContact020 plant,float extra)
        {
            float radius=player.Radius+plant.Radius+extra;
            float gap=math.max(0f,math.max(plant.RootWorld.y-(player.FeetWorld.y+player.Height-player.Radius),
                player.FeetWorld.y+player.Radius-(plant.RootWorld.y+plant.Height)));
            return Vertical(player.FeetWorld.y,player.Height,plant)&&DistanceSquared(player.FeetWorld,plant.RootWorld)+gap*gap<=radius*radius;
        }
        static bool Clip(float origin,float delta,ref float low,ref float high)
        {
            if(math.abs(delta)<1e-8f)return origin>=0f;
            float point=-origin/delta;if(delta>0f)low=math.max(low,point);else high=math.min(high,point);return low<=high;
        }
        static void Minimum(float2 a,float2 delta,float gap,float gapDelta,float low,float high,ref float best)
        {
            float denominator=math.lengthsq(delta)+gapDelta*gapDelta;
            float t=denominator>1e-12f?math.clamp(-(math.dot(a,delta)+gap*gapDelta)/denominator,low,high):high;
            float y=gap+gapDelta*t;best=math.min(best,math.lengthsq(a+delta*t)+y*y);
        }
        public static int CellRange(NativeArray<CampaignGardenContactCell020> cells,int count,uint key)
        {
            int low=0,high=count-1;while(low<=high){int middle=(low+high)>>1;uint value=cells[middle].Morton;
                if(value==key)return middle;if(value<key)low=middle+1;else high=middle-1;}return -1;
        }
        public static bool Before(in CampaignGardenPlantContact020 a,in CampaignGardenPlantContact020 b)=>
            a.MortonXZ<b.MortonXZ||(a.MortonXZ==b.MortonXZ&&a.PlantId<b.PlantId);
    }

    [BurstCompile]
    public struct CampaignGardenContactIndexJob020:IJob
    {
        [ReadOnly]public NativeArray<CampaignGardenPlantContact020> Plants;
        public NativeArray<int> Order;
        public NativeArray<CampaignGardenContactCell020> Cells;
        public NativeArray<CampaignGardenContactIndex020> Index;
        public NativeArray<CampaignGardenContactPair020> FirstPairs,SecondPairs;
        public NativeArray<CampaignGardenContactPlayerState020> Players;
        public NativeArray<double> PlantNext;
        public NativeArray<CampaignGardenContactState020> State;
        public NativeArray<CampaignGardenContactFrame020> Frame;
        public CampaignGardenContactScope020 Scope;
        public uint AudioEpoch;
        public void Execute()
        {
            if(!Index.IsCreated||Index.Length!=1)return;Index[0]=default;
            if(!CampaignGardenContactMath020.ScopeValid(Scope)||AudioEpoch==0||!Plants.IsCreated||Plants.Length!=128||
                !Order.IsCreated||Order.Length!=128||!Cells.IsCreated||Cells.Length!=128||
                !FirstPairs.IsCreated||FirstPairs.Length!=256||!SecondPairs.IsCreated||SecondPairs.Length!=256||
                !Players.IsCreated||Players.Length!=2||!PlantNext.IsCreated||PlantNext.Length!=128||
                !State.IsCreated||State.Length!=1||!Frame.IsCreated||Frame.Length!=1)return;
            for(int i=0;i<128;i++)
            {
                var p=Plants[i];uint key=i<64?6000u+(uint)(i/8):6100u+(uint)((i-64)/8),ordinal=(uint)(i%8);
                if(p.AnchorKey!=key||p.Ordinal!=ordinal||p.PlantId!=(((ulong)key<<32)|(ordinal+1UL))||
                    !math.all(math.isfinite(p.RootWorld))||p.Radius!=.30f||!math.isfinite(p.Height)||p.Height<=0f||p.Height>(i<64?.93f:.72f)||
                    !CampaignGardenContactMath020.TryCell(p.RootWorld,out _,out _,out uint cell)||cell!=p.MortonXZ)return;
            }
            for(int i=0;i<128;i++)Order[i]=i;
            // Cold bounded insertion sort: <=8128 comparisons; no hot sort or borrowed publisher storage.
            for(int i=1;i<128;i++)
            {int item=Order[i],j=i-1;while(j>=0&&CampaignGardenContactMath020.Before(Plants[item],Plants[Order[j]])){Order[j+1]=Order[j];j--;}Order[j+1]=item;}
            int ranges=0;
            for(int start=0;start<128;)
            {
                uint cell=Plants[Order[start]].MortonXZ;int end=start+1;while(end<128&&Plants[Order[end]].MortonXZ==cell)end++;
                Cells[ranges++]=new CampaignGardenContactCell020{Morton=cell,Start=start,Count=end-start};start=end;
            }
            for(int i=0;i<256;i++){FirstPairs[i]=default;SecondPairs[i]=default;}
            for(int i=0;i<128;i++)PlantNext[i]=0;
            for(int i=0;i<2;i++)Players[i]=default;
            State[0]=new CampaignGardenContactState020{Scope=Scope,AudioEpoch=AudioEpoch,NeedRebase=1};Frame[0]=default;
            Index[0]=new CampaignGardenContactIndex020{Scope=Scope,RangeCount=ranges,Ready=1};
        }
    }

    // Nine explicit scalar fields keep neighborhood sorting allocation-free without an unsafe pointer or dynamic container.
    struct CampaignGardenContactNeighbors020
    {
        uint a,b,c,d,e,f,g,h,i;
        public uint Get(int n){switch(n){case 0:return a;case 1:return b;case 2:return c;case 3:return d;case 4:return e;case 5:return f;case 6:return g;case 7:return h;default:return i;}}
        public void Set(int n,uint v){switch(n){case 0:a=v;break;case 1:b=v;break;case 2:c=v;break;case 3:d=v;break;case 4:e=v;break;case 5:f=v;break;case 6:g=v;break;case 7:h=v;break;default:i=v;break;}}
        public int Fill(int x,int z)
        {
            int count=0;for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)
                if(x+dx>=0&&x+dx<128&&z+dz>=0&&z+dz<128)Set(count++,CampaignGardenContactMath020.Morton(x+dx,z+dz));
            for(int n=1;n<count;n++){uint value=Get(n);int j=n-1;while(j>=0&&Get(j)>value){Set(j+1,Get(j));j--;}Set(j+1,value);}return count;
        }
    }

    [BurstCompile]
    public struct CampaignGardenContactJob020:IJob
    {
        [ReadOnly]public NativeArray<CampaignGardenPlantContact020> Plants;
        [ReadOnly]public NativeArray<int> Order;
        [ReadOnly]public NativeArray<CampaignGardenContactCell020> Cells;
        [ReadOnly]public NativeArray<CampaignGardenContactIndex020> Index;
        [ReadOnly]public NativeArray<CampaignGardenContactPair020> SourcePairs;
        public NativeArray<CampaignGardenContactPair020> TargetPairs;
        [ReadOnly]public NativeArray<CampaignGardenContactPlayer020> InputPlayers;
        public NativeArray<CampaignGardenContactPlayerState020> Players;
        public NativeArray<double> PlantNext;
        public NativeArray<CampaignGardenContactState020> State;
        public NativeArray<CampaignGardenContactFrame020> Frame;
        public CampaignGardenContactScope020 Scope;
        public ulong Sequence;
        public uint AudioEpoch,Step;
        public float Delta;
        public byte Held,AllowEvents,AllowMotion;
        public void Execute()
        {
            if(!Frame.IsCreated||Frame.Length!=1)return;var previousFrame=Frame[0];Frame[0]=new CampaignGardenContactFrame020{Failure=1};
            if(!Plants.IsCreated||Plants.Length!=128||!Order.IsCreated||Order.Length!=128||!Cells.IsCreated||Cells.Length!=128||
                !Index.IsCreated||Index.Length!=1||!SourcePairs.IsCreated||SourcePairs.Length!=256||!TargetPairs.IsCreated||TargetPairs.Length!=256||
                !InputPlayers.IsCreated||InputPlayers.Length!=2||!Players.IsCreated||Players.Length!=2||!PlantNext.IsCreated||PlantNext.Length!=128||
                !State.IsCreated||State.Length!=1)return;
            var index=Index[0];var state=State[0];
            if(index.Ready!=1||index.RangeCount<1||index.RangeCount>128||!CampaignGardenContactMath020.ScopeValid(Scope)||
                !CampaignGardenContactMath020.Same(Scope,index.Scope)||!CampaignGardenContactMath020.Same(Scope,state.Scope)||
                AudioEpoch==0||AudioEpoch!=state.AudioEpoch||Step==0||Step==uint.MaxValue||Step<=state.Step||
                Sequence==0||Sequence==ulong.MaxValue||Sequence<=state.Sequence||state.Serial>ulong.MaxValue-3UL||
                !math.isfinite(Delta)||Delta<0f||Held>1||AllowEvents>1||AllowMotion>1)return;
            var p0=InputPlayers[0];var p1=InputPlayers[1];
            if(p0.Accepted==1&&p1.Accepted==1&&p0.PlayerId!=0&&p0.PlayerId==p1.PlayerId)return;
            bool hold=Held==1||Delta==0f;
            var frame=new CampaignGardenContactFrame020{Scope=Scope,Sequence=Sequence,AudioEpoch=AudioEpoch,Step=Step,Held=(byte)(hold?1:0)};
            bool policy=state.LastEvents!=AllowEvents||state.LastMotion!=AllowMotion;
            bool globalRebase=state.NeedRebase==1||policy||Delta>CampaignGardenContactMath020.MaximumGap||
                (state.Sequence!=0&&Sequence!=state.Sequence+1UL);
            double dt=hold?0d:math.min(Delta,CampaignGardenContactMath020.MaximumGap),clock=state.Clock+dt;
            if(!math.isfinite(clock))return;
            for(int player=0;player<2;player++)
            {
                var current=InputPlayers[player];var prior=Players[player];
                bool valid=CampaignGardenContactMath020.PlayerValid(current);
                bool rebase=globalRebase||prior.Valid!=1||prior.PlayerId!=current.PlayerId||prior.Radius!=current.Radius||prior.Height!=current.Height;
                float3 from=prior.PreviousFeet;float movement=valid&&prior.Valid==1?math.distance(current.FeetWorld,from):0f;
                float speed=Delta>0f?movement/Delta:0f;
                if(!math.isfinite(movement)||movement>CampaignGardenContactMath020.MaximumMove||!math.isfinite(speed)||speed>CampaignGardenContactMath020.MaximumSpeed)rebase=true;
                bool eligible=valid&&!hold&&!rebase&&AllowEvents==1&&movement>=CampaignGardenContactMath020.MinimumMove&&
                    speed>=CampaignGardenContactMath020.MinimumSpeed&&speed<=CampaignGardenContactMath020.MaximumSpeed;
                for(int order=0;order<128;order++)
                {
                    int root=Order[order],pairSlot=player*128+root;var pair=SourcePairs[pairSlot];pair.Near=pair.Entered=pair.Eligible=0;
                    if(!valid)pair=default;
                    else if(rebase&&!hold){pair=default;pair.Latched=(byte)(CampaignGardenContactMath020.Current(current,Plants[root],CampaignGardenContactMath020.EnterExtra)?1:0);}
                    TargetPairs[pairSlot]=pair;
                }
                if(valid&&!hold&&!rebase)
                {
                    CampaignGardenContactMath020.TryCell(current.FeetWorld,out int cellX,out int cellZ,out _);
                    CampaignGardenContactNeighbors020 neighbors=default;int count=neighbors.Fill(cellX,cellZ);
                    for(int n=0;n<count;n++)
                    {
                        int rangeIndex=CampaignGardenContactMath020.CellRange(Cells,index.RangeCount,neighbors.Get(n));if(rangeIndex<0)continue;
                        var range=Cells[rangeIndex];
                        for(int offset=0;offset<range.Count;offset++)
                        {
                            int root=Order[range.Start+offset],slot=player*128+root;frame.CandidateTests++;
                            var pair=TargetPairs[slot];var plant=Plants[root];
                            if(CampaignGardenContactMath020.Sweep(from,current,plant,out float d2))
                            {pair.Near=1;pair.DistanceSquared=d2;if(pair.Latched==0){pair.Latched=1;pair.Entered=1;pair.Eligible=(byte)(eligible?1:0);}}
                            TargetPairs[slot]=pair;
                        }
                    }
                }
                for(int order=0;order<128;order++)
                {
                    int root=Order[order],slot=player*128+root;var pair=TargetPairs[slot];frame.PairVisits++;
                    if(valid&&!hold&&!rebase&&pair.Latched==1)
                    {
                        if(CampaignGardenContactMath020.Current(current,Plants[root],CampaignGardenContactMath020.EnterExtra+CampaignGardenContactMath020.ExitGap))pair.ExitQuiet=0;
                        else{pair.ExitQuiet+=dt;if(pair.ExitQuiet>=CampaignGardenContactMath020.ExitQuiet){pair.Latched=0;pair.ExitQuiet=0;}}
                    }
                    TargetPairs[slot]=pair;
                }
                if(hold)
                {
                    if(valid&&prior.Valid==1&&prior.PlayerId==current.PlayerId&&AllowMotion==1&&previousFrame.Ready==1&&CampaignGardenContactMath020.Same(Scope,previousFrame.Scope))
                    {if(player==0){frame.Brush0=previousFrame.Brush0;frame.Vertical0=previousFrame.Vertical0;}else{frame.Brush1=previousFrame.Brush1;frame.Vertical1=previousFrame.Vertical1;}}
                }
                else
                {
                    if(valid&&AllowMotion==1)
                    {float4 brush=new float4(current.FeetWorld.x,current.FeetWorld.z,current.Radius,1f);float2 vertical=new float2(current.FeetWorld.y,current.Height);
                        if(player==0){frame.Brush0=brush;frame.Vertical0=vertical;}else{frame.Brush1=brush;frame.Vertical1=vertical;}}
                    if(!valid)prior=default;
                    else{if(prior.PlayerId!=current.PlayerId)prior.NextCue=clock;prior.PlayerId=current.PlayerId;prior.PreviousFeet=current.FeetWorld;prior.Radius=current.Radius;prior.Height=current.Height;prior.Valid=1;}
                    Players[player]=prior;
                }
            }
            // Each observed enter is already latched above. Rejection/cooldown/queue overflow never creates a later retry.
            if(!hold&&AllowEvents==1&&!globalRebase)
            for(int attempt=0;attempt<2;attempt++)
            {
                int best=-1;float distance=float.PositiveInfinity;ulong bestPlant=ulong.MaxValue,bestPlayer=ulong.MaxValue;
                for(int player=0;player<2;player++)
                {
                    var current=InputPlayers[player];var playerState=Players[player];if(playerState.Valid!=1||clock<playerState.NextCue)continue;
                    for(int order=0;order<128;order++)
                    {
                        int root=Order[order],slot=player*128+root;var pair=TargetPairs[slot];var plant=Plants[root];
                        if(pair.Entered!=1||pair.Eligible!=1||clock<PlantNext[root])continue;
                        if(pair.DistanceSquared<distance||(pair.DistanceSquared==distance&&(plant.PlantId<bestPlant||plant.PlantId==bestPlant&&current.PlayerId<bestPlayer)))
                        {best=slot;distance=pair.DistanceSquared;bestPlant=plant.PlantId;bestPlayer=current.PlayerId;}
                    }
                }
                if(best<0)break;
                int who=best/128,plantIndex=best%128;var plantValue=Plants[plantIndex];var playerValue=Players[who];
                var cue=new CampaignGardenContactCue020{OwnerEpoch=AudioEpoch,Generation=Scope.Generation,WorldRevision=Scope.WorldRevision,
                    GeometryRevision=Scope.GeometryRevision,FoliageRevision=Scope.FoliageRevision,Step=Step,Serial=++state.Serial,
                    Plant=plantValue.PlantId,Player=playerValue.PlayerId,Position=plantValue.RootWorld,
                    Strength=math.saturate(1f-math.sqrt(math.max(distance,0f))/(playerValue.Radius+plantValue.Radius+CampaignGardenContactMath020.EnterExtra)),
                    Variant=(int)((plantValue.PlantId^(plantValue.PlantId>>32)^state.Serial)&3UL)};
                if(frame.TokenCount++==0)frame.First=cue;else frame.Second=cue;
                playerValue.NextCue=clock+CampaignGardenContactMath020.PlayerCooldown;Players[who]=playerValue;
                PlantNext[plantIndex]=clock+CampaignGardenContactMath020.PlantCooldown;
            }
            state.Clock=clock;state.Sequence=Sequence;state.Step=Step;state.NeedRebase=(byte)(hold?1:0);
            state.LastEvents=AllowEvents;state.LastMotion=AllowMotion;State[0]=state;
            frame.Ready=1;frame.Failure=0;Frame[0]=frame;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted immutable world/Y-up roots and actual two capsule sweeps;2m/7-bit Morton XZ cells, true vertical overlap, fixed roots across all visual LODs.
// Array Formats:128 copied40B roots,128 int indices,128 cell ranges,2x256 pair banks,2 input/player states,128 shared cooldowns,one index/state/frame; <=256 query tests,256 maintenance visits and2 transient cue tokens.
// Dependencies/Pointers: Root cold lease owns distinct native buffers and completed writer/reader fences; exact session/world/root/publisher references plus full value scope are checked before/after dispatch. No Object APIs, hot allocation, audio playback or invented support/contact authority.
