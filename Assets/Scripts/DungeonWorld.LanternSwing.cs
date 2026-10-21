using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        internal LanternSwingOwner LanternSwings;
        internal bool TryHitLantern(Vector3 origin, Vector3 direction, float distance, float radius, float strength, out float contact)
        {
            contact = distance;
            return LanternSwings != null && LanternSwings.TryHit(origin, direction, distance, radius, strength, out contact);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing refuge world metres; contact restricted strictly before existing projectile obstruction.
// Array Formats: one current room-owned reference; no independent cast/physics/protocol store.
// Dependencies/Pointers: room placement assigns newest owner; stale owner cannot reset newer reference. Offline-only owner checks retained.
