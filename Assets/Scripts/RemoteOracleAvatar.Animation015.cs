using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class RemoteOracleAvatar
    {
        OraclePlayerPoseAnimation015 nativePose015;
        public bool NativePlayerPoseActive015=>nativePose015!=null&&nativePose015.Active;
        public ulong NativePlayerPoseEvaluations015=>nativePose015!=null?nativePose015.EvaluationCount:0;
        internal void PreparePlayerPoseCold015(){if(initialized&&nativePose015==null)nativePose015=OraclePlayerPoseAnimation015.PrepareCold(visualBody,false);}
        internal void BindPreparedPlayer015(ulong id,Vector3 position,float yaw)
        {
            ClientId=id;gameObject.name="Remote Oracle "+id;targetPosition=position;targetRotation=Quaternion.Euler(0,yaw,0);transform.SetPositionAndRotation(position,targetRotation);
            motion=phase=0f;visualBody.localRotation=Quaternion.identity;nativePose015?.Reset();appearanceModel.Apply(OracleAvatarAppearance.StartingLook((int)(id&7UL)));gameObject.SetActive(true);
        }
        internal void RetirePreparedPlayer015(){nativePose015?.Reset();motion=phase=0;visualBody.localRotation=Quaternion.identity;gameObject.SetActive(false);}
        internal void PulseAcceptedPlayer015(){nativePose015?.PulseAccepted();}
        bool EvaluatePlayerPose015(float dt,bool downed)=>nativePose015!=null&&nativePose015.Evaluate(dt,motion,phase/(Mathf.PI*2f),1f,!downed);
        void DisposePlayerPose015(){nativePose015?.Dispose();nativePose015=null;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted/interpolated feet root and separate downed body tilt; only cached child articulation is native.
// Array Formats: One prewarmed remote graph/value clocks, unchanged eight appearance variants and peer health snapshot.
// Dependencies/Pointers: Room admission rents/resets exact prepared visual; authoritative pose and accepted shot presentation remain canonical. No local body or input/HP/transport mutation.
