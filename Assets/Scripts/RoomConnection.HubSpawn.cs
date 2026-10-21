using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RoomConnection
    {
        const string HubSpawnChannel="odyssey.hub.spawn.v1";
        const byte SpawnRequest=1,SpawnOffer=2,SpawnAck=3,SpawnReject=4;
        const int SpawnOfferBytes=31;
        readonly CoopHubSpawnSlots hubSpawnSlots=new CoopHubSpawnSlots();
        readonly ulong[] hubJoiningIds=new ulong[8],hubExpiredIds=new ulong[8];
        readonly double[] hubJoinDeadlines=new double[8],hubNextRequests=new double[8];
        uint hubEpoch,hubRequest;
        bool hubAwaiting,hubOfferReady,hubApplied,hubRejected;
        CoopHubSpawnGrant hubLocalGrant;
        public bool HasAcceptedHubSpawn=>hubApplied;

        void ResetHubSpawns()
        {
            hubEpoch++;if(hubEpoch==0)hubEpoch++;
            hubSpawnSlots.Reset(hubEpoch);hubRequest=0;hubAwaiting=hubOfferReady=hubApplied=hubRejected=false;hubLocalGrant=default;
            Array.Clear(hubJoiningIds,0,8);Array.Clear(hubJoinDeadlines,0,8);Array.Clear(hubNextRequests,0,8);
        }
        void BeginHubSpawnDeadline(ulong client)
        {
            hubSpawnSlots.Release(client);
            if(network==null||client==network.LocalClientId||!admissions.TryGetSlot(client,out int slot)||slot<1)return;
            hubJoiningIds[slot]=client;hubJoinDeadlines[slot]=Time.realtimeSinceStartupAsDouble+12d;hubNextRequests[slot]=0;
        }
        void ReleaseHubSpawn(ulong client)
        {hubSpawnSlots.Release(client);for(int i=1;i<8;i++)if(hubJoiningIds[i]==client){hubJoiningIds[i]=0;hubJoinDeadlines[i]=hubNextRequests[i]=0;}}
        void TickHubSpawnDeadlines()
        {
            if(network==null||!network.IsServer)return;double now=Time.realtimeSinceStartupAsDouble;int count=0;
            for(int i=1;i<8;i++)
            {
                if(hubJoinDeadlines[i]==0)continue;
                if(hubSpawnSlots.Placed(hubJoiningIds[i])){hubJoinDeadlines[i]=0;continue;}
                if(now>=hubJoinDeadlines[i]){hubExpiredIds[count++]=hubJoiningIds[i];hubJoinDeadlines[i]=0;}
            }
            for(int i=0;i<count;i++)network.DisconnectClient(hubExpiredIds[i],"A supported initial refuge spawn was not completed in time. Rejoin using the current room code.");
        }
        async Task PrepareHubSpawnAsync(int ticket,RoomConnectionAttempt attempt)
        {
            var game=GameSession.Instance;
            if(game==null||game.Player==null||game.World==null||game.World.Mode020!=WorldMode020.TutorialRefuge)throw new InvalidOperationException("A shared refuge is required before assigning a safe join position.");
            if(network.IsServer){hubApplied=true;return;} // Preserve host feet/yaw exactly.
            hubRequest=(uint)ticket;if(hubRequest==0)throw new InvalidOperationException("Spawn request sequence exhausted.");
            hubAwaiting=true;hubOfferReady=hubRejected=false;double deadline=Time.realtimeSinceStartupAsDouble+8d,next=0;
            attempt.Phase="waiting for the host's supported refuge spawn";Status="Choosing a separate supported place in the refuge...";
            try
            {
                while(!hubOfferReady)
                {
                    EnsureConnectionAttemptCurrent(ticket,attempt);
                    if(hubRejected)throw new InvalidOperationException("The host could not reserve a supported, unoccupied refuge spawn. Ask the host to make space and retry.");
                    double now=Time.realtimeSinceStartupAsDouble;if(now>=deadline)throw new TimeoutException("The host did not confirm a supported refuge spawn within eight seconds.");
                    if(network==null||!network.IsConnectedClient)throw new InvalidOperationException("Transport closed before the spawn was assigned.");
                    if(now>=next){next=now+.75d;SendHubControl(NetworkManager.ServerClientId,SpawnRequest,hubRequest);}
                    await Awaitable.NextFrameAsync();
                }
            }
            finally{hubAwaiting=false;}
        }
        public bool TryGetLocalHubSpawn(out Vector3 position,out float yaw)
        {
            position=default;yaw=0;var game=GameSession.Instance;
            if(network==null||game==null||game.Player==null)return false;
            if(network.IsServer){position=game.Player.transform.position;yaw=game.Player.transform.eulerAngles.y;return true;}
            if(!hubOfferReady)return false;position=new Vector3(hubLocalGrant.X,hubLocalGrant.Y,hubLocalGrant.Z);yaw=hubLocalGrant.Yaw;return true;
        }
        public void CommitLocalHubSpawn()
        {
            if(network==null)return;
            if(network.IsServer){hubApplied=true;return;}
            var game=GameSession.Instance;
            if(!hubOfferReady||game==null||game.Player==null||(game.Player.transform.position-new Vector3(hubLocalGrant.X,hubLocalGrant.Y,hubLocalGrant.Z)).sqrMagnitude>.01f)
                throw new InvalidOperationException("Apply the accepted host spawn before enabling the shared refuge camera.");
            hubApplied=true;SendHubControl(NetworkManager.ServerClientId,SpawnAck,hubRequest,hubLocalGrant.Epoch,hubLocalGrant.Revision);
        }
        void SendHubControl(ulong recipient,byte kind,uint request,uint epoch=0,uint revision=0)
        {
            if(network==null||!network.IsListening)return;
            int size=kind==SpawnAck?13:5;
            using(var writer=new FastBufferWriter(size,Allocator.Temp,size))
            {writer.WriteValueSafe(kind);writer.WriteValueSafe(request);if(kind==SpawnAck){writer.WriteValueSafe(epoch);writer.WriteValueSafe(revision);}network.CustomMessagingManager.SendNamedMessage(HubSpawnChannel,recipient,writer,NetworkDelivery.ReliableSequenced);}
        }
        void ReceiveHubSpawn(ulong sender,FastBufferReader reader)
        {
            if(network==null||!reader.TryBeginRead(5))return;
            try
            {
                reader.ReadValueSafe(out byte kind);reader.ReadValueSafe(out uint request);if(request==0)return;
                if(network.IsServer)
                {
                    if(sender==network.LocalClientId||!IsConnectedPeer(sender)||!admissions.TryGetSlot(sender,out int slot)||slot<1)return;
                    if(kind==SpawnRequest&&reader.Length==5)
                    {
                        double now=Time.realtimeSinceStartupAsDouble;if(now<hubNextRequests[slot])return;hubNextRequests[slot]=now+.5d;
                        if(HostAdmissionsBlocked){SendHubControl(sender,SpawnReject,request);return;}
                        if(hubSpawnSlots.TryGet(sender,out var previous))
                        {if(previous.Request==request&&previous.Epoch==hubEpoch)SendHubOffer(previous);return;}
                        if(!TryReserveHubSpawn(sender,slot,request,out var grant)){SendHubControl(sender,SpawnReject,request);return;}
                        SendHubOffer(grant);
                    }
                    else if(kind==SpawnAck&&reader.Length==13&&reader.TryBeginRead(8))
                    {reader.ReadValueSafe(out uint epoch);reader.ReadValueSafe(out uint revision);hubSpawnSlots.Acknowledge(sender,request,epoch,revision);}
                    return;
                }
                if(sender!=NetworkManager.ServerClientId||!hubAwaiting||request!=hubRequest)return;
                if(kind==SpawnReject&&reader.Length==5){hubRejected=true;return;}
                if(kind!=SpawnOffer||reader.Length!=SpawnOfferBytes||!reader.TryBeginRead(26))return;
                reader.ReadValueSafe(out uint offeredEpoch);reader.ReadValueSafe(out uint offeredRevision);reader.ReadValueSafe(out byte offeredSlot);reader.ReadValueSafe(out byte candidate);
                reader.ReadValueSafe(out Vector3 feet);reader.ReadValueSafe(out float yaw);
                if(offeredEpoch==0||offeredRevision==0||offeredSlot<1||offeredSlot>7||!CoopHubSpawnLayout.Candidate(candidate,out float x,out float z)||!Finite(feet.x)||!Finite(feet.y)||!Finite(feet.z)||!Finite(yaw)||Mathf.Abs(feet.x-x)>.001f||Mathf.Abs(feet.z-z)>.001f)return;
                var game=GameSession.Instance;
                if(game==null||!CoopHubSpawnGeometry.TryResolve(game.World,candidate,out var supported)||(supported-feet).sqrMagnitude>.01f){hubRejected=true;return;}
                if(hubOfferReady&&(hubLocalGrant.Epoch!=offeredEpoch||hubLocalGrant.Revision!=offeredRevision))return;
                hubLocalGrant=new CoopHubSpawnGrant{ClientId=network.LocalClientId,Request=request,Epoch=offeredEpoch,Revision=offeredRevision,Slot=offeredSlot,Candidate=candidate,X=feet.x,Y=feet.y,Z=feet.z,Yaw=yaw,State=1};hubOfferReady=true;
            }
            catch(OverflowException){ }
        }
        bool TryReserveHubSpawn(ulong client,int slot,uint request,out CoopHubSpawnGrant grant)
        {
            grant=default;var game=GameSession.Instance;
            if(game==null||game.Player==null||game.World==null||game.World.Mode020!=WorldMode020.TutorialRefuge)return false;
            Vector3 host=game.Player.transform.position;
            for(int attempt=0;attempt<16;attempt++)
            {
                int candidate=CoopHubSpawnLayout.OrderedCandidate(slot,attempt);CoopHubSpawnLayout.Candidate(candidate,out float x,out float z);
                if(!CoopHubSpawnLayout.Separated(x,z,host.x,host.z)||!hubSpawnSlots.SeparatedFromReservations(x,z,client))continue;
                bool occupied=false;
                foreach(var pose in poses)if(pose.Key!=client&&!CoopHubSpawnLayout.Separated(x,z,pose.Value.Position.x,pose.Value.Position.z)){occupied=true;break;}
                if(occupied||!CoopHubSpawnGeometry.TryResolve(game.World,candidate,out Vector3 feet))continue;
                return hubSpawnSlots.TryOffer(client,slot,request,candidate,feet.x,feet.y,feet.z,out grant);
            }
            return false;
        }
        void SendHubOffer(CoopHubSpawnGrant grant)
        {
            using(var writer=new FastBufferWriter(SpawnOfferBytes,Allocator.Temp,SpawnOfferBytes))
            {
                writer.WriteValueSafe(SpawnOffer);writer.WriteValueSafe(grant.Request);writer.WriteValueSafe(grant.Epoch);writer.WriteValueSafe(grant.Revision);writer.WriteValueSafe((byte)grant.Slot);writer.WriteValueSafe((byte)grant.Candidate);
                writer.WriteValueSafe(new Vector3(grant.X,grant.Y,grant.Z));writer.WriteValueSafe(grant.Yaw);
                network.CustomMessagingManager.SendNamedMessage(HubSpawnChannel,grant.ClientId,writer,NetworkDelivery.ReliableSequenced);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Host-accepted supported refuge feet/yaw only; arena correction and pose wire remain unchanged.
// Array Formats: Eight fixed48-byte grants; fixed5-byte request/reject,31-byte offer,13-byte acknowledgement; existing bounded NGO send writers dispose synchronously.
// Dependencies/Pointers: Existing NGO sender/admission authority, current per-attempt request and host epoch/revision; no player-chosen coordinate accepted. Host stays in place, guest commits before body/camera/pose publication; room reset and peer departure release reservations. Root owns v11 protocol/inactivity composition and native acceptance.
