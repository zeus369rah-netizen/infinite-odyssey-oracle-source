using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RoomConnection
    {
        const string RefugeFlameEventChannel020="odyssey.refuge.flames.v1";
        const int RefugeFlameEventBytes020=56;
        internal bool PublishRefugeFlameEvent020(DungeonWorld source,in RefugeFlameEvent56 frame)
        {
            if(!isActiveAndEnabled||!Connected||!IsHost||!hubApplied||network==null||source==null||source!=GameSession.Instance?.World||
                frame.Epoch!=RoomReactionEpoch||frame.Epoch==0||!IsCampaignMemberRegistered(frame.Caster))return false;
            if(!source.ReceiveRefugeFlameEvent020(frame,RoomReactionEpoch,LocalClientId))return false;
            EnsureRoomReactionWriter();roomReactionWriter.Seek(0);roomReactionWriter.Truncate(0);
            roomReactionWriter.WriteValueSafe(frame.Epoch);roomReactionWriter.WriteValueSafe(frame.Generation);roomReactionWriter.WriteValueSafe(frame.Revision);roomReactionWriter.WriteValueSafe(frame.Shot);
            roomReactionWriter.WriteValueSafe(frame.Caster);roomReactionWriter.WriteValueSafe(frame.LayoutHash);
            roomReactionWriter.WriteValueSafe(frame.Position.x);roomReactionWriter.WriteValueSafe(frame.Position.y);roomReactionWriter.WriteValueSafe(frame.Position.z);
            roomReactionWriter.WriteValueSafe(frame.Extinguished);roomReactionWriter.WriteValueSafe(frame.EligibleCount);roomReactionWriter.WriteValueSafe(frame.Flags);
            if(roomReactionWriter.Length!=RefugeFlameEventBytes020)throw new System.InvalidOperationException("Refuge flame event byte contract changed.");
            Broadcast(RefugeFlameEventChannel020,roomReactionWriter,NetworkDelivery.ReliableSequenced);return true;
        }
        void ReceiveRefugeFlameEvent020(ulong sender,FastBufferReader reader)
        {
            if(!isActiveAndEnabled||!Connected||IsHost||!hubApplied||network==null||sender!=NetworkManager.ServerClientId||
                reader.Length!=RefugeFlameEventBytes020||!reader.TryBeginRead(RefugeFlameEventBytes020))return;
            RefugeFlameEvent56 frame=default;
            reader.ReadValueSafe(out frame.Epoch);reader.ReadValueSafe(out frame.Generation);reader.ReadValueSafe(out frame.Revision);reader.ReadValueSafe(out frame.Shot);
            reader.ReadValueSafe(out frame.Caster);reader.ReadValueSafe(out frame.LayoutHash);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);frame.Position=new float3(x,y,z);
            reader.ReadValueSafe(out frame.Extinguished);reader.ReadValueSafe(out frame.EligibleCount);reader.ReadValueSafe(out frame.Flags);
            if(frame.Epoch==0||frame.Epoch!=RoomReactionEpoch||!IsCampaignMemberRegistered(frame.Caster))return;
            GameSession.Instance?.World?.ReceiveRefugeFlameEvent020(frame,RoomReactionEpoch,LocalClientId);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing current accepted hub epoch, host generation/revision/shot/caster and one world-space contact; event is presentation/profile fact only.
// Array Formats: explicit56B reliable event reuses the existing112B-capacity Persistent writer via Seek/Truncate; no new writer, queue, strings, byte arrays or state snapshot format.
// Dependencies/Pointers: only actual current server/current member may publish/receive; World validates layout/count/flags/finite contact and monotonic receipt. Event never mutates targets; original Frame112 serializer/validation unchanged. Root real two-peer/audio/persistence/lifetime acceptance pending.
