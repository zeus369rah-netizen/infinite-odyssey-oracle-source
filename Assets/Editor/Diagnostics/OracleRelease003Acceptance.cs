#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OdysseyOracle.Editor
{
    /// <summary>Explicit root-run native probes. Never starts Play, supplies OS input or writes a profile.</summary>
    public static class OracleRelease003Acceptance
    {
        // # ARCHITECTURE SNAPSHOT
        // Editor-only, cold test boundary. One synchronous owned scene island or
        // one bounded read-only update observer; never both. World coordinates
        // are metres. Only public APIs are used. No private reflection, RPC
        // forgery, profile restoration or claim that DTO tests are transport tests.
        [Serializable] public sealed class Check
        { public string name, detail, status; }
        [Serializable] public sealed class Receipt
        {
            public string status = "NOT_STARTED", reason = "", mode, startedUtc, endedUtc, path;
            public bool nativeExecuted, profileWritesRequested, osInputRequested, networkExecuted, callbacksDetached;
            public bool transientFieldsRestored, ownedObjectsDeactivated, profileReadbackUnchanged;
            public int assertions, failures, incomplete, observedFrames, observedAttacks, observedStrikes;
            public int groundSamples, towerDamageEvents, destroyedTowerCleanupEvents, maximumAlive;
            public double observedGameSeconds;
            public List<Check> checks = new List<Check>(64);
            public CoopDefenseDirector.DangerWireReceipt wire;
        }
        sealed class Profile
        {
            internal int XpExists, XpClaim, CrystalExists, CrystalClaim, Level, Xp, Points, Mask, Wallet, PermanentMask;
            internal int[] Wands = new int[4];
            internal Profile(GameSession owner)
            {
                XpExists = PlayerPrefs.HasKey(ElementProgression.TutorialXpClaimKey) ? 1 : 0;
                XpClaim = PlayerPrefs.GetInt(ElementProgression.TutorialXpClaimKey, 0);
                CrystalExists = PlayerPrefs.HasKey(ElementProgression.TutorialHeartstoneClaimKey) ? 1 : 0;
                CrystalClaim = PlayerPrefs.GetInt(ElementProgression.TutorialHeartstoneClaimKey, 0);
                if (owner.Progression != null)
                { Level=owner.Progression.Level; Xp=owner.Progression.Experience; Points=owner.Progression.SkillPoints; Mask=owner.Progression.UpgradeMask; }
                if (owner.Shop != null)
                { Wallet=owner.Shop.Heartcrystals; PermanentMask=owner.Shop.PermanentPassiveMask; for(int i=0;i<4;i++)Wands[i]=owner.Shop.WandLevel(i); }
            }
            internal bool Same(GameSession owner)
            {
                var next = new Profile(owner);
                if(XpExists!=next.XpExists||XpClaim!=next.XpClaim||CrystalExists!=next.CrystalExists||CrystalClaim!=next.CrystalClaim||
                    Level!=next.Level||Xp!=next.Xp||Points!=next.Points||Mask!=next.Mask||Wallet!=next.Wallet||PermanentMask!=next.PermanentMask)return false;
                for(int i=0;i<4;i++)if(Wands[i]!=next.Wands[i])return false;
                return true;
            }
        }
        sealed class EnemyObservation
        {
            internal OracleEnemy Enemy;
            internal MeshFilter[] Feet;
            internal OracleEnemy.GuardianAttackPhase Phase;
            internal int Sequence;
            internal double WindupAt;
            internal float WindupDuration;
            internal bool SawStart;
        }
        sealed class TowerObservation
        { internal PlacedElementShrine Tower; internal float Health; internal bool AwaitCleanup; internal int DestroyFrame; }
        sealed class Watch
        {
            internal GameSession Owner;
            internal CoopDefenseDirector Defense;
            internal Receipt Report;
            internal string Code;
            internal double StartWall, StartGame, LastGame, LastProgressWall, Duration;
            internal float LastRemaining;
            internal int Frame = -1;
            internal readonly Dictionary<int, EnemyObservation> Enemies = new Dictionary<int, EnemyObservation>(100);
            internal readonly Dictionary<int, TowerObservation> Towers = new Dictionary<int, TowerObservation>(6);
            internal readonly RaycastHit[] GroundHits = new RaycastHit[16];
        }
        static Watch active;
        static Receipt last = new Receipt();
        public static bool IsRunning => active != null;
        public static string CurrentReceiptJson() => JsonUtility.ToJson(active != null ? active.Report : last, true);

        /// <summary>Call during stable Play with no room connected and no pending connection.</summary>
        public static string RunIsolated(GameSession owner, string receiptPath)
        {
            if(active!=null)return CurrentReceiptJson();
            var report=NewReceipt("isolated_public_spawn_and_uncollected_reward",receiptPath);
            last=report;
            if(!Stable(owner)||owner.CoopMode||owner.Rooms!=null&&(owner.Rooms.Connected||owner.Rooms.Busy)||
                owner.Defense!=null&&owner.Defense.Running||owner.Tutorial==null||owner.Progression==null||
                owner.IsDowned||AbilityBindings.IsRebinding)
                return Reject(report,"Requires stable offline Play, initialized owner, no running defense/downed state/rebind. Never leaves or starts a room.");
            var originalCombat=owner.Combat;var originalWorld=owner.World;var originalProgression=owner.Progression;
            var originalAudio=owner.Audio;
            bool started=owner.HasStarted,paused=owner.Paused,unlocked=owner.CombatUnlocked,victory=owner.Victory;
            int goal=owner.Goal,kills=owner.Defeated;string objective=owner.Objective;
            var profile=new Profile(owner);
            var oldXp=Ids(Object.FindObjectsByType<XpPickup>(FindObjectsInactive.Include));
            var oldCrystals=Ids(Object.FindObjectsByType<HeartcrystalPickup>(FindObjectsInactive.Include));
            GameObject island=null;
            try
            {
                // No frame is yielded while any public owner pointer is temporarily replaced.
                island=new GameObject("003 acceptance owned island"){hideFlags=HideFlags.DontSave};
                var worldGo=new GameObject("Unbuilt isolated world");worldGo.transform.SetParent(island.transform,false);
                var world=worldGo.AddComponent<DungeonWorld>();
                var progressionGo=new GameObject("Isolated uncollected rewards");progressionGo.transform.SetParent(island.transform,false);
                var progression=progressionGo.AddComponent<ElementProgression>();progression.Initialize(owner.Player);
                var combatGo=new GameObject("Isolated actual combat");combatGo.transform.SetParent(island.transform,false);
                var combat=combatGo.AddComponent<OracleCombat>();
                owner.World=world;owner.Combat=combat;owner.Progression=progression;owner.Audio=null;
                owner.HasStarted=true;owner.Paused=false;owner.CombatUnlocked=true;owner.Victory=false;
                combat.Initialize(owner.Player,world);owner.Goal=int.MaxValue;owner.Defeated=0;
                Vector3 origin=new Vector3(1200f,0f,1200f);
                combat.Shrine.transform.position=origin+new Vector3(0,0,20);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Owned flat support";
                floor.transform.SetParent(island.transform,false);floor.transform.position=origin+Vector3.down*.5f;
                floor.transform.localScale=new Vector3(32,1,12);floor.GetComponent<Renderer>().enabled=false;
                Physics.SyncTransforms();
                var first=combat.SpawnEnemy(origin+Vector3.up*.2f,101,OracleEnemy.MonsterArchetype.Stalker);
                CheckResult(report,"actual_spawn",first!=null,"Actual initialized combat admitted a Stalker on the owned native collider.");
                if(first==null)throw new InvalidOperationException("No first actor; later reward probes require its actual instance.");
                CheckResult(report,"stable_id_deduplicates",ReferenceEquals(first,combat.SpawnEnemy(origin,101,OracleEnemy.MonsterArchetype.Shade)),"Same stable ID returns the existing actor; no second actor.");
                CheckResult(report,"occupied_spawn_defers",combat.SpawnEnemy(origin+Vector3.right*.1f,102,OracleEnemy.MonsterArchetype.Stalker)==null,"Public admission rejects a different ID overlapping the actor footprint.");
                var second=combat.SpawnEnemy(origin+Vector3.right*6,102,OracleEnemy.MonsterArchetype.Shade);
                var third=combat.SpawnEnemy(origin+Vector3.right*12,103,OracleEnemy.MonsterArchetype.Spider);
                CheckResult(report,"distinct_roles_admit",second!=null&&third!=null&&combat.ActiveEnemies.Count==3,"Separated Stalker, Shade and Spider use the production admission path.");
                CheckResult(report,"spawn_support_y",Mathf.Abs(first.transform.position.y-.01f)<.025f,"Root raycast found the actual floor. Moving sole planting is a separate observed check.");
                var opening=CurrentDefensePacing.EnemyAt(0,0);
                first.ConfigureDefenseWave(opening.Health/CurrentDefensePacing.BaseHealth(opening.Archetype),opening.SpeedScale,opening.DamageScale);
                CheckResult(report,"opening_twenty_hp",Mathf.Abs(first.MaxHP-20f)<.002f&&Mathf.Abs(first.HP-20f)<.002f&&!first.IsFlying,"Configured actual opening role has20HP and stays grounded.");
                first.TakeHit(0);first.TakeHit(float.NaN);first.TakeHit(float.PositiveInfinity);first.TakeHit(-1);
                CheckResult(report,"invalid_damage_rejected",Mathf.Abs(first.HP-20f)<.002f,"Zero/negative/nonfinite hit cannot modify actual actor HP.");
                first.TakeHit(first.MaxHP+1);
                int xp=NewActive<XpPickup>(oldXp),crystals=NewActive<HeartcrystalPickup>(oldCrystals);
                CheckResult(report,"actual_kill_one_drop_each",!first.Alive&&owner.Defeated==1&&xp==1&&crystals==1,"Actual TakeHit→GuardianDefeated emitted one uncollected XP mote and one Heartstone, with one transient kill.");
                first.TakeHit(999);
                CheckResult(report,"repeat_dead_hit_no_duplicate_reward",owner.Defeated==1&&NewActive<XpPickup>(oldXp)==xp&&NewActive<HeartcrystalPickup>(oldCrystals)==crystals,"A repeat hit of the same dead actor cannot emit another reward.");
                if(third!=null)
                {
                    third.ApplyRemoteState(third.transform.position,0,false,third.MaxHP);
                    CheckResult(report,"replica_death_no_reward",owner.Defeated==1&&NewActive<XpPickup>(oldXp)==xp&&NewActive<HeartcrystalPickup>(oldCrystals)==crystals,"Public replica state changes presentation/HP without granting a kill or drop.");
                }
                bool launched=combat.AutoCastFrom(origin+Vector3.up*5f,0,1f,Vector3.forward,0,17);
                CheckResult(report,"owned_pooled_launch",launched,"One actual public host launch admitted to the isolated production projectile pool; no frame is advanced.");
                combat.SetRemoteSimulation(true);
                bool retired=true;foreach(var projectile in world.GetComponentsInChildren<OracleProjectile>(true))if(projectile.gameObject.activeSelf)retired=false;
                CheckResult(report,"remote_transition_retires_pool",retired,"Actual SetRemoteSimulation cancels all owned pooled projectile objects. Generation/replay semantics are tested independently by the actual DTO adapter.");
                if(second!=null){float hp=second.HP;second.TakeHit(1);CheckResult(report,"replica_takehit_rejected",second.HP==hp,"Production remote-simulation gate rejects gameplay damage.");}
                CheckResult(report,"fixed_cooldown_constants",OracleCombat.AbilityCooldownSeconds==4f&&OracleCombat.PrimaryCadenceSeconds==1f,"Exact production constants; timed casting acceptance uses the separate confirmed-casting fixture.");
                report.nativeExecuted=true;
            }
            catch(Exception error){CheckResult(report,"probe_exception",false,error.GetType().Name+": "+error.Message);}
            finally
            {
                // Restore owner pointers even if Unity rejects disposal of a native resource.
                owner.Combat=originalCombat;owner.World=originalWorld;owner.Progression=originalProgression;owner.Audio=originalAudio;
                owner.HasStarted=started;owner.Paused=paused;owner.CombatUnlocked=unlocked;owner.Victory=victory;
                owner.Goal=goal;owner.Defeated=kills;owner.Objective=objective;
                try
                {
                    // Deactivate new loose XP motes before any Update can collect them.
                    CleanupNew<XpPickup>(oldXp);CleanupNew<HeartcrystalPickup>(oldCrystals);
                    if(island!=null){island.SetActive(false);Object.DestroyImmediate(island);}
                    report.ownedObjectsDeactivated=true;
                }
                catch(Exception error){CheckResult(report,"owned_cleanup",false,error.GetType().Name+": "+error.Message);if(island!=null)island.SetActive(false);}
                report.transientFieldsRestored=owner.Combat==originalCombat&&owner.World==originalWorld&&owner.Progression==originalProgression&&owner.Audio==originalAudio&&owner.HasStarted==started&&owner.Paused==paused&&owner.CombatUnlocked==unlocked&&owner.Victory==victory&&owner.Goal==goal&&owner.Defeated==kills&&owner.Objective==objective;
                report.profileReadbackUnchanged=profile.Same(owner);
                CheckResult(report,"transient_state_restored",report.transientFieldsRestored,"Original public session references/counters/flags restored in the same call.");
                CheckResult(report,"profile_readback_unchanged",report.profileReadbackUnchanged,"Read-only XP/skill/profile wallet/wands/tutorial-claim snapshot agrees; no pickup collection or profile write requested.");
            }
            End(report);return CurrentReceiptJson();
        }

        /// <summary>Read-only watch of a real host match. Root must arrange actual attacks/towers normally.</summary>
        public static string BeginWatch(GameSession owner,string receiptPath,double seconds=24d)
        {
            if(active!=null)FinishWatch("INCOMPLETE","Superseded explicitly.");
            var report=NewReceipt("read_only_actual_host_encounter",receiptPath);last=report;
            if(!Stable(owner)||owner.Rooms==null||!owner.Rooms.Connected||!owner.Rooms.IsHost||owner.Defense==null||!owner.Defense.ArenaReady||owner.Builder==null)
                return Reject(report,"Requires an actual connected host, prepared running arena and real builder. No room or ready state is fabricated.");
            double now=Time.timeAsDouble,wall=EditorApplication.timeSinceStartup;
            active=new Watch{Owner=owner,Defense=owner.Defense,Report=report,Code=owner.Rooms.Code,StartGame=now,LastGame=now,StartWall=wall,LastProgressWall=wall,Duration=Math.Max(6d,Math.Min(60d,seconds)),LastRemaining=owner.Defense.RemainingSeconds};
            Attach();Save(report);return CurrentReceiptJson();
        }
        public static string RunWire(string receiptPath)
        {
            if(active!=null)return CurrentReceiptJson();
            var report=NewReceipt("native_private_dto_and_pure_ledgers",receiptPath);last=report;
            report.wire=CoopDefenseDirector.RunDangerWireAcceptance();report.nativeExecuted=report.wire.nativeJsonExecuted;
            CheckResult(report,"production_dto_roundtrip",report.wire.nativeJsonExecuted&&report.wire.failures==0,"Actual private DTO native JsonUtility adapter; this does not send transport messages or prove2-client rendering.");
            End(report);return CurrentReceiptJson();
        }
        /// <summary>Actual joined client only: verifies that public damage APIs cannot simulate gameplay locally.</summary>
        public static string RunClientRejection(GameSession owner,string receiptPath)
        {
            if(active!=null)return CurrentReceiptJson();
            var report=NewReceipt("actual_client_public_damage_rejection",receiptPath);last=report;
            if(!Stable(owner)||owner.Rooms==null||!owner.Rooms.Connected||owner.Rooms.IsHost||owner.Defense==null||!owner.Defense.ArenaReady)
                return Reject(report,"Requires an actual prepared connected client. Role/transport/membership are never fabricated.");
            var profile=new Profile(owner);int testedActors=0,testedTowers=0;
            foreach(var actor in owner.Combat.ActiveEnemies)
            {
                if(actor==null||!actor.Alive)continue;float hp=actor.HP;actor.TakeHit(1f);
                CheckResult(report,"client_actor_damage_rejected_"+actor.TargetId,actor.HP==hp,"Actual public TakeHit under remote simulation is a no-op.");testedActors++;
                if(testedActors==3)break;
            }
            for(int id=1;id<=CoopDefenseDirector.MaximumPlacedShrines;id++)if(PlacedElementShrine.TryGetRegistered(id,out var tower)&&tower!=null&&!tower.IsDestroyed)
            {float hp=tower.Health;tower.TakeDamage(1f);CheckResult(report,"client_tower_damage_rejected_"+id,tower.Health==hp,"Actual public TakeDamage requires host authority.");testedTowers++;}
            CheckResult(report,"client_actor_present",testedActors>0,"At least one real replicated actor is required.",testedActors==0);
            CheckResult(report,"client_tower_present",testedTowers>0,"At least one real replicated tower is required.",testedTowers==0);
            CheckResult(report,"client_profile_unchanged",profile.Same(owner),"No collection, purchases or profile writes requested.");
            report.nativeExecuted=true;End(report);return CurrentReceiptJson();
        }
        public static string Cancel(){if(active!=null)FinishWatch("INCOMPLETE","Cancelled explicitly.");return CurrentReceiptJson();}
        static void Attach(){Detach();EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;AssemblyReloadEvents.beforeAssemblyReload+=OnReload;EditorApplication.quitting+=OnQuit;}
        static void Detach(){EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlay;AssemblyReloadEvents.beforeAssemblyReload-=OnReload;EditorApplication.quitting-=OnQuit;}
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)FinishWatch("INCOMPLETE","Play exited.");}
        static void OnReload(){FinishWatch("INCOMPLETE","Assembly reload.");}
        static void OnQuit(){FinishWatch("INCOMPLETE","Editor quit.");}
        static void Tick()
        {
            if(active==null){Detach();return;}
            try{Observe(active);}catch(Exception e){FinishWatch("INCOMPLETE",e.GetType().Name+": "+e.Message);}
        }
        static void Observe(Watch run)
        {
            var owner=run.Owner;var report=run.Report;
            if(!Stable(owner)||owner.Rooms==null||!owner.Rooms.Connected||!owner.Rooms.IsHost||owner.Rooms.Code!=run.Code||owner.Defense!=run.Defense||!run.Defense.ArenaReady)
            {FinishWatch("INCOMPLETE","Actual host/session/arena lifetime changed.");return;}
            double now=Time.timeAsDouble,wall=EditorApplication.timeSinceStartup;
            if(run.Defense.RemainingSeconds>run.LastRemaining+.5f){FinishWatch("INCOMPLETE","Defense clock reset/new run detected.");return;}
            run.LastRemaining=run.Defense.RemainingSeconds;
            if(now>run.LastGame){run.LastGame=now;run.LastProgressWall=wall;}
            if(wall-run.LastProgressWall>2d||wall-run.StartWall>run.Duration*3+5){FinishWatch("INCOMPLETE","Game clock stalled or bounded wall timeout.");return;}
            if(run.Frame==Time.frameCount)return;run.Frame=Time.frameCount;report.observedFrames++;report.observedGameSeconds=now-run.StartGame;
            int alive=0;
            foreach(var enemy in owner.Combat.ActiveEnemies)
            {
                if(enemy==null)continue;if(enemy.Alive)alive++;
                if(!run.Enemies.TryGetValue(enemy.TargetId,out var record)||record.Enemy!=enemy)
                {
                    var all=enemy.GetComponentsInChildren<MeshFilter>(true);var feet=new List<MeshFilter>(8);
                    foreach(var mesh in all)if(mesh.name=="Left leg"||mesh.name=="Right leg"||mesh.name.StartsWith("Spider leg ",StringComparison.Ordinal))feet.Add(mesh);
                    record=new EnemyObservation{Enemy=enemy,Feet=feet.ToArray(),Phase=enemy.AttackPhase,Sequence=enemy.AttackPresentationSequence};run.Enemies[enemy.TargetId]=record;
                }
                if(enemy.AttackPhase==OracleEnemy.GuardianAttackPhase.Windup&&enemy.AttackPresentationSequence!=record.Sequence)
                {
                    record.Sequence=enemy.AttackPresentationSequence;record.WindupAt=now;record.WindupDuration=enemy.AttackWindupSeconds;record.SawStart=enemy.AttackProgress<=.15f;report.observedAttacks++;
                    CheckResult(report,"visible_warning_"+enemy.TargetId+"_"+record.Sequence,enemy.AttackWarningVisible,"Actual warning GameObject is visible during a newly observed tell.");
                    if(enemy.AttackPresentationStyle==2)
                        CheckResult(report,"locked_tower_endpoint_"+enemy.TargetId+"_"+record.Sequence,enemy.AttackPresentationTargetId>0&&PlacedElementShrine.TryGetRegistered(enemy.AttackPresentationTargetId,out var targetTower)&&targetTower!=null&&(enemy.LockedAttackEndpoint-targetTower.Position).sqrMagnitude<.0025f,"Style2 commits to an existing tower position; no physical/dodgeable projectile is inferred.");
                }
                if(enemy.AttackPhase==OracleEnemy.GuardianAttackPhase.Strike&&record.Phase==OracleEnemy.GuardianAttackPhase.Windup&&record.SawStart)
                {
                    double elapsed=now-record.WindupAt;
                    CheckResult(report,"tell_before_strike_"+enemy.TargetId+"_"+record.Sequence,elapsed>=record.WindupDuration-.18f,"Observed public phase transition; allowance covers one clamped150ms frame plus sampling. elapsed="+elapsed.ToString("F3")+", authored="+record.WindupDuration.ToString("F3"));report.observedStrikes++;record.SawStart=false;
                }
                record.Phase=enemy.AttackPhase;
                if(enemy.Alive&&enemy.IsAwake&&!enemy.IsFlying&&record.Feet.Length>=2&&report.observedFrames%6==0)CheckGround(run,record);
            }
            report.maximumAlive=Math.Max(report.maximumAlive,alive);
            if(alive>OracleCombat.MaxActiveEnemies)CheckResult(report,"actual_actor_cap",false,"Alive production actors exceeded100.");
            for(int id=1;id<=CoopDefenseDirector.MaximumPlacedShrines;id++)
            {
                if(PlacedElementShrine.TryGetRegistered(id,out var tower)&&tower!=null)
                {
                    if(!run.Towers.TryGetValue(id,out var seen)||seen.Tower!=tower){seen=new TowerObservation{Tower=tower,Health=tower.Health};run.Towers[id]=seen;}
                    if(tower.Health<seen.Health-.001f){report.towerDamageEvents++;CheckResult(report,"actual_tower_damage_"+id,true,"Real host tower HP decreased by "+(seen.Health-tower.Health).ToString("F3")+"; observer never called damage.");}
                    seen.Health=tower.Health;
                    if(tower.IsDestroyed&&!seen.AwaitCleanup){seen.AwaitCleanup=true;seen.DestroyFrame=Time.frameCount;}
                }
            }
            foreach(var item in run.Towers)
            {
                var seen=item.Value;
                if(!seen.AwaitCleanup||Time.frameCount-seen.DestroyFrame<3)continue;
                bool absent=!PlacedElementShrine.TryGetRegistered(item.Key,out var target)||target==null||!target.gameObject.activeInHierarchy;
                CheckResult(report,"destroyed_tower_cleanup_"+item.Key,absent,"After three observed frames, destroyed registry object must be absent/inactive; token refund is not inferred.");
                if(absent)report.destroyedTowerCleanupEvents++;seen.AwaitCleanup=false;
            }
            if(report.observedGameSeconds>=run.Duration)FinishWatch(null,null);
        }
        static void CheckGround(Watch run,EnemyObservation record)
        {
            var enemy=record.Enemy;Vector3 at=enemy.transform.position;
            int count=Physics.RaycastNonAlloc(at+Vector3.up*1.5f,Vector3.down,run.GroundHits,3f,~0,QueryTriggerInteraction.Ignore);
            if(count==run.GroundHits.Length)return;float support=float.NegativeInfinity,nearest=float.PositiveInfinity;
            for(int i=0;i<count;i++){var hit=run.GroundHits[i];if(hit.collider==null||hit.normal.y<.8f||hit.collider.transform.IsChildOf(enemy.transform)||hit.collider.GetComponentInParent<OraclePlayer>()!=null||hit.collider.GetComponentInParent<OracleShrine>()!=null||hit.collider.GetComponentInParent<OracleProjectile>()!=null)continue;if(hit.distance<nearest){nearest=hit.distance;support=hit.point.y;}}
            if(float.IsNegativeInfinity(support))return;
            float sole=float.PositiveInfinity;
            foreach(var mesh in record.Feet){if(mesh==null||mesh.sharedMesh==null)continue;Bounds b=mesh.sharedMesh.bounds;Matrix4x4 m=mesh.transform.localToWorldMatrix;Vector3 e=b.extents;float bottom=m.MultiplyPoint3x4(b.center).y-(Mathf.Abs(m.m10)*e.x+Mathf.Abs(m.m11)*e.y+Mathf.Abs(m.m12)*e.z);sole=Mathf.Min(sole,bottom);}
            if(float.IsPositiveInfinity(sole))return;run.Report.groundSamples++;
            if(Mathf.Abs(sole-support)>.065f)CheckResult(run.Report,"ground_sole_"+enemy.TargetId,false,"Actual transformed leg mesh bound differs from native floor by "+(sole-support).ToString("F4")+"m. Screenshot/gait review still required.");
        }
        static void FinishWatch(string forced,string reason)
        {
            var run=active;Detach();active=null;if(run==null)return;
            var report=run.Report;report.callbacksDetached=true;report.nativeExecuted=report.observedFrames>0;
            if(forced!=null){report.status=forced;report.reason=reason??"";}
            else
            {
                CheckResult(report,"observed_attack_transition",report.observedStrikes>0,"At least one actual complete tell→Strike must occur.",report.observedStrikes==0);
                CheckResult(report,"observed_ground_support",report.groundSamples>0,"Actual leg mesh bounds vs native supporting collider sampled.",report.groundSamples==0);
                CheckResult(report,"observed_tower_attrition",report.towerDamageEvents>0,"Arrange a real tower and off-lane attacker through normal match inputs.",report.towerDamageEvents==0);
                CheckResult(report,"observed_destroyed_tower_cleanup",report.destroyedTowerCleanupEvents>0,"Destroyed-tower cleanup requires an actual observed destruction; no fake damage is supplied.",report.destroyedTowerCleanupEvents==0);
            }
            End(report);last=report;
        }
        static bool Stable(GameSession owner)=>EditorApplication.isPlaying&&!EditorApplication.isCompiling&&!EditorApplication.isPaused&&owner!=null&&ReferenceEquals(owner,GameSession.Instance)&&owner.Player!=null&&owner.World!=null&&owner.Combat!=null;
        static HashSet<string> Ids<T>(T[] values)where T:Object{var ids=new HashSet<string>();foreach(var item in values)if(item!=null)ids.Add(item.GetEntityId().ToString());return ids;}
        static int NewActive<T>(HashSet<string> before)where T:Component{int count=0;foreach(var item in Object.FindObjectsByType<T>(FindObjectsInactive.Include))if(!before.Contains(item.GetEntityId().ToString())&&item.gameObject.activeInHierarchy)count++;return count;}
        static void CleanupNew<T>(HashSet<string> before)where T:Component{foreach(var item in Object.FindObjectsByType<T>(FindObjectsInactive.Include))if(!before.Contains(item.GetEntityId().ToString())){item.gameObject.SetActive(false);Object.DestroyImmediate(item.gameObject);}}
        static Receipt NewReceipt(string mode,string path)=>new Receipt{mode=mode,path=ValidPath(path),status="RUNNING",startedUtc=DateTime.UtcNow.ToString("o")};
        static void CheckResult(Receipt report,string name,bool passed,string detail,bool incomplete=false)
        {report.assertions++;if(!passed){if(incomplete)report.incomplete++;else report.failures++;}if(report.checks.Count<128)report.checks.Add(new Check{name=name,detail=detail,status=passed?"PASS":incomplete?"INCOMPLETE":"FAIL"});}
        static string Reject(Receipt report,string reason){report.status="INCOMPLETE";report.reason=reason;End(report);return JsonUtility.ToJson(report,true);}
        static void End(Receipt report){if(report.status=="RUNNING")report.status=report.failures>0?"FAILED":report.incomplete>0?"INCOMPLETE":"PASSED";report.endedUtc=DateTime.UtcNow.ToString("o");Save(report);}
        static string ValidPath(string path)
        {if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("Explicit work receipt path required.");string full=Path.GetFullPath(path),work=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work"))+Path.DirectorySeparatorChar;if(!full.StartsWith(work,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(full)!=".json")throw new ArgumentException("Receipt must be a JSON file inside this workspace work directory.");return full;}
        static void Save(Receipt report){Directory.CreateDirectory(Path.GetDirectoryName(report.path));File.WriteAllText(report.path,JsonUtility.ToJson(report,true));}
    }
}
#endif
