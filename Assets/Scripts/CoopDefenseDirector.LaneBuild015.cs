using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Netcode;
using OdysseyOracle.Foundation.World;

namespace OdysseyOracle
{
    public sealed partial class CoopDefenseDirector
    {
        uint laneLocalNonce015;CharacterController lanePlayerBody015;
        LaneBuildPending96 lanePending015;
        ShrinePlacementBuilder lanePendingBuilder015;
        bool BeginLaneRequest015(in LaneBuildFrame64 frame)
        {
            if(!Running||rooms==null||!rooms.Connected||owner==null||owner.Builder==null||!owner.Builder.isActiveAndEnabled||
                !LaneBuildDelivery015.Begin(ref lanePending015,frame,Time.realtimeSinceStartupAsDouble))return false;
            lanePendingBuilder015=owner.Builder; // Capture before a synchronous solo-host reply.
            TickLaneRequest015(false);return true; // Queued, never an accepted gameplay outcome.
        }
        internal void ClearLaneRequest015(){LaneBuildDelivery015.Clear(ref lanePending015);lanePendingBuilder015=null;}
        internal void TickLaneRequest015(bool cancel)
        {
            if(lanePending015.Active==0)return;var frame=lanePending015.Command;
            bool current=Running&&rooms!=null&&rooms.Connected&&owner!=null&&owner.Builder!=null&&owner.Builder.isActiveAndEnabled&&ReferenceEquals(owner.Builder,lanePendingBuilder015)&&owner.World!=null&&
                LaneBuildMath015.RunKey(runId,out ulong a,out ulong b)&&a==frame.RunA&&b==frame.RunB&&LaneBuildEpoch015==frame.Epoch&&
                owner.World.LaneBuildSemantic015==frame.Semantic&&frame.Lane>=0&&(admissionLaneMask&(1<<frame.Lane))!=0;
            double now=Time.realtimeSinceStartupAsDouble;bool expired=LaneBuildDelivery015.Expired(lanePending015,now);
            if(cancel||!current||expired)
            {var builder=lanePendingBuilder015;ClearLaneRequest015();if(builder!=null&&builder.isActiveAndEnabled&&owner!=null&&ReferenceEquals(owner.Builder,builder))builder.OnLaneRequestEnded015(expired&&!cancel&&current);return;}
            if(LaneBuildDelivery015.TakeDue(ref lanePending015,now,out var command))DispatchLaneCommand015(command);
        }
        void CompleteLaneRequest015(in LaneBuildFrame64 response)
        {
            if(owner==null||owner.Builder==null||!owner.Builder.isActiveAndEnabled||!ReferenceEquals(owner.Builder,lanePendingBuilder015))
            {ClearLaneRequest015();return;} // Never invoke a disabled/replaced lifetime's UI callback.
            if(!LaneBuildDelivery015.Matches(lanePending015,response))return;
            bool expired=LaneBuildDelivery015.Expired(lanePending015,Time.realtimeSinceStartupAsDouble);
            if(expired||!Application.isFocused||AbilityBindings.IsRebinding)
            {ClearLaneRequest015();owner.Builder?.OnLaneRequestEnded015(expired);return;}
            ClearLaneRequest015(); // Terminal before a synchronous UI/cancel callback.
            owner.Builder?.ApplyLaneResponse015(response);
        }
        int LaneBuildEpoch015=>CampaignActive?campaign.Epoch:0;
        internal int CurrentLaneBuildEpoch015=>LaneBuildEpoch015;
        internal uint NextLaneBuildNonce015(){if(laneLocalNonce015==uint.MaxValue)return 0;return ++laneLocalNonce015;}
        internal bool LocalCanBuildLaneType015(int type)=>rooms!=null&&Running&&TowerForgeDefinitions.ValidType(type)&&(!CampaignActive||campaign.CanBuildTower(rooms.LocalClientId,type));
        LaneBuildFrame64 LaneCommand015(uint kind,uint nonce,uint context,int lane,uint semantic,uint cell=0,int element=0,int tower=0,int revision=0)
        {
            LaneBuildMath015.RunKey(runId,out ulong a,out ulong b);
            return new LaneBuildFrame64{Version=LaneBuildMath015.Version,Kind=kind,RunA=a,RunB=b,Epoch=LaneBuildEpoch015,Nonce=nonce,Context=context,Lane=lane,Semantic=semantic,Cell=cell,Element=element,TowerId=tower,TowerRevision=revision};
        }
        internal bool RequestLaneOpen015(uint nonce,int lane,int tower,int revision)
        {return BeginLaneRequest015(LaneCommand015(LaneBuildMath015.Open,nonce,0,lane,owner.World.LaneBuildSemantic015,0,0,tower,revision));}
        internal bool RequestLaneAction015(uint kind,uint context,int lane,uint semantic,Vector3 point,int element,int tower,int revision)
        {
            if(lanePending015.Active!=0)return false;uint nonce=NextLaneBuildNonce015();if(nonce==0||!LaneBuildMath015.Canonical(new float3(point.x,point.y,point.z),out uint cell))return false;
            return BeginLaneRequest015(LaneCommand015(kind,nonce,context,lane,semantic,cell,element,tower,revision));
        }
        internal void RequestLaneClose015(uint context,int lane,uint semantic)
        {ClearLaneRequest015();uint nonce=NextLaneBuildNonce015();if(context!=0&&nonce!=0)DispatchLaneCommand015(LaneCommand015(LaneBuildMath015.Close,nonce,context,lane,semantic));}
        bool DispatchLaneCommand015(in LaneBuildFrame64 frame)
        {
            if(!Running||rooms==null||!rooms.Connected||!LaneBuildMath015.FrameShape(frame))return false;
            if(IsAuthority){OnLaneBuildFrame015(rooms.LocalClientId,frame);return true;}
            return rooms.SendLaneBuild015(frame,NetworkManager.ServerClientId);
        }
        void OnLaneBuildFrame015(ulong sender,LaneBuildFrame64 frame)
        {
            if(!Running||rooms==null||!LaneBuildMath015.FrameShape(frame)||!LaneBuildMath015.RunKey(runId,out ulong a,out ulong b)||frame.RunA!=a||frame.RunB!=b||frame.Epoch!=LaneBuildEpoch015)return;
            if(!IsAuthority){CompleteLaneRequest015(frame);return;}
            if(!peers.TryGetValue(sender,out var peer)||!peer.ArenaReady)return;
            if(frame.Nonce<=peer.LaneLastNonce015){if(frame.Nonce==peer.LaneLastNonce015&&LaneBuildDelivery015.Same(frame,peer.LaneRequest015))ReplyLane015(sender,peer.LaneResponse015);return;}
            peer.LaneLastNonce015=frame.Nonce;peer.LaneRequest015=frame;
            LaneBuildReason015 result=LaneBuildReason015.Unavailable;
            var reply=frame;reply.Kind=frame.Kind==LaneBuildMath015.Open?LaneBuildMath015.Context:LaneBuildMath015.Receipt;
            reply.Context=frame.Kind==LaneBuildMath015.Open?frame.Nonce:frame.Context;
            if(frame.Kind==LaneBuildMath015.Open)
            {
                peer.LaneContext015=0;
                if(owner.World!=null&&owner.Builder!=null&&owner.Builder.ModelsReady&&owner.World.LaneBuildSemantic015!=0&&
                    rooms.TryGetPlayerPose(sender,out Vector3 feet,out _)&&!(owner.Revival!=null&&owner.Revival.IsPeerDowned(sender)))
                {
                    int lane=LaneBuildMath015.NearestLane(new float3(feet.x,feet.y,feet.z),admissionLaneMask);
                    result=lane!=frame.Lane||frame.Semantic!=owner.World.LaneBuildSemantic015?LaneBuildReason015.Stale:LaneBuildReason015.Legal;
                    reply.Lane=lane;reply.Semantic=owner.World.LaneBuildSemantic015;
                    if(result==LaneBuildReason015.Legal&&frame.TowerId>0&&(!PlacedElementShrine.TryGetRegistered(frame.TowerId,out var live)||live.IsDestroyed||!TryGetBuilding(frame.TowerId,out var aimed)||aimed.owner!=sender||
                        aimed.buildRevision!=frame.TowerRevision||!rooms.TryGetPlayerAim(sender,out Vector3 look)||!OwnTowerReach015(feet,look,aimed.position)))result=LaneBuildReason015.Ownership;
                    if(result==LaneBuildReason015.Legal){peer.LaneContext015=frame.Nonce;peer.LaneLocked015=lane;peer.LaneSemantic015=reply.Semantic;peer.LaneEpoch015=frame.Epoch;peer.LaneRunA015=a;peer.LaneRunB015=b;peer.LaneTarget015=frame.TowerId;peer.LaneTargetRevision015=frame.TowerRevision;}
                }
            }
            else if(frame.Context==peer.LaneContext015&&peer.LaneContext015!=0&&peer.LaneRunA015==a&&peer.LaneRunB015==b&&
                peer.LaneEpoch015==frame.Epoch&&frame.Lane==peer.LaneLocked015&&frame.Semantic==peer.LaneSemantic015&&
                owner.World!=null&&frame.Semantic==owner.World.LaneBuildSemantic015&&(admissionLaneMask&(1<<frame.Lane))!=0)
            {
                if(frame.Kind==LaneBuildMath015.Close){peer.LaneContext015=0;result=LaneBuildReason015.Legal;}
                else {result=CommitLaneAction015(sender,peer,frame,out reply.TowerId);if(result==LaneBuildReason015.Legal)peer.LaneContext015=0;}
            }
            else result=LaneBuildReason015.Stale;
            reply.Result=(uint)result;peer.LaneResponse015=reply;ReplyLane015(sender,reply);
        }
        void ReplyLane015(ulong sender,in LaneBuildFrame64 reply)
        {if(sender==rooms.LocalClientId)CompleteLaneRequest015(reply);else rooms.SendLaneBuild015(reply,sender);}
        LaneBuildReason015 CommitLaneAction015(ulong sender,Peer peer,in LaneBuildFrame64 frame,out int acceptedId)
        {
            acceptedId=frame.TowerId;
            if(owner.Builder==null||!owner.Builder.ModelsReady)return LaneBuildReason015.Model;
            if(owner.Revival!=null&&owner.Revival.IsPeerDowned(sender))return LaneBuildReason015.Unavailable;
            if(!rooms.TryGetPlayerPose(sender,out Vector3 feet,out _)||!rooms.TryGetPlayerAim(sender,out Vector3 aim))return LaneBuildReason015.Unavailable;
            BuildingState old=null;
            if(frame.Kind!=LaneBuildMath015.Build)
            {
                if(peer.LaneTarget015!=frame.TowerId||peer.LaneTargetRevision015!=frame.TowerRevision||!PlacedElementShrine.TryGetRegistered(frame.TowerId,out var live)||live.IsDestroyed||!TryGetBuilding(frame.TowerId,out old)||old.owner!=sender||old.buildRevision!=frame.TowerRevision||old.health<=0||
                    (feet-old.position).sqrMagnitude>3.5f*3.5f||!owner.Combat.HasLineOfSight(feet+Vector3.up*1.65f,old.position+Vector3.up*1.4f))return LaneBuildReason015.Ownership;
                if(frame.Kind==LaneBuildMath015.Remove)
                {if(frame.Cell!=old.cell||!owner.Builder.RemovePlacedShrine(old.id))return LaneBuildReason015.Stale;buildings.Remove(old);rooms.RecordAcceptedPeerActivity(sender);SendState();return LaneBuildReason015.Legal;}
            }
            if(!TowerForgeDefinitions.ValidType(frame.Element)||CampaignActive&&!campaign.CanBuildTower(sender,frame.Element))return LaneBuildReason015.Unavailable;
            if(peer.Tokens<=0)return LaneBuildReason015.Tokens;
            if(buildings.Count-(old!=null?1:0)>=ActiveTowerCapacity||buildingSerial>=65535)return LaneBuildReason015.Capacity;
            if(!LaneBuildMath015.FromCell(frame.Cell,out float3 p))return LaneBuildReason015.Uneven;
            Vector3 point=new Vector3(p.x,p.y,p.z);
            if(!owner.World.EvaluateLaneBuild015(point,feet,frame.Lane,frame.Element,old!=null?old.id:0,false,out var reason))return reason;
            Vector3 eye=feet+Vector3.up*1.65f;
            if((eye-point).sqrMagnitude>625||!owner.Combat.HasLineOfSight(eye,point+Vector3.up*.01f))return LaneBuildReason015.Blocked;
            uint semantic=owner.World.LaneBuildSemantic015;
            if(semantic!=peer.LaneSemantic015||LaneBuildEpoch015!=peer.LaneEpoch015)return LaneBuildReason015.Stale;
            int id=++buildingSerial; // Never reuse even a failed dormant construction identity.
            var candidate=new BuildingState{id=id,element=frame.Element,owner=sender,position=point,tier=peer.Forge.GetTier(frame.Element),tierRevision=peer.Forge.Revision,
                cell=frame.Cell,lane=frame.Lane,buildRevision=1};
            bool applied=old==null?owner.Builder.TryApplyPlacedShrine(id,frame.Element,point,candidate.tier,candidate.tierRevision,frame.Lane):
                owner.Builder.TryReplaceLaneTower015(old.id,id,frame.Element,point,candidate.tier,candidate.tierRevision,frame.Lane);
            if(!applied)return LaneBuildReason015.Model;
            if(old!=null)buildings.Remove(old);buildings.Add(candidate);peer.Tokens--;acceptedId=id;
            rooms.RecordAcceptedPeerActivity(sender);
            if(owner.Builder.PresentAcceptedWorkshop015(id,candidate.tierRevision,true))owner.Audio?.TryPlayWorkshopAssembly015(point,(uint)candidate.tierRevision);
            else owner.Audio?.PlayProductionCue(ProductionCue.TowerConstruct,point);
            SendState();return LaneBuildReason015.Legal;
        }
        bool OwnTowerReach015(Vector3 feet,Vector3 aim,Vector3 tower)
        {
            Vector3 delta=tower+Vector3.up*1.4f-(feet+Vector3.up*1.65f);
            return delta.sqrMagnitude<=3.5f*3.5f&&delta.sqrMagnitude>.001f&&Vector3.Dot(aim,delta.normalized)>=.82f&&owner.Combat.HasLineOfSight(feet+Vector3.up*1.65f,tower+Vector3.up*1.4f);
        }
        internal bool TryGetLocalOwnedTower015(int id,out Vector3 point,out int revision)
        {point=default;revision=0;if(rooms==null||!TryGetBuilding(id,out var b)||b.owner!=rooms.LocalClientId)return false;point=b.position;revision=b.buildRevision;return true;}
        internal int GatherLaneBuildBodies015(NativeArray<LaneBuildBody32> output)
        {
            if(rooms==null||owner==null||owner.Player==null||output.Length<54||rooms.ConnectedClientIds.Count>2)return -1;
            if(lanePlayerBody015==null)lanePlayerBody015=owner.Player.GetComponent<CharacterController>();if(lanePlayerBody015==null||owner.Player.transform.lossyScale!=Vector3.one)return -1;
            int count=0;var ids=rooms.ConnectedClientIds;
            for(int i=0;i<ids.Count;i++)
            {
                if(!rooms.TryGetPlayerPose(ids[i],out Vector3 p,out _))return -1;
                output[count++]=new LaneBuildBody32{Center=new float3(p.x,p.y,p.z),Radius=lanePlayerBody015.radius,
                    MinimumY=p.y+lanePlayerBody015.center.y-lanePlayerBody015.height*.5f,MaximumY=p.y+lanePlayerBody015.center.y+lanePlayerBody015.height*.5f};
            }
            var enemies=owner.Combat.ActiveEnemies;if(enemies.Count>48)return -1;
            for(int i=0;i<enemies.Count;i++)
            {var e=enemies[i];if(e==null||!e.Targetable)continue;Vector3 p=e.transform.position;
                output[count++]=new LaneBuildBody32{Center=new float3(p.x,p.y,p.z),Radius=e.NavigationRadius,MinimumY=p.y+e.NavigationBottom,MaximumY=p.y+e.NavigationTop};}
            for(int i=0;i<buildings.Count;i++)
            {var b=buildings[i];if(!owner.World.LaneBuildEnvelope015(b.element,out float r,out float top))return -1;Vector3 p=b.position;
                output[count++]=new LaneBuildBody32{Center=new float3(p.x,p.y,p.z),Radius=r,MinimumY=p.y,MaximumY=p.y+top,Identity=(uint)b.id,Kind=b.owner==rooms.LocalClientId?3u:1u};}
            return count;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted host feet/aim and locked active lane; exact world roots/128bit run/epoch/context identity, no client-selected permission.
// Array Formats: Existing2peer records gain bounded context/replay state; fixed64B commands,54 body snapshots and at most4 canonical building records.
// Dependencies/Pointers: Host revalidates full geometry/sole/body/LOS/economy before commit. Dormant replacement retains old actor until preparation succeeds. No XP/damage/range/wave/audio permission rewrites; native/two-peer acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current authenticated run/epoch/context and monotonic real-time pending request ages; placement/world coordinates unchanged.
// Array Formats: Immutable Frame64, fixed Pending96 scalar state and one cached Frame64 per existing peer; no per-frame heap container or ABI change.
// Dependencies/Pointers: Bounded same-nonce delivery, exact host replay and response-controlled UI only. Root alone imports/native-tests; frozen original packet remains unchanged.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current active Builder lifetime and existing run/epoch/nonce identities; no world or input coordinate changes.
// Array Formats: Existing Frame64/Pending96 plus one borrowed local Builder identity reference; no ABI/native/container allocation changes.
// Dependencies/Pointers: Canonical disable cancels pending/context and clears visuals; inactive/replaced lifetime cannot receive UI callbacks. Root alone imports/native-tests this tiny guarded layer.
