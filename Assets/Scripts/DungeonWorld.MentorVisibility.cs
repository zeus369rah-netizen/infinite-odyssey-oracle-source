using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        void TickRefugeMentorVisibility()
        {
            if(!IsTutorialRefuge || refugeRaykonInteractionRoot==null)return;
            var session=GameSession.Instance;
            bool visible=session==null || session.Tutorial==null || session.Tutorial.RaykonIntroduced;
            var node=refugeRaykonInteractionRoot.gameObject;
            if(node.activeSelf!=visible)node.SetActive(visible);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual floor-supported Raykon logical root in the existing refuge alcove.
// Array Formats: One cached root and one observed tutorial flag; no per-frame allocation or search.
// Dependencies/Pointers: Tutorial owns introduction; World owns model/collider visibility and lifetime; no automatic talk/quest/teleport.
