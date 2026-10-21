using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle.VisualDressing
{
    public sealed partial class RaykonPresentation
    {
        NpcAttentionLease009 headAttention009;
        Transform logicalRoot009;
        uint talkSerial009;
        static readonly int HeadAngles009=Shader.PropertyToID("_HeadAngles009");
        void InitializeAttention009(Transform root){logicalRoot009=root;headAttention009=new NpcAttentionLease009(1);}
        public void NotifyAcceptedTalk009(){talkSerial009++;}
        public void TickAttention009(Vector3 actualEye,float dt)
        {
            if(logicalRoot009==null||headAttention009==null||bodyRenderer==null)return;
            Vector3 rootPosition=logicalRoot009.position;Quaternion rootRotation=logicalRoot009.rotation;
            headAttention009.Set(0,new NpcAttentionInput009{Enabled=1,Tracking=1,Role=4,TalkSerial=talkSerial009,
                EyeWorld=new float3(actualEye.x,actualEye.y,actualEye.z),HeadWorld=new float3(rootPosition.x,rootPosition.y+1.60f,rootPosition.z),
                RootRotation=new quaternion(rootRotation.x,rootRotation.y,rootRotation.z,rootRotation.w),LimitRadians=math.radians(new float2(55,18))});
            if(!headAttention009.Evaluate(dt))return;var pose=headAttention009.Read(0);
            bodyBlock.SetVector(HeadAngles009,new Vector4(pose.Angles.y,pose.Angles.x,0,0));bodyRenderer.SetPropertyBlock(bodyBlock);
        }
        void DisposeAttention009(){headAttention009?.Dispose();headAttention009=null;logicalRoot009=null;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Cached exact Y-up unit/yaw logical Raykon root, world eye -> fixed local neck pivot(0,1.60,0), radians.
// Array Formats: Fixed1 persistent numeric attention record and existing body MPB; no bone, skin, CPU vertex loop or new audio owner.
// Dependencies/Pointers: Complete job then upload bounded pitch/yaw only; body shader rotates whole head/hood/jaw normals equally; existing feet/staff/slam/cauldron/support remains authoritative and unmodified.
