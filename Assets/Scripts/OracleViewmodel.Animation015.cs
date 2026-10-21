using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class OracleViewmodel
    {
        Transform nativeHandPivot015;OraclePlayerPoseAnimation015 nativeHands015;
        public bool NativeHandPoseActive015=>nativeHands015!=null&&nativeHands015.Active;
        public ulong NativeHandPoseEvaluations015=>nativeHands015!=null?nativeHands015.EvaluationCount:0;
        void CreateHandPivot015(){nativeHandPivot015=new GameObject("Native hand animation015").transform;nativeHandPivot015.SetParent(cameraRig,false);}
        void PrepareHandsCold015(){nativeHands015=OraclePlayerPoseAnimation015.PrepareCold(cameraRig,true);}
        bool EvaluateHands015(float dt,float strength)
        {
            if(nativeHands015==null||nativeHandPivot015==null)return false;
            Vector3 priorPosition=nativeHandPivot015.localPosition;Quaternion priorRotation=nativeHandPivot015.localRotation;
            bool active=nativeHands015.Evaluate(dt,movement,player.GroundedForBobbing&&movement>.001f?player.StepPhase/(Mathf.PI*4f):clock*1.6f/(Mathf.PI*2f),player.GroundedForBobbing?strength:0f,true);
            if(active&&float.IsFinite(dt)&&dt>0f)
            {
                float blend=1f-Mathf.Exp(-Mathf.Min(dt,.05f)*22f);
                nativeHandPivot015.localPosition=Vector3.Lerp(priorPosition,nativeHandPivot015.localPosition,blend);
                nativeHandPivot015.localRotation=Quaternion.Slerp(priorRotation,nativeHandPivot015.localRotation,blend);
            }
            return active;
        }
        void PulseNativeHands015(){nativeHands015?.PulseAccepted();}
        void ResetNativeHands015(){nativeHands015?.Reset();}
        Matrix4x4 HandLocalMatrix015()=>Matrix4x4.TRS(nativeHandPivot015!=null?nativeHandPivot015.localPosition:Vector3.zero,nativeHandPivot015!=null?nativeHandPivot015.localRotation:Quaternion.identity,Vector3.one)*Matrix4x4.TRS(wandRoot.localPosition,wandRoot.localRotation,Vector3.one);
        void DisposeHands015(){nativeHands015?.Dispose();nativeHands015=null;nativeHandPivot015=null;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: One identity camera-local child only; existing cameraRig clearance scale and wand rest/sway/spring remain external.
// Array Formats: One cold native graph/fixed scalar presentation input; original geometry/material/spiral arrays retained.
// Dependencies/Pointers: Accepted own PulseCast only; original pause/visibility/preferences retained. Composed child/wand matrix feeds all existing wall/near-plane probes; no camera/input writes.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing shared rigid hand/staff camera-local pivot; shortest-arc rotation preserves positive scale, grip attachment and the authored60degree hand basis.
// Array Formats: two stack value snapshots and bounded1-exp(-22dt) Lerp/Slerp only; no new graph, bone, object, field array or frame allocation.
// Dependencies/Pointers: existing native graph evaluates/owns its target. Only its displayed rigid pivot is eased across accepted-age restarts; existing exact damped wand spring, pickup, pause/reset and clearance ownership remain unchanged.
