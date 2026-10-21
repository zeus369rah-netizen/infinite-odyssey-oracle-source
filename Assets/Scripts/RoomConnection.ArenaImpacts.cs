using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using ImpactTransportReload=UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using ImpactTransportReload=System.Action;
#endif
namespace OdysseyOracle
{
    public sealed partial class RoomConnection
    {
        const string ArenaImpactChannel="odyssey.arena.impact.v1";
        const int ArenaImpactBytes=64;
        FastBufferWriter arenaImpactWriter;bool arenaImpactWriterReady,arenaImpactWriterSubscribed,arenaImpactSending,arenaImpactReleasePending;
        ImpactTransportReload arenaImpactReload;
        double arenaImpactSendWindow,arenaImpactReceiveWindow;int arenaImpactSent,arenaImpactReceived;
        internal event Action<ulong,ArenaImpactFrame64> ArenaImpactMessage;
        void EnsureArenaImpactWriter()
        {
            if(arenaImpactWriterReady)return;
            if(!BitConverter.IsLittleEndian||Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<ArenaImpactFrame64>()!=ArenaImpactBytes)
                throw new InvalidOperationException("Arena impact64B ABI is unsupported.");
            arenaImpactWriter=new FastBufferWriter(ArenaImpactBytes,Allocator.Persistent,ArenaImpactBytes);arenaImpactWriterReady=true;
            // Prime the known unmanaged generic serializer only at this cold lifetime boundary.
            arenaImpactWriter.WriteValueSafe(default(ArenaImpactFrame64));arenaImpactWriter.Seek(0);arenaImpactWriter.Truncate(0);
            if(arenaImpactReload==null)arenaImpactReload=ReleaseArenaImpactTransport;
            if(!arenaImpactWriterSubscribed){LightPoolLifecycle.SubscribeBeforeReload(arenaImpactReload);arenaImpactWriterSubscribed=true;}
        }
        void RegisterArenaImpactHandler()
        {
            EnsureArenaImpactWriter();
            try{network.CustomMessagingManager.RegisterNamedMessageHandler(ArenaImpactChannel,ReceiveArenaImpact);}
            catch{ReleaseArenaImpactTransport();throw;}
        }
        void OnEnable(){if(handlersRegistered&&network!=null){EnsureArenaImpactWriter();EnsureXpWriter015();}}
        internal bool SendArenaImpact(in ArenaImpactFrame64 frame,ulong recipient)
        {
            if(!isActiveAndEnabled||!Connected||!IsHost||network==null||!network.IsServer||network.CustomMessagingManager==null||
                !arenaImpactWriterReady||arenaImpactSending||recipient==LocalClientId||!IsConnectedPeer(recipient)||!ArenaImpactFrameRules.Shape(frame))return false;
            double now=Time.realtimeSinceStartupAsDouble;
            if(now-arenaImpactSendWindow>=1){arenaImpactSendWindow=now;arenaImpactSent=0;}
            if(arenaImpactSent>=32)return false;
            arenaImpactSending=true;
            try
            {
                arenaImpactWriter.Seek(0);arenaImpactWriter.Truncate(0);arenaImpactWriter.WriteValueSafe(frame);
                if(arenaImpactWriter.Length!=ArenaImpactBytes)return false;
                // Same existing reliable fragmented/sequenced pipeline as accepted cast JSON:
                // casts and their later impacts retain transport order without a second socket.
                network.CustomMessagingManager.SendNamedMessage(ArenaImpactChannel,recipient,arenaImpactWriter,NetworkDelivery.ReliableFragmentedSequenced);
                arenaImpactSent++;return true;
            }
            finally{arenaImpactSending=false;if(arenaImpactReleasePending)ReleaseArenaImpactTransport();}
        }
        void ReceiveArenaImpact(ulong sender,FastBufferReader reader)
        {
            if(!isActiveAndEnabled||!Connected||IsHost||network==null||sender!=NetworkManager.ServerClientId||
                reader.Length!=ArenaImpactBytes||!reader.TryBeginRead(ArenaImpactBytes))return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(now-arenaImpactReceiveWindow>=1){arenaImpactReceiveWindow=now;arenaImpactReceived=0;}
            if(arenaImpactReceived>=32)return;
            reader.ReadValueSafe(out ArenaImpactFrame64 frame);if(!ArenaImpactFrameRules.Shape(frame))return;
            arenaImpactReceived++;ArenaImpactMessage?.Invoke(sender,frame);
        }
        void ReleaseArenaImpactTransport()
        {
            if(arenaImpactSending){arenaImpactReleasePending=true;return;}
            arenaImpactReleasePending=false;
            if(arenaImpactWriterReady){arenaImpactWriter.Dispose();arenaImpactWriterReady=false;}
            if(arenaImpactWriterSubscribed&&arenaImpactReload!=null)LightPoolLifecycle.UnsubscribeBeforeReload(arenaImpactReload);
            arenaImpactWriterSubscribed=arenaImpactSending=false;arenaImpactSent=arenaImpactReceived=0;arenaImpactSendWindow=arenaImpactReceiveWindow=0;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing authenticated NGO Room/server only,64B actual host landing data; no new socket/session/input/physics authority.
// Array Formats: one Persistent fixed64B FastBufferWriter allocated/primed cold and reused via Seek/Truncate; max32frames/sec/direction, no per-impact JSON/string/byte-array/temporary writer.
// Dependencies/Pointers: Room registers/unregisters named handler and releases writer on disable/detach/before reload; OnEnable cold re-primes existing-handler lifetime. Send uses same ordered reliable pipeline as old accepted casts; framework/native/browser allocation/order/audio proof remains root-owned.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world and accepted015 campaign/source identity, original arena positions retained.
// Array Formats: Eight milestones/40waves/905logical rewards within4096native slots, fixed128B XP channel and existing bounded pools.
// Dependencies/Pointers: Root composes actual current pool/audio owners with reviewed campaign/XP hunks; source/native/browser/performance/public acceptance remain separate.
