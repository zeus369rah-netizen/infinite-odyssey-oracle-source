using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        // Read-only authoring seam. Does not create/switch a World or touch global sky.
        public bool TryGetMenuShowcaseArenaRoot(out Transform root)
        {root=IsCoopArena?arenaRoot:null;return root!=null&&root.gameObject.activeInHierarchy;}
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing arena-local metre/Y-up root is borrowed by explicit Editor capture only.
// Array Formats: No arrays, ownership, geometry, gameplay state, Rooms, saves or global rendering settings change.
// Dependencies/Pointers: Explicit capture/catalog borrows existing World and saved asset references; no live actor, gameplay or save ownership is transferred.
