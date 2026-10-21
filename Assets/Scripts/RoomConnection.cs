using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main-thread Unity/transport boundary. Fixed8 admission/pose buffers own no
    // native memory; Netcode writers dispose per send. Cold party JSON remains
    // allocating; no whole-network zeroGC/performance claim. Source IDs are NGO IDs.
    // Real session + transport state. Cloud membership alone is never reported as connected.
    public sealed partial class RoomConnection : MonoBehaviour
    {
        public static RoomConnection Instance { get; private set; }
        public string Code => RoomShareCode020;
        public string Status { get; private set; } = "Create a private game or enter your friend's room code.";
        public bool Busy { get; private set; }
        public bool Connected { get; private set; }
        public bool IsHost => Connected && network != null && network.IsHost;
        public int PlayerCount => !Connected || network == null ? 0 : network.IsServer ? network.ConnectedClientsIds.Count : memberIds.Count;
        public bool CampaignActive { get; private set; }
        public bool LocalReady => Connected && partyMembers.TryGetValue(LocalClientId, out var member) && member.Ready;
        public int ReadyPlayerCount { get; private set; }
        public string PartyReadyStatus { get; private set; } = "Connect to a room to prepare your party.";
        public ulong ReliableSenderId { get; private set; }
        public event Action CampaignStarted;
        // Optional owner installed once after session initialization. The original
        // one-stage callback remains the fallback when no campaign is installed.
        public Action CampaignStartHandler { get; set; }
        public Func<bool> CampaignLifecycleActive { get; set; }
        public bool CampaignRosterSettled
        {
            get
            {
                if(!Connected||!IsHost||network==null||admissions.PendingCount!=0||
                    partyMembers.Count!=network.ConnectedClientsIds.Count)return false;
                foreach(ulong id in network.ConnectedClientsIds)
                    if(!partyMembers.TryGetValue(id,out var member)||!member.Registered)return false;
                return partyMembers.Count>0;
            }
        }
        public bool IsCampaignMemberRegistered(ulong id)
        {
            if(!Connected||!partyMembers.TryGetValue(id,out var member)||!member.Registered)return false;
            var ids=ConnectedClientIds;for(int i=0;i<ids.Count;i++)if(ids[i]==id)return true;
            return false;
        }
        public event Action<ulong, string> ReliableMessageReceived;
        // Host membership is transport-owned; guests expose only the validated full host roster.
        public IReadOnlyList<ulong> ConnectedClientIds => network == null ? Array.Empty<ulong>() :
            network.IsServer ? network.ConnectedClientsIds : memberIds;
        public bool TryGetAdmissionSlot(ulong clientId,out int slot) => admissions.TryGetSlot(clientId,out slot);
        public Func<ulong,Vector3,bool> ArenaPoseValidator { private get; set; }
        public bool ClearArenaPoseValidator(Func<ulong,Vector3,bool> expected)
        {
            if(ArenaPoseValidator!=expected)return false;
            ArenaPoseValidator=null;return true;
        }
        public bool AdmissionsBlocked => network != null && network.IsServer ? HostAdmissionsBlocked : remoteAdmissionsBlocked;
        bool HostAdmissionsBlocked => idleStopAdmissions || (requestedRoomMode020==CoopRoomMode020.TowerDefense&&GameSession.Instance != null && GameSession.Instance.Defense != null && GameSession.Instance.Defense.RoomAdmissionsBlocked);
        public ulong LocalClientId => network == null ? NetworkManager.ServerClientId : network.LocalClientId;

        uint LocalAppearanceCode()
        {
            int slot=0;admissions.TryGetSlot(LocalClientId,out slot);
            return OracleAvatarLocalAppearance.Resolve(slot).Packed;
        }

        public const int MaximumPlayers = CoopPartyCapacity.MaximumPlayers;
        public const int PoseSnapshotCapacityBytes = CoopPartyCapacity.MaximumPoseSnapshotBytes;
        public const int MaximumReliablePayloadBytes = 64 * 1024;
        const string UploadChannel = "odyssey.hub.pose.v7";
        const string SnapshotChannel = "odyssey.hub.snapshot.v7";
        const string CampaignChannel = "odyssey.campaign.start.v7";
        const string ReliableChannel = "odyssey.coop.reliable.v7";
        const string PartyPrefix = "oracle.party.v7.";
        const int MaximumPartyPayloadBytes = 2048;
        const float SendInterval = .1f;
        const double ConnectionDeadlineSeconds = 45;

        struct Pose
        {
            public Vector3 Position;
            public float Yaw;
            public Vector3 Aim;
            public uint Sequence,Appearance;
        }

        struct TrafficBudget { public double Window; public int Messages; public int Bytes; }

        sealed class PartyMember
        {
            public int Slot;
            public string Request;
            public uint Sequence;
            public bool Registered, Ready;
        }

        [Serializable]
        sealed class PartyMemberState
        {
            public ulong id;
            public int slot;
            public bool registered, ready;
        }

        [Serializable]
        sealed class PartyMessage
        {
            public string kind, nonce, request, capabilities;
            public bool admissionsBlocked;
            public uint sequence;
            public bool ready, trained;
            public PartyMemberState[] members;
        }

        readonly Dictionary<ulong, Pose> poses = new Dictionary<ulong, Pose>();
        readonly Dictionary<ulong, RemoteOracleAvatar> avatars = new Dictionary<ulong, RemoteOracleAvatar>();
        readonly Dictionary<ulong, double> lastUpload = new Dictionary<ulong, double>();
        readonly Dictionary<ulong, TrafficBudget> reliableBudgets = new Dictionary<ulong, TrafficBudget>();
        readonly Dictionary<ulong, TrafficBudget> partyBudgets = new Dictionary<ulong, TrafficBudget>();
        readonly Dictionary<ulong, PartyMember> partyMembers = new Dictionary<ulong, PartyMember>();
        readonly List<ulong> removals = new List<ulong>(MaximumPlayers);
        readonly CoopAdmissionRoster admissions = new CoopAdmissionRoster();
        readonly List<ulong> memberIds = new List<ulong>(MaximumPlayers);
        readonly CoopMemberSlot[] memberBuffer = new CoopMemberSlot[MaximumPlayers];
        readonly ulong[] poseIds = new ulong[MaximumPlayers];
        readonly Pose[] poseBuffer = new Pose[MaximumPlayers];
        bool approvalInstalled, priorApproval, remoteAdmissionsBlocked, lastHostBlocked;
        byte[] priorConnectionData;
        string connectionRejection = string.Empty, pendingCampaignNonce = string.Empty, startedCampaignNonce = string.Empty;
        uint campaignStartSequence, pendingCampaignSequence, appliedCampaignSequence;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        ISession session;
        NetworkManager network;
        Task<string> disconnectTask;
        RoomConnectionAttempt connectionAttempt;
        bool ownsNetwork;
        bool handlersRegistered;
        bool destroyed;
        bool intentionalStop;
        bool campaignPending;
        bool leaving;
        int pendingConnections;
        int operation;
        int playerCount;
        float nextSend;
        uint sequence;
        string partyNonce = string.Empty, partyRequest = string.Empty;
        uint partyStateSequence, partyAppliedSequence, partyRequestSequence;
        double nextPartyHello, nextPartyAction;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            PrepareRemotePlayerCold015();
        }

        public void CreateRoom() => CreateRoomCore(CoopRoomMode020.TowerDefense,false);
        public void CreateSoloRoom() => CreateRoomCore(CoopRoomMode020.TowerDefense,true);
        public void CreateRoom(CoopRoomMode020 mode) => CreateRoomCore(mode,false);

        async void CreateRoomCore(CoopRoomMode020 mode,bool solo)
        {
            if(!BeginTypedRequest020(mode,true))return;
            Busy = true;
            pendingConnections++;
            int ticket = ++operation;
            var attempt = BeginConnectionAttempt(ticket);
            try
            {
                Status = "Connecting to game services...";
                await PrepareAsync(ticket, true, attempt);
                EnsureConnectionAttemptCurrent(ticket, attempt);
                roomPhase020=RoomConnectionPhase020.Hosting;
                Status = solo ? "Starting a one-player tower-defense session..." : "Creating a private game for up to " + MaximumPlayers + " players...";
                var options = new SessionOptions
                {
                    Name = mode==CoopRoomMode020.CampaignTown?"Oracle shared Campaign town":solo ? "Oracle solo tower defense" : "Oracle private co-op",
                    MaxPlayers = solo ? 1 : CoopRoomModeRules020.SupportedMaximum(mode),
                    IsPrivate = true,
                    Type = CoopRoomModeRules020.ServiceKey(mode)
                }.WithRelayNetwork().WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.WSS });
                AddTypedSessionProperties020(options);
                attempt.Phase = "creating the private room and secure game connection";
                var created = await MultiplayerService.Instance.CreateSessionAsync(options, attempt.Token);
                // Retain even a late returned session until our owned cleanup has left it.
                session = created;
                session.RemovedFromSession += OnRemovedFromSession;
                EnsureConnectionAttemptCurrent(ticket, attempt);
                await FinishConnectionAsync(ticket, attempt);
            }
            catch (Exception error)
            {
                if (IsCurrent(ticket))
                {await FailConnectionAsync(attempt, attempt.TimedOut ? new TimeoutException(attempt.TimeoutReason, error) : error);if(IsCurrent(ticket))roomPhase020=RoomConnectionPhase020.Failed;}
                else await DisconnectAsync();
            }
            finally { EndConnectionAttempt(attempt); }
        }

        public void JoinRoom(string roomCode)=>JoinRoom(roomCode,CoopRoomMode020.TowerDefense);
        public async void JoinRoom(string roomCode,CoopRoomMode020 expectedMode)
        {
            if (Busy || Connected) return;
            string cleaned = (roomCode ?? string.Empty).Trim().ToUpperInvariant();
            if (cleaned.Length < 4 || cleaned.Length > 16 || !IsCode(cleaned))
            {
                Status = "Enter the room code shown by the host.";
                return;
            }
            if(!BeginTypedRequest020(expectedMode,false))return;
            Busy = true;
            pendingConnections++;
            int ticket = ++operation;
            var attempt = BeginConnectionAttempt(ticket);
            try
            {
                Status = "Connecting to game services...";
                await PrepareAsync(ticket, false, attempt);
                EnsureConnectionAttemptCurrent(ticket, attempt);
                roomPhase020=RoomConnectionPhase020.Joining;
                Status = "Joining your friend's room...";
                attempt.Phase = "joining the room and secure game connection";
                var joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(cleaned,
                    new JoinSessionOptions { Type = CoopRoomModeRules020.ServiceKey(expectedMode) }
                        .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.WSS }), attempt.Token);
                // Retain even a late returned session until our owned cleanup has left it.
                session = joined;
                session.RemovedFromSession += OnRemovedFromSession;
                EnsureConnectionAttemptCurrent(ticket, attempt);
                await FinishConnectionAsync(ticket, attempt);
            }
            catch (Exception error)
            {
                if (IsCurrent(ticket))
                {await FailConnectionAsync(attempt, attempt.TimedOut ? new TimeoutException(attempt.TimeoutReason, error) : error);if(IsCurrent(ticket))roomPhase020=RoomConnectionPhase020.Failed;}
                else await DisconnectAsync();
            }
            finally { EndConnectionAttempt(attempt); }
        }

        public async void LeaveRoom()
        {
            if(roomPhase020!=RoomConnectionPhase020.Canceled)roomPhase020=RoomConnectionPhase020.Leaving;
            await LeaveRoomAsync();
        }

        async Task PrepareAsync(int ticket, bool hosting, RoomConnectionAttempt attempt)
        {
            EnsureConnectionAttemptCurrent(ticket, attempt);
            // A prior loss or failed attempt must finish dismantling its owned transport
            // before another session can reuse NetworkManager.Singleton.
            if (network != null || session != null || pendingIdleHostDeletion != null || disconnectTask != null && !disconnectTask.IsCompleted)
            {
                attempt.Phase = "closing the previous game connection";
                Status = "Closing the previous game connection...";
                await DisconnectAsync();
                EnsureConnectionAttemptCurrent(ticket, attempt);
                if (pendingIdleHostDeletion != null) throw new InvalidOperationException("The previous inactive room cleanup must succeed before another connection starts.");
            }
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // Separate anonymous guests also work in two tabs on the same browser origin.
                var options = new InitializationOptions().SetProfile("oracle-" + Guid.NewGuid().ToString("N").Substring(0, 16));
                attempt.Phase = "initializing game services";
                Status = "Initializing game services...";
                await UnityServices.InitializeAsync(options);
            }
            EnsureConnectionAttemptCurrent(ticket, attempt);
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                attempt.Phase = "signing in to game services";
                Status = "Signing in to game services...";
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            EnsureConnectionAttemptCurrent(ticket, attempt);
            CreateNetworkManager(hosting);
        }

        RoomConnectionAttempt BeginConnectionAttempt(int ticket)
        {
            if (connectionAttempt != null) throw new InvalidOperationException("The previous connection attempt is still settling.");
            connectionAttempt = new RoomConnectionAttempt(ticket, Time.realtimeSinceStartupAsDouble,
                ConnectionDeadlineSeconds, Application.exitCancellationToken);
            return connectionAttempt;
        }

        void EndConnectionAttempt(RoomConnectionAttempt attempt)
        {
            // Release the owner before signaling drain waiters, which may resume inline.
            if (ReferenceEquals(connectionAttempt, attempt)) connectionAttempt = null;
            pendingConnections--;
            attempt.Dispose();
            if (!destroyed) Busy = leaving || pendingConnections > 0;
        }

        void EnsureConnectionAttemptCurrent(int ticket, RoomConnectionAttempt attempt)
        {
            EnsureCurrent(ticket);
            attempt.Expire(Time.realtimeSinceStartupAsDouble);
            attempt.ThrowIfCancellationRequested();
        }

        void UpdateConnectionAttemptDeadline()
        {
            var attempt = connectionAttempt;
            if (attempt == null || !attempt.Expire(Time.realtimeSinceStartupAsDouble)) return;
            // Cancellation may synchronously finish and release this attempt.
            if (!destroyed && ReferenceEquals(connectionAttempt, attempt) && IsCurrent(attempt.Ticket))
                Status = "Could not connect: " + attempt.TimeoutReason +
                    " Waiting for service cleanup before retry. If this does not finish, reload this game tab.";
        }

        async Task CancelAndDrainConnectionAttemptAsync()
        {
            var attempt = connectionAttempt;
            if (attempt == null) return;
            attempt.Cancel();
            if (!destroyed) Status = "Canceling the connection; waiting for service cleanup. If this does not finish, reload this game tab.";
            // Init/auth and session leave have no cancellation API in the installed SDK.
            // Never release the retry/manager lease while those operations can return late.
            await attempt.Settled;
        }

        void CreateNetworkManager(bool hosting)
        {
            intentionalStop = false;
            if (network != null) throw new InvalidOperationException("The previous game connection is still closing.");
            // Validate a foreign singleton before recording it as our cleanup
            // lease. Failure must not shut down another owner's live transport.
            var available = NetworkManager.Singleton;
            if (available != null && (available.IsListening || available.ShutdownInProgress))
                throw new InvalidOperationException("Another network connection is already running or closing.");
            if (available != null && available.ConnectionApprovalCallback != null)
                throw new InvalidOperationException("This game connection already has an approval owner.");
            if (available != null && available.GetComponent<UnityTransport>() == null)
                throw new InvalidOperationException("Unity Transport is required for this room connection.");
            network = available;
            if (network == null)
            {
                var root = new GameObject("Oracle co-op transport");
                root.SetActive(false);
                var transport = root.AddComponent<UnityTransport>();
                transport.UseWebSockets = true;
                network = root.AddComponent<NetworkManager>();
                network.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport,
                    EnableSceneManagement = false,
                    ConnectionApproval = false,
                    TickRate = 20
                };
                ownsNetwork = true;
                root.SetActive(true);
            }
            else
            {
                var transport = network.GetComponent<UnityTransport>();
                if (transport == null) throw new InvalidOperationException("Unity Transport is required for this room connection.");
                transport.UseWebSockets = true;
                network.NetworkConfig.NetworkTransport = transport;
                network.NetworkConfig.EnableSceneManagement = false;
            }
            if(network.ConnectionApprovalCallback != null) throw new InvalidOperationException("This game connection already has an approval owner.");
            priorApproval=network.NetworkConfig.ConnectionApproval;
            priorConnectionData=network.NetworkConfig.ConnectionData;
            admissions.Clear();memberIds.Clear();connectionRejection=string.Empty;
            ResetHubSpawns();
            if(hosting&&!admissions.ReserveHost())throw new InvalidOperationException("Could not reserve the host's room slot.");
            network.NetworkConfig.ConnectionApproval=true;
            network.NetworkConfig.ConnectionData=Utf8.GetBytes(CoopRoomModeRules020.Protocol(requestedRoomMode020));
            network.ConnectionApprovalCallback=ApproveConnection;approvalInstalled=true;
            network.OnClientConnectedCallback += OnClientConnected;
            network.OnClientDisconnectCallback += OnClientDisconnected;
        }

        async Task FinishConnectionAsync(int ticket, RoomConnectionAttempt attempt)
        {
            Status = "Opening the secure game connection...";
            attempt.Phase = "opening the secure game connection";
            while (network != null && !network.IsConnectedClient)
            {
                EnsureConnectionAttemptCurrent(ticket, attempt);
                if(!string.IsNullOrEmpty(connectionRejection))throw new InvalidOperationException(connectionRejection);
                attempt.Expire(Time.realtimeSinceStartupAsDouble);
                attempt.ThrowIfCancellationRequested();
                // Managed Timer/Task.Delay is not relied on for browser timeouts.
                await Awaitable.NextFrameAsync();
            }
            EnsureConnectionAttemptCurrent(ticket, attempt);
            if (network == null || !network.IsListening || !network.IsConnectedClient)
                throw new InvalidOperationException("The game transport disconnected before the room became ready.");
            RequireTypedService020();roomPhase020=RoomConnectionPhase020.Negotiating;
            RegisterHandlers();
            if(requestedRoomMode020==CoopRoomMode020.CampaignTown)
            {await FinishCampaignTownRoom020(ticket,attempt);EnsureConnectionAttemptCurrent(ticket,attempt);return;}
            await PrepareHubSpawnAsync(ticket, attempt);
            EnsureConnectionAttemptCurrent(ticket, attempt);
            Connected = true;
            playerCount = network.IsServer ? network.ConnectedClientsIds.Count : 1;
            nextSend = 0f;
            InitializeParty();
            InitializeRoomInactivity();
            Status = network.IsHost ? "Room connected. Share the code, then ready at the portal." : "Room connected. Complete your lessons, then ready at the portal.";
            var game = GameSession.Instance;
            if (game == null) throw new InvalidOperationException("GameSession is not available.");
            if(!CompleteSoloRestartConnection(game))game.BeginCoop();
            if (!hubApplied) throw new InvalidOperationException("The accepted refuge spawn was not applied before arrival.");
            TryApplyCampaignStart();PublishDefenseRoom020(ticket);
        }

        void Update()
        {
            UpdateConnectionAttemptDeadline();
            if (network != null && network.IsListening) RegisterHandlers();
            if (!Connected || network == null) return;
            if (!network.IsConnectedClient)
            {
                ConnectionLost("Game connection lost. Leave and rejoin using the host's code.");
                return;
            }
            if(network.IsServer)admissions.Expire(Time.realtimeSinceStartupAsDouble);
            if (!hubApplied) return;
            if(requestedRoomMode020==CoopRoomMode020.CampaignTown)
            {if(!RoomOnline020){ConnectionLost("The accepted Campaign town lease is no longer current.");return;}TickCampaignTownRoomTransport020();return;}
            TickRoomReactionTransport();
            TickHubSpawnDeadlines();
            TickAdmissionPhase();
            TickParty();
            if (Time.unscaledTime < nextSend) return;
            nextSend = Time.unscaledTime + SendInterval;
            var game = GameSession.Instance;
            if (game == null || game.Player == null) return;
            var pose = new Pose
            {
                Position = game.Player.transform.position, Yaw = game.Player.transform.eulerAngles.y,
                Aim = game.Player.ViewCamera != null ? game.Player.ViewCamera.transform.forward : game.Player.transform.forward,
                Sequence = ++sequence, Appearance = LocalAppearanceCode()
            };
            if (!ValidPose(pose)) return;
            if (network.IsServer)
            {
                if(ValidateArenaPose(network.LocalClientId,pose.Position))poses[network.LocalClientId] = pose;
                playerCount = network.ConnectedClientsIds.Count;
                SendSnapshot();
            }
            else
            {
                using (var writer = new FastBufferWriter(CoopPartyCapacity.PoseBytes, Allocator.Temp))
                {
                    WritePose(writer, pose);
                    network.CustomMessagingManager.SendNamedMessage(UploadChannel, NetworkManager.ServerClientId, writer, NetworkDelivery.UnreliableSequenced);
                }
            }
        }

        void RegisterHandlers()
        {
            if (handlersRegistered || network == null || network.CustomMessagingManager == null) return;
            if(requestedRoomMode020==CoopRoomMode020.CampaignTown){RegisterCampaignTownRoomHandlers020();handlersRegistered=true;return;}
            var messages = network.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(UploadChannel, ReceiveUpload);
            messages.RegisterNamedMessageHandler(SnapshotChannel, ReceiveSnapshot);
            messages.RegisterNamedMessageHandler(HubSpawnChannel, ReceiveHubSpawn);
            messages.RegisterNamedMessageHandler(CampaignChannel, ReceiveCampaign);
            messages.RegisterNamedMessageHandler(ReliableChannel, ReceiveReliable);
            messages.RegisterNamedMessageHandler(IdleChannel, ReceiveIdleFrame);
            messages.RegisterNamedMessageHandler(SharedLabChannel, ReceiveSharedLab);
            RegisterRoomReactionHandler();
            RegisterArenaImpactHandler();
            RegisterXpHandler015();
            RegisterLaneBuildHandler015();
            handlersRegistered = true;
        }

        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject=false;response.Pending=false;
            if(intentionalStop||leaving||destroyed)
            {response.Approved=false;response.Reason="This room is closing. Ask the host for a new code.";return;}
            bool local=request.ClientNetworkId==NetworkManager.ServerClientId;
            bool compatible=CoopRoomModeRules020.Valid(requestedRoomMode020)&&(local||CoopRoomModeRules020.MatchesPayload(requestedRoomMode020,request.Payload));
            bool open=local||!HostAdmissionsBlocked;
            bool reserved=compatible&&open&&(local?admissions.ReserveHost():admissions.Reserve(request.ClientNetworkId,Time.realtimeSinceStartupAsDouble,ConnectionDeadlineSeconds,out _));
            response.Approved=reserved;
            response.Reason=reserved?string.Empty:!compatible?"This room requires the same game and multiplayer version.":
                !open?"A defense is in progress. Rejoin when the party returns to the refuge.":"This room already has "+MaximumPlayers+" connected or joining players.";
        }

        void OnClientConnected(ulong clientId)
        {
            RegisterHandlers();
            if(network==null||!network.IsServer)return;
            admissions.Expire(Time.realtimeSinceStartupAsDouble);
            if(clientId!=network.LocalClientId&&HostAdmissionsBlocked)
            {admissions.Remove(clientId);network.DisconnectClient(clientId,"A defense is in progress. Rejoin at the refuge.");return;}
            if(!admissions.Confirm(clientId)){network.DisconnectClient(clientId,"Room admission expired. Rejoin using the current code.");return;}
            if(requestedRoomMode020==CoopRoomMode020.CampaignTown){CampaignTownRoomPeerChanged020(clientId,true);return;}
            BeginHubSpawnDeadline(clientId);
        }

        void OnClientDisconnected(ulong clientId)
        {
            if(requestedRoomMode020==CoopRoomMode020.CampaignTown)
            {
                admissions.Remove(clientId);poses.Remove(clientId);RemoveAvatar(clientId);
                try{CampaignTownRoomPeerChanged020(clientId,false);}
                finally{if(!intentionalStop&&network!=null&&clientId==network.LocalClientId)ConnectionLost("The shared Campaign town connection closed.");}
                return;
            }
            ReleaseHubSpawn(clientId);
            poses.Remove(clientId);
            if(network!=null&&network.IsServer)
            {
                if(!intentionalStop&&Connected&&clientId!=network.LocalClientId&&partyMembers.ContainsKey(clientId))UnexpectedSoloRestartPeerDeparture?.Invoke(clientId);
                admissions.Remove(clientId);
                if(!intentionalStop&&Connected&&partyMembers.ContainsKey(clientId))InvalidatePartyReadiness();
            }
            // ReconcileParty must observe the departure before it clears Ready.
            // Guest membership changes only through an authenticated host roster.
            if(!intentionalStop&&network!=null&&clientId==network.LocalClientId&&!Connected)
                connectionRejection=string.IsNullOrEmpty(network.DisconnectReason)?"The game connection closed before admission completed.":network.DisconnectReason;
            lastUpload.Remove(clientId);
            reliableBudgets.Remove(clientId);
            RemoveAvatar(clientId);
            if (!intentionalStop && Connected && network != null && clientId == network.LocalClientId)
                ConnectionLost("The host or game connection closed. Leave and create or join another room.");
        }

        void ReceiveUpload(ulong sender, FastBufferReader reader)
        {
            if (network == null || !network.IsServer || !IsConnectedPeer(sender) || sender == network.LocalClientId) return;
            if (!reader.TryBeginRead(CoopPartyCapacity.PoseBytes)) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (lastUpload.TryGetValue(sender, out var last) && now - last < .04) return;
            try
            {
                Pose pose = ReadPose(ref reader);
                if (!ValidPose(pose)||!admissions.TryGetSlot(sender,out _)||!ValidateArenaPose(sender,pose.Position)) return;
                if (!hubSpawnSlots.AcceptPose(sender,pose.Position.x,pose.Position.y,pose.Position.z)) return;
                if (poses.TryGetValue(sender, out var previous) && !Newer(pose.Sequence, previous.Sequence)) return;
                lastUpload[sender] = now;
                poses[sender] = pose;
                ApplyAvatar(sender, pose);
            }
            catch (OverflowException) { /* Discard malformed or truncated packets. */ }
        }

        void SendSnapshot()
        {
            removals.Clear();
            foreach(var item in poses)if(!IsConnectedPeer(item.Key))removals.Add(item.Key);
            foreach(ulong id in removals){poses.Remove(id);RemoveAvatar(id);}
            int members=network.ConnectedClientsIds.Count;
            if(members<1||members>MaximumPlayers||poses.Count>members)return;
            using(var writer=new FastBufferWriter(PoseSnapshotCapacityBytes,Allocator.Temp,PoseSnapshotCapacityBytes))
            {
                writer.WriteValueSafe((byte)members);writer.WriteValueSafe(HostAdmissionsBlocked);writer.WriteValueSafe((byte)poses.Count);
                foreach(var item in poses){writer.WriteValueSafe(item.Key);WritePose(writer,item.Value);}
                Broadcast(SnapshotChannel,writer,NetworkDelivery.UnreliableSequenced);
            }
        }

        void ReceiveSnapshot(ulong sender,FastBufferReader reader)
        {
            if(network==null||network.IsServer||!hubApplied||sender!=NetworkManager.ServerClientId||!reader.TryBeginRead(3))return;
            try
            {
                reader.ReadValueSafe(out byte count);reader.ReadValueSafe(out bool blocked);reader.ReadValueSafe(out byte poseCount);
                if(count!=memberIds.Count||!CoopPartyCapacity.ValidPoseHeader(count,poseCount,reader.Length-reader.Position)||!reader.TryBeginRead(poseCount*CoopPartyCapacity.PoseEntryBytes))return;
                for(int i=0;i<poseCount;i++)
                {
                    reader.ReadValueSafe(out poseIds[i]);poseBuffer[i]=ReadPose(ref reader);
                    if(!admissions.TryGetSlot(poseIds[i],out _)||!ValidPose(poseBuffer[i]))return;
                    for(int j=0;j<i;j++)if(poseIds[i]==poseIds[j])return;
                }
                // Validate the whole bounded frame before changing any pose/avatar.
                removals.Clear();foreach(var avatar in avatars)removals.Add(avatar.Key);
                for(int i=0;i<poseCount;i++)
                {
                    ulong id=poseIds[i];Pose pose=poseBuffer[i];removals.Remove(id);
                    if(id==network.LocalClientId)continue;
                    if(poses.TryGetValue(id,out var old)&&!Newer(pose.Sequence,old.Sequence))continue;
                    poses[id]=pose;ApplyAvatar(id,pose);
                }
                foreach(ulong id in removals){RemoveAvatar(id);poses.Remove(id);}
                remoteAdmissionsBlocked=blocked;
                // A phase bit is never permission to replay a past campaign on a refuge join.
            }
            catch(OverflowException){ }
        }

        public void StartCampaign()
        {
            if(requestedRoomMode020!=CoopRoomMode020.TowerDefense)return;
            if(!Connected||!IsHost){Status="Only the connected host can start the campaign.";return;}
            if(HostAdmissionsBlocked || CampaignLifecycleActive != null && CampaignLifecycleActive())return;
            ReconcileParty();
            if(!AllPartyMembersReady()||partyNonce==startedCampaignNonce){RefreshPartyStatus();Status=PartyReadyStatus;return;}
            uint serial=++campaignStartSequence;
            using(var writer=new FastBufferWriter(36,Allocator.Temp))
            {writer.WriteValueSafe(serial);writer.WriteBytesSafe(Utf8.GetBytes(partyNonce));Broadcast(CampaignChannel,writer,NetworkDelivery.ReliableSequenced);}
            pendingCampaignNonce=partyNonce;pendingCampaignSequence=serial;campaignPending=true;TryApplyCampaignStart();
        }

        void ReceiveCampaign(ulong sender,FastBufferReader reader)
        {
            if(requestedRoomMode020!=CoopRoomMode020.TowerDefense)return;
            if(network==null||network.IsServer||sender!=NetworkManager.ServerClientId||!reader.TryBeginRead(36))return;
            try
            {
                reader.ReadValueSafe(out uint serial);var bytes=new byte[32];reader.ReadBytesSafe(ref bytes,32);
                string nonce=Utf8.GetString(bytes);
                if(!ValidPartyToken(nonce)||!Newer(serial,appliedCampaignSequence)||campaignPending&&!Newer(serial,pendingCampaignSequence))return;
                pendingCampaignNonce=nonce;pendingCampaignSequence=serial;campaignPending=true;TryApplyCampaignStart();
            }
            catch(OverflowException){ }catch(DecoderFallbackException){ }
        }

        void TryApplyCampaignStart()
        {
            if(requestedRoomMode020!=CoopRoomMode020.TowerDefense)return;
            if(!Connected||!campaignPending||pendingCampaignNonce!=partyNonce||!ValidPartyToken(partyNonce)||
                !partyMembers.TryGetValue(LocalClientId,out var local)||!local.Registered||pendingCampaignNonce==startedCampaignNonce)return;
            appliedCampaignSequence=pendingCampaignSequence;startedCampaignNonce=pendingCampaignNonce;
            CampaignActive=true;campaignPending=false;
            Status="Campaign started.";PartyReadyStatus="Party ready. Campaign started.";
            if(CampaignStartHandler != null) CampaignStartHandler(); else CampaignStarted?.Invoke();
        }

        void TickAdmissionPhase()
        {
            if(!IsHost)return;
            bool blocked=HostAdmissionsBlocked;
            if(blocked==lastHostBlocked)return;
            lastHostBlocked=blocked;
            if(!blocked)ResetPartyReadiness();else SendPartyState();
        }

        bool ValidateArenaPose(ulong id,Vector3 position)
        {
            var game=GameSession.Instance;
            return game==null||game.World==null||!game.World.IsCoopArena||ArenaPoseValidator==null||ArenaPoseValidator(id,position);
        }

        // Trusted Director callback after host/run/sequence validation and Player.Teleport.
        // This updates transport cache only; it is not a client teleport request.
        public void ApplyLocalArenaPoseCorrection(Vector3 position)
        {
            var game=GameSession.Instance;
            if(!Connected||network==null||game==null||game.Player==null)return;
            var player=game.Player;
            Vector3 aim=player.ViewCamera!=null?player.ViewCamera.transform.forward:player.transform.forward;
            var pose=new Pose{Position=position,Yaw=player.transform.eulerAngles.y,Aim=aim,Sequence=++sequence,Appearance=LocalAppearanceCode()};
            if(!ValidPose(pose))return;
            poses[network.LocalClientId]=pose;nextSend=0;
        }

        // The host sends state to peers; clients send requests to the host. A client request is
        // never automatically rebroadcast as authoritative state. Root validates and applies it.
        public void SendReliable(string payload)
        {
            if (!Connected || network == null || string.IsNullOrEmpty(payload)) return;
            int count = Utf8.GetByteCount(payload);
            if (count > MaximumReliablePayloadBytes) throw new ArgumentException("Co-op payload exceeds the64KiB UTF-8 limit.", nameof(payload));
            byte[] bytes = Utf8.GetBytes(payload);
            using (var writer = new FastBufferWriter(count + 4, Allocator.Temp, MaximumReliablePayloadBytes + 4))
            {
                writer.WriteValueSafe(count);
                writer.WriteBytesSafe(bytes);
                if (network.IsServer) Broadcast(ReliableChannel, writer, NetworkDelivery.ReliableFragmentedSequenced);
                else network.CustomMessagingManager.SendNamedMessage(ReliableChannel, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        public void SendReliableTo(ulong clientId, string payload)
        {
            if (!Connected || !IsHost || network == null || string.IsNullOrEmpty(payload)) return;
            if (!IsConnectedPeer(clientId)) return;
            int count = Utf8.GetByteCount(payload);
            if (count > MaximumReliablePayloadBytes) throw new ArgumentException("Co-op payload exceeds the64KiB UTF-8 limit.", nameof(payload));
            if (clientId == network.LocalClientId)
            {
                ReliableSenderId = NetworkManager.ServerClientId;
                if (!ReceivePartyMessage(NetworkManager.ServerClientId, payload)) ReliableMessageReceived?.Invoke(NetworkManager.ServerClientId, payload);
                return;
            }
            byte[] bytes = Utf8.GetBytes(payload);
            using (var writer = new FastBufferWriter(count + 4, Allocator.Temp, MaximumReliablePayloadBytes + 4))
            {
                writer.WriteValueSafe(count);
                writer.WriteBytesSafe(bytes);
                network.CustomMessagingManager.SendNamedMessage(ReliableChannel, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        void ReceiveReliable(ulong sender, FastBufferReader reader)
        {
            if (network == null || !network.IsListening) return;
            if (network.IsServer ? !IsConnectedPeer(sender) || sender == network.LocalClientId : sender != NetworkManager.ServerClientId) return;
            if (!reader.TryBeginRead(4)) return;
            try
            {
                reader.ReadValueSafe(out int count);
                if (count < 1 || count > MaximumReliablePayloadBytes || !reader.TryBeginRead(count)) return;
                if (network.IsServer && !AdmitReliableRequest(sender, count)) return;
                byte[] bytes = new byte[count];
                reader.ReadBytesSafe(ref bytes, count);
                string message = Utf8.GetString(bytes);
                ReliableSenderId = sender;
                if (!ReceivePartyMessage(sender, message)) ReliableMessageReceived?.Invoke(sender, message);
            }
            catch (OverflowException) { }
            catch (DecoderFallbackException) { }
        }

        bool AdmitReliableRequest(ulong sender, int bytes)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            reliableBudgets.TryGetValue(sender, out var budget);
            if (now - budget.Window >= 1d) budget = new TrafficBudget { Window = now };
            budget.Messages++;
            budget.Bytes += bytes;
            reliableBudgets[sender] = budget;
            return budget.Messages <= 40 && budget.Bytes <= 256 * 1024;
        }

        void InitializeParty()
        {
            partyMembers.Clear();memberIds.Clear();partyBudgets.Clear();
            partyNonce=string.Empty;partyAppliedSequence=partyRequestSequence=partyStateSequence=0;
            partyRequest = Guid.NewGuid().ToString("N");
            if (IsHost) ReconcileParty();
            else PartyReadyStatus = "Registering with the host's party...";
        }

        void ResetParty()
        {
            ResetRoomInactivity();
            partyMembers.Clear();memberIds.Clear();admissions.Clear();
            partyBudgets.Clear();
            partyNonce = partyRequest = string.Empty;
            partyStateSequence = partyAppliedSequence = partyRequestSequence = 0;
            nextPartyHello = nextPartyAction = 0d;
            ReadyPlayerCount = 0;remoteAdmissionsBlocked=lastHostBlocked=false;
            pendingCampaignNonce=startedCampaignNonce=string.Empty;campaignStartSequence=pendingCampaignSequence=appliedCampaignSequence=0;
            PartyReadyStatus = "Connect to a room to prepare your party.";
            ResetHubSpawns();
        }

        void TickParty()
        {
            if (IsHost) { ReconcileParty(); return; }
            if (Time.realtimeSinceStartupAsDouble < nextPartyHello ||
                partyMembers.TryGetValue(LocalClientId, out var member) && member.Registered && !string.IsNullOrEmpty(partyNonce)) return;
            nextPartyHello = Time.realtimeSinceStartupAsDouble + 1d;
            SendReliable(JsonUtility.ToJson(new PartyMessage { kind = PartyPrefix + "hello.v1", request = partyRequest }));
        }

        // Readiness records portal intent, not an authentication or tutorial-proof claim.
        public void SetCoopReady(bool ready)
        {
            if(requestedRoomMode020!=CoopRoomMode020.TowerDefense){PartyReadyStatus="Town arrival readiness is separate from tower defense.";return;}
            if (!Connected || network == null) { PartyReadyStatus = "Connect to a room before preparing your party."; return; }
            if (AdmissionsBlocked) { PartyReadyStatus = "The defense is active. Ready again at the refuge."; return; }
            var game = GameSession.Instance;
            bool trained = game != null && game.Tutorial != null && game.Tutorial.Finished;
            if (ready && !trained) { PartyReadyStatus = "Complete the tutorial before readying at the portal."; return; }
            double now = Time.realtimeSinceStartupAsDouble;
            if (ready && now < nextPartyAction) return;
            nextPartyAction = now + .15d;
            if (IsHost)
            {
                ReconcileParty();
                AcceptPartyReady(LocalClientId, ready, trained, ++partyRequestSequence);
                return;
            }
            if (string.IsNullOrEmpty(partyNonce) || !partyMembers.TryGetValue(LocalClientId, out var local) || !local.Registered)
            {
                PartyReadyStatus = "Waiting for the host to register your party. Ready again in a moment.";
                nextPartyHello = 0d;
                return;
            }
            SendReliable(JsonUtility.ToJson(new PartyMessage
            {
                kind = PartyPrefix + "ready.v1", nonce = partyNonce, request = partyRequest,
                sequence = ++partyRequestSequence, ready = ready, trained = trained
            }));
            PartyReadyStatus = ready ? "Waiting for the host to confirm readiness..." : "Waiting for the host to cancel readiness...";
        }

        void ReconcileParty()
        {
            if (!IsHost || network == null) return;
            bool changed = false;
            removals.Clear();
            foreach (ulong id in partyMembers.Keys) if (!IsConnectedPeer(id)) removals.Add(id);
            foreach (ulong id in removals) { partyMembers.Remove(id); partyBudgets.Remove(id); changed = true; }
            foreach (ulong id in network.ConnectedClientsIds)
            {
                if (partyMembers.ContainsKey(id) || partyMembers.Count >= MaximumPlayers || !admissions.TryGetSlot(id,out int slot)) continue;
                partyMembers.Add(id, new PartyMember
                {
                    Slot=slot, Registered = id == LocalClientId,
                    Request = id == LocalClientId ? partyRequest : string.Empty
                });
                changed = true;
            }
            playerCount = network.ConnectedClientsIds.Count;
            if (changed) ResetPartyReadiness();
            else RefreshPartyStatus();
        }

        void ResetPartyReadiness()
        {
            InvalidatePartyReadiness();
            SendPartyState();
        }

        void InvalidatePartyReadiness()
        {
            partyNonce=Guid.NewGuid().ToString("N");
            foreach(var member in partyMembers.Values)member.Ready=false;
            RefreshPartyStatus();
        }

        bool AllPartyMembersReady()
        {
            if (!IsHost || network == null || admissions.PendingCount > 0 || network.ConnectedClientsIds.Count < 1 ||
                network.ConnectedClientsIds.Count > MaximumPlayers || partyMembers.Count != network.ConnectedClientsIds.Count) return false;
            foreach (ulong id in network.ConnectedClientsIds)
                if (!partyMembers.TryGetValue(id, out var member) || !member.Registered || !member.Ready) return false;
            return true;
        }

        void RefreshPartyStatus()
        {
            ReadyPlayerCount = 0;
            int registered = 0;
            foreach (var member in partyMembers.Values)
            {
                if (member.Registered) registered++;
                if (member.Registered && member.Ready) ReadyPlayerCount++;
            }
            if (!Connected) PartyReadyStatus = "Connect to a room to prepare your party.";
            else if (AdmissionsBlocked) PartyReadyStatus = "Defense active. New joins wait for the refuge.";
            else if (IsHost && admissions.PendingCount>0) PartyReadyStatus = "Waiting for joining players to finish connecting...";
            else if (partyMembers.Count < 1 || registered != partyMembers.Count) PartyReadyStatus = "Waiting for all connected players to register...";
            else PartyReadyStatus = ReadyPlayerCount + "/" + partyMembers.Count + " ready · Every player must ready at the portal.";
        }

        void SendPartyState(ulong? recipient = null)
        {
            if (!IsHost || string.IsNullOrEmpty(partyNonce)) return;
            var members = new PartyMemberState[partyMembers.Count];
            int index = 0;
            foreach (var item in partyMembers)
                members[index++] = new PartyMemberState { id = item.Key, slot=item.Value.Slot, registered = item.Value.Registered, ready = item.Value.Ready };
            uint serial = ++partyStateSequence;
            foreach (var item in partyMembers)
            {
                if (item.Key == LocalClientId || !item.Value.Registered || recipient.HasValue && item.Key != recipient.Value) continue;
                SendReliableTo(item.Key, JsonUtility.ToJson(new PartyMessage
                {
                    kind = PartyPrefix + "state.v1", nonce = partyNonce, request = item.Value.Request,
                    sequence = serial, members = members, capabilities=CoopPartyCapacity.WireProtocol, admissionsBlocked=HostAdmissionsBlocked
                }));
            }
        }

        bool ReceivePartyMessage(ulong sender, string payload)
        {
            // Other reliable consumers retain their own protocols and authority checks.
            if (string.IsNullOrEmpty(payload) || payload.IndexOf(PartyPrefix, StringComparison.Ordinal) < 0) return false;
            if (!Connected || network == null || payload.Length > MaximumPartyPayloadBytes || Utf8.GetByteCount(payload) > MaximumPartyPayloadBytes) return true;
            PartyMessage message;
            try { message = JsonUtility.FromJson<PartyMessage>(payload); }
            catch (ArgumentException) { return true; }
            if (message == null || string.IsNullOrEmpty(message.kind)) return true;
            if (!message.kind.StartsWith(PartyPrefix, StringComparison.Ordinal)) return false;
            if (IsHost)
            {
                if (HostAdmissionsBlocked || sender == LocalClientId || !IsConnectedPeer(sender) || !AdmitPartyRequest(sender, payload.Length)) return true;
                ReconcileParty();
                if (!partyMembers.TryGetValue(sender, out var member) || !ValidPartyToken(message.request)) return true;
                if (message.kind == PartyPrefix + "hello.v1")
                {
                    bool registrationChanged = !member.Registered || member.Request != message.request;
                    member.Registered = true;
                    member.Request = message.request;
                    if (registrationChanged) { member.Sequence = 0; ResetPartyReadiness(); }
                    else SendPartyState(sender);
                }
                else if (message.kind == PartyPrefix + "ready.v1" && member.Registered && member.Request == message.request && message.nonce == partyNonce)
                    AcceptPartyReady(sender, message.ready, message.trained, message.sequence);
            }
            else if (sender == NetworkManager.ServerClientId && message.kind == PartyPrefix + "state.v1" &&
                message.request == partyRequest && ValidPartyToken(message.nonce) && Newer(message.sequence, partyAppliedSequence))
                ApplyPartyState(message);
            return true;
        }

        bool AdmitPartyRequest(ulong sender, int bytes)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            partyBudgets.TryGetValue(sender, out var budget);
            if (now - budget.Window >= 1d) budget = new TrafficBudget { Window = now };
            budget.Messages++;
            budget.Bytes += bytes;
            partyBudgets[sender] = budget;
            return budget.Messages <= 8 && budget.Bytes <= 8 * MaximumPartyPayloadBytes;
        }

        void AcceptPartyReady(ulong sender, bool ready, bool trained, uint serial)
        {
            if (!partyMembers.TryGetValue(sender, out var member) || !member.Registered || !IsConnectedPeer(sender) || !Newer(serial, member.Sequence)) return;
            member.Sequence = serial;
            if (!ready) { RecordAcceptedPeerActivity(sender); ResetPartyReadiness(); return; }
            if (!trained) { SendPartyState(sender); return; }
            member.Ready = true;
            RecordAcceptedPeerActivity(sender);
            RefreshPartyStatus();
            SendPartyState();
            if (AllPartyMembersReady()) StartCampaign();
        }

        void ApplyPartyState(PartyMessage message)
        {
            if(message.capabilities!=CoopPartyCapacity.WireProtocol||message.members==null||message.members.Length<1||message.members.Length>MaximumPlayers)return;
            bool local=false;
            for(int i=0;i<message.members.Length;i++)
            {
                var item=message.members[i];if(item==null||item.ready&&!item.registered)return;
                memberBuffer[i]=new CoopMemberSlot(item.id,item.slot);local|=item.id==LocalClientId;
            }
            if(!local||!admissions.ApplyHostSnapshot(memberBuffer,message.members.Length))return;
            partyMembers.Clear();memberIds.Clear();
            foreach(var item in message.members)
            {partyMembers.Add(item.id,new PartyMember{Slot=item.slot,Registered=item.registered,Ready=item.ready});memberIds.Add(item.id);}
            partyNonce=message.nonce;partyAppliedSequence=message.sequence;playerCount=message.members.Length;
            remoteAdmissionsBlocked=message.admissionsBlocked;
            removals.Clear();foreach(var item in poses)if(!admissions.TryGetSlot(item.Key,out _))removals.Add(item.Key);
            foreach(ulong id in removals){poses.Remove(id);RemoveAvatar(id);}
            RefreshPartyStatus();TryApplyCampaignStart();
        }

        static bool ValidPartyToken(string value)
        {
            if (value == null || value.Length != 32) return false;
            foreach (char c in value) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
            return true;
        }

        void Broadcast(string channel, FastBufferWriter writer, NetworkDelivery delivery)
        {
            if (network == null || !network.IsServer) return;
            foreach (ulong peer in network.ConnectedClientsIds)
                if (peer != network.LocalClientId)
                    network.CustomMessagingManager.SendNamedMessage(channel, peer, writer, delivery);
        }

        bool IsConnectedPeer(ulong id)
        {
            if (network == null) return false;
            foreach (ulong peer in network.ConnectedClientsIds) if (peer == id) return true;
            return false;
        }

        static void WritePose(FastBufferWriter writer, Pose pose)
        {
            writer.WriteValueSafe(pose.Position);
            writer.WriteValueSafe(pose.Yaw);
            writer.WriteValueSafe(pose.Aim);
            writer.WriteValueSafe(pose.Sequence);
            writer.WriteValueSafe(pose.Appearance);
        }

        public bool TryGetPlayerPose(ulong clientId, out Vector3 position, out float yaw)
        {
            if(network!=null&&network.IsServer&&clientId==network.LocalClientId&&GameSession.Instance!=null&&GameSession.Instance.World!=null&&
                GameSession.Instance.World.IsCoopArena&&ArenaPoseValidator!=null)
            {
                if(poses.TryGetValue(clientId,out var accepted)){position=accepted.Position;yaw=accepted.Yaw;return true;}
                position=Vector3.zero;yaw=0f;return false;
            }
            if (network != null && clientId == network.LocalClientId && GameSession.Instance != null && GameSession.Instance.Player != null)
            {
                position = GameSession.Instance.Player.transform.position;
                yaw = GameSession.Instance.Player.transform.eulerAngles.y;
                return true;
            }
            if (poses.TryGetValue(clientId, out var pose)) { position = pose.Position; yaw = pose.Yaw; return true; }
            position = Vector3.zero;
            yaw = 0f;
            return false;
        }

        public bool TryGetPlayerAim(ulong clientId, out Vector3 direction)
        {
            var game = GameSession.Instance;
            if (network != null && clientId == network.LocalClientId && game != null && game.Player != null && game.Player.ViewCamera != null)
            {
                direction = game.Player.ViewCamera.transform.forward;
                return ValidAim(direction);
            }
            if (poses.TryGetValue(clientId, out var pose) && ValidAim(pose.Aim))
            {
                direction = pose.Aim.normalized;
                return true;
            }
            direction = Vector3.forward;
            return false;
        }

#if UNITY_EDITOR
        // # ARCHITECTURE SNAPSHOT
        // Cold native-codec acceptance of this class's actual private WritePose/
        // ReadPose methods. No room creation, fake transport or auth mutation.
        [Serializable] public sealed class PoseWireReceipt
        {
            public string status, error;
            public int roomCapacity, capacityBytes, actualBytes, roundtrippedPeers;
            public bool nativeCodecExecuted, networkExecuted, malformedHeadersRejected;
        }
        public static PoseWireReceipt RunEightPoseWireAcceptance(int expectedPeers = 8)
        {
            var result = new PoseWireReceipt { status="FAIL", roomCapacity=MaximumPlayers, capacityBytes=PoseSnapshotCapacityBytes };
            try
            {
                if (expectedPeers != 8 || MaximumPlayers != expectedPeers || PoseSnapshotCapacityBytes != 355)
                    throw new InvalidOperationException("This probe requires the separately approved eight-member capacity source; no capacity flag is changed by the probe.");
                var expected = new Pose[8];
                using (var writer = new FastBufferWriter(PoseSnapshotCapacityBytes, Allocator.Temp, PoseSnapshotCapacityBytes))
                {
                    writer.WriteValueSafe((byte)8);writer.WriteValueSafe(true);writer.WriteValueSafe((byte)8);
                    for (int i=0;i<8;i++)
                    {
                        expected[i]=new Pose {Position=new Vector3(-31.234567f+i*3.1234567f,.01234567f,17.234567f-i),
                            Yaw=7.1234567f+i*37.125f,Aim=new Vector3(.1234567f+i*.017f,.2345678f,.964111f).normalized,Sequence=uint.MaxValue-(uint)i,Appearance=OracleAvatarAppearance.StartingLook(i).Packed};
                        writer.WriteValueSafe(i==0?0UL:ulong.MaxValue-(ulong)i);WritePose(writer,expected[i]);
                    }
                    result.actualBytes=writer.Length;
                    if (writer.Length!=355)throw new InvalidOperationException("Actual writer width differs from the declared codec bound.");
                    var reader=new FastBufferReader(writer,Allocator.Temp);
                    try
                    {
                        if(!reader.TryBeginRead(3))throw new InvalidOperationException("Actual header is truncated.");
                        reader.ReadValueSafe(out byte members);reader.ReadValueSafe(out bool campaign);reader.ReadValueSafe(out byte count);
                        if(!campaign||members!=8||count!=8||!CoopPartyCapacity.ValidPoseHeader(members,count,reader.Length-reader.Position))throw new InvalidOperationException("Actual header does not admit all eight poses.");
                        for(int i=0;i<8;i++)
                        {
                            if(!reader.TryBeginRead(CoopPartyCapacity.PoseEntryBytes))throw new InvalidOperationException("Actual vector entry is truncated.");
                            reader.ReadValueSafe(out ulong id);Pose pose=ReadPose(ref reader);
                            if(id!=(i==0?0UL:ulong.MaxValue-(ulong)i)||!pose.Position.Equals(expected[i].Position)||pose.Yaw!=expected[i].Yaw||
                                !pose.Aim.Equals(expected[i].Aim)||pose.Sequence!=expected[i].Sequence||pose.Appearance!=expected[i].Appearance||!ValidPose(pose))
                                throw new InvalidOperationException("Actual private pose codec failed peer "+i);
                            result.roundtrippedPeers++;
                        }
                        if(reader.Position!=reader.Length)throw new InvalidOperationException("Unexpected trailing pose bytes.");
                    }
                    finally{reader.Dispose();}
                }
                result.malformedHeadersRejected=!CoopPartyCapacity.ValidPoseHeader(9,8,352)&&!CoopPartyCapacity.ValidPoseHeader(8,9,396)&&
                    !CoopPartyCapacity.ValidPoseHeader(8,8,351)&&!CoopPartyCapacity.ValidPoseHeader(0,0,0)&&!CoopPartyCapacity.ValidPoseHeader(1,2,88);
                if(!result.malformedHeadersRejected)throw new InvalidOperationException("Malformed header was admitted.");
                result.nativeCodecExecuted=true;result.status="PASS_NATIVE_CODEC_ONLY_NOT_EIGHT_CLIENT_TRANSPORT";
            }
            catch(Exception error){result.error=error.GetType().Name+": "+error.Message;}
            return result;
        }
#endif

#if UNITY_EDITOR
        [Serializable] public sealed class PartyRosterWireReceipt
        {
            public string status,error;
            public int actualBytes,roundtrippedPeers;
            public bool nativeJsonExecuted,networkExecuted;
        }
        public static PartyRosterWireReceipt RunPartyRosterWireAcceptance()
        {
            var receipt=new PartyRosterWireReceipt{status="FAIL"};
            try
            {
                var message=new PartyMessage{kind=PartyPrefix+"state.v1",nonce=new string('a',32),request=new string('b',32),
                    capabilities=CoopPartyCapacity.WireProtocol,sequence=uint.MaxValue,admissionsBlocked=true,members=new PartyMemberState[MaximumPlayers]};
                for(int i=0;i<MaximumPlayers;i++)message.members[i]=new PartyMemberState{id=i==0?0UL:ulong.MaxValue-(ulong)i,slot=i,registered=true,ready=(i&1)==0};
                string json=JsonUtility.ToJson(message);receipt.actualBytes=Utf8.GetByteCount(json);
                if(receipt.actualBytes>MaximumPartyPayloadBytes)throw new InvalidOperationException("Full party roster exceeds its real receive budget.");
                var copy=JsonUtility.FromJson<PartyMessage>(json);
                if(copy==null||copy.kind!=message.kind||copy.nonce!=message.nonce||copy.request!=message.request||copy.capabilities!=message.capabilities||
                    copy.sequence!=message.sequence||!copy.admissionsBlocked||copy.members==null||copy.members.Length!=MaximumPlayers)
                    throw new InvalidOperationException("Native party header roundtrip failed.");
                var slots=new CoopMemberSlot[MaximumPlayers];
                for(int i=0;i<MaximumPlayers;i++)
                {
                    var peer=copy.members[i];var expected=message.members[i];
                    if(peer==null||peer.id!=expected.id||peer.slot!=expected.slot||peer.registered!=expected.registered||peer.ready!=expected.ready)
                        throw new InvalidOperationException("Native party member roundtrip failed at "+i);
                    slots[i]=new CoopMemberSlot(peer.id,peer.slot);receipt.roundtrippedPeers++;
                }
                if(!new CoopAdmissionRoster().ApplyHostSnapshot(slots,slots.Length))throw new InvalidOperationException("Roundtripped roster fails production admission validation.");
                receipt.nativeJsonExecuted=true;receipt.status="PASS_NATIVE_JSON_ONLY_NOT_EIGHT_CLIENT_TRANSPORT";
            }
            catch(Exception error){receipt.error=error.GetType().Name+": "+error.Message;}
            return receipt;
        }
#endif

        static Pose ReadPose(ref FastBufferReader reader)
        {
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out float yaw);
            reader.ReadValueSafe(out Vector3 aim);
            reader.ReadValueSafe(out uint serial);
            reader.ReadValueSafe(out uint appearance);
            return new Pose { Position = position, Yaw = yaw, Aim = aim, Sequence = serial, Appearance = appearance };
        }

        static bool ValidPose(Pose pose)
        {
            return OracleAvatarAppearance.TryDecode(pose.Appearance,out _) && Finite(pose.Position.x) && Finite(pose.Position.y) && Finite(pose.Position.z) && Finite(pose.Yaw) && ValidAim(pose.Aim)
                && Mathf.Abs(pose.Position.x) <= 512f && Mathf.Abs(pose.Position.z) <= 512f
                && pose.Position.y >= -25f && pose.Position.y <= 100f;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool ValidAim(Vector3 direction) => Finite(direction.x) && Finite(direction.y) && Finite(direction.z) &&
            direction.sqrMagnitude >= .9f && direction.sqrMagnitude <= 1.1f;
        static bool Newer(uint incoming, uint previous) => unchecked((int)(incoming - previous)) > 0;
        static bool IsCode(string value) { foreach (char c in value) if (c < 'A' || c > 'Z') if (c < '0' || c > '9') return false; return true; }

        void ApplyAvatar(ulong id, Pose pose)
        {
            if (network == null || !hubApplied || id == network.LocalClientId || network.IsServer && !hubSpawnSlots.Placed(id)) return;
            if (!avatars.TryGetValue(id, out var avatar) || avatar == null)
            {
                avatar = RentRemotePlayer015(id,pose.Position,pose.Yaw);
                if(avatar==null)return;
                avatars[id] = avatar;
            }
            avatar.ApplyAppearance(pose.Appearance);
            avatar.ApplyPose(pose.Position, pose.Yaw);
        }

        void RemoveAvatar(ulong id)
        {
            if (!avatars.TryGetValue(id, out var avatar)) return;
            avatars.Remove(id);
            if (avatar != null) ReturnRemotePlayer015(avatar);
        }

        void OnRemovedFromSession() { if (!intentionalStop) ConnectionLost("You were removed from the room, or the room closed."); }

        void ConnectionLost(string reason)
        {
            if (intentionalStop || leaving || destroyed) return;
            roomPhase020=RoomConnectionPhase020.Failed;InvalidateTypedPublication020();
            if(requestedRoomMode020==CoopRoomMode020.TowerDefense)UnexpectedSoloRestartConnectionLoss?.Invoke();
            BeginOwnedLeave(true, false, reason);
        }

        async Task FailConnectionAsync(RoomConnectionAttempt attempt, Exception error)
        {
            string phase = attempt.Phase;
            string reason = !string.IsNullOrEmpty(connectionRejection)?connectionRejection:Explain(error);
            bool current = IsCurrent(attempt.Ticket) && ReferenceEquals(connectionAttempt, attempt);
            if (current) Status = "Could not connect while " + phase + ": " + reason + " Closing the connection...";
            attempt.Phase = "closing the failed game connection";
            string cleanupFailure = await DisconnectAsync();
            // Leave/destruction may change the ticket while SDK cleanup awaits.
            // An old failure must not overwrite the new owner's visible status.
            if (IsCurrent(attempt.Ticket) && ReferenceEquals(connectionAttempt, attempt))
                Status = "Could not connect while " + phase + ": " + reason +
                    (cleanupFailure == null ? "" : " Room cleanup failed: " + cleanupFailure);
        }

        Task<string> DisconnectAsync()
        {
            if (disconnectTask != null && !disconnectTask.IsCompleted) return disconnectTask;
            disconnectTask = DisconnectCoreAsync();
            return disconnectTask;
        }

        async Task<string> DisconnectCoreAsync()
        {
            bool closeHostedSession = closeHostedSessionNextDisconnect;
            closeHostedSessionNextDisconnect = false;
            intentionalStop = true;
            Connected = false;
            var retiringTownGrant020=campaignTownRoomGrant020;campaignTownRoomGrant020=default;InvalidateTypedPublication020();
            if(roomPhase020!=RoomConnectionPhase020.Failed&&roomPhase020!=RoomConnectionPhase020.Canceled)roomPhase020=RoomConnectionPhase020.Leaving;
            CampaignActive = campaignPending = false;
            ResetParty();
            playerCount = 0;
            sequence = 0;
            var leaving = session;
            session = null;
            if (leaving == null && pendingIdleHostDeletion != null)
            {
                leaving = pendingIdleHostDeletion; pendingIdleHostDeletion = null;
                closeHostedSession = true;
            }
            if (leaving != null) leaving.RemovedFromSession -= OnRemovedFromSession;
            string failure = null;
            if(requestedRoomMode020==CoopRoomMode020.CampaignTown)
            {try{RetireCampaignTownRoomTransport020(retiringTownGrant020);}catch(Exception error){failure=Explain(error);}}
            try { DetachNetwork(); ClearAvatars(); }
            catch (Exception error) { failure = failure==null?Explain(error):failure+"; "+Explain(error); }
            // MPS owns its NGO session. LeaveAsync awaits the SDK's transport stop;
            // direct Shutdown before it bypasses that lifecycle owner.
            if (leaving != null)
            {
                try
                {
                    if (closeHostedSession)
                    {
                        if (!leaving.IsHost) throw new InvalidOperationException("The captured room is no longer host-owned.");
                        await leaving.AsHost().DeleteAsync();
                    }
                    else await leaving.LeaveAsync();
                }
                catch (Exception error)
                {
                    if (closeHostedSession) pendingIdleHostDeletion = leaving;
                    failure = failure==null?Explain(error):failure+"; "+Explain(error);
                }
            }
            // An unreturned/failed session can leave an orphan manager. Only that
            // remaining transport uses the guarded, bounded direct-stop fallback.
            string transportFailure = await StopRemainingTransportAsync();
            if (transportFailure != null) failure = failure == null ? transportFailure : failure + "; " + transportFailure;
            if (network != null && (network.IsListening || network.ShutdownInProgress)) return failure;
            RestoreApprovalConfiguration();
            if (ownsNetwork && network != null)
            {
                var root = network.gameObject;
                network = null;
                ownsNetwork = false;
                if (root != null) Destroy(root);
                if (!destroyed) await Awaitable.NextFrameAsync();
            }
            else network = null;
            if(roomPhase020!=RoomConnectionPhase020.Failed&&roomPhase020!=RoomConnectionPhase020.Canceled)roomPhase020=RoomConnectionPhase020.Offline;
            return failure;
        }

        async Task<string> StopRemainingTransportAsync()
        {
            if (network == null || (!network.IsListening && !network.ShutdownInProgress)) return null;
            try
            {
                if (!network.ShutdownInProgress) network.Shutdown();
                double deadline = Time.realtimeSinceStartupAsDouble + 5d;
                while (network != null && (network.IsListening || network.ShutdownInProgress))
                {
                    if (Application.exitCancellationToken.IsCancellationRequested)
                        return "Application exit interrupted transport cleanup.";
                    if (Time.realtimeSinceStartupAsDouble >= deadline)
                        return "Transport cleanup did not finish within five seconds; retry leaving before reconnecting.";
                    await Awaitable.NextFrameAsync();
                }
                return null;
            }
            catch (Exception error) { return Explain(error); }
        }

        void DetachNetwork()
        {
            if(requestedRoomMode020==CoopRoomMode020.CampaignTown)
            {if(network!=null){network.OnClientConnectedCallback-=OnClientConnected;network.OnClientDisconnectCallback-=OnClientDisconnected;}handlersRegistered=false;return;}
            ReleaseXpTransport015();
            ReleaseLaneBuildTransport015();
            ReleaseArenaImpactTransport();
            ReleaseRoomReactionTransport();
            if (network == null) return;
            network.OnClientConnectedCallback -= OnClientConnected;
            network.OnClientDisconnectCallback -= OnClientDisconnected;
            if (handlersRegistered && network.CustomMessagingManager != null)
            {
                network.CustomMessagingManager.UnregisterNamedMessageHandler(UploadChannel);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(SnapshotChannel);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(HubSpawnChannel);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(CampaignChannel);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(ReliableChannel);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(IdleChannel);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(SharedLabChannel);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(RoomReactionChannel);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(RefugeFlameEventChannel020);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(ArenaImpactChannel);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(XpChannel015);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(LaneBuildChannel015);
            }
            handlersRegistered = false;
        }

        void RestoreApprovalConfiguration()
        {
            if(network==null)return;
            if(approvalInstalled)
            {
                if(network.ConnectionApprovalCallback==ApproveConnection)
                {
                    network.ConnectionApprovalCallback=null;
                    network.NetworkConfig.ConnectionApproval=priorApproval;network.NetworkConfig.ConnectionData=priorConnectionData;
                }
                priorConnectionData=null;approvalInstalled=false;
            }
        }

        void ClearAvatars()
        {
            foreach (var item in avatars) if (item.Value != null) ReturnRemotePlayer015(item.Value);
            avatars.Clear();
            poses.Clear();
            lastUpload.Clear();
            reliableBudgets.Clear();
        }

        bool IsCurrent(int ticket) => !destroyed && operation == ticket;
        void EnsureCurrent(int ticket) { if (!IsCurrent(ticket)) throw new OperationCanceledException(); }
        static string Explain(Exception error)
        {
            string detail = error is OperationCanceledException ? "The connection was canceled." : error.Message;
            if (string.IsNullOrWhiteSpace(detail)) detail = error.GetType().Name;
            detail = detail.Replace('\n', ' ').Replace('\r', ' ');
            return detail.Length > 240 ? detail.Substring(0, 240) : detail;
        }

        async void OnDestroy()
        {
            destroyed = true;ArenaPoseValidator=null;
            ++operation;
            if (Instance == this) Instance = null;
            await CancelAndDrainConnectionAttemptAsync();
            await DisconnectAsync();
        }
    }
}

// # ARCHITECTURE SNAPSHOT: Unity metres/Y-up/XZ and fixed8 NGO admission/pose arrays are unchanged. Cold per-attempt CTS is main-thread owned through SDK completion plus SDK-first cleanup; operation tickets reject late sessions, retained sessions are left before the manager lease is released, and unsupported init/auth awaits cannot spawn a manager after timeout. Saved assets/profiles are borrowed and unchanged.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: unchanged main-thread SDK/NGO connection ownership, no spatial changes.
// Array Formats: existing fixed8 admission/pose formats and one existing attempt lease remain unchanged.
// Dependencies/Pointers: reject foreign active managers before taking cleanup ownership; stale tickets cannot replace status after await.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge world metres/Y-up; host-owned shared lab, model local+Z.
// Array Formats: Two native peer records and bounded fixed frames; inherited48 projectile pool and two real actors.
// Dependencies/Pointers: Dedicated shared owner validates requests/fences; guests remote-only; no rewards, migration or narrator; root integrates complete slice.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: host accepted World contacts and existing run/epoch/shot identity; no damage/HP/path permission changes.
// Array Formats: existing bounded launch receipts add three-bit landing dedupe; fixed256B shared wire unchanged; new64B arena frame through existing NGO pipeline.
// Dependencies/Pointers: sole host/network/audio owners retain authority, lifetime and gesture/focus policies; actual peer/audio acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world and accepted015 campaign/source identity, original arena positions retained.
// Array Formats: Eight milestones/40waves/905logical rewards within4096native slots, fixed128B XP channel and existing bounded pools.
// Dependencies/Pointers: Root composes actual current pool/audio owners with reviewed campaign/XP hunks; source/native/browser/performance/public acceptance remain separate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original Oracle accepted world roots and camera/child-local poses only; no authority geometry changes.
// Array Formats: Fixed31shared body slots/2prewarmed player graphs and original palette/pose/feedback buffers; no wire ABI growth.
// Dependencies/Pointers: Player/Rooms/Combat remain sole owners; root integrates default-OFF original native player pilot and verifies real bindings/peer/clearance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up/XZ, exact1m terrain/.25m build roots and locked active-road context.
// Array Formats: Bounded native semantic/preview/body stores, exact64B versioned command and existing2peer/4tower snapshots.
// Dependencies/Pointers: Root integrates exact guarded slices; host economy/readiness/LOS and World geometry remain canonical, prepared/source compile is not native or browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing authenticated service/NGO room boundary; no gameplay/root changes.
// Array Formats: Existing fixed DTOs and same-length ASCII WireProtocol; one central0.1.1 RoomType for create/join.
// Dependencies/Pointers: Existing approval compares exact bytes before remote reservation, party capabilities share the same constant; no new protocol owner. Native/service rejection and same-build join pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing connection teardown and accepted refuge event scope.
// Array Formats: one extra named-handler unregister only, existing writer disposal unchanged.
// Dependencies/Pointers: normal disconnect/reload lifetime invalidates World receipts; no new transport owner, state-frame bytes, message authority or gameplay state.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Explicit typed room destination and accepted local arrival; legacy TD2 refuge/arena semantics retained.
// Array Formats: Existing current buffers/wire retained for TD; cold mode/cap/protocol SDK metadata and one operation-derived online publication.
// Dependencies/Pointers: Campaign has no default destination/transport implementation and cannot fall through to BeginCoop/defense/solo recovery. Root owns full town lease/peer protocol, source import and actual two-peer proof; Quick Host stays preparing.
