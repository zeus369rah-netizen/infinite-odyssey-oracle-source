using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RoomConnection
    {
        CoopRoomMode020 requestedRoomMode020,acceptedRoomMode020;
        RoomConnectionPhase020 roomPhase020;
        int acceptedRoomOperation020;
        CampaignTownRoomGrant020 campaignTownRoomGrant020;
        uint requestedTownGeneration020,requestedTownRevision020;
        public CoopRoomMode020 RequestedRoomMode020=>requestedRoomMode020;
        public CoopRoomMode020 AcceptedRoomMode020=>RoomOnline020?acceptedRoomMode020:CoopRoomMode020.None;
        public RoomConnectionPhase020 RoomPhase020=>roomPhase020;
        public string RoomStatus020=>Status;
        public string RoomShareCode020=>RoomOnline020&&session!=null?session.Code:string.Empty;
        public int RoomCapacity020=>RoomOnline020&&session!=null?session.MaxPlayers:0;
        public bool RoomOnline020=>CoopRoomModeRules020.CanPublish(acceptedRoomMode020,acceptedRoomOperation020,operation,
            TypedServiceMatches020(),Connected&&network!=null&&network.IsListening&&network.IsConnectedClient,TypedArrivalCurrent020(),intentionalStop||leaving||destroyed);

        // No implementations in Phase1: destination/pose/travel owner must be installed and verified before Campaign can connect.
        partial void CampaignTownCapabilities020(ref bool host,ref bool join);
        partial void PrepareCampaignTownLocalWorld020(bool hosting,ref bool prepared);
        partial void PrepareCampaignTownRoomArrival020(CampaignTownRoomContext020 context,CancellationToken cancellation,ref Task<CampaignTownRoomGrant020> result);
        partial void CampaignTownRoomLeaseCurrent020(CampaignTownRoomGrant020 grant,ref bool current);
        partial void RegisterCampaignTownRoomHandlers020();
        partial void TickCampaignTownRoomTransport020();
        partial void CampaignTownRoomPeerChanged020(ulong client,bool connected);
        partial void RetireCampaignTownRoomTransport020(CampaignTownRoomGrant020 grant);

        public bool SupportsRoomMode020(CoopRoomMode020 mode)
        {
            if(mode==CoopRoomMode020.TowerDefense)return true;
            if(mode!=CoopRoomMode020.CampaignTown)return false;
            bool host=false,join=false;CampaignTownCapabilities020(ref host,ref join);return host&&join;
        }
        public bool CanHostRoom020(CoopRoomMode020 mode)=>CanBeginTypedRequest020(mode);
        public bool CanJoinRoom020(CoopRoomMode020 mode)=>CanBeginTypedRequest020(mode);
        public bool CanQuickHost020(CoopRoomMode020 mode)=>false; // Core world-entry/upgrade request owner is a later explicit phase.
        public bool CancelRoomAttempt020(){if(!Busy&&network==null&&session==null&&pendingIdleHostDeletion==null)return false;roomPhase020=RoomConnectionPhase020.Canceled;LeaveRoom();return true;}
        public int SupportedRoomCapacity020(CoopRoomMode020 mode)=>RoomOnline020&&mode==acceptedRoomMode020?RoomCapacity020:SupportsRoomMode020(mode)?CoopRoomModeRules020.SupportedMaximum(mode):0;
        bool CanBeginTypedRequest020(CoopRoomMode020 mode)=>SupportsRoomMode020(mode)&&!Busy&&!Connected&&!leaving&&!destroyed&&pendingConnections==0&&connectionAttempt==null&&
            (disconnectTask==null||disconnectTask.IsCompleted)&&(mode==requestedRoomMode020||network==null&&session==null&&pendingIdleHostDeletion==null)&&
            (mode==CoopRoomMode020.CampaignTown||GameSession.Instance?.World?.Mode020!=WorldMode020.CampaignHub);
        bool BeginTypedRequest020(CoopRoomMode020 mode,bool hosting)
        {
            if(!CanBeginTypedRequest020(mode))
            {if(!SupportsRoomMode020(mode))Status=mode==CoopRoomMode020.CampaignTown?"Shared Campaign town is preparing; its destination and separate arrival are not admitted yet.":"That room mode is unavailable.";return false;}
            if(operation==int.MaxValue){Status="Room operation sequence exhausted.";return false;}
            if(mode==CoopRoomMode020.CampaignTown)
            {
                bool prepared=false;PrepareCampaignTownLocalWorld020(hosting,ref prepared);var game=GameSession.Instance;
                if(!prepared||game?.World==null||!game.World.CampaignHubActive020){Status="The Campaign town destination is not ready.";return false;}
                requestedTownGeneration020=game.World.CampaignHubGeneration020;requestedTownRevision020=game.World.WorldRevision020;
            }
            requestedRoomMode020=mode;acceptedRoomMode020=CoopRoomMode020.None;acceptedRoomOperation020=0;
            roomPhase020=hosting?RoomConnectionPhase020.Hosting:RoomConnectionPhase020.Joining;return true;
        }
        void AddTypedSessionProperties020(SessionOptions options)
        {
            options.SessionProperties[CoopRoomModeRules020.ModeProperty]=new SessionProperty(CoopRoomModeRules020.Tag(requestedRoomMode020));
            options.SessionProperties[CoopRoomModeRules020.CapacityProperty]=new SessionProperty(options.MaxPlayers.ToString(CultureInfo.InvariantCulture));
            options.SessionProperties[CoopRoomModeRules020.ProtocolProperty]=new SessionProperty(CoopRoomModeRules020.Protocol(requestedRoomMode020));
        }
        string TypedProperty020(string name)=>session!=null&&session.Properties!=null&&session.Properties.TryGetValue(name,out var property)?property?.Value:null;
        bool TypedServiceMatches020()=>session!=null&&!string.IsNullOrEmpty(session.Id)&&CoopRoomModeRules020.SessionMatches(requestedRoomMode020,session.MaxPlayers,
            TypedProperty020(CoopRoomModeRules020.ModeProperty),TypedProperty020(CoopRoomModeRules020.CapacityProperty),TypedProperty020(CoopRoomModeRules020.ProtocolProperty));
        void RequireTypedService020()
        {if(!TypedServiceMatches020())throw new InvalidOperationException("The room mode, capacity or multiplayer version does not match the selected game.");}
        bool TypedArrivalCurrent020()
        {
            if(acceptedRoomMode020==CoopRoomMode020.TowerDefense)return hubApplied;
            if(acceptedRoomMode020!=CoopRoomMode020.CampaignTown)return false;
            var world=GameSession.Instance?.World;
            if(world==null||!world.CampaignHubActive020||world.CampaignHubGeneration020!=campaignTownRoomGrant020.LocalGeneration||world.WorldRevision020!=campaignTownRoomGrant020.LocalRevision)return false;
            bool current=false;CampaignTownRoomLeaseCurrent020(campaignTownRoomGrant020,ref current);return current;
        }
        async Task FinishCampaignTownRoom020(int ticket,RoomConnectionAttempt attempt)
        {
            roomPhase020=RoomConnectionPhase020.RegionPreparing;Task<CampaignTownRoomGrant020> admission=null;
            var context=new CampaignTownRoomContext020{RoomId=session.Id,Operation=ticket,LocalClientId=network.LocalClientId,Hosting=network.IsServer,LocalGeneration=requestedTownGeneration020,LocalRevision=requestedTownRevision020};
            PrepareCampaignTownRoomArrival020(context,attempt.Token,ref admission);
            if(admission==null)throw new InvalidOperationException("No Campaign town destination/arrival owner is installed.");
            var grant=await admission;EnsureConnectionAttemptCurrent(ticket,attempt);RequireTypedService020();
            var game=GameSession.Instance;var world=game?.World;
            bool identity=grant.RoomId==session.Id&&grant.RegionName=="PortalLanding020"&&grant.RegionInstance!=0&&grant.ClientId==network.LocalClientId&&
                grant.Seat==(network.IsServer?0:1)&&grant.LocalGeneration==requestedTownGeneration020&&grant.LocalRevision==requestedTownRevision020&&
                grant.Supported&&grant.Applied&&grant.Acknowledged&&(network.IsServer||grant.Distinct)&&Finite(grant.X)&&Finite(grant.Y)&&Finite(grant.Z)&&Finite(grant.Yaw);
            bool current=false;if(identity)CampaignTownRoomLeaseCurrent020(grant,ref current);
            if(!identity||!current||world==null||!world.CampaignHubActive020||world.CampaignHubGeneration020!=grant.LocalGeneration||world.WorldRevision020!=grant.LocalRevision||
                game.Player==null||(game.Player.transform.position-new Vector3(grant.X,grant.Y,grant.Z)).sqrMagnitude>.01f)
                throw new InvalidOperationException("The Campaign room has no current supported, applied and acknowledged separate town arrival.");
            campaignTownRoomGrant020=grant;hubApplied=true;Connected=true;playerCount=network.IsServer?network.ConnectedClientsIds.Count:1;nextSend=0;
            acceptedRoomMode020=CoopRoomMode020.CampaignTown;acceptedRoomOperation020=ticket;roomPhase020=RoomConnectionPhase020.Online;
            Status="Shared Campaign town connected.";PartyReadyStatus="Town arrival accepted; defense and combat remain separate.";
        }
        void PublishDefenseRoom020(int ticket)
        {RequireTypedService020();if(!hubApplied)throw new InvalidOperationException("The defense refuge arrival is not accepted.");acceptedRoomMode020=CoopRoomMode020.TowerDefense;acceptedRoomOperation020=ticket;roomPhase020=RoomConnectionPhase020.Online;}
        void InvalidateTypedPublication020()
        {acceptedRoomMode020=CoopRoomMode020.None;acceptedRoomOperation020=0;} // Retain the owned grant until disconnect passes it to retirement.
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: One selected authenticated room mode and exact local world/arrival lease; existing SDK Type is not treated as remote mode proof.
// Array Formats: Existing TD2 storage unchanged, three cold SDK metadata properties, one operation-derived publication and one value town grant. No new serialization, frame owner or duplicate task/manager.
// Dependencies/Pointers: Missing private partial town destination/transport hooks keep Campaign false and Quick Host remains closed. Root owns their complete protocol/peer/instance/spawn implementation. Existing RoomConnection attempt/SDK-first cleanup remains authoritative; no TD BeginCoop/start/recovery fallback for Campaign.
