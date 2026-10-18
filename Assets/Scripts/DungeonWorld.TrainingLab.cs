using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        public Vector3 TrainingLabTowerPosition=>new Vector3(-(tutorialRefuge!=null?tutorialRefuge.HalfWidth:8f)-.9f,.08f,16.6f);
        public Vector3 TrainingLabDummyPosition=>new Vector3(TrainingLabTowerPosition.x,0f,18.9f);
        void BuildTrainingLabPad()
        {
            Vector3 top=TrainingLabTowerPosition;
            Cube(Surface.DarkStone,top-Vector3.up*.04f,new Vector3(1.20f,.08f,1.20f));
            BoxCollider("Offline training lab supported Water tower pad",top-Vector3.up*.04f,new Vector3(1.20f,.08f,1.20f));
            Cube(Surface.Azure,top+new Vector3(0,.006f,-.53f),new Vector3(.90f,.012f,.06f));
            Cube(Surface.Azure,top+new Vector3(-.53f,.006f,0),new Vector3(.06f,.012f,.90f));
            Cube(Surface.Azure,top+new Vector3(.53f,.006f,0),new Vector3(.06f,.012f,.90f));
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: West third-alcove mouth, World Y-up metres; pad top exactly y.08 and dummy sole y0.
// Array Formats: Three cold batched marker faces plus one supported slab collider; no floor-tile GameObjects/Update.
// Dependencies/Pointers: Existing World batches/collider owner; same x and z16.6->18.9 avoid pier rows13/22. Native clearance/LOS remains an explicit root acceptance gate.
