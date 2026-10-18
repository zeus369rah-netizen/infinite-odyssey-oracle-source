using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class NpcBlacksmith
    {
        BoxCollider towerAnvilCollider;
        public bool TowerAnvilReady=>towerAnvilCollider!=null&&towerAnvilCollider.enabled&&towerAnvilCollider.gameObject.activeInHierarchy;
        public Vector3 TowerAnvilFeet=>transform.TransformPoint(new Vector3(1.10f,0f,.12f));
        public Vector3 TowerAnvilAimPoint=>transform.TransformPoint(new Vector3(1.10f,.75f,.12f));
        public bool OwnsTowerAnvilCollider(Collider value)=>TowerAnvilReady&&value==towerAnvilCollider;
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original Smith local +Z forward/Y-up, base pivot Y0; existing root transforms anchors to world metres.
// Array Formats: One borrowed original BoxCollider reference; original block/horn/rune/frame/hammer mesh recipe is unchanged.
// Dependencies/Pointers: NpcBlacksmith owns/retires the original collider; callers borrow it. No new renderers, physics actors, buffers or economy.
