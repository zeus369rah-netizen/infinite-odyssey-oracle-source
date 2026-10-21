using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleCombat
    {
        private uint roomReactionFlight;
        internal uint NextRoomReactionFlight(){roomReactionFlight++;if(roomReactionFlight==0)roomReactionFlight=1;return roomReactionFlight;}
        private bool RoomReactionAuthority(OracleProjectile.Kind kind,int combo,bool nova,int mask,int networkShot,uint flight,out ulong caster,out uint ticket)
        {
            caster=0;ticket=flight;
            if(!Playing||world==null||!world.IsTutorialRefuge||world.IsCoopArena||combo!=0||nova||!ElementalRoomReactionState.Single(mask)||
                (int)kind!=(int)OracleProjectile.Kind.Fire+(mask==1?0:mask==4?2:3)||flight==0)return false;
            var session=GameSession.Instance;var rooms=session!=null?session.Rooms:null;
            if(rooms==null||!rooms.Connected)return networkShot==0;
            if(!rooms.IsHost||session.SharedTrainingLab==null||networkShot<1||!session.SharedTrainingLab.TryGetAcceptedShot(networkShot,out caster,out int acceptedMask)||acceptedMask!=mask)return false;
            ticket=(uint)networkShot;return rooms.IsCampaignMemberRegistered(caster);
        }
        internal bool TryRoomReactionProjectile(OracleProjectile.Kind kind,int combo,bool nova,int mask,int networkShot,uint flight,Vector3 origin,Vector3 direction,float distance,float radius,out float contact)
        {
            contact=distance;return RoomReactionAuthority(kind,combo,nova,mask,networkShot,flight,out _,out _)&&world.TryRoomReactionContact(mask,origin,direction,distance,radius,out contact);
        }
        internal void NotifyRoomReactionImpact(OracleProjectile.Kind kind,int combo,bool nova,int mask,int networkShot,uint flight,Vector3 point)
        {
            if(RoomReactionAuthority(kind,combo,nova,mask,networkShot,flight,out ulong caster,out uint ticket))world.AcceptRoomReactionHit(caster,ticket,mask,point);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted projectile world origin/direction/contact; environmental query stops before normal world obstruction and never changes damage/lesson contact ownership.
// Array Formats: One accepted-flight serial; shared caster/mask comes read-only from existing48-shot SharedTrainingLab receipts. Numeric World store deduplicates actual accepted impacts; no hot arrays/Objects or client state payload.
// Dependencies/Pointers: Existing Playing/recovery/accepted-launch host remains canonical; single Fire1/Earth4/Wind8 only, no pairs/Nova. No XP, HP, reward, casting permission, quest or saved state mutation.
