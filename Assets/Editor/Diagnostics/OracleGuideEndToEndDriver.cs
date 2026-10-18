#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace OdysseyOracle.Editor
{
    /// <summary>Root-run disposable offline tutorial driver. No OS input, private writes or profile restoration.</summary>
    public static class OracleGuideEndToEndDriver
    {
        // # ARCHITECTURE SNAPSHOT
        // One bounded Editor observer, one owned virtual Keyboard. Normal engine
        // input updates consume queued state; the driver never calls InputSystem.Update.
        // Movement/selection/acknowledgement use actual bound keyboard actions.
        // Public ManualCast is a semantic click substitute, not mouse-routing proof.
        // Teleports are separately labelled clear approaches, never travel evidence.
        // Current tutorial owns lessons/actors/timers. No ready/mask/HP/lesson writes.
        const double WallLimit=240,StallLimit=5;
        const string TownKey="OdysseyOracle.TownProgression.v1",BackupKey="OdysseyOracle.TownProgression.v1.backup";
        static readonly Key[] NoKeys=Array.Empty<Key>();
        static readonly string[] MoveActions={"Forward","Left","Back","Right"};
        [Serializable] public sealed class CheckRecord {public string name,detail;public bool passed;}
        [Serializable] public sealed class Observation
        {
            public string stage,heading,instruction;
            public int frame,guideIndex,highlight,confirmedMask,acceptedMask,novaCount,resolved,trialAlive,kills;
            public double wallSeconds,gameSeconds;
            public Vector3 position;
            public bool paused,speaking,wandVisible;
        }
        [Serializable] public sealed class Receipt
        {
            public string status="NOT_STARTED",reason="",path,startedUtc,endedUtc;
            public bool nativeExecuted,osInputRequested,mouseInputRequested,networkRequested,profileWritesRequested;
            public bool keyboardRemoved,previousKeyboardRestored,callbacksDetached,leftPaused,profileUnchanged;
            public bool disposableRunRequiresRestart=true,manualCastUsedSemanticPublicApi=true;
            public int assertions,failures,distinctFrames,virtualInputEvents,clearApproachTeleports,manualCastAttempts,acceptedCasts;
            public int movementDirections,branchPreviewMask,guardianConfirmedMask,elementPreviewMask,novaAcknowledgements;
            public int novaImpactEvents,novaDamagedTargets,novaAliveAtImpact,novaAcceptedAtImpact,guardianHpReductions,guardianOwnHitConfirmations;
            public float observedJumpRise,shrineApproachDistance;public double observedSprintSeconds,wallSeconds,gameSeconds,minimumSpokenSeconds;
            public List<CheckRecord> checks=new List<CheckRecord>(64);
            public List<Observation> observations=new List<Observation>(32);
            public List<string> actions=new List<string>(96);
        }
        sealed class Profile
        {
            internal int Wallet,Permanent,Level,Xp,Points,Mask;
            internal bool HasTown,HasBackup,HasXp,HasHeart;
            internal string Town,Backup;internal int ClaimXp,ClaimHeart;
            internal readonly int[] Wands=new int[4];
            internal Profile(GameSession owner)
            {
                Wallet=owner.Shop.Heartcrystals;Permanent=owner.Shop.PermanentPassiveMask;
                Level=owner.Progression.Level;Xp=owner.Progression.Experience;Points=owner.Progression.SkillPoints;Mask=owner.Progression.UpgradeMask;
                for(int i=0;i<4;i++)Wands[i]=owner.Shop.WandLevel(i);
                HasTown=PlayerPrefs.HasKey(TownKey);Town=PlayerPrefs.GetString(TownKey,"");
                HasBackup=PlayerPrefs.HasKey(BackupKey);Backup=PlayerPrefs.GetString(BackupKey,"");
                HasXp=PlayerPrefs.HasKey(ElementProgression.TutorialXpClaimKey);ClaimXp=PlayerPrefs.GetInt(ElementProgression.TutorialXpClaimKey,0);
                HasHeart=PlayerPrefs.HasKey(ElementProgression.TutorialHeartstoneClaimKey);ClaimHeart=PlayerPrefs.GetInt(ElementProgression.TutorialHeartstoneClaimKey,0);
            }
            internal bool DefaultPower {get{if(Level!=1||Xp!=0||Points!=0||Mask!=0||Permanent!=0)return false;for(int i=0;i<4;i++)if(Wands[i]!=1)return false;return true;}}
            internal bool Same(GameSession owner)
            {
                var p=new Profile(owner);if(Wallet!=p.Wallet||Permanent!=p.Permanent||Level!=p.Level||Xp!=p.Xp||Points!=p.Points||Mask!=p.Mask||
                    HasTown!=p.HasTown||Town!=p.Town||HasBackup!=p.HasBackup||Backup!=p.Backup||HasXp!=p.HasXp||ClaimXp!=p.ClaimXp||HasHeart!=p.HasHeart||ClaimHeart!=p.ClaimHeart)return false;
                for(int i=0;i<4;i++)if(Wands[i]!=p.Wands[i])return false;return true;
            }
        }
        sealed class Run
        {
            internal GameSession Owner;internal TutorialDirector Tutorial;internal OracleCombat Combat;
            internal Keyboard Keyboard,PreviousKeyboard;internal Profile Profile;internal Receipt Report;
            internal double StartedWall,StartedGame,LastClockWall,LastGame,LastProfileWall,StageTime,LastCastGame,RunStartedTime;
            internal int LastFrame=-1,StageFrame,MoveIndex,GuideKey=-999,Branch=-1,TapStage,Element,TeachingPage=-1;
            internal TutorialDirector.Lesson LastLesson=(TutorialDirector.Lesson)(-1);
            internal Vector3 StagePosition;internal float JumpStart,GuardianHp,LastHitFlash;internal bool JumpSent,FinishRightTravel,FirstPairLaunched,FinisherLaunched;
            internal int InitialNova,InitialResolutions;internal Key HeldA=Key.None,HeldB=Key.None;
            internal float[] GuideDurations=new float[9],TeachingDurations=new float[2];internal float TeachingElapsed;
            internal readonly RaycastHit[] Hits=new RaycastHit[32];internal readonly Collider[] Overlaps=new Collider[32];
        }
        static Run active;static Receipt last=new Receipt();
        public static bool IsRunning=>active!=null;
        public static string CurrentReceiptJson()=>JsonUtility.ToJson(active!=null?active.Report:last,true);

        public static string Begin(GameSession owner,string receiptPath)
        {
            if(active!=null)return CurrentReceiptJson();
            var report=new Receipt{path=ValidPath(receiptPath),startedUtc=DateTime.UtcNow.ToString("o")};last=report;
            if(EditorApplication.isPlaying&&!Application.isFocused)
                return Reject(report,"Actual TutorialDirector.CanAdvanceTime and narration require Application.isFocused. Hidden/background Editor cannot exercise this complete flow without an unauthorized focus/private-state bypass. No keyboard or campaign was started.","BLOCKED_FOCUS");
            if(!EditorApplication.isPlaying||EditorApplication.isPaused||EditorApplication.isCompiling||
                owner==null||!ReferenceEquals(owner,GameSession.Instance)||owner.HasStarted||owner.CoopMode||owner.World==null||owner.World.IsCoopArena||
                owner.Tutorial==null||owner.Combat==null||owner.Player==null||owner.Player.ViewCamera==null||owner.Progression==null||owner.Shop==null||
                owner.Rooms!=null&&(owner.Rooms.Connected||owner.Rooms.Busy)||owner.Defense!=null&&owner.Defense.Running||
                owner.Opening!=null&&(owner.Opening.IsIntroPlaying||owner.Opening.IsTransitioning)||GameActionBindings.IsRebinding||owner.Combat.AutoFireEnabled)
                return Reject(report,"Requires focused stable fresh offline menu after intro, default power, auto-fire off, no room/rebind/arena. Driver never starts Play, skips intro, joins a room or focuses a window.");
            var profile=new Profile(owner);
            if(!profile.DefaultPower||Object.FindObjectsByType<XpPickup>().Length>0||Object.FindObjectsByType<HeartcrystalPickup>().Length>0)
                return Reject(report,"Use a fresh disposable run with level1/zero XP/points/passives, four level1 wands and no live reward objects. No profile resets or restoration are performed.");
            var run=new Run{Owner=owner,Tutorial=owner.Tutorial,Combat=owner.Combat,Profile=profile,Report=report,
                PreviousKeyboard=Keyboard.current,StartedWall=EditorApplication.timeSinceStartup,StartedGame=Time.timeAsDouble,
                LastClockWall=EditorApplication.timeSinceStartup,LastGame=Time.timeAsDouble,LastCastGame=-100};
            for(int i=0;i<run.GuideDurations.Length;i++)
            {run.GuideDurations[i]=Duration("Tutorial/guide_"+i.ToString("00"),TutorialDirector.GuideLines[i]);report.minimumSpokenSeconds+=Math.Max(0,run.GuideDurations[i]-.7);}
            report.minimumSpokenSeconds+=Math.Max(0,Duration("Tutorial/lesson_combat",TutorialDirector.CombatLessonLine)-.7)+
                Math.Max(0,Duration("Tutorial/lesson_nova_prepare",TutorialDirector.NovaPrepareLessonLine)-.7)+Math.Max(0,Duration("Tutorial/lesson_nova_splash",TutorialDirector.NovaSplashLessonLine)-.7);
            run.TeachingDurations[0]=Duration("Tutorial/lesson_nova_prepare",TutorialDirector.NovaPrepareLessonLine);
            run.TeachingDurations[1]=Duration("Tutorial/lesson_nova_splash",TutorialDirector.NovaSplashLessonLine);
            active=run;
            try
            {
                run.Keyboard=InputSystem.AddDevice<Keyboard>("Oracle004OwnedGuideKeyboard");run.Keyboard.MakeCurrent();
                QueueKeys(run,Key.None);run.Combat.CosmicNovaResolved+=OnNovaResolved;
                EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlayState;
                AssemblyReloadEvents.beforeAssemblyReload+=OnReload;EditorApplication.quitting+=OnQuit;
                report.status="RUNNING";report.nativeExecuted=true;
                report.actions.Add("Normal public BeginCampaign; it owns its ordinary arrival and pointer policy.");
                owner.BeginCampaign();
                if(!owner.HasStarted){Finish("INCOMPLETE","Normal BeginCampaign did not start the fresh run.");return CurrentReceiptJson();}
                Observe(run,"normal_campaign_started");Save(report);
            }
            catch(Exception exception){Finish("FAILED",exception.ToString());}
            return CurrentReceiptJson();
        }

        public static void Cancel()=>Finish("CANCELLED","Root/user cancelled. Disposable run is left paused; no profile restore.");
        static void Tick()
        {
            var r=active;if(r==null)return;
            try
            {
                double now=EditorApplication.timeSinceStartup;
                if(now-r.StartedWall>WallLimit){Finish("INCOMPLETE","240-second wall limit reached without skipping any narration.");return;}
                if(!EditorApplication.isPlaying||EditorApplication.isPaused||EditorApplication.isCompiling||r.Owner==null||
                    !ReferenceEquals(r.Owner,GameSession.Instance)||r.Owner.Tutorial!=r.Tutorial||r.Owner.Combat!=r.Combat)
                {Finish("CANCELLED","Play/compilation/session ownership changed.");return;}
                if(!Application.isFocused){Finish("BLOCKED_FOCUS","Actual tutorial focus gate stopped progression; no OS focus/private bypass.");return;}
                if(r.Keyboard==null||!r.Keyboard.added||Keyboard.current!=r.Keyboard||GameActionBindings.IsRebinding)
                {Finish("CANCELLED","Focus, keyboard ownership or binding context changed. No device/window is forced back.");return;}
                if(r.Owner.CoopMode||r.Owner.Rooms!=null&&(r.Owner.Rooms.Connected||r.Owner.Rooms.Busy))
                {Finish("CANCELLED","Offline context changed.");return;}
                bool branchPause=r.Tutorial.CurrentLesson==TutorialDirector.Lesson.Guide&&r.Tutorial.NeedsGuideContinue;
                if(branchPause&&(r.Owner.Hud==null||!r.Owner.Hud.IsGuidePreviewVisible))
                {Finish("CANCELLED","Actual guide preview is no longer visible. No hidden panel or resume action is issued.");return;}
                if(r.Owner.Paused&&!branchPause){Finish("CANCELLED","Gameplay was paused. The driver never resumes an interruption.");return;}
                if(Time.timeAsDouble>r.LastGame){r.LastGame=Time.timeAsDouble;r.LastClockWall=now;}
                else if(now-r.LastClockWall>StallLimit){Finish("INCOMPLETE","Game clock stalled for five seconds.");return;}
                if(Time.frameCount==r.LastFrame)return;r.LastFrame=Time.frameCount;r.Report.distinctFrames++;
                if(r.Owner.IsDowned||r.Owner.IsDead||r.Owner.Victory||r.Tutorial.CurrentLesson==TutorialDirector.Lesson.Downed||
                    r.Tutorial.CurrentLesson==TutorialDirector.Lesson.Recovery||r.Tutorial.CurrentLesson==TutorialDirector.Lesson.Shop||r.Tutorial.Finished)
                {Finish("FAILED","Driver crossed the TrainingEcho stop boundary or gameplay failed.");return;}
                if(now-r.LastProfileWall>.2)
                {
                    r.LastProfileWall=now;
                    if(!r.Profile.Same(r.Owner)){Finish("FAILED","Profile/run rewards changed; no arbitrary restoration was attempted.");return;}
                    if(RewardsNear(r,r.Owner.Player.transform.position,4.5f)){Finish("INCOMPLETE","Normal guardian reward approached pickup range; paused before further driver movement.");return;}
                }
                var lesson=r.Tutorial.CurrentLesson;
                if(lesson!=r.LastLesson)
                {
                    if(lesson==TutorialDirector.Lesson.Run&&r.LastLesson==TutorialDirector.Lesson.Move)
                    {
                        // Runtime may accept a new held key while prior momentum remains.
                        // Keep Right held in Run until real rightward travel is independently observed.
                        r.FinishRightTravel=true;
                    }
                    if(lesson==TutorialDirector.Lesson.Jump&&r.LastLesson==TutorialDirector.Lesson.Run)
                    {r.Report.observedSprintSeconds=r.StageTime-r.RunStartedTime;if(!Check(r,"real_sprint_transition",r.Report.observedSprintSeconds>=.5,"Actual tutorial accepted at least0.6s of held sprint with travel."))return;}
                    if(lesson==TutorialDirector.Lesson.Shrine&&r.LastLesson==TutorialDirector.Lesson.Jump)
                    {r.Report.observedJumpRise=Mathf.Max(r.Report.observedJumpRise,r.Owner.Player.transform.position.y-r.JumpStart);if(!Check(r,"real_jump_transition",r.Report.observedJumpRise>.2f,"Actual jump key and CharacterController rise, without teleport."))return;}
                    if(lesson==TutorialDirector.Lesson.Guide&&r.LastLesson==TutorialDirector.Lesson.Shrine)
                    {Vector3 d=r.Owner.Player.transform.position-r.Owner.World.ShrinePosition;d.y=0;r.Report.shrineApproachDistance=d.magnitude;
                        if(!Check(r,"real_shrine_approach",d.magnitude<5&&r.Report.clearApproachTeleports==0,"Shrine approach used actual movement; later teleports are separately labelled."))return;}
                    QueueKeys(r,Key.None);r.LastLesson=lesson;r.StageTime=0;r.StageFrame=Time.frameCount;
                    r.StagePosition=r.Owner.Player.transform.position;r.TapStage=0;r.GuideKey=-999;r.Branch=-1;
                    Observe(r,"lesson_"+lesson);
                    if(lesson==TutorialDirector.Lesson.NovaPractice)
                    {
                        r.InitialNova=r.Combat.OwnCosmicNovaCastCount;r.InitialResolutions=r.Combat.CosmicNovaResolvedCount;
                        if(!Check(r,"five_owned_trial_targets",r.Combat.CosmicNovaTrialActive&&r.Combat.CosmicNovaTrialAliveCount==5,"Normal guide acknowledgement created the production trial."))return;
                        if(!PlaceNovaApproach(r)){Finish("INCOMPLETE","No public Physics-clear reward-safe Nova approach candidate.");return;}
                    }
                }
                double dt=Math.Min(.1,Math.Max(0,Time.unscaledDeltaTime));
                if(r.Owner.IsPlaying||branchPause)r.StageTime+=dt;
                if(r.Tutorial.GuideAbilityPreviewIndex>=0)r.Report.elementPreviewMask|=1<<r.Tutorial.GuideAbilityPreviewIndex;
                switch(lesson)
                {
                    case TutorialDirector.Lesson.Move:DriveMove(r);break;
                    case TutorialDirector.Lesson.Run:DriveRun(r);break;
                    case TutorialDirector.Lesson.Jump:DriveJump(r);break;
                    case TutorialDirector.Lesson.Shrine:QueueKeys(r,GameActionBindings.GetKey("Forward"));break;
                    case TutorialDirector.Lesson.Guide:DriveGuide(r);break;
                    case TutorialDirector.Lesson.Guardian:DriveGuardian(r);break;
                    case TutorialDirector.Lesson.NovaTeaching:DriveTeaching(r);break;
                    case TutorialDirector.Lesson.NovaPractice:DriveNova(r);break;
                    case TutorialDirector.Lesson.TrainingEcho:
                        QueueKeys(r,Key.None);
                        bool passed=r.Report.movementDirections==15&&r.Report.branchPreviewMask==15&&r.Report.elementPreviewMask==15&&
                            r.Report.guardianConfirmedMask==15&&r.Report.guardianHpReductions>0&&r.Report.guardianOwnHitConfirmations>0&&r.Report.novaAcknowledgements==2&&
                            r.Report.novaImpactEvents==1&&r.Report.novaDamagedTargets==5&&r.Report.novaAliveAtImpact==0;
                        Check(r,"real_flow_reached_echo_boundary",passed,"Actual lessons, confirmed inputs, guardian damage and owned five-target Nova were observed.");
                        if(active!=null)Finish(passed?"PASSED":"FAILED","Stopped at TrainingEcho before its narration/downed/shop/reward continuation.");break;
                }
            }
            catch(Exception exception){Finish("FAILED",exception.ToString());}
        }

        static void DriveMove(Run r)
        {
            if(!r.Owner.IsPlaying)return;
            if(r.MoveIndex>=4){QueueKeys(r,Key.None);return;}
            QueueKeys(r,GameActionBindings.GetKey(MoveActions[r.MoveIndex]));
            if(r.StageTime<.35)return;
            Vector3 d=r.Owner.Player.transform.position-r.StagePosition;d.y=0;
            Vector3 direction=r.MoveIndex==0?r.Owner.Player.transform.forward:r.MoveIndex==1?-r.Owner.Player.transform.right:r.MoveIndex==2?-r.Owner.Player.transform.forward:r.Owner.Player.transform.right;
            if(!Check(r,"actual_move_"+MoveActions[r.MoveIndex],Vector3.Dot(d,direction)>.12f,"Owned bound key held while actual CharacterController position changed in its intended direction."))return;
            r.Report.movementDirections|=1<<r.MoveIndex;r.MoveIndex++;r.StageTime=0;r.StagePosition=r.Owner.Player.transform.position;
            QueueKeys(r,r.MoveIndex<4?GameActionBindings.GetKey(MoveActions[r.MoveIndex]):Key.None);
        }
        static void DriveRun(Run r)
        {
            if(r.FinishRightTravel)
            {
                QueueKeys(r,GameActionBindings.GetKey("Right"));Vector3 travel=r.Owner.Player.transform.position-r.StagePosition;travel.y=0;
                if(r.StageTime<.15||Vector3.Dot(travel,r.Owner.Player.transform.right)<=.12f)return;
                r.Report.movementDirections|=8;r.FinishRightTravel=false;r.RunStartedTime=r.StageTime;
                if(!Check(r,"actual_move_Right",r.Report.movementDirections==15,"Actual rightward travel completed after the runtime's lesson transition, before sprint input."))return;
            }
            QueueKeys(r,GameActionBindings.GetKey("Forward"),GameActionBindings.GetKey("Sprint"));
        }
        static void DriveJump(Run r)
        {
            if(!r.Owner.IsPlaying)return;
            if(!r.JumpSent){r.JumpSent=true;r.JumpStart=r.Owner.Player.transform.position.y;QueueKeys(r,GameActionBindings.GetKey("Jump"));}
            else if(Time.frameCount-r.StageFrame>=3)QueueKeys(r,Key.None);
            r.Report.observedJumpRise=Mathf.Max(r.Report.observedJumpRise,r.Owner.Player.transform.position.y-r.JumpStart);
        }
        static void DriveGuide(Run r)
        {
            QueueKeys(r,Key.None);var t=r.Tutorial;
            if(!t.NeedsGuideContinue)return;int branch=t.GuideHighlightElement;
            if(branch!=r.Branch){r.Branch=branch;r.StageTime=0;Observe(r,"branch_preview_"+branch);}
            // Only the changing branch line needs a fresh read; its common intro is already spoken once.
            string visible=t.Subtitle;int split=visible.LastIndexOf('\n');string branchText=split>=0?visible.Substring(split+1):visible;
            double required=CaptionDuration(branchText);
            if(branch==0)required=Math.Max(required,r.GuideDurations[3]);
            if(r.StageTime<required||t.IsSpeaking)return;
            r.Report.branchPreviewMask|=1<<branch;
            r.Report.actions.Add("Public AdvanceGuide after actual branch caption/voice duration: "+branch);
            // Same public route as the real Next Element/Continue Guide button.
            // Its fourth call owns the normal ClosePanels/Resume; no separate Resume is invoked.
            t.AdvanceGuide();r.StageTime=0;
        }
        static void DriveGuardian(Run r)
        {
            if(!r.Owner.IsPlaying)return;
            if(r.Combat.HitFlash>r.LastHitFlash+.1f)r.Report.guardianOwnHitConfirmations++;
            r.LastHitFlash=r.Combat.HitFlash;
            if(r.Combat.HasConfirmedSelection)r.Report.guardianConfirmedMask|=r.Combat.ConfirmedMask;
            var guardian=r.Combat.TutorialGuardian;
            if(guardian==null){Finish("FAILED","Production guardian was not created by the real tutorial.");return;}
            if(r.GuardianHp>0&&guardian.HP<r.GuardianHp)r.Report.guardianHpReductions++;
            r.GuardianHp=guardian.HP;
            if(guardian.Alive)
            {
                float distance=Vector3.Distance(guardian.transform.position,r.Owner.Player.transform.position);
                if(distance<5.5f||distance>7f)
                {
                    if(Time.frameCount-r.StageFrame<3)return;
                    float z=guardian.transform.position.z-5.7f;
                    if(z<4.2f)z=guardian.transform.position.z+6.3f;
                    if(z>18.5f||!TryTeleport(r,new Vector3(0,.2f,z),guardian.transform.position.z>z?0:180))
                    {Finish("INCOMPLETE","No clear guardian approach within the bounded main hall.");return;}
                    r.StageFrame=Time.frameCount;
                    if(!r.Combat.TutorialGuardianAwake)return;
                }
                if(!r.Combat.TutorialGuardianAwake)return;
            }
            if(r.Combat.TutorialGuardianDefeated&&r.Report.guardianConfirmedMask==15){QueueKeys(r,Key.None);return;}
            int element=r.Element%4;
            if(!TapSequence(r,element,element,1<<element))return;
            r.Report.guardianConfirmedMask|=r.Combat.ConfirmedMask;
            if(!guardian.Alive){r.Element++;r.TapStage=0;return;}
            if(r.Combat.CooldownRemaining[element]>.02f||Time.timeAsDouble-r.LastCastGame<1.05)return;
            Collider body=guardian.GetComponent<Collider>();
            if(body==null||!body.enabled){Finish("FAILED","Awake guardian has no actual public collider bounds to aim at.");return;}
            if(TryManualCast(r,body.bounds.center,r.Combat.ConfirmedMask))
            {r.Element++;r.TapStage=0;r.LastCastGame=Time.timeAsDouble;}
        }
        static void DriveTeaching(Run r)
        {
            int page=r.Tutorial.GuideAbilityPreviewIndex>=2?1:0;
            if(page!=r.TeachingPage){QueueKeys(r,Key.None);r.TeachingPage=page;r.TeachingElapsed=0;r.TapStage=0;r.StageFrame=Time.frameCount;Observe(r,"nova_teaching_"+page);}
            r.TeachingElapsed+=Mathf.Min(Time.unscaledDeltaTime,.1f);
            float duration=r.TeachingDurations[page];
            if(r.TapStage==1){if(Time.frameCount-r.StageFrame>=2){QueueKeys(r,Key.None);r.TapStage=2;}return;}
            if(r.TapStage==2)return;
            if(r.TeachingElapsed<duration+.05f||r.Tutorial.IsSpeaking||!r.Tutorial.Instruction.StartsWith("Press ",StringComparison.Ordinal))return;
            if(!Check(r,"nova_ack_after_real_duration_"+page,!r.Owner.CombatUnlocked&&!r.Combat.CosmicNovaTrialActive,"No trial/attack permission before completed explanation."))return;
            QueueKeys(r,GameActionBindings.GetKey("Interact"));r.Report.novaAcknowledgements++;r.TapStage=1;r.StageFrame=Time.frameCount;
        }
        static void DriveNova(Run r)
        {
            if(!r.Owner.IsPlaying)return;
            if(r.FinisherLaunched){QueueKeys(r,Key.None);return;}
            int a=r.FirstPairLaunched?2:0,b=r.FirstPairLaunched?3:1,mask=r.FirstPairLaunched?12:3;
            if(!TapSequence(r,a,b,mask))return;
            if(r.Combat.CooldownRemaining[a]>.02f||r.Combat.CooldownRemaining[b]>.02f||Time.timeAsDouble-r.LastCastGame<1.05)return;
            if(r.FirstPairLaunched&&!Check(r,"real_nova_prepared",r.Combat.CosmicNovaPrepared&&r.Combat.NovaIngredientMask==3,"Two accepted ingredient bits plus the confirmed complementary pair."))return;
            if(!TryManualCast(r,r.Combat.CosmicNovaTrialAimPoint,mask))return;
            r.LastCastGame=Time.timeAsDouble;r.TapStage=0;
            if(!r.FirstPairLaunched){r.FirstPairLaunched=true;Check(r,"accepted_first_pair",r.Combat.NovaIngredientMask==3,"Selection did not set this mask; actual accepted launch did.");}
            else{r.FinisherLaunched=true;Check(r,"accepted_finisher",r.Combat.OwnCosmicNovaCastCount==r.InitialNova+1&&r.Combat.NovaIngredientMask==0,"Own accepted finisher count advanced once; chain cleared by actual cast.");}
        }
        static bool TapSequence(Run r,int first,int second,int expected)
        {
            int frame=Time.frameCount;
            if(r.TapStage==0){QueueKeys(r,AbilityBindings.GetKey(first));r.StageFrame=frame;r.TapStage=1;return false;}
            if(frame-r.StageFrame<2)return false;
            if(r.TapStage==1){QueueKeys(r,Key.None);r.StageFrame=frame;r.TapStage=2;return false;}
            if(r.TapStage==2){QueueKeys(r,AbilityBindings.GetKey(second));r.StageFrame=frame;r.TapStage=3;return false;}
            if(r.TapStage==3){QueueKeys(r,Key.None);r.StageFrame=frame;r.TapStage=4;return false;}
            if(!r.Combat.HasConfirmedSelection||r.Combat.ConfirmedMask!=expected)
            {Finish("FAILED","Bound virtual keyboard taps did not produce the expected ordinary confirmed selection.");return false;}
            return true;
        }
        static bool TryManualCast(Run r,Vector3 point,int mask)
        {
            Transform camera=r.Owner.Player.ViewCamera.transform;Vector3 delta=point-camera.position;
            if(delta.sqrMagnitude<.01f)return false;
            int element=r.Combat.ConfirmedElement;float before=r.Combat.CooldownRemaining[element];Quaternion saved=camera.rotation;
            r.Report.manualCastAttempts++;
            try{camera.rotation=Quaternion.LookRotation(delta,Vector3.up);r.Combat.ManualCast();}
            finally{camera.rotation=saved;}
            bool accepted=r.Combat.CooldownRemaining[element]>before+.5f;
            if(accepted){r.Report.acceptedCasts++;r.Report.actions.Add("Public ManualCast accepted mask"+mask+" at game"+Time.timeAsDouble.ToString("F3"));}
            return accepted;
        }
        static bool PlaceNovaApproach(Run r)
        {
            foreach(Vector3 p in new[]{new Vector3(0,.2f,6.5f),new Vector3(2.8f,.2f,6.5f),new Vector3(-2.8f,.2f,6.5f),new Vector3(0,.2f,5f)})
                if(TryTeleport(r,p,0))return true;
            return false;
        }
        static bool TryTeleport(Run r,Vector3 position,float yaw)
        {
            if(RewardsNear(r,position,4.8f))return false;
            int count=Physics.RaycastNonAlloc(position+Vector3.up*2,Vector3.down,r.Hits,4,~(1<<2),QueryTriggerInteraction.Ignore);
            if(count==r.Hits.Length)return false;float nearest=float.PositiveInfinity;bool floor=false;
            for(int i=0;i<count;i++)
            {
                Collider c=r.Hits[i].collider;if(c==null||c.GetComponentInParent<OraclePlayer>()!=null||c.GetComponentInParent<OracleEnemy>()!=null||c.GetComponentInParent<OracleProjectile>()!=null||r.Hits[i].distance>=nearest)continue;
                nearest=r.Hits[i].distance;floor=r.Hits[i].normal.y>.9f&&Mathf.Abs(r.Hits[i].point.y)<.1f;
            }
            if(!floor)return false;
            CharacterController cc=r.Owner.Player.GetComponent<CharacterController>();if(cc==null)return false;
            Vector3 centre=position+cc.center;float shaft=Mathf.Max(0,cc.height*.5f-cc.radius);
            count=Physics.OverlapCapsuleNonAlloc(centre+Vector3.up*shaft,centre-Vector3.up*shaft,cc.radius+.02f,r.Overlaps,~(1<<2),QueryTriggerInteraction.Ignore);
            if(count==r.Overlaps.Length)return false;
            for(int i=0;i<count;i++){Collider c=r.Overlaps[i];if(c!=null&&c.GetComponentInParent<OraclePlayer>()!=r.Owner.Player)return false;}
            r.Owner.Player.Teleport(position,yaw);r.Report.clearApproachTeleports++;
            r.Report.actions.Add("Public Teleport clear approach "+position+"; excluded from movement/travel acceptance.");return true;
        }
        static bool RewardsNear(Run r,Vector3 position,float distance)
        {
            Vector3 collector=position+Vector3.up*.85f;float squared=distance*distance;
            foreach(var pickup in Object.FindObjectsByType<HeartcrystalPickup>())if(pickup.gameObject.scene==r.Owner.gameObject.scene&&
                (pickup.transform.position-collector).sqrMagnitude<squared)return true;
            foreach(var pickup in Object.FindObjectsByType<XpPickup>())if(pickup.gameObject.scene==r.Owner.gameObject.scene&&
                (pickup.transform.position-collector).sqrMagnitude<squared)return true;
            return false;
        }
        static void QueueKeys(Run r,Key first,Key second=Key.None)
        {
            if(r.Keyboard==null||!r.Keyboard.added||r.HeldA==first&&r.HeldB==second)return;
            r.HeldA=first;r.HeldB=second;
            Key[] keys=first==Key.None?NoKeys:second==Key.None?new[]{first}:new[]{first,second};
            InputSystem.QueueStateEvent(r.Keyboard,new KeyboardState(keys));r.Report.virtualInputEvents++;
        }
        static void OnNovaResolved(Vector3 point,int damaged)
        {
            var r=active;if(r==null||r.Tutorial.CurrentLesson!=TutorialDirector.Lesson.NovaPractice||!r.Combat.CosmicNovaTrialActive)return;
            r.Report.novaImpactEvents++;r.Report.novaDamagedTargets=damaged;r.Report.novaAliveAtImpact=r.Combat.CosmicNovaTrialAliveCount;
            r.Report.novaAcceptedAtImpact=r.Combat.OwnCosmicNovaCastCount-r.InitialNova;
            Check(r,"actual_five_target_impact",damaged==5&&r.Report.novaAliveAtImpact==0&&r.Report.novaAcceptedAtImpact==1&&r.Combat.CosmicNovaResolvedCount==r.InitialResolutions+1,"Real projectile contact/AoE event; no direct resolver or HP write.");
            Observe(r,"actual_nova_impact");
        }
        static bool Check(Run r,string name,bool passed,string detail)
        {
            r.Report.assertions++;r.Report.checks.Add(new CheckRecord{name=name,passed=passed,detail=detail});
            if(!passed){r.Report.failures++;Finish("FAILED",name+": "+detail);}return passed;
        }
        static void Observe(Run r,string label)
        {
            if(r.Report.observations.Count>=48)return;var t=r.Tutorial;var c=r.Combat;
            r.Report.observations.Add(new Observation{stage=label,heading=t.Heading,instruction=t.Instruction,frame=Time.frameCount,
                guideIndex=t.CurrentGuideIndex,highlight=t.GuideHighlightElement,confirmedMask=c.ConfirmedMask,acceptedMask=c.NovaIngredientMask,
                novaCount=c.OwnCosmicNovaCastCount,resolved=c.CosmicNovaResolvedCount,trialAlive=c.CosmicNovaTrialAliveCount,kills=r.Owner.Defeated,
                wallSeconds=EditorApplication.timeSinceStartup-r.StartedWall,gameSeconds=Time.timeAsDouble-r.StartedGame,
                position=r.Owner.Player.transform.position,paused=r.Owner.Paused,speaking=t.IsSpeaking,wandVisible=t.WandPreviewVisible});
        }
        static float CaptionDuration(string line)=>Mathf.Max(6,line.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Length/2.35f);
        static float Duration(string resource,string caption){var clip=Resources.Load<AudioClip>(resource);return clip!=null?clip.length+.7f:CaptionDuration(caption);}
        static void Finish(string status,string reason)
        {
            var r=active;if(r==null)return;active=null;last=r.Report;
            r.Report.status=status;r.Report.reason=reason;r.Report.endedUtc=DateTime.UtcNow.ToString("o");
            r.Report.wallSeconds=EditorApplication.timeSinceStartup-r.StartedWall;r.Report.gameSeconds=Time.timeAsDouble-r.StartedGame;
            EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload-=OnReload;EditorApplication.quitting-=OnQuit;
            r.Report.callbacksDetached=true;
            if(r.Combat!=null)r.Combat.CosmicNovaResolved-=OnNovaResolved;
            try
            {
                if(r.Owner!=null&&ReferenceEquals(r.Owner,GameSession.Instance))
                {
                    r.Report.profileUnchanged=r.Profile.Same(r.Owner);
                    if(r.Owner.HasStarted)r.Owner.Pause();r.Report.leftPaused=r.Owner.Paused;
                }
            }
            catch(Exception exception){r.Report.status="FAILED";r.Report.failures++;r.Report.reason+=" Cleanup observation: "+exception.Message;}
            finally
            {
                try{if(r.Keyboard!=null&&r.Keyboard.added)InputSystem.RemoveDevice(r.Keyboard);}
                catch(Exception exception){r.Report.status="FAILED";r.Report.failures++;r.Report.reason+=" Keyboard removal: "+exception.Message;}
                finally
                {
                    r.Report.keyboardRemoved=r.Keyboard==null||!r.Keyboard.added;
                    if(r.PreviousKeyboard!=null&&r.PreviousKeyboard.added)r.PreviousKeyboard.MakeCurrent();
                    r.Report.previousKeyboardRestored=r.PreviousKeyboard==null?Keyboard.current==null:r.PreviousKeyboard.added&&Keyboard.current==r.PreviousKeyboard;
                }
                if(r.Report.status=="PASSED"&&(!r.Report.profileUnchanged||!r.Report.keyboardRemoved||!r.Report.previousKeyboardRestored||!r.Report.leftPaused))
                {r.Report.status="FAILED";r.Report.failures++;r.Report.reason+=" Final profile/device/pause cleanup assertion failed.";}
                Save(r.Report);
            }
        }
        static string Reject(Receipt report,string reason,string status="REJECTED"){report.status=status;report.reason=reason;report.endedUtc=DateTime.UtcNow.ToString("o");Save(report);return JsonUtility.ToJson(report,true);}
        static string ValidPath(string path)
        {
            if(string.IsNullOrWhiteSpace(path))return "";string full=Path.GetFullPath(path);
            string project=Directory.GetParent(Application.dataPath).FullName;string workspace=Directory.GetParent(Directory.GetParent(project).FullName).FullName;
            string allowed=Path.GetFullPath(Path.Combine(workspace,"work"))+Path.DirectorySeparatorChar;
            if(!full.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||!full.EndsWith(".json",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Receipt must be a JSON file inside this workspace's work folder.");
            return full;
        }
        static void Save(Receipt report){if(string.IsNullOrEmpty(report.path))return;Directory.CreateDirectory(Path.GetDirectoryName(report.path));File.WriteAllText(report.path,JsonUtility.ToJson(report,true));}
        static void OnPlayState(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredPlayMode)Finish("CANCELLED","Play mode changed.");}
        static void OnReload()=>Finish("CANCELLED","Source reload requested; driver detached.");
        static void OnQuit()=>Finish("CANCELLED","Editor quit requested; driver detached.");
    }
}
#endif
