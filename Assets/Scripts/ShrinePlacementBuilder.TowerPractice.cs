using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class ShrinePlacementBuilder
    {
        const int TutorialPracticeTowerId=1;
        PlacedElementShrine tutorialPracticeTower;
        internal bool TryCreateTutorialPracticeTower(OracleEnemy target,out PlacedElementShrine tower)
        {
            tower=null;
            if(!ModelsReady||session==null||session.World==null||session.Tutorial==null||
                !session.Tutorial.TowerPracticeOwnerContext||session.Tutorial.TowerPracticePhase!=TutorialTowerPracticePhase.Preparing||
                session.Combat==null||!session.Combat.IsTutorialTowerPracticeTarget(target)||!object.ReferenceEquals(tutorialPracticeTower,null)||placed.Count!=0||
                PlacedElementShrine.TryGetRegistered(TutorialPracticeTowerId,out _))return false;
            var go=new GameObject("Offline Water tower practice");go.transform.SetParent(session.World.transform,false);
            go.transform.position=session.World.TutorialTowerPracticePosition;
            tower=go.AddComponent<PlacedElementShrine>();tutorialPracticeTower=tower;
            try
            {
                tower.Initialize(session,TutorialPracticeTowerId,1,green,gold,elementMaterials[1],towerModelLibrary,1,0);
                tower.BindTutorialPractice(session.Tutorial,target);
                return true;
            }
            catch
            {
                ReleaseTutorialPracticeTower(tower);tower=null;throw;
            }
        }
        internal bool OwnsTutorialPracticeTower(PlacedElementShrine tower)=>
            !object.ReferenceEquals(tower,null)&&object.ReferenceEquals(tutorialPracticeTower,tower);
        internal void ReleaseTutorialPracticeTower(PlacedElementShrine tower)
        {
            if(!OwnsTutorialPracticeTower(tower))return;
            var owned=tutorialPracticeTower;tutorialPracticeTower=null;
            if(owned==null)return;
            owned.ClearTutorialPracticeBinding();
            owned.gameObject.SetActive(false);Destroy(owned.gameObject);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Reuses existing saved tower library/materials/registry at one exact World floor anchor; creation is cold tutorial setup only.
// Offline-only one actor, no Director placement request, tokens, Forge tier, wallet, persistent state or new native storage.
// # ARCHITECTURE SNAPSHOT
// Existing Y-up world/tower anchors, real native enemy HP and sole shot/cooldown owners remain unchanged.
// One separate tutorial tower field never enters player placed counts; reset/destruction release before model-library disposal.
// No rewards, quest completion, save state, currency or worker/native ownership changes.
