using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RefugeNpcRuntimeOwner
    {
        NpcAttentionLease009 fluidAttention009;
        void InitializeFluidNpc009(){fluidAttention009=new NpcAttentionLease009(2);InitializeGuardTap009();}
        void DisposeFluidNpc009(){DisposeGuardTap009();fluidAttention009?.Dispose();fluidAttention009=null;}
        void BeginFluidConversation009(int role)
        {
            Actor a=role==2?actors[0]:role==3?actors[1]:null;
            if(a!=null)a.FluidTalkSerial++;BeginGuardTap009(role);
            session.RefugeActivities?.TryPlayNpcGreeting009(role,this,1);
        }
        // Ending the read panel ends only its gesture. The accepted voice has its own world/owner lifetime.
        void StopFluidConversation009(){CancelGuardTaps009();}
        void TickFluidNpc009()
        {
            if(!initialized||fluidAttention009==null||visualRoot==null||!visualRoot.activeInHierarchy||!Application.isFocused||session==null)return;
            bool ownsModal=session.Hud!=null&&session.Hud.IsRefugeNpcConversationVisible(this);
            if(session.Paused&&!ownsModal)return;
            var camera=session.Player!=null?session.Player.ViewCamera:null;
            if(camera==null)return;
            Vector3 eye=camera.transform.position;
            CaptureFluidNpc009(0,eye);CaptureFluidNpc009(1,eye);
            if(!fluidAttention009.Evaluate(Time.unscaledDeltaTime))return;
            ApplyFluidNpc009(actors[0],fluidAttention009.Read(0));
            ApplyFluidNpc009(actors[1],fluidAttention009.Read(1));
            TickGuardTaps009();
        }
        void CaptureFluidNpc009(int index,Vector3 eye)
        {
            Actor a=actors[index];
            if(a==null||a.Root==null){fluidAttention009.Set(index,default);return;}
            Quaternion rotation=a.RootBasis.rotation;Vector3 headWorld=a.Head.position;
            bool speaking=session.RefugeActivities!=null&&session.RefugeActivities.IsNpcGreetingSpeaking009(a.Role,this);
            fluidAttention009.Set(index,new NpcAttentionInput009{
                Enabled=1,Tracking=1,Role=(uint)a.Role,TalkSerial=a.FluidTalkSerial,Speaking=speaking?1u:0u,
                EyeWorld=new float3(eye.x,eye.y,eye.z),HeadWorld=new float3(headWorld.x,headWorld.y,headWorld.z),
                RootRotation=new quaternion(rotation.x,rotation.y,rotation.z,rotation.w),
                LimitRadians=math.radians(new float2(a.Role==3?65:55,18))
            });
        }
        static Quaternion FluidRotation009(quaternion q)=>new Quaternion(q.value.x,q.value.y,q.value.z,q.value.w);
        static void ApplyFluidNpc009(Actor a,NpcAttentionOutput009 pose)
        {
            if(a==null||a.Root==null)return;
            // Fixed cached renderer writes after completed numeric job, no query or math loop.
            a.Torso.localPosition=a.TorsoRest+new Vector3(0,pose.Breath*.0025f,0);
            a.Head.localRotation=a.HeadRest*FluidRotation009(pose.Head);
            a.Left.localRotation=a.LeftRest*FluidRotation009(pose.Left);
            a.Right.localRotation=a.RightRest*FluidRotation009(pose.Right);
            a.Lids.localScale=new Vector3(a.LidsRest.x,a.LidsRest.y+(1-a.LidsRest.y)*pose.Blink,a.LidsRest.z);
            if(a.FluidMouth!=null)a.FluidMouth.localScale=new Vector3(a.FluidMouthRest.x,a.FluidMouthRest.y*(1+pose.Jaw*2),a.FluidMouthRest.z);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Cached supported Ilyra/Bram roots/world head+eye -> job-local gaze; original local rest transforms retained.
// Array Formats: Fixed2 persistent numeric records plus existing cached Actor references, no main-thread spatial/animation loops.
// Dependencies/Pointers: Real accepted lore-open event alone triggers nod/voice; renderer writes follow job completion; roots/feet/tool children/quest bridge remain unchanged; speaking is approximate source-gated cadence, not phoneme/RMS analysis.
