#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OdysseyOracle.Editor
{
    // Root-run production lesson check. It never starts/skips a lesson, writes
    // ready bits or HP, calls a damage resolver, teleports, resumes or takes OS input.
    public static class OracleRapid150TutorialNativeAcceptance
    {
        enum Phase { Warmup, HoldThree, WaitImpact, WaitRealHold }
        [Serializable] public sealed class Check { public string name,detail; public bool passed; }
        [Serializable] public sealed class Receipt
        {
            public string status="NOT_STARTED",reason="",path="",startedUtc="",endedUtc="";
            public string profileObservationScope="Runtime level/XP/points/passive mask/wallet/permanent training/wand levels; no full filesystem or PlayerPrefs audit.";
            public bool nativeExecuted,actualBrowserExecuted,networkExecuted,osInputRequested;
            public bool privateStateWritesRequested,profileWritesRequested,callbacksDetached;
            public bool manualThreeBlocked,autoThreeBlocked,directFourAccepted,allFourFiveSeconds;
            public bool realPositiveImpact,realTutorialHold,profileUnchanged;
            public int assertions,failures,distinctFrames,manualCastCalls,impactEvents,damagedTargets;
            public double threeStartedAt,acceptedAt,impactAt,echoAt;
            public List<Check> checks=new List<Check>(24);
            public TutorialDirector.Rapid150Readback lastLesson;
        }
        sealed class Run
        {
            internal GameSession Owner; internal TutorialDirector Tutorial; internal OracleCombat Combat;
            internal Receipt Report; internal Phase Phase; internal int Frame,LastFrame=-1,InitialCasts,InitialResolutions;
            internal int Level,Xp,Points,Mask,Wallet,Permanent,Wand0,Wand1,Wand2,Wand3;
            internal double Wall,LastClockWall,LastGame; internal Vector3 Position;
        }
        static Run active;
        static Receipt last=new Receipt();
        public static bool IsRunning=>active!=null;
        public static string CurrentReceiptJson()=>JsonUtility.ToJson(active!=null?active.Report:last,true);

        public static string ObserveCurrent()
        {
            var game=GameSession.Instance;
            if(!EditorApplication.isPlaying||game==null||game.Tutorial==null)
                return "{\"status\":\"NO_ACTUAL_PLAY_TUTORIAL\"}";
            return JsonUtility.ToJson(game.Tutorial.CaptureRapid150Readback(),true);
        }

        public static string Begin(GameSession owner,string absoluteReceiptPath)
        {
            if(active!=null)return CurrentReceiptJson();
            var report=new Receipt{path=ValidPath(absoluteReceiptPath),startedUtc=DateTime.UtcNow.ToString("o")};last=report;
            if(!EditorApplication.isPlaying||EditorApplication.isPaused||EditorApplication.isCompiling||
                owner==null||!ReferenceEquals(owner,GameSession.Instance)||owner.Tutorial==null||owner.Combat==null||
                owner.Player==null||owner.Player.ViewCamera==null||owner.Progression==null||owner.Shop==null||
                !owner.IsPlaying||!Application.isFocused||owner.CoopMode||
                owner.Rooms!=null&&(owner.Rooms.Busy||owner.Rooms.Connected)||
                owner.Tutorial.CurrentLesson!=TutorialDirector.Lesson.NovaPractice||
                !owner.Combat.CosmicNovaTrialActive||owner.Combat.CosmicNovaTrialAliveCount!=5||
                owner.Combat.AutoFireEnabled||owner.Combat.ConfirmedMask!=0||
                owner.Combat.ActiveGameplayProjectileCount!=0||!Ready(owner.Combat)||
                AbilityBindings.IsRebinding||owner.Builder!=null&&owner.Builder.IsPlacing)
                return Reject(report,"Requires the actual newly entered offline five-target NovaPractice lesson, standing still with empty selection/projectile pool, ready ingredients and auto off. No setup or lesson skip is performed.");
            var state=owner.Tutorial.CaptureRapid150Readback();
            if(!state.Active||!state.NovaTrialOwned||state.NovaImpactAccepted)
                return Reject(report,"The actual tutorial must own its untouched trial and have no accepted impact.");
            var run=new Run{Owner=owner,Tutorial=owner.Tutorial,Combat=owner.Combat,Report=report,Phase=Phase.Warmup,
                InitialCasts=owner.Combat.OwnCosmicNovaCastCount,InitialResolutions=owner.Combat.CosmicNovaResolvedCount,
                Frame=Time.frameCount,Wall=EditorApplication.timeSinceStartup,LastClockWall=EditorApplication.timeSinceStartup,
                LastGame=Time.timeAsDouble,Position=owner.Player.transform.position,
                Level=owner.Progression.Level,Xp=owner.Progression.Experience,Points=owner.Progression.SkillPoints,
                Mask=owner.Progression.UpgradeMask,Wallet=owner.Shop.Heartcrystals,Permanent=owner.Shop.PermanentPassiveMask,
                Wand0=owner.Shop.WandLevel(0),Wand1=owner.Shop.WandLevel(1),Wand2=owner.Shop.WandLevel(2),Wand3=owner.Shop.WandLevel(3)};
            active=run;report.status="RUNNING";report.nativeExecuted=true;
            run.Combat.CosmicNovaResolved+=OnImpact;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
            AssemblyReloadEvents.beforeAssemblyReload+=OnReload;EditorApplication.quitting+=OnQuit;
            Save(report);return CurrentReceiptJson();
        }
        public static string Cancel(){Finish("CANCELLED","Root/user canceled; tutorial trial is retained by its original owner.");return CurrentReceiptJson();}
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("INCOMPLETE","Play exited.");}
        static void OnReload(){Finish("INCOMPLETE","Assembly reload.");}
        static void OnQuit(){Finish("INCOMPLETE","Editor quit.");}
        static void Tick()
        {
            var run=active;if(run==null)return;
            try{Advance(run);}catch(Exception error){Finish("INCOMPLETE",error.GetType().Name+": "+error.Message);}
        }
        static void Advance(Run r)
        {
            var owner=r.Owner;var combat=r.Combat;var tutorial=r.Tutorial;var report=r.Report;
            double wall=EditorApplication.timeSinceStartup,now=Time.timeAsDouble;
            if(!EditorApplication.isPlaying||EditorApplication.isPaused||EditorApplication.isCompiling||
                owner==null||combat==null||tutorial==null||!ReferenceEquals(owner,GameSession.Instance)||
                owner.Tutorial!=tutorial||owner.Combat!=combat||!Application.isFocused||owner.Paused||
                owner.CoopMode||owner.IsDead||owner.Victory||owner.IsDowned||
                owner.Rooms!=null&&(owner.Rooms.Busy||owner.Rooms.Connected))
            {Finish("INCOMPLETE","Actual Play/focus/session/trial ownership changed; nothing is resumed or forced back.");return;}
            if(wall-r.Wall>45d){Finish("INCOMPLETE","45-second bounded observation expired.");return;}
            if(now>r.LastGame){r.LastGame=now;r.LastClockWall=wall;}
            else if(wall-r.LastClockWall>3d){Finish("INCOMPLETE","Actual game clock stalled.");return;}
            if(!SameProfile(r)){Finish("FAILED","Profile/run reward state changed; no profile restoration is attempted.");return;}
            if(Vector3.Distance(owner.Player.transform.position,r.Position)>.08f)
            {Finish("INCOMPLETE","Player moved during stationary test; no movement input is overridden.");return;}
            if(Time.frameCount==r.LastFrame)return;r.LastFrame=Time.frameCount;report.distinctFrames++;
            report.lastLesson=tutorial.CaptureRapid150Readback();
            if(r.Phase==Phase.WaitRealHold&&tutorial.CurrentLesson==TutorialDirector.Lesson.TrainingEcho)
            {
                report.echoAt=now;
                if(!CheckIt(r,"real_positive_impact_hold",report.realPositiveImpact&&now-report.impactAt>=1.20d,
                    "Production tutorial advanced through its own positive-impact gate and 1.25-second advancing hold."))return;
                report.realTutorialHold=true;Finish("PASSED","Actual production input/collision/event/lesson flow observed.");return;
            }
            if(tutorial.CurrentLesson!=TutorialDirector.Lesson.NovaPractice||!combat.CosmicNovaTrialActive)
            {Finish("INCOMPLETE","The tutorial left its owned NovaPractice before the required observation.");return;}
            if(r.Phase==Phase.Warmup)
            {
                if(Time.frameCount-r.Frame<2)return;
                if(!CheckIt(r,"live_numeric_contract",OracleCombat.PrimaryCadenceSeconds==.5f&&
                    OracleCombat.AbilityCooldownSeconds==4f&&OracleCombat.SuperAbilityCooldownSeconds==5f,
                    "Actual compiled combat constants are .5/4/5 seconds."))return;
                bool selected=combat.TrySelectElement(0)&&combat.TrySelectElement(1)&&combat.TrySelectElement(2);
                combat.ManualCast();report.manualCastCalls++;
                report.manualThreeBlocked=selected&&combat.ConfirmedMask==7&&!combat.HasConfirmedSelection&&
                    combat.OwnCosmicNovaCastCount==r.InitialCasts&&combat.CosmicNovaResolvedCount==r.InitialResolutions&&
                    combat.ActiveGameplayProjectileCount==0&&Ready(combat);
                if(!CheckIt(r,"manual_three_blocked_without_spend",report.manualThreeBlocked,
                    "Three real selected bits remain highlighted; ManualCast launches nothing and spends no recovery."))return;
                combat.SetAutoFire(true);report.threeStartedAt=now;r.Phase=Phase.HoldThree;return;
            }
            if(r.Phase==Phase.HoldThree)
            {
                if(combat.ConfirmedMask!=7){Finish("INCOMPLETE","Selection changed during incomplete-three observation.");return;}
                if(now-report.threeStartedAt<.60d)return;
                report.autoThreeBlocked=combat.OwnCosmicNovaCastCount==r.InitialCasts&&
                    combat.CosmicNovaResolvedCount==r.InitialResolutions&&combat.ActiveGameplayProjectileCount==0&&Ready(combat);
                if(!CheckIt(r,"auto_three_blocked_after_settle",report.autoThreeBlocked,
                    "Actual auto-fire ran beyond its .5-second wait while three selected bits spent nothing."))return;
                combat.SetAutoFire(false);
                if(!CheckIt(r,"fourth_completes_direct_recipe",combat.TrySelectElement(3)&&combat.ConfirmedMask==15&&combat.CosmicNovaPrepared,
                    "The fourth selection completes direct Nova with no earlier accepted pairs."))return;
                Transform eye=owner.Player.ViewCamera.transform;Quaternion previous=eye.rotation;
                try
                {
                    Vector3 direction=combat.CosmicNovaTrialAimPoint-eye.position;
                    if(direction.sqrMagnitude<.01f){Finish("INCOMPLETE","No finite target aim distance.");return;}
                    eye.rotation=Quaternion.LookRotation(direction);combat.ManualCast();report.manualCastCalls++;
                }
                finally{eye.rotation=previous;}
                report.directFourAccepted=combat.OwnCosmicNovaCastCount==r.InitialCasts+1&&combat.ActiveGameplayProjectileCount==1;
                report.allFourFiveSeconds=FiveSeconds(combat);
                if(!CheckIt(r,"one_direct_four_launch_five_seconds",report.directFourAccepted&&report.allFourFiveSeconds,
                    "Actual production launch accepts one Nova and commits5s to all four ingredients."))return;
                report.acceptedAt=now;r.Phase=Phase.WaitImpact;return;
            }
            if(r.Phase==Phase.WaitImpact&&report.impactEvents>0)
            {
                if(!CheckIt(r,"actual_positive_owned_trial_impact",report.realPositiveImpact&&report.impactEvents==1,
                    "Production projectile collision/AOE event caused the tutorial's positive-impact gate; no resolver/HP/lesson write."))return;
                r.Phase=Phase.WaitRealHold;return;
            }
            if(r.Phase==Phase.WaitImpact&&now-report.acceptedAt>3d)
                Finish("INCOMPLETE","Accepted Nova has no actual positive impact in the3-second window; check real aim/LOS/collision.");
        }
        static void OnImpact(Vector3 point,int damaged)
        {
            var r=active;if(r==null)return;
            r.Report.impactEvents++;r.Report.damagedTargets=damaged;r.Report.impactAt=Time.timeAsDouble;
            var state=r.Tutorial.CaptureRapid150Readback();
            r.Report.realPositiveImpact=damaged>0&&state.NovaImpactAccepted&&state.NovaTrialOwned&&
                r.Tutorial.CurrentLesson==TutorialDirector.Lesson.NovaPractice&&
                r.Combat.OwnCosmicNovaCastCount==r.InitialCasts+1&&r.Combat.CosmicNovaResolvedCount>r.InitialResolutions;
        }
        static bool Ready(OracleCombat c){for(int i=0;i<4;i++)if(c.CooldownRemaining[i]>.001f||c.ElementSwitchCooldowns[i]>.001f)return false;return true;}
        static bool FiveSeconds(OracleCombat c){for(int i=0;i<4;i++)if(c.CooldownRemaining[i]<4.99f||c.CooldownRemaining[i]>5.001f||c.CooldownDuration[i]!=5f)return false;return true;}
        static bool SameProfile(Run r)=>r.Owner.Progression.Level==r.Level&&r.Owner.Progression.Experience==r.Xp&&
            r.Owner.Progression.SkillPoints==r.Points&&r.Owner.Progression.UpgradeMask==r.Mask&&
            r.Owner.Shop.Heartcrystals==r.Wallet&&r.Owner.Shop.PermanentPassiveMask==r.Permanent&&
            r.Owner.Shop.WandLevel(0)==r.Wand0&&r.Owner.Shop.WandLevel(1)==r.Wand1&&r.Owner.Shop.WandLevel(2)==r.Wand2&&r.Owner.Shop.WandLevel(3)==r.Wand3;
        static bool CheckIt(Run r,string name,bool passed,string detail)
        {r.Report.assertions++;r.Report.checks.Add(new Check{name=name,passed=passed,detail=detail});if(!passed){r.Report.failures++;Finish("FAILED",name);}return passed;}
        static string Reject(Receipt r,string why){r.status="BLOCKED_PRECONDITION";r.reason=why;Save(r);return JsonUtility.ToJson(r,true);}
        static void Finish(string status,string reason)
        {
            var r=active;active=null;if(r==null)return;
            EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlay;
            AssemblyReloadEvents.beforeAssemblyReload-=OnReload;EditorApplication.quitting-=OnQuit;
            if(r.Combat!=null)r.Combat.CosmicNovaResolved-=OnImpact;
            if(r.Owner!=null&&ReferenceEquals(r.Owner,GameSession.Instance)&&r.Owner.Combat==r.Combat&&r.Combat!=null&&r.Combat.AutoFireEnabled)
                r.Combat.SetAutoFire(false);
            r.Report.callbacksDetached=true;r.Report.profileUnchanged=r.Owner!=null&&SameProfile(r);
            r.Report.status=status;r.Report.reason=reason;r.Report.endedUtc=DateTime.UtcNow.ToString("o");
            last=r.Report;Save(last);
        }
        static string ValidPath(string value)
        {
            if(string.IsNullOrEmpty(value)||!Path.IsPathRooted(value))throw new ArgumentException("An absolute receipt path under this workspace's work directory is required.");
            string path=Path.GetFullPath(value),work=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work"))+Path.DirectorySeparatorChar;
            if(!path.StartsWith(work,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".json",StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Receipt must be JSON under the existing workspace work directory.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));return path;
        }
        static void Save(Receipt r){if(!string.IsNullOrEmpty(r.path))File.WriteAllText(r.path,JsonUtility.ToJson(r,true));}
    }
}
#endif

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: real existing offline trial/player/target world metres; no position or OS input writes.
// Array Formats: bounded Editor receipt/check list; borrows four recovery slots and the owned tutorial readback.
// Dependencies/Pointers: real public selection/cast APIs and projectile-impact event; tutorial alone owns actors/hold/transition, no fabricated completion.
