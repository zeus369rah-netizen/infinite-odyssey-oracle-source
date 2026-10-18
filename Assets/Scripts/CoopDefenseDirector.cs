using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace OdysseyOracle
{
    // The connected host owns stage time, enemy admissions, damage simulation and building.
    // Clients render accepted snapshots; campaign stages use the bounded150s/100-admission plan.
    // # ARCHITECTURE SNAPSHOT
    // Director owns the confirmed run, admission/geometry masks and packet validation.
    // Peer health owns fixed host records/tickets; combat owns main-thread collision.
    // Common state carries public health; complete private progression is owner-only.
    public sealed partial class CoopDefenseDirector : MonoBehaviour
    {
        public bool Running { get; private set; }
        public bool ArenaReady => Running && clockStarted;
        public string CurrentRunId=>runId??string.Empty;
        public double SimulationClock=>simulationClock;
        public bool RoomAdmissionsBlocked=>Running||(owner!=null&&owner.Loading!=null&&owner.Loading.IsLoading);
        public bool PeerHealthAuthoritative=>Running&&peerHealth!=null&&peerHealth.RunMatches(CurrentRunId);
        public bool TryGetPeerHealth(ulong id,out PeerHealthSnapshot state)
        {state=default;return peerHealth!=null&&peerHealth.TryGetPeerHealth(id,out state);}
        public int CopyPeerCombatTargets(PeerCombatTarget[] output)=>peerHealth!=null?peerHealth.CopyPeerCombatTargets(output):0;
        public bool AuthorizePeerAttack(int source,int serial,ulong target,Vector3 origin,float range,bool projectile)=>
            peerHealth!=null&&peerHealth.AuthorizePeerAttack(source,serial,target,origin,range,projectile);
        public bool AuthorizeLaneProjectile(int source,int serial,Vector3 origin,float range)=>
            peerHealth!=null&&peerHealth.AuthorizeLaneProjectile(source,serial,origin,range);
        public bool ApplyPeerDamage(ulong id,float amount,int source,int serial)=>peerHealth!=null&&peerHealth.ApplyPeerDamage(id,amount,source,serial);
        public bool TryGetEnemyLane(int source,out int lane)=>enemyLanes.TryGetValue(source,out lane);
        public int ActiveLaneMask=>activeLaneMask;
        public int AdmissionLaneMask=>admissionLaneMask;
        public int AdmissionLaneCount=>CoopPartyCapacity.LaneCount(admissionLaneMask);
        public int DrainingLaneCount=>CoopPartyCapacity.LaneCount(activeLaneMask&~admissionLaneMask);
        public const int PartyProtocolVersion=7;
        public bool IsAuthority => rooms != null && rooms.Connected && rooms.IsHost;
        public float RemainingSeconds { get; private set; } = CampaignStagePacing.StageSeconds;
        float DefenseDurationSeconds=>CampaignActive?CampaignStagePacing.StageSeconds:(float)CurrentDefensePacing.StageSeconds;
        const string CampaignDurationLabel="150 simulation seconds · 100 enemies";
        string DefenseDurationLabel=>CampaignActive?CampaignDurationLabel:"ten minutes";
        CampaignStageAdmissions campaignAdmissions;
        public string Status { get; private set; } = "The host can start a ten-minute shrine defense.";
        public int LocalBuildTokens => IsAuthority && peers.TryGetValue(rooms.LocalClientId, out var peer) ? peer.Tokens : localTokens;
        public int PlacedCount => buildings.Count;
        public int ActiveLaneCount=>CoopPartyCapacity.LaneCount(activeLaneMask);
        public int ActiveTowerCapacity=>MaximumPlacedShrines;
        public int WaveNumber { get; private set; }
        public int SpawnedThisWave { get; private set; }
        public int WaveQuota { get; private set; }
        public int AliveCount { get; private set; }
        public const int MaximumPlacedShrines = 4;
        public const int MaximumPerLane = 2;
        public const int TowerProtocolVersion = 5;
        public const int NovaProtocolVersion = 5;

        // Original 003 candidate: finite per-lane admissions, soft two-alive gate.
        readonly int[] laneAdmissions = new int[CoopPartyCapacity.MaximumPlayers];
        readonly int[] laneAlive = new int[CoopPartyCapacity.MaximumPlayers];
        readonly Dictionary<int, int> enemyLanes = new Dictionary<int, int>(CampaignStagePacing.AdmissionsPerStage);
        int laneCursor,activeLaneMask,admissionLaneMask,correctionSerial,appliedCorrectionSerial;
        double nextPersonalSync;
        PeerCombatAuthority peerHealth;
        bool ownsPeerHealth;
        readonly List<PeerHealthState> publicHealthBuffer=new List<PeerHealthState>(8);
        readonly PeerCombatTarget[] drainPeers=new PeerCombatTarget[8];
        static readonly PlayerState[] NoCommonPersonal=new PlayerState[0];
        public int MissingHealthSnapshots{get;private set;}

        [Serializable] sealed class EnemyState
        {
            public int id, archetype;
            public bool shade, flying;
            public Vector3 position;
            public float hp, maximum;
            [SerializeField] int q, p, s,l;
            public int lane{get=>l;set=>l=value;}
            [SerializeField] float u, w, t, r;
            [SerializeField] bool a;
            [SerializeField] Vector3 d;
            public int attackSequence { get => q; set => q = value; }
            public int attackPhase { get => p; set => p = value; }
            public int attackStyle { get => s; set => s = value; }
            public float attackProgress { get => u; set => u = value; }
            public float windupSeconds { get => w; set => w = value; }
            public float strikeSeconds { get => t; set => t = value; }
            public float recoverySeconds { get => r; set => r = value; }
            public bool aggro { get => a; set => a = value; }
            public Vector3 lockedDirection { get => d; set => d = value; }
            [SerializeField] int b;
            [SerializeField] Vector3 c;
            public int targetTowerId { get => b; set => b = value; }
            public Vector3 targetPoint { get => c; set => c = value; }
        }
        [Serializable] sealed class BuildingState
        {
            public int id, element;
            public int tier=1,tierRevision;
            public ulong owner;
            public Vector3 position;
            public float health = PlacedElementShrine.DefaultMaxHealth;
        }
        [Serializable] sealed class PlayerState
        {
            public ulong id;
            public bool autoFire;
            public int preparedElement = -1, selectionMask, confirmedElement = -1;
            public uint selectionSequence;
            public int tokens, element, combo;
            public PeerHealthState health;
            public int forgeVersion,forgeBank,forgeTiers,forgeRevision,forgeOutcome;
            public uint forgeLastRequest;
            public int level, xp, points, mask;
            public int[] claimed;
            public float[] returnCooldowns;
            public float cooldown, comboSeconds;
            [SerializeField] int n;
            [SerializeField] float f;
            public int novaMask { get => n; set => n=value; }
            public float novaSeconds { get => f; set => f=value; }
        }
        [Serializable] sealed class DropState { public int id, amount; public Vector3 position; }
        [Serializable] sealed class PresentationMessage
        {
            public string kind, run;
            public int eventSequence, shotId, towerId, element, combo, secondaryCount;
            public ulong casterId;
            public Vector3 origin, direction, point, secondaryA, secondaryB;
            public bool cosmicNova;
            public float radius;
        }
        struct PendingShot
        {
            public ulong Caster;
            public Vector3 Origin;
            public double Expires;
            public bool Nova, HitConfirmed, ImpactConfirmed;
        }
        [Serializable] sealed class Message
        {
            public string kind, run, campaign;
            public int campaignStage,campaignEpoch,campaignVersion;
            public CampaignProgressSnapshot campaignProgress;
            public string text = string.Empty;
            public bool hasPersonal;
            public int sequence, result, element, node, dropId, activeLanes, wave, spawned, quota;
            public bool running, clockStarted;
            public bool enabled;
            public uint controlSequence, selectionSequence, castSequence;
            public int selectionMask;
            public int forgeExpectedTier,forgeExpectedRevision,forgeOutcome;
            public uint forgeRequestId;
            [SerializeField] int tv;
            public int towerVersion {get=>tv;set=>tv=value;}
            public float remaining, shrine;
            public Vector3 position, direction;
            public EnemyState[] enemies;
            public BuildingState[] buildings;
            public PlayerState[] players;
            public PlayerState personal;
            public PeerHealthState[] health;
            [SerializeField] int pv,am,gm,cs;
            public int partyVersion{get=>pv;set=>pv=value;}
            public int admissionMask{get=>am;set=>am=value;}
            public int geometryMask{get=>gm;set=>gm=value;}
            public int correctionSequence{get=>cs;set=>cs=value;}
            public DropState[] drops;
            public int dangerVersion, hostileCount;
            [SerializeField] int nv;
            public int novaVersion { get => nv; set => nv=value; }
            public HostileBoltState[] hostileBolts;
        }
        sealed class Peer
        {
            public int Tokens = 3;
            public readonly MatchTowerForgeAccount Forge=new MatchTowerForgeAccount();
            public double NextForgeRequest,NextPersonalSync;
            public int Element;
            public bool ArenaReady;
            public bool AutoFireEnabled;
            public ConfirmedSelection Selection;
            public CosmicNovaChain Nova;
            public ConfirmedRequestSequence TapRequests, CastRequests;
            public uint AutoFireSequence;
            public double NextAutoFireChange;
            public double NextCast;
            public AutoSelectionWait AutoSelectionWait;
            public readonly double[] ReturnReadyAt = new double[4];
            public int Level = 1, Xp, Points, Mask;
            public int CampaignProgressRevision;
            public readonly HashSet<int> Claimed = new HashSet<int>();
        }

        readonly Dictionary<ulong, Peer> peers = new Dictionary<ulong, Peer>();
        readonly Dictionary<int, OracleEnemy> replicas = new Dictionary<int, OracleEnemy>();
        readonly List<BuildingState> buildings = new List<BuildingState>(MaximumPlacedShrines);
        readonly List<EnemyState> enemyBuffer = new List<EnemyState>(OracleCombat.MaxActiveEnemies);
        readonly List<int> removed = new List<int>(OracleCombat.MaxActiveEnemies);
        readonly List<ulong> missingPeers = new List<ulong>(4);
        readonly HashSet<int> snapshotIds = new HashSet<int>();
        readonly Dictionary<int, DropState> drops = new Dictionary<int, DropState>();
        readonly Queue<int> dropOrder = new Queue<int>(128);
        readonly PickupVisibilityQuery xpClaimVisibility = new PickupVisibilityQuery();
        readonly HashSet<int> liveDropIds = new HashSet<int>();
        readonly Dictionary<int, PendingShot> pendingShots = new Dictionary<int, PendingShot>(128);
        readonly List<int> expiredShots = new List<int>(128);
        // Mandatory danger rides existing10Hz snapshots, separate from the
        // best-effort32/s shot cosmetic limiter. Party capacity is eight peers.
        readonly HostileBoltState[] hostileStateBuffer = new HostileBoltState[RemoteDangerPolicy.Capacity];
        readonly bool[] dangerEnemyIds = new bool[RemoteDangerPolicy.MaximumActorId + 1];
        readonly bool[] dangerBoltSlots = new bool[RemoteDangerPolicy.Capacity];
        readonly RemoteDangerRunGate dangerRunGate = new RemoteDangerRunGate();
        public int OversizedDangerSnapshots { get; private set; }
        readonly bool[] seenTowerPads=new bool[4];
        readonly MatchTowerForgeClientState localForge=new MatchTowerForgeClientState();
        NpcBlacksmith forgeSmith;
        GameSession owner;
        RoomConnection rooms;
        OracleCombat feedbackCombat;
        string runId;
        double simulationClock, nextSpawn, nextSnapshot, nextReadyMessage;
        int enemySerial, buildingSerial, snapshotSerial, appliedSerial, result, waveIndex;
        int localTokens = 3, activeLaneCount;
        int shotSerial, presentationSerial, appliedPresentationSerial, appliedShotSerial, presentationCount, presentationBytes;
        uint localAutoFireSequence, localSelectionSequence, localCastSequence;
        double presentationWindow;
        bool towerFeedbackSubscribed;
        bool hasRun, endingApplied, clockStarted;

        public void Initialize(GameSession session)
        {
            owner = session;
            peerHealth=GetComponent<PeerCombatAuthority>();
            if(peerHealth==null){peerHealth=gameObject.AddComponent<PeerCombatAuthority>();ownsPeerHealth=true;}
            peerHealth.Initialize(owner,this);
            peerHealth.HealthChanged+=OnPeerHealthChanged;peerHealth.PoseCorrectionRequired+=OnPoseCorrection;
            SubscribeRoom();
            SubscribeFeedback();
        }

        void SubscribeFeedback()
        {
            var current = owner != null ? owner.Combat : null;
            if (current != feedbackCombat)
            {
                if (feedbackCombat != null) { feedbackCombat.AuthoritativeShotHit -= OnAuthoritativeShotHit; feedbackCombat.AuthoritativeNovaImpact -= OnAuthoritativeNovaImpact; }
                feedbackCombat = current;
                if (feedbackCombat != null) { feedbackCombat.AuthoritativeShotHit += OnAuthoritativeShotHit; feedbackCombat.AuthoritativeNovaImpact += OnAuthoritativeNovaImpact; }
            }
            if (!towerFeedbackSubscribed)
            {
                PlacedElementShrine.AuthoritativeShot += OnAuthoritativeTowerShot;
                towerFeedbackSubscribed = true;
            }
        }

        void SubscribeRoom()
        {
            var current = owner != null ? owner.Rooms : RoomConnection.Instance;
            if (current == rooms) return;
            if (rooms != null) rooms.ReliableMessageReceived -= OnMessage;
            dangerRunGate.ResetConnection();
            owner?.Combat?.ClearRemoteDangerState();
            owner?.Combat?.ApplyRemoteNovaState(0,0f);
            rooms = current;
            if (rooms != null) rooms.ReliableMessageReceived += OnMessage;
        }

        public void StartDefense()
        {
            SubscribeRoom();
            if(Running||owner==null||rooms==null||!rooms.Connected)return;
            if(peerHealth==null||!peerHealth.Initialized){Status="Peer combat is not initialized; defense was not started.";return;}
            ResetLocalState();
            owner.Combat.ClearEnemies();
            owner.Combat.SetRemoteSimulation(!IsAuthority);
            if (owner.Builder != null) owner.Builder.ClearPlacedShrines();
            if (owner.Progression != null) owner.Progression.ResetNetworkXp();
            if (owner.Combat.Shrine != null) owner.Combat.Shrine.ResetShrine();
            Running = hasRun = true;
            RemainingSeconds = DefenseDurationSeconds;
            nextSpawn = CampaignActive?CampaignStagePacing.FirstAdmissionAt:CurrentDefensePacing.GetPhase(0).StartsAt;
            WaveNumber = 1;
            if (IsAuthority)
            {
                runId = Guid.NewGuid().ToString("N");
                if(!peerHealth.ResetRun(runId)){Running=hasRun=false;Status="Peer combat could not prepare this run.";return;}
                ReconcilePeers();
                WaveQuota = CampaignActive?CampaignStagePacing.AdmissionsPerStage:ActiveLaneCount;
                SendPersonal(rooms.LocalClientId, 0);
                if (owner.Combat.Shrine != null) owner.Combat.Shrine.Activate();
                Status = "Waiting for every connected player to enter the arena...";
                SendState();
            }
            else Status = "Waiting for the host's defense state...";
            owner.Objective = Status;
        }

        public void ResetDefense()
        {
            Running = hasRun = false;
            ResetLocalState();
            RemainingSeconds = DefenseDurationSeconds;
            Status = "The host can start a ten-minute shrine defense.";
            if (owner != null && owner.Combat != null)
            {
                owner.Combat.ClearEnemies();
                owner.Combat.SetRemoteSimulation(false);
            }
            if (owner != null && owner.Builder != null) owner.Builder.ClearPlacedShrines();
            if (owner != null && owner.Progression != null) owner.Progression.ResetNetworkXp();
        }

        void ResetLocalState()
        {
            dangerRunGate.EndRun();
            owner?.Combat?.ClearRemoteDangerState();
            peerHealth?.ClearRun();owner?.Combat?.InvalidatePeerTargetCache();
            if(preserveCampaignPeers)ResetCampaignPeerTransients();else peers.Clear();
            enemyLanes.Clear();
            Array.Clear(laneAdmissions, 0, laneAdmissions.Length);
            Array.Clear(laneAlive, 0, laneAlive.Length);
            laneCursor = 0;campaignAdmissions.Reset();
            replicas.Clear();
            buildings.Clear();
            snapshotIds.Clear();
            drops.Clear();
            dropOrder.Clear();
            liveDropIds.Clear();
            localTokens = 3;
            if(preserveCampaignPeers)localForge.ReleasePendingAfterTimeout();else localForge.ResetForRun();
            activeLaneCount=activeLaneMask=admissionLaneMask=correctionSerial=appliedCorrectionSerial=0;nextPersonalSync=0d;
            enemySerial = buildingSerial = snapshotSerial = appliedSerial = result = 0;
            waveIndex = WaveNumber = SpawnedThisWave = WaveQuota = AliveCount = 0;
            localAutoFireSequence = localSelectionSequence = localCastSequence = 0;
            runId = null;
            endingApplied = clockStarted = false;
            simulationClock = nextSnapshot = nextReadyMessage = 0d;
            ResetPresentationState();
        }

        void ResetPresentationState()
        {
            owner?.Combat?.ApplyRemoteNovaState(0,0f);
            pendingShots.Clear();
            expiredShots.Clear();
            shotSerial = presentationSerial = appliedPresentationSerial = appliedShotSerial = presentationCount = presentationBytes = 0;
            presentationWindow = Time.realtimeSinceStartupAsDouble;
        }

        void Update()
        {
            if (owner == null) return;
            SubscribeRoom();
            SubscribeFeedback();
            if (!hasRun) return;
            if(!string.IsNullOrEmpty(runId)&&(peerHealth==null||!peerHealth.RunMatches(runId)))
            {Running=false;Status="Peer combat authority ended; restart the defense.";owner.Pause();return;}
            if (rooms == null || !rooms.Connected)
            {
                dangerRunGate.ResetConnection();
                peerHealth?.ClearRun();owner.Combat.InvalidatePeerTargetCache();
                owner.Combat.ClearRemoteDangerState();
                owner.Combat.ApplyRemoteNovaState(0,0f);
                if (Running)
                {
                    Running = false;
                    Status = "The game connection ended. Reconnect before starting another defense.";
                    owner.Combat.ClearEnemies();
                    owner.Pause();
                }
                return;
            }
            if (!IsAuthority)
            {
                if(Running&&clockStarted&&Time.realtimeSinceStartupAsDouble>=nextPersonalSync)
                {nextPersonalSync=Time.realtimeSinceStartupAsDouble+2d;rooms.SendReliable(JsonUtility.ToJson(new Message{kind="oracle.personal.sync.v1",run=runId,partyVersion=PartyProtocolVersion}));}
                PrunePendingShots();
                return;
            }
            ReconcilePeers();
            peerHealth.CaptureLocalPose();
            RefreshTowerHealth();
            AliveCount = CountAliveEnemies();
            PrunePendingShots();
            if (Running)
            {
                if (!clockStarted && AllPeersReady())
                {
                    clockStarted = true;
                    Status = "Defend the green shrine · " + ActiveLaneCount + " active portal" + (ActiveLaneCount == 1 ? "" : "s") + " · " + DefenseDurationLabel;
                }
                if (clockStarted && owner.SimulationActive)
                {
                    // Enemies and projectiles advance with Time.deltaTime too. A suspended tab
                    // must not receive wall-clock survival credit for frames it never simulated.
                    simulationClock += Mathf.Clamp(Time.deltaTime, 0f, Time.maximumDeltaTime);
                    if(CampaignActive)simulationClock=Math.Min(CampaignStagePacing.MaximumStageSeconds,simulationClock);
                    RemainingSeconds = Mathf.Max(0f, DefenseDurationSeconds - (float)simulationClock);
                    bool shrineIntact=owner.Combat.Shrine!=null&&!owner.Combat.Shrine.IsDestroyed;
                    if(CampaignActive)
                    {
                        if(shrineIntact)RunSpawns(simulationClock);
                        var outcome=CampaignStagePacing.Outcome(simulationClock,campaignAdmissions.Count,shrineIntact);
                        if(outcome!=CampaignStageOutcome.Running)EndDefense(outcome==CampaignStageOutcome.Succeeded);
                        else
                        {
                            if(RemainingSeconds<=0f)Status="Finishing enemy admissions · "+campaignAdmissions.Count+"/100 · up to30 seconds grace";
                            AutoCastPlayers(simulationClock);
                        }
                    }
                    else if(!shrineIntact)EndDefense(false);
                    else if(RemainingSeconds<=0f)EndDefense(true);
                    else{RunSpawns(simulationClock);AutoCastPlayers(simulationClock);}
                }
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextSnapshot)
            {
                nextSnapshot = now + (Running ? .1d : .5d);
                SendState();
            }
        }

        void ReconcilePeers()
        {
            if(!IsAuthority)return;
            missingPeers.Clear();foreach(var item in peers)if(!ConnectedId(item.Key))missingPeers.Add(item.Key);
            foreach(ulong id in missingPeers){peers.Remove(id);peerHealth.RemovePeer(id);owner.Combat.InvalidatePeerTargetCache();}
            foreach(ulong id in rooms.ConnectedClientIds)
            {
                if(!rooms.TryGetAdmissionSlot(id,out int slot))continue;
                if(!peers.TryGetValue(id,out Peer peer))
                {peer=new Peer{NextCast=simulationClock+1d,ArenaReady=id==rooms.LocalClientId&&owner.World!=null&&owner.World.IsCoopArena};peers[id]=peer;}
                peerHealth.ReconcilePeer(id,slot,peer.ArenaReady);
            }
            RefreshActiveLanes();
        }

        void RefreshActiveLanes()
        {
            int mask=0;
            foreach(var item in peers)if(item.Value.ArenaReady&&rooms.TryGetAdmissionSlot(item.Key,out int slot))
                mask|=1<<(CampaignActive?CampaignStagePacing.LaneForAdmissionSlot(slot,campaign.StageNumber):CoopPartyCapacity.LaneForAdmissionSlot(slot));
            if(mask==0)mask=1<<(CampaignActive?CampaignStagePacing.StageRoadOffset(campaign.StageNumber):0);
            ConfigureLaneMask(mask);
        }

        void ConfigureLaneMask(int requestedMask)
        {
            if(!CoopPartyCapacity.ValidLaneMask(requestedMask)||owner.World==null||!owner.World.IsCoopArena)return;
            admissionLaneMask=requestedMask;owner.World.ConfigureActiveLaneMask(requestedMask);
            int bolts=owner.Combat.CopyHostileDangerState(hostileStateBuffer);
            int peerCount=peerHealth.CopyPeerCombatTargets(drainPeers);
            for(int lane=0;lane<8;lane++)if((owner.World.DrainingLaneMask&(1<<lane))!=0)
            {
                bool occupied=false;
                foreach(var enemy in owner.Combat.ActiveEnemies)
                    if(enemy!=null&&enemy.Alive&&enemyLanes.TryGetValue(enemy.TargetId,out int actorLane)&&actorLane==lane){occupied=true;break;}
                for(int i=0;!occupied&&i<bolts;i++)if(hostileStateBuffer[i].lane==lane)occupied=true;
                // Remote peer bodies have no host PhysX collider. Preserve their
                // validated poses too, including downed/shielded allies; the10m
                // radius is conservative before the007 road barrier starts at16m.
                for(int i=0;!occupied&&i<peerCount;i++)if(drainPeers[i].Lane==lane)
                {Vector3 delta=drainPeers[i].Feet-owner.World.ShrinePosition;delta.y=0f;if(delta.sqrMagnitude>=100f)occupied=true;}
                if(!occupied)owner.World.CompleteLaneDrain(lane);
            }
            activeLaneMask=owner.World.ActiveLaneMask;activeLaneCount=CoopPartyCapacity.LaneCount(activeLaneMask);
            if(hasRun)WaveQuota=CampaignActive?CampaignStagePacing.AdmissionsPerStage:Mathf.Max(SpawnedThisWave,CurrentDefensePacing.GetPhase(waveIndex).PerLaneQuota*AdmissionLaneCount);
            // Four shared bastions are independent of membership lanes. Departure
            // stops new admissions and drains actors/bolts; no teleport or refund.
        }

        bool ConnectedId(ulong id)
        {
            foreach (ulong connected in rooms.ConnectedClientIds) if (connected == id) return true;
            return false;
        }

        bool AllPeersReady()
        {
            if (peers.Count == 0) return false;
            foreach (var peer in peers.Values) if (!peer.ArenaReady) return false;
            return true;
        }

        void AcknowledgeArena(string preparedRun, int capability, int novaVersion, int towerVersion,int partyVersion)
        {
            if (capability != RemoteDangerPolicy.Version || novaVersion != NovaProtocolVersion || towerVersion != TowerProtocolVersion||partyVersion!=PartyProtocolVersion)
            { Status = "Incompatible combat presentation version. Update both players."; return; }
            if (owner.Loading != null && owner.Loading.IsLoading && !owner.Loading.IsLocallyPrepared) return;
            if (IsAuthority || !hasRun || !Running || owner.World == null || !owner.World.IsCoopArena || string.IsNullOrEmpty(preparedRun)) return;
            if (!dangerRunGate.Prepare(preparedRun)) return;
            if (runId != preparedRun)
            {
                owner.Combat.ClearEnemies(); replicas.Clear(); snapshotIds.Clear(); ResetPresentationState();
                string previousRun=runId;runId=preparedRun;
                if(peerHealth==null||!peerHealth.ResetRun(runId)){runId=previousRun;Status="Peer combat is not prepared.";return;}
                if(CampaignActive)localForge.ReleasePendingAfterTimeout();else localForge.ResetForRun();nextPersonalSync=0d;
                appliedSerial = 0;
                    nextReadyMessage = 0d;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextReadyMessage) return;
            nextReadyMessage = now + 1d;
            rooms.SendReliable(JsonUtility.ToJson(new Message { kind = "oracle.ready.v1", run = runId, dangerVersion = RemoteDangerPolicy.Version, novaVersion = NovaProtocolVersion, towerVersion = TowerProtocolVersion,partyVersion=PartyProtocolVersion }));
        }

        void RunSpawns(double now)
        {
            if(CampaignActive){RunCampaignSpawns(now);return;}
            int phase=CurrentDefensePacing.PhaseAt(now);
            if(phase<0||now>=CurrentDefensePacing.StageSeconds||AdmissionLaneCount<1)return;
            if(phase!=waveIndex)
            {waveIndex=phase;WaveNumber=phase+1;SpawnedThisWave=0;Array.Clear(laneAdmissions,0,laneAdmissions.Length);}
            var plan=CurrentDefensePacing.GetPhase(phase);
            WaveQuota=Mathf.Max(SpawnedThisWave,plan.PerLaneQuota*AdmissionLaneCount);
            if(now<nextSpawn||now<plan.StartsAt||SpawnedThisWave>=WaveQuota)return;
            Array.Clear(laneAlive,0,laneAlive.Length);
            foreach(var enemy in owner.Combat.ActiveEnemies)
                if(enemy!=null&&enemy.Alive&&enemyLanes.TryGetValue(enemy.TargetId,out int lane)&&lane>=0&&lane<8)laneAlive[lane]++;
            int candidate=MaskedDefensePacing.FindLane(laneAlive,laneAdmissions,admissionLaneMask,laneCursor,phase);
            if(candidate<0||!CanAdmitWaveEnemy(AliveCount,SpawnedThisWave,WaveQuota)){nextSpawn=now+.2d;return;}
            laneCursor=(candidate+1)&7;
            if(!SpawnFromLane(candidate,(float)now)){nextSpawn=now+.2d;return;}
            laneAdmissions[candidate]++;SpawnedThisWave++;AliveCount++;
            nextSpawn=now+plan.PerLaneSpacing/AdmissionLaneCount;
        }

        void RunCampaignSpawns(double now)
        {
            if(!IsAuthority||AdmissionLaneCount<1)return;
            waveIndex=CampaignStagePacing.PhaseAt(now);WaveNumber=waveIndex+1;
            WaveQuota=CampaignStagePacing.AdmissionsPerStage;
            if(!campaignAdmissions.CanAccept(now,AliveCount))return;
            Array.Clear(laneAlive,0,laneAlive.Length);
            foreach(var enemy in owner.Combat.ActiveEnemies)
                if(enemy!=null&&enemy.Alive&&enemyLanes.TryGetValue(enemy.TargetId,out int lane)&&lane>=0&&lane<8)laneAlive[lane]++;
            int candidate=CampaignStagePacing.FindLane(laneAlive,laneAdmissions,admissionLaneMask,laneCursor);
            if(candidate<0||!SpawnFromLane(candidate,(float)now))return;
            // Source admission, actor ID and count advance ONLY after a real
            // successful spawn. Bounded cap/pool failures consume nothing.
            if(!campaignAdmissions.CommitAccepted(now))throw new InvalidOperationException("Campaign admission context drifted.");
            laneCursor=(candidate+1)&7;laneAdmissions[candidate]++;
            SpawnedThisWave=campaignAdmissions.Count;AliveCount++;
        }

        public static bool CanAdmitWaveEnemy(int alive, int admitted, int quota)
        {
            return alive >= 0 && alive < OracleCombat.MaxActiveEnemies && admitted >= 0 &&
                quota > 0 && quota <= 200 && admitted < quota;
        }

        int CountAliveEnemies()
        {
            int count = 0;
            foreach (var enemy in owner.Combat.ActiveEnemies) if (enemy != null && enemy.Alive) count++;
            return count;
        }

        bool SpawnFromLane(int lane, float elapsed)
        {
            if(!owner.World.TryGetActiveInvasionPortal(lane,out Vector3 portal))return false;
            var profile = CampaignActive?CampaignStagePacing.EnemyAt(campaign.StageNumber,campaignAdmissions.Count):CurrentDefensePacing.EnemyAt(waveIndex,laneAdmissions[lane]);
            var archetype = (OracleEnemy.MonsterArchetype)profile.Archetype;
            int id = enemySerial + 1;
            var enemy = owner.Combat.SpawnEnemy(portal, id, archetype, profile.Flying);
            if (enemy == null) return false;
            enemySerial = id;
            enemyLanes[id] = lane;
            enemy.ConfigureDefenseWave(1f, profile.SpeedScale, profile.DamageScale, profile.Health >= 45f);
            return true;
        }

        void RefreshTowerHealth()
        {
            if (!IsAuthority) return;
            for (int i = buildings.Count - 1; i >= 0; i--)
            {
                BuildingState building = buildings[i];
                if (PlacedElementShrine.TryGetRegistered(building.id, out var tower) && !tower.IsDestroyed)
                { building.health = tower.Health; continue; }
                buildings.RemoveAt(i);
                owner.Builder?.RemovePlacedShrine(building.id);
                // Destruction opens the pad, but spent personal placements stay spent.
                Status = "Tower destroyed · " + buildings.Count + "/" + ActiveTowerCapacity + " towers remain";
            }
        }

        void AutoCastPlayers(double now)
        {
            foreach (var item in peers)
                if (item.Value.AutoFireEnabled && item.Value.Selection.HasConfirmedSelection && now >= item.Value.NextCast && item.Value.AutoSelectionWait.Ready(now))
                    TryCast(item.Key, item.Value.Selection.ConfirmedElement, item.Value.Selection.ConfirmedMask, now,
                        null, true, 0, item.Value.TapRequests.Last);
        }

        public void RequestAutoFire(bool enabled)
        {
            if (!Running || rooms == null || !rooms.Connected) return;
            uint serial = ++localAutoFireSequence;
            if (IsAuthority) TrySetAutoFire(rooms.LocalClientId, enabled, serial);
            else rooms.SendReliable(JsonUtility.ToJson(new Message
                { kind = "oracle.autofire.v1", run = runId, enabled = enabled, controlSequence = serial }));
        }

        void TrySetAutoFire(ulong sender, bool enabled, uint serial)
        {
            if (!Running || !IsAuthority || !peers.TryGetValue(sender, out var peer) || !peer.ArenaReady ||
                unchecked((int)(serial - peer.AutoFireSequence)) <= 0) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < peer.NextAutoFireChange)
            {
                if (sender == rooms.LocalClientId) SendPersonal(sender, 0);
                return;
            }
            peer.AutoFireSequence = serial;
            peer.NextAutoFireChange = now + .08d;
            peer.AutoFireEnabled = enabled;
            SendPersonal(sender, 0);
        }

        public void RequestCast(int element) { RequestCast(element, owner != null && owner.Combat != null ? owner.Combat.ConfirmedMask : 0); }
        public void RequestCast(int element, int mask)
        {
            if (!Running || rooms == null || !rooms.Connected || element < 0 || element > 3 || !ConfirmedSelection.ValidMask(mask) ||
                !rooms.TryGetPlayerAim(rooms.LocalClientId, out var direction)) return;
            uint serial = ++localCastSequence;
            if (IsAuthority) TryCast(rooms.LocalClientId, element, mask, simulationClock, direction, false, serial, localSelectionSequence);
            else rooms.SendReliable(JsonUtility.ToJson(new Message { kind = "oracle.cast.v2", run = runId, element = element,
                selectionMask = mask, selectionSequence = localSelectionSequence, castSequence = serial, direction = direction }));
        }

        void TryCast(ulong id, int element, int usedMask, double now, Vector3? requestedAim, bool automatic, uint requestSequence, uint selectionSequence)
        {
            if (!Running || !clockStarted || !IsAuthority || !peers.TryGetValue(id, out var peer) || !peer.ArenaReady) return;
            // Consume a manual request nonce even when cooling down: replaying an
            // old rejected click later must never turn it into a delayed shot.
            if (!automatic && !peer.CastRequests.Accept(requestSequence)) return;
            if (automatic && (!peer.AutoFireEnabled || !peer.AutoSelectionWait.Ready(now)) || !peer.Selection.HasConfirmedSelection ||
                !ConfirmedSelection.ValidMask(usedMask) || usedMask != peer.Selection.ConfirmedMask ||
                element != peer.Selection.ConfirmedElement || selectionSequence != peer.TapRequests.Last) return;
            if (owner.Revival != null && owner.Revival.IsPeerDowned(id)) return;
            if (!peer.Selection.Ready(now, peer.NextCast, peer.ReturnReadyAt)) return;
            if (!rooms.TryGetPlayerPose(id, out var position, out _)) return;
            Vector3 direction;
            if (requestedAim.HasValue)
            {
                if (!ValidAim(requestedAim.Value)) return;
                direction = requestedAim.Value.normalized;
            }
            else if (!rooms.TryGetPlayerAim(id, out direction)) return;
            // Origin comes from the sender's bounded network pose, never a request-supplied origin.
            float damage = 0f;
            for (int ingredient = 0; ingredient < 4; ingredient++)
                if ((usedMask & (1 << ingredient)) != 0) damage += ElementProgression.GetDamageMultiplier(peer.Mask, ingredient);
            damage /= SpellIngredientRules.Count(usedMask);
            // Accepted launches use fixed normal4s/super5s ingredient recovery
            // and a separate global0.5s cadence; passive haste does not shorten either.
            if (owner.Builder != null) damage *= owner.Builder.SupportDamageMultiplierAt(position);
            int combo = peer.Selection.Combo;
            Vector3 origin = position + Vector3.up * 1.65f;
            int shotId = ++shotSerial;
            PrunePendingShots();
            if (pendingShots.Count >= 128) return;
            bool nova = SpellIngredientRules.IsSuper(usedMask);
            pendingShots[shotId] = new PendingShot { Caster = id, Origin = origin, Expires = simulationClock + 4d, Nova = nova };
            bool fired = owner.Combat.AutoCastFrom(origin, element, damage, direction, combo, shotId, nova, statusMask: peer.Mask, ingredientMask: usedMask);
            if (!fired) { pendingShots.Remove(shotId); return; }
            peer.Nova.Reset();
            ChargeAcceptedShot(peer,usedMask,nova,now);
            // The current wand has one primary stream, capped at two accepted shots per second.
            // ChargeAcceptedShot already advanced the single accepted-launch cadence.
            if (id == rooms.LocalClientId) owner.Combat.CooldownRemaining[element] = SpellIngredientRules.RecoverySeconds(usedMask);
            owner.Combat.PlayNetworkShotFeedback(element, origin, direction, combo, id == rooms.LocalClientId, nova);
            if (fired) SendPresentation(new PresentationMessage
            {
                kind = "oracle.shot.v1", shotId = shotId, casterId = id, element = element,
                combo = combo, origin = origin, direction = direction, cosmicNova = nova
            });
            SendPersonal(id,0);
        }

        // # ARCHITECTURE SNAPSHOT
        // Host-owned fixed4 double timestamps use simulation seconds. Actual used/
        // pair/Nova masks charge only after pooled launch acceptance; no selection,
        // renderer state, persistence or native ownership is changed here.
        static void ChargeAcceptedShot(Peer peer, int usedMask, bool cosmicNova, double now)
        {
            int recoveryMask = cosmicNova ? 15 : usedMask;
            ConfirmedSelection.CommitRecovery(recoveryMask, now, SpellIngredientRules.RecoverySeconds(recoveryMask), peer.ReturnReadyAt);
            peer.NextCast = now + OracleCombat.PrimaryCadenceSeconds;
            peer.Selection.MarkCastAccepted();
        }

        void PrunePendingShots()
        {
            expiredShots.Clear();
            foreach (var shot in pendingShots)
                if (shot.Value.Expires < simulationClock || shot.Value.Caster != rooms.LocalClientId && !ConnectedId(shot.Value.Caster)) expiredShots.Add(shot.Key);
            foreach (int id in expiredShots) pendingShots.Remove(id);
        }

        void OnAuthoritativeShotHit(int shotId, Vector3 point)
        {
            if (!Running || !IsAuthority || !ValidFeedbackPoint(point) || !pendingShots.TryGetValue(shotId, out var shot) ||
                shot.Expires < simulationClock || !peers.TryGetValue(shot.Caster, out var peer) || !peer.ArenaReady ||
                (point - shot.Origin).sqrMagnitude > 20f * 20f) return;
            // Keep the accepted launch until expiry so hit and Nova impact ordering is immaterial.
            if (shot.HitConfirmed) return;
            shot.HitConfirmed=true; pendingShots[shotId]=shot;
            if (shot.Caster == rooms.LocalClientId) owner.Combat.PlayOwnHitConfirmation(point);
            else SendPresentation(new PresentationMessage
            {
                kind = "oracle.hit.v1", shotId = shotId, casterId = shot.Caster, point = point
            }, shot.Caster);
        }

        void OnAuthoritativeNovaImpact(int shotId,Vector3 point,float radius)
        {
            if (!Running || !IsAuthority || !ValidFeedbackPoint(point) || !Finite(radius) || radius<=0f || radius>4.5f ||
                !pendingShots.TryGetValue(shotId,out var shot) || !shot.Nova || shot.ImpactConfirmed || shot.Expires<simulationClock ||
                !peers.TryGetValue(shot.Caster,out var peer) || !peer.ArenaReady || (point-shot.Origin).sqrMagnitude>400f) return;
            shot.ImpactConfirmed=true;pendingShots[shotId]=shot;
            SendPresentation(new PresentationMessage {kind="oracle.nova.impact.v1",shotId=shotId,casterId=shot.Caster,point=point,radius=radius});
        }

        void OnPeerHealthChanged(ulong id)
        {owner?.Combat?.InvalidatePeerTargetCache();SendPersonal(id,0);}
        void OnPoseCorrection(ulong id,Vector3 position)
        {
            if(!IsAuthority||(!Running&&!CampaignPeacePoses)||!peers.ContainsKey(id))return;
            if(id==rooms.LocalClientId){owner.Player.Teleport(position,owner.Player.transform.eulerAngles.y);rooms.ApplyLocalArenaPoseCorrection(position);return;}
            rooms.SendReliableTo(id,JsonUtility.ToJson(new Message{kind="oracle.pose.correct.v1",run=runId,partyVersion=PartyProtocolVersion,
                correctionSequence=++correctionSerial,position=position}));
        }

        void OnAuthoritativeTowerShot(int towerId, Vector3 primary, Vector3 secondaryA, Vector3 secondaryB, int secondaryCount)
        {
            if (!Running || !IsAuthority || !TryGetBuilding(towerId, out var tower) || !ValidTowerPath(tower,
                primary, secondaryA, secondaryB, secondaryCount)) return;
            SendPresentation(new PresentationMessage
            {
                kind = "oracle.tower.shot.v1", towerId = towerId, point = primary,
                secondaryA = secondaryA, secondaryB = secondaryB, secondaryCount = secondaryCount
            });
        }

        bool TryGetBuilding(int towerId, out BuildingState tower)
        {
            foreach (var building in buildings) if (building.id == towerId) { tower = building; return true; }
            tower = null;
            return false;
        }

        static bool ValidTowerPath(BuildingState tower, Vector3 primary, Vector3 a, Vector3 b, int count)
        {
            if (count < 0 || count > 2 || !ValidFeedbackPoint(primary) || (primary - tower.position).sqrMagnitude > 20f * 20f) return false;
            if (count > 0 && (!ValidFeedbackPoint(a) || (a - tower.position).sqrMagnitude > 20f * 20f)) return false;
            return count < 2 || ValidFeedbackPoint(b) && (b - tower.position).sqrMagnitude <= 20f * 20f;
        }

        bool AdmitPresentation(int bytes)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - presentationWindow >= 1d)
            {
                presentationWindow = now;
                presentationCount = presentationBytes = 0;
            }
            if (bytes < 1 || bytes > 1024 || presentationCount >= 32 || presentationBytes + bytes > 32 * 1024) return false;
            presentationCount++;
            presentationBytes += bytes;
            return true;
        }

        void SendPresentation(PresentationMessage message, ulong? recipient = null)
        {
            if (!Running || !IsAuthority) return;
            message.run = runId;
            message.eventSequence = ++presentationSerial;
            string payload = JsonUtility.ToJson(message);
            if (!AdmitPresentation(System.Text.Encoding.UTF8.GetByteCount(payload))) return;
            if (recipient.HasValue)
            {
                if (recipient.Value != rooms.LocalClientId && peers.TryGetValue(recipient.Value, out var peer) && peer.ArenaReady)
                    rooms.SendReliableTo(recipient.Value, payload);
                return;
            }
            foreach (var peer in peers)
                if (peer.Key != rooms.LocalClientId && peer.Value.ArenaReady) rooms.SendReliableTo(peer.Key, payload);
        }

        void ApplyPresentation(string payload)
        {
            if (!Running || IsAuthority || payload.Length > 1024 || string.IsNullOrEmpty(runId)) return;
            PresentationMessage message;
            try { message = JsonUtility.FromJson<PresentationMessage>(payload); }
            catch (ArgumentException) { return; }
            if (message == null || message.run != runId || message.eventSequence <= appliedPresentationSerial || !AdmitPresentation(System.Text.Encoding.UTF8.GetByteCount(payload))) return;
            if (message.kind == "oracle.shot.v1")
            {
                if (message.shotId <= appliedShotSerial || message.element < 0 || message.element > 3 || message.combo < 0 || message.combo > 6 ||
                    !KnownCaster(message.casterId) || !ValidFeedbackPoint(message.origin) || !ValidAim(message.direction)) return;
                { // Accepted launch identity also deduplicates late replay after expiry.
                    PrunePendingShots();
                    if (pendingShots.Count >= 128) return;
                    appliedPresentationSerial = message.eventSequence;
                    appliedShotSerial = message.shotId;
                    pendingShots[message.shotId] = new PendingShot
                    {
                        Caster = message.casterId, Origin = message.origin, Expires = simulationClock + 4d, Nova=message.cosmicNova
                    };
                }
                owner.Combat.PlayNetworkShotFeedback(message.element, message.origin, message.direction.normalized,
                    message.combo, message.casterId == rooms.LocalClientId,message.cosmicNova);
            }
            else if (message.kind == "oracle.hit.v1")
            {
                if (message.shotId < 1 || message.casterId != rooms.LocalClientId || !ValidFeedbackPoint(message.point) ||
                    !pendingShots.TryGetValue(message.shotId, out var shot) || shot.Caster != rooms.LocalClientId ||
                    shot.Expires < simulationClock || (message.point - shot.Origin).sqrMagnitude > 20f * 20f) return;
                appliedPresentationSerial = message.eventSequence;
                if(shot.HitConfirmed)return;
                shot.HitConfirmed=true;pendingShots[message.shotId]=shot;
                owner.Combat.PlayOwnHitConfirmation(message.point);
            }
            else if (message.kind == "oracle.nova.impact.v1")
            {
                if(message.shotId<1 || !KnownCaster(message.casterId) || !ValidFeedbackPoint(message.point) || !Finite(message.radius) ||
                    message.radius<=0f || message.radius>4.5f || !pendingShots.TryGetValue(message.shotId,out var shot) || !shot.Nova ||
                    shot.Caster!=message.casterId || shot.ImpactConfirmed || shot.Expires<simulationClock ||
                    (message.point-shot.Origin).sqrMagnitude>400f)return;
                appliedPresentationSerial=message.eventSequence;shot.ImpactConfirmed=true;pendingShots[message.shotId]=shot;
                owner.Combat.PlayCosmicNovaFeedback(message.point,message.radius,owner.Graphics!=null?owner.Graphics.ParticleQuality:1);
            }
            else if (message.kind == "oracle.tower.shot.v1")
            {
                if (!TryGetBuilding(message.towerId, out var tower) || !ValidTowerPath(tower, message.point,
                    message.secondaryA, message.secondaryB, message.secondaryCount)) return;
                appliedPresentationSerial = message.eventSequence;
                PlacedElementShrine.PlayRemoteShotFeedback(message.towerId, message.point, message.secondaryA,
                    message.secondaryB, message.secondaryCount);
            }
        }

        bool KnownCaster(ulong id)
        {
            if (id == NetworkManager.ServerClientId || id == rooms.LocalClientId) return true;
            return peers.ContainsKey(id);
        }

        void SendSelectedElement() { /* Explicit taps only; no preview polling can confirm a spell. */ }

        public bool RequestSelectElement(int element)
        {
            if (!Running || rooms == null || !rooms.Connected || element < 0 || element > 3) return false;
            uint serial = ++localSelectionSequence;
            if (IsAuthority) return TrySelectElement(rooms.LocalClientId, element, serial);
            rooms.SendReliable(JsonUtility.ToJson(new Message { kind = "oracle.element.v2", run = runId, element = element, selectionSequence = serial }));
            return true;
        }

        bool TrySelectElement(ulong id, int element, uint sequence)
        {
            if (!Running || !IsAuthority || !peers.TryGetValue(id, out var peer) || !peer.ArenaReady || !peer.TapRequests.Accept(sequence)) return false;
            if (element < 0 || element > 3 || owner.Revival != null && owner.Revival.IsPeerDowned(id))
            { SendPersonal(id, 0); return false; }
            if (!peer.Selection.Tap(element)) return false;
            peer.AutoSelectionWait.NotifyInput(simulationClock);
            peer.Element = peer.Selection.PreviewElement;
            SendPersonal(id, 0); return true;
        }

        static int ComboFor(int a, int b)
        { return a < 0 || a > 3 || b < 0 || b > 3 ? 0 : ConfirmedSelection.ComboForMask((1 << a) | (1 << b)); }

        public void RequestBuild(int element, Vector3 position)
        {
            if (!Running || rooms == null || !rooms.Connected) { Status = "Start the connected defense before placing a tower."; return; }
            if (IsAuthority) TryBuild(rooms.LocalClientId, element, position);
            else rooms.SendReliable(JsonUtility.ToJson(new Message { kind = "oracle.build.v1", run = runId, element = element, position = position }));
        }

        void TryBuild(ulong sender, int element, Vector3 position)
        {
            if (!Running || !IsAuthority || !peers.TryGetValue(sender, out var peer) || !peer.ArenaReady) return;
            string reason = null;
            Vector3 canonical=default;
            if (!TowerForgeDefinitions.ValidType(element)) reason = "Choose a tower type.";
            else if(CampaignActive&&!campaign.CanBuildTower(sender,element))reason="Complete the Smith and Runekeeper visits first.";
            else if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)) reason = "Choose a valid tower pad.";
            else if (owner.World == null || owner.World.TowerPlatforms==null || !owner.World.TowerPlatforms.TryGetCanonical(position,out canonical,out _) || !owner.World.IsBuildPadActive(canonical)) reason = "Choose a pad on an active invasion lane.";
            else if(owner.Revival!=null&&owner.Revival.IsPeerDowned(sender)) reason="Recover before building.";
            else if (peer.Tokens <= 0) reason = "You have used your three tower placements.";
            else if (buildings.Count >= ActiveTowerCapacity) reason = "The active lanes have their " + ActiveTowerCapacity + " towers.";
            else if (!rooms.TryGetPlayerPose(sender, out var playerPosition, out _) || (playerPosition - position).sqrMagnitude > 25f * 25f) reason = "Stand within 25 meters of the tower pad.";
            //007 four shared bastions have no x-sign or admission-slot owner cap.
            else if (owner.Builder == null) reason = "The tower builder is not ready.";
            else if (!owner.Builder.ModelsReady) reason="Tower building is unavailable until its saved art is ready.";
            else if (!rooms.TryGetPlayerPose(sender,out Vector3 feet,out _)) reason="Your position is not ready.";
            else if (!owner.Builder.ValidatePlacementFrom(feet,canonical,out var placementReason)) reason=string.IsNullOrEmpty(placementReason)?"Choose a reachable tower pad.":placementReason;
            if (!string.IsNullOrEmpty(reason)) { Notify(sender, reason); return; }
            var building = new BuildingState { id=++buildingSerial,element=element,owner=sender,position=canonical,
                tier=peer.Forge.GetTier(element),tierRevision=peer.Forge.Revision };
            // Placement spends no token unless the validated saved model exists.
            if(!owner.Builder.TryApplyPlacedShrine(building.id,element,canonical,building.tier,building.tierRevision))
            {Notify(sender,"Tower building is unavailable.");return;}
            buildings.Add(building);
            peer.Tokens--;

            if (PlacedElementShrine.TryGetRegistered(building.id, out var audibleTower) && !audibleTower.IsDestroyed)
            {
                owner.Audio?.PlayProductionCue(ProductionCue.TowerConstruct, building.position);
                if (sender == rooms.LocalClientId) owner.Audio?.PlayProductionCue(ProductionCue.TowerConfirm);
            }
            Status = "Tower placed · " + buildings.Count + "/" + ActiveTowerCapacity + " shared pads";
            SendState();
        }

        public int LaneCount(int lane)
        {
            int count = 0;
            foreach (var tower in buildings) if ((tower.position.x < 0f ? 0 : 1) == lane) count++;
            return count;
        }

        void Notify(ulong clientId, string text)
        {
            if (clientId == rooms.LocalClientId) Status = text;
            else rooms.SendReliableTo(clientId, JsonUtility.ToJson(new Message { kind = "oracle.notice.v1", run = runId, text = text }));
        }

        public void RegisterXpDrop(int enemyId, Vector3 position, int amount = 1)
        {
            if(CampaignActive)enemyId=CampaignQuestState.ScopedDropId(campaign.StageNumber,enemyId);
            if (!Running || !IsAuthority || enemyId < 1 || drops.ContainsKey(enemyId) || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) return;
            while (drops.Count >= 128 && dropOrder.Count > 0)
            {
                int retired = dropOrder.Dequeue();
                if (!drops.Remove(retired)) continue;
                foreach (var peer in peers.Values) peer.Claimed.Remove(retired);
                if (owner.Progression != null) owner.Progression.ConfirmNetworkXp(retired);
                rooms.SendReliable(JsonUtility.ToJson(new Message { kind = "oracle.xp.retire.v1", run = runId, dropId = retired }));
            }
            var drop = new DropState { id = enemyId, position = position, amount = Mathf.Clamp(amount, 1, 10) };
            drops[enemyId] = drop;
            dropOrder.Enqueue(enemyId);
            if (owner.Progression != null) owner.Progression.SpawnNetworkXp(drop.id, drop.position, drop.amount);
        }

        public void RequestXpClaim(int dropId)
        {
            if (!Running || rooms == null || !rooms.Connected || dropId < 1) return;
            if (IsAuthority) TryClaim(rooms.LocalClientId, dropId);
            else rooms.SendReliable(JsonUtility.ToJson(new Message { kind = "oracle.xp.claim.v1", run = runId, dropId = dropId }));
        }

        void TryClaim(ulong sender, int dropId)
        {
            if (!Running || !IsAuthority || !peers.TryGetValue(sender, out var peer) || !peer.ArenaReady || !drops.TryGetValue(dropId, out var drop)) return;
            if (peer.Claimed.Contains(dropId)) { SendPersonal(sender, dropId); return; }
            if (owner.Revival != null && owner.Revival.IsPeerDowned(sender)) return;
            if (!rooms.TryGetPlayerPose(sender, out var position, out _) ||
                !PickupReachRules.WithinReach((position - drop.position).sqrMagnitude, peer.Mask)) return;
            Vector3 origin = position + Vector3.up * .95f;
            Vector3 destination = drop.position + Vector3.up * .4f;
            Transform collector = sender == rooms.LocalClientId && owner.Player != null ? owner.Player.transform : null;
            if (!xpClaimVisibility.IsClear(origin, destination, collector)) return;
            peer.Claimed.Add(dropId);
            peer.Forge.TryCreditAcceptedDrop(dropId); // Separate ephemeral +1; XP amount is unchanged.
            long total = (long)peer.Xp + drop.amount;
            while (peer.Level < ElementProgression.MaximumLevel && total >= 2 + (peer.Level - 1) / 2)
            {
                total -= 2 + (peer.Level - 1) / 2;
                peer.Level++;
                peer.Points++;
            }
            peer.Xp = peer.Level >= ElementProgression.MaximumLevel ? 0 : (int)total;
            SendPersonal(sender, dropId);
        }

        public void ConfigureMatchForgeStation(NpcBlacksmith smith) { forgeSmith = smith; }
        public string ForgeRunId=>runId??string.Empty;
        public bool ForgeStateReady=>localForge.HasState;
        public int LocalForgeBalance => localForge.Balance;
        public bool ForgeRequestPending => localForge.Pending;
        public MatchForgeOutcome LastForgeOutcome => localForge.LastOutcome;
        public int GetLocalTowerForgeTier(int type) => localForge.GetTier(type);
        public int GetTowerTier(int buildingId, int expectedBaseType)
        {
            if (!MatchTowerForgeRules.ValidType(expectedBaseType) || !TryGetBuilding(buildingId, out BuildingState building)
                || building.element != expectedBaseType || !MatchTowerForgeRules.ValidTier(building.tier)) return 1;
            return building.tier;
        }
        public ITowerForgeModeAdapter CreateMatchForgeAdapter(Func<bool> localUiContext)
        {
            if (localUiContext == null) throw new ArgumentNullException(nameof(localUiContext));
            return new CoopMatchTowerForgeAdapter(localForge,
                // The legacy arena view may show earned charge/tiers, but it
                // cannot dispatch upgrades. The refuge has its own gated adapter.
                () => false,
                SendForgeRequest);
        }
        bool LocalForgeStationInRange()
        {
            if (rooms == null || !rooms.Connected || forgeSmith == null || !forgeSmith.gameObject.activeInHierarchy
                || owner == null || owner.World == null || !owner.World.IsCoopArena || owner.Player == null) return false;
            Vector3 delta = owner.Player.transform.position - forgeSmith.transform.position;
            return Mathf.Abs(delta.y) < 1.5f && delta.x * delta.x + delta.z * delta.z <= 3.25f * 3.25f;
        }
        void SendForgeRequest(MatchForgeRequest request)
        {
            if (rooms == null || !rooms.Connected || (!Running&&!CampaignRefuge)) { localForge.ReleasePendingAfterTimeout(); return; }
            if (IsAuthority) TryTowerForgeUpgrade(rooms.LocalClientId, request);
            else rooms.SendReliable(JsonUtility.ToJson(StampCampaign(new Message { kind = "oracle.forge.buy.v1", run = runId,
                forgeRequestId = request.RequestId, element = request.BaseType,
                forgeExpectedTier = request.ExpectedTier, forgeExpectedRevision = request.ExpectedRevision })));
        }
        bool HostForgeContext(ulong sender, Peer peer)
        {
            // Arena purchases and the initial refuge are authoritatively denied.
            // CanUseSmith retains the accepted visit plus host pose/aim/range/LOS.
            return CampaignTowerUpgradePhase&&IsAuthority&&campaign.CanUseSmith(sender);
        }
        void TryTowerForgeUpgrade(ulong sender, MatchForgeRequest request)
        {
            if ((!Running&&!CampaignRefuge) || !IsAuthority || !ConnectedId(sender) || !peers.TryGetValue(sender, out Peer peer)) return;
            MatchForgeReceipt receipt;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < peer.NextForgeRequest && request.RequestId != peer.Forge.LastRequestId)
                receipt = new MatchForgeReceipt(request.RequestId, MatchForgeOutcome.RateLimited, peer.Forge.Snapshot, false);
            else
            {
                peer.NextForgeRequest = now + .15d;
                receipt = peer.Forge.TryUpgrade(request, HostForgeContext(sender, peer));
                if (receipt.Outcome == MatchForgeOutcome.Applied && !receipt.Replayed)
                {
                    // Integer writes only, with no yield/callback between bank and building ranks.
                    // Surviving disconnected-owner towers retain this accepted value, not host-local ranks.
                    for (int i = 0; i < buildings.Count; i++)
                        if (buildings[i].owner == sender && buildings[i].element == request.BaseType)
                        {
                            buildings[i].tier=peer.Forge.GetTier(request.BaseType);buildings[i].tierRevision=peer.Forge.Revision;
                        }
                    // Integer bank/ranks are committed before visual/UI callbacks.
                    foreach(var building in buildings)if(building.owner==sender&&building.element==request.BaseType&&
                        PlacedElementShrine.TryGetRegistered(building.id,out var tower))
                        tower.ApplyAcceptedModelTier(building.tier,building.tierRevision,MatchTowerForgeRules.Interval(building.tier));
                }
            }
            SendForgeResult(sender, receipt);
            if (receipt.Outcome == MatchForgeOutcome.Applied && !receipt.Replayed) SendState();
        }
        void ApplyForgePersonal(PlayerState state)
        {
            if (state == null || state.forgeVersion != MatchTowerForgeRules.Version) return;
            var snapshot = new MatchForgeSnapshot(state.forgeVersion, state.forgeBank, state.forgeTiers, state.forgeRevision);
            if (state.forgeLastRequest == 0) localForge.ApplyHostState(snapshot);
            else localForge.ApplyHostReceipt(new MatchForgeReceipt(state.forgeLastRequest, (MatchForgeOutcome)state.forgeOutcome, snapshot, true));
        }
        void SendForgeResult(ulong sender, MatchForgeReceipt receipt)
        {
            if(CampaignRefuge){SendCampaignPersonalTo(sender,receipt);return;}
            if (!peers.TryGetValue(sender, out Peer peer)) return;
            PlayerState personal = PersonalState(sender, peer);if(personal==null)return;
            if (sender == rooms.LocalClientId)
            { ApplyPersonal(personal, 0); localForge.ApplyHostReceipt(receipt); }
            else rooms.SendReliableTo(sender, JsonUtility.ToJson(new Message { kind = "oracle.forge.result.v1", run = runId,
                hasPersonal = true, personal = personal,partyVersion=PartyProtocolVersion, forgeRequestId = receipt.RequestId, forgeOutcome = (int)receipt.Outcome }));
        }
        void ApplyForgeResult(Message message)
        {
            if(message==null||message.partyVersion!=PartyProtocolVersion||!message.hasPersonal||!ValidPersonalPayload(message.personal)||message.personal.id!=rooms.LocalClientId||
                message.forgeRequestId==0||(uint)message.forgeOutcome>(uint)MatchForgeOutcome.RateLimited)return;
            ApplyPersonal(message.personal, 0);
            localForge.ApplyHostReceipt(new MatchForgeReceipt(message.forgeRequestId, (MatchForgeOutcome)message.forgeOutcome,
                new MatchForgeSnapshot(message.personal.forgeVersion, message.personal.forgeBank,
                    message.personal.forgeTiers, message.personal.forgeRevision), false));
        }

        public void RequestUpgrade(int element, int node)
        {
            if ((!Running&&!CampaignRefuge) || rooms == null || !rooms.Connected) return;
            if (IsAuthority) TryUpgrade(rooms.LocalClientId, element, node);
            else rooms.SendReliable(JsonUtility.ToJson(StampCampaign(new Message { kind = "oracle.upgrade.v1", run = runId, element = element, node = node })));
        }

        void TryUpgrade(ulong sender, int element, int node)
        {
            if ((!Running&&!CampaignRefuge) || !IsAuthority || !peers.TryGetValue(sender, out var peer)) return;
            if(CampaignRefuge?!campaign.CanUseRunekeeper(sender):!peer.ArenaReady)return;
            if (element < 0 || element > 3 || node < 0 || node > 2) return;
            int bit = 1 << (element * 3 + node);
            if (peer.Points < 1 || (peer.Mask & bit) != 0 || node > 0 && (peer.Mask & (bit >> 1)) == 0)
            {
                Notify(sender, "Earn a skill point and learn the previous node first.");
                return;
            }
            peer.Points--;
            peer.Mask |= bit;
            SendPersonal(sender, 0);
        }

        PlayerState PersonalState(ulong id, Peer peer)
        {
            if(peerHealth==null||!peerHealth.TryCaptureHealth(id,out PeerHealthState health))return null;
            var claimed = new int[peer.Claimed.Count];
            peer.Claimed.CopyTo(claimed);
            var returning = new float[4];
            for (int i = 0; i < returning.Length; i++) returning[i] = Mathf.Max(0f, (float)(peer.ReturnReadyAt[i] - simulationClock));
            return new PlayerState
            {
                id = id,health=health, tokens = peer.Tokens, element = peer.Element, autoFire = peer.AutoFireEnabled,
                forgeVersion=MatchTowerForgeRules.Version,forgeBank=peer.Forge.Balance,forgeTiers=peer.Forge.PackedTiers,
                forgeRevision=peer.Forge.Revision,forgeLastRequest=peer.Forge.LastRequestId,forgeOutcome=(int)peer.Forge.LastOutcome,
                combo = peer.Selection.Combo, comboSeconds = 0f,
                preparedElement = peer.Selection.PreparedElement, selectionMask = peer.Selection.ConfirmedMask,
                confirmedElement = peer.Selection.ConfirmedElement, selectionSequence = peer.TapRequests.Last,
                level = peer.Level, xp = peer.Xp, points = peer.Points, mask = peer.Mask, claimed = claimed,
                returnCooldowns = returning,
                novaMask = peer.Nova.MaskAt(simulationClock), novaSeconds = peer.Nova.RemainingAt(simulationClock),
                cooldown = Mathf.Max(0f, (float)(peer.NextCast - simulationClock))
            };
        }

        void SendPersonal(ulong id, int acknowledgedDrop)
        {
            if(CampaignRefuge){SendCampaignPersonalTo(id);return;}
            if (!peers.TryGetValue(id, out var peer)) return;
            var state = PersonalState(id, peer);if(state==null)return;
            if (id == rooms.LocalClientId) ApplyPersonal(state, acknowledgedDrop);
            else rooms.SendReliableTo(id, JsonUtility.ToJson(new Message { kind = "oracle.personal.v1", run = runId, hasPersonal = true, personal = state, partyVersion=PartyProtocolVersion, dropId = acknowledgedDrop }));
        }

        // JsonUtility can materialize a default object for an absent class.
        // Presence and complete scalar/array validity precede any UI/progress mutation.
        static bool HasPersonalPayload(Message message) => message != null && message.kind == "oracle.personal.v1" &&
            message.hasPersonal && message.partyVersion==PartyProtocolVersion&&ValidPersonalPayload(message.personal);
        static bool ValidPersonalPayload(PlayerState state)
        {
            if (state == null || !state.health.present||state.health.id!=state.id||!PeerCombatAuthority.ValidHealthState(state.health)||!new MatchForgeSnapshot(state.forgeVersion,state.forgeBank,state.forgeTiers,state.forgeRevision).Valid ||
                (uint)state.forgeOutcome>(uint)MatchForgeOutcome.RateLimited ||
                (state.forgeLastRequest==0)!=(state.forgeOutcome==0) || state.level < 1 || state.level > ElementProgression.MaximumLevel ||
                state.xp < 0 || state.points < 0 || state.mask < 0 || state.mask > 0xFFF || state.tokens < 0 || state.tokens > 3 ||
                state.element < 0 || state.element > 3 || state.combo < 0 || state.combo > 6 ||
                state.novaMask < 0 || state.novaMask > 15 || !Finite(state.novaSeconds) || state.novaSeconds < 0f || state.novaSeconds > 4f ||
                state.claimed == null || state.claimed.Length > 128 || state.returnCooldowns == null || state.returnCooldowns.Length != 4 ||
                !Finite(state.cooldown) || state.cooldown < 0f || state.cooldown > OracleCombat.PrimaryCadenceSeconds + .001f ||
                !ConfirmedSelection.ValidState(state.preparedElement, state.selectionMask, state.confirmedElement)) return false;
            for (int i=0;i<4;i++) if (!Finite(state.returnCooldowns[i]) || state.returnCooldowns[i] < 0f ||
                state.returnCooldowns[i] > OracleCombat.SuperAbilityCooldownSeconds + .001f) return false;
            for (int i=0;i<state.claimed.Length;i++) if (state.claimed[i] < 1) return false;
            int spent=0;
            for (int element=0;element<4;element++)
            {
                int branch=(state.mask>>(element*3))&7;
                if(branch!=0&&branch!=1&&branch!=3&&branch!=7)return false;
                for(int bit=0;bit<3;bit++)if((branch&(1<<bit))!=0)spent++;
            }
            if ((long)state.points+spent > state.level-1) return false;
            return state.level == ElementProgression.MaximumLevel ? state.xp == 0 : state.xp < 2+(state.level-1)/2;
        }

        void ApplyPersonal(PlayerState state, int acknowledgedDrop)
        {
            if (!ValidPersonalPayload(state) || state.id != rooms.LocalClientId || owner.Progression == null) return;
            if(state.returnCooldowns==null||state.returnCooldowns.Length!=4||!Finite(state.cooldown)||state.cooldown<0f||
                state.cooldown>OracleCombat.PrimaryCadenceSeconds+.001f)return;
            for(int slot=0;slot<4;slot++)if(!Finite(state.returnCooldowns[slot])||state.returnCooldowns[slot]<0f||
                state.returnCooldowns[slot]>OracleCombat.SuperAbilityCooldownSeconds+.001f)return;
            if (!ConfirmedSelection.ValidState(state.preparedElement, state.selectionMask, state.confirmedElement) ||
                !owner.Combat.ApplyRemoteSelectionState(state.preparedElement, state.selectionMask, state.confirmedElement, state.selectionSequence)) return;
            if(state.novaMask < 0 || state.novaMask > 15 || !Finite(state.novaSeconds) || state.novaSeconds < 0f || state.novaSeconds > 4f)return;
            peerHealth.TryApplyHostHealth(runId,state.health);
            owner.Combat.ApplyRemoteNovaState(state.novaMask, state.novaSeconds);
            ApplyForgePersonal(state);
            localTokens = Mathf.Clamp(state.tokens, 0, 3);
            owner.Progression.ApplyAuthoritativeState(state.level, state.xp, state.points, state.mask);
            owner.Combat.ApplyRemoteElementState(state.element, state.returnCooldowns, state.combo, state.comboSeconds);
            owner.Combat.ApplyRemoteAutoFireState(state.autoFire);
            for (int i = 0; i < 4; i++)
            {
                float returning = state.returnCooldowns != null && state.returnCooldowns.Length == 4 ? Mathf.Clamp(state.returnCooldowns[i], 0f, OracleCombat.SuperAbilityCooldownSeconds) : 0f;
                owner.Combat.CooldownRemaining[i] = Mathf.Max(returning, i == state.element ? state.cooldown : 0f);
            }
            if (acknowledgedDrop > 0) owner.Progression.ConfirmNetworkXp(acknowledgedDrop);
            if (state.claimed != null)
                foreach (int id in state.claimed) owner.Progression.ConfirmNetworkXp(id);
        }

        void OnMessage(ulong sender, string payload)
        {
            if (owner == null || rooms == null || !rooms.Connected || string.IsNullOrEmpty(payload) ||
                payload.Length > RemoteDangerPolicy.MaximumSnapshotCharacters ||
                System.Text.Encoding.UTF8.GetByteCount(payload) > RoomConnection.MaximumReliablePayloadBytes) return;
            Message message;
            try { message = JsonUtility.FromJson<Message>(payload); }
            catch (ArgumentException) { return; }
            if (message == null || string.IsNullOrEmpty(message.kind)) return;
            if(HandleCampaignProgress(sender,message))return;
            if (IsAuthority)
            {
                bool refugeRequest=CampaignRefuge&&(message.kind=="oracle.upgrade.v1"||message.kind=="oracle.forge.buy.v1");
                if ((!Running&&!refugeRequest) || !ConnectedId(sender) || message.run != runId || payload.Length > 2048 ||
                    refugeRequest&&(!MatchesCampaign(message)||!rooms.IsCampaignMemberRegistered(sender))) return;
                if (message.kind == "oracle.ready.v1" && (message.dangerVersion != RemoteDangerPolicy.Version || message.novaVersion != NovaProtocolVersion || message.towerVersion != TowerProtocolVersion||message.partyVersion!=PartyProtocolVersion))
                {
                    rooms.SendReliableTo(sender, JsonUtility.ToJson(new Message { kind = "oracle.notice.v1", run = runId,
                        text = "Incompatible combat presentation version. Update both players." }));
                    return;
                }
                if (message.kind == "oracle.ready.v1" && message.dangerVersion == RemoteDangerPolicy.Version && message.novaVersion == NovaProtocolVersion && message.towerVersion == TowerProtocolVersion&&message.partyVersion==PartyProtocolVersion && peers.TryGetValue(sender, out var readyPeer))
                {
                    readyPeer.ArenaReady = true;
                    if(rooms.TryGetAdmissionSlot(sender,out int slot))peerHealth.ReconcilePeer(sender,slot,true);
                    SendPersonal(sender,0);
                    RefreshActiveLanes();
                    SendState();
                }
                else if (message.kind == "oracle.cast.v2") TryCast(sender, message.element, message.selectionMask, simulationClock, message.direction, false, message.castSequence, message.selectionSequence);
                else if (message.kind == "oracle.autofire.v1") TrySetAutoFire(sender, message.enabled, message.controlSequence);
                else if (message.kind == "oracle.element.v2") TrySelectElement(sender, message.element, message.selectionSequence);
                else if (message.kind == "oracle.build.v1") TryBuild(sender, message.element, message.position);
                else if (message.kind == "oracle.xp.claim.v1") TryClaim(sender, message.dropId);
                else if (message.kind == "oracle.upgrade.v1") TryUpgrade(sender, message.element, message.node);
                else if(message.kind=="oracle.personal.sync.v1"&&message.partyVersion==PartyProtocolVersion&&peers.TryGetValue(sender,out var syncing)&&syncing.ArenaReady&&Time.realtimeSinceStartupAsDouble>=syncing.NextPersonalSync)
                {syncing.NextPersonalSync=Time.realtimeSinceStartupAsDouble+1d;SendPersonal(sender,0);}
                else if(message.kind=="oracle.forge.buy.v1")TryTowerForgeUpgrade(sender,new MatchForgeRequest(message.forgeRequestId,message.element,message.forgeExpectedTier,message.forgeExpectedRevision));
            }
            else if (sender == NetworkManager.ServerClientId)
            {
                if((message.kind=="oracle.prepare.v1"||message.kind=="oracle.state.v1")&&!MatchesCampaign(message))return;
                if (message.kind == "oracle.shot.v1" || message.kind == "oracle.hit.v1" || message.kind == "oracle.tower.shot.v1" || message.kind == "oracle.nova.impact.v1") ApplyPresentation(payload);
                else if (message.kind == "oracle.prepare.v1") AcknowledgeArena(message.run, message.dangerVersion, message.novaVersion, message.towerVersion,message.partyVersion);
                else if (message.kind == "oracle.state.v1") ApplyState(message);
                else if (message.run == runId)
                {
                    if(message.kind=="oracle.pose.correct.v1"&&message.partyVersion==PartyProtocolVersion&&message.correctionSequence>appliedCorrectionSerial&&PeerCombatLedger.ValidPosition(message.position.x,message.position.y,message.position.z))
                    {appliedCorrectionSerial=message.correctionSequence;owner.Player.Teleport(message.position,owner.Player.transform.eulerAngles.y);rooms.ApplyLocalArenaPoseCorrection(message.position);}
                    else if (message.kind == "oracle.notice.v1") Status = string.IsNullOrEmpty(message.text) ? "Tower request was declined." : message.text;
                    else if (HasPersonalPayload(message)) ApplyPersonal(message.personal, message.dropId);
                    else if(message.kind=="oracle.forge.result.v1")ApplyForgeResult(message);
                    else if (message.kind == "oracle.xp.retire.v1" && owner.Progression != null) owner.Progression.ConfirmNetworkXp(message.dropId);
                }
            }
        }

        void SendState()
        {
            if (!IsAuthority || !hasRun) return;
            enemyBuffer.Clear();
            foreach (var enemy in owner.Combat.ActiveEnemies)
                if (enemy != null && enemy.Alive && enemyBuffer.Count < OracleCombat.MaxActiveEnemies)
                    enemyBuffer.Add(new EnemyState { lane=enemyLanes.TryGetValue(enemy.TargetId,out int canonicalLane)?canonicalLane:-1,id = enemy.TargetId, archetype = (int)enemy.Archetype, shade = enemy.IsRanged, flying = enemy.IsFlying, position = enemy.transform.position, hp = enemy.HP, maximum = enemy.MaxHP,
                        attackSequence = enemy.AttackPresentationSequence, attackPhase = (int)enemy.AttackPhase,
                        attackProgress = enemy.AttackProgress, aggro = enemy.IsAggroedOnPlayer,
                        lockedDirection = enemy.LockedAttackDirection, attackStyle = enemy.AttackPresentationStyle,
                        windupSeconds = enemy.AttackWindupSeconds, strikeSeconds = enemy.AttackStrikeSeconds,
                        recoverySeconds = enemy.AttackRecoverySeconds, targetTowerId = enemy.AttackPresentationTargetId,
                        targetPoint = enemy.LockedAttackEndpoint });
            int hostileCount = owner.Combat.CopyHostileDangerState(hostileStateBuffer);
            publicHealthBuffer.Clear();
            foreach(var item in peers)if(item.Value.ArenaReady)
            {if(!peerHealth.TryCaptureHealth(item.Key,out var health)){MissingHealthSnapshots++;return;}publicHealthBuffer.Add(health);}
            // Own full progress is sent on ready, accepted changes and bounded
            // full resync. Other peers never receive128 private claim IDs each.
            var players=NoCommonPersonal;
            var worldDrops = new DropState[drops.Count];
            drops.Values.CopyTo(worldDrops, 0);
            var state = new Message
            {
                kind = "oracle.state.v1", run = runId, sequence = ++snapshotSerial,
                running = Running, clockStarted = clockStarted, remaining = RemainingSeconds, result = result,
                activeLanes = ActiveLaneCount,admissionMask=admissionLaneMask,geometryMask=activeLaneMask,health=publicHealthBuffer.ToArray(),partyVersion=PartyProtocolVersion,
                wave = WaveNumber, spawned = SpawnedThisWave, quota = WaveQuota,
                shrine = owner.Combat.Shrine != null ? owner.Combat.Shrine.Health : 0f,
                enemies = enemyBuffer.ToArray(), buildings = buildings.ToArray(), players = players, drops = worldDrops,
                dangerVersion = RemoteDangerPolicy.Version, novaVersion = NovaProtocolVersion, towerVersion = TowerProtocolVersion, hostileCount = hostileCount, hostileBolts = hostileStateBuffer
            };
            StampCampaign(state);
            string payload = JsonUtility.ToJson(state);
            if (payload.Length > RemoteDangerPolicy.MaximumSnapshotCharacters ||
                System.Text.Encoding.UTF8.GetByteCount(payload) > RoomConnection.MaximumReliablePayloadBytes)
            { OversizedDangerSnapshots++; return; }
            string prepare = null;
            foreach (var peer in peers)
            {
                if (peer.Key == rooms.LocalClientId) continue;
                if (peer.Value.ArenaReady) rooms.SendReliableTo(peer.Key, payload);
                else
                {
                    if (prepare == null) prepare = JsonUtility.ToJson(StampCampaign(new Message { kind = "oracle.prepare.v1", run = runId, dangerVersion = RemoteDangerPolicy.Version, novaVersion = NovaProtocolVersion, towerVersion = TowerProtocolVersion,partyVersion=PartyProtocolVersion }));
                    rooms.SendReliableTo(peer.Key, prepare);
                }
            }
        }

        bool ValidTowerPayload(Message state)
        {
            if(owner==null||owner.World==null||owner.World.TowerPlatforms==null||
                state.buildings==null||state.buildings.Length>4||state.players==null||state.players.Length!=0||
                state.health==null||state.health.Length<1||state.health.Length>8||
                !CoopPartyCapacity.ValidLaneMask(state.admissionMask)||!CoopPartyCapacity.ValidLaneMask(state.geometryMask)||
                (state.admissionMask&~state.geometryMask)!=0||state.activeLanes!=CoopPartyCapacity.LaneCount(state.geometryMask))return false;
            for(int i=0;i<4;i++)seenTowerPads[i]=false;
            for(int i=0;i<state.buildings.Length;i++)
            {
                var b=state.buildings[i];
                if(b==null||b.id<1||b.id>65535||!TowerForgeDefinitions.ValidType(b.element)||!MatchTowerForgeRules.ValidTier(b.tier)||
                    b.tierRevision<0||b.tierRevision>MatchTowerForgeRules.MaximumRevision||!ValidFeedbackPoint(b.position)||
                    !Finite(b.health)||b.health<=0f||b.health>PlacedElementShrine.DefaultMaxHealth||
                    !owner.World.TowerPlatforms.TryGetCanonical(b.position,out _,out int pad)||seenTowerPads[pad])return false;
                seenTowerPads[pad]=true;for(int j=0;j<i;j++)if(state.buildings[j].id==b.id)return false;
            }
            for(int i=0;i<state.health.Length;i++)
            {
                var health=state.health[i];
                if(!health.present||!PeerCombatAuthority.ValidHealthState(health)||!ConnectedId(health.id))return false;
                for(int j=0;j<i;j++)if(state.health[j].id==health.id)return false;
            }
            return true;
        }

        bool ValidDangerState(Message state)
        {
            if (state.dangerVersion != RemoteDangerPolicy.Version || state.novaVersion != NovaProtocolVersion || state.towerVersion != TowerProtocolVersion||state.partyVersion!=PartyProtocolVersion || !ValidTowerPayload(state) || !RemoteDangerPolicy.Run(state.run) || state.sequence < 1 ||
                state.enemies == null || state.enemies.Length > OracleCombat.MaxActiveEnemies ||
                state.hostileBolts == null || state.hostileBolts.Length != RemoteDangerPolicy.Capacity ||
                state.hostileCount < 0 || state.hostileCount > RemoteDangerPolicy.Capacity) return false;
            for (int i = 0; i < dangerEnemyIds.Length; i++) dangerEnemyIds[i] = false;
            foreach (EnemyState actor in state.enemies)
            {
                if (actor == null || actor.id < 1 || actor.id > RemoteDangerPolicy.MaximumActorId || dangerEnemyIds[actor.id] ||
                    !CoopPartyCapacity.LaneActive(state.geometryMask,actor.lane)||actor.archetype < 0 || actor.archetype > 2 || actor.shade != (actor.archetype == 1) ||
                    !RemoteDangerPolicy.Point(actor.position) || !Finite(actor.hp) || !Finite(actor.maximum) ||
                    actor.hp <= 0f || actor.maximum <= 0f || actor.maximum > 100000f || actor.hp > actor.maximum ||
                    actor.attackPhase == (int)OracleEnemy.GuardianAttackPhase.Dead ||
                    !RemoteDangerPolicy.Phase(actor.attackPhase, actor.attackProgress, actor.attackSequence) ||
                    !RemoteDangerPolicy.Direction(actor.lockedDirection) ||
                    !RemoteDangerPolicy.Style(actor.attackStyle, actor.windupSeconds, actor.strikeSeconds, actor.recoverySeconds) ||
                    !RemoteDangerPolicy.Point(actor.targetPoint) || actor.targetTowerId < 0 || actor.targetTowerId > 65535 ||
                    (actor.attackStyle == 2 && (actor.targetTowerId == 0 || (actor.targetPoint - actor.position).sqrMagnitude > 81f)) ||
                    (actor.attackStyle != 2 && actor.targetTowerId != 0) ||
                    (actor.attackPhase == (int)OracleEnemy.GuardianAttackPhase.Windup ||
                        actor.attackPhase == (int)OracleEnemy.GuardianAttackPhase.Strike) && actor.attackSequence == 0) return false;
                dangerEnemyIds[actor.id] = true;
            }
            for (int i = 0; i < dangerBoltSlots.Length; i++) dangerBoltSlots[i] = false;
            for (int i = 0; i < state.hostileCount; i++)
            {
                HostileBoltState bolt = state.hostileBolts[i];
                if (!RemoteDangerPolicy.Bolt(bolt)||!CoopPartyCapacity.LaneActive(state.geometryMask,bolt.lane) || dangerBoltSlots[bolt.slot]) return false;
                dangerBoltSlots[bolt.slot] = true;
            }
            // Fixed array tail is unused and cannot cause effects. Counted records
            // are all validated before any actor/visual/state mutation occurs.
            return true;
        }

        void ApplyState(Message state)
        {
            // The reliable campaign event (or room snapshot) must let root build the arena first.
            // A state packet can arrive on a different NGO delivery pipeline before that event.
            if (!owner.CoopMode || owner.World == null || !owner.World.IsCoopArena || string.IsNullOrEmpty(state.run) || state.enemies == null || state.enemies.Length > OracleCombat.MaxActiveEnemies || state.buildings == null || state.buildings.Length > MaximumPlacedShrines) return;
            if (!ValidDangerState(state)) return;
            int phaseLimit=CampaignActive?CampaignStagePacing.PhaseCount:CurrentDefensePacing.PhaseCount;
            if(!Finite(state.remaining)||state.remaining<0f||state.remaining>DefenseDurationSeconds||
                state.wave<1||state.wave>phaseLimit||state.quota<1||state.quota>200||state.spawned<0||state.spawned>state.quota)return;
            if(CampaignActive&&(state.quota!=CampaignStagePacing.AdmissionsPerStage||state.spawned>CampaignStagePacing.AdmissionsPerStage||
                state.result==1&&(state.spawned!=CampaignStagePacing.AdmissionsPerStage||state.remaining!=0f)))return;
            if (state.run == runId && state.sequence <= appliedSerial) return;
            if (!dangerRunGate.Accept(state.run, state.sequence)) return;
            if (runId != state.run)
            {
                string previousRun=runId;runId=state.run;
                if(peerHealth==null||!peerHealth.ResetRun(runId)){runId=previousRun;return;}
                if(CampaignActive)localForge.ReleasePendingAfterTimeout();else localForge.ResetForRun();
                appliedSerial = 0;
                endingApplied = false;
                owner.Combat.ClearEnemies();
                replicas.Clear();
                buildings.Clear();
                drops.Clear();
                liveDropIds.Clear();
                    if (owner.Builder != null) owner.Builder.ClearPlacedShrines();
                if (owner.Progression != null) owner.Progression.ResetNetworkXp();
                ResetPresentationState();
            }
            appliedSerial = state.sequence;
            hasRun = true;
            Running = state.running;
            clockStarted = state.clockStarted;
            result = state.result;
            RemainingSeconds = Mathf.Clamp(state.remaining, 0f, DefenseDurationSeconds);
            WaveNumber = state.wave;
            SpawnedThisWave = state.spawned;
            WaveQuota = state.quota;
            AliveCount = state.enemies.Length;
            simulationClock = DefenseDurationSeconds - RemainingSeconds;
            admissionLaneMask=state.admissionMask;activeLaneMask=state.geometryMask;activeLaneCount=state.activeLanes;
            owner.World.ApplyRemoteLaneState(admissionLaneMask,activeLaneMask);
            for(int i=0;i<state.health.Length;i++)peerHealth.TryApplyHostHealth(runId,state.health[i]);
            owner.Combat.SetRemoteSimulation(true);
            if (owner.Combat.Shrine != null) owner.Combat.Shrine.ApplyRemoteHealth(state.shrine);
            snapshotIds.Clear();
            foreach (var enemyState in state.enemies)
            {
                enemyLanes[enemyState.id]=enemyState.lane;
                if (!Finite(enemyState.position.x) || !Finite(enemyState.position.y) || !Finite(enemyState.position.z) || enemyState.id < 1 || enemyState.archetype < 0 || enemyState.archetype > 2) continue;
                snapshotIds.Add(enemyState.id);
                if (!replicas.TryGetValue(enemyState.id, out var enemy) || enemy == null)
                {
                    enemy = owner.Combat.SpawnEnemy(enemyState.position, enemyState.id, (OracleEnemy.MonsterArchetype)enemyState.archetype, enemyState.flying);
                    if (enemy == null) continue;
                    replicas[enemyState.id] = enemy;
                }
                enemy.ApplyRemoteState(enemyState.position, enemyState.hp, true, enemyState.maximum);
                enemy.ApplyRemoteAttackPresentation(enemyState.attackPhase, enemyState.attackProgress, enemyState.aggro,
                    enemyState.attackSequence, state.sequence, enemyState.lockedDirection, enemyState.attackStyle,
                    enemyState.windupSeconds, enemyState.strikeSeconds, enemyState.recoverySeconds,
                    enemyState.targetTowerId, enemyState.targetPoint);
            }
            removed.Clear();
            foreach (var replica in replicas) if (!snapshotIds.Contains(replica.Key)) removed.Add(replica.Key);
            foreach (int id in removed)
            {
                if (replicas[id] != null) replicas[id].ApplyRemoteState(replicas[id].transform.position, 0f, false);
                replicas.Remove(id);
            }
            owner.Combat.ApplyRemoteDangerState(state.hostileBolts, state.hostileCount);
            foreach (var previous in buildings)
            {
                bool found = false;
                foreach (var incoming in state.buildings) if (incoming.id == previous.id) { found = true; break; }
                if (!found)
                {
                    if (PlacedElementShrine.TryGetRegistered(previous.id, out var destroyed)) destroyed.ApplyRemoteHealth(0f);
                    owner.Builder?.RemovePlacedShrine(previous.id);
                }
            }
            buildings.Clear();
            foreach (var building in state.buildings)
            {
                if (building == null || building.id < 1 || building.element < 0 || building.element >= ShrinePlacementBuilder.TowerTypeCount ||
                    !Finite(building.health) || building.health <= 0f || !ValidFeedbackPoint(building.position)) continue;
                buildings.Add(building);
                if (owner.Builder != null) owner.Builder.ApplyPlacedShrine(building.id, building.element, building.position,building.tier,building.tierRevision);
                if (PlacedElementShrine.TryGetRegistered(building.id, out var tower)) tower.ApplyRemoteHealth(building.health);
            }
            if (state.players != null)
                foreach (var player in state.players)
                    if (player.id == rooms.LocalClientId)
                    {
                        localTokens = Mathf.Clamp(player.tokens, 0, 3);
                        ApplyPersonal(player, 0);
                    }
            if (state.drops != null && state.drops.Length <= 128 && owner.Progression != null)
            {
                liveDropIds.Clear();
                foreach (var drop in state.drops) if (drop != null && drop.id > 0) liveDropIds.Add(drop.id);
                removed.Clear();
                foreach (int id in drops.Keys) if (!liveDropIds.Contains(id)) removed.Add(id);
                foreach (int id in removed) owner.Progression.ConfirmNetworkXp(id);
                drops.Clear();
                foreach (var drop in state.drops)
                {
                    if (drop == null || drop.id < 1 || !Finite(drop.position.x) || !Finite(drop.position.y) || !Finite(drop.position.z)) continue;
                    drops[drop.id] = drop;
                    owner.Progression.SpawnNetworkXp(drop.id, drop.position, Mathf.Clamp(drop.amount, 1, 10));
                }
            }
            Status = Running ? clockStarted ? "Defend the green shrine · " + buildings.Count + "/" + ActiveTowerCapacity + " towers" : "Waiting for every connected player to enter the arena..." : result == 1 ? "The green shrine survived " + DefenseDurationLabel + "." : "The defense ended before stage completion.";
            if (!Running && result != 0 && !endingApplied) ApplyEnding(result == 1);
        }

        void EndDefense(bool victory)
        {
            if (!Running || !IsAuthority) return;
            if(victory&&CampaignActive&&CampaignStagePacing.Outcome(simulationClock,campaignAdmissions.Count,
                owner.Combat.Shrine!=null&&!owner.Combat.Shrine.IsDestroyed)!=CampaignStageOutcome.Succeeded)return;
            Running = false;
            result = victory ? 1 : 2;
            owner.Combat.ClearEnemies();
            AliveCount = 0;
            Status = victory ? "The green shrine survived " + DefenseDurationLabel + "." : "The defense ended before stage completion.";
            ApplyEnding(victory);
            SendState();
        }

        void ApplyEnding(bool victory)
        {
            endingApplied = true;
            owner.Combat.ClearEnemies();
            replicas.Clear();
            if(CampaignActive){campaign.NotifyDefenseEnded(victory,runId,RemainingSeconds);return;}
            owner.Victory = victory;
            if (!victory) owner.HP = 0f;
            owner.Objective = Status;
            owner.Pause();
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool ValidFeedbackPoint(Vector3 point) => Finite(point.x) && Finite(point.y) && Finite(point.z) &&
            Mathf.Abs(point.x) <= 512f && Mathf.Abs(point.z) <= 512f && point.y >= -25f && point.y <= 100f;
        static bool ValidAim(Vector3 direction) => Finite(direction.x) && Finite(direction.y) && Finite(direction.z) &&
            direction.sqrMagnitude >= .9f && direction.sqrMagnitude <= 1.1f;
        void OnDestroy()
        {
            dangerRunGate.ResetConnection();
            owner?.Combat?.ClearRemoteDangerState();
            if(peerHealth!=null){peerHealth.HealthChanged-=OnPeerHealthChanged;peerHealth.PoseCorrectionRequired-=OnPoseCorrection;peerHealth.ClearRun();if(ownsPeerHealth)Destroy(peerHealth);}
            if (rooms != null) rooms.ReliableMessageReceived -= OnMessage;
            if (feedbackCombat != null) { feedbackCombat.AuthoritativeShotHit -= OnAuthoritativeShotHit; feedbackCombat.AuthoritativeNovaImpact -= OnAuthoritativeNovaImpact; }
            if (towerFeedbackSubscribed) PlacedElementShrine.AuthoritativeShot -= OnAuthoritativeTowerShot;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ actor space, host Forge ledger and sole combat owners remain.
// Exact composed Refuge/interstage/balance adapter hunks; no new native/save authority.
