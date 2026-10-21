// PSEUDOCODE: own one cold fixed feedback store; commit a value after real HP
// decreases; dispatch bounded observers synchronously; propose numeric movement,
// copy nonalloc native collision/support facts, admit and apply exact current steps.
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using FeedbackReload022 = UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using FeedbackReload022 = System.Action;
#endif
namespace OdysseyOracle
{
    public sealed partial class OracleCombat
    {
        NativeArray<EnemyFeedbackPose022> feedbackPoses022;
        NativeArray<EnemyFeedbackState022> feedbackStates022;
        NativeArray<EnemyFeedbackStep022> feedbackSteps022;
        NativeArray<EnemyFeedbackFloor022> feedbackSupports022;
        NativeParallelMultiHashMap<uint,int> feedbackCells022;
        JobHandle feedbackFence022;
        readonly RaycastHit[] feedbackCasts022=new RaycastHit[64],feedbackFloorHits022=new RaycastHit[16];
        readonly Collider[] feedbackOverlaps022=new Collider[64];
        readonly Action<EnemyAcceptedHit022>[] feedbackObservers022=new Action<EnemyAcceptedHit022>[4];
        FeedbackReload022 feedbackReload022;
        DungeonWorld feedbackWorld022;
        uint feedbackEncounter022,feedbackRevision022;
        ulong feedbackSerial022;
        bool feedbackReady022,feedbackPublishing022,feedbackReloadSubscribed022,feedbackContextHeld022,feedbackHasWork022;

