using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using ReactionTransportReload=UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using ReactionTransportReload=System.Action;
#endif

namespace OdysseyOracle
{
    public sealed partial class RoomConnection
    {
        private const string RoomReactionChannel="odyssey.refuge.reactions.v1";
        private const int RoomReactionBytes=112;
        private FastBufferWriter roomReactionWriter;
        private bool roomReactionWriterReady,roomReactionWriterSubscribed;
        private ReactionTransportReload roomReactionWriterReload;
        private uint roomReactionSentRevision,roomReactionSentGeneration;
        private double roomReactionNextSend;
        internal uint RoomReactionEpoch=>!Connected||!hubApplied?0:IsHost?hubEpoch:hubLocalGrant.Epoch;
        private void RegisterRoomReactionHandler()
        {
            network.CustomMessagingManager.RegisterNamedMessageHandler(RoomReactionChannel,ReceiveRoomReactionFrame);
            network.CustomMessagingManager.RegisterNamedMessageHandler(RefugeFlameEventChannel020,ReceiveRefugeFlameEvent020);
            EnsureRoomReactionWriter();
        }
        private void EnsureRoomReactionWriter()
        {
            if(roomReactionWriterReady)return;roomReactionWriter=new FastBufferWriter(RoomReactionBytes,Allocator.Persistent,RoomReactionBytes);roomReactionWriterReady=true;
            if(roomReactionWriterReload==null)roomReactionWriterReload=ReleaseRoomReactionTransport;
            if(!roomReactionWriterSubscribed){LightPoolLifecycle.SubscribeBeforeReload(roomReactionWriterReload);roomReactionWriterSubscribed=true;}
        }
        private void TickRoomReactionTransport()
        {
            if(!isActiveAndEnabled||!Connected||!IsHost||!hubApplied||network==null)return;
            var world=GameSession.Instance!=null?GameSession.Instance.World:null;
            if(world==null||!world.RoomReactionReady)return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(world.RoomReactionRevision==roomReactionSentRevision&&world.RoomReactionSceneGeneration==roomReactionSentGeneration&&now<roomReactionNextSend)return;
            if(!world.CaptureRoomReactions(RoomReactionEpoch,out var frame))return;
            EnsureRoomReactionWriter();roomReactionWriter.Seek(0);roomReactionWriter.Truncate(0);
            WriteRoomReactionFrame(ref roomReactionWriter,frame);
            if(roomReactionWriter.Length!=RoomReactionBytes)throw new System.InvalidOperationException("Refuge reaction frame size changed.");
            Broadcast(RoomReactionChannel,roomReactionWriter,NetworkDelivery.ReliableSequenced);
            roomReactionSentRevision=frame.Revision;roomReactionSentGeneration=frame.Generation;roomReactionNextSend=now+.5d;
        }
        private static void WriteRoomReactionFrame(ref FastBufferWriter writer,in RoomReactionFrame112 frame)
        {
            writer.WriteValueSafe(frame.Magic);writer.WriteValueSafe(frame.Version);writer.WriteValueSafe(frame.Epoch);writer.WriteValueSafe(frame.Generation);writer.WriteValueSafe(frame.Revision);writer.WriteValueSafe(frame.Count);
            writer.WriteValueSafe(frame.LayoutHash);writer.WriteValueSafe(frame.Bits0);writer.WriteValueSafe(frame.Bits1);writer.WriteValueSafe(frame.Bits2);writer.WriteValueSafe(frame.Bits3);
            writer.WriteValueSafe(frame.Bits4);writer.WriteValueSafe(frame.Bits5);writer.WriteValueSafe(frame.Bits6);writer.WriteValueSafe(frame.Bits7);
            writer.WriteValueSafe(frame.PlantStage);writer.WriteValueSafe(frame.PlantStartTick);writer.WriteValueSafe(frame.ServerTick);writer.WriteValueSafe(frame.Reserved);
        }
        private void ReceiveRoomReactionFrame(ulong sender,FastBufferReader reader)
        {
            if(!isActiveAndEnabled||!Connected||IsHost||!hubApplied||network==null||sender!=NetworkManager.ServerClientId||reader.Length!=RoomReactionBytes||!reader.TryBeginRead(RoomReactionBytes))return;
            RoomReactionFrame112 f=default;
            reader.ReadValueSafe(out f.Magic);reader.ReadValueSafe(out f.Version);reader.ReadValueSafe(out f.Epoch);reader.ReadValueSafe(out f.Generation);reader.ReadValueSafe(out f.Revision);reader.ReadValueSafe(out f.Count);
            reader.ReadValueSafe(out f.LayoutHash);reader.ReadValueSafe(out f.Bits0);reader.ReadValueSafe(out f.Bits1);reader.ReadValueSafe(out f.Bits2);reader.ReadValueSafe(out f.Bits3);
            reader.ReadValueSafe(out f.Bits4);reader.ReadValueSafe(out f.Bits5);reader.ReadValueSafe(out f.Bits6);reader.ReadValueSafe(out f.Bits7);
            reader.ReadValueSafe(out f.PlantStage);reader.ReadValueSafe(out f.PlantStartTick);reader.ReadValueSafe(out f.ServerTick);reader.ReadValueSafe(out f.Reserved);
            var world=GameSession.Instance!=null?GameSession.Instance.World:null;world?.ApplyRoomReactions(f,RoomReactionEpoch);
        }
        private void ReleaseRoomReactionTransport()
        {
            if(roomReactionWriterReady){roomReactionWriter.Dispose();roomReactionWriterReady=false;}
            if(roomReactionWriterSubscribed&&roomReactionWriterReload!=null)LightPoolLifecycle.UnsubscribeBeforeReload(roomReactionWriterReload);roomReactionWriterSubscribed=false;
            roomReactionSentRevision=roomReactionSentGeneration=0;roomReactionNextSend=0;
            GameSession.Instance?.World?.ClearRoomReactionsOnDetach();
        }
        private void OnDisable(){ReleaseXpTransport015();ReleaseArenaImpactTransport();ReleaseRoomReactionTransport();}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Transport-only current accepted shared-refuge epoch; host numeric state uses existing World coordinates and layout fingerprint, no pose/physics authority.
// Array Formats: Exact112-byte explicit-field frame, one Persistent writer reused via Seek/Truncate and disposed on disable/detach;8lit words, growth/revision/generation, no JSON/string/byte-array allocation per message.
// Dependencies/Pointers: Only actual current server may publish; clients cannot submit reaction targets. Existing room membership/hub spawn protects scope, World validates count/hash/revision/reserved. Native two-peer/transport lifetime acceptance remains root-owned.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: host accepted World contacts and existing run/epoch/shot identity; no damage/HP/path permission changes.
// Array Formats: existing bounded launch receipts add three-bit landing dedupe; fixed256B shared wire unchanged; new64B arena frame through existing NGO pipeline.
// Dependencies/Pointers: sole host/network/audio owners retain authority, lifetime and gesture/focus policies; actual peer/audio acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world and accepted015 campaign/source identity, original arena positions retained.
// Array Formats: Eight milestones/40waves/905logical rewards within4096native slots, fixed128B XP channel and existing bounded pools.
// Dependencies/Pointers: Root composes actual current pool/audio owners with reviewed campaign/XP hunks; source/native/browser/performance/public acceptance remain separate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: same current accepted shared-refuge epoch.
// Array Formats: Frame112 unchanged; new56B reliable event reuses the existing bounded writer and normal callback lifetime.
// Dependencies/Pointers: existing server-only reaction transport remains sole authority; no event-to-target mutation or snapshot-based achievement, exact source/server/epoch/layout/receipt guards remain.
