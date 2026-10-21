using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main-thread adapter over the existing room/reliable transport. Host alone
    // advances CampaignQuestState; client snapshots are presentation. World metres,
    // maximum8 scalar peer gates/3NPC targets; no service creation or wallet IO.
    [DisallowMultipleComponent]
    public sealed partial class CoopCampaignDirector : MonoBehaviour
    {
        [Serializable] sealed class Frame
        {
            public string kind, hello, campaign;
            public int version;
            public int sequence, request, epoch, stage, role;
            public bool ready;
            public CampaignSnapshot state;
        }
        sealed class Gate { public string Hello; public int Request, Count; public double Window; }
        readonly Dictionary<ulong, Gate> gates = new Dictionary<ulong, Gate>(8);
        readonly List<ulong> removed = new List<ulong>(8);
        readonly Transform[] npcRoots = new Transform[4];
        readonly Vector3[] npcFeet = new Vector3[4], npcAim = new Vector3[4];
        readonly RaycastHit[] sightHits = new RaycastHit[32];
        GameSession owner;
        CampaignSessionFlow flow;
        Action startHandler;
        Func<bool> activeHandler;
        RoomConnection rooms;
        CampaignQuestState model;
        CampaignSnapshot view;
        CampaignViewFence viewFence;
        string localHello, appliedCampaign;
        string preparationNotice = "";
        string cachedStatus = "Waiting for the connected host's campaign.";
        int requestSequence, packetSequence, appliedPacket, appliedEpoch;
        double nextHello, nextMembership, preparationStarted;
        public const double PreparationTimeoutSeconds=CampaignQuestState.PreparationTimeoutSeconds;
        bool helloAccepted, wasConnected;

        public const int ProtocolVersion = 12;
        const string HelloKind = "oracle.campaign.hello.v1", StateKind = "oracle.campaign.state.v1";
        const string VisitKind = "oracle.campaign.visit.v1", ReadyKind = "oracle.campaign.ready.v1", ReturnKind = "oracle.campaign.return.v1";
        const string FaultKind = "oracle.campaign.prepare.failed.v1";
        public event Action StateChanged;
        public CampaignPhase Phase => view != null ? (CampaignPhase)view.phase : CampaignPhase.None;
        public int Epoch => view?.epoch ?? 0;
        public int StageNumber => view != null ? view.stage : 1;
        public bool Active => Phase != CampaignPhase.None;
        public bool FirstStageCoreLoadout => view != null && view.firstStageCoreLoadout;
        public bool InRefuge => Phase == CampaignPhase.Refuge;
        public string CampaignId => view?.campaign ?? "";
        public string Context => view == null ? "" : view.campaign + ".refuge." + view.stage;
        public int MemberCount => view?.peers.Length ?? 0;
        public int LocalVisitMask => LocalPeer()?.visits ?? 0;
        public int LocalTowerMask => LocalPeer()?.towers ?? 0;
        public bool LocalReady => LocalPeer()?.ready ?? false;
        public int ReadyPlayerCount
        { get { int n = 0; if (view != null) foreach (var p in view.peers) if (p.ready) n++; return n; } }
        // Compatibility position only: the first actual active road, never a synthetic shrine exit.
        public static Vector3 ReturnPortalPosition=>GameSession.Instance!=null&&GameSession.Instance.World!=null&&GameSession.Instance.World.IsCoopArena?GameSession.Instance.World.GetInvasionPortal(0):Vector3.zero;
        public string Status => cachedStatus;
        string BuildStatus() => (string.IsNullOrEmpty(preparationNotice) ? "" : preparationNotice + " ") +
            (Phase == CampaignPhase.Refuge ? "Level " + StageNumber + "/" + CampaignQuestState.StageCount + " · " + InterstageBlockedReason + " (" + ReadyPlayerCount + "/" + (view?.peers.Length ?? 0) + " ready)." :
            Phase == CampaignPhase.Preparing ? "Preparing level " + StageNumber + "/" + CampaignQuestState.StageCount + " · Waiting for the party's arena barrier." :
            Phase == CampaignPhase.Defense ? "Level " + StageNumber + "/" + CampaignQuestState.StageCount + " · " + StageNumber + " active road(s). Clear five waves; forty waves span all eight milestones." :
            Phase == CampaignPhase.StageClear ? "Level " + StageNumber + "/" + CampaignQuestState.StageCount + " clear · " + InterstageBlockedReason :
            Phase == CampaignPhase.Complete ? "All eight challenge levels complete. Thank you for playing this prototype." :
            Phase == CampaignPhase.Failed ? "The milestone ended before all five waves were admitted and cleared. Your party remains connected." : "Waiting for the connected host's campaign.");
        bool Authority => rooms != null && rooms.Connected && rooms.IsHost;

        public void Initialize(GameSession session, Action prepareArena)
        {
            if (owner != null) throw new InvalidOperationException("Initialize the campaign once.");
            if(session==null||session.Rooms==null||session.Defense==null)throw new ArgumentNullException(nameof(session));
            owner = session; rooms = owner.Rooms; model = new CampaignQuestState(RoomConnection.MaximumPlayers);
            WarmInterstagePortalJob();
            flow=new CampaignSessionFlow(owner,this,prepareArena);
            startHandler=BeginConnectedCampaign;activeHandler=()=>Active;
            if(rooms.CampaignStartHandler!=null)throw new InvalidOperationException("The room already has a campaign lifecycle owner.");
            rooms.CampaignStartHandler=startHandler;rooms.CampaignLifecycleActive=activeHandler;
            owner.Defense.BindCampaign(this);
            InitializeSoloRestart();
            rooms.ReliableMessageReceived += OnMessage;
        }
        public static bool IsRefugeSession(GameSession session)
        {var campaign=session!=null?session.GetComponent<CoopCampaignDirector>():null;return campaign!=null&&campaign.InRefuge&&session.World!=null&&session.World.Mode020==WorldMode020.TutorialRefuge;}
        public void BeginConnectedCampaign()
        {
            if (rooms == null || !rooms.Connected) return;
            if (Authority) { if (!Active) StartInitialFromValidatedPortal(); }
            else { RotateHello(); SendHello(); }
        }
        void StartInitialFromValidatedPortal()
        {
            // CampaignActive is set only by Room's accepted common start nonce.
            // A local portal click, partial roster or menu CreateParty is insufficient.
            if (!Authority || Active || !rooms.CampaignActive || !rooms.CampaignRosterSettled ||
                !rooms.LocalReady || rooms.PlayerCount < 1 || rooms.PlayerCount > 2 ||
                rooms.PlayerCount > RoomConnection.MaximumPlayers || rooms.ReadyPlayerCount != rooms.PlayerCount) return;
            var members = ConnectedMembers();
            if (members.Length != rooms.PlayerCount) return;
            preparationNotice = "";
            owner.Defense.ClearSoloRestartRewardScope();
            if (!model.BeginFromPortal(Guid.NewGuid().ToString("N"), members)) return;
            Publish();
        }
        public void StartNewCampaign()
        {
            if (!Authority || Active && Phase != CampaignPhase.Complete && Phase != CampaignPhase.Failed) return;
            preparationNotice = "";
            owner.Defense.ClearSoloRestartRewardScope();
            if (!model.Begin(Guid.NewGuid().ToString("N"), ConnectedMembers())) return;
            Publish();
        }
        public void RegisterNpcTarget(int role, Transform targetRoot, Vector3 feet, Vector3 aim)
        {
            if (role < 1 || role > 3) return;
            if(targetRoot==null){npcRoots[role]=null;return;}
            if(!Finite(feet)||!Finite(aim))return;
            npcRoots[role] = targetRoot; npcFeet[role] = feet; npcAim[role] = aim;
        }
        public void RequestNpcVisit(int role) { Request(VisitKind, role, false); }
        public void RequestRaykonMentorVisit()
        {
            if(!InRefuge||owner==null||owner.World==null||!owner.World.TryGetRaykonInteractionTarget(out var root,out var feet,out var aim))return;
            RegisterNpcTarget(CampaignQuestState.RunekeeperRole,root,feet,aim);
            RequestNpcVisit(CampaignQuestState.RunekeeperRole);
        }
        public void SetReady(bool ready) { if (ready) { preparationNotice = ""; cachedStatus = BuildStatus(); } Request(ReadyKind, 0, ready); }
        public void RequestReturnToRefuge() { Request(ReturnKind, 0, false); }
        public void ReportPreparationFailure(string reason)
        {
            preparationNotice = string.IsNullOrWhiteSpace(reason) ? "Arena preparation failed. Ready again to retry." : reason.Substring(0, Math.Min(reason.Length, 160));
            cachedStatus = BuildStatus();
            Request(FaultKind, 0, false);
        }
        public bool CanBuildTower(ulong peer, int type) => Authority && model.CanBuild(peer, type);
        public bool CanUseSmith(ulong peer) => Authority && model.HasVisit(peer,1) && NpcReach(peer,1);
        public bool CanUseRunekeeper(ulong peer) => Authority && model.CanLearn(peer) && NpcReach(peer, CampaignQuestState.RunekeeperRole);
        public void NotifyDefenseEnded(bool survived, string hostRun, float remaining)
        {
            if (Authority && model.CompleteDefense(hostRun, remaining, survived, owner.Defense.CampaignAcceptedAdmissions, owner.Defense.CampaignCompletedWaves)) Publish();
        }
        public void CancelCampaign()
        {
            view = null; appliedCampaign = null; appliedEpoch = 0; helloAccepted = false;localHello=null;viewFence.Clear();
            cachedStatus = BuildStatus();
            interstageReturnBlocked="";
            StateChanged?.Invoke();
        }
        void Update()
        {
            if (owner == null || rooms == null) return;
            if(TickSoloRestart())return;
            bool connected = rooms.Connected;
            if (!owner.HasStarted && Active) { CancelCampaign(); return; }
            if (!connected)
            {
                if (wasConnected)
                {
                    bool hadCampaign = Active;
                    gates.Clear(); appliedPacket = packetSequence = requestSequence = 0; CancelCampaign();
                    if (hadCampaign) { owner.CombatUnlocked = false; owner.Objective = "The party connection ended. " + rooms.Status; owner.Pause(); }
                }
                wasConnected = false; return;
            }
            wasConnected = true;
            if (!Authority)
            {
                if (!helloAccepted && Time.realtimeSinceStartupAsDouble >= nextHello) SendHello();
            }
            else if (Active)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (now >= nextMembership)
                {
                    nextMembership = now + .25d;
                    if (MembershipChanged())
                    {
                        model.ReplaceMembers(ConnectedMembers());
                        removed.Clear(); foreach (var pair in gates) if (!ConnectedId(pair.Key)) removed.Add(pair.Key);
                        foreach (ulong id in removed) gates.Remove(id);
                        if (model.Phase==CampaignPhase.Refuge) owner.Defense.ReconcileCampaignMembers();
                        if (model.AllReady&&rooms.CampaignRosterSettled) model.BeginPreparation();
                        Publish();
                    }
                }
                if(model.AllReady&&rooms.CampaignRosterSettled&&model.BeginPreparation())Publish();
                if(model.Phase==CampaignPhase.Preparing&&CampaignQuestState.PreparationTimedOut(preparationStarted,now))
                {preparationNotice="The party did not finish preparing. Ready again to retry.";if(model.AbortPreparation())Publish();}
                if (model.Phase == CampaignPhase.Preparing && owner.Defense.ArenaReady &&
                    model.BeginDefense(owner.Defense.CurrentRunId)) Publish();
            }
            if(Active&&owner.Objective!=Status)owner.Objective=Status;
            if (Phase == CampaignPhase.StageClear && owner.IsPlaying && !AbilityBindings.IsRebinding && Keyboard.current != null &&
                Keyboard.current[GameActionBindings.GetKey("Interact")].wasPressedThisFrame &&
                LocalStageClearPortal())
                RequestReturnToRefuge();
        }
        void RotateHello()
        { localHello = Guid.NewGuid().ToString("N"); viewFence.Begin(localHello); helloAccepted = false; appliedPacket = 0; requestSequence = 0; }
        void SendHello()
        {
            if (Authority || !rooms.Connected) return;
            if (localHello == null) RotateHello();
            nextHello = Time.realtimeSinceStartupAsDouble + 1d;
            rooms.SendReliable(JsonUtility.ToJson(new Frame { kind = HelloKind, version=ProtocolVersion, hello = localHello }));
        }
        void Request(string kind, int role, bool ready)
        {
            if (!Active || !rooms.Connected) return;
            var frame = new Frame { kind = kind, version=ProtocolVersion, campaign = view.campaign, stage = view.stage, epoch = view.epoch,
                request = ++requestSequence, role = role, ready = ready, hello = localHello };
            if (Authority && rooms.IsCampaignMemberRegistered(rooms.LocalClientId)) AcceptRequest(rooms.LocalClientId, frame);
            else if (helloAccepted) rooms.SendReliable(JsonUtility.ToJson(frame));
        }
        void OnMessage(ulong sender, string payload)
        {
            if(soloRestart.HoldsSimulation)return;
            if (rooms == null || !rooms.Connected || string.IsNullOrEmpty(payload) || payload.Length > 4096 ||
                payload.IndexOf("\"kind\":\"oracle.campaign.", StringComparison.Ordinal) < 0) return;
            Frame frame; try { frame = JsonUtility.FromJson<Frame>(payload); } catch (ArgumentException) { return; }
            if (frame == null || frame.version!=ProtocolVersion) return;
            if (Authority)
            {
                if (!ConnectedId(sender) || !rooms.IsCampaignMemberRegistered(sender)) return;
                if (!gates.TryGetValue(sender, out var gate)) { gate = new Gate(); gates[sender] = gate; }
                double now = Time.realtimeSinceStartupAsDouble;
                if (now - gate.Window >= 1d) { gate.Window = now; gate.Count = 0; }
                if (++gate.Count > 8) return;
                if (frame.kind == HelloKind)
                {
                    if (!CampaignQuestState.ValidNonce(frame.hello)) return;
                    if (gate.Hello != frame.hello) { gate.Hello = frame.hello; gate.Request = 0; }
                    SendStateTo(sender, ++packetSequence);
                    if(InRefuge)owner.Defense.SendCampaignPersonalTo(sender);return;
                }
                if (gate.Hello == null || frame.hello != gate.Hello || frame.request <= gate.Request) return;
                gate.Request = frame.request; AcceptRequest(sender, frame);
            }
            else if (frame.kind == StateKind && viewFence.TryAccept(sender,NetworkManager.ServerClientId,
                frame.version,frame.hello,frame.sequence,frame.state,RoomConnection.MaximumPlayers,rooms.LocalClientId))
            {
                appliedPacket = frame.sequence; helloAccepted = true; ApplyLocal(frame.state);
            }
        }
        void AcceptRequest(ulong sender, Frame frame)
        {
            if (!Authority || !Active || frame.campaign != model.CampaignId || frame.stage != model.StageNumber || frame.epoch != model.Epoch || !ConnectedId(sender)) return;
            bool changed = false;
            if (frame.kind == VisitKind && NpcReach(sender, frame.role)) changed = model.AcceptNpcVisit(sender, frame.role);
            else if (frame.kind == ReadyKind && (!frame.ready || PortalReach(sender, false) || NpcReach(sender, 3))) changed = model.SetReady(sender, frame.ready);
            else if (frame.kind == ReturnKind && PortalReach(sender, true)) changed = model.ReturnToRefuge(sender);
            else if (frame.kind == FaultKind && model.Phase == CampaignPhase.Preparing)
            {
                if (string.IsNullOrEmpty(preparationNotice)) preparationNotice = "A party member could not prepare the arena. Ready again to retry.";
                changed = model.AbortPreparation();
            }
            if (model.AllReady&&rooms.CampaignRosterSettled) { model.BeginPreparation(); changed = true; }
            // Even an idempotent/declined request receives the current accepted state.
            if (changed) Publish(); else SendStateTo(sender, ++packetSequence);
        }
        void Publish()
        {
            CampaignSnapshot next = model.Snapshot(); int sequence = ++packetSequence;
            // Queue the phase before any local transition sends stage/personal messages.
            foreach (ulong id in rooms.ConnectedClientIds) if (id != rooms.LocalClientId) SendStateTo(id, sequence, next);
            ApplyLocal(next);
            if(InRefuge)foreach(ulong id in rooms.ConnectedClientIds)owner.Defense.SendCampaignPersonalTo(id);
        }
        void SendStateTo(ulong id, int sequence, CampaignSnapshot state = null)
        {
            if (!Authority || !Active && state == null) return;
            if (id == rooms.LocalClientId) return;
            if (!gates.TryGetValue(id, out var gate) || gate.Hello == null) return;
            rooms.SendReliableTo(id, JsonUtility.ToJson(new Frame { kind = StateKind, version=ProtocolVersion, hello = gate.Hello,
                sequence = sequence, state = state ?? model.Snapshot() }));

        }
        void ApplyLocal(CampaignSnapshot next)
        {
            bool newCampaign = next.campaign != appliedCampaign;
            bool transition = newCampaign || next.epoch != appliedEpoch;
            bool sameRefuge = !newCampaign && view != null && view.phase == (int)CampaignPhase.Refuge &&
                next.phase == (int)CampaignPhase.Refuge && view.stage == next.stage;
            var previous=view;
            view = next;
            CacheAcceptedSoloRestartBoundary(next);
            cachedStatus = BuildStatus();
            if (transition)
            {
                appliedCampaign = next.campaign; appliedEpoch = next.epoch;
                if(Phase==CampaignPhase.Preparing)preparationStarted=Time.realtimeSinceStartupAsDouble;
                if (Phase == CampaignPhase.Refuge)
                {
                    // A ready epoch alone must not rebuild the refuge/teleport peers.
                    if (!sameRefuge)
                    {
                        if(!newCampaign && previous!=null && previous.phase==(int)CampaignPhase.Preparing) flow.AbortPreparation();
                        else flow.EnterRefuge(newCampaign);
                    }
                }
                else if (Phase == CampaignPhase.Preparing && LocalPeer()?.participating == true) flow.PrepareArena();
                else if (Phase == CampaignPhase.StageClear) flow.StageClear();
                else if (Phase == CampaignPhase.Complete || Phase == CampaignPhase.Failed) flow.Ending(Phase == CampaignPhase.Complete);
            }
            if(Phase!=CampaignPhase.StageClear)interstageReturnBlocked="";
            owner.Objective = Status; StateChanged?.Invoke();
        }
        CampaignPeerSnapshot LocalPeer()
        { if (view != null && rooms != null) foreach (var peer in view.peers) if (peer.id == rooms.LocalClientId) return peer; return null; }
        public bool NpcReach(ulong id, int role)
        {
            if (!InRefuge || role < 1 || role > 3 || owner.World.Mode020!=WorldMode020.TutorialRefuge) return false;
            if (role == CampaignQuestState.SmithRole && owner.Shop?.Blacksmith != null)
                RegisterNpcTarget(role, owner.Shop.Blacksmith.transform, owner.Shop.Blacksmith.TowerAnvilFeet, owner.Shop.Blacksmith.TowerAnvilAimPoint);
            if(role==CampaignQuestState.RunekeeperRole)
            {
                if(!owner.World.TryGetRaykonInteractionTarget(out var raykon,out var feetAtRaykon,out var aimAtRaykon))return false;
                RegisterNpcTarget(role,raykon,feetAtRaykon,aimAtRaykon);
            }
            Transform target = npcRoots[role];
            if (target == null || !target.gameObject.activeInHierarchy || !rooms.TryGetPlayerPose(id, out var feet, out _) ||
                !rooms.TryGetPlayerAim(id, out var facing) || !Finite(feet) || !Finite(facing) ||
                facing.sqrMagnitude<.9f || facing.sqrMagnitude>1.1f ||
                (feet - npcFeet[role]).sqrMagnitude > 3.25f * 3.25f) return false;
            Vector3 eye = feet + Vector3.up * 1.65f, direction = npcAim[role] - eye;
            return direction.sqrMagnitude > .001f && Vector3.Dot(facing.normalized, direction.normalized) >= .65f && Visible(eye, npcAim[role], target);
        }
        bool PortalReach(ulong id, bool returning)
        {
            if (returning ? Phase != CampaignPhase.StageClear || !owner.World.IsCoopArena : !InRefuge || owner.World.Mode020!=WorldMode020.TutorialRefuge) return false;
            if(returning)return rooms.TryGetPlayerPose(id,out var acceptedFeet,out _)&&TryStageClearPortal(acceptedFeet,CampaignPortalMath.HostReach,out _,out _,out _,out _);
            Vector3 point=owner.World.PortalPosition;
            return rooms.TryGetPlayerPose(id, out var feet, out _) && Finite(feet) &&
                (feet - point).sqrMagnitude <= 3.4f * 3.4f && Visible(feet + Vector3.up * 1.65f, point + Vector3.up * 1.5f, null);
        }
        bool Visible(Vector3 from, Vector3 to, Transform allowed)
        {
            Vector3 delta = to - from;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, sightHits, delta.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Transform hit = sightHits[i].transform;
                if (hit == null || allowed != null && (hit == allowed || hit.IsChildOf(allowed)) ||
                    hit.GetComponentInParent<OraclePlayer>() != null || hit.GetComponentInParent<RemoteOracleAvatar>() != null) continue;
                return false;
            }
            return true;
        }
        bool MembershipChanged()
        {
            if (view == null || view.peers.Length != rooms.ConnectedClientIds.Count) return true;
            foreach (ulong id in rooms.ConnectedClientIds) if (!Contains(view, id)) return true;
            return false;
        }
        ulong[] ConnectedMembers()
        {
            var ids = new ulong[rooms.ConnectedClientIds.Count]; int i = 0;
            foreach (ulong id in rooms.ConnectedClientIds) ids[i++] = id;
            return ids;
        }
        bool ConnectedId(ulong id) { foreach (ulong peer in rooms.ConnectedClientIds) if (peer == id) return true; return false; }
        static bool Contains(CampaignSnapshot s, ulong id) { foreach (var p in s.peers) if (p.id == id) return true; return false; }
        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) &&
            !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        void OnDestroy()
        {
            DisposeSoloRestart();
            if (rooms != null)
            {
                rooms.ReliableMessageReceived -= OnMessage;
                if(rooms.CampaignStartHandler==startHandler)rooms.CampaignStartHandler=null;
                if(rooms.CampaignLifecycleActive==activeHandler)rooms.CampaignLifecycleActive=null;
            }

        }
    }
}
// # ARCHITECTURE SNAPSHOT
// World metres/Y-up/XZ; existing station mesh/pivots and canonical host roles remain.
// Bounded cached collider/root references only; no new economy/native buffers or quest bits.
// Root owns Unity integration and native acceptance; this is a guarded work candidate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing host-owned campaign identities/phase and accepted arena world coordinates.
// Array Formats: Explicit stage-one baseline entitlement scalar; inherited bounded peers and real arena barrier.
// Dependencies/Pointers: Room validates the full ready roster before first Preparing; no fabricated NPC visits, HP, clock, reward or protocol change.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Stable existing arena road IDs and host-owned eight-level campaign identity.
// Array Formats: Existing two peers/eight lane arrays/four shared pads/48 actors/4096 reward tombstones; no ABI growth.
// Dependencies/Pointers: Level-derived lane prefix, actual host ready/admission/recovery fences; root owns shared protocol and native acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual cleared-level monster portal world anchors and refuge-only Smith; stable same-party campaign identity.
// Array Formats: Bounded cold8-point reach job and existing phase/ready/Forge stores; no new visual/actor/wire field.
// Dependencies/Pointers: Existing host phase/epoch/pose/LOS and interlevel visits/readiness own progression; no midwave exit, battlefield shop or SDK session reset.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world and accepted015 campaign/source identity, original arena positions retained.
// Array Formats: Eight milestones/40waves/905logical rewards within4096native slots, fixed128B XP channel and existing bounded pools.
// Dependencies/Pointers: Root composes actual current pool/audio owners with reviewed campaign/XP hunks; source/native/browser/performance/public acceptance remain separate.
