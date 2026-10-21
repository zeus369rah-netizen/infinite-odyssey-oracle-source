using System;
using Unity.Collections;
using Unity.Netcode;

namespace OdysseyOracle
{
    public sealed partial class RoomConnection
    {
        const string SharedLabChannel="odyssey.lab.v3";
        internal event Action<ulong,SharedLabFrame> SharedLabMessage;
        internal bool SendSharedLab(SharedLabFrame frame,ulong recipient)
        {
            if(!Connected||network==null||!network.IsConnectedClient||frame.Version!=SharedLabFrame.LabVersion)return false;
            if(network.IsServer)
            {
                if(!IsConnectedPeer(recipient))return false;
                if(recipient==LocalClientId){SharedLabMessage?.Invoke(LocalClientId,frame);return true;}
            }
            else if(recipient!=NetworkManager.ServerClientId)return false;
            using(var writer=new FastBufferWriter(256,Allocator.Temp))
            {writer.WriteValueSafe(frame);network.CustomMessagingManager.SendNamedMessage(SharedLabChannel,recipient,writer,NetworkDelivery.ReliableSequenced);}
            return true;
        }
        void ReceiveSharedLab(ulong sender,FastBufferReader reader)
        {
            if(!Connected||network==null||reader.Length!=256||!reader.TryBeginRead(256)||
                (network.IsServer?!IsConnectedPeer(sender):sender!=NetworkManager.ServerClientId))return;
            try{reader.ReadValueSafe(out SharedLabFrame frame);if(frame.Version==SharedLabFrame.LabVersion)SharedLabMessage?.Invoke(sender,frame);}
            catch(OverflowException){}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Authenticated existing NGO sender identity; no request-supplied identity accepted.
// Array Formats: Exactly256B unmanaged frame, bounded native temporary writer; no string/JSON/boxing or retained reader ownership.
// Dependencies/Pointers: Existing Room owns registration/session lifetime; guest accepts server only, host accepts connected peer only; shared owner validates epoch/admission/rates before mutations.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: authenticated existing NGO room and Y-up lab world; no new physics or gameplay authority.
// Array Formats: fixed256-byte LabVersion3 frame, word224 LiveStatus only on State; fixed64-byte arena impact event.
// Dependencies/Pointers: exact room tl3/rc1/ls1/bi1 capability gate and v2 lab channel reject older peers; requests and Impact require zero LiveStatus; root native/two-peer acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.
