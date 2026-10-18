using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        public Vector3 TutorialMonsterPracticePosition => new Vector3(-10f,0f,-3f);
        public Vector3 TutorialRaykonPosition => new Vector3(-10f,0f,8f);
        public Vector3 TutorialTowerPracticePosition => TrainingLabTowerPosition;
        public Vector3 TutorialTowerPracticeTargetPosition => TrainingLabDummyPosition;
        public Vector3 TutorialStaffPosition => new Vector3(2.6f,0f,-1.2f);
        public bool TrainingStaffAvailable => trainingStaff!=null && trainingStaff.activeSelf;
        GameObject trainingStaff;

        void BuildRefugeActivities()
        {
            BuildTrainingLabPad();
            // Each left alcove has one activity. Its centre and doorway remain clear.
            // Opposite alcoves retain shelves/books, never a second overlapping room shell.
            foreach(float c in alcoves)
            {
                Banner(-tutorialRefuge.OuterX+.32f,c,true,3.65f);
                Banner(tutorialRefuge.OuterX-.32f,c,true,3.65f);
                CandleCluster(new Vector3(tutorialRefuge.HalfWidth+1.3f,0f,c-1.35f),3);
            }
            Crate(new Vector3(8.65f,0f,-1.55f),new Vector3(.82f,.72f,.7f),0f);
            EmptyRefugeTable(new Vector3(3.2f,0f,2f));
            Transform saved=geometryRoot;
            var staffParent=new GameObject("Interactable Oracle practice staff").transform;
            staffParent.SetParent(saved,false);staffParent.localPosition=TutorialStaffPosition;
            var staffBatch=new Batch(batches[Surface.Wood].Material);
            staffBatch.Box(new Vector3(0,.78f,0),new Vector3(.075f,1.56f,.075f),Quaternion.identity);
            FinishBatch("Practice staff",staffBatch,staffParent);
            var focus=new Batch(batches[Surface.PortalCrystal].Material);
            focus.Crystal(new Vector3(0,1.64f,0),.105f,.22f,Quaternion.identity);
            FinishBatch("Practice staff focus",focus,staffParent,false);
            var body=staffParent.gameObject.AddComponent<UnityEngine.BoxCollider>();
            body.center=new Vector3(0,.85f,0);body.size=new Vector3(.21f,1.7f,.21f);
            trainingStaff=staffParent.gameObject;
        }

        void EmptyRefugeTable(Vector3 feet)
        {
            Cube(Surface.Wood,feet+new Vector3(0,.9f,0),new Vector3(1.4f,.12f,.8f));
            BoxCollider("Empty shrine table top",feet+new Vector3(0,.9f,0),new Vector3(1.4f,.12f,.8f));
            foreach(float x in new[]{-.56f,.56f})foreach(float z in new[]{-.28f,.28f})
            {
                var center=feet+new Vector3(x,.42f,z);
                Cube(Surface.Wood,center,new Vector3(.12f,.84f,.12f));
                BoxCollider("Empty shrine table leg",center,new Vector3(.12f,.84f,.12f));
            }
        }
        public bool TakeTrainingStaff()
        {
            if(!IsTutorialRefuge || !TrainingStaffAvailable)return false;
            trainingStaff.SetActive(false);return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Y-up refuge metres, supported floor vertexY0; explicit nonoverlapping activity footprints.
// Array Formats: Fixed three zone anchors and one cold combined staff, shared original batch materials.
// Dependencies/Pointers: DungeonWorld owns all meshes/materials and the one pickup lifetime; gameplay authority stays with tutorial/combat.
