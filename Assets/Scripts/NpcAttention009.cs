using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    public struct NpcAttentionInput009
    {
        public float3 EyeWorld,HeadWorld;
        public quaternion RootRotation;
        public float2 LimitRadians;
        public uint Enabled,Tracking,TalkSerial,Role,Speaking;
    }
    public struct NpcAttentionState009
    {
        public float2 Angles;
        public float4 Phase;
        public float BlinkWait,BlinkAge,TalkAge;
        public uint TalkSerial,Initialized;
    }
    public struct NpcAttentionOutput009
    {
        public quaternion Head,Left,Right;
        public float2 Angles;
        public float Breath,Blink,Talk,Glow,Jaw;
    }
    public static class NpcAttentionMath009
    {
        public const float MaximumDelta=.1f,Response=4.5f,TurnRate=1.9198622f;
        public static bool Valid(NpcAttentionInput009 input,float dt)
        {
            float n=math.lengthsq(input.RootRotation.value);
            return math.all(math.isfinite(input.EyeWorld))&&math.all(math.isfinite(input.HeadWorld))&&
                math.all(math.isfinite(input.RootRotation.value))&&n>.999f&&n<1.001f&&
                math.all(math.isfinite(input.LimitRadians))&&input.LimitRadians.x>0&&input.LimitRadians.x<=math.radians(70)&&
                input.LimitRadians.y>0&&input.LimitRadians.y<=math.radians(22)&&math.isfinite(dt)&&dt>0&&dt<=MaximumDelta&&
                input.Role>=1&&input.Role<=4&&input.Enabled<=1&&input.Tracking<=1&&input.Speaking<=1;
        }
        public static NpcAttentionOutput009 Step(NpcAttentionInput009 input,ref NpcAttentionState009 state,float dt)
        {
            var neutral=new NpcAttentionOutput009{Head=quaternion.identity,Left=quaternion.identity,Right=quaternion.identity,Glow=.94f};
            if(!Valid(input,dt)||input.Enabled==0){state=default;return neutral;}
            if(state.Initialized==0||!math.all(math.isfinite(state.Angles))||!math.all(math.isfinite(state.Phase))){
                state=default;state.Initialized=1;state.Phase=new float4(input.Role*.71f,input.Role*.33f,0,input.Role*.23f);
                state.BlinkWait=2.3f+input.Role*.23f;state.BlinkAge=-1;state.TalkAge=1;
            }
            state.Phase=math.fmod(state.Phase+dt*new float4(1.55f,.70f,.80f,8.5f),2*math.PI);
            if(state.TalkSerial!=input.TalkSerial){state.TalkSerial=input.TalkSerial;state.TalkAge=0;}
            state.TalkAge=math.min(1,state.TalkAge+dt);
            float talk=math.sin(math.saturate(state.TalkAge/.85f)*math.PI);
            if(state.TalkAge>=.85f)talk=0;
            float2 target=0;
            if(input.Tracking==1){
                float3 local=math.mul(math.conjugate(input.RootRotation),input.EyeWorld-input.HeadWorld);
                float planar=math.length(local.xz);
                float yaw=math.atan2(local.x,local.z);
                float fade=(1-math.smoothstep(6f,8f,planar))*(1-math.smoothstep(math.radians(95),math.radians(115),math.abs(yaw)));
                if(planar>.05f){target.x=math.clamp(yaw,-input.LimitRadians.x,input.LimitRadians.x)*fade;
                    target.y=math.clamp(-math.atan2(local.y,planar),-input.LimitRadians.y,input.LimitRadians.y)*fade;}
            }
            target.y=math.clamp(target.y+math.radians(5f)*talk,-input.LimitRadians.y,input.LimitRadians.y);
            float2 desired=(target-state.Angles)*(1-math.exp(-Response*dt));
            state.Angles=math.clamp(state.Angles+math.clamp(desired,-TurnRate*dt,TurnRate*dt),-input.LimitRadians,input.LimitRadians);
            state.BlinkWait-=dt;
            if(state.BlinkAge<0&&state.BlinkWait<=0)state.BlinkAge=0;
            float blink=0;
            if(state.BlinkAge>=0){state.BlinkAge+=dt;blink=math.sin(math.saturate(state.BlinkAge/.16f)*math.PI);
                if(state.BlinkAge>=.16f){state.BlinkAge=-1;state.BlinkWait=3.2f+.6f*math.sin(state.Phase.x);blink=0;}}
            float left=math.radians(math.sin(state.Phase.y)*.35f);
            float right=math.radians(math.sin(state.Phase.z)*.35f);
            // Bram lifts his intact lantern hand slightly; spear/grips remain attached.
            float acknowledgment=input.Role==3?math.radians(-4f)*talk:math.radians(-1.5f)*talk;
            return new NpcAttentionOutput009{
                Angles=state.Angles,Head=quaternion.EulerXYZ(state.Angles.y,state.Angles.x,0),
                Left=quaternion.EulerXYZ(left,0,right),Right=quaternion.EulerXYZ(left+acknowledgment,0,-right),
                Breath=math.sin(state.Phase.x),Blink=blink,Talk=talk,Glow=.94f+.06f*math.sin(state.Phase.y),
                Jaw=input.Speaking==1?(.3f+.7f*math.abs(math.sin(state.Phase.w)*math.sin(state.Phase.w*.7176f))):0
            };
        }
    }
    [BurstCompile]
    public struct NpcAttentionJob009:IJobParallelFor
    {
        [ReadOnly] public NativeArray<NpcAttentionInput009> Inputs;
        public NativeArray<NpcAttentionState009> States;
        [WriteOnly] public NativeArray<NpcAttentionOutput009> Outputs;
        public float DeltaSeconds;
        public void Execute(int index){var state=States[index];Outputs[index]=NpcAttentionMath009.Step(Inputs[index],ref state,DeltaSeconds);States[index]=state;}
    }
    public sealed class NpcAttentionLease009:IDisposable
    {
        NativeArray<NpcAttentionInput009> inputs;
        NativeArray<NpcAttentionState009> states;
        NativeArray<NpcAttentionOutput009> outputs;
        JobHandle handle;
        public bool IsCreated=>inputs.IsCreated;
        public NpcAttentionLease009(int count)
        {
            if(count<1||count>2)throw new ArgumentOutOfRangeException(nameof(count));
            try{inputs=new NativeArray<NpcAttentionInput009>(count,Allocator.Persistent);states=new NativeArray<NpcAttentionState009>(count,Allocator.Persistent);outputs=new NativeArray<NpcAttentionOutput009>(count,Allocator.Persistent);}
            catch{Dispose();throw;}
        }
        public void Set(int index,NpcAttentionInput009 value){handle.Complete();inputs[index]=value;}
        public bool Evaluate(float dt)
        {if(!math.isfinite(dt)||dt<=0)return false;handle.Complete();handle=new NpcAttentionJob009{Inputs=inputs,States=states,Outputs=outputs,DeltaSeconds=math.min(dt,.1f)}.Schedule(inputs.Length,1);handle.Complete();return true;}
        public NpcAttentionOutput009 Read(int index){handle.Complete();return outputs[index];}
        public void Dispose(){handle.Complete();if(inputs.IsCreated)inputs.Dispose();if(states.IsCreated)states.Dispose();if(outputs.IsCreated)outputs.Dispose();}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World eye/head snapshots -> yaw-only root-local radians; rigid offsets leave support/root untouched.
// Array Formats: Fixed1/2 persistent blittable input/state/output arrays per existing owner; no Unity Object or physics inside Burst job.
// Dependencies/Pointers: Cold lease allocation only, completed JobHandle before cached renderer reads/writes and disposal; numerical finite/rate/range limits do not establish native/Burst/browser acceptance.
