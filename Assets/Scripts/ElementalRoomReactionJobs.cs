using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct RoomReactionRuntime48
    {
        public uint Generation,Revision,PlantStage,PlantStartTick,ServerTick,SeenCursor;
        public float Growth,TickRemainder;
        public uint HasSnapshot,Count,EligibleFlames,EligibleLitFlames;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct RoomReactionContact8 {public float Distance;public uint Found;}
    public static class RoomReactionKernelMath
    {
        public static bool Single(int mask)=>mask==1||mask==4||mask==8;
        public static bool Finite(float value)=>math.isfinite(value);
        public static bool Point(float3 value)=>math.all(math.isfinite(value))&&math.all(math.abs(value)<=1000);
        public static float Move(float a,float b,float amount)=>a<b?math.min(b,a+amount):math.max(b,a-amount);
        public static bool Target(NativeArray<ulong> targets,int index)=>(targets[index>>6]&(1UL<<(index&63)))!=0;
        public static void SetTarget(NativeArray<ulong> targets,int index,bool value){int word=index>>6;ulong mask=1UL<<(index&63);targets[word]=value?targets[word]|mask:targets[word]&~mask;}
        public static void Advance(ref RoomReactionRuntime48 state){state.Revision++;if(state.Revision==0){state.Generation++;if(state.Generation==0)state.Generation=1;state.Revision=1;}}
        public static bool Sphere(float3 origin,float3 direction,float3 center,float radius,out float hit)
        {
            hit=0;float3 offset=origin-center;float c=math.dot(offset,offset)-radius*radius;if(c<=0)return true;
            float b=math.dot(offset,direction),discriminant=b*b-c;if(discriminant<0)return false;
            hit=-b-math.sqrt(discriminant);return hit>=0&&Finite(hit);
        }
    }
    [BurstCompile]
    public struct RoomReactionQueryJob:IJob
    {
        [ReadOnly] public NativeArray<RoomReactionNode32> Nodes;
        public NativeArray<RoomReactionContact8> Result;
        public int Count,Ingredient;public float3 Origin,Direction;public float Distance,ProjectileRadius;
        public void Execute()
        {
            RoomReactionContact8 result=new RoomReactionContact8{Distance=Distance};
            for(int i=0;i<Count;i++)
            {
                var node=Nodes[i];if(Ingredient==4?(node.Kind!=RoomReactionKind.Plant):(node.Kind==RoomReactionKind.Plant))continue;
                if(RoomReactionKernelMath.Sphere(Origin,Direction,node.Center,node.Radius+ProjectileRadius,out float hit)&&hit<result.Distance-.0001f){result.Distance=hit;result.Found=1;}
            }
            Result[0]=result;
        }
    }
    [BurstCompile]
    public struct RoomReactionAcceptJob:IJob
    {
        [ReadOnly] public NativeArray<RoomReactionNode32> Nodes;
        public NativeArray<ulong> Targets;
        public NativeArray<RoomReactionSeen16> Seen;
        public NativeArray<RoomReactionRuntime48> Runtime;
        public NativeArray<uint> Changed;
        public int Count,Ingredient;public ulong Caster;public uint Shot;public float3 Position;
        public void Execute()
        {
            Changed[0]=0;RoomReactionRuntime48 state=Runtime[0];bool nearby=false;
            for(int i=0;i<Count;i++)
            {
                var node=Nodes[i];if(Ingredient==4?(node.Kind!=RoomReactionKind.Plant):(node.Kind==RoomReactionKind.Plant))continue;
                float reach=Ingredient==8?3.25f:Ingredient==1?1.35f:node.Radius+.22f;
                if(math.lengthsq(node.Center-Position)<=reach*reach){nearby=true;break;}
            }
            if(!nearby)return;
            for(int i=0;i<ElementalRoomReactionState.SeenCapacity;i++)if(Seen[i].Generation==state.Generation&&Seen[i].Caster==Caster&&Seen[i].Shot==Shot)return;
            Seen[(int)state.SeenCursor]=new RoomReactionSeen16{Caster=Caster,Shot=Shot,Generation=state.Generation};state.SeenCursor=(state.SeenCursor+1)%ElementalRoomReactionState.SeenCapacity;
            bool changed=false;uint extinguished=0;
            for(int i=0;i<Count;i++)
            {
                var node=Nodes[i];float reach=Ingredient==8?3.25f:Ingredient==1?1.35f:node.Radius+.22f;
                if(math.lengthsq(node.Center-Position)>reach*reach)continue;
                if(Ingredient==4){if(node.Kind==RoomReactionKind.Plant&&state.PlantStage==0){state.PlantStage=1;state.PlantStartTick=state.ServerTick;changed=true;}continue;}
                if(node.Kind==RoomReactionKind.Plant)continue;
                bool lit=Ingredient==1;if(RoomReactionKernelMath.Target(Targets,i)!=lit){
                    RoomReactionKernelMath.SetTarget(Targets,i,lit);changed=true;
                    if(!lit&&RefugeFlameRules020.Visual(node.Kind))extinguished++;
                    if((node.Flags&RefugeFlameRules020.EligibleFlag)!=0){if(lit)state.EligibleLitFlames++;else if(state.EligibleLitFlames>0)state.EligibleLitFlames--;}
                }
            }
            if(changed){RoomReactionKernelMath.Advance(ref state);Changed[0]=1u|(extinguished<<1);}Runtime[0]=state;
        }
    }
    [BurstCompile]
    public struct RoomReactionClockJob:IJob
    {
        public NativeArray<RoomReactionRuntime48> Runtime;
        public float Delta;public uint Authority;
        public void Execute()
        {
            RoomReactionRuntime48 state=Runtime[0];
            if(Authority!=0)
            {
                state.TickRemainder+=Delta;
                while(state.TickRemainder>=.1f){state.TickRemainder-=.1f;state.ServerTick++;if(state.ServerTick==0)state.ServerTick=1;}
                if(state.PlantStage==1&&unchecked(state.ServerTick-state.PlantStartTick)>=30){state.PlantStage=2;RoomReactionKernelMath.Advance(ref state);}
            }
            state.Growth=RoomReactionKernelMath.Move(state.Growth,state.PlantStage==0?0:state.PlantStage==1?.45f:1,Delta*.5f);Runtime[0]=state;
        }
    }
    [BurstCompile]
    public struct RoomReactionGainJob:IJobParallelFor
    {
        [ReadOnly] public NativeArray<ulong> Targets;
        public NativeArray<float> Gains;
        public float Delta;
        public void Execute(int index){Gains[index]=RoomReactionKernelMath.Move(Gains[index],RoomReactionKernelMath.Target(Targets,index)?1:0,Delta*2.5f);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Finite numeric world metres/unit rays and delta seconds only; no Object/physics/render API in any kernel.
// Array Formats: Read-only Node32[512], sole-writer Runtime48[1],Seen16[48],8ulong target words,Contact8[1],uint decision[1]; parallel gains each own one float index. All buffers owned by ElementalRoomReactionState.
// Dependencies/Pointers: Burst IJob query/accepted-neighbor/dedup/packed-word/clock kernels and IJobParallelFor fade kernel. Owner completes one fence before serial snapshot mutation, main-thread Light/MPB presentation or dispose; CPU tests execute these exact kernels through storage/scheduling projection only.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: same pure accepted host world-neighbor mutation.
// Array Formats: Runtime48 reuses two padding uints for eligible total/lit; existing uint result bit0=changed, bits1..10=aggregate visual flame-off count.
// Dependencies/Pointers: same sealed Node32/Targets/Seen/runtime native owners and fence; only real bit transitions update counts, replay/no-op/plant/proxy-only changes cannot award; no new job framework.
