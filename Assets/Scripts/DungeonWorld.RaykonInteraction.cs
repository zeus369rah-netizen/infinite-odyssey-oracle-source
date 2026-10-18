using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        public bool TryGetRaykonInteractionTarget(out Transform root,out Vector3 feet,out Vector3 aim)
        {
            root=null;feet=aim=default;
            if(!IsTutorialRefuge||IsCoopArena||!RefugeRaykonReady||refugeRaykonInteractionRoot==null||
                !refugeRaykonInteractionRoot.gameObject.activeInHierarchy)return false;
            root=refugeRaykonInteractionRoot;feet=root.position;aim=feet+Vector3.up*1.2f;return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual floor-supported Raykon root in Unity world metres/Y-up; local+Z presentation faces yaw90/+X.
// Array Formats: One borrowed cached Transform, two value vectors; no search, buffers or geometry changes.
// Dependencies/Pointers: Existing World creates only after numeric support acceptance and clears this root before disposal; no gameplay authority here.
