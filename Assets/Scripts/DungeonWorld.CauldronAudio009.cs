using OdysseyOracle.VisualDressing;
using UnityEngine;

namespace OdysseyOracle
{
    public struct CauldronAudioFrame009
    {
        public Vector3 LiquidPosition, HearthPosition;
        public float TalkBlend, HearthFuel;
    }

    public sealed partial class DungeonWorld
    {
        [SerializeField] int cauldronHearthAudioNodePlusOne009;
        public bool PortalReadyForAudio009 => worldMode020==WorldMode020.TutorialRefuge && portalUnlocked;
        public bool TryGetCauldronAudio009(out CauldronAudioFrame009 frame)
        {
            frame=default;
            if(!IsTutorialRefuge||IsCoopArena||refugeRaykon==null||refugeRaykonInteractionRoot==null||
                !refugeRaykonInteractionRoot.gameObject.activeInHierarchy)return false;
            Vector3 local=RaykonMeshes.CauldronColliderBounds.center;
            frame.LiquidPosition=refugeRaykonInteractionRoot.TransformPoint(local+Vector3.up*.30f);
            frame.HearthPosition=refugeRaykonInteractionRoot.TransformPoint(local-Vector3.up*.17f);
            frame.TalkBlend=refugeRaykon.TalkBlend;
            int node=cauldronHearthAudioNodePlusOne009-1;
            if(node>=0&&node<roomReactionCount&&roomReactionNodes[node].Kind==RoomReactionKind.Hearth)
            {
                frame.HearthPosition=roomReactionNodes[node].Center;
                frame.HearthFuel=RoomReactionGain(node);
            }
            return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual supported Raykon local cauldron center transformed to Y-up world metres; fixed center offsets locate sound only, never change mesh/floor/collider.
// Array Formats:32B scalar frame, one serialized cached existing Hearth node ordinal; O(1) reads, no hot spatial loops or native allocation.
// Dependencies/Pointers: World owns Raykon/room reaction state. Cold RegisterRoomReactionLiving caches the existing node; getter reads canonical TalkBlend/fenced gain/portal flag without accepting hits or changing readiness.

