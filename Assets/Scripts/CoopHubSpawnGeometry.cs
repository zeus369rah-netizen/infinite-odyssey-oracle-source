using System;
using UnityEngine;

namespace OdysseyOracle
{
    // Cold bounded PhysX publication boundary, never called from jobs/active pose loops.
    public static class CoopHubSpawnGeometry
    {
        static readonly RaycastHit[] support=new RaycastHit[16];
        static readonly Collider[] clearance=new Collider[32];
        static bool busy;
        public static bool TryResolve(DungeonWorld world,int candidate,out Vector3 feet)
        {
            feet=default;
            if(busy||world==null||world.Mode020!=WorldMode020.TutorialRefuge||!CoopHubSpawnLayout.Candidate(candidate,out float x,out float z))return false;
            busy=true;
            try
            {
                int count=Physics.RaycastNonAlloc(new Vector3(x,2.5f,z),Vector3.down,support,4f,~0,QueryTriggerInteraction.Ignore);
                if(count==0||count==support.Length)return false;
                bool found=false;float height=float.NegativeInfinity;
                for(int i=0;i<count;i++)
                {
                    var hit=support[i];var collider=hit.collider;
                    if(collider==null||hit.normal.y<.95f||hit.point.y<-.15f||hit.point.y>.3f)continue;
                    Transform t=collider.transform;if(t!=world.transform&&!t.IsChildOf(world.transform))continue;
                    height=Mathf.Max(height,hit.point.y);found=true;
                }
                if(!found)return false;
                Vector3 proposed=new Vector3(x,height+.04f,z);
                // Radius/height conservatively enclose the actual .29m/1.85m controller.
                count=Physics.OverlapCapsuleNonAlloc(proposed+Vector3.up*.39f,proposed+Vector3.up*1.50f,.36f,clearance,~0,QueryTriggerInteraction.Ignore);
                if(count!=0)return false; // Saturation, walls and any real actor are all rejected.
                feet=proposed;return true;
            }
            finally{Array.Clear(support,0,support.Length);Array.Clear(clearance,0,clearance.Length);busy=false;}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual refuge world metres/Y-up, feet4cm above verified near-horizontal authored floor; capsule remains above floor contact.
// Array Formats: Fixed16-hit support and32-collider clearance buffers reused only by this cold main-thread adapter; saturated queries reject.
// Dependencies/Pointers: Borrows actual World colliders; ignores no wall/tower/player collider, never activates geometry or runs in a job. Buffers clear on every exit; owner rejects reentrant calls. Root native geometry checks remain required.
