using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        [SerializeField] int smithFurnaceNodePlusOne015;
        public float SmithFurnaceFuel015
        {
            get{int node=smithFurnaceNodePlusOne015-1;return IsTutorialRefuge&&!IsCoopArena&&node>=0&&node<roomReactionCount&&roomReactionNodes[node].StableId==710015&&roomReactionNodes[node].Kind==RoomReactionKind.LivingFlame?RoomReactionGain(node):0f;}
        }
        internal bool BindSmithFurnace015(LivingCampfirePresentation flame,Renderer body,Material[] borrowed,Vector3 center,float radius)
        {
            if(!IsTutorialRefuge||IsCoopArena||tutorialRefuge==null||flame==null||body==null)return false;
            int node=smithFurnaceNodePlusOne015-1;
            if(node>=0&&node<roomReactionCount){
                var previous=roomReactionNodes[node];
                if(previous.Kind!=RoomReactionKind.LivingFlame||previous.StableId!=710015||((Vector3)previous.Center-center).sqrMagnitude>1e-8f||Mathf.Abs(previous.Radius-radius)>.0001f)return false;
            }else{
                // Registration must precede the first sealed state. A late
                // optional visual fails dark instead of invalidating all nodes.
                if(roomReactionState!=null)return false;
                node=RegisterRoomNode(center,radius,RoomReactionKind.LivingFlame,710015);
                if(node<0)return false;smithFurnaceNodePlusOne015=node+1;
            }
            roomReactionLiving[node]=flame;roomReactionGlow[node]=RoomReactionGlowBinding.Create(body,null,borrowed);
            float fuel=RoomReactionGain(node);flame.SetRoomReactionFuel(fuel);roomReactionGlow[node].Apply(fuel);roomReactionApplied[node]=float.NaN;
            return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual supported furnace flame centre in original world metres; existing+Y-up host room node, stable710015.
// Array Formats: One cached existing512-slot LivingFlame node plus existing flame/glow binding arrays, no extra authority or native buffer.
// Dependencies/Pointers: Existing accepted Fire/Wind, snapshot generation and completed-reader fuel; original visual/material owners remain Smith. New node only before seal; matching cold visual rebind allowed, late/changed binding fails dark.
