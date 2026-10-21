using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class NpcBlacksmith
    {
        NpcAttentionLease009 fluidSmith009;
        uint fluidSmithTalk009;
        Transform fluidSmithRoot009;
        public void NotifyAcceptedTalk009(){fluidSmithTalk009++;}
        void InitializeFluidSmith009(){fluidSmith009=new NpcAttentionLease009(1);fluidSmithRoot009=transform;fluidSmithTalk009=0;}
        void DisposeFluidSmith009(){fluidSmith009?.Dispose();fluidSmith009=null;fluidSmithRoot009=null;}
        void TickFluidSmith009()
        {
            if(visual==null||fluidSmith009==null||player==null||player.ViewCamera==null||!Application.isFocused)return;
            var session=GameSession.Instance;
            bool ownDialogue=session!=null&&((session.Shop!=null&&session.Shop.Visible&&session.Shop.Blacksmith==this)||
                (session.FieldSmith!=null&&session.FieldSmith.IsTalkingToSmith009(this)));
            if(session!=null&&(!session.HasStarted||session.Victory||session.IsDead||session.Paused&&!ownDialogue))return;
            Vector3 eye=player.ViewCamera.transform.position,origin=head.position;Quaternion root=fluidSmithRoot009.rotation;
            fluidSmith009.Set(0,new NpcAttentionInput009{Enabled=1,Tracking=1,Role=1,TalkSerial=fluidSmithTalk009,
                EyeWorld=new float3(eye.x,eye.y,eye.z),HeadWorld=new float3(origin.x,origin.y,origin.z),
                RootRotation=new quaternion(root.x,root.y,root.z,root.w),LimitRadians=math.radians(new float2(55,18))});
            if(!fluidSmith009.Evaluate(Time.unscaledDeltaTime))return;var pose=fluidSmith009.Read(0);
            breathing.localPosition=new Vector3(0,1.01f+pose.Breath*.0025f,0);
            breathing.localScale=new Vector3(1,1+pose.Breath*.004f,1+pose.Breath*.002f);
            head.localRotation=new Quaternion(pose.Head.value.x,pose.Head.value.y,pose.Head.value.z,pose.Head.value.w);
            hammerArm.localRotation=new Quaternion(pose.Right.value.x,pose.Right.value.y,pose.Right.value.z,pose.Right.value.w);
            SetGlow(pose.Glow);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual Smith unit/yaw root and world eye/head snapshots, original supported boots/rigid hammer grip retained.
// Array Formats: Fixed1 persistent numeric attention state, cached original transforms/material; no new mesh/skin/actor or hot allocation.
// Dependencies/Pointers: Existing accepted shop/Forge open invokes acknowledgment; unrelated pause freezes motion; job completion precedes cached transform/material writes and retirement disposal.
