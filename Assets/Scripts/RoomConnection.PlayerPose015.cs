using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class RoomConnection
    {
        RemoteOracleAvatar preparedPlayer015;bool player015Leased;
        void PrepareRemotePlayerCold015()
        {
            if(preparedPlayer015!=null)return;
            var node=new GameObject("Prewarmed remote Oracle015");node.transform.SetParent(transform,false);node.SetActive(false);
            try{preparedPlayer015=node.AddComponent<RemoteOracleAvatar>();preparedPlayer015.Initialize(ulong.MaxValue,Vector3.zero,0);preparedPlayer015.PreparePlayerPoseCold015();}
            catch{Destroy(node);preparedPlayer015=null;throw;}
        }
        RemoteOracleAvatar RentRemotePlayer015(ulong id,Vector3 position,float yaw)
        {
            if(preparedPlayer015==null||player015Leased||network==null||id==network.LocalClientId)return null;
            player015Leased=true;preparedPlayer015.BindPreparedPlayer015(id,position,yaw);return preparedPlayer015;
        }
        void ReturnRemotePlayer015(RemoteOracleAvatar avatar)
        {
            if(avatar==null)return;
            if(avatar==preparedPlayer015){avatar.RetirePreparedPlayer015();player015Leased=false;}else Destroy(avatar.gameObject);
        }
        internal void PresentAcceptedPlayerCast015(ulong id)
        {
            if(network==null||!hubApplied||id==network.LocalClientId)return;
            if(avatars.TryGetValue(id,out var avatar)&&avatar!=null)avatar.PulseAcceptedPlayer015();
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted peer world pose before visual visibility; no camera/controller root owned here.
// Array Formats: Exactly1cold-prepared remote visual for actual2peer cap; original avatar dictionary stores its lease; no new protocol/records.
// Dependencies/Pointers: Existing admission and validated launch consumers alone rent/notify; hot callbacks cannot create graphs/meshes. Child owner disposes with Room GameObject.
