using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using OdysseyOracle.Foundation.World;
#if UNITY_EDITOR
using LaneBuildReload015=UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using LaneBuildReload015=System.Action;
#endif

namespace OdysseyOracle
{
    public sealed partial class RoomConnection
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential,Pack=4)]
        struct LaneBuildWire64:INetworkSerializeByMemcpy { public LaneBuildFrame64 Data; }
        const string LaneBuildChannel015="odyssey.build.binary.015.v1";
        FastBufferWriter laneBuildWriter015;bool laneBuildWriterReady015,laneBuildSending015,laneBuildReleasePending015;
        LaneBuildReload015 laneBuildReload015;double laneBuildSendWindow015,laneBuildReceiveWindow015;int laneBuildSent015,laneBuildReceived015;
        internal event Action<ulong,LaneBuildFrame64> LaneBuildMessage015;
        public static bool VerifyLaneBuildCodec015(out int bytes)
        {
            bytes=0;
            using(var writer=new FastBufferWriter(64,Allocator.Temp,64))
            {
                for(uint kind=1;kind<=7;kind++)
                {
                    var frame=new LaneBuildFrame64{Version=1,Kind=kind,RunA=0x0102030405060708ul,RunB=0x1112131415161718ul,Epoch=13,Nonce=7,Lane=7,Semantic=0x12345678u,Context=kind==1?0u:7u};
                    if(kind==4||kind==5){frame.TowerId=65535;frame.TowerRevision=1;}
                    if(!LaneBuildMath015.FrameShape(frame))return false;
                    writer.Seek(0);writer.Truncate(0);writer.WriteValueSafe(new LaneBuildWire64{Data=frame});bytes=writer.Length;if(bytes!=64)return false;
                    using(var reader=new FastBufferReader(writer,Allocator.Temp))
                    {
                        reader.ReadValueSafe(out LaneBuildWire64 wire);var r=wire.Data;
                        if(r.Version!=frame.Version||r.Kind!=frame.Kind||r.RunA!=frame.RunA||r.RunB!=frame.RunB||r.Epoch!=frame.Epoch||r.Nonce!=frame.Nonce||r.Cell!=frame.Cell||r.Element!=frame.Element||r.TowerId!=frame.TowerId||r.TowerRevision!=frame.TowerRevision||r.Lane!=frame.Lane||r.Semantic!=frame.Semantic||r.Result!=frame.Result||r.Context!=frame.Context)return false;
                    }
                }
            }
            return true;
        }
        void RegisterLaneBuildHandler015()
        {
            if(!laneBuildWriterReady015)
            {
                if(!BitConverter.IsLittleEndian||Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<LaneBuildFrame64>()!=64||Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<LaneBuildWire64>()!=64)throw new InvalidOperationException("Lane builder requires its exact64byte ABI.");
                laneBuildWriter015=new FastBufferWriter(64,Allocator.Persistent,64);laneBuildWriter015.WriteValueSafe(default(LaneBuildWire64));laneBuildWriter015.Seek(0);laneBuildWriter015.Truncate(0);
                laneBuildWriterReady015=true;laneBuildReload015=ReleaseLaneBuildTransport015;LightPoolLifecycle.SubscribeBeforeReload(laneBuildReload015);
            }
            try{network.CustomMessagingManager.RegisterNamedMessageHandler(LaneBuildChannel015,ReceiveLaneBuild015);}catch{ReleaseLaneBuildTransport015();throw;}
        }
        internal bool SendLaneBuild015(in LaneBuildFrame64 frame,ulong recipient)
        {
            bool response=frame.Kind==LaneBuildMath015.Context||frame.Kind==LaneBuildMath015.Receipt;
            if(!isActiveAndEnabled||!Connected||network==null||network.CustomMessagingManager==null||!laneBuildWriterReady015||laneBuildSending015||
                recipient==LocalClientId||!LaneBuildMath015.FrameShape(frame)||(IsHost?!response||!IsConnectedPeer(recipient):response||recipient!=NetworkManager.ServerClientId))return false;
            double now=Time.realtimeSinceStartupAsDouble;if(now-laneBuildSendWindow015>=1){laneBuildSendWindow015=now;laneBuildSent015=0;}if(laneBuildSent015>=8)return false;
            laneBuildSending015=true;
            try{laneBuildWriter015.Seek(0);laneBuildWriter015.Truncate(0);laneBuildWriter015.WriteValueSafe(new LaneBuildWire64{Data=frame});if(laneBuildWriter015.Length!=64)return false;
                network.CustomMessagingManager.SendNamedMessage(LaneBuildChannel015,recipient,laneBuildWriter015,NetworkDelivery.ReliableSequenced);laneBuildSent015++;return true;}
            finally{laneBuildSending015=false;if(laneBuildReleasePending015)ReleaseLaneBuildTransport015();}
        }
        void ReceiveLaneBuild015(ulong sender,FastBufferReader reader)
        {
            if(!isActiveAndEnabled||!Connected||network==null||reader.Length!=64||!reader.TryBeginRead(64)||(IsHost?sender==LocalClientId||!IsConnectedPeer(sender):sender!=NetworkManager.ServerClientId))return;
            double now=Time.realtimeSinceStartupAsDouble;if(now-laneBuildReceiveWindow015>=1){laneBuildReceiveWindow015=now;laneBuildReceived015=0;}if(laneBuildReceived015>=8)return;
            reader.ReadValueSafe(out LaneBuildWire64 wire);LaneBuildFrame64 frame=wire.Data;if(!LaneBuildMath015.FrameShape(frame))return;
            bool response=frame.Kind==LaneBuildMath015.Context||frame.Kind==LaneBuildMath015.Receipt;if(IsHost?response:!response)return;
            laneBuildReceived015++;LaneBuildMessage015?.Invoke(sender,frame);
        }
        void ReleaseLaneBuildTransport015()
        {
            if(laneBuildSending015){laneBuildReleasePending015=true;return;}laneBuildReleasePending015=false;
            if(laneBuildWriterReady015){laneBuildWriter015.Dispose();laneBuildWriterReady015=false;LightPoolLifecycle.UnsubscribeBeforeReload(laneBuildReload015);}
            laneBuildSent015=laneBuildReceived015=0;laneBuildSendWindow015=laneBuildReceiveWindow015=0;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing authenticated NGO room, exact128bit current run plus epoch/context; no new sockets or client authority.
// Array Formats: One persistent64B writer, validated fixed Frame64 and bounded8frames/sec/direction; no hot JSON or temporary payload arrays.
// Dependencies/Pointers: Room owns handler/writer/reload cleanup and deferred release during send; Director validates semantics and recipient ownership. Existing XP/lab ABIs and protocols are preserved separately.
