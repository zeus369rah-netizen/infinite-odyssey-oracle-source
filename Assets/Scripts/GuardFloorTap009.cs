using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    public struct GuardFloorTapInput009
    {
        public float FloorY,RootY,TipRestRootY,TorsoRestY,StartTorsoY;
        public quaternion ArmCurrent,ArmRest;
        public uint Enabled,Serial;
    }
    public struct GuardFloorTapState009{public float Age,StartY;public uint Serial,Active,ContactSent;}
    public struct GuardFloorTapOutput009{public float TorsoY;public quaternion Arm;public uint Active,Contact;}
    public static class GuardFloorTapMath009
    {
        public static GuardFloorTapOutput009 Step(GuardFloorTapInput009 input,ref GuardFloorTapState009 state,float dt)
        {
            var off=new GuardFloorTapOutput009{Arm=quaternion.identity};
            if(input.Enabled!=1||!math.isfinite(dt)||dt<=0||dt>.1f||
                !math.all(math.isfinite(new float4(input.FloorY,input.RootY,input.TipRestRootY,input.TorsoRestY)))||!math.isfinite(input.StartTorsoY)||
                !math.all(math.isfinite(input.ArmCurrent.value))||!math.all(math.isfinite(input.ArmRest.value))||
                math.abs(math.lengthsq(input.ArmCurrent.value)-1)>.001f||math.abs(math.lengthsq(input.ArmRest.value)-1)>.001f||
                math.abs(input.StartTorsoY-input.TorsoRestY)>.12f){state=default;return off;}
            float contact=input.FloorY-input.RootY-input.TipRestRootY+input.TorsoRestY;
            if(math.abs(contact-input.TorsoRestY)>.12f){state=default;return off;}
            if(state.Active==0||state.Serial!=input.Serial||!math.isfinite(state.Age)||!math.isfinite(state.StartY)){state=new GuardFloorTapState009{Active=1,Serial=input.Serial,StartY=input.StartTorsoY};}
            state.Age=math.min(1,state.Age+dt);
            float lift=math.smoothstep(0,.20f,state.Age);
            float drop=math.smoothstep(.20f,.36f,state.Age);
            float target=state.Age<.20f?math.lerp(state.StartY,contact+.10f,lift):math.lerp(contact+.10f,contact,drop);
            bool hit=state.Age>=.36f;
            if(hit)target=contact; // Direct contact assignment, never a residual interpolation offset.
            uint edge=hit&&state.ContactSent==0?1u:0u;if(hit)state.ContactSent=1;
            float armLock=math.smoothstep(0,.18f,state.Age);
            return new GuardFloorTapOutput009{Active=1,TorsoY=target,Arm=hit?input.ArmRest:math.slerp(input.ArmCurrent,input.ArmRest,armLock),Contact=edge};
        }
    }
    [BurstCompile]
    public struct GuardFloorTapJob009:IJobParallelFor
    {
        [ReadOnly] public NativeArray<GuardFloorTapInput009> Inputs;
        public NativeArray<GuardFloorTapState009> States;
        [WriteOnly] public NativeArray<GuardFloorTapOutput009> Outputs;
        public float DeltaSeconds;
        public void Execute(int index){var state=States[index];Outputs[index]=GuardFloorTapMath009.Step(Inputs[index],ref state,DeltaSeconds);States[index]=state;}
    }
    public sealed class GuardFloorTapLease009:IDisposable
    {
        NativeArray<GuardFloorTapInput009> input;NativeArray<GuardFloorTapState009> state;NativeArray<GuardFloorTapOutput009> output;JobHandle handle;
        public GuardFloorTapLease009(){try{input=new NativeArray<GuardFloorTapInput009>(2,Allocator.Persistent);state=new NativeArray<GuardFloorTapState009>(2,Allocator.Persistent);output=new NativeArray<GuardFloorTapOutput009>(2,Allocator.Persistent);}catch{Dispose();throw;}}
        public void Set(int index,GuardFloorTapInput009 value){handle.Complete();input[index]=value;}
        public bool Evaluate(float dt){if(!math.isfinite(dt)||dt<=0)return false;handle.Complete();handle=new GuardFloorTapJob009{Inputs=input,States=state,Outputs=output,DeltaSeconds=math.min(.1f,dt)}.Schedule(2,1);handle.Complete();return true;}
        public GuardFloorTapOutput009 Read(int index){handle.Complete();return output[index];}
        public void Reset(){handle.Complete();state[0]=state[1]=default;output[0]=output[1]=default;}
        public void Dispose(){handle.Complete();if(input.IsCreated)input.Dispose();if(state.IsCreated)state.Dispose();if(output.IsCreated)output.Dispose();}
    }
    public struct GuardToolSupport009{public float RootY;public int Vertex;public uint Valid;}
    [BurstCompile]
    public struct GuardToolSupportJob009:IJob
    {
        [ReadOnly] public NativeArray<float3> Vertices;
        public float4x4 ToolToRoot;
        public NativeArray<GuardToolSupport009> Result;
        public void Execute(){float y=float.PositiveInfinity;int vertex=-1;
            for(int i=0;i<Vertices.Length;i++){float3 p=math.transform(ToolToRoot,Vertices[i]);if(!math.all(math.isfinite(p))){Result[0]=default;return;}if(p.y<y){y=p.y;vertex=i;}}
            Result[0]=new GuardToolSupport009{RootY=y,Vertex=vertex,Valid=vertex>=0?1u:0u};}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact supported world floor and measured original tool rest vertex, translated through fixed root-local torso Y.
// Array Formats: Fixed2 persistent blittable gesture records and cold TempJob support-min result; no Unity Object inside numeric jobs.
// Dependencies/Pointers: Actual accepted talk serial/modal visibility gates start/hold; direct contact assignment at impact, closed grips/root feet preserved; complete handles before renderer writes/disposal.
