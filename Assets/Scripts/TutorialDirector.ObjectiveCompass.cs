using OdysseyOracle.Foundation.Presentation;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class TutorialDirector
    {
        // Read-only lesson adapter. Directions never acknowledge, wake, spawn or complete anything.
        public bool TryGetCompassObjective(out CompassDestination destination,out Vector3 point)
        {
            destination=CompassDestination.None;point=default;
            if(!active||Finished||SuppressAutomaticTutorialPresentation||session==null||session.World==null)return false;
            var world=session.World;
            switch(CurrentLesson)
            {
                case Lesson.Move:case Lesson.Run:case Lesson.Jump:
                    if(world.RoomV2Layout==null)return false;
                    var practice=world.RoomV2Layout.Anchor(TutorialRoomV2Layout.AnchorId.GuideStart);
                    point=world.transform.TransformPoint(new Vector3(practice.X,practice.Y,practice.Z));destination=CompassDestination.MovementArea;return true;
                case Lesson.Shrine:
                    if(NeedsPracticeStaff015){point=world.TutorialStaffPosition;destination=CompassDestination.Shrine;return true;}
                    point=world.ShrinePosition;
                    if(world.RoomV2Layout!=null){var p=world.RoomV2Layout.Anchor(TutorialRoomV2Layout.AnchorId.ShrineApproach);point=world.transform.TransformPoint(new Vector3(p.X,p.Y,p.Z));}
                    destination=CompassDestination.Shrine;return true;
                case Lesson.Guide:
                    if(HasPendingInstruction&&guideIndex==2&&world.TryGetRaykonInteractionTarget(out _,out point,out _)){point+=new Vector3(2.2f,0,.55f);destination=CompassDestination.Raykon;return true;}return false;
                case Lesson.Guardian:
                    if(sharedPractice){point=world.PortalPosition;destination=CompassDestination.Portal;return true;}
                    var guardian=session.Combat!=null?session.Combat.TutorialGuardian:null;
                    if(guardian!=null&&guardian.Alive){point=guardian.transform.position;destination=CompassDestination.Guardian;return true;}return false;
                case Lesson.NovaPractice:
                    if(session.Combat!=null&&session.Combat.CosmicNovaTrialActive&&session.Combat.CosmicNovaTrialAliveCount>0){point=session.Combat.CosmicNovaTrialAimPoint;destination=CompassDestination.NovaTarget;return true;}return false;
                case Lesson.TowerPractice:
                    if(towerPracticePhase==TutorialTowerPracticePhase.Instruction){point=world.TutorialTowerPracticePosition;destination=CompassDestination.TowerPractice;return true;}
                    if(towerPracticePhase==TutorialTowerPracticePhase.Running){point=world.TutorialTowerPracticeTargetPosition;destination=CompassDestination.TowerDemonstration;return true;}return false;
                case Lesson.Shop:
                    var smith=session.Shop!=null?session.Shop.Blacksmith:null;
                    if(smith!=null&&smith.gameObject.activeInHierarchy){point=smith.transform.TransformPoint(new Vector3(0,0,1.8f));destination=CompassDestination.Smith;return true;}return false;
                default:return false;
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing supported World/metadata and actual actor roots in World metres; current lesson supplies the sole spatial purpose.
// Array Formats: two scalar out-values, static enum destinations; no native owner, cached delegate, scene search, active allocation or Physics.
// Dependencies/Pointers: existing Tutorial/Combat/World/Shop remain sole lesson/actor/progression authorities. Movement target is the real practice area, not a new arrival gate; recovery/casting cards without a spatial goal return false.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.