        // Subscription is cold. A cold multicast check bounds real observers;
        // dispatch never expands delegates or allocates a temporary list.
        public event Action<EnemyAcceptedHit022> EnemyDamageAccepted022
        {
            add
            {
                if(value==null)return;
                if(feedbackPublishing022)throw new InvalidOperationException("Subscribe outside accepted-damage dispatch.");
                if(value.GetInvocationList().Length!=1)throw new InvalidOperationException("One callback per cold subscription.");
                for(int i=0;i<feedbackObservers022.Length;i++)if(feedbackObservers022[i]==value)return;
                for(int i=0;i<feedbackObservers022.Length;i++)if(feedbackObservers022[i]==null){feedbackObservers022[i]=value;return;}
                throw new InvalidOperationException("Four cold accepted-hit observers are the bounded capacity.");
            }
            remove
            {
                if(feedbackPublishing022)throw new InvalidOperationException("Unsubscribe outside accepted-damage dispatch.");
                for(int i=0;i<feedbackObservers022.Length;i++)if(feedbackObservers022[i]==value)feedbackObservers022[i]=null;
            }
        }
        void PrepareEnemyFeedback022()
        {
            if(feedbackReady022)return;
            try
            {
                feedbackPoses022=new NativeArray<EnemyFeedbackPose022>(56,Allocator.Persistent);
                feedbackStates022=new NativeArray<EnemyFeedbackState022>(48,Allocator.Persistent);
                feedbackSteps022=new NativeArray<EnemyFeedbackStep022>(48,Allocator.Persistent);
                feedbackSupports022=new NativeArray<EnemyFeedbackFloor022>(48*16,Allocator.Persistent);
                feedbackCells022=new NativeParallelMultiHashMap<uint,int>(56,Allocator.Persistent);
                feedbackWorld022=world;feedbackEncounter022=1;feedbackRevision022=world!=null?world.WorldRevision020:0;
                feedbackFence022=new EnemyFeedbackProposeJob022{Poses=feedbackPoses022,States=feedbackStates022,Steps=feedbackSteps022,Cells=feedbackCells022,Delta=0}.Schedule();
                feedbackFence022=new EnemyFeedbackAdmitJob022{Poses=feedbackPoses022,States=feedbackStates022,Steps=feedbackSteps022,Cells=feedbackCells022,Supports=feedbackSupports022}.Schedule(feedbackFence022);
                feedbackFence022.Complete();feedbackReady022=true;
                if(feedbackReload022==null)feedbackReload022=DisposeEnemyFeedback022;
                LightPoolLifecycle.SubscribeBeforeReload(feedbackReload022);feedbackReloadSubscribed022=true;
            }
            catch{DisposeEnemyFeedback022();throw;}
        }
        bool FeedbackOwnerCurrent022()=>feedbackReady022&&isActiveAndEnabled&&initialized&&!destroying&&world!=null&&world.isActiveAndEnabled&&
            ReferenceEquals(world,feedbackWorld022)&&GameSession.Instance!=null&&ReferenceEquals(GameSession.Instance.Combat,this)&&
            ReferenceEquals(GameSession.Instance.World,world)&&GameSession.Instance.LegacyCombatWorld020&&feedbackEncounter022!=0&&world.WorldRevision020!=0;
        bool SyncFeedbackScope022()
        {
            if(!FeedbackOwnerCurrent022())return false;
            if(feedbackRevision022!=world.WorldRevision020){AdvanceEnemyFeedbackScope022();feedbackRevision022=world.WorldRevision020;}
            return feedbackEncounter022!=0;
        }
        void AdvanceEnemyFeedbackScope022()
        {
            feedbackFence022.Complete();feedbackHasWork022=false;
            if(feedbackEncounter022!=0)feedbackEncounter022=feedbackEncounter022==uint.MaxValue?0:feedbackEncounter022+1;
        }
        internal bool CanCommitEnemyDamage022(OracleEnemy actor)=>actor!=null&&Playing&&isActiveAndEnabled&&!RemotePresentation&&
            GameSession.Instance!=null&&ReferenceEquals(GameSession.Instance.Combat,this)&&ReferenceEquals(GameSession.Instance.World,world)&&
            actor.FeedbackSlot022>=0&&actor.FeedbackSlot022<48&&ReferenceEquals(pooledEnemies015[actor.FeedbackSlot022],actor)&&actor.PoolLeased015&&actor.PoolGeneration015!=0;
        EnemyLifeToken022 Life022(OracleEnemy actor)=>new EnemyLifeToken022{Encounter=feedbackEncounter022,WorldRevision=feedbackRevision022,
            PoolGeneration=actor.PoolGeneration015,Slot=actor.FeedbackSlot022,TargetId=actor.TargetId};
        public bool IsEnemyLifeCurrent022(in EnemyLifeToken022 life)=>FeedbackOwnerCurrent022()&&feedbackRevision022==world.WorldRevision020&&
            life.Encounter==feedbackEncounter022&&life.WorldRevision==feedbackRevision022&&life.Encounter!=0&&life.PoolGeneration!=0&&life.TargetId>0&&
            life.Slot>=0&&life.Slot<48&&pooledEnemies015[life.Slot]!=null&&pooledEnemies015[life.Slot].PoolLeased015&&
            pooledEnemies015[life.Slot].TargetId==life.TargetId&&pooledEnemies015[life.Slot].PoolGeneration015==life.PoolGeneration;
        public bool IsEnemyHitCurrent022(in EnemyAcceptedHit022 hit)=>hit.Serial!=0&&hit.Serial<=feedbackSerial022&&IsEnemyLifeCurrent022(hit.Life);
        internal bool TryReadEnemyHealth022(OracleEnemy actor,out EnemyHealthReadout022 readout)
        {
            readout=default;if(actor==null||!FeedbackOwnerCurrent022()||feedbackRevision022!=world.WorldRevision020||actor.FeedbackSlot022<0||actor.FeedbackSlot022>=48||
                !ReferenceEquals(pooledEnemies015[actor.FeedbackSlot022],actor)||!actor.PoolLeased015||actor.PoolGeneration015==0)return false;
            readout=new EnemyHealthReadout022{Life=Life022(actor),HP=actor.HP,MaxHP=actor.MaxHP,Position=actor.FeedbackAnchor022,
                Alive=actor.Alive?(byte)1:(byte)0,Targetable=actor.Targetable?(byte)1:(byte)0,Remote=RemotePresentation?(byte)1:(byte)0};return true;
        }
        EnemyFeedbackPose022 CaptureFeedbackPose022(OracleEnemy actor)
        {
            if(actor==null||!actor.PoolLeased015||actor.PoolGeneration015==0||!actor.Targetable)return default;
            Vector3 p=actor.transform.position;
            return new EnemyFeedbackPose022{Position=new float3(p.x,p.y,p.z),Radius=actor.NavigationRadius,CrowdRadius=actor.CrowdRadius,
                Encounter=feedbackEncounter022,Revision=feedbackRevision022,Generation=actor.PoolGeneration015,Id=actor.TargetId,
                Active=1,Flying=actor.IsFlying?(byte)1:(byte)0,Movable=actor.FeedbackMovementAllowed022?(byte)1:(byte)0};
        }
        internal EnemyAcceptedHit022 CommitEnemyDamageFeedback022(OracleEnemy actor,float before,float health,float maximum,Vector3 direction,int ingredients,Vector3 point,bool surfaceContact)
        {
            // HP was already committed once by its original owner. Presentation
            // exhaustion never cancels or repeats that accepted gameplay damage.
            if(!CanCommitEnemyDamage022(actor)||!SyncFeedbackScope022())return default;
            feedbackFence022.Complete();int slot=actor.FeedbackSlot022;
            ingredients=ingredients>=0&&ingredients<=15?ingredients:0;
            float3 vector=new float3(direction.x,direction.y,direction.z);if(!math.all(math.isfinite(vector)))vector=0;vector.y=0;
            if(!EnemyFeedbackMath022.Point(new float3(point.x,point.y,point.z))){point=actor.AimPoint;surfaceContact=false;}
            var pose=CaptureFeedbackPose022(actor);bool survived=health>0;EnemyFeedbackState022 state=default;
            bool requested=survived&&EnemyFeedbackMath022.Request(pose,vector,ingredients,out state);
            if(requested){feedbackStates022[slot]=state;feedbackHasWork022=true;}
            else if(!survived||!actor.FeedbackMovementAllowed022)feedbackStates022[slot]=default;
            if(feedbackSerial022==ulong.MaxValue)return default;
            int element=ingredients==1?0:ingredients==2?1:ingredients==4?2:ingredients==8?3:-1;
            EnemyHitFlags022 flags=survived?EnemyHitFlags022.Survived:EnemyHitFlags022.None;
            if((ingredients&8)!=0)flags|=EnemyHitFlags022.Wind;if(requested)flags|=EnemyHitFlags022.KnockbackRequested;
            if(surfaceContact)flags|=EnemyHitFlags022.SurfaceContact;
            return new EnemyAcceptedHit022{Life=Life022(actor),Serial=++feedbackSerial022,Before=before,HP=health,MaxHP=maximum,AcceptedDamage=before-health,
                Position=point,Anchor=actor.FeedbackAnchor022,Direction=new Vector3(vector.x,vector.y,vector.z),Ingredients=ingredients,Element=element,Flags=flags};
        }
        internal void PublishEnemyDamageFeedback022(in EnemyAcceptedHit022 hit)
        {
            if(hit.Serial==0||hit.AcceptedDamage<=0||!IsEnemyHitCurrent022(hit)||feedbackPublishing022)return;
            feedbackPublishing022=true;
            try{for(int i=0;i<feedbackObservers022.Length;i++)
                {var observer=feedbackObservers022[i];if(observer==null)continue;try{observer(hit);}catch(Exception failure){Debug.LogException(failure,this);}}}
            finally{feedbackPublishing022=false;}
        }
        internal bool EnemyKickActive022(OracleEnemy actor)
        {
            if(actor==null||!FeedbackOwnerCurrent022()||feedbackRevision022!=world.WorldRevision020||actor.FeedbackSlot022<0||actor.FeedbackSlot022>=48)return false;
            feedbackFence022.Complete();var state=feedbackStates022[actor.FeedbackSlot022];
            return state.Remaining>0&&EnemyFeedbackMath022.Same(CaptureFeedbackPose022(actor),state);
        }
        internal void ClearEnemyKick022(OracleEnemy actor)
        {
            if(actor==null||!feedbackStates022.IsCreated||actor.FeedbackSlot022<0||actor.FeedbackSlot022>=48)return;
            feedbackFence022.Complete();var state=feedbackStates022[actor.FeedbackSlot022];
            if(state.Generation==actor.PoolGeneration015&&state.Id==actor.TargetId)feedbackStates022[actor.FeedbackSlot022]=default;
        }
        void TickEnemyFeedback022(float delta)
        {
            if(!SyncFeedbackScope022())
            {if(!feedbackContextHeld022){AdvanceEnemyFeedbackScope022();feedbackContextHeld022=true;}return;}
            feedbackContextHeld022=false;
            if(!feedbackHasWork022||!Playing||delta==0)return;
            feedbackFence022.Complete();
            for(int i=0;i<48;i++)feedbackPoses022[i]=CaptureFeedbackPose022(pooledEnemies015[i]);
            for(int i=48;i<56;i++)feedbackPoses022[i]=default;
            bool peers=PeerArenaCombat;int count=peers?QueryPeerTargets(out _):0;
            if(peers)
            {for(int i=0;i<count&&i<8;i++){var p=peerTargets[i];feedbackPoses022[48+i]=new EnemyFeedbackPose022{Position=new float3(p.Feet.x,p.Feet.y,p.Feet.z),Radius=.29f,CrowdRadius=.29f,Id=int.MinValue+i,Active=p.Targetable?(byte)1:(byte)0};}}
            else if(player!=null&&player.isActiveAndEnabled&&GameSession.Instance.HP>0&&!GameSession.Instance.IsDowned)
            {Vector3 p=player.transform.position;feedbackPoses022[48]=new EnemyFeedbackPose022{Position=new float3(p.x,p.y,p.z),Radius=.29f,CrowdRadius=.29f,Id=int.MinValue,Active=1};}
            feedbackFence022=new EnemyFeedbackProposeJob022{Poses=feedbackPoses022,States=feedbackStates022,Steps=feedbackSteps022,Cells=feedbackCells022,Delta=Playing?delta:0}.Schedule();
            feedbackFence022.Complete();
            for(int i=0;i<48;i++)
            {
                var step=feedbackSteps022[i];if(step.Wanted==0)continue;
                var actor=pooledEnemies015[i];step.Clear=CaptureFeedbackPhysics022(actor,i,step)?(byte)1:(byte)0;feedbackSteps022[i]=step;
            }
            feedbackFence022=new EnemyFeedbackAdmitJob022{Poses=feedbackPoses022,States=feedbackStates022,Steps=feedbackSteps022,Cells=feedbackCells022,Supports=feedbackSupports022}.Schedule();
            feedbackFence022.Complete();
            feedbackHasWork022=false;
            for(int i=0;i<48;i++)
            {
                if(feedbackStates022[i].Remaining>0)feedbackHasWork022=true;
                var step=feedbackSteps022[i];if(step.Accepted!=1)continue;
                var pose=feedbackPoses022[i];var actor=pooledEnemies015[i];
                if(!Playing||actor==null||!IsEnemyLifeCurrent022(new EnemyLifeToken022{Encounter=pose.Encounter,WorldRevision=pose.Revision,PoolGeneration=pose.Generation,Slot=i,TargetId=pose.Id})||!actor.Targetable||!actor.FeedbackMovementAllowed022)
                {feedbackStates022[i]=default;continue;}
                Vector3 from=new Vector3(pose.Position.x,pose.Position.y,pose.Position.z);
                if(actor.transform.position!=from){feedbackStates022[i]=default;continue;}
                actor.ApplyFeedbackStep022(new Vector3(step.Move.x,0,step.Move.z));
            }
        }
        bool CaptureFeedbackPhysics022(OracleEnemy actor,int slot,in EnemyFeedbackStep022 step)
        {
            for(int f=0;f<16;f++)feedbackSupports022[slot*16+f]=default;
            if(actor==null||!actor.Targetable||!actor.FeedbackMovementAllowed022)return false;
            Vector3 foot=actor.transform.position,move=new Vector3(step.Move.x,0,step.Move.z);
            if(!MovementStepMath.TryNormalize(move.x,0,move.z,out var travel))return false;
            Vector3 low=foot+Vector3.up*actor.NavigationBottom,high=foot+Vector3.up*actor.NavigationTop;
            int overlaps=Physics.OverlapCapsuleNonAlloc(low,high,actor.NavigationRadius,feedbackOverlaps022,~0,QueryTriggerInteraction.Ignore);
            if(overlaps==feedbackOverlaps022.Length)return false;
            for(int i=0;i<overlaps;i++){var c=feedbackOverlaps022[i];if(c!=null&&c.transform!=actor.transform&&!c.transform.IsChildOf(actor.transform))return false;}
            int count=Physics.CapsuleCastNonAlloc(low,high,actor.NavigationRadius,new Vector3(travel.UnitX,0,travel.UnitZ),feedbackCasts022,travel.Distance+.01f,~0,QueryTriggerInteraction.Ignore);
            if(count==feedbackCasts022.Length)return false;
            for(int i=0;i<count;i++){var c=feedbackCasts022[i].collider;if(c!=null&&c.transform!=actor.transform&&!c.transform.IsChildOf(actor.transform))return false;}
            if(actor.IsFlying)return true;
            count=Physics.RaycastNonAlloc(foot+Vector3.up*.12f,Vector3.down,feedbackFloorHits022,.30f,~0,QueryTriggerInteraction.Ignore);
            if(count==feedbackFloorHits022.Length)return false;
            for(int i=0;i<count;i++)
            {
                var box=feedbackFloorHits022[i].collider as BoxCollider;
                if(box==null||box.isTrigger||box.attachedRigidbody!=null||!box.transform.IsChildOf(world.transform))continue;
                Transform root=box.transform;Vector3 scale=root.lossyScale;if(scale.x<=0||scale.y<=0||scale.z<=0||Vector3.Dot(root.up,Vector3.up)<.99999f)continue;
                Vector3 center=root.TransformPoint(box.center),top=root.TransformPoint(box.center+Vector3.up*(box.size.y*.5f));Matrix4x4 m=root.localToWorldMatrix;
                feedbackSupports022[slot*16+i]=new EnemyFeedbackFloor022{Center=new float3(center.x,center.y,center.z),
                    AxisX=new float3(m.m00,m.m10,m.m20),AxisY=new float3(m.m01,m.m11,m.m21),AxisZ=new float3(m.m02,m.m12,m.m22),
                    HalfX=box.size.x*.5f,HalfZ=box.size.z*.5f,Top=top.y,Valid=1};
            }
            return true;
        }
        void SuspendEnemyFeedback022(){AdvanceEnemyFeedbackScope022();}
        void DisposeEnemyFeedback022()
        {
            if(feedbackReloadSubscribed022&&feedbackReload022!=null)LightPoolLifecycle.UnsubscribeBeforeReload(feedbackReload022);
            feedbackReloadSubscribed022=false;feedbackReady022=false;feedbackFence022.Complete();
            if(feedbackPoses022.IsCreated)feedbackPoses022.Dispose();if(feedbackStates022.IsCreated)feedbackStates022.Dispose();
            if(feedbackSteps022.IsCreated)feedbackSteps022.Dispose();if(feedbackSupports022.IsCreated)feedbackSupports022.Dispose();if(feedbackCells022.IsCreated)feedbackCells022.Dispose();
            feedbackWorld022=null;feedbackEncounter022=feedbackRevision022=0;
            for(int i=0;i<feedbackObservers022.Length;i++)feedbackObservers022[i]=null;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact current legacy Combat/World/revision, actor pool epoch and fixed world Y; no campaign combat admission is invented.
// Array Formats:48 state/step rows,56 pose/Morton entries,768 oriented solid support values;64 cast/overlap and16 floor scratch;four cold observers.
// Dependencies/Pointers: Existing pool cold preparation/retirement/disable/disposal own fences. Physics/Object calls stay on main-thread boundary; jobs are numeric only. HP/death/network authority stays in OracleEnemy; root applies and measures native behavior/GC.
