using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class ShrinePlacementBuilder
    {
        const int TrainingLabTowerId=4;
        internal bool TryCreateTrainingLabTower(TrainingLabDirector lab,out PlacedElementShrine tower)
        {
            tower=null;
            if(!ModelsReady||session==null||session.World==null||lab==null||(!lab.OwnerContext&&!lab.SharedReplicaApplying)||!TowerForgeDefinitions.ValidType(lab.SelectedTowerType)||placed.Count!=0||
                PlacedElementShrine.TryGetRegistered(TrainingLabTowerId,out _))return false;
            var go=new GameObject("Offline training lab | tier-one tower "+lab.SelectedTowerType);go.transform.SetParent(session.World.transform,false);
            go.transform.position=session.World.TrainingLabTowerPosition;
            try
            {
                tower=go.AddComponent<PlacedElementShrine>();
                tower.Initialize(session,TrainingLabTowerId,lab.SelectedTowerType,green,gold,elementMaterials[lab.SelectedTowerType],towerModelLibrary,1,0);
                tower.BindTrainingLab(lab);
                if(lab.SharedReplicaApplying)tower.SetRemote(true);
            }
            catch
            {
                if(go!=null){go.SetActive(false);Destroy(go);}tower=null;return false;
            }
            // Deliberately outside purchased/placed dictionary: this is not a build and must not unlock a build achievement.
            return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact supported west-alcove pad top; original saved four-core tower scale/rotation ownership.
// Array Formats: One reserved registry slot4 within existing fixed4 tower cap; no purchased-building dictionary entry.
// Dependencies/Pointers: Cold explicit spawn uses existing saved model library/materials; TrainingLabDirector owns release, no build tokens/Forge/currency/save/network writes.
// # ARCHITECTURE SNAPSHOT
// Existing Y-up/metre lab anchors and fixed actor pools/real HP owners remain.
// Pending four-type selectors apply only on explicit Replace/Add; zero/one actors retain the same Combat reward-exclusion owner.
// Remove/Replace reset both attacks and retire shots before actor mutation; full EndSession alone releases the lab session.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge world metres/Y-up; host-owned shared lab, model local+Z.
// Array Formats: Two native peer records and bounded fixed frames; inherited48 projectile pool and two real actors.
// Dependencies/Pointers: Dedicated shared owner validates requests/fences; guests remote-only; no rewards, migration or narrator; root integrates complete slice.
