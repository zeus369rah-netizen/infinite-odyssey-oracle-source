using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        // Read-only canonical target adapter. InvasionPortals[] is compressed and is never indexed by lane.
        public bool TryGetActiveInvasionPortalTarget(int canonicalLane,out Vector3 feet,out Transform gateRoot)
        {
            gateRoot=null;
            if(!TryGetActiveInvasionPortal(canonicalLane,out feet))return false;
            GameObject gate=invasionGateRoots[canonicalLane];
            if(gate==null||!gate.activeInHierarchy)return false;
            gateRoot=gate.transform;return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing GetInvasionPortal canonical world anchors; borrowed gate-root hierarchy, no new placement.
// Array Formats: Existing fixed8 gate roots and scalar admission mask; compressed legacy portal array is not a lane index.
// Dependencies/Pointers: Read-only World adapter; original gate/lane lifecycle owns geometry/colliders. No new Object, authority, Update or disposal owner.
