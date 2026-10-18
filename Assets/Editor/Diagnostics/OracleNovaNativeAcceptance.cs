#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OdysseyOracle.Editor
{
    /// <summary>Root-run actual five-target trial and fixed-pool failure/retry test. No OS input or profile writes.</summary>
    public static class OracleNovaNativeAcceptance
    {
        // # ARCHITECTURE SNAPSHOT
        // One bounded Editor update observer drives public confirmed-selection
        // and manual-cast APIs in a fresh disposable offline trial. Positions
        // are metres; recovery/chain use the actual gameplay clock. It owns no
        // NativeArray, transport or private reflection and never writes ready bits.
        // EndTrial clears the test encounter; restart Play for normal training.
        enum Phase { Warmup, WaitSecondPair, WaitFreeSlot, WaitImpact }
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Observation
        {
            public string label;
            public double gameTime;
            public int frame, activeProjectiles, confirmedMask, ingredientMask, ownNovaCasts, resolved, alive, kills;
            public float chainSeconds;
            public float[] recovery = new float[4];
        }
        [Serializable] public sealed class Receipt
        {
            public string status="NOT_STARTED", reason="", path, startedUtc, endedUtc;
            public bool nativeExecuted, networkExecuted, profileWritesRequested, osInputRequested, callbacksDetached;
            public bool trialEnded, wardRestored, pausedRestored, autoFireRestored, profileUnchanged;
            public bool disposableEncounterRequiresRestart=true;
            public int assertions, failures, editorTicks, distinctFrames, impactEvents, eventDamagedTargets, eventKills;
            public double firstAcceptedAt, failedAt, retryAt, resolvedAt;
            public Vector3 impactPoint;
            public List<Check> checks=new List<Check>(48);
            public List<Observation> observations=new List<Observation>(12);
        }
        sealed class Profile
        {
            internal int Level,Xp,Points,Mask,Wallet,Permanent,ClaimXp,ClaimCrystal;
            internal bool HasXp,HasCrystal;
            internal readonly int[] Wands=new int[4];
            internal Profile(GameSession owner)
            {
                Level=owner.Progression.Level;Xp=owner.Progression.Experience;Points=owner.Progression.SkillPoints;Mask=owner.Progression.UpgradeMask;
                Wallet=owner.Shop!=null?owner.Shop.Heartcrystals:0;Permanent=owner.Shop!=null?owner.Shop.PermanentPassiveMask:0;
                for(int i=0;i<4;i++)Wands[i]=owner.Shop!=null?owner.Shop.WandLevel(i):1;
                HasXp=PlayerPrefs.HasKey(ElementProgression.TutorialXpClaimKey);ClaimXp=PlayerPrefs.GetInt(ElementProgression.TutorialXpClaimKey,0);
                HasCrystal=PlayerPrefs.HasKey(ElementProgression.TutorialHeartstoneClaimKey);ClaimCrystal=PlayerPrefs.GetInt(ElementProgression.TutorialHeartstoneClaimKey,0);
            }
            internal bool Same(GameSession owner)
            {
                var now=new Profile(owner);
                if(Level!=now.Level||Xp!=now.Xp||Points!=now.Points||Mask!=now.Mask||Wallet!=now.Wallet||Permanent!=now.Permanent||
                    HasXp!=now.HasXp||ClaimXp!=now.ClaimXp||HasCrystal!=now.HasCrystal||ClaimCrystal!=now.ClaimCrystal)return false;
                for(int i=0;i<4;i++)if(Wands[i]!=now.Wands[i])return false;return true;
            }
            internal bool DefaultPower{get{if(Level!=1||Xp!=0||Points!=0||Mask!=0||Permanent!=0)return false;for(int i=0;i<4;i++)if(Wands[i]!=1)return false;return true;}}
        }
        sealed class Run
        {
            internal GameSession Owner;
            internal OracleCombat Combat;
            internal OracleShrine Ward;
            internal Profile Profile;
            internal Receipt Report;
            internal Phase Phase;
            internal bool OldWard,OldPaused,OldAuto;
            internal int OldGoal,OldKills,OriginalFrame,LastFrame=-1;
            internal string OldObjective;
            internal double StartedWall,LastProgressWall,LastGame;
            internal int FirstMask;
        }
        static Run active;
        static Receipt last=new Receipt();
        public static bool IsRunning=>active!=null;
        public static string CurrentReceiptJson()=>JsonUtility.ToJson(active!=null?active.Report:last,true);

        public static string Begin(GameSession owner,string receiptPath)
        {
            if(active!=null)return CurrentReceiptJson();
            var report=new Receipt{status="RUNNING",path=ValidPath(receiptPath),startedUtc=DateTime.UtcNow.ToString("o")};last=report;
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isPaused||owner==null||!ReferenceEquals(owner,GameSession.Instance)||
                owner.Combat==null||owner.Player==null||owner.Progression==null||owner.World==null||owner.World.IsCoopArena||owner.CoopMode||
                owner.Rooms!=null&&(owner.Rooms.Connected||owner.Rooms.Busy)||owner.Defense!=null&&owner.Defense.Running||
                owner.Tutorial==null||!owner.Tutorial.Finished||!owner.IsPlaying||!owner.CombatUnlocked||AbilityBindings.IsRebinding||
                owner.Builder!=null&&owner.Builder.IsPlacing)
                return Reject(report,"Requires stable offline Play, finished tutorial, active gameplay, no room, rebind, placement or arena. Fixture never starts/leaves a room or skips training.");
            var profile=new Profile(owner);
            if(!profile.DefaultPower||owner.Goal!=0||owner.Defeated!=0||HasLiving(owner.Combat)||HasRewards()||
                owner.Combat.CosmicNovaTrialActive||owner.Combat.ActiveGameplayProjectileCount!=0||owner.Combat.AutoFireEnabled||!AllReady(owner.Combat))
                return Reject(report,"Use a fresh disposable empty default-power encounter (level1, no passive/wand upgrades, zero goal/kills/rewards/projectiles, auto off). No profile is reset by this fixture.");
            Vector3 camera=owner.Player.ViewCamera.transform.position;
            if(Vector3.Distance(camera,new Vector3(0,1.2f,14.2f))>10f||camera.z>12f)
                return Reject(report,"Root must position the player in the clear central hall within10m of (0,1.2,14.2), facing the trial. No OS input is requested.");
            var run=new Run{Owner=owner,Combat=owner.Combat,Ward=owner.Combat.Shrine,Profile=profile,Report=report,Phase=Phase.Warmup,
                OldWard=owner.Combat.Shrine!=null&&owner.Combat.Shrine.enabled,OldPaused=owner.Paused,OldAuto=owner.Combat.AutoFireEnabled,
                OldGoal=owner.Goal,OldKills=owner.Defeated,OldObjective=owner.Objective,OriginalFrame=Time.frameCount,
                StartedWall=EditorApplication.timeSinceStartup,LastProgressWall=EditorApplication.timeSinceStartup,LastGame=Time.timeAsDouble};
            active=run;
            try
            {
                run.Combat.SetAutoFire(false);
                if(!run.Combat.BeginCosmicNovaTrial()){Finish("INCOMPLETE","Public trial rejected setup.");return CurrentReceiptJson();}
                run.Combat.CosmicNovaResolved+=OnResolved;
                if(!Assert(run,"actual_five_trial_targets",run.Combat.CosmicNovaTrialAliveCount==5&&owner.Goal==5,"Production trial admitted five real reward-free enemies."))return CurrentReceiptJson();
                if(!Assert(run,"trial_suspends_autonomous_ward",run.Ward==null||!run.Ward.enabled,"Production trial suspends its own ward; fixture does not pre-disable it."))return CurrentReceiptJson();
                Observe(run,"trial_started");Attach();Save(report);return CurrentReceiptJson();
            }
            catch(Exception error){Finish("INCOMPLETE",error.GetType().Name+": "+error.Message);return CurrentReceiptJson();}
        }
        public static string Cancel(){if(active!=null)Finish("INCOMPLETE","Cancelled explicitly.");return CurrentReceiptJson();}
        static void Attach(){Detach();EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;AssemblyReloadEvents.beforeAssemblyReload+=OnReload;EditorApplication.quitting+=OnQuit;}
        static void Detach(){EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlay;AssemblyReloadEvents.beforeAssemblyReload-=OnReload;EditorApplication.quitting-=OnQuit;}
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("INCOMPLETE","Play exited.");}
        static void OnReload(){Finish("INCOMPLETE","Assembly reload.");}
        static void OnQuit(){Finish("INCOMPLETE","Editor quit.");}
        static void Tick(){if(active==null){Detach();return;}try{Advance(active);}catch(Exception error){Finish("INCOMPLETE",error.GetType().Name+": "+error.Message);}}
        static void Advance(Run run)
        {
            var owner=run.Owner;var combat=run.Combat;var report=run.Report;report.editorTicks++;
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isPaused||owner==null||combat==null||!ReferenceEquals(owner,GameSession.Instance)||owner.Combat!=combat||!owner.IsPlaying||!combat.CosmicNovaTrialActive||owner.CoopMode||owner.Rooms!=null&&(owner.Rooms.Connected||owner.Rooms.Busy))
            {Finish("INCOMPLETE","Actual session, trial, gameplay or offline lifetime changed.");return;}
            double now=Time.timeAsDouble,wall=EditorApplication.timeSinceStartup;
            if(now>run.LastGame){run.LastGame=now;run.LastProgressWall=wall;}
            if(wall-run.StartedWall>20d||wall-run.LastProgressWall>2d){Finish("INCOMPLETE","Bounded clock stall/wall timeout.");return;}
            if(!run.Profile.Same(owner)||HasRewards()){Finish("FAILED","A reward/profile value changed or a forbidden trial drop appeared; fixture never restores a profile.");return;}
            if(run.LastFrame==Time.frameCount)return;run.LastFrame=Time.frameCount;report.distinctFrames++;
            if(run.Phase==Phase.Warmup)
            {
                if(Time.frameCount-run.OriginalFrame<3)return;
                if(!Assert(run,"first_pair_confirms_without_charge",ConfirmPair(combat,0,1)&&combat.ConfirmedMask==3&&AllReady(combat)&&combat.NovaIngredientMask==0,"Real preparation/confirmation writes no recovery or ingredient progress."))return;
                AimAndCast(run);
                if(!Assert(run,"first_pair_accepted",combat.NovaIngredientMask==3&&combat.OwnCosmicNovaCastCount==0&&NearFour(combat.ElementSwitchCooldowns[0])&&NearFour(combat.ElementSwitchCooldowns[1])&&combat.ElementSwitchCooldowns[2]==0&&combat.ElementSwitchCooldowns[3]==0,"Actual manual Fire/Water pair contributes two ingredients and charges only its used slots."))return;
                report.firstAcceptedAt=now;run.FirstMask=3;report.nativeExecuted=true;Observe(run,"first_pair_accepted");
                run.Phase=Phase.WaitSecondPair;return;
            }
            if(run.Phase==Phase.WaitSecondPair)
            {
                if(now-report.firstAcceptedAt<1.05d)return;
                if(now-report.firstAcceptedAt>1.6d){Finish("INCOMPLETE","Missed bounded global-cadence observation window.");return;}
                if(!Assert(run,"second_pair_truthful_ready",ConfirmPair(combat,2,3)&&combat.ConfirmedMask==12&&combat.NovaIngredientMask==run.FirstMask&&combat.CosmicNovaPrepared,"Actual disjoint Earth/Air pair predicts the next accepted finisher, without spending it."))return;
                FillOrdinaryPool(run);
                if(!Assert(run,"all_forty_eight_slots_busy",combat.ActiveGameplayProjectileCount==48,"48 actual ordinary launches occur in one callback; no frame or pool-state write is fabricated."))return;
                var before=Observe(run,"before_failed_full_pool_nova");AimAndCast(run);var after=Observe(run,"after_failed_full_pool_nova");
                if(!Assert(run,"failed_nova_consumes_nothing",SameRecovery(before,after)&&before.ingredientMask==after.ingredientMask&&before.ownNovaCasts==after.ownNovaCasts&&before.activeProjectiles==after.activeProjectiles&&before.chainSeconds==after.chainSeconds&&combat.CosmicNovaPrepared,"Actual full-pool finisher failure leaves accepted chain, used-slot clocks and own Nova count unchanged."))return;
                report.failedAt=now;run.Phase=Phase.WaitFreeSlot;return;
            }
            if(run.Phase==Phase.WaitFreeSlot)
            {
                if(combat.NovaChainSeconds<=0){Finish("INCOMPLETE","Natural pool retirement did not arrive before the real chain expired.");return;}
                if(combat.ActiveGameplayProjectileCount>=48)return;
                AimAndCast(run);
                if(!Assert(run,"retry_accepts_one_nova",combat.OwnCosmicNovaCastCount==1&&combat.NovaIngredientMask==0&&AllNearFour(combat),"Retry after natural projectile retirement accepts exactly one Nova and charges all four slots for4s."))return;
                report.retryAt=now;Observe(run,"nova_retry_accepted");
                FillOrdinaryPool(run); // One extra ordinary reuse must preserve the accepted Nova seed.
                if(!Assert(run,"ordinary_reuse_keeps_nova",combat.ActiveGameplayProjectileCount==48&&combat.OwnCosmicNovaCastCount==1,"Actual ordinary pool reuse fills all slots while the accepted finisher remains scheduled for impact."))return;
                run.Phase=Phase.WaitImpact;return;
            }
            if(run.Phase==Phase.WaitImpact)
            {
                if(report.impactEvents==0)
                {if(now-report.retryAt>1.25d)Finish("FAILED","Accepted protected Nova seed did not produce its actual impact event in the expected travel window.");return;}
                if(!Assert(run,"one_actual_five_target_impact",report.impactEvents==1&&report.eventDamagedTargets==5&&combat.LastNovaDamagedTargets==5&&combat.CosmicNovaResolvedCount==1,"Actual production collision/LOS/AOE path reports five damaged targets once; no resolver is called directly."))return;
                if(!Assert(run,"five_target_practice_completes_after_impact",combat.CosmicNovaTrialAliveCount==0&&combat.OwnCosmicNovaCastCount==1&&owner.Defeated==0&&owner.Goal==5&&report.eventKills==0&&!owner.Victory,"Five actual deaths complete practice; normal kill/portal progression stays unchanged. The guide consumes the actual resolved event and alive count."))return;
                if(!Assert(run,"trial_has_no_reward",run.Profile.Same(owner)&&!HasRewards(),"Five trial kills create no XP/Heartstone objects and preserve profile/run counters outside the transient objective."))return;
                Observe(run,"five_targets_resolved");Finish("PASSED","");
            }
        }
        static void FillOrdinaryPool(Run run)
        {
            Vector3 safeOrigin=run.Owner.Player.ViewCamera.transform.position+Vector3.up*20f;
            for(int i=0;i<48;i++)if(!run.Combat.AutoCastFrom(safeOrigin,0,1f,Vector3.up,0,0,false,0))
                throw new InvalidOperationException("Ordinary public pool fill rejected slot "+i+".");
        }
        static bool ConfirmPair(OracleCombat combat,int a,int b)=>combat.TrySelectElement(a)&&combat.TrySelectElement(b);
        static void AimAndCast(Run run)
        {
            Transform camera=run.Owner.Player.ViewCamera.transform;Quaternion old=camera.rotation;
            try{Vector3 direction=run.Combat.CosmicNovaTrialAimPoint-camera.position;if(direction.sqrMagnitude<.01f)throw new InvalidOperationException("Invalid real target direction.");camera.rotation=Quaternion.LookRotation(direction);run.Combat.ManualCast();}
            finally{camera.rotation=old;}
        }
        static void OnResolved(Vector3 point,int damaged)
        {var run=active;if(run==null)return;run.Report.impactEvents++;run.Report.impactPoint=point;run.Report.eventDamagedTargets=damaged;run.Report.eventKills=run.Owner!=null?run.Owner.Defeated:-1;run.Report.resolvedAt=Time.timeAsDouble;}
        static Observation Observe(Run run,string label)
        {
            var combat=run.Combat;var result=new Observation{label=label,gameTime=Time.timeAsDouble,frame=Time.frameCount,activeProjectiles=combat.ActiveGameplayProjectileCount,
                confirmedMask=combat.ConfirmedMask,ingredientMask=combat.NovaIngredientMask,chainSeconds=combat.NovaChainSeconds,ownNovaCasts=combat.OwnCosmicNovaCastCount,
                resolved=combat.CosmicNovaResolvedCount,alive=combat.CosmicNovaTrialAliveCount,kills=run.Owner.Defeated};
            for(int i=0;i<4;i++)result.recovery[i]=combat.ElementSwitchCooldowns[i];
            if(run.Report.observations.Count<16)run.Report.observations.Add(result);return result;
        }
        static bool SameRecovery(Observation a,Observation b){for(int i=0;i<4;i++)if(a.recovery[i]!=b.recovery[i])return false;return true;}
        static bool AllReady(OracleCombat combat){for(int i=0;i<4;i++)if(combat.CooldownRemaining[i]>.001f||combat.ElementSwitchCooldowns[i]>.001f)return false;return true;}
        static bool NearFour(float value)=>value>=3.99f&&value<=4.001f;
        static bool AllNearFour(OracleCombat combat){for(int i=0;i<4;i++)if(!NearFour(combat.ElementSwitchCooldowns[i]))return false;return true;}
        static bool HasLiving(OracleCombat combat){foreach(var enemy in combat.ActiveEnemies)if(enemy!=null&&enemy.Alive)return true;return false;}
        static bool HasRewards()=>Object.FindObjectsByType<XpPickup>().Length!=0||Object.FindObjectsByType<HeartcrystalPickup>().Length!=0;
        static bool Assert(Run run,string name,bool passed,string detail)
        {run.Report.assertions++;if(!passed)run.Report.failures++;run.Report.checks.Add(new Check{name=name,detail=detail,passed=passed});if(!passed)Finish("FAILED",name);return passed;}
        static void Finish(string status,string reason)
        {
            var run=active;Detach();active=null;if(run==null)return;var report=run.Report;report.callbacksDetached=true;
            bool sameOwner=run.Owner!=null&&ReferenceEquals(GameSession.Instance,run.Owner)&&run.Owner.Combat==run.Combat;
            bool ownsTrial=sameOwner&&run.Combat!=null&&run.Combat.CosmicNovaTrialActive&&!run.Owner.CoopMode;
            try
            {
                if(run.Combat!=null)
                {
                    run.Combat.CosmicNovaResolved-=OnResolved;
                    if(ownsTrial){run.Combat.EndCosmicNovaTrial();run.Combat.SetAutoFire(run.OldAuto);}
                    report.trialEnded=!run.Combat.CosmicNovaTrialActive;report.autoFireRestored=run.Combat.AutoFireEnabled==run.OldAuto;
                }
            }
            catch(Exception error){status="INCOMPLETE";reason="Cleanup interrupted: "+error.GetType().Name+": "+error.Message;}
            finally
            {
                // Independent restoration survives a failed EndTrial; never overwrite a replacement session/run.
                if(sameOwner&&run.Ward!=null&&run.Combat!=null&&run.Combat.Shrine==run.Ward)
                {run.Ward.enabled=run.OldWard;report.wardRestored=run.Ward.enabled==run.OldWard;}
                else report.wardRestored=run.Ward==null;
                if(sameOwner)
                {
                    if(ownsTrial){run.Owner.Goal=run.OldGoal;run.Owner.Defeated=run.OldKills;run.Owner.Objective=run.OldObjective;}
                    // Paused was observed, never changed: user/Editor interruptions keep their state.
                    report.pausedRestored=run.Owner.Paused==run.OldPaused;report.profileUnchanged=run.Profile.Same(run.Owner);
                }
            }
            if(status=="PASSED"&&(!report.trialEnded||!report.wardRestored||!report.autoFireRestored||!report.pausedRestored||!report.profileUnchanged))
            {status="FAILED";reason="Cleanup/profile readback mismatch.";report.failures++;}
            report.status=status;report.reason=reason;report.endedUtc=DateTime.UtcNow.ToString("o");last=report;Save(report);
        }
        static string Reject(Receipt report,string reason){report.status="INCOMPLETE";report.reason=reason;report.endedUtc=DateTime.UtcNow.ToString("o");Save(report);return CurrentReceiptJson();}
        static string ValidPath(string path)
        {if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("Explicit work JSON receipt path required.");string full=Path.GetFullPath(path),work=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work"))+Path.DirectorySeparatorChar;if(!full.StartsWith(work,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(full)!=".json")throw new ArgumentException("Receipt must be inside this workspace work directory.");return full;}
        static void Save(Receipt report){Directory.CreateDirectory(Path.GetDirectoryName(report.path));File.WriteAllText(report.path,JsonUtility.ToJson(report,true));}
    }
}
#endif
