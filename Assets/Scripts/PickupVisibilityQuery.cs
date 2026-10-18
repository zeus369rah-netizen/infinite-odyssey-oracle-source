using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main-thread public-Physics adapter. Caller owns one reusable query instance;
    //32 ray hits/16 endpoint overlaps, no static scratch or per-query allocation.
    // World solids block. Player bodies are not loot walls; inspect ALL hits so
    // an ignored local/remote player can never hide masonry behind that player.
    // Full buffers fail closed; a nearer self hit cannot hide a wall behind it.
    public sealed class PickupVisibilityQuery
    {
        readonly RaycastHit[] hits=new RaycastHit[32];
        readonly Collider[] overlaps=new Collider[16];
        const float EndpointRadius=.025f;
        public bool IsClear(Vector3 from,Vector3 to,Transform collector)
        {
            if(!Finite(from)||!Finite(to)||BlockedEndpoint(from,collector)||BlockedEndpoint(to,collector))return false;
            Vector3 delta=to-from;float distance=delta.magnitude;if(distance<.0001f)return true;
            int count=Physics.RaycastNonAlloc(from,delta/distance,hits,distance,~(1<<2),QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(Blocks(hits[i].collider,collector))return false;
            return true;
        }
        bool BlockedEndpoint(Vector3 point,Transform collector)
        {
            int count=Physics.OverlapSphereNonAlloc(point,EndpointRadius,overlaps,~(1<<2),QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return true;
            for(int i=0;i<count;i++)if(Blocks(overlaps[i],collector))return true;
            return false;
        }
        static bool Blocks(Collider c,Transform collector)
        {
            if(c==null||collector!=null&&(c.transform==collector||c.transform.IsChildOf(collector)))return false;
            return c.GetComponentInParent<OraclePlayer>()==null&&c.GetComponentInParent<RemoteOracleAvatar>()==null;
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
    }
}
