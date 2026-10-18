using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    /// <summary>Recoverable player defeat. Only the connected host authorizes teammate revivals.</summary>
    [DefaultExecutionOrder(-80), DisallowMultipleComponent]
    public sealed class OracleRevival : MonoBehaviour
    {
        private const float SoloDelay = 6f, PartyDelay = 15f, HoldDuration = 2f;
        private const float HelpRange = 2.5f, HoldInterval = .2f, HoldLease = .45f;
        private const string Prefix = "oracle.revival.";
        internal sealed class Peer
        {
            public ulong Id, Helper;
            public bool Downed, Tutorial, HasHelper;
            public float Remaining, Progress, Shield;
            public double LastHold, LastSync = -10d;
            public int DownSequence, HoldSequence;
        }
        [Serializable] private sealed class PeerView
        {
            public ulong id;
            public bool downed, tutorial;
            public float remaining, progress, shield;
            public int report;
        }
        [Serializable] private sealed class Message
        {
            public string kind, context, request, run;
            public int sequence;
            public ulong target;
            public bool held, tutorial, arena;
            public PeerView[] peers;
        }

        private readonly Dictionary<ulong, Peer> peers = new Dictionary<ulong, Peer>(PeerCombatLedger.Capacity);
        private readonly List<ulong> removed = new List<ulong>(PeerCombatLedger.Capacity);
        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        private GameSession session;
        private RoomConnection rooms;
        private string context = "", syncRequest = "", roomCode = "";
        private bool wasNetworked, networkArena, localTutorial, sentHolding;
        private ulong heldTarget;
        private int downSequence, holdSequence, stateSequence, appliedSequence;
        private double clock, nextState, nextSync, nextReport, nextHold;
        private float shieldRemaining;
        PeerCombatAuthority combatHealth;
        public event Action<ulong> AuthoritativePeerRevived;
        bool IsNetworkArena=>session!=null&&session.CoopMode&&session.World!=null&&session.World.IsCoopArena;
        bool ArenaHealthAuthority=>Networked&&IsNetworkArena&&session.Defense!=null&&session.Defense.PeerHealthAuthoritative;

        public bool IsDowned { get; private set; }
        public bool IsHelping { get; private set; }
        public float ReviveRemainingSeconds { get; private set; }
        public float ReviveProgress { get; private set; }
        public bool Invulnerable
        {
            get
            {
                if(IsNetworkArena)return combatHealth==null||rooms==null||!combatHealth.TryGetPeerHealth(rooms.LocalClientId,out var health)||
                    health.Downed||health.ShieldRemaining>0f;
                return IsDowned||shieldRemaining>0f;
            }
        }
        public bool CanBeRevived => IsDowned && !localTutorial && Networked && rooms.PlayerCount > 1;
        public string DownedHint
        {
            get
            {
                if (IsDowned)
                    return (CanBeRevived ? "You are downed · an ally can help · recovery in " : "You are downed · recovery in ") +
                        Mathf.CeilToInt(ReviveRemainingSeconds) + "s";
                return TryFindNearbyDowned(out _) ? "Hold [" + GameActionBindings.GetLabel("Interact") + "] for 2 seconds to revive your teammate" : "";
            }
        }

        private bool ActiveGame => session != null && session.HasStarted && !session.Victory && session.World != null &&
            (session.Combat == null || session.Combat.Shrine == null || !session.Combat.Shrine.IsDestroyed);
        private bool Networked => ActiveGame && session.CoopMode && rooms != null && rooms.Connected;

        public void Initialize(GameSession owner)
        {
            session = owner;
            SubscribeRoom();
            ResetState();
        }

        public void ResetState()
        {
            IsDowned = IsHelping = localTutorial = sentHolding = false;
            ReviveRemainingSeconds = ReviveProgress = shieldRemaining = 0f;
            peers.Clear();
            context = syncRequest = roomCode = "";
            wasNetworked = false;
            downSequence = holdSequence = stateSequence = appliedSequence = 0;
            nextState = nextSync = nextReport = nextHold = clock = 0d;
            if (session != null && session.Player != null) session.Player.SetDownedPose(false);
        }

        public void EnterDowned(bool tutorial = false)
        {
            if (!ActiveGame || IsDowned || shieldRemaining > 0f || session.Player == null) return;
            SubscribeRoom();
            EnsureNetworkMode();
            if(IsNetworkArena)return; // Arena falls arrive only from committed host health.
            localTutorial = tutorial;
            IsDowned = true;
            IsHelping = false;
            session.HP = 0f;
            downSequence++;
            ReviveRemainingSeconds = AutoDelay(tutorial);
            ReviveProgress = 0f;
            session.Player.SetDownedPose(true);
            if (session.Builder != null) session.Builder.CancelPlacement();
            nextReport = 0d;
            if (Networked && rooms.IsHost) AcceptDown(rooms.LocalClientId, downSequence, tutorial);
            else if (Networked) SendDownReport();
        }

        // # ARCHITECTURE SNAPSHOT
        // Arena HP/report/revision belong to the host ledger. Recovery owns only
        // validated helper leases/timers. No arena down report or timer packet
        // can author HP; commit paths contain no callback/yield between owners.
        internal readonly struct DownCommit
        {
            internal readonly Peer Peer;
            internal readonly string Context;
            internal readonly int Report;
            internal DownCommit(Peer peer,string context,int report){Peer=peer;Context=context;Report=report;}
        }
        internal void SetPeerCombatAuthority(PeerCombatAuthority value){combatHealth=value;}
        internal void ClearPeerCombatAuthority(PeerCombatAuthority expected){if(combatHealth==expected)combatHealth=null;}
        internal bool TryPreparePeerDowned(ulong id,int report,out DownCommit token)
        {
            token=default;SubscribeRoom();EnsureNetworkMode();
            if(!ArenaHealthAuthority||rooms==null||!rooms.IsHost||!ConnectedId(id)||report<1)return false;
            ReconcilePeers();
            if(!peers.TryGetValue(id,out var peer)||peer.Downed||peer.DownSequence!=report-1||
                !rooms.TryGetPlayerPose(id,out Vector3 position,out _)||!Finite(position))return false;
            token=new DownCommit(peer,context,report);return true;
        }
        internal void CommitPeerDowned(DownCommit token)
        {
            // Only the authority calls this immediately after successful pure
            // ledger Damage, with no callback or native operation in between.
            if(token.Peer==null||token.Context!=context||token.Peer.DownSequence!=token.Report-1)
                throw new InvalidOperationException("A prepared host fall must commit in the same main-thread transaction.");
            Peer peer=token.Peer;peer.DownSequence=token.Report;peer.Downed=true;peer.Tutorial=false;
            peer.Remaining=AutoDelay(false);peer.Shield=peer.Progress=0f;peer.HasHelper=false;nextState=0d;
        }
        public bool TryGetPeerRevivalState(ulong id,out bool downed,out float shield,out int report)
        {
            if(peers.TryGetValue(id,out var peer)){downed=peer.Downed;shield=peer.Shield;report=peer.DownSequence;return true;}
            downed=false;shield=0;report=0;return false;
        }
        internal void ApplyAcceptedArenaHealth(PeerHealthSnapshot snapshot)
        {
            SubscribeRoom();EnsureNetworkMode();
            if(!ArenaHealthAuthority||!snapshot.Present||snapshot.RunId!=session.Defense.CurrentRunId||
                rooms==null||snapshot.PeerId!=rooms.LocalClientId||session.Player==null)return;
            bool wasDowned=IsDowned;
            session.HP=snapshot.Health;session.MaxHP=snapshot.MaximumHealth;downSequence=snapshot.DownReport;
            IsDowned=snapshot.Downed;localTutorial=false;shieldRemaining=snapshot.ShieldRemaining;
            if(IsDowned)
            {
                IsHelping=false;if(!wasDowned)ReviveRemainingSeconds=AutoDelay(false);
                session.Player.SetDownedPose(true);session.Builder?.CancelPlacement();
            }
            else
            {
                ReviveRemainingSeconds=ReviveProgress=0;session.Player.SetDownedPose(false);
                if(wasDowned&&rooms.TryGetAdmissionSlot(rooms.LocalClientId,out int slot))
                    session.Player.Teleport(session.World.GetCoopPlayerSpawn(slot),session.World.GetCoopPlayerSpawnYaw(slot));
            }
        }

        public bool IsPeerDowned(ulong clientId)
        {
            if (rooms != null && clientId == rooms.LocalClientId && IsDowned) return true;
            return peers.TryGetValue(clientId, out Peer peer) && peer.Downed;
        }

        private void SubscribeRoom()
        {
            RoomConnection current = session != null ? session.Rooms : null;
            if (rooms == current) return;
            if (rooms != null) rooms.ReliableMessageReceived -= OnMessage;
            rooms = current;
            if (rooms != null) rooms.ReliableMessageReceived += OnMessage;
        }

        private void EnsureNetworkMode()
        {
            bool connected = Networked;
            bool arena = session != null && session.World != null && session.World.IsCoopArena;
            if (connected && (!wasNetworked || roomCode != rooms.Code || networkArena != arena))
            {
                peers.Clear();
                roomCode = rooms.Code;
                networkArena = arena;
                context = rooms.IsHost ? Guid.NewGuid().ToString("N") : "";
                syncRequest = Guid.NewGuid().ToString("N");
                stateSequence = appliedSequence = 0;
                sentHolding = false;
                IsHelping = false;
                nextState = nextSync = nextReport = 0d;
                if (rooms.IsHost)
                {
                    ReconcilePeers();
                    if (IsDowned) AcceptDown(rooms.LocalClientId, downSequence, localTutorial);
                }
            }
            else if (!connected && wasNetworked)
            {
                peers.Clear();
                context = "";
                sentHolding = false;
                IsHelping = false;
                if (IsDowned) ReviveRemainingSeconds = Mathf.Min(ReviveRemainingSeconds, SoloDelay);
            }
            wasNetworked = connected;
        }

        private void Update()
        {
            SubscribeRoom();
            if (!ActiveGame)
            {
                if (IsDowned || wasNetworked || peers.Count > 0) ResetState();
                return;
            }
            EnsureNetworkMode();
            float dt = Time.unscaledDeltaTime;
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f) dt = 0f;
            dt = Mathf.Min(dt, .1f);
            clock += dt;
            shieldRemaining = Mathf.Max(0f, shieldRemaining - dt);
            if (!Networked)
            {
                if(IsNetworkArena)return; // Disconnect is not a solo heal authority.
                if (IsDowned)
                {
                    ReviveRemainingSeconds = Mathf.Max(0f, ReviveRemainingSeconds - dt);
                    if (ReviveRemainingSeconds <= 0f) ReviveLocal();
                }
                return;
            }
            if (!rooms.IsHost && clock >= nextSync && string.IsNullOrEmpty(context))
            {
                nextSync = clock + 1d;
                rooms.SendReliable(JsonUtility.ToJson(new Message
                    { kind = Prefix + "hello.v1", request = syncRequest, arena = networkArena, run = networkArena ? session.Defense.CurrentRunId : null }));
            }
            if (rooms.IsHost)
            {
                ReconcilePeers();
                if (IsDowned) AcceptDown(rooms.LocalClientId, downSequence, localTutorial);
            }
            else if (IsDowned && clock >= nextReport) SendDownReport();
            UpdateLocalHold();
            if (rooms.IsHost)
            {
                if(IsNetworkArena&&!ArenaHealthAuthority)return;
                TickAuthority(dt);
                if (clock >= nextState) { nextState = clock + .2d; SendState(); }
            }
            else if (IsDowned) ReviveRemainingSeconds = Mathf.Max(0f, ReviveRemainingSeconds - dt);
        }

        private float AutoDelay(bool tutorial) => tutorial || !Networked || rooms.PlayerCount <= 1 ? SoloDelay : PartyDelay;

        private void ReconcilePeers()
        {
            removed.Clear();
            foreach (ulong id in peers.Keys) if (!ConnectedId(id)) removed.Add(id);
            for (int i = 0; i < removed.Count; i++) peers.Remove(removed[i]);
            IReadOnlyList<ulong> ids = rooms.ConnectedClientIds;
            for (int i = 0; i < ids.Count && peers.Count < RoomConnection.MaximumPlayers; i++)
                if (!peers.ContainsKey(ids[i])) peers.Add(ids[i], new Peer { Id = ids[i] });
        }

        private bool ConnectedId(ulong id)
        {
            if (rooms == null || !rooms.Connected) return false;
            // NGO's complete transport membership is server-owned. Clients use
            // the bounded membership in their accepted host revival snapshot.
            if (!rooms.IsHost) return id == rooms.LocalClientId || peers.ContainsKey(id);
            IReadOnlyList<ulong> ids = rooms.ConnectedClientIds;
            for (int i = 0; i < ids.Count && i < RoomConnection.MaximumPlayers; i++) if (ids[i] == id) return true;
            return false;
        }

        private void SendDownReport()
        {
            if(IsNetworkArena)return;
            if (!Networked || rooms.IsHost || !IsDowned || string.IsNullOrEmpty(context)) return;
            if (peers.TryGetValue(rooms.LocalClientId, out Peer known) && known.DownSequence >= downSequence) return;
            nextReport = clock + .5d;
            rooms.SendReliable(JsonUtility.ToJson(new Message
                { kind = Prefix + "down.v1", context = context, sequence = downSequence, tutorial = localTutorial, arena = networkArena, run = networkArena ? session.Defense.CurrentRunId : null }));
        }

        private void AcceptDown(ulong sender, int sequence, bool tutorial)
        {
            if (!peers.TryGetValue(sender, out Peer peer) || peer.Shield > 0f || sequence <= peer.DownSequence || sequence < 1 ||
                !rooms.TryGetPlayerPose(sender, out Vector3 position, out _) || !Finite(position)) return;
            // Existing remote HP is client-owned. A report can down its sender
            // only; it cannot identify or revive another player.
            peer.DownSequence = sequence;
            if (peer.Downed) return;
            peer.Downed = true;
            peer.Tutorial = tutorial && !networkArena;
            peer.Remaining = AutoDelay(peer.Tutorial);
            peer.Shield = peer.Progress = 0f;
            peer.HasHelper = false;
            nextState = 0d;
        }

        private void UpdateLocalHold()
        {
            Peer target = null;
            bool found = !IsDowned && TryFindNearbyDowned(out target);
            bool held = found && !session.Paused && Application.isFocused && Cursor.lockState == CursorLockMode.Locked &&
                !AbilityBindings.IsRebinding && Keyboard.current != null && Keyboard.current[GameActionBindings.GetKey("Interact")].isPressed;
            IsHelping = held;
            ulong targetId = found ? target.Id : heldTarget;
            if (sentHolding && (!held || targetId != heldTarget)) SendHold(heldTarget, false);
            if (held && (!sentHolding || targetId != heldTarget || clock >= nextHold)) SendHold(targetId, true);
            sentHolding = held;
            heldTarget = targetId;
            if (!IsDowned) ReviveProgress = found ? Mathf.Clamp01(target.Progress / HoldDuration) : 0f;
        }

        private void SendHold(ulong target, bool held)
        {
            nextHold = clock + HoldInterval;
            holdSequence++;
            if (rooms.IsHost) AcceptHold(rooms.LocalClientId, target, held, holdSequence);
            else if (!string.IsNullOrEmpty(context)) rooms.SendReliable(JsonUtility.ToJson(new Message
                { kind = Prefix + "hold.v1", context = context, sequence = holdSequence, target = target, held = held, arena = networkArena, run = networkArena ? session.Defense.CurrentRunId : null }));
        }

        private void AcceptHold(ulong sender, ulong targetId, bool held, int sequence)
        {
            if (sender == targetId || !peers.TryGetValue(sender, out Peer helper) || !peers.TryGetValue(targetId, out Peer target) ||
                sequence <= helper.HoldSequence || sequence < 1) return;
            helper.HoldSequence = sequence;
            if (!held)
            {
                if (target.HasHelper && target.Helper == sender) { target.HasHelper = false; target.Progress = 0f; }
                return;
            }
            if (helper.Downed || !target.Downed || target.Tutorial || !CanHelp(sender, targetId)) return;
            if (target.HasHelper && target.Helper != sender) return;
            target.HasHelper = true;
            target.Helper = sender;
            target.LastHold = clock;
        }

        private void TickAuthority(float dt)
        {
            foreach (Peer peer in peers.Values)
            {
                peer.Shield = Mathf.Max(0f, peer.Shield - dt);
                if (!peer.Downed) continue;
                peer.Remaining = Mathf.Max(0f, peer.Remaining - dt);
                if (peer.HasHelper && clock - peer.LastHold <= HoldLease && CanHelp(peer.Helper, peer.Id) &&
                    peers.TryGetValue(peer.Helper, out Peer helper) && !helper.Downed)
                    peer.Progress = Mathf.Min(HoldDuration, peer.Progress + dt);
                else { peer.HasHelper = false; peer.Progress = 0f; }
                if (peer.Remaining <= 0f || peer.Progress >= HoldDuration)
                {
                    bool committedArena=IsNetworkArena;
                    if(committedArena&&(combatHealth==null||!combatHealth.TryCommitRevive(peer.Id,peer.DownSequence)))continue;
                    peer.Downed = peer.HasHelper = false;
                    peer.Remaining = peer.Progress = 0f;
                    peer.Shield = 2f;
                    if(committedArena)combatHealth.PublishRevival(peer.Id);
                    AuthoritativePeerRevived?.Invoke(peer.Id);
                    nextState = 0d;
                }
                if (peer.Id == rooms.LocalClientId) ApplyLocal(peer);
            }
        }

        private bool TryFindNearbyDowned(out Peer target)
        {
            target = null;
            if (!Networked || IsDowned) return false;
            foreach (Peer peer in peers.Values)
                if (peer.Id != rooms.LocalClientId && peer.Downed && !peer.Tutorial && CanHelp(rooms.LocalClientId, peer.Id))
                { target = peer; return true; }
            return false;
        }

        private bool CanHelp(ulong helper, ulong target)
        {
            if (helper == target || !ConnectedId(helper) || !ConnectedId(target) ||
                !rooms.TryGetPlayerPose(helper, out Vector3 from, out _) || !rooms.TryGetPlayerPose(target, out Vector3 to, out _) ||
                !Finite(from) || !Finite(to) || (from - to).sqrMagnitude > HelpRange * HelpRange) return false;
            Vector3 origin = from + Vector3.up * 1.1f, destination = to + Vector3.up * .5f;
            Vector3 delta = destination - origin;
            if (delta.sqrMagnitude < .001f) return true;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, sightHits, delta.magnitude,
                ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider hit = sightHits[i].collider;
                if (hit == null || hit.GetComponentInParent<OraclePlayer>() != null ||
                    hit.GetComponentInParent<RemoteOracleAvatar>() != null || hit.GetComponentInParent<OracleEnemy>() != null ||
                    hit.GetComponentInParent<OracleProjectile>() != null) continue;
                return false;
            }
            return true;
        }

        private void SendState(ulong? recipient = null, string request = null)
        {
            if (!Networked || !rooms.IsHost || string.IsNullOrEmpty(context)) return;
            var views = new PeerView[peers.Count];
            int index = 0;
            foreach (Peer peer in peers.Values) views[index++] = new PeerView
                { id = peer.Id, downed = peer.Downed, tutorial = peer.Tutorial, remaining = peer.Remaining,
                    progress = peer.Progress, shield = peer.Shield, report = peer.DownSequence };
            string payload = JsonUtility.ToJson(new Message
                { kind = Prefix + "state.v1", context = context, request = request, sequence = ++stateSequence, arena = networkArena, run = networkArena ? session.Defense.CurrentRunId : null, peers = views });
            if (recipient.HasValue) rooms.SendReliableTo(recipient.Value, payload);
            else rooms.SendReliable(payload);
        }

        private void OnMessage(ulong sender, string payload)
        {
            if (!Networked || string.IsNullOrEmpty(payload) || payload.Length > 4096 ||
                payload.IndexOf(Prefix, StringComparison.Ordinal) < 0) return;
            EnsureNetworkMode();
            Message message;
            try { message = JsonUtility.FromJson<Message>(payload); }
            catch (ArgumentException) { return; }
            if (message == null || string.IsNullOrEmpty(message.kind) || message.arena != networkArena ||
                networkArena&&(session.Defense==null||message.run!=session.Defense.CurrentRunId)) return;
            if (rooms.IsHost)
            {
                if (!ConnectedId(sender) || payload.Length > 512) return;
                ReconcilePeers();
                if (!peers.TryGetValue(sender, out Peer peer)) return;
                if (message.kind == Prefix + "hello.v1")
                {
                    if (message.request == null || message.request.Length != 32 || clock - peer.LastSync < .5d) return;
                    peer.LastSync = clock;
                    SendState(sender, message.request);
                }
                else if (message.context == context)
                {
                    if (message.kind == Prefix + "down.v1"&&!IsNetworkArena) AcceptDown(sender, message.sequence, message.tutorial);
                    else if (message.kind == Prefix + "hold.v1") AcceptHold(sender, message.target, message.held, message.sequence);
                }
            }
            else if (sender == NetworkManager.ServerClientId && message.kind == Prefix + "state.v1") ApplyState(message);
        }

        private void ApplyState(Message state)
        {
            if (state.context == null || state.context.Length != 32 || state.sequence < 1 || state.peers == null ||
                state.peers.Length < 1 || state.peers.Length > RoomConnection.MaximumPlayers) return;
            bool handshake = state.request == syncRequest;
            if (!handshake && state.context != context)
            {
                if (!string.IsNullOrEmpty(context))
                { context = ""; syncRequest = Guid.NewGuid().ToString("N"); nextSync = 0d; appliedSequence = 0; }
                return;
            }
            if (state.context == context && state.sequence <= appliedSequence) return;
            for (int i = 0; i < state.peers.Length; i++)
            {
                PeerView view = state.peers[i];
                if (view == null || view.report < 0 || !Bounded(view.remaining, PartyDelay) || !Bounded(view.progress, HoldDuration) ||
                    !Bounded(view.shield, 2f)) return;
                bool member=false;var ids=rooms.ConnectedClientIds;
                for(int m=0;m<ids.Count&&m<RoomConnection.MaximumPlayers;m++)member|=ids[m]==view.id;
                if(!member)return;
                for(int j=0;j<i;j++)if(view.id==state.peers[j].id)return;
            }
            bool localPresent=false,hostPresent=false;
            foreach(var view in state.peers){localPresent|=view.id==rooms.LocalClientId;hostPresent|=view.id==NetworkManager.ServerClientId;}
            if(!localPresent||!hostPresent)return;
            context = state.context;
            appliedSequence = state.sequence;
            peers.Clear();
            for (int i = 0; i < state.peers.Length; i++)
            {
                PeerView view = state.peers[i];
                var peer = new Peer { Id = view.id, Downed = view.downed, Tutorial = view.tutorial,
                    Remaining = view.remaining, Progress = view.progress, Shield = view.shield, DownSequence = view.report };
                peers.Add(peer.Id, peer);
                if (peer.Id == rooms.LocalClientId) ApplyLocal(peer);
            }
            if (IsDowned) SendDownReport();
        }

        private void ApplyLocal(Peer peer)
        {
            if(IsNetworkArena)
            {
                if(combatHealth==null||rooms==null||!combatHealth.TryGetPeerHealth(rooms.LocalClientId,out var health)||
                    health.DownReport!=peer.DownSequence||health.Downed!=peer.Downed)return;
                if(health.Downed){ReviveRemainingSeconds=peer.Remaining;ReviveProgress=Mathf.Clamp01(peer.Progress/HoldDuration);}
                return; // Timers never write arena HP, shield, pose or down state.
            }
            if (IsDowned && peer.DownSequence < downSequence) return; // An old alive snapshot cannot cancel a fresh fall.
            if (peer.Downed)
            {
                if (!IsDowned)
                {
                    IsDowned = true;
                    session.HP = 0f;
                    session.Player.SetDownedPose(true);
                }
                localTutorial = peer.Tutorial;
                downSequence = Mathf.Max(downSequence, peer.DownSequence);
                ReviveRemainingSeconds = peer.Remaining;
                ReviveProgress = Mathf.Clamp01(peer.Progress / HoldDuration);
            }
            else if (IsDowned) ReviveLocal();
            shieldRemaining = peer.Shield;
        }

        private void ReviveLocal()
        {
            if(IsNetworkArena)return;
            IsDowned = localTutorial = false;
            ReviveRemainingSeconds = ReviveProgress = 0f;
            shieldRemaining = 2f;
            session.HP = Mathf.Max(1f, session.MaxHP);
            session.Player.SetDownedPose(false);
            session.Player.Teleport(session.World.PlayerSpawn);
            // Pause and browser pointer lock remain owned by visible user actions.
        }

        private static bool Bounded(float value, float maximum) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= maximum;
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        private void OnDestroy() { if (rooms != null) rooms.ReliableMessageReceived -= OnMessage; }
    }
}
