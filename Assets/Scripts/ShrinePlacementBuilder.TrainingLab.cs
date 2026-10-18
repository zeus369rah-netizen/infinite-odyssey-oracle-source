using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class ShrinePlacementBuilder
    {
        const int TrainingLabTowerId=4;
        internal bool TryCreateTrainingLabTower(TrainingLabDirector lab,out PlacedElementShrine tower)
        {
            tower=null;
            if(!ModelsReady||session==null||session.World==null||lab==null||!lab.OwnerContext||lab.Dummy==null||placed.Count!=0||
                PlacedElementShrine.TryGetRegistered(TrainingLabTowerId,out _))return false;
            var go=new GameObject("Offline training lab | tier-one Water tower");go.transform.SetParent(session.World.transform,false);
            go.transform.position=session.World.TrainingLabTowerPosition;
            try
            {
                tower=go.AddComponent<PlacedElementShrine>();
                tower.Initialize(session,TrainingLabTowerId,1,green,gold,elementMaterials[1],towerModelLibrary,1,0);
                tower.BindTrainingLab(lab);
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
// Coordinate Space: Exact supported west-alcove pad top; original saved Water tower scale/rotation ownership.
// Array Formats: One reserved registry slot4 within existing fixed4 tower cap; no purchased-building dictionary entry.
// Dependencies/Pointers: Cold explicit spawn uses existing saved model library/materials; TrainingLabDirector owns release, no build tokens/Forge/currency/save/network writes.
