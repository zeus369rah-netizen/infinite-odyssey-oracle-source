using System;
using System.Threading.Tasks;
using Unity.Netcode;

namespace OdysseyOracle
{
    public sealed partial class RoomConnection
    {
        internal event Action<ulong> UnexpectedSoloRestartPeerDeparture;
        internal event Action UnexpectedSoloRestartConnectionLoss;
        bool soloRestartConnecting;
        int soloRestartConnectingOperation;
        internal int SoloRestartOperation=>operation;
        internal bool SoloRestartTransportQuiescent=>!destroyed&&!Connected&&!Busy&&!leaving&&pendingConnections==0&&
            session==null&&network==null&&!ownsNetwork&&pendingIdleHostDeletion==null&&
            (ownedLeaveTask==null||ownedLeaveTask.IsCompleted)&&(disconnectTask==null||disconnectTask.IsCompleted)&&NetworkManager.Singleton==null;
        internal bool TryBeginOrReuseSoloRestartLeave(SoloStageRestartTicket ticket,bool unexpectedLossStartedLeave,out Task<string> cleanup)
        {
            cleanup=null;
            if(!ticket.Valid)return false;
            if(operation==ticket.SourceRoomOperation){cleanup=LeaveRoomAsync();return true;}
            // A fast owned loss cleanup may already have completed before the next Update.
            // Reuse that exact task rather than starting a second leave/generation.
            if(ticket.CanReuseOwnedLossCleanup(operation,unexpectedLossStartedLeave,ownedLeaveTask!=null)){cleanup=ownedLeaveTask;return true;}
            return false;
        }
        internal void CreateSoloRestartRoom()
        {
            if(!SoloRestartTransportQuiescent)throw new InvalidOperationException("The old room/owned manager is not fully released.");
            if(operation==int.MaxValue)throw new InvalidOperationException("Room operation sequence exhausted.");
            soloRestartConnectingOperation=operation+1;soloRestartConnecting=true;CreateSoloRoom();
        }
        internal bool CompleteSoloRestartConnection(GameSession game)
        {
            if(!soloRestartConnecting)return false;
            if(operation!=soloRestartConnectingOperation){soloRestartConnecting=false;soloRestartConnectingOperation=0;return false;}
            if(game==null||!Connected||!IsHost||PlayerCount!=1||!TryGetAdmissionSlot(LocalClientId,out int slot)||slot!=0)
                throw new InvalidOperationException("The recovery room did not admit exactly one local host at slot0.");
            soloRestartConnecting=false;game.BeginSoloRestartConnection();return true;
        }
        internal void CancelSoloRestartConnection(int expectedOperation,bool leaveOwned=true)
        {
            if(soloRestartConnectingOperation==expectedOperation){soloRestartConnecting=false;soloRestartConnectingOperation=0;}
            if(leaveOwned&&operation==expectedOperation)_ = LeaveRoomAsync();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing owned room/NGO operation generations; no actor positions.
// Array Formats: cold event hooks/one connection flag; existing Task/manager/session ownership remains sole cleanup authority.
// Dependencies/Pointers: full SDK leave/owned manager destruction and null singleton fence precede CreateSoloRoom; no competing authority, target switch or arbitrary transport shutdown.
