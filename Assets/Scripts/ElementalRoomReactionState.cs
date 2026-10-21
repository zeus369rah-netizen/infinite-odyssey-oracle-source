using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

namespace OdysseyOracle
{
    public enum RoomReactionKind : uint { Light=1,FixtureLight=2,Candle=3,VoxelFlame=4,LivingFlame=5,Lantern=6,TorchGlow=7,Hearth=8,Plant=9 }
    [Serializable,StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct RoomReactionNode32
    {
        public float3 Center;public float Radius;
        public RoomReactionKind Kind;public uint OwnerSlot,StableId,Flags;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct RoomReactionSeen16 { public ulong Caster;public uint Shot,Generation; }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct RoomReactionFrame112
    {
        public uint Magic,Version,Epoch,Generation,Revision,Count;
        public ulong LayoutHash,Bits0,Bits1,Bits2,Bits3,Bits4,Bits5,Bits6,Bits7;
        public uint PlantStage,PlantStartTick,ServerTick,Reserved;
        public ulong Word(int word)
        {
            switch(word){case 0:return Bits0;case 1:return Bits1;case 2:return Bits2;case 3:return Bits3;case 4:return Bits4;case 5:return Bits5;case 6:return Bits6;case 7:return Bits7;default:return 0;}
        }
        public void SetWord(int word,ulong value)
        {
            switch(word){case 0:Bits0=value;break;case 1:Bits1=value;break;case 2:Bits2=value;break;case 3:Bits3=value;break;case 4:Bits4=value;break;case 5:Bits5=value;break;case 6:Bits6=value;break;case 7:Bits7=value;break;}
        }
    }
    // Persistent single owner; jobs are always completed before any consumer gets a value.
    public sealed class ElementalRoomReactionState : IDisposable
    {
        public const int Capacity=512,Words=8,SeenCapacity=48;
        public const uint FrameMagic=0x4f525231,FrameVersion=1;
        private NativeArray<RoomReactionNode32> Nodes;
        private NativeArray<float> Gains;
        private NativeArray<ulong> Targets;
        private NativeArray<RoomReactionSeen16> seen;
        private NativeArray<RoomReactionRuntime48> runtime;
        private NativeArray<RoomReactionContact8> queryResult;
        private NativeArray<uint> changedResult;
        private JobHandle fence;
        public int Count{get;private set;}
        public ulong LayoutHash{get;private set;}
        public uint Generation=>ReadRuntime().Generation;
        public uint Revision=>ReadRuntime().Revision;
        public uint PlantStage=>ReadRuntime().PlantStage;
        public uint PlantStartTick=>ReadRuntime().PlantStartTick;
        public uint ServerTick=>ReadRuntime().ServerTick;
        public float Growth=>ReadRuntime().Growth;
        public bool HasSnapshot=>ReadRuntime().HasSnapshot!=0;
        public uint EligibleFlameCount=>ReadRuntime().EligibleFlames;
        public uint EligibleLitFlameCount=>ReadRuntime().EligibleLitFlames;
        public bool IsCreated=>Nodes.IsCreated&&Gains.IsCreated&&Targets.IsCreated&&seen.IsCreated&&runtime.IsCreated&&queryResult.IsCreated&&changedResult.IsCreated;
        private void CompleteFence(){fence.Complete();}
        private RoomReactionRuntime48 ReadRuntime(){CompleteFence();return runtime.IsCreated?runtime[0]:default;}
        public ElementalRoomReactionState(RoomReactionNode32[] source,int count)
        {
            if(source==null||count<1||count>Capacity||count>source.Length)throw new ArgumentException("Valid bounded cold room nodes required.");
            try
            {
                Nodes=new NativeArray<RoomReactionNode32>(Capacity,Allocator.Persistent);Gains=new NativeArray<float>(Capacity,Allocator.Persistent);
                Targets=new NativeArray<ulong>(Words,Allocator.Persistent);seen=new NativeArray<RoomReactionSeen16>(SeenCapacity,Allocator.Persistent);
                runtime=new NativeArray<RoomReactionRuntime48>(1,Allocator.Persistent);queryResult=new NativeArray<RoomReactionContact8>(1,Allocator.Persistent);changedResult=new NativeArray<uint>(1,Allocator.Persistent);
                Count=count;ulong hash=1469598103934665603UL;
                // Cold layout identity only; not a repeated spatial query.
                for(int i=0;i<count;i++)
                {
                    var node=source[i];if(!ValidNode(node))throw new ArgumentException("Invalid room reaction node.");Nodes[i]=node;int3 mm=(int3)math.round(node.Center*1000f);
                    hash=Mix(hash,(uint)mm.x);hash=Mix(hash,(uint)mm.y);hash=Mix(hash,(uint)mm.z);hash=Mix(hash,(uint)math.round(node.Radius*1000f));
                    hash=Mix(hash,(uint)node.Kind);hash=Mix(hash,node.StableId);hash=Mix(hash,node.Flags);hash=Mix(hash,node.OwnerSlot);
                }
                LayoutHash=hash==0?1:hash;Reset(1,true);
            }
            catch{Dispose();throw;}
        }
        // Cold world/connection boundary. The previous fence is complete before any array write.
        public void Reset(uint generation,bool authoredDefaults)
        {
            CompleteFence();if(!IsCreated)return;
            var reset=new RoomReactionRuntime48{Generation=generation==0?1:generation,Revision=1,Count=(uint)Count};
            for(int w=0;w<Words;w++)Targets[w]=0;
            for(int i=0;i<Count;i++){bool lit=authoredDefaults&&Nodes[i].Kind!=RoomReactionKind.Plant&&(Nodes[i].Flags&1u)==0;RoomReactionKernelMath.SetTarget(Targets,i,lit);Gains[i]=lit?1:0;
                if((Nodes[i].Flags&RefugeFlameRules020.EligibleFlag)!=0){reset.EligibleFlames++;if(lit)reset.EligibleLitFlames++;}}
            runtime[0]=reset;
            for(int i=0;i<SeenCapacity;i++)seen[i]=default;
        }
        public void Tick(float delta,bool authority)
        {
            if(!IsCreated||!Finite(delta)||delta<0)return;CompleteFence();delta=math.min(delta,.1f);
            var clock=new RoomReactionClockJob{Runtime=runtime,Delta=delta,Authority=authority?1u:0u};
            var gain=new RoomReactionGainJob{Targets=Targets,Gains=Gains,Delta=delta};
            fence=clock.Schedule(fence);
            fence=gain.Schedule(Count,64,fence);
            CompleteFence();
        }
        public bool Query(int ingredient,float3 origin,float3 direction,float distance,float projectileRadius,out float contact)
        {
            contact=distance;if(!IsCreated||!Single(ingredient)||!Point(origin)||!Point(direction)||!Finite(distance)||distance<=.0001f||!Finite(projectileRadius)||projectileRadius<0||projectileRadius>.6f||math.abs(math.lengthsq(direction)-1)>.01f)return false;
            CompleteFence();var query=new RoomReactionQueryJob{Nodes=Nodes,Result=queryResult,Count=Count,Ingredient=ingredient,Origin=origin,Direction=direction,Distance=distance,ProjectileRadius=projectileRadius};
            fence=query.Schedule(fence);CompleteFence();var result=queryResult[0];contact=result.Distance;return result.Found!=0;
        }
        public bool AcceptHit(bool authority,ulong caster,uint shot,int ingredient,float3 point)=>AcceptHit(authority,caster,shot,ingredient,point,out _);
        public bool AcceptHit(bool authority,ulong caster,uint shot,int ingredient,float3 point,out RoomReactionAccepted24 accepted)
        {
            accepted=default;if(!IsCreated||!authority||shot==0||!Single(ingredient)||!Point(point))return false;CompleteFence();
            var before=runtime[0];
            var mutation=new RoomReactionAcceptJob{Nodes=Nodes,Targets=Targets,Seen=seen,Runtime=runtime,Changed=changedResult,Count=Count,Ingredient=ingredient,Caster=caster,Shot=shot,Position=point};
            fence=mutation.Schedule(fence);CompleteFence();uint decision=changedResult[0];if((decision&1u)==0)return false;
            var after=runtime[0];accepted=new RoomReactionAccepted24{Generation=after.Generation,Revision=after.Revision,Extinguished=decision>>1,EligibleCount=after.EligibleFlames,BeforeLit=before.EligibleLitFlames,AfterLit=after.EligibleLitFlames};return true;
        }
        // Cold/2Hz wire pack/validation is intentionally serial, not claimed Burst.
        public RoomReactionFrame112 Capture(uint epoch)
        {
            CompleteFence();RoomReactionRuntime48 state=ReadRuntime();
            var frame=new RoomReactionFrame112{Magic=FrameMagic,Version=FrameVersion,Epoch=epoch,Generation=state.Generation,Revision=state.Revision,Count=(uint)Count,LayoutHash=LayoutHash,PlantStage=state.PlantStage,PlantStartTick=state.PlantStartTick,ServerTick=state.ServerTick};
            if(IsCreated)for(int i=0;i<Words;i++)frame.SetWord(i,Targets[i]);return frame;
        }
        public bool Apply(in RoomReactionFrame112 frame,uint expectedEpoch)
        {
            CompleteFence();if(!IsCreated)return false;RoomReactionRuntime48 state=runtime[0];
            if(expectedEpoch==0||frame.Epoch!=expectedEpoch||frame.Magic!=FrameMagic||frame.Version!=FrameVersion||frame.Generation==0||frame.Revision==0||frame.Count!=Count||frame.LayoutHash!=LayoutHash||frame.Reserved!=0||frame.PlantStage>2||frame.PlantStage==0&&frame.PlantStartTick!=0||frame.PlantStage!=0&&frame.ServerTick<frame.PlantStartTick)return false;
            for(int word=0;word<Words;word++)
            {
                int remaining=Count-word*64;ulong allowed=remaining>=64?ulong.MaxValue:remaining<=0?0:(1UL<<remaining)-1;if((frame.Word(word)&~allowed)!=0)return false;
            }
            if(frame.PlantStage==1&&frame.ServerTick-frame.PlantStartTick>=30||frame.PlantStage==2&&frame.ServerTick-frame.PlantStartTick<30)return false;
            uint eligibleLit=0;
            for(int i=0;i<Count;i++){bool lit=(frame.Word(i>>6)&(1UL<<(i&63)))!=0;if(Nodes[i].Kind==RoomReactionKind.Plant&&lit)return false;if((Nodes[i].Flags&RefugeFlameRules020.EligibleFlag)!=0&&lit)eligibleLit++;}
            bool first=state.HasSnapshot==0;
            if(!first)
            {
                if(frame.Generation!=state.Generation&&!Newer(frame.Generation,state.Generation))return false;
                if(frame.Generation==state.Generation)
                {
                    if(frame.Revision==state.Revision)
                    {
                        if(frame.PlantStage!=state.PlantStage||frame.PlantStartTick!=state.PlantStartTick)return false;
                        for(int w=0;w<Words;w++)if(frame.Word(w)!=Targets[w])return false;
                        if(frame.ServerTick>=state.ServerTick)state.ServerTick=frame.ServerTick;runtime[0]=state;return true;
                    }
                    if(!Newer(frame.Revision,state.Revision))return false;
                }
            }
            bool newGeneration=frame.Generation!=state.Generation;
            state.Generation=frame.Generation;state.Revision=frame.Revision;state.PlantStage=frame.PlantStage;state.PlantStartTick=frame.PlantStartTick;state.ServerTick=frame.ServerTick;
            for(int w=0;w<Words;w++)Targets[w]=frame.Word(w);
            if(first||newGeneration){for(int i=0;i<Count;i++)Gains[i]=RoomReactionKernelMath.Target(Targets,i)?1:0;state.Growth=state.PlantStage==0?0:state.PlantStage==1?.45f:1;}
            state.EligibleLitFlames=eligibleLit;state.HasSnapshot=1;runtime[0]=state;return true;
        }
        public float GainAt(int index){CompleteFence();return index>=0&&index<Count?Gains[index]:0;}
        public bool Target(int index){CompleteFence();return index>=0&&index<Count&&RoomReactionKernelMath.Target(Targets,index);}
        public static bool Newer(uint value,uint previous)=>value!=previous&&unchecked(value-previous)<0x80000000u;
        public static bool Single(int mask)=>RoomReactionKernelMath.Single(mask);
        public static bool Finite(float value)=>RoomReactionKernelMath.Finite(value);
        public static bool Point(float3 value)=>RoomReactionKernelMath.Point(value);
        private static bool ValidNode(RoomReactionNode32 n)=>Point(n.Center)&&Finite(n.Radius)&&n.Radius>=.05f&&n.Radius<=1.2f&&(uint)n.Kind>=1&&(uint)n.Kind<=9&&n.StableId!=0&&(n.Flags&~3u)==0&&((n.Flags&RefugeFlameRules020.EligibleFlag)==0||RefugeFlameRules020.Eligible(n.Kind,n.Flags));
        private static ulong Mix(ulong hash,uint value){unchecked{return(hash^value)*1099511628211UL;}}
        public void Dispose()
        {
            CompleteFence();if(changedResult.IsCreated)changedResult.Dispose();if(queryResult.IsCreated)queryResult.Dispose();if(runtime.IsCreated)runtime.Dispose();
            if(seen.IsCreated)seen.Dispose();if(Targets.IsCreated)Targets.Dispose();if(Gains.IsCreated)Gains.Dispose();if(Nodes.IsCreated)Nodes.Dispose();Count=0;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Numeric supported-fixture world metres/Y-up. Scheduled query/neighbor/clock/gain kernels have no Object API; main-thread callers get values only after the owner fence completes.
// Array Formats: Fixed512 Node32/float gains,8ulong targets,48Seen16,Runtime48[1],Contact8[1],uint changed[1];19324 payload bytes plus native allocator bookkeeping. Exact112-byte wire frame unchanged.
// Dependencies/Pointers: One NativeArray owner/JobHandle; Query IJob,Accept IJob,Clock IJob andGain IJobParallelFor complete before network serial mutation, scene presentation and dispose/reload. Cold identity/reset/wire validation loops remain serial and are not claimed Burst; root native job/Burst/safety/performance acceptance remains pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: unchanged accepted reaction world domain and snapshot coordinates.
// Array Formats: same19324B native payload, exact Frame112 and Runtime48 sizes; reused padding stores counts, Accepted24 is stack-only.
// Dependencies/Pointers: existing node hash includes eligibility and default-state facts; reset/relight/snapshot restore coherent counts without award; previous AcceptHit API retained, existing wire epoch/generation/revision/layout/reserved validation unchanged.
