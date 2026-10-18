#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit root-run005 disposable fixture. Public gameplay commands only;
    // no fake peer/DTO/currency/HP/drop/tier and no private/reflected engine calls.
    // Teleport is restricted to real pickup/Smith clear approaches, not walk proof.
    // Existing host owns all placement, damage, claims, bank debit and saved art.
    public static class OracleForgeAcceptanceDriver
    {
        const double WallLimit=300,CleanupAllowance=15;
        const string TownKey="OdysseyOracle.TownProgression.v1",BackupKey="OdysseyOracle.TownProgression.v1.backup";
        enum Stage { Hub,Room,Ready,Arena,Place,SmithZero,SmithZeroCapture,Organic,DuplicateDrop,SmithUpgrade,Upgrade,Tier2Capture,Repeat,Leaving }
        [Serializable] public sealed class Gate {public string name,status="UNVISITED",detail="";public bool required;}
        [Serializable] public sealed class Observation
        {
            public string stage,detail;public double wall,remaining;public int frame,bank,tokens,placed,kills,acceptedCasts,organicClaims;
            public Vector3 feet;public bool focused,paused;
        }
        [Serializable] public sealed class ScreenProof
        {
            public string stage,status="NOT_REQUESTED",path,sha256,detail;
            public int frame,width,height,acceptedTier,bank;public double advancingFrameSeconds;
            public bool actualPanelVisible,applicationFocused;public Vector3 cameraPosition;public Quaternion cameraRotation;
            public float fieldOfView;public Rect cameraPixelRect;
        }
        [Serializable] public sealed class Receipt
        {
            public string status="NOT_STARTED",reason="",path,startedUtc,endedUtc;
            public bool nativeExecuted,networkRequested,ownedRoomCreated,ownedRoomLeft,callbacksDetached;
            public bool osInputRequested,virtualMouseRequested,profileRollbackRequested,profileChanged,publicSkipSetupExcludedFromTutorial;
            public string profileBeforeHash,profileAfterHash;
            public double wallSeconds;public int frames,castAttempts,acceptedCasts,ownHitEvents,towerShotEvents,observedDeaths,organicClaims,clearApproachTeleports;
            public int initialBank,bankBeforeUpgrade,bankAfterUpgrade,duplicateDropId,repeatBalance;
            public int towerId,choiceMask,acceptedTier,modelGroupMatches;
            public float acceptedDamage,acceptedInterval;
            public List<Gate> gates=new List<Gate>(24);
            public List<Observation> observations=new List<Observation>(64);
            public List<string> actions=new List<string>(96);
            public List<ScreenProof> screenshots=new List<ScreenProof>(2);
        }
        sealed class Seen
        {internal OracleEnemy Enemy;internal int Id;internal bool Dead;internal Vector3 DeathPosition;}
        sealed class Run
        {
            internal GameSession Owner;internal RoomConnection Rooms;internal OracleCombat Combat;internal Receipt Report;
            internal Stage Stage;internal double Started,StageStarted,NextScan,NextCast,LastClockWall,LastGame;
            internal int LastFrame=-1,Pad=-1,BankObserved,PendingDrop=-1,LastClaimedDrop=-1;
            internal bool RoomRequested,ReadyRequested,BuildRequested,ReadyResumeUsed;
            internal string TerminalStatus,TerminalReason,ProfileBefore;internal Vector3 PadPosition;
            internal bool CaptureScreens;internal ScreenProof Screen;internal double CaptureSeconds,CaptureDeadline;
            internal XpPickup PendingPickup;internal double PendingPickupAt;internal NpcBlacksmith Smith;
            internal readonly List<Seen> Enemies=new List<Seen>(512);
            internal readonly RaycastHit[] Hits=new RaycastHit[64];internal readonly Collider[] Overlaps=new Collider[64];
        }
        static Run active;static Receipt last=new Receipt();
        public static bool IsRunning=>active!=null;
        public static string CurrentReceiptJson()=>JsonUtility.ToJson(active!=null?active.Report:last,true);
        public static void Cancel()=>Stop("CANCELLED","Root/user cancelled; leaving only the fixture-owned room.");
        public static string Begin(GameSession owner,string receiptPath,bool allowAuthorizedPublicSkipSetup,bool captureScreens=true)
        {
            if(active!=null)return CurrentReceiptJson();
            var report=new Receipt{path=ValidPath(receiptPath),startedUtc=DateTime.UtcNow.ToString("o")};last=report;
            AddGates(report);
            if(File.Exists(report.path))
            {report.status="BLOCKED_RECEIPT_EXISTS";report.reason="Use a new receipt path; previous native evidence is never overwritten.";return JsonUtility.ToJson(report,true);}
            if(!EditorApplication.isPlaying||EditorApplication.isPaused||EditorApplication.isCompiling||owner==null||
                !ReferenceEquals(owner,GameSession.Instance)||owner.Rooms==null||owner.Defense==null||owner.Combat==null||owner.Player==null||
                owner.Player.ViewCamera==null||owner.World==null||owner.Builder==null||owner.Shop==null||owner.FieldSmith==null||owner.Tutorial==null||
                owner.Rooms.Connected||owner.Rooms.Busy||owner.Defense.Running||owner.CoopMode||owner.World.IsCoopArena||owner.Combat.AutoFireEnabled||
                AbilityBindings.IsRebinding||GameActionBindings.IsRebinding||owner.Opening!=null&&(owner.Opening.IsIntroPlaying||owner.Opening.IsTransitioning))
                return Reject(report,"BLOCKED_CONTEXT","Requires stable disposable offline Play menu/hub after normal intro; no existing room, auto-fire, rebind or active arena.");
            if(!Application.isFocused)return Reject(report,"BLOCKED_FOCUS","Real Player/Smith focus gates require actual Application.isFocused. No window is focused by this fixture.");
            if(!owner.Tutorial.Finished&&!allowAuthorizedPublicSkipSetup)
                return Reject(report,"BLOCKED_TRAINING","005 setup needs genuine completed training or root's explicit public SkipTutorialToHub authorization; no lesson/Finished writes.");
            var r=new Run{Owner=owner,Rooms=owner.Rooms,Combat=owner.Combat,Report=report,Started=EditorApplication.timeSinceStartup,
                StageStarted=EditorApplication.timeSinceStartup,Stage=Stage.Hub,LastClockWall=EditorApplication.timeSinceStartup,LastGame=Time.timeAsDouble,CaptureScreens=captureScreens};
            r.ProfileBefore=Profile(owner);report.profileBeforeHash=Hash(r.ProfileBefore);report.nativeExecuted=true;report.status="RUNNING";active=r;
            try
            {
                EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlayState;
                AssemblyReloadEvents.beforeAssemblyReload+=OnReload;EditorApplication.quitting+=OnQuit;
                r.Combat.AuthoritativeShotHit+=OnOwnHit;PlacedElementShrine.AuthoritativeShot+=OnTowerShot;
                if(!owner.Tutorial.Finished)
                {
                    report.publicSkipSetupExcludedFromTutorial=true;
                    report.actions.Add("Authorized public SkipTutorialToHub fixture setup; explicitly EXCLUDED from tutorial acceptance/reward proof.");
                    owner.SkipTutorialToHub();
                }
                SetGate(r,"setup",owner.Tutorial.Finished?"PASS":"BLOCKED","Public training-hub setup only; full004 tutorial is a separate native receipt.");
                if(!owner.Tutorial.Finished)Stop("BLOCKED_TRAINING","Normal public setup did not produce a trained hub.");
                Save(report);
            }
            catch(Exception error){Stop("FAILED",error.ToString());}
            return CurrentReceiptJson();
        }
        static void Tick()
        {
            var r=active;if(r==null)return;
            try
            {
                double now=EditorApplication.timeSinceStartup;
                if(r.Stage==Stage.Leaving){CleanupTick(r,now);return;}
                if(now-r.Started>=WallLimit-CleanupAllowance){Stop("INCOMPLETE","285-second work bound reached;15 seconds reserved for owned-room cleanup.");return;}
                if(!EditorApplication.isPlaying||EditorApplication.isPaused||EditorApplication.isCompiling||r.Owner==null||
                    !ReferenceEquals(r.Owner,GameSession.Instance)||r.Owner.Rooms!=r.Rooms||r.Owner.Combat!=r.Combat)
                {Stop("CANCELLED","Play/session/compiler ownership changed.");return;}
                if(!Application.isFocused){Stop("BLOCKED_FOCUS","Actual focus was lost; no hidden Resume/focus bypass.");return;}
                if(r.Owner.IsDead||r.Owner.IsDowned||r.Owner.Victory){Stop("INCOMPLETE","Normal defense ended/downed before requested gates; no HP reset.");return;}
                if(AbilityBindings.IsRebinding||GameActionBindings.IsRebinding){Stop("CANCELLED","Binding context changed.");return;}
                if(Time.timeAsDouble>r.LastGame){r.LastGame=Time.timeAsDouble;r.LastClockWall=now;}
                else if(now-r.LastClockWall>10){Stop("INCOMPLETE","Actual game clock stalled for ten wall seconds.");return;}
                if(Time.frameCount==r.LastFrame)return;r.LastFrame=Time.frameCount;r.Report.frames++;
                if(r.RoomRequested&&r.Stage!=Stage.Room&&!r.Rooms.Connected){Stop("BLOCKED_SERVICES","Owned real room disconnected: "+r.Rooms.Status);return;}
                if(r.Rooms.Connected&&r.Rooms.PlayerCount!=1){Stop("CANCELLED","Another participant joined the fixture-owned room; no shared-party mutations attempted.");return;}
                if(r.Owner.Paused&&r.Stage!=Stage.Arena&&r.Stage!=Stage.SmithZero&&r.Stage!=Stage.SmithZeroCapture&&r.Stage!=Stage.SmithUpgrade&&r.Stage!=Stage.Upgrade&&r.Stage!=Stage.Tier2Capture&&r.Stage!=Stage.Repeat)
                {Stop("CANCELLED","Unexpected gameplay pause; fixture does not resume interruptions.");return;}
                TrackEnemies(r);
                switch(r.Stage)
                {
                    case Stage.Hub:
                        if(r.Owner.Opening!=null&&r.Owner.Opening.IsTransitioning)return;
                        if(!r.RoomRequested){r.RoomRequested=true;r.Report.networkRequested=true;r.Report.actions.Add("Public CreateRoom exactly once: real UGS anonymous auth + private Relay session.");r.Rooms.CreateRoom();Go(r,Stage.Room);}
                        break;
                    case Stage.Room:
                        if(r.Rooms.Connected)
                        {
                            if(!Require(r,"real_room",r.Rooms.IsHost&&r.Rooms.PlayerCount==1,"Real connected host; no synthetic peer/DTO."))return;
                            r.Report.ownedRoomCreated=true;Go(r,Stage.Ready);
                        }
                        else if(!r.Rooms.Busy||now-r.StageStarted>60)Stop("BLOCKED_SERVICES","CreateRoom did not produce an actual Relay host: "+r.Rooms.Status);
                        break;
                    case Stage.Ready:
                        if(r.Owner.Opening!=null&&r.Owner.Opening.IsTransitioning)return;
                        if(!r.ReadyRequested){r.ReadyRequested=true;r.Rooms.SetCoopReady(true);r.Report.actions.Add("Public SetCoopReady(true); normal host party gate starts campaign.");}
                        if(r.Rooms.CampaignActive){if(!Require(r,"real_ready",r.Rooms.LocalReady&&r.Rooms.ReadyPlayerCount==1,"Real registered ready host accepted by existing party gate."))return;Go(r,Stage.Arena);}
                        else if(now-r.StageStarted>20)Stop("BLOCKED_READY",r.Rooms.PartyReadyStatus);
                        break;
                    case Stage.Arena:
                        if(r.Owner.Defense.Running&&r.Owner.Defense.ArenaReady&&r.Owner.Paused&&
                            (r.Owner.Loading==null||!r.Owner.Loading.IsLoading)&&!r.ReadyResumeUsed)
                        {
                            Button resume=FindButton(r.Owner.Hud,"Resume");
                            if(resume!=null&&resume.gameObject.activeInHierarchy&&resume.interactable)
                            {r.ReadyResumeUsed=true;r.Report.actions.Add("Actual active Resume button UnityEvent once after normal ready/loading completion; semantic gesture, no OS/device input.");resume.onClick.Invoke();}
                        }
                        if(r.Owner.Defense.Running&&r.Owner.Defense.ArenaReady&&r.Owner.IsPlaying)
                        {
                            if(!Require(r,"real_arena",r.Owner.CoopMode&&r.Owner.World.IsCoopArena&&r.Owner.Defense.IsAuthority&&
                                r.Owner.World.TowerBuildPads.Length==4&&r.Owner.Defense.PlacedCount==0,"Actual005 four-pad arena/run, host authority."))return;
                            if(!CheckChoices(r))return;r.BankObserved=r.Owner.Defense.LocalForgeBalance;Go(r,Stage.Place);
                        }
                        else if(now-r.StageStarted>40)Stop("BLOCKED_ARENA","Normal loading/ready handoff did not finish: "+r.Owner.Defense.Status);
                        break;
                    case Stage.Place:Place(r);break;
                    case Stage.SmithZero:
                        if(!OpenSmith(r,now))return;
                        int zero=r.Owner.Defense.LocalForgeBalance;bool allRejected=zero==0;
                        for(int type=0;type<4;type++)allRejected&=r.Owner.FieldSmith.Submit(type)==TowerForgeSubmission.Rejected&&r.Owner.Defense.GetLocalTowerForgeTier(type)==1;
                        if(!Require(r,"zero_charge_choices",allRejected&&r.Owner.Defense.LocalForgeBalance==0,"All four actual accepted base ranks/catalog choices exist; optional upgrades reject at zero charge. UI mouse/key routing excluded."))return;
                        Go(r,Stage.SmithZeroCapture);break;
                    case Stage.SmithZeroCapture:
                        if(!CapturePanel(r,now,"smith-zero"))return;
                        r.Owner.FieldSmith.Close(true);Go(r,Stage.Organic);break;
                    case Stage.Organic:
                        CollectOrganic(r,now);if(active!=r||r.Stage!=Stage.Organic)return;
                        if(r.Report.organicClaims>=8&&r.Owner.Defense.LocalForgeBalance>=8)
                        {if(!Require(r,"eight_organic_claims",r.Report.observedDeaths>=8&&r.Report.acceptedCasts>0&&r.Report.ownHitEvents>0,
                            "Eight normal XP proximity claims credited +1 each, after actual spawned enemy deaths; real ManualCast and owned hit observed."))return;Go(r,Stage.DuplicateDrop);return;}
                        CastAtRealEnemy(r,now);break;
                    case Stage.DuplicateDrop:
                        if(r.LastClaimedDrop<1){Stop("INCOMPLETE","No exact public dead-enemy ID matched an actually collected XP mote; duplicate-drop proof unavailable.");return;}
                        int before=r.Owner.Defense.LocalForgeBalance,xpSequence=r.Owner.Progression.XpGainSequence;
                        r.Owner.Defense.RequestXpClaim(r.LastClaimedDrop);r.Owner.Defense.RequestXpClaim(r.LastClaimedDrop);
                        if(!Require(r,"duplicate_drop",before==r.Owner.Defense.LocalForgeBalance&&xpSequence==r.Owner.Progression.XpGainSequence,
                            "Repeated public claim for observed/collected enemy dropID "+r.LastClaimedDrop+" grants no additional bank or XP."))return;
                        r.Report.duplicateDropId=r.LastClaimedDrop;Go(r,Stage.SmithUpgrade);break;
                    case Stage.SmithUpgrade:
                        if(!OpenSmith(r,now))return;
                        r.Report.bankBeforeUpgrade=r.Owner.Defense.LocalForgeBalance;
                        if(!Require(r,"smith_range_los",r.Owner.FieldSmith.CanTransact()&&SmithRay(r),"Actual focused visible Smith panel, real range/nearest-solid NPC LOS and host authority."))return;
                        if(r.Owner.FieldSmith.Submit(0)!=TowerForgeSubmission.AwaitingHost){Stop("INCOMPLETE","Actual Smith Tier2 request was not submitted.");return;}
                        Go(r,Stage.Upgrade);break;
                    case Stage.Upgrade:
                        if(r.Owner.Defense.ForgeRequestPending){if(now-r.StageStarted>8)Stop("INCOMPLETE","Real upgrade receipt timed out.");return;}
                        r.Report.bankAfterUpgrade=r.Owner.Defense.LocalForgeBalance;r.Report.acceptedTier=r.Owner.Defense.GetLocalTowerForgeTier(0);
                        if(!Require(r,"tier2_debit",r.Owner.Defense.LastForgeOutcome==MatchForgeOutcome.Applied&&r.Report.acceptedTier==2&&
                            r.Report.bankAfterUpgrade==r.Report.bankBeforeUpgrade-8,"Host accepted Tier2 once with exact8 match-only debit."))return;
                        if(!CheckAcceptedModel(r,2))return;Go(r,Stage.Tier2Capture);break;
                    case Stage.Tier2Capture:
                        if(CapturePanel(r,now,"smith-tier2"))Go(r,Stage.Repeat);break;
                    case Stage.Repeat:
                        int repeatBank=r.Owner.Defense.LocalForgeBalance;
                        Button upgrade=FindButton(r.Owner.Hud,"RequestUpgrade0");
                        if(upgrade==null){Stop("INCOMPLETE","Actual repeated-click button was not present.");return;}
                        upgrade.onClick.Invoke();upgrade.onClick.Invoke(); // Semantic UnityEvent, not OS/input routing proof.
                        r.Report.repeatBalance=r.Owner.Defense.LocalForgeBalance;
                        if(!Require(r,"repeat_ui",r.Report.repeatBalance==repeatBank&&r.Owner.Defense.GetLocalTowerForgeTier(0)==2&&!r.Owner.Defense.ForgeRequestPending,
                            "Two actual existing upgrade-button UnityEvents at Tier2/<18 charge leave bank/tier alone; not protocol replay proof."))return;
                        Stop("CORE_ACCEPTED_WITH_EXCLUSIONS","Requested organic Tier2 core visited; raw stale/reordered Forge protocol, UI device routing and Tier3 remain explicitly unvisited/excluded.");break;
                }
            }
            catch(Exception error){Stop("FAILED",error.ToString());}
        }
        static bool CheckChoices(Run r)
        {
            var catalog=r.Owner.TowerModels;
            if(!Require(r,"catalog",catalog!=null&&catalog.TryValidate(out _)&&r.Owner.Builder.ModelsReady&&ShrinePlacementBuilder.TowerTypeCount==4,"Actual bound saved four-type/12-template catalog validates."))return false;
            r.Report.initialBank=r.Owner.Defense.LocalForgeBalance;
            for(int type=0;type<4;type++)
            {
                Button button=FindButton(r.Owner.Hud,"BuildTower"+type);Text label=button!=null?button.GetComponentInChildren<Text>(true):null;
                if(button==null||label==null||label.text.IndexOf(ShrinePlacementBuilder.TowerNames[type],StringComparison.OrdinalIgnoreCase)<0||
                    r.Owner.Defense.GetLocalTowerForgeTier(type)!=1||catalog.GetMesh(type,1,0)==null)
                {Stop("FAILED","Missing actual tier-one choice/catalog for type "+type);return false;}
                r.Report.choiceMask|=1<<type;
            }
            if(r.Report.initialBank!=0){Stop("FAILED","Fresh actual match did not start at zero Forge charge.");return false;}
            return true;
        }
        static void Place(Run r)
        {
            if(!r.BuildRequested)
            {
                Vector3[] pads=r.Owner.World.TowerBuildPads;Transform eye=r.Owner.Player.ViewCamera.transform;Quaternion old=eye.rotation;
                try
                {
                    for(int i=0;i<pads.Length;i++)
                    {
                        if(!r.Owner.World.IsBuildPadActive(pads[i])||!r.Owner.Builder.ValidatePlacementFrom(r.Owner.Player.transform.position,pads[i],out _))continue;
                        eye.rotation=Quaternion.LookRotation(pads[i]-eye.position,Vector3.up);
                        if(!r.Owner.World.TowerPlatforms.TryPick(r.Owner.Player,25,out int picked,out RaycastHit hit)||picked!=i)continue;
                        r.Pad=i;r.PadPosition=pads[i];break;
                    }
                }
                finally{eye.rotation=old;}
                if(!Require(r,"pad_pick",r.Pad>=0,"Real public nearest-solid viewport physics pick + host range/LOS placement validation; no virtual mouse or pad teleport."))return;
                r.BuildRequested=true;r.Owner.Defense.RequestBuild(0,r.PadPosition);return;
            }
            PlacedElementShrine tower=FindPlaced(r,1);
            if(tower==null){if(EditorApplication.timeSinceStartup-r.StageStarted>5)Stop("INCOMPLETE","Host did not accept real build request: "+r.Owner.Defense.Status);return;}
            r.Report.towerId=tower.Id;
            if(!Require(r,"zero_charge_tier1",r.Owner.Defense.PlacedCount==1&&r.Owner.Defense.LocalBuildTokens==2&&r.Owner.Defense.LocalForgeBalance==0&&
                r.Owner.Defense.GetTowerTier(tower.Id,0)==1,"Real accepted Fire build consumes one normal token, zero Forge charge."))return;
            if(!CheckAcceptedModel(r,1))return;Go(r,Stage.SmithZero);
        }
        static bool CheckAcceptedModel(Run r,int tier)
        {
            PlacedElementShrine tower=FindPlaced(r,r.Report.towerId);
            SavedTowerModelPresentation model=tower!=null?tower.GetComponentInChildren<SavedTowerModelPresentation>():null;
            bool match=model!=null&&model.AcceptedTier==tier&&!tower.IsDestroyed;int matched=0;
            if(match)for(int group=0;group<6;group++)
            {
                string name="Tower "+(TowerModelGroup)group;MeshFilter found=null;
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))if(filter.name==name){found=filter;break;}
                if(found==null||found.sharedMesh!=r.Owner.TowerModels.GetMesh(0,tier,group))match=false;
                else{var renderer=found.GetComponent<Renderer>();if(renderer==null||renderer.sharedMaterial!=r.Owner.TowerModels.GetMaterial(0))match=false;else matched++;}
            }
            if(tier==2){r.Report.modelGroupMatches=matched;r.Report.acceptedDamage=MatchTowerForgeRules.Damage(2);r.Report.acceptedInterval=MatchTowerForgeRules.Interval(2);}
            return Require(r,tier==1?"saved_tier1":"saved_tier2",match&&matched==6,
                "Actual surviving placed tower uses host-accepted tier "+tier+" and all six exact saved catalog mesh/material references. Damage/cadence live isolation is separate.");
        }
        static PlacedElementShrine FindPlaced(Run r,int id)
        {return PlacedElementShrine.TryGetRegistered(id,out var tower)&&tower!=null&&tower.gameObject.scene==r.Owner.gameObject.scene?tower:null;}
        static bool OpenSmith(Run r,double now)
        {
            if(r.Owner.FieldSmith.Visible)return r.Owner.FieldSmith.CanTransact();
            if(r.Smith==null)foreach(var npc in Object.FindObjectsByType<NpcBlacksmith>())
                if(npc.gameObject.scene==r.Owner.gameObject.scene&&(npc.transform.position-r.Owner.World.MatchForgePosition).sqrMagnitude<.1f){r.Smith=npc;break;}
            if(r.Smith==null){if(now-r.StageStarted>10)Stop("BLOCKED_SMITH","Actual field Smith did not prepare: "+r.Owner.FieldSmith.Status);return false;}
            Vector3 offset=r.Owner.Player.transform.position-r.Smith.transform.position;
            if(offset.x*offset.x+offset.z*offset.z>3f*3f)
            {
                bool placed=false;
                foreach(float distance in new[]{2.4f,2.8f,2f})
                {
                    Vector3 p=r.Smith.transform.position+r.Smith.transform.forward*distance;p.y=.2f;
                    Vector3 toward=r.Smith.transform.position-p;
                    if(TryTeleport(r,p,Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg,"actual Smith")){placed=true;break;}
                }
                if(!placed){Stop("INCOMPLETE","No real capsule-clear actual Smith approach.");return false;}return false;
            }
            if(r.Owner.FieldSmith.ClaimsInteract&&r.Owner.FieldSmith.TryInteract())return false;
            if(now-r.StageStarted>12)Stop("INCOMPLETE","Actual Smith look/range/LOS interaction did not open; no context override.");return false;
        }
        static bool SmithRay(Run r)
        {
            if(r.Smith==null)return false;Vector3 origin=r.Owner.Player.ViewCamera.transform.position,to=r.Smith.AimPoint-origin;
            return Physics.Raycast(origin,to.normalized,out RaycastHit hit,to.magnitude+.08f,~(1<<2),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<NpcBlacksmith>()==r.Smith;
        }
        static void TrackEnemies(Run r)
        {
            foreach(var enemy in r.Combat.ActiveEnemies)
            {
                if(enemy==null)continue;Seen seen=null;foreach(var item in r.Enemies)if(item.Id==enemy.TargetId){seen=item;break;}
                if(seen==null){seen=new Seen{Enemy=enemy,Id=enemy.TargetId};r.Enemies.Add(seen);}
                if(!seen.Dead&&!enemy.Alive){seen.Dead=true;seen.DeathPosition=enemy.transform.position;r.Report.observedDeaths++;}
            }
        }
        static void CollectOrganic(Run r,double now)
        {
            int bank=r.Owner.Defense.LocalForgeBalance;
            if(bank<r.BankObserved){Stop("FAILED","Forge bank decreased before any requested upgrade.");return;}
            if(bank>r.BankObserved)
            {
                r.Report.organicClaims+=bank-r.BankObserved;r.BankObserved=bank;
                if(r.PendingDrop>0)r.LastClaimedDrop=r.PendingDrop;
                r.PendingDrop=-1;r.PendingPickup=null;Observe(r,"Actual host-accepted XP proximity claim; bank now "+bank);
            }
            if(r.PendingPickup!=null&&r.PendingPickup.gameObject.activeInHierarchy&&now-r.PendingPickupAt<2)return;
            r.PendingPickup=null;r.PendingDrop=-1;
            if(now<r.NextScan)return;r.NextScan=now+.2;
            foreach(var pickup in Object.FindObjectsByType<XpPickup>())
            {
                if(pickup.gameObject.scene!=r.Owner.gameObject.scene||!pickup.gameObject.activeInHierarchy)continue;
                Vector3 p=pickup.transform.position;int id=-1;
                foreach(var seen in r.Enemies)if(seen.Dead&&(new Vector2(p.x-seen.DeathPosition.x,p.z-seen.DeathPosition.z)).sqrMagnitude<.1f){id=seen.Id;break;}
                if(id<1)continue;bool clear=false;
                foreach(Vector3 offset in new[]{new Vector3(0,0,-.8f),new Vector3(0,0,.8f),new Vector3(-.8f,0,0),new Vector3(.8f,0,0)})
                {
                    Vector3 feet=new Vector3(p.x,.2f,p.z)+offset;
                    if(TryTeleport(r,feet,r.Owner.Player.transform.eulerAngles.y,"actual observed XP pickup "+id)){clear=true;break;}
                }
                if(clear){r.PendingPickup=pickup;r.PendingDrop=id;r.PendingPickupAt=now;return;}
            }
        }
        static void CastAtRealEnemy(Run r,double now)
        {
            if(now<r.NextCast||!r.Owner.IsPlaying)return;
            // Enemy query colliders intentionally use isTrigger=true. Borrow the
            // actual public combat target-grid/LOS path instead of an Ignore ray
            // that can never hit the target. No new damage or targeting authority.
            if(!r.Combat.TryFindNearestVisible(r.Owner.Player.ViewCamera.transform.position,11.7f,out OracleEnemy target)||
                target==null||!target.Targetable||target.GetComponent<Collider>()==null||!target.GetComponent<Collider>().enabled)return;
            int element=-1;
            for(int i=0;i<3;i++)if(r.Combat.CooldownRemaining[i]<=.001f){element=i;break;}
            if(element<0)return;
            if(!r.Combat.HasConfirmedSelection||r.Combat.ConfirmedMask!=(1<<element))
            {
                if(!r.Combat.TrySelectElement(element))return;
                if(r.Combat.SelectionPending&&!r.Combat.TrySelectElement(element))return;
                if(!r.Combat.HasConfirmedSelection||r.Combat.ConfirmedMask!=(1<<element))return;
            }
            Transform eye=r.Owner.Player.ViewCamera.transform;Quaternion old=eye.rotation;float before=r.Combat.CooldownRemaining[element];
            try{eye.rotation=Quaternion.LookRotation(target.GetComponent<Collider>().bounds.center-eye.position,Vector3.up);r.Report.castAttempts++;r.Combat.ManualCast();}
            finally{eye.rotation=old;}
            if(r.Combat.CooldownRemaining[element]>before+.5f){r.Report.acceptedCasts++;r.NextCast=now+1.05;}
            else r.NextCast=now+.2;
        }
        static bool TryTeleport(Run r,Vector3 feet,float yaw,string purpose)
        {
            Physics.SyncTransforms();int count=Physics.RaycastNonAlloc(feet+Vector3.up*2,Vector3.down,r.Hits,4,~(1<<2),QueryTriggerInteraction.Ignore);
            if(count==r.Hits.Length)return false;float nearest=float.PositiveInfinity;bool ground=false;
            for(int i=0;i<count;i++)
            {
                var hit=r.Hits[i];if(hit.collider==null||hit.collider.GetComponentInParent<OraclePlayer>()==r.Owner.Player||hit.distance>=nearest)continue;
                nearest=hit.distance;ground=hit.normal.y>.9f&&Mathf.Abs(hit.point.y)<.1f;
            }
            if(!ground)return false;var cc=r.Owner.Player.GetComponent<CharacterController>();if(cc==null)return false;
            Vector3 center=feet+cc.center;float shaft=Mathf.Max(0,cc.height*.5f-cc.radius);
            count=Physics.OverlapCapsuleNonAlloc(center+Vector3.up*shaft,center-Vector3.up*shaft,cc.radius+.02f,r.Overlaps,~(1<<2),QueryTriggerInteraction.Ignore);
            if(count==r.Overlaps.Length)return false;
            for(int i=0;i<count;i++)if(r.Overlaps[i]!=null&&r.Overlaps[i].GetComponentInParent<OraclePlayer>()!=r.Owner.Player)return false;
            r.Owner.Player.Teleport(feet,yaw);r.Report.clearApproachTeleports++;
            r.Report.actions.Add("Public Physics-clear approach to "+purpose+" at "+feet+"; excluded from walking/input acceptance.");return true;
        }
        static void OnOwnHit(int id,Vector3 point){if(active!=null&&active.Stage!=Stage.Leaving)active.Report.ownHitEvents++;}
        static void OnTowerShot(int id,Vector3 a,Vector3 b,Vector3 c,int n){if(active!=null&&id==active.Report.towerId)active.Report.towerShotEvents++;}
        static void Go(Run r,Stage stage){r.Stage=stage;r.StageStarted=EditorApplication.timeSinceStartup;r.Screen=null;r.CaptureSeconds=0;Observe(r,"Entered "+stage);Save(r.Report);}
        static bool CapturePanel(Run r,double now,string name)
        {
            if(!r.CaptureScreens)return true;
            if(r.Screen!=null)
            {
                if(File.Exists(r.Screen.path))
                {
                    byte[] bytes=File.ReadAllBytes(r.Screen.path);
                    if(bytes.Length>24&&bytes[0]==137&&bytes[1]==80&&bytes[2]==78&&bytes[3]==71&&
                        PngInt(bytes,16)==r.Screen.width&&PngInt(bytes,20)==r.Screen.height)
                    {using(var sha=SHA256.Create())r.Screen.sha256=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                        r.Screen.status="PNG_WRITTEN_NOT_VISUALLY_REVIEWED";return true;}
                }
                if(now<r.CaptureDeadline)return false;
                r.Screen.status="CAPTURE_FAILED_NON_BLOCKING";r.Screen.detail="No complete matching actual-viewport PNG within5s; Forge gates remain independent.";return true;
            }
            if(!r.Owner.FieldSmith.Visible)
            {r.Report.screenshots.Add(new ScreenProof{stage=name,status="CAPTURE_FAILED_NON_BLOCKING",detail="Actual Smith panel ceased to be visible; no context was fabricated."});return true;}
            r.CaptureSeconds+=Mathf.Min(Time.unscaledDeltaTime,.1f);
            if(r.CaptureSeconds<.2)return false;
            Camera camera=r.Owner.Player.ViewCamera;
            var shot=new ScreenProof{stage=name,frame=Time.frameCount,width=Screen.width,height=Screen.height,actualPanelVisible=r.Owner.FieldSmith.Visible,
                applicationFocused=Application.isFocused,acceptedTier=r.Owner.Defense.GetLocalTowerForgeTier(0),bank=r.Owner.Defense.LocalForgeBalance,
                advancingFrameSeconds=r.CaptureSeconds,cameraPosition=camera.transform.position,cameraRotation=camera.transform.rotation,
                fieldOfView=camera.fieldOfView,cameraPixelRect=camera.pixelRect,
                path=Path.Combine(Path.GetDirectoryName(r.Report.path),Path.GetFileNameWithoutExtension(r.Report.path)+"-"+name+".png")};
            r.Screen=shot;r.Report.screenshots.Add(shot);
            if(shot.width<=0||shot.height<=0||File.Exists(shot.path))
            {shot.status="CAPTURE_FAILED_NON_BLOCKING";shot.detail="No valid viewport or destination already exists; previous evidence never overwritten.";return true;}
            try{ScreenCapture.CaptureScreenshot(shot.path);shot.status="CAPTURE_REQUESTED";r.CaptureDeadline=now+5;}
            catch(Exception error){shot.status="CAPTURE_FAILED_NON_BLOCKING";shot.detail=error.Message;return true;}
            Save(r.Report);return false;
        }
        static int PngInt(byte[] bytes,int offset)=>bytes[offset]<<24|bytes[offset+1]<<16|bytes[offset+2]<<8|bytes[offset+3];
        static void Observe(Run r,string detail)
        {
            r.Report.observations.Add(new Observation{stage=r.Stage.ToString(),detail=detail,wall=EditorApplication.timeSinceStartup-r.Started,
                remaining=r.Owner.Defense.RemainingSeconds,frame=Time.frameCount,bank=r.Owner.Defense.LocalForgeBalance,tokens=r.Owner.Defense.LocalBuildTokens,
                placed=r.Owner.Defense.PlacedCount,kills=r.Owner.Defeated,acceptedCasts=r.Report.acceptedCasts,organicClaims=r.Report.organicClaims,
                feet=r.Owner.Player.transform.position,focused=Application.isFocused,paused=r.Owner.Paused});
        }
        static bool Require(Run r,string name,bool value,string detail)
        {SetGate(r,name,value?"PASS":"FAIL",detail);if(!value)Stop("FAILED",name+": "+detail);return value;}
        static void SetGate(Run r,string name,string status,string detail)
        {foreach(var gate in r.Report.gates)if(gate.name==name){gate.status=status;gate.detail=detail;return;}throw new InvalidOperationException("Unknown gate "+name);}
        static void AddGates(Receipt report)
        {
            foreach(string name in new[]{"setup","real_room","real_ready","real_arena","catalog","pad_pick","zero_charge_choices","zero_charge_tier1","saved_tier1",
                "eight_organic_claims","duplicate_drop","smith_range_los","tier2_debit","saved_tier2","repeat_ui","owned_room_cleanup"})report.gates.Add(new Gate{name=name,required=true});
            report.gates.Add(new Gate{name="raw_forge_duplicate_stale",status="UNVISITED_API_BOUNDARY",detail="No public raw request/replay API. No fabricated DTO/private host call."});
            report.gates.Add(new Gate{name="ui_device_routing",status="EXCLUDED",detail="Semantic public requests/picking and existing UnityEvents; no OS or virtual mouse/input routing claim."});
            report.gates.Add(new Gate{name="isolated_tier2_damage_cadence",status="UNVISITED",detail="Saved model switch and host tier observed; isolated live damage/cadence is separate native coverage."});
            report.gates.Add(new Gate{name="tier3",status="NOT_REQUIRED",detail="No additional18 charge farming/purchase required by this300s fixture."});
        }
        static void Stop(string status,string reason)
        {
            var r=active;if(r==null||r.Stage==Stage.Leaving)return;r.TerminalStatus=status;r.TerminalReason=reason;
            r.Stage=Stage.Leaving;r.StageStarted=EditorApplication.timeSinceStartup;
            try{r.Owner?.FieldSmith?.Close(false);r.Owner?.Builder?.CancelPlacement();if(r.Owner!=null&&r.Owner.HasStarted)r.Owner.Pause();}catch(Exception e){r.TerminalStatus="FAILED";r.TerminalReason+=" Cleanup pause: "+e.Message;}
            if(r.RoomRequested&&r.Rooms!=null){r.Rooms.LeaveRoom();}
            else Finish(r,true);
        }
        static void CleanupTick(Run r,double now)
        {
            if(r.Rooms==null||!r.Rooms.Connected&&!r.Rooms.Busy){Finish(r,true);return;}
            if(now-r.Started>=WallLimit||now-r.StageStarted>=CleanupAllowance){r.TerminalStatus="INCOMPLETE_CLEANUP";r.TerminalReason+=" Owned room cleanup did not settle inside300s/15s cleanup allowance.";Finish(r,false);}
        }
        static void Finish(Run r,bool roomLeft)
        {
            if(active!=r)return;
            EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload-=OnReload;EditorApplication.quitting-=OnQuit;
            if(r.Combat!=null)r.Combat.AuthoritativeShotHit-=OnOwnHit;PlacedElementShrine.AuthoritativeShot-=OnTowerShot;
            r.Report.callbacksDetached=true;r.Report.ownedRoomLeft=roomLeft;
            SetGate(r,"owned_room_cleanup",roomLeft?"PASS":"FAIL",r.RoomRequested?"Only this fixture-owned real room was left; no pre-existing room was accepted.":"No room was created.");
            if(r.Owner!=null){string after=Profile(r.Owner);r.Report.profileAfterHash=Hash(after);r.Report.profileChanged=after!=r.ProfileBefore;}
            r.Report.wallSeconds=EditorApplication.timeSinceStartup-r.Started;r.Report.endedUtc=DateTime.UtcNow.ToString("o");
            r.Report.status=r.TerminalStatus;r.Report.reason=r.TerminalReason;
            if(r.Report.status=="CORE_ACCEPTED_WITH_EXCLUSIONS")foreach(var gate in r.Report.gates)if(gate.required&&gate.status!="PASS")
            {r.Report.status="INCOMPLETE";r.Report.reason+=" Required gate unvisited/failed: "+gate.name;break;}
            last=r.Report;active=null;Save(last);
        }
        static void OnPlayState(PlayModeStateChange change){if(change==PlayModeStateChange.ExitingPlayMode){Stop("CANCELLED","Play exited.");if(active!=null)Finish(active,active.Rooms==null||!active.Rooms.Connected&&!active.Rooms.Busy);}}
        static void OnReload(){Stop("CANCELLED","Assembly reload; owned room LeaveRoom requested.");if(active!=null)Finish(active,false);}
        static void OnQuit(){Stop("CANCELLED","Editor quitting; owned room LeaveRoom requested.");if(active!=null)Finish(active,false);}
        static string Profile(GameSession owner)
        {
            return (PlayerPrefs.HasKey(TownKey)?"1":"0")+PlayerPrefs.GetString(TownKey,"")+"|"+(PlayerPrefs.HasKey(BackupKey)?"1":"0")+PlayerPrefs.GetString(BackupKey,"")+"|"+
                PlayerPrefs.GetInt(ElementProgression.TutorialXpClaimKey,0)+"|"+PlayerPrefs.GetInt(ElementProgression.TutorialHeartstoneClaimKey,0)+"|"+
                (owner.Shop!=null?owner.Shop.Heartcrystals:0)+"|"+(owner.Shop!=null?owner.Shop.PermanentPassiveMask:0);
        }
        static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
        static Button FindButton(OdysseyHud hud,string name){foreach(var b in hud.GetComponentsInChildren<Button>(true))if(b.name==name)return b;return null;}
        static string Reject(Receipt report,string status,string reason){report.status=status;report.reason=reason;report.endedUtc=DateTime.UtcNow.ToString("o");Save(report);return JsonUtility.ToJson(report,true);}
        static string ValidPath(string path)
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work"))+Path.DirectorySeparatorChar;
            string full=Path.GetFullPath(path??Path.Combine(root,"native-005-forge-driver.json"));
            if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!full.EndsWith(".json",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Receipt must be an absolute workspace work/*.json path.");
            return full;
        }
        static void Save(Receipt report){Directory.CreateDirectory(Path.GetDirectoryName(report.path));File.WriteAllText(report.path,JsonUtility.ToJson(report,true));}
    }
}
#endif
