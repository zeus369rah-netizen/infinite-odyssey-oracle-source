using UnityEngine;
using OdysseyOracle.Foundation.Collision015;

namespace OdysseyOracle
{
    public sealed partial class OracleEnemy
    {
        // Object-copy boundary only: numeric placement/normalization/grid work runs in the publisher job.
        internal MonsterRawPose80 CaptureMonsterPose015()
        {
            if(!Targetable||!PoolLeased015||PoolGeneration015==0)return default;
            Vector3 p=transform.position,s=transform.lossyScale,c=presentation.Hitbox.center+(body!=null?body.localPosition:Vector3.zero),e=presentation.Hitbox.extents;
            Quaternion q=transform.rotation;
            return new MonsterRawPose80{X=p.x,Y=p.y,Z=p.z,Flags=MonsterContactMath015.Active|(IsFlying?MonsterContactMath015.Flying:0),
                CenterX=c.x,CenterY=c.y,CenterZ=c.z,ExtentX=e.x,ExtentY=e.y,ExtentZ=e.z,
                StableId=TargetId,Generation=PoolGeneration015,RotationX=q.x,RotationY=q.y,RotationZ=q.z,RotationW=q.w,
                ScaleX=s.x,ScaleY=s.y,ScaleZ=s.z};
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current actor world pose plus original admission-cached local hitbox/body offset, matching existing trigger envelope.
// Array Formats: One80B raw value per existing pool slot, no native ownership or allocation in this adapter.
// Dependencies/Pointers: OracleCombat owns publisher/readers and current pool generation. This method grants no attack, changes no pose and never calls damage/Physics.
