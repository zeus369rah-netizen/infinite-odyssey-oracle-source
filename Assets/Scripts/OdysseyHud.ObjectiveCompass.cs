using System.Globalization;
using OdysseyOracle.Foundation.Presentation;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        RectTransform objectiveCompassRoot,objectiveCompassArrow;
        CanvasGroup objectiveCompassAlpha;
        UnityEngine.UI.Text objectiveCompassFacing,objectiveCompassHeading,objectiveCompassDestination,objectiveCompassMetres,objectiveCompassMode,objectiveCompassContext;
        GameObject objectiveCompassLegacyLocation;bool objectiveCompassLocationLeased,objectiveCompassPriorLocationActive;int objectiveCompassShownContext=-1;
        readonly RectTransform[] objectiveCompassCardinalViews=new RectTransform[4];
        NativeArray<float4> objectiveCompassRooms;NativeArray<ObjectiveCompass32> objectiveCompassResult;JobHandle objectiveCompassFence;
        NativeArray<float2> objectiveCompassCardinalOffsets;
        Rect[] objectiveCompassObservedRooms;DungeonWorld objectiveCompassWorld;int objectiveCompassRoomCount;
        float objectiveCompassNextSample,objectiveCompassAngle,objectiveCompassOpacity,objectiveCompassDisplayHeading;bool objectiveCompassSeeded,objectiveCompassHasContent;
        int objectiveCompassShownHeading=-1,objectiveCompassShownCardinal=-1,objectiveCompassShownDistance=-1,objectiveCompassShownDestination=-1,objectiveCompassShownMode=-1;
        bool objectiveCompassShownStaff015;
        static readonly string[] CompassCardinals={"NORTH","EAST","SOUTH","WEST"};
        static readonly string[] CompassLetters={"N","E","S","W"};
        static readonly string[] CompassDegrees=BuildCompassNumbers(360,true),CompassDistances=BuildCompassNumbers(1000,false);
        static string[] BuildCompassNumbers(int count,bool degrees)
        {
            var values=new string[count];for(int i=0;i<count;i++)values[i]=degrees?i.ToString("000",CultureInfo.InvariantCulture)+"°":i.ToString(CultureInfo.InvariantCulture)+" m";return values;
        }
        void BuildObjectiveCompass()
        {
            var legacyLocation=hudRoot.Find("Location");objectiveCompassLegacyLocation=legacyLocation!=null?legacyLocation.gameObject:null;
            objectiveCompassRooms=new NativeArray<float4>(32,Allocator.Persistent);
            objectiveCompassResult=new NativeArray<ObjectiveCompass32>(1,Allocator.Persistent);
            objectiveCompassCardinalOffsets=new NativeArray<float2>(4,Allocator.Persistent);
            objectiveCompassRoot=Frame(canvas.transform,"ObjectiveCompass",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-34),new Vector2(520,98),new Color(.025f,.04f,.035f,.94f),Gold);
            objectiveCompassAlpha=objectiveCompassRoot.gameObject.AddComponent<CanvasGroup>();objectiveCompassAlpha.alpha=0;objectiveCompassAlpha.blocksRaycasts=false;objectiveCompassAlpha.interactable=false;
            var ring=Rect(objectiveCompassRoot,"CompassCardinalRing",new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(49,0),new Vector2(90,90));
            Border(ring,MutedGold,1);
            objectiveCompassArrow=Rect(ring,"SingleObjectiveArrow",new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(38,42));
            objectiveCompassArrow.gameObject.AddComponent<CanvasRenderer>();
            var graphic=objectiveCompassArrow.gameObject.AddComponent<ObjectiveCompassArrowGraphic>();graphic.raycastTarget=false;
            for(int i=0;i<4;i++)objectiveCompassCardinalViews[i]=Label(ring,"CompassCardinal"+CompassLetters[i],CompassLetters[i],12,Ivory,TextAnchor.MiddleCenter,Vector2.zero,new Vector2(18,16),new Vector2(.5f,.5f)).rectTransform;
            objectiveCompassFacing=Label(objectiveCompassRoot,"CompassFacingCardinal","NORTH",16,Ivory,TextAnchor.MiddleLeft,new Vector2(103,-10),new Vector2(140,22),new Vector2(0,1));
            objectiveCompassContext=Label(objectiveCompassRoot,"CompassLocationContext","",10,Muted,TextAnchor.MiddleLeft,new Vector2(255,-10),new Vector2(140,22),new Vector2(0,1));
            objectiveCompassHeading=Label(objectiveCompassRoot,"CompassActualHeading","000°",16,Ivory,TextAnchor.MiddleRight,new Vector2(-17,-10),new Vector2(100,22),new Vector2(1,1));
            objectiveCompassDestination=Label(objectiveCompassRoot,"CompassDestination","",16,Gold,TextAnchor.MiddleLeft,new Vector2(103,-39),new Vector2(300,24),new Vector2(0,1));
            objectiveCompassMetres=Label(objectiveCompassRoot,"CompassMetres","",15,Ivory,TextAnchor.MiddleRight,new Vector2(-17,-39),new Vector2(85,24),new Vector2(1,1));
            objectiveCompassMode=Label(objectiveCompassRoot,"CompassWaypointMode","",10,Muted,TextAnchor.MiddleLeft,new Vector2(103,-68),new Vector2(400,18),new Vector2(0,1));
            // Cardinal word and precise numeric heading have independent labels; no hot concatenation.
            objectiveCompassHeading.text="000°";
            objectiveCompassRoot.gameObject.SetActive(false);
        }
        void DisposeObjectiveCompass()
        {
            SetCompassLocationLease(false);objectiveCompassFence.Complete();
            if(objectiveCompassCardinalOffsets.IsCreated)objectiveCompassCardinalOffsets.Dispose();if(objectiveCompassResult.IsCreated)objectiveCompassResult.Dispose();if(objectiveCompassRooms.IsCreated)objectiveCompassRooms.Dispose();
        }
        string CompassDestinationName(int id)
        {
            switch((CompassDestination)id)
            {
                case CompassDestination.MovementArea:return "MOVEMENT PRACTICE AREA";case CompassDestination.Shrine:return session.Tutorial!=null&&session.Tutorial.NeedsPracticeStaff015?"TAKE PRACTICE STAFF":"GREEN SHRINE";
                case CompassDestination.Raykon:return "RAYKON · SPELL MENTOR";case CompassDestination.Smith:return "SMITH · TOWN SHOP";
                case CompassDestination.Anvil:return "TOWER ANVIL";case CompassDestination.TowerPractice:return "TOWER PRACTICE";
                case CompassDestination.TowerDemonstration:return "WATCH THE PRACTICE TOWER";case CompassDestination.MonsterPractice:return "MONSTER PRACTICE";
                case CompassDestination.Portal:return "VIOLET PORTAL";case CompassDestination.Guardian:return "PRACTICE GUARDIAN";
                case CompassDestination.NovaTarget:return "NOVA PRACTICE TARGET";case CompassDestination.ReturnPortal:return "RETURN PORTAL";
                case CompassDestination.DefendShrine:return "DEFEND THE SHRINE";default:return "FOLLOW THE TOP GUIDE";
            }
        }
        bool TryGetCurrentCompassTarget(out CompassDestination destination,out Vector3 point)
        {
            destination=CompassDestination.None;point=default;var world=session.World;
            var campaign=session.FieldSmith!=null?session.FieldSmith.BoundCampaign:null;
            if(world.IsCoopArena)
            {
                if(campaign!=null&&campaign.Phase==CampaignPhase.StageClear){destination=CompassDestination.ReturnPortal;point=CoopCampaignDirector.ReturnPortalPosition;return true;}
                if(session.Defense!=null&&session.Defense.Running){destination=CompassDestination.DefendShrine;point=world.ShrinePosition;return true;}
                return false;
            }
            if(!world.IsTutorialRefuge)return false;
            if(session.Tutorial!=null&&session.Tutorial.TryGetCompassObjective(out destination,out point))return true;
            if(session.Tutorial!=null&&!session.Tutorial.Finished&&!session.Tutorial.SuppressAutomaticTutorialPresentation)return false;
            if(campaign!=null&&campaign.InRefuge)
            {
                var smith=session.Shop!=null?session.Shop.Blacksmith:null;
                if((campaign.LocalVisitMask&CampaignQuestState.SmithRole)==0&&smith!=null&&smith.gameObject.activeInHierarchy)
                {
                    if(smith.TowerAnvilReady){destination=CompassDestination.Anvil;point=smith.TowerAnvilFeet+new Vector3(-.6f,0,2.9f);}
                    else{destination=CompassDestination.Smith;point=smith.transform.position+Vector3.forward*1.8f;}return true;
                }
                if((campaign.LocalVisitMask&CampaignQuestState.RunekeeperRole)==0&&world.TryGetRaykonInteractionTarget(out _,out point,out _))
                {destination=CompassDestination.Raykon;point+=new Vector3(2.2f,0,.55f);return true;}
            }
            var lab=session.TrainingLab;
            if(lab!=null&&lab.CanAct&&lab.HasActors)
            {
                var dummy=lab.Dummy;if(dummy!=null&&dummy.Alive){destination=CompassDestination.MonsterPractice;point=dummy.transform.position;return true;}
                var tower=lab.Tower;if(tower!=null&&!tower.IsDestroyed){destination=CompassDestination.TowerPractice;point=world.TrainingLabTowerPosition;return true;}
            }
            destination=CompassDestination.Portal;point=world.PortalPosition;
            if(world.RoomV2Layout!=null){var p=world.RoomV2Layout.Anchor(TutorialRoomV2Layout.AnchorId.PortalApproach);point=world.transform.TransformPoint(new Vector3(p.X,p.Y,p.Z));}
            return true;
        }
        void RefreshObjectiveCompass()
        {
            if(objectiveCompassRoot==null||session==null)return;
            bool visible=session.HasStarted&&!session.Paused&&!session.IsDead&&!session.IsDowned&&!session.Victory&&session.World!=null&&session.Player!=null&&session.Player.ViewCamera!=null&&
                currentPanel==JourneyPanel.None&&!ShopVisible&&!IntroActive&&!LoadingActive&&(session.Opening==null||!session.Opening.IsTransitioning);
            float dt=Mathf.Clamp(Time.unscaledDeltaTime,0,.05f);
            objectiveCompassDisplayHeading=ObjectiveCompassMath.Step(objectiveCompassDisplayHeading,objectiveCompassResult[0].CameraHeading,dt*540f);
            if(visible&&Time.unscaledTime>=objectiveCompassNextSample)
            {
                objectiveCompassNextSample=Time.unscaledTime+.05f;
                RefreshCompassRoomSnapshot();
                bool target=TryGetCurrentCompassTarget(out CompassDestination destination,out Vector3 point);
                bool guideOnly=!target&&session.Tutorial!=null&&!session.Tutorial.Finished&&!session.Tutorial.SuppressAutomaticTutorialPresentation;
                objectiveCompassHasContent=target||guideOnly;
                Vector3 position=session.Player.transform.position,forward=session.Player.ViewCamera.transform.forward;
                objectiveCompassFence=new ObjectiveCompassJob{Rooms=objectiveCompassRooms,RoomCount=objectiveCompassRoomCount,Destination=(int)destination,
                    Player=new float2(position.x,position.z),Target=new float2(point.x,point.z),Forward=new float2(forward.x,forward.z),
                    PreviousHeading=objectiveCompassResult[0].CameraHeading,DisplayedHeading=objectiveCompassDisplayHeading,Result=objectiveCompassResult,CardinalOffsets=objectiveCompassCardinalOffsets}.Schedule();objectiveCompassFence.Complete();
                PresentCompassReadout(objectiveCompassResult[0]);
                for(int i=0;i<4;i++){float2 offset=objectiveCompassCardinalOffsets[i];objectiveCompassCardinalViews[i].anchoredPosition=new Vector2(offset.x,offset.y);}
            }
            visible&=objectiveCompassHasContent;
            if(visible&&!objectiveCompassRoot.gameObject.activeSelf)objectiveCompassRoot.gameObject.SetActive(true);
            objectiveCompassOpacity=Mathf.MoveTowards(objectiveCompassOpacity,visible?1f:0f,dt*8f);objectiveCompassAlpha.alpha=objectiveCompassOpacity;
            if(!visible&&objectiveCompassOpacity<=0&&objectiveCompassRoot.gameObject.activeSelf)objectiveCompassRoot.gameObject.SetActive(false);
            SetCompassLocationLease(objectiveCompassRoot.gameObject.activeSelf&&objectiveCompassOpacity>0);
            var readout=objectiveCompassResult[0];
            if((readout.Flags&ObjectiveCompassMath.HasBearing)!=0)
            {
                float target=ObjectiveCompassMath.Normalize(-readout.RelativeHeading);
                if(!objectiveCompassSeeded){objectiveCompassAngle=target;objectiveCompassSeeded=true;}
                else objectiveCompassAngle=ObjectiveCompassMath.Step(objectiveCompassAngle,target,dt*540f);
                objectiveCompassArrow.localRotation=Quaternion.Euler(0,0,objectiveCompassAngle);
            }
        }
        void SetCompassLocationLease(bool occupied)
        {
            if(objectiveCompassLegacyLocation==null)return;
            if(occupied&&!objectiveCompassLocationLeased)
            {objectiveCompassPriorLocationActive=objectiveCompassLegacyLocation.activeSelf;objectiveCompassLocationLeased=true;objectiveCompassLegacyLocation.SetActive(false);}
            else if(!occupied&&objectiveCompassLocationLeased)
            {objectiveCompassLegacyLocation.SetActive(objectiveCompassPriorLocationActive);objectiveCompassLocationLeased=false;}
        }
        void RefreshCompassRoomSnapshot()
        {
            var world=session.World;var rooms=world.MapRooms;
            if(objectiveCompassWorld==world&&ReferenceEquals(objectiveCompassObservedRooms,rooms))return;
            objectiveCompassFence.Complete();objectiveCompassWorld=world;objectiveCompassObservedRooms=rooms;objectiveCompassRoomCount=0;
            world.EnsureRefugeNavigation(null); // Compatibility retirement only; no floor mesh publication.
            if(!world.IsTutorialRefuge||rooms==null||rooms.Length<1||rooms.Length>32)return;
            for(int i=0;i<rooms.Length;i++){var r=rooms[i];objectiveCompassRooms[i]=new float4(r.xMin,r.yMin,r.xMax,r.yMax);}
            objectiveCompassRoomCount=rooms.Length;
        }
        void PresentCompassReadout(ObjectiveCompass32 value)
        {
            int context=session.World.IsTutorialRefuge?1:0;
            if(context!=objectiveCompassShownContext){objectiveCompassShownContext=context;objectiveCompassContext.text=context==1?"TUTORIAL ROOM":"LOWER CATACOMBS";}
            int heading=Mathf.Clamp(Mathf.FloorToInt(value.CameraHeading),0,359),cardinal=((heading+45)/90)&3;
            if(heading!=objectiveCompassShownHeading){objectiveCompassShownHeading=heading;objectiveCompassHeading.text=CompassDegrees[heading];}
            if(cardinal!=objectiveCompassShownCardinal)
            {objectiveCompassShownCardinal=cardinal;objectiveCompassFacing.text=CompassCardinals[cardinal];}
            bool bearing=(value.Flags&ObjectiveCompassMath.HasBearing)!=0;
            if(objectiveCompassArrow.gameObject.activeSelf!=bearing)objectiveCompassArrow.gameObject.SetActive(bearing);
            int destination=value.Destination;
            bool needsStaff=destination==(int)CompassDestination.Shrine&&session.Tutorial!=null&&session.Tutorial.NeedsPracticeStaff015;
            if(destination!=objectiveCompassShownDestination||needsStaff!=objectiveCompassShownStaff015){objectiveCompassShownDestination=destination;objectiveCompassShownStaff015=needsStaff;objectiveCompassDestination.text=CompassDestinationName(destination);}
            int distance=(value.Flags&ObjectiveCompassMath.Valid)==0?-1:Mathf.Clamp(Mathf.CeilToInt(value.Metres),0,1000);
            if(distance!=objectiveCompassShownDistance){objectiveCompassShownDistance=distance;objectiveCompassMetres.text=distance<0?"—":distance>=1000?"999+ m":CompassDistances[distance];}
            int mode=destination==0?5:(value.Flags&(ObjectiveCompassMath.ViaEntry|ObjectiveCompassMath.ViaExit))!=0?((value.Flags&ObjectiveCompassMath.ViaExit)!=0?2:1):destination==(int)CompassDestination.MovementArea?3:!bearing?4:0;
            if(mode!=objectiveCompassShownMode){objectiveCompassShownMode=mode;objectiveCompassMode.text=mode==2?"VIA CURRENT ALCOVE EXIT":mode==1?"VIA ALCOVE ENTRY":mode==3?"PRACTICE AREA · USE THE TOP INSTRUCTION":mode==4?"AT OBJECTIVE / READ TOP GUIDE":mode==5?"NO SPATIAL TARGET · FOLLOW CURRENT LESSON":"OBJECTIVE BEARING · WALK AROUND OBSTACLES";}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing World/actor/approach metres and real player-cameraXZ -> one top Canvas guide, above existing short lesson and clear of portal corner.
// Array Formats: persistentfloat4[32]+result32[1]+fourcardinalfloat2 offsets, fenced job at20Hz; one cold14vertex arrow, all4cardinal labels, prebuilt360degree/1000distance labels and cached text buckets.
// Dependencies/Pointers: HUD owns/disposes only its numeric/UI stores; Tutorial/campaign/lab/World retain targets and all acceptance. No hot strings/instantiation/Physics or player/camera mutation; pathway guidance isn't full route/LOS proof, whole-game GC/native/browser acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.
