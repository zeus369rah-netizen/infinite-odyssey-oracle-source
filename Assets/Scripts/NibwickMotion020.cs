using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using OdysseyOracle.CampaignNpcs;

namespace OdysseyOracle
{
    public struct NibwickPose020 { public float3 Position,Scale; public quaternion Rotation; }
    [BurstCompile]
    public struct NibwickRouteValidationJob020 : IJob
    {
        public LandingAdmission020 Admission;
        [WriteOnly] public NativeArray<byte> Result;
        public void Execute() { Result[0]=NibwickTourMath020.ValidateRoute(Admission)?(byte)1:(byte)0; }
    }
    [BurstCompile]
    public struct NibwickMotionJob020 : IJob
    {
        [ReadOnly] public NativeArray<NibwickTourInput020> Input;
        [ReadOnly] public NativeArray<float3> Pivots;
        public NativeArray<NibwickTourState020> State;
        public NativeArray<NpcAttentionState009> Attention;
        [WriteOnly] public NativeArray<NibwickPose020> Output;
        public void Execute()
        {
            var input=Input[0];var state=State[0];NibwickTourMath020.Step(input,ref state);
            var root=new NpcVector(state.Position.X,state.BodyY,state.Position.Z);
            var leftLeg=NibwickRigidLeg020.Solve(root,state.Yaw,false,new NpcVector(state.LeftFoot.X,state.LeftFoot.Y,state.LeftFoot.Z),state.LeftYaw);
            var rightLeg=NibwickRigidLeg020.Solve(root,state.Yaw,true,new NpcVector(state.RightFoot.X,state.RightFoot.Y,state.RightFoot.Z),state.RightYaw);
            if(leftLeg.Valid!=1||rightLeg.Valid!=1){state.Generation=0;state.Phase=NibwickPhase020.Inactive;state.CancelVoice=1;}
            State[0]=state;
            var attention=Attention[0];
            var pose=NpcAttentionMath009.Step(new NpcAttentionInput009{
                Enabled=state.Generation!=0?(uint)1:0,Tracking=1,Role=1,TalkSerial=state.VoiceToken,
                Speaking=input.Speaking==1&&input.VoiceToken==state.VoiceToken?1u:0u,
                EyeWorld=new float3(input.Eye.X,input.Eye.Y,input.Eye.Z),
                HeadWorld=new float3(state.Position.X,state.BodyY+.82f,state.Position.Z),
                RootRotation=quaternion.RotateY(math.radians(state.Yaw)),LimitRadians=math.radians(new float2(45,18))
            },ref attention,math.min(.1f,input.Delta));
            Attention[0]=attention;
            for(int p=0;p<17;p++)Output[p]=new NibwickPose020{Position=Pivots[p],Scale=new float3(1),Rotation=quaternion.identity};
            float moving=state.StepActive==1?1:0;
            float phase=state.StepDuration>0?state.StepAge/state.StepDuration:0;
            float side=state.SwingRight==1?1:-1;
            Set(1,Pivots[1]+new float3(0,pose.Breath*.0035f,0),quaternion.RotateZ(math.radians(2.4f)*math.sin(phase*math.PI)*side*moving),new float3(1));
            Set(2,Pivots[2],pose.Head,new float3(1));
            Set(3,Pivots[3],quaternion.identity,new float3(1,1+pose.Jaw*2,1));
            Set(4,Pivots[4],quaternion.identity,new float3(1,.001f+.999f*pose.Blink,1));
            Set(5,Pivots[5],quaternion.RotateZ(math.radians(2.5f)*pose.Talk),new float3(1));
            Set(6,Pivots[6],quaternion.RotateZ(-math.radians(1.7f)*pose.Talk),new float3(1));
            float arm=math.radians(4f)*math.sin(phase*math.PI)*side*moving;
            Set(7,Pivots[7],math.mul(pose.Left,quaternion.RotateX(arm)),new float3(1));
            bool carry=state.StepActive==1||state.BodyY<state.Position.Y-.08f||math.abs(state.LeftFoot.Y-state.RightFoot.Y)>.1f;
            Set(8,Pivots[8],math.mul(pose.Right,quaternion.RotateX(-arm-(carry?math.radians(24):0))),new float3(1));
            Leg(9,13,15,leftLeg,state);Leg(10,14,16,rightLeg,state);
        }
        void Set(int index,float3 position,quaternion rotation,float3 scale)
        {Output[index]=new NibwickPose020{Position=position,Rotation=rotation,Scale=scale};}
        static quaternion Rotation(NibwickRotation020 q)=>new quaternion(q.X,q.Y,q.Z,q.W);
        float3 Local(NpcVector p,NibwickTourState020 state)
        {
            var inv=math.conjugate(quaternion.RotateY(math.radians(state.Yaw)));
            return math.mul(inv,new float3(p.X-state.Position.X,p.Y-state.BodyY,p.Z-state.Position.Z))-Pivots[0];
        }
        void Leg(int upper,int lower,int boot,NibwickLegPose020 leg,NibwickTourState020 state)
        {
            var a=Rotation(leg.Upper);var b=Rotation(leg.Lower);var c=Rotation(leg.Boot);
            var inv=math.conjugate(quaternion.RotateY(math.radians(state.Yaw)));
            Set(upper,Pivots[upper],math.mul(inv,a),new float3(1,NibwickRigidLeg020.UpperScaleY,1));
            Set(lower,Local(leg.Knee,state),math.mul(inv,b),new float3(1,NibwickRigidLeg020.LowerScaleY,1));
            Set(boot,Local(leg.Ankle,state),math.mul(inv,c),new float3(1));
        }
    }
    public sealed class NibwickMotionLease020 : IDisposable
    {
        NativeArray<NibwickTourInput020> inputs;
        NativeArray<NibwickTourState020> states;
        NativeArray<NpcAttentionState009> attention;
        NativeArray<NibwickPose020> poses;
        NativeArray<float3> pivots;
        NativeArray<byte> validation;
        JobHandle handle;
        public NibwickMotionLease020(float3[] sourcePivots)
        {
            if(sourcePivots==null||sourcePivots.Length!=17)throw new ArgumentException("Seventeen partitioned original cached pivots required.");
            try{
                inputs=new NativeArray<NibwickTourInput020>(1,Allocator.Persistent);
                states=new NativeArray<NibwickTourState020>(1,Allocator.Persistent);
                attention=new NativeArray<NpcAttentionState009>(1,Allocator.Persistent);
                poses=new NativeArray<NibwickPose020>(17,Allocator.Persistent);
                pivots=new NativeArray<float3>(sourcePivots,Allocator.Persistent);
                validation=new NativeArray<byte>(1,Allocator.Persistent);
            }catch{Dispose();throw;}
        }
        public bool Validate(in LandingAdmission020 admission)
        {handle.Complete();handle=new NibwickRouteValidationJob020{Admission=admission,Result=validation}.Schedule();handle.Complete();return validation[0]==1;}
        public NibwickTourState020 Evaluate(in NibwickTourInput020 input)
        {
            handle.Complete();inputs[0]=input;
            handle=new NibwickMotionJob020{Input=inputs,Pivots=pivots,State=states,Attention=attention,Output=poses}.Schedule();
            handle.Complete();return states[0];
        }
        public NibwickPose020 Pose(int index){handle.Complete();return poses[index];}
        public void Reset(){handle.Complete();if(states.IsCreated)states[0]=default;if(attention.IsCreated)attention[0]=default;}
        public void Dispose()
        {
            handle.Complete();if(inputs.IsCreated)inputs.Dispose();if(states.IsCreated)states.Dispose();
            if(attention.IsCreated)attention.Dispose();if(poses.IsCreated)poses.Dispose();if(pivots.IsCreated)pivots.Dispose();if(validation.IsCreated)validation.Dispose();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current supported route/world planted feet -> positive-yaw local rigid poses, original pivot hierarchy and Y0 sole convention.
// Array Formats: Fixed one input/state/attention/result record, seventeen native base pivots/output poses, one completed bounded worker; cold persistent allocation only.
// Dependencies/Pointers: Reuses existing numeric attention style seed1 without changing canonical roles/counts. No Object APIs inside jobs; complete before renderer publication, reset or disposal. Native/Burst/performance acceptance remains root-owned.
