using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace OdysseyOracle
{
    /// <summary>The Oracle's new automatic four-element first-person adaptation.</summary>
    public sealed partial class OracleCombat : MonoBehaviour
    {
        public const float AbilityCooldownSeconds = SpellIngredientRules.NormalRecoverySeconds;
        public const float SuperAbilityCooldownSeconds = SpellIngredientRules.SuperRecoverySeconds;
        public const float PrimaryCadenceSeconds = SpellIngredientRules.GlobalCadenceSeconds;
        // # ARCHITECTURE SNAPSHOT
        // Accepted-shot recovery is scalar simulation time, independent per
        // element. One global cadence gate remains0.5s; selection spends nothing.
        // The fixed4 timestamps are local-owned; shared matches use host state.
        readonly double[] elementReadyAt = new double[4];
        public static int RecoveryIngredientMask(int element, int combo, bool cosmicNova = false)
        { return element < 0 || element > 3 ? 0 : cosmicNova ? 15 : SpellIngredientRules.MaskFor(element, combo); }

        void CommitAcceptedRecovery(int usedMask, bool cosmicNova)
        {
            int recoveryMask = cosmicNova ? 15 : usedMask;
            float seconds = SpellIngredientRules.RecoverySeconds(recoveryMask);
            ConfirmedSelection.CommitRecovery(recoveryMask, abilityClock, seconds, elementReadyAt);
            for (int i = 0; i < 4; i++) if ((recoveryMask & (1 << i)) != 0)
            {
                CooldownRemaining[i] = ElementSwitchCooldowns[i] = seconds;
                CooldownDuration[i] = seconds;
            }
            primaryReadyAt = abilityClock + PrimaryCadenceSeconds;
            selection.MarkCastAccepted();
        }

        double abilityClock, primaryReadyAt;
        ConfirmedSelection selection;
        ManualCastClickGate manualClick;
        AutoSelectionWait autoSelectionWait;
        uint remoteSelectionSequence;
        bool hasRemoteSelection;
        string selectionStatus = "Tap an element to prepare";
        public int PreparedElement => selection.PreparedElement;
        public int ConfirmedMask => selection.ConfirmedMask;
        public int ConfirmedElement => selection.ConfirmedElement;
        public bool HasConfirmedSelection => selection.HasConfirmedSelection;
        public bool SelectionPending => selection.SelectionPending;
        public string SelectionStatus => selectionStatus;
        public int SelectedSpell { get; private set; }
        public string[] SpellNames { get; } = new[] { "Fire", "Water", "Earth", "Air" };
        public Color[] SpellColors { get; } = new[]
        {
            new Color(1f, .36f, .12f), new Color(.18f, .68f, 1f),
            new Color(.62f, .78f, .29f), new Color(.66f, .91f, 1f)
        };
        public float[] CooldownRemaining { get; } = new float[4];
        public float[] CooldownDuration { get; } = new[] { AbilityCooldownSeconds, AbilityCooldownSeconds, AbilityCooldownSeconds, AbilityCooldownSeconds };
        public float[] ElementSwitchCooldowns { get; } = new float[4];
        public enum ElementCombo { None, Combustion, BindingMud, Steam, Shrapnel, Magma, FrostGale }
        public bool AutoFireEnabled { get; private set; }
        public string CastIndicator => castFlash > 0f ? "CAST" : SpellIngredientRules.Count(ConfirmedMask) == 3 ? "ADD FOURTH ELEMENT" : SelectionPending ? "PREPARING" :
            HasConfirmedSelection ? AutoFireEnabled ? "AUTO-FIRE ON" : "Left-click to cast" : "Prepare and confirm an element";
        public string ComboText => selectionStatus;
        public string CombatStatus { get; private set; } = "Prepare, confirm, then aim and click";
        public const float CosmicNovaRadius = 4.5f, CosmicNovaBaseDamage = 20f;
        public const int CosmicNovaMaximumTargets = 8;
        CosmicNovaChain novaChain;
        int remoteNovaMask;
        float remoteNovaSeconds, novaCastFlash;
        CosmicNovaVfx novaVisuals;
        readonly OracleEnemy[] novaTargets = new OracleEnemy[CosmicNovaMaximumTargets];
        readonly float[] novaDistances = new float[CosmicNovaMaximumTargets];
        readonly OracleEnemy[] novaTrialTargets = new OracleEnemy[5];
        bool destroying;
        bool SharedNovaState => GameSession.Instance != null && GameSession.Instance.CoopMode &&
            GameSession.Instance.Defense != null && GameSession.Instance.Defense.Running;
        public int NovaIngredientMask => SharedNovaState ? remoteNovaSeconds > 0f ? remoteNovaMask : 0 : novaChain.MaskAt(abilityClock);
        public float NovaChainSeconds => SharedNovaState ? remoteNovaSeconds : novaChain.RemainingAt(abilityClock);
        public float NovaCastFlash => novaCastFlash;
        public bool CosmicNovaTrialActive { get; private set; }
        public int OwnCosmicNovaCastCount { get; private set; }
        public int CosmicNovaResolvedCount { get; private set; }
        public int LastNovaDamagedTargets { get; private set; }
        public Vector3 CosmicNovaTrialAimPoint => novaTrialTargets[0] != null && novaTrialTargets[0].Alive ?
            novaTrialTargets[0].AimPoint : new Vector3(0f, 1.2f, 14.2f);
        public event System.Action<int, Vector3, float> AuthoritativeNovaImpact;
        public event System.Action<Vector3, int> CosmicNovaResolved;
        public int CosmicNovaTrialAliveCount
        {
            get { int alive=0;for(int i=0;i<novaTrialTargets.Length;i++)if(novaTrialTargets[i]!=null&&novaTrialTargets[i].Alive)alive++;return alive; }
        }
        public int ActiveGameplayProjectileCount
        {
            get { int active=0;for(int i=0;i<projectiles.Count;i++)if(projectiles[i].Flying)active++;return active; }
        }
        OracleShrine trialSuppressedShrine;
        bool trialShrineWasEnabled;
        public int CurrentCombo => selection.Combo;
        public bool CosmicNovaPrepared
        {
            get
            {
                if (!HasConfirmedSelection || ConfirmedMask != 15) return false;
                for (int i = 0; i < 4; i++) if ((ConfirmedMask & (1 << i)) != 0 &&
                    (ElementSwitchCooldowns[i] > 0f || CooldownRemaining[i] > 0f)) return false;
                return true;
            }
        }
        public int ArcaneCharge => 0;
        public int ArcaneChargeMax => 4;
        public bool ArcaneReady => false;
        public float HitFlash { get; private set; }
        public string InteractionHint { get; private set; } = "";
        public OracleShrine Shrine => shrine;
        public bool ShrineActive => shrine != null && shrine.Active;
        public float ShrineHP => shrine != null ? shrine.Health : 0f;
        public float ShrineMaxHP => shrine != null ? shrine.MaxHealth : 300f;
        public string ShrineHint => shrine != null ? shrine.InteractionHint : "";
        public bool TutorialGuardianAwake => tutorialGuardian != null && tutorialGuardian.IsAwake;
        public bool TutorialGuardianDefeated => tutorialGuardian != null && !tutorialGuardian.Alive;
        public IReadOnlyList<OracleEnemy> ActiveEnemies => enemies;
        public OracleEnemy TutorialGuardian => tutorialGuardian;
        public event System.Action<int, Vector3> AuthoritativeShotHit;

        internal bool Playing
        {
            get
            {
                var session = GameSession.Instance;
                if (!initialized || session == null || player == null || world == null || remoteSimulation || !session.HasStarted ||
                    (!session.CombatUnlocked && !TutorialTowerPracticeActive) || session.Victory || session.IsDead) return false;
                bool sharedAuthority = session.CoopMode && session.Defense != null && session.Defense.Running &&
                    session.Rooms != null && session.Rooms.IsHost;
                return !session.Paused || sharedAuthority;
            }
        }
        internal bool RemotePresentation => initialized && remoteSimulation;
        // # ARCHITECTURE SNAPSHOT
        // Host-only peer adapters use a caller-owned8-value snapshot once/frame.
        // Feet/aim and sweeps are world metres; native pose/health/tickets remain
        // Director-owned. Solo/tutorial retain their existing cached player path.
        internal bool PeerArenaCombat
        {
            get
            {
                var context = GameSession.Instance;
                return context != null && context.CoopMode && world != null && world.IsCoopArena &&
                    context.Defense != null && context.Defense.Running && context.Defense.IsAuthority;
            }
        }
        readonly PeerCombatTarget[] peerTargets = new PeerCombatTarget[8];
        int peerTargetFrame = -1, peerTargetCount;
        public void InvalidatePeerTargetCache(){peerTargetFrame=-1;peerTargetCount=0;}
        internal int QueryPeerTargets(out PeerCombatTarget[] targets)
        {
            targets = peerTargets;
            if (!PeerArenaCombat) return 0;
            if (peerTargetFrame != Time.frameCount)
            {
                peerTargetFrame = Time.frameCount;
                peerTargetCount = Mathf.Clamp(GameSession.Instance.Defense.CopyPeerCombatTargets(peerTargets), 0, peerTargets.Length);
            }
            return peerTargetCount;
        }
        internal bool FindPeerProjectileContact(Vector3 origin, Vector3 direction, float distance, float radius,
            int lane, out ulong peerId, out float contact)
        {
            peerId = 0; contact = distance;
            if (!PeerArenaCombat || lane < 0) return false;
            int count = QueryPeerTargets(out PeerCombatTarget[] targets);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                PeerCombatTarget target = targets[i];
                if (!target.Targetable || target.Lane != lane ||
                    !PeerCapsuleSweep.TryHit(origin, direction, distance, radius, target.Feet, out float hit)) continue;
                if (!found || hit < contact - .00001f || Mathf.Abs(hit - contact) <= .00001f && target.PeerId < peerId)
                { found = true; peerId = target.PeerId; contact = hit; }
            }
            return found;
        }
        internal void PeerProjectileHit(ulong peerId, int sourceEnemyId, int serial, Vector3 point, float damageScale)
        {
            if (!Playing || !PeerArenaCombat) return;
            bool damaged = GameSession.Instance.Defense.ApplyPeerDamage(peerId,
                Mathf.Min(20f, 7f * .6375f * damageScale), sourceEnemyId, serial);
            SpawnImpact(point, damaged ? new Color(1f, .28f, .13f) : new Color(.5f, .85f, 1f), .35f);
        }
        internal OraclePlayer Player => player;
        internal IReadOnlyList<OracleEnemy> Enemies => enemies;
        const float UnitsPerMetre = 32f;
        const float MissileRange = 396f / UnitsPerMetre;
        const int ProjectilePoolSize = 48;
        const int CosmeticShotPoolSize = 12;
        public const int MaxActiveEnemies = 48;
        readonly List<OracleEnemy> enemies = new List<OracleEnemy>(MaxActiveEnemies);
        readonly List<OracleEnemy> splashTargets = new List<OracleEnemy>(6);
        readonly List<OracleProjectile> projectiles = new List<OracleProjectile>(ProjectilePoolSize);
        readonly List<Impact> impacts = new List<Impact>();
        readonly RaycastHit[] rayHits = new RaycastHit[64];
        readonly RaycastHit[] sphereHits = new RaycastHit[48];
        readonly RaycastHit[] capsuleHits = new RaycastHit[32];
        readonly Material[] elementMaterials = new Material[4];
        readonly CosmeticShot[] cosmeticShots = new CosmeticShot[CosmeticShotPoolSize];
        OraclePlayer player;
        DungeonWorld world;
        OracleShrine shrine;
        OracleEnemy tutorialGuardian;
        Transform encounterRoot, enemyRoot;
        SpatialTargetGrid targetGrid;
        CrowdSpacingSolver crowdSpacing;
        Material stone, darkStone, turquoise, gold, violet, red, pale, barBack;
        MaterialPropertyBlock impactBlock;
        bool initialized, portalUnlocked, encounterActive, portalEntered, remoteSimulation;
        float messageRemaining, targetSearchRemaining, castRemaining, castFlash;
        string message = "";
        int projectileCursor, impactCursor, nextTargetId, cosmeticCursor, hostileGeneration;
        RemoteHostileBoltVisuals remoteHostileBolts;

        sealed class CosmeticShot
        {
            public Transform Root, Focus, WingA, WingB;
            public Renderer FocusRenderer, WingRendererA, WingRendererB;
            public Vector3 Direction;
            public float Remaining, Speed, Radius;
            public int Element, Combo, PiercedCount;
            public bool CosmicNova;
            public readonly int[] PiercedIds = new int[2];
        }

        sealed class Impact
        {
            public Transform Root;
            public Renderer[] Renderers;
            public float Remaining, Duration, Size;
        }

        public void Initialize(OraclePlayer oraclePlayer, DungeonWorld dungeonWorld)
        {
            if (initialized) return;
            player = oraclePlayer;
            world = dungeonWorld;
            impactBlock = new MaterialPropertyBlock();
            targetGrid = new SpatialTargetGrid(MaxActiveEnemies, 2f);
            crowdSpacing = new CrowdSpacingSolver(MaxActiveEnemies);
            stone = MakeMaterial("Golem weathered stone", new Color(.26f, .29f, .31f));
            darkStone = MakeMaterial("Golem joints", new Color(.085f, .105f, .13f));
            turquoise = MakeMaterial("Oracle living light", new Color(.282f, .871f, .835f), 1.3f);
            gold = MakeMaterial("Oracle astral brass", new Color(1f, .77f, .30f), 1.25f);
            violet = MakeMaterial("Guardian living light", new Color(.498f, .914f, .862f), 1.25f);
            red = MakeMaterial("Golem attack warning", new Color(1f, .28f, .13f), 1.2f);
            pale = MakeMaterial("Hit sparks", new Color(.9f, 1f, .94f), 1.1f);
            barBack = MakeMaterial("Golem health background", new Color(.035f, .045f, .055f));
            for (int i = 0; i < 4; i++) elementMaterials[i] = MakeMaterial("Oracle " + SpellNames[i], SpellColors[i], 1.3f);
            novaVisuals = gameObject.AddComponent<CosmicNovaVfx>();
            novaVisuals.Initialize(pale);
            initialized = true;
            ResetEncounter();
        }

        public void ResetEncounter()
        {
            if (!initialized) return;
            AbortTrainingLab();
            remoteHostileBolts?.Dispose(); remoteHostileBolts = null; hostileGeneration = 0;
            if (encounterRoot != null) { encounterRoot.gameObject.SetActive(false); Destroy(encounterRoot.gameObject); }
            encounterRoot = new GameObject("Oracle staged encounter").transform;
            encounterRoot.SetParent(world.transform, false);
            enemies.Clear(); projectiles.Clear(); impacts.Clear();
            tutorialGuardian = null;
            targetGrid.Clear();
            crowdSpacing.Clear();
            InvalidatePeerTargetCache(); towerAttackSlots.Clear();
            portalUnlocked = encounterActive = portalEntered = false;
            HitFlash = 0f; SelectedSpell = 0; messageRemaining = targetSearchRemaining = castRemaining = castFlash = 0f;
            AutoFireEnabled = false; manualClick.Clear(); autoSelectionWait.Clear(); selection.Clear(); hasRemoteSelection = false; remoteSelectionSequence = 0; primaryReadyAt = 0d; RefreshSelectionStatus();
            abilityClock = 0d;
            ResetNovaState();
            for (int i = 0; i < elementReadyAt.Length; i++) elementReadyAt[i] = 0d;
            CombatStatus = "Manual casting · Prepare and confirm an element";
            projectileCursor = impactCursor = nextTargetId = 0;
            for (int i = 0; i < 4; i++) { CooldownRemaining[i] = 0f; ElementSwitchCooldowns[i] = 0f; CooldownDuration[i] = AbilityCooldownSeconds; }
            GameSession.Instance.Goal = GameSession.Instance.Defeated = 0;
            InteractionHint = "Complete the movement tutorial to awaken your abilities.";
            enemyRoot = new GameObject("Stage guardians").transform;
            enemyRoot.SetParent(encounterRoot, false);
            for (int i = 0; i < ProjectilePoolSize; i++)
            {
                var go = new GameObject("Pooled element / Shade projectile " + i);
                go.transform.SetParent(encounterRoot, false);
                var projectile = go.AddComponent<OracleProjectile>();
                projectile.Initialize(this, turquoise);
                projectiles.Add(projectile);
            }
            remoteHostileBolts = new RemoteHostileBoltVisuals(this, encounterRoot, red);
            cosmeticCursor = 0;
            for (int i = 0; i < cosmeticShots.Length; i++)
            {
                var shot = cosmeticShots[i] = new CosmeticShot();
                shot.Root = new GameObject("Pooled visual-only peer shot " + i).transform;
                shot.Root.SetParent(encounterRoot, false);
                shot.Focus = CreateBlock(shot.Root, "Cosmetic focus", Vector3.zero, Vector3.one, turquoise);
                shot.WingA = CreateBlock(shot.Root, "Cosmetic wing A", Vector3.zero, Vector3.one, turquoise);
                shot.WingB = CreateBlock(shot.Root, "Cosmetic wing B", Vector3.zero, Vector3.one, turquoise);
                shot.FocusRenderer = shot.Focus.GetComponent<Renderer>();
                shot.WingRendererA = shot.WingA.GetComponent<Renderer>();
                shot.WingRendererB = shot.WingB.GetComponent<Renderer>();
                shot.Root.gameObject.SetActive(false);
            }
            for (int i = 0; i < 20; i++)
            {
                var effect = new Impact { Root = new GameObject("Pooled hit sparks " + i).transform };
                effect.Root.SetParent(encounterRoot, false);
                effect.Renderers = new Renderer[3];
                for (int j = 0; j < 3; j++)
                {
                    Transform bit = CreateBlock(effect.Root, "Spark", Vector3.zero, Vector3.one * .06f, pale);
                    bit.localRotation = Quaternion.Euler(j * 35f, j * 51f, 45f);
                    effect.Renderers[j] = bit.GetComponent<Renderer>();
                }
                effect.Root.gameObject.SetActive(false);
                impacts.Add(effect);
            }
            var shrineObject = new GameObject("Green Oracle shrine");
            shrineObject.transform.SetParent(encounterRoot, false);
            shrineObject.transform.position = world.ShrinePosition;
            shrine = shrineObject.AddComponent<OracleShrine>();
            shrine.Initialize(this, darkStone, stone, turquoise, gold);
            shrine.ResetShrine();
        }

        public void BeginTutorialEncounter()
        {
            if (!initialized) return;
            PrepareStage(1);
            tutorialGuardian = SpawnGuardian(world.TutorialMonsterPracticePosition, false, true);
            Tell("A stone guardian rests in the first left alcove. Approach to awaken it.", 5f);
        }

        public void BeginStage(int stage)
        {
            if (!initialized) return;
            int count = Mathf.Clamp(10 + Mathf.Max(0, stage - 2) * 8, 10, MaxActiveEnemies);
            Vector3[] spawns = world.EnemySpawns;
            count = Mathf.Min(count, spawns.Length);
            PrepareStage(count);
            for (int i = 0; i < count; i++) SpawnGuardian(spawns[i], i % 4 == 3, false);
            Tell("Stage " + stage + " · Defend the green shrine", 3f);
        }

        public void SetRemoteSimulation(bool remote)
        {
            if (remoteSimulation != remote) { ClearCosmeticShots(); ClearRemoteDangerState(); }
            remoteSimulation = remote;
            if (remote) foreach (OracleProjectile projectile in projectiles) projectile.Retire();
        }

        public void ClearEnemies()
        {
            if (!initialized) return;
            AbortTrainingLab();
            if (towerPracticeOwned) EndTutorialTowerPractice();
            if (CosmicNovaTrialActive) EndCosmicNovaTrial();
            if (enemyRoot != null) { enemyRoot.gameObject.SetActive(false); Destroy(enemyRoot.gameObject); }
            enemyRoot = new GameObject("Stage guardians").transform;
            enemyRoot.SetParent(encounterRoot, false);
            enemies.Clear(); targetGrid.Clear(); crowdSpacing.Clear(); towerAttackSlots.Clear(); tutorialGuardian = null;
            CosmicNovaTrialActive = false;
            for (int i=0;i<novaTrialTargets.Length;i++) novaTrialTargets[i]=null;
            foreach (OracleProjectile projectile in projectiles) { projectile.Retire(); projectile.ClearDangerState(); }
            ClearCosmeticShots();
            remoteHostileBolts?.Clear(); hostileGeneration = 0;
            encounterActive = false;
        }

        void PrepareStage(int count)
        {
            ClearEnemies();
            GameSession.Instance.Defeated = 0;
            GameSession.Instance.Goal = count;
            GameSession.Instance.Objective = "Defend the green shrine · defeat the guardians";
            encounterActive = count > 0;
            portalUnlocked = portalEntered = false;
            world.LockPortal();
        }

        public OracleEnemy SpawnEnemy(Vector3 position, int stableId, bool shade, bool flying = false)
        { return SpawnEnemy(position, stableId, shade ? OracleEnemy.MonsterArchetype.Shade : OracleEnemy.MonsterArchetype.Stalker, flying); }

        public OracleEnemy SpawnEnemy(Vector3 position, int stableId, OracleEnemy.MonsterArchetype archetype, bool flying = false)
        {
            if (!initialized) return null;
            for (int i = 0; i < enemies.Count; i++) if (enemies[i].TargetId == stableId) return enemies[i];
            for (int i = enemies.Count - 1; i >= 0; i--)
                if (!enemies[i].Alive)
                {
                    enemies[i].gameObject.SetActive(false);
                    Destroy(enemies[i].gameObject);
                    enemies.RemoveAt(i);
                }
            crowdSpacing.Refresh(enemies, Time.time);
            if (!remoteSimulation && !crowdSpacing.CanAdmit(position, OracleEnemy.SpawnFootprintRadius(flying))) return null;
            OracleEnemy spawned = SpawnGuardian(position, archetype == OracleEnemy.MonsterArchetype.Shade, false, stableId, flying,
                archetype == OracleEnemy.MonsterArchetype.Spider ? MonsterVoxelCosmetics.Style.Spider :
                archetype == OracleEnemy.MonsterArchetype.Shade ? MonsterVoxelCosmetics.Style.Shade : MonsterVoxelCosmetics.Style.Stalker);
            if (spawned != null)
            {
                spawned.ConfigureArchetype(archetype);
                crowdSpacing.Track(spawned);
                encounterActive = true;
            }
            return spawned;
        }

        readonly TowerAttackSlots towerAttackSlots = new TowerAttackSlots();

        internal bool GetTowerApproach(OracleEnemy enemy, PlacedElementShrine tower, Vector3 forward,
            out Vector3 point, out bool canStrike)
        { return towerAttackSlots.Approach(enemy, tower, forward, out point, out canStrike); }

        internal bool GetCoreApproach(OracleEnemy enemy, OracleShrine core, Vector3 forward,
            out Vector3 point, out bool canStrike)
        { return towerAttackSlots.ApproachCore(enemy, core, forward, out point, out canStrike); }

        internal Vector3 SpaceMovement(OracleEnemy enemy, Vector3 desired, Vector3 laneOrigin, Vector3 laneForward,
            bool constrainLane, float speed, float dt)
        {
            crowdSpacing.Refresh(enemies, Time.time);
            bool peers=PeerArenaCombat;int count=peers?QueryPeerTargets(out _):0;
            return crowdSpacing.Resolve(enemy,desired,laneOrigin,laneForward,constrainLane,speed,dt,
                !peers&&GameSession.Instance.HP>0f&&!GameSession.Instance.IsDowned?player:null,
                enemy.AttackLaneHalfWidth,peers?peerTargets:null,count);
        }

        OracleEnemy SpawnGuardian(Vector3 spawn, bool ranged, bool seated, int? stableId = null, bool flying = false,
            MonsterVoxelCosmetics.Style? visualStyle = null)
        {
            if (enemies.Count >= MaxActiveEnemies) return null;
            var go = new GameObject(seated ? "Seated portal guardian" : ranged ? "Shade guardian" : "Stalker guardian");
            go.transform.SetParent(enemyRoot, false);
            go.transform.position = spawn;
            if (Physics.Raycast(spawn + Vector3.up * 2f, Vector3.down, out RaycastHit floor, 4f, ~0, QueryTriggerInteraction.Ignore))
                go.transform.position = new Vector3(spawn.x, floor.point.y, spawn.z);
            var enemy = go.AddComponent<OracleEnemy>();
            int id = stableId ?? ++nextTargetId;
            nextTargetId = Mathf.Max(nextTargetId, id);
            enemy.Initialize(this, ranged, stone, darkStone, turquoise, violet, red, barBack, id,
                visualStyle ?? (seated ? MonsterVoxelCosmetics.Style.GuardianGolem : ranged ?
                    MonsterVoxelCosmetics.Style.Shade : MonsterVoxelCosmetics.Style.Stalker));
            if (flying) enemy.ConfigureFlying();
            if (seated) enemy.SitDormant();
            enemies.Add(enemy);
            return enemy;
        }

        void Update()
        {
            // A script reload can clear the static owner before runtime bindings exist.
            var liveSession = GameSession.Instance;
            if (!initialized || liveSession == null || player == null || world == null) return;
            HitFlash = Mathf.Max(0f, HitFlash - Time.unscaledDeltaTime * 4f);
            var progression = GameSession.Instance.Progression;
            if (progression != null) SelectedSpell = Mathf.Clamp(progression.SelectedElement, 0, 3);
            bool localClock = Playing || GameSession.Instance.IsPlaying && GameSession.Instance.CombatUnlocked;
            if (localClock)
            {
                float clockDt = Time.deltaTime;
                abilityClock += clockDt;
                remoteNovaSeconds = Mathf.Max(0f, remoteNovaSeconds - clockDt);
                novaCastFlash = Mathf.Max(0f, novaCastFlash - clockDt * 2f);
                castRemaining = Mathf.Max(0f, castRemaining - clockDt);
                castFlash = Mathf.Max(0f, castFlash - clockDt);
                for (int i = 0; i < 4; i++)
                {
                    if (ElementSwitchCooldowns[i] <= 0f) CooldownDuration[i] = AbilityCooldownSeconds;
                    bool sharedClock = GameSession.Instance.CoopMode && GameSession.Instance.Defense != null && GameSession.Instance.Defense.Running;
                    float remaining = sharedClock ? Mathf.Max(0f, ElementSwitchCooldowns[i] - clockDt) :
                        Mathf.Max(0f, (float)(elementReadyAt[i] - abilityClock));
                    CooldownRemaining[i] = ElementSwitchCooldowns[i] = remaining;
                }
            }
            bool dangerClock = RemotePresentation && liveSession.Defense != null && liveSession.Defense.Running;
            if (dangerClock) remoteHostileBolts?.Tick(Time.unscaledDeltaTime);
            bool cosmeticClock = GameSession.Instance.SimulationActive && GameSession.Instance.CombatUnlocked;
            if ((cosmeticClock || dangerClock) && remoteSimulation)
            {
                UpdateCosmeticShots(Time.deltaTime);
                UpdateImpacts(dangerClock ? Time.unscaledDeltaTime : Time.deltaTime);
            }
            else if (!cosmeticClock) ClearCosmeticShots();
            var inputSession = GameSession.Instance;
            bool castContext = inputSession.IsPlaying && inputSession.CombatUnlocked &&
                Cursor.lockState == CursorLockMode.Locked && !AbilityBindings.IsRebinding &&
                Time.frameCount > AbilityBindings.LastChangeFrame &&
                (inputSession.Builder == null || !inputSession.Builder.IsPlacing) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            bool pressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool pressEdge = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (manualClick.Step(castContext && Mouse.current != null, pressed, pressEdge)) ManualCast();
            if (!Playing) return;
            float dt = Time.deltaTime;
            UpdateImpacts(dt);
            targetSearchRemaining = Mathf.Max(0f, targetSearchRemaining - dt);
            if (messageRemaining > 0f) messageRemaining -= dt;
            if (!world.IsCoopArena && !CosmicNovaTrialActive && encounterActive && !portalUnlocked &&
                (GameSession.Instance.Tutorial == null || GameSession.Instance.Tutorial.Finished) &&
                GameSession.Instance.Goal > 0 && GameSession.Instance.Defeated >= GameSession.Instance.Goal)
            {
                portalUnlocked = true;
                world.UnlockPortal();
                GameSession.Instance.Objective = "The portal is open. Walk into its light.";
                Tell("All guardians defeated · Walk into the awakened portal", 4f);
            }
            if (!world.IsCoopArena && portalUnlocked && !portalEntered && Vector3.Distance(player.transform.position, world.PortalPosition) < 1.45f)
            {
                portalEntered = true;
                GameSession.Instance.CompleteStage();
                return;
            }
            InteractionHint = messageRemaining > 0f ? message : portalUnlocked ? "Walk into the portal to continue" : selectionStatus;
            bool sharedAuto = GameSession.Instance.CoopMode && GameSession.Instance.Defense != null && GameSession.Instance.Defense.Running;
            if (AutoFireEnabled && HasConfirmedSelection && !sharedAuto && GameSession.Instance.IsPlaying && encounterActive &&
                !AbilityBindings.IsRebinding && (GameSession.Instance.Builder == null || !GameSession.Instance.Builder.IsPlacing)) TryCastConfirmed(true);

        }

        public void SetAutoFire(bool enabled)
        {
            AutoFireEnabled = enabled;
            CombatStatus = enabled ? "AUTO-FIRE ON | Change in Settings" : "MANUAL CAST | Left-click confirmed spell";
            var session = GameSession.Instance;
            if (session != null && session.CoopMode && session.Defense != null && session.Defense.Running)
                session.Defense.RequestAutoFire(enabled);
        }

        public void ApplyRemoteAutoFireState(bool enabled)
        {
            AutoFireEnabled = enabled;
            CombatStatus = enabled ? "AUTO-FIRE ON | Change in Settings" : "MANUAL CAST | Left-click confirmed spell";
        }

        public void SelectSpell(int index)
        { TrySelectElement(index); }

        public bool TrySelectElement(int index)
        {
            var session = GameSession.Instance;
            if (index < 0 || index > 3 || session == null || !session.IsPlaying || !session.CombatUnlocked ||
                AbilityBindings.IsRebinding || Time.frameCount <= AbilityBindings.LastChangeFrame) return false;
            if (session.CoopMode && session.Defense != null && session.Defense.Running) return session.Defense.RequestSelectElement(index);
            if (!selection.Tap(index)) return false;
            autoSelectionWait.NotifyInput(abilityClock);
            SelectedSpell = selection.PreviewElement;
            session.Progression?.SetSelectedElementDirect(SelectedSpell);
            RefreshSelectionStatus(); return true;
        }

        void RefreshSelectionStatus()
        {
            selectionStatus = SelectionPending ? SpellNames[PreparedElement] + " prepared | Tap again or add an element" :
                SpellIngredientRules.Count(ConfirmedMask) == 3 ? "3 elements selected | Add fourth for Cosmic Nova" :
                HasConfirmedSelection ? RecipeName(ConfirmedMask, ConfirmedElement, CurrentCombo) + " ready | Add elements or left-click to cast" :
                "Tap an element to select";
            CombatStatus = selectionStatus;
        }

        public bool ApplyRemoteSelectionState(int prepared, int mask, int dominant, uint sequence)
        {
            if (hasRemoteSelection && sequence != remoteSelectionSequence && !ConfirmedSelection.Newer(sequence, remoteSelectionSequence)) return false;
            bool changed = PreparedElement != prepared || ConfirmedMask != mask || ConfirmedElement != dominant;
            if (!selection.Restore(prepared, mask, dominant)) return false;
            hasRemoteSelection = true; remoteSelectionSequence = sequence;
            SelectedSpell = selection.PreviewElement;
            GameSession.Instance?.Progression?.SetSelectedElementDirect(SelectedSpell);
            if (changed) RefreshSelectionStatus(); return true;
        }

        public void ApplyRemoteElementState(int element, float[] returnCooldowns, int combo, float comboSeconds)
        {
            SelectedSpell = Mathf.Clamp(element, 0, 3);
            if (GameSession.Instance.Progression != null) GameSession.Instance.Progression.SetSelectedElementDirect(SelectedSpell);
            if (returnCooldowns != null && returnCooldowns.Length == 4)
                for (int i = 0; i < 4; i++)
                {
                    float remaining = Mathf.Clamp(returnCooldowns[i], 0f, SuperAbilityCooldownSeconds);
                    if (remaining > AbilityCooldownSeconds) CooldownDuration[i] = SuperAbilityCooldownSeconds;
                    else if (remaining <= 0f) CooldownDuration[i] = AbilityCooldownSeconds;
                    CooldownRemaining[i] = ElementSwitchCooldowns[i] = remaining;
                }
        }

        string RecipeName(int mask, int dominant, int combo)
        {
            if (mask == 15) return "Cosmic Nova";
            return combo == 0 ? SpellNames[dominant] : ComboName((ElementCombo)combo);
        }

        static ElementCombo PairCombo(int a, int b)
        { return a < 0 || a > 3 || b < 0 || b > 3 ? ElementCombo.None : (ElementCombo)ConfirmedSelection.ComboForMask((1 << a) | (1 << b)); }
        static string ComboName(ElementCombo combo) => combo == ElementCombo.BindingMud ? "Binding Mud" : combo == ElementCombo.FrostGale ? "Frost Gale" : combo.ToString();

        public void Cast() { ManualCast(); }
        public void ManualCast() { TryCastConfirmed(false); }

        bool TryCastConfirmed(bool automatic)
        {
            var session = GameSession.Instance;
            if (automatic && (!AutoFireEnabled || !autoSelectionWait.Ready(abilityClock)) || !HasConfirmedSelection || session == null || !session.IsPlaying ||
                !session.CombatUnlocked || AbilityBindings.IsRebinding || Time.frameCount <= AbilityBindings.LastChangeFrame ||
                session.Builder != null && session.Builder.IsPlacing) return false;
            if (session.CoopMode && session.Defense != null && session.Defense.Running)
            { if (!automatic) session.Defense.RequestCast(ConfirmedElement, ConfirmedMask); return false; }
            if (!Playing || !selection.Ready(abilityClock, primaryReadyAt, elementReadyAt)) return false;
            Vector3 origin = player.ViewCamera.transform.position;
            int element = ConfirmedElement, usedMask = ConfirmedMask, combo = CurrentCombo;
            float scale = SelectedDamageMultiplier(usedMask);
            float speed = element == 3 ? 32f : element == 2 ? 17f : 740f / UnitsPerMetre;
            bool nova = SpellIngredientRules.IsSuper(usedMask);
            if (!Launch((OracleProjectile.Kind)((int)OracleProjectile.Kind.Fire + element), origin,
                player.ViewCamera.transform.forward, speed, MissileRange, element == 2 ? .19f : .11f,
                elementMaterials[element], scale, combo, 0, nova, statusMask: session.Progression != null ? session.Progression.EffectivePassiveMask : 0, ingredientMask: usedMask)) return false;
            CommitAcceptedRecovery(usedMask, nova);
            novaChain.Reset();
            if (nova) { CountAcceptedNovaLaunch(); novaCastFlash = 1f; }
            castRemaining = PrimaryCadenceSeconds; castFlash = .22f;
            session.Audio?.PlayCast(element); session.Viewmodel?.PulseCast(element);
            return true;
        }

        public bool AutoCastFrom(Vector3 origin, int element, float damageMultiplier = 1f)
        { return AutoCastFrom(origin, element, damageMultiplier, player != null ? player.ViewCamera.transform.forward : Vector3.forward); }

        public bool AutoCastFrom(Vector3 origin, int element, float damageMultiplier, Vector3 aimDirection, int combo = 0, int networkShotId = 0, bool cosmicNova = false, int statusMask = 0, int ingredientMask = 0)
        {
            if (!Playing) return false;
            element = Mathf.Clamp(element, 0, 3);
            if (!FiniteDirection(aimDirection) || SpellIngredientRules.ResolveMask(element, combo, ingredientMask) == 0) return false;
            float speed = element == 3 ? 32f : element == 2 ? 17f : 740f / UnitsPerMetre;
            return Launch((OracleProjectile.Kind)((int)OracleProjectile.Kind.Fire + element), origin,
                aimDirection.normalized, speed, MissileRange, element == 2 ? .19f : .11f,
                elementMaterials[element], Mathf.Max(.1f, damageMultiplier), Mathf.Clamp(combo, 0, 6), Mathf.Max(0, networkShotId), cosmicNova, statusMask: statusMask & 0xFFF, ingredientMask: ingredientMask);
        }

        public void PlayNetworkShotFeedback(int element, Vector3 origin, Vector3 direction, int combo, bool ownCaster, bool cosmicNova = false)
        {
            var session = GameSession.Instance;
            if (!initialized || session == null || !session.CombatUnlocked || !session.SimulationActive ||
                element < 0 || element >= 4 || !FinitePosition(origin) || !FiniteDirection(direction)) return;
            if (!ownCaster) session.Audio?.PlayRemoteCast(element, origin);
            if (ownCaster)
            {
                castFlash = .22f;
                if (cosmicNova) { OwnCosmicNovaCastCount++; novaCastFlash = 1f; }
                if (session.Viewmodel != null) session.Viewmodel.PulseCast(element);
                if (session.Audio != null) session.Audio.PlayCast(element);
            }
            // The host already has the damaging projectile. Only clients borrow
            // this separate pool; these records have no damage or wall-edit API.
            if (!remoteSimulation) return;
            CosmeticShot shot = cosmeticShots[cosmeticCursor];
            cosmeticCursor = (cosmeticCursor + 1) % cosmeticShots.Length;
            shot.Element = element; shot.Combo = Mathf.Clamp(combo, 0, 6); shot.PiercedCount = 0; shot.CosmicNova = cosmicNova;
            shot.Direction = direction.normalized;
            shot.Speed = element == 3 ? 32f : element == 2 ? 17f : 740f / UnitsPerMetre;
            shot.Remaining = MissileRange;
            shot.Radius = element == 2 ? .19f : .11f;
            shot.Root.SetPositionAndRotation(origin, Quaternion.LookRotation(shot.Direction));
            shot.Focus.localRotation = Quaternion.identity;
            shot.Focus.localScale = new Vector3(shot.Radius * 1.5f, shot.Radius * 1.5f, shot.Radius * (element == 2 ? 1.8f : 3.5f));
            shot.WingA.localPosition = new Vector3(shot.Radius * 1.4f, 0f, -.06f);
            shot.WingB.localPosition = new Vector3(-shot.Radius * 1.4f, 0f, -.06f);
            shot.WingA.localRotation = shot.WingB.localRotation = Quaternion.identity;
            shot.WingA.localScale = shot.WingB.localScale = element == 3 ? new Vector3(.22f, .025f, .08f) :
                element == 0 ? new Vector3(.06f, .06f, .18f) : new Vector3(.07f, .07f, .07f);
            shot.FocusRenderer.sharedMaterial = shot.WingRendererA.sharedMaterial = shot.WingRendererB.sharedMaterial = elementMaterials[element];
            shot.Root.gameObject.SetActive(true);
        }

        public void PlayOwnHitConfirmation(Vector3 point)
        {
            var session = GameSession.Instance;
            if (!initialized || session == null || !session.CombatUnlocked || !session.SimulationActive || !FinitePosition(point)) return;
            HitFlash = 1f;
            if (remoteSimulation) SpawnImpact(point, SpellColors[SelectedSpell], .35f);
        }

        void ClearCosmeticShots()
        {
            for (int i = 0; i < cosmeticShots.Length; i++)
                if (cosmeticShots[i] != null && cosmeticShots[i].Root != null)
                    cosmeticShots[i].Root.gameObject.SetActive(false);
        }

        void UpdateCosmeticShots(float dt)
        {
            for (int i = 0; i < cosmeticShots.Length; i++)
            {
                CosmeticShot shot = cosmeticShots[i];
                if (shot == null || !shot.Root.gameObject.activeSelf) continue;
                float distance = Mathf.Min(shot.Speed * dt, shot.Remaining);
                int count = QueryProjectileSweep(shot.Root.position, shot.Radius, shot.Direction, distance, out RaycastHit[] hits);
                RaycastHit nearest = default;
                float nearestDistance = float.PositiveInfinity;
                bool found = false;
                for (int j = 0; j < count; j++)
                {
                    Collider collider = hits[j].collider;
                    if (IsPlayerCollider(collider) || collider.GetComponentInParent<OracleProjectile>() != null ||
                        collider.GetComponentInParent<OracleShrine>() != null) continue;
                    OracleEnemy enemy = collider.GetComponentInParent<OracleEnemy>();
                    if (enemy != null)
                    {
                        bool skip = !enemy.Targetable;
                        for (int p = 0; p < shot.PiercedCount; p++) skip |= shot.PiercedIds[p] == enemy.TargetId;
                        if (skip) continue;
                    }
                    if (hits[j].distance < nearestDistance)
                    { nearest = hits[j]; nearestDistance = hits[j].distance; found = true; }
                }
                if (found)
                {
                    Vector3 point = nearest.distance <= .0001f ? nearest.collider.ClosestPoint(shot.Root.position) : nearest.point;
                    SpawnImpact(point, SpellColors[shot.Element], shot.Element == 2 ? .6f : .35f);
                    OracleEnemy enemy = nearest.collider.GetComponentInParent<OracleEnemy>();
                    if (!shot.CosmicNova && shot.Combo == 4 && enemy != null && shot.PiercedCount < shot.PiercedIds.Length)
                    {
                        shot.PiercedIds[shot.PiercedCount++] = enemy.TargetId;
                        float advance = Mathf.Max(0f, nearest.distance);
                        shot.Root.position += shot.Direction * advance;
                        shot.Remaining -= advance;
                    }
                    else shot.Root.gameObject.SetActive(false);
                }
                else
                {
                    shot.Root.position += shot.Direction * distance;
                    shot.Remaining -= distance;
                    shot.Focus.Rotate(0f, 0f, dt * (shot.Element == 1 ? 110f : 210f), Space.Self);
                    shot.WingA.Rotate(0f, 0f, dt * 180f, Space.Self);
                    shot.WingB.Rotate(0f, 0f, -dt * 180f, Space.Self);
                }
                if (shot.Remaining <= 0f) shot.Root.gameObject.SetActive(false);
            }
        }

        static bool FinitePosition(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        static bool FiniteDirection(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z) && value.sqrMagnitude > .001f;

        internal float DamageMultiplier(int element, bool includeWandUpgrade = true)
        {
            var session = GameSession.Instance;
            float multiplier = session.Progression != null ? session.Progression.GetElementDamageMultiplier(element) : 1f;
            if (includeWandUpgrade && session.Shop != null) multiplier *= session.Shop.WandDamageMultiplier(element);
            return multiplier;
        }

        float SelectedDamageMultiplier(int mask)
        {
            float total = 0f;
            for (int element = 0; element < 4; element++)
                if ((mask & (1 << element)) != 0) total += DamageMultiplier(element);
            int count = SpellIngredientRules.Count(mask);
            return count > 0 ? total / count : 1f;
        }

        internal OracleEnemy FindNearestVisible(Vector3 origin, float range)
        {
            targetGrid.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                OracleEnemy enemy = enemies[i];
                if (!enemy.Targetable) continue;
                Vector3 point = enemy.AimPoint;
                float dx = point.x - origin.x, dz = point.z - origin.z;
                if (dx * dx + dz * dz > range * range || !HasLineOfSight(origin, point)) continue;
                targetGrid.Add(enemy.TargetId, point);
            }
            if (!targetGrid.TryFindNearest(origin, range, out int id, out Vector3 pointFound)) return null;
            for (int i = 0; i < enemies.Count; i++) if (enemies[i].TargetId == id && enemies[i].Targetable) return enemies[i];
            return null;
        }

        public bool TryFindNearestVisible(Vector3 origin, float radius, out OracleEnemy enemy)
        {
            enemy = Playing ? FindNearestVisible(origin, radius) : null;
            return enemy != null;
        }

        public void HitEffect(Vector3 position, Color color, float size = .4f)
        { if (Playing) SpawnImpact(position, color, size); }

        bool Launch(OracleProjectile.Kind kind, Vector3 origin, Vector3 direction, float speed, float range,
            float radius, Material material, float damageScale = 1f, int combo = 0, int networkShotId = 0, bool cosmicNova = false,
            int sourceEnemyId = 0, int sourceAttackSerial = 0, int sourceLane = -1, int statusMask = 0, int ingredientMask = 0)
        {
            if (!initialized || projectiles.Count == 0 || !FinitePosition(origin) || !FiniteDirection(direction)) return false;
            OracleProjectile selected = null;
            for (int i = 0; i < projectiles.Count; i++)
            {
                int index = (projectileCursor + i) % projectiles.Count;
                if (!projectiles[index].Flying) { selected = projectiles[index]; projectileCursor = (index + 1) % projectiles.Count; break; }
            }
            if (selected == null && !cosmicNova)
                for(int i=0;i<projectiles.Count;i++)
                {
                    int index=(projectileCursor+i)%projectiles.Count;
                    if(projectiles[index].IsCosmicNova)continue;
                    selected=projectiles[index];projectileCursor=(index+1)%projectiles.Count;break;
                }
            // A finisher requires a free fixed slot; failure consumes nothing.
            // Accepted Nova seeds are never overwritten by ordinary pool reuse.
            if(selected==null)return false;
            int dangerId = 0;
            if (kind == OracleProjectile.Kind.ShadeBolt && sourceEnemyId > 0 && sourceAttackSerial > 0 && sourceLane >= 0)
            { if (hostileGeneration == int.MaxValue) return false; dangerId = ++hostileGeneration; }
            selected.Launch(kind, origin, direction, speed, range, radius, material, damageScale, combo, networkShotId, cosmicNova,
                sourceEnemyId, sourceAttackSerial, sourceLane, dangerId, statusMask, ingredientMask);
            return true;
        }

        internal int CopyHostileDangerState(HostileBoltState[] output)
        {
            if (output == null || output.Length != RemoteDangerPolicy.Capacity || remoteSimulation) return 0;
            int count = 0;
            for (int slot = 0; slot < projectiles.Count && slot < RemoteDangerPolicy.Capacity; slot++)
                if (projectiles[slot].CopyDangerState(slot, out HostileBoltState state)) output[count++] = state;
            for (int i = count; i < output.Length; i++) output[i] = default;
            return count;
        }
        internal void ApplyRemoteDangerState(HostileBoltState[] states, int count)
        { if (RemotePresentation) remoteHostileBolts?.Apply(states, count); }
        internal void ClearRemoteDangerState()
        {
            remoteHostileBolts?.Clear();
            if (RemotePresentation) foreach (OracleEnemy enemy in enemies) if (enemy != null) enemy.ClearRemoteAttackPresentation();
        }
        internal void PlayRemoteHostileImpact(Vector3 point)
        {
            if (!RemotePresentation || !RemoteDangerPolicy.Point(point)) return;
            // Actual host terminal contact only. No hurt/hit confirmation, sound,
            // physics, damage or XP; borrows existing bounded spark/light pools.
            SpawnImpact(point, new Color(1f, .28f, .13f), .35f);
        }

        internal void PlayRemoteHostileLaunch(Vector3 origin)
        {
            if (RemotePresentation && RemoteDangerPolicy.Point(origin))
                GameSession.Instance?.Audio?.PlayProductionCue(ProductionCue.ShadeSpecial, origin);
        }
        internal void LaunchShadeBolt(Vector3 origin, Vector3 direction, float damageScale = 1f,
            int sourceEnemyId = 0, int sourceAttackSerial = 0, int sourceLane = -1)
        {
            if (Playing && Launch(OracleProjectile.Kind.ShadeBolt, origin, direction, 246.675f / UnitsPerMetre, 588f / UnitsPerMetre, .16f, red, damageScale, 0, 0, false, sourceEnemyId, sourceAttackSerial, sourceLane))
                GameSession.Instance.Audio?.PlayProductionCue(ProductionCue.ShadeSpecial, origin);
        }
        internal void LaunchShrineBolt(Vector3 origin, Vector3 target)
        { if (Playing) Launch(OracleProjectile.Kind.ShrineBolt, origin, (target - origin).normalized, 740f / UnitsPerMetre, MissileRange, .11f, gold, DamageMultiplier(SelectedSpell, false)); }

        internal void ProjectileHit(OracleProjectile.Kind kind, OracleEnemy direct, Vector3 point,
            float damageScale = 1f, Vector3 impactDirection = default, int combo = 0, int networkShotId = 0, bool cosmicNova = false, int statusMask = 0, int ingredientMask = 0)
        {
            if (!Playing) return;
            int element = (int)kind - (int)OracleProjectile.Kind.Fire;
            bool elemental = element >= 0 && element < 4;
            ingredientMask = elemental ? SpellIngredientRules.ResolveMask(element, combo, ingredientMask) : 0;
            if (elemental && ingredientMask == 0) return;

            Color color = elemental ? SpellColors[element] : new Color(.5f, 1f, .88f);
            SpawnImpact(point, color, elemental && element == 2 ? .85f : .45f);
            Vector3 effectDirection = impactDirection.sqrMagnitude > .001f ? impactDirection :
                player.ViewCamera.transform.forward;
            bool damaged = false;
            if (direct != null && direct.Targetable)
            {
                float before = direct.HP;
                direct.TakeHit((elemental ? Foundation.CombatConfig.Default.PlayerDamage : 10f) * damageScale);
                damaged = direct.HP < before;
                if (elemental && !cosmicNova) ApplyIngredientEffects(direct, ingredientMask, effectDirection, damageScale, statusMask);
                if (!cosmicNova) direct.ApplyComboEffect(combo, effectDirection, damageScale, statusMask);
            }
            if (cosmicNova && elemental)
            {
                damaged |= ResolveCosmicNova(point, effectDirection, damageScale, networkShotId, statusMask);
                ConfirmProjectileDamage(damaged, true, networkShotId, point);
                return;
            }
            if (!elemental || (ingredientMask & 1) == 0 && combo == 0)
            { ConfirmProjectileDamage(damaged, elemental, networkShotId, point); return; }
            float radius = combo == 1 ? 2.5f : combo == 2 ? 1.8f : combo == 3 ? 2.3f : combo == 4 ? 1.25f : combo == 5 || combo == 6 ? 1.8f : 1.875f;
            int maximum = combo == 0 || combo == 4 ? 2 : 3;
            splashTargets.Clear();
            Vector3 sightOrigin = point - effectDirection.normalized * .06f;
            foreach (OracleEnemy enemy in enemies)
                if (enemy != direct && enemy.Targetable && (enemy.AimPoint - point).sqrMagnitude < radius * radius && HasLineOfSight(sightOrigin, enemy.AimPoint))
                    splashTargets.Add(enemy);
            int count = Mathf.Min(maximum, splashTargets.Count);
            for (int i = 0; i < count; i++)
            {
                float splashDamage = combo == 0 ? 5.5f / count : combo == 2 ? 3f : combo == 4 ? 2f : 4f;
                float before = splashTargets[i].HP;
                splashTargets[i].TakeHit(splashDamage * damageScale);
                damaged |= splashTargets[i].HP < before;
                ApplyIngredientEffects(splashTargets[i], ingredientMask, effectDirection, damageScale, statusMask);
                splashTargets[i].ApplyComboEffect(combo, effectDirection, damageScale, statusMask);
            }
            ConfirmProjectileDamage(damaged, elemental, networkShotId, point);
        }

        static void ApplyIngredientEffects(OracleEnemy target, int mask, Vector3 direction, float damageScale, int statusMask)
        {
            // Four fixed ingredient bits; this is a main-thread actor adapter,
            // not a spatial query/job and it owns no native data or target tracking.
            for (int element = 0; element < 4; element++) if ((mask & (1 << element)) != 0)
            {
                target.ApplyElementEffect(element, direction, statusMask: statusMask);
                if (element == 0) target.ApplyBurn(2f * damageScale, 2f);
            }
        }

        bool ResolveCosmicNova(Vector3 point, Vector3 direction, float damageScale, int shotId, int statusMask)
        {
            int count = 0;
            Vector3 sight = point - direction.normalized * .08f;
            for (int i = 0; i < enemies.Count; i++)
            {
                OracleEnemy enemy = enemies[i];
                if (enemy == null || !enemy.Targetable || CosmicNovaTrialActive && !IsNovaTrialTarget(enemy)) continue;
                float squared = (enemy.AimPoint - point).sqrMagnitude;
                if (squared > CosmicNovaRadius * CosmicNovaRadius || !HasLineOfSight(sight, enemy.AimPoint)) continue;
                int slot = 0;
                while (slot < count && (novaDistances[slot] < squared || novaDistances[slot] == squared && novaTargets[slot].TargetId < enemy.TargetId)) slot++;
                if (slot >= CosmicNovaMaximumTargets) continue;
                int last = Mathf.Min(count, CosmicNovaMaximumTargets - 1);
                for (int j = last; j > slot; j--) { novaTargets[j] = novaTargets[j - 1]; novaDistances[j] = novaDistances[j - 1]; }
                novaTargets[slot] = enemy; novaDistances[slot] = squared;
                count = Mathf.Min(count + 1, CosmicNovaMaximumTargets);
            }
            int damaged = 0;
            for (int i = 0; i < count; i++)
            {
                OracleEnemy enemy = novaTargets[i];
                float before = enemy.HP;
                enemy.TakeHit(CosmicNovaBaseDamage * damageScale);
                ApplyIngredientEffects(enemy, 15, direction, damageScale, statusMask);
                if (enemy.HP < before) damaged++;
                novaTargets[i] = null;
            }
            LastNovaDamagedTargets = damaged; CosmicNovaResolvedCount++;
            int quality = GameSession.Instance.Graphics != null ? GameSession.Instance.Graphics.ParticleQuality : 1;
            PlayCosmicNovaFeedback(point, CosmicNovaRadius, quality);
            CosmicNovaResolved?.Invoke(point, damaged);
            AuthoritativeNovaImpact?.Invoke(shotId, point, CosmicNovaRadius);
            return damaged > 0;
        }

        public void PlayCosmicNovaFeedback(Vector3 point, float radius, int quality)
        {
            var context = GameSession.Instance;
            if (!initialized || context == null || !FinitePosition(point) || float.IsNaN(radius) || float.IsInfinity(radius) ||
                radius <= 0f || !context.SimulationActive) return;
            novaVisuals?.Play(point, Mathf.Min(radius, CosmicNovaRadius), Mathf.Clamp(quality, 0, 2));
            context.Audio?.PlayCosmicNova(point);
        }

        public void ApplyRemoteNovaState(int ingredientMask, float seconds)
        {
            remoteNovaMask = ingredientMask & 15;
            remoteNovaSeconds = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0f : Mathf.Clamp(seconds, 0f, 4f);
        }

        void ResetNovaState()
        {
            novaChain.Reset(); abilityClock = primaryReadyAt = 0d; selection.Clear(); manualClick.Clear(); autoSelectionWait.Clear(); hasRemoteSelection = false; remoteSelectionSequence = 0; RefreshSelectionStatus(); remoteNovaMask = 0; remoteNovaSeconds = novaCastFlash = 0f;
            for (int i = 0; i < 4; i++) { elementReadyAt[i] = 0d; CooldownRemaining[i] = ElementSwitchCooldowns[i] = 0f; }
            OwnCosmicNovaCastCount = CosmicNovaResolvedCount = LastNovaDamagedTargets = 0;
            RestoreTrialShrine();
            var context = GameSession.Instance;
            if (context != null && context.Combat == this && context.Viewmodel != null)
                context.Viewmodel.ResetConfirmedAppearance(0);
            CosmicNovaTrialActive = false;
            for (int i = 0; i < novaTrialTargets.Length; i++) novaTrialTargets[i] = null;
            novaVisuals?.ResetBursts(); towerAttackSlots.Clear();
        }

        public bool BeginCosmicNovaTrial()
        {
            AbortTrainingLab();
            var session = GameSession.Instance;
            if (destroying || !initialized || session == null || session.CoopMode || !session.HasStarted || session.Victory || session.IsDead) return false;
            PrepareStage(5); ResetNovaState();
            CosmicNovaTrialActive = true;
            // Practice belongs to the learner; the autonomous ward must not
            // consume these five rewardless actors while explanations are read.
            trialSuppressedShrine=shrine;
            if(trialSuppressedShrine!=null)
            {trialShrineWasEnabled=trialSuppressedShrine.enabled;trialSuppressedShrine.enabled=false;}
            session.Objective = "Select Fire, Water, Earth and Air, then aim and cast Cosmic Nova";
            for (int i = 0; i < 4; i++) { elementReadyAt[i] = 0d; CooldownRemaining[i] = ElementSwitchCooldowns[i] = 0f; }
            float maxScale = 1f, minScale = float.PositiveInfinity;
            for (int i = 0; i < 4; i++) { float scale = DamageMultiplier(i); maxScale = Mathf.Max(maxScale, scale); minScale = Mathf.Min(minScale, scale); }
            for (int i = 0; i < 5; i++)
            {
                Vector3 position = i == 0 ? new Vector3(0f, .01f, 14.2f) : i == 1 ? new Vector3(-2.5f, .01f, 14.2f) :
                    i == 2 ? new Vector3(2.5f, .01f, 14.2f) : i == 3 ? new Vector3(-1.25f, .01f, 16.5f) : new Vector3(1.25f, .01f, 16.5f);
                OracleEnemy enemy = SpawnGuardian(position, false, false);
                if (enemy == null) { EndCosmicNovaTrial(); return false; }
                enemy.ConfigureDefenseWave((i == 0 ? 40f * maxScale : 20f * minScale) / Foundation.CombatConfig.Default.MonsterHealth, .1f, .05f);
                novaTrialTargets[i] = enemy;
            }
            Tell("Tap Fire, Water, Earth and Air before casting. Aim at the middle target and left-click to launch Cosmic Nova.", 6f);
            return true;
        }

        public void EndCosmicNovaTrial()
        {
            if (!CosmicNovaTrialActive) return;
            CosmicNovaTrialActive = false;
            if(!destroying)RestoreTrialShrine();else trialSuppressedShrine=null;
            // Trial owns these actors. Never allocate a replacement encounter root
            // from guide OnDisable/OnDestroy, nor reuse disposed native containers.
            if (!destroying)
            {
                for (int i=0;i<enemies.Count;i++) if (enemies[i]!=null)
                { enemies[i].gameObject.SetActive(false); Destroy(enemies[i].gameObject); }
                enemies.Clear(); targetGrid?.Clear(); crowdSpacing?.Clear(); towerAttackSlots.Clear();
                foreach (OracleProjectile projectile in projectiles)
                    if(projectile!=null) { projectile.Retire(); projectile.ClearDangerState(); }
                ClearCosmeticShots(); remoteHostileBolts?.Clear(); hostileGeneration=0;
            }
            tutorialGuardian=null; encounterActive=false; portalUnlocked=portalEntered=false;
            novaChain.Reset(); remoteNovaMask=0; remoteNovaSeconds=novaCastFlash=0f;
            for(int i=0;i<novaTrialTargets.Length;i++)novaTrialTargets[i]=null;
            novaVisuals?.ResetBursts();
            if(world!=null)world.LockPortal();
            var context=GameSession.Instance;
            if(context!=null && context.Combat==this)context.Goal=context.Defeated=0;
        }

        bool IsNovaTrialTarget(OracleEnemy enemy)
        {
            for (int i = 0; i < novaTrialTargets.Length; i++) if (novaTrialTargets[i] == enemy) return true;
            return false;
        }

        void RestoreTrialShrine()
        {
            if(trialSuppressedShrine!=null)trialSuppressedShrine.enabled=trialShrineWasEnabled;
            trialSuppressedShrine=null;
        }

        void ConfirmProjectileDamage(bool damaged, bool elemental, int networkShotId, Vector3 point)
        {
            if (!damaged || !elemental) return;
            if (networkShotId > 0) AuthoritativeShotHit?.Invoke(networkShotId, point);
            else HitFlash = 1f;
        }

        internal void HostileProjectileHit(bool hitPlayer, OracleShrine hitShrine, Vector3 point, float damageScale = 1f)
        {
            if (!Playing) return;
            if (hitPlayer && !PeerArenaCombat) GameSession.Instance.TakeDamage(7f * .6375f * damageScale);
            else if (hitShrine != null) hitShrine.TakeDamage(7f * .6375f * damageScale);
            SpawnImpact(point, new Color(1f, .28f, .13f), .35f);
        }

        internal void GuardianDefeated(OracleEnemy enemy)
        {
            if (!Playing) return;
            if (IsNovaTrialTarget(enemy)||IsTutorialTowerPracticeTarget(enemy)||IsTrainingLabTarget(enemy)) return;
            GameSession.Instance.AddKill();
            var session = GameSession.Instance;
            if (session.CoopMode && session.Defense != null && session.Defense.Running && session.Defense.IsAuthority)
                session.Defense.RegisterXpDrop(enemy.TargetId, enemy.transform.position, 1);
            else if (!session.CoopMode && session.Progression != null)
                session.Progression.SpawnXp(enemy.transform.position, 1, tutorialReward: enemy == tutorialGuardian);
            SpawnImpact(enemy.AimPoint, new Color(.4f, 1f, .8f), .9f);
        }
        internal bool IsPlayerCollider(Collider collider)
        {
            return collider!=null&&(collider.GetComponentInParent<RemoteOracleAvatar>()!=null||
                player!=null&&(collider.transform==player.transform||collider.transform.IsChildOf(player.transform)));
        }

        internal bool HasLineOfSight(Vector3 from, Vector3 to, bool ignoreShrine = false)
        {
            Vector3 offset = to - from;
            if (offset.sqrMagnitude < .001f) return true;
            int count = QueryRay(from, offset.normalized, offset.magnitude, out RaycastHit[] hits);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (ignoreShrine && hit.collider.GetComponentInParent<OracleShrine>() != null) continue;
                if (IsPlayerCollider(hit.collider) || hit.collider.GetComponentInParent<OracleEnemy>() != null ||
                    hit.collider.GetComponentInParent<OracleProjectile>() != null) continue;
                return false;
            }
            return true;
        }

        internal bool CanMove(OracleEnemy enemy, Vector3 movement)
        {
            if (movement.sqrMagnitude < .00001f) return false;
            Vector3 foot = enemy.transform.position;
            // Admission-cached body capsule follows the actual upright mesh, in world metres.
            int count = Physics.CapsuleCastNonAlloc(foot + Vector3.up * enemy.NavigationBottom,
                foot + Vector3.up * enemy.NavigationTop, enemy.NavigationRadius, movement.normalized, capsuleHits,
                movement.magnitude + .04f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = capsuleHits;
            if (count == capsuleHits.Length)
            {
                hits = Physics.CapsuleCastAll(foot + Vector3.up * enemy.NavigationBottom, foot + Vector3.up * enemy.NavigationTop,
                    enemy.NavigationRadius, movement.normalized, movement.magnitude + .04f, ~0, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider.transform == enemy.transform || hit.collider.transform.IsChildOf(enemy.transform)) continue;
                if (hit.collider.GetComponentInParent<OracleProjectile>() != null) continue;
                OracleEnemy other = hit.collider.GetComponentInParent<OracleEnemy>();
                if (other != null || IsPlayerCollider(hit.collider)) continue;
                return false;
            }
            // No unsupported ledges: keep the feet above actual dungeon flooring.
            if (enemy.IsFlying) return true;
            Vector3 next = foot + movement;
            return Physics.Raycast(next + Vector3.up * .35f, Vector3.down, 1f, ~0, QueryTriggerInteraction.Ignore);
        }

        int QueryRay(Vector3 origin, Vector3 direction, float distance, out RaycastHit[] hits)
        {
            hits = rayHits;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            // Saturation is rare in this small scene. A complete fallback preserves occlusion.
            if (count == hits.Length)
            {
                hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            return count;
        }

        internal int QueryProjectileSweep(Vector3 origin, float radius, Vector3 direction,
            float distance, out RaycastHit[] hits)
        {
            hits = sphereHits;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits,
                distance, ~0, QueryTriggerInteraction.Collide);
            if (count == hits.Length)
            {
                hits = Physics.SphereCastAll(origin, radius, direction, distance, ~0, QueryTriggerInteraction.Collide);
                count = hits.Length;
            }
            int kept = 0;
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.isTrigger || hits[i].collider.GetComponentInParent<OracleEnemy>() != null)
                    hits[kept++] = hits[i];
            return kept;
        }

        internal void SpawnImpact(Vector3 point, Color color, float size)
        {
            if (GameSession.Instance != null && GameSession.Instance.SpellLights != null)
                GameSession.Instance.SpellLights.Pulse(point, color, size);
            if (impacts.Count == 0) return;
            Impact effect = impacts[impactCursor];
            impactCursor = (impactCursor + 1) % impacts.Count;
            effect.Root.position = point;
            effect.Root.localScale = Vector3.one;
            effect.Size = size;
            effect.Duration = effect.Remaining = .3f;
            effect.Root.gameObject.SetActive(true);
            impactBlock.SetColor("_BaseColor", color);
            impactBlock.SetColor("_Color", color);
            impactBlock.SetColor("_EmissionColor", color * 1.3f);
            foreach (Renderer renderer in effect.Renderers) renderer.SetPropertyBlock(impactBlock);
        }

        void UpdateImpacts(float dt)
        {
            foreach (Impact effect in impacts)
            {
                if (effect.Remaining <= 0f) continue;
                effect.Remaining = Mathf.Max(0f, effect.Remaining - dt);
                float t = 1f - effect.Remaining / effect.Duration;
                for (int i = 0; i < effect.Renderers.Length; i++)
                {
                    Transform bit = effect.Renderers[i].transform;
                    float angle = i * Mathf.PI * 2f / 3f;
                    bit.localPosition = new Vector3(Mathf.Cos(angle), .3f + i * .18f, Mathf.Sin(angle)) * t * effect.Size;
                    bit.localScale = Vector3.one * Mathf.Max(.004f, .12f * (1f - t) * effect.Size);
                }
                if (effect.Remaining <= 0f) effect.Root.gameObject.SetActive(false);
            }
        }

        void Tell(string text, float seconds) { message = text; messageRemaining = seconds; InteractionHint = text; }

        internal static Transform CreateBlock(Transform parent, string label, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = label;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        static Material MakeMaterial(string label, Color color, float glow = 0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = label };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .2f);
            if (glow > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * glow);
            }
            return material;
        }

        void OnDestroy()
        {
            destroying = true;
            AbortTrainingLab();
            EndTutorialTowerPractice();
            remoteHostileBolts?.Dispose(); remoteHostileBolts = null;
            if (targetGrid != null) targetGrid.Dispose();
            if (crowdSpacing != null) crowdSpacing.Dispose();
            if (encounterRoot != null) Destroy(encounterRoot.gameObject);
            foreach (Material material in elementMaterials) if (material != null) Destroy(material);
            Material[] materials = { stone, darkStone, turquoise, gold, violet, red, pale, barBack };
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Unity metres, Y-up/XZ; four ingredient bits and caller-owned four-double recovery timestamps.
// Existing host/solo owners retain simulation clocks, pooled48 projectiles and private native memory lifetimes.

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ actor space, host Forge ledger and sole combat owners remain.
// Exact composed Refuge/interstage/balance adapter hunks; no new native/save authority.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Y-up refuge/Canvas and true floor contact.
// Array Formats: Fixed activity anchors, existing bounded combat/room owners and event-only UI.
// Dependencies/Pointers: Root-integrated tutorial activities retain the connected party and use the existing Skip/Resume authority.

// # ARCHITECTURE SNAPSHOT
// Bounded offline tutorial adapter; existing actor/HP/tower/native owners and reward boundaries remain.
