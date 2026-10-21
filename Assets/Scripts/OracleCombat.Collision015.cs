using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using OdysseyOracle.Foundation.Collision015;
#if UNITY_EDITOR
using MonsterReload015 = UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using MonsterReload015 = System.Action;
#endif

namespace OdysseyOracle
{
    internal struct ProjectileContact015
    {
        internal Collider WorldCollider;
        internal Vector3 Point,Normal;
        internal float Distance;
        internal MonsterResult64 Monster;
        internal bool Blocked,HasMonster;
    }
    public sealed partial class OracleCombat
    {
        NativeArray<MonsterRawPose80> monsterRaw015;
        NativeArray<MonsterTarget64> monsterTargets015;
        NativeParallelMultiHashMap<ulong,int> monsterCells015;
        NativeArray<double> monsterBound015;
        NativeArray<int> monsterStatus015;
        NativeArray<MonsterQuery80> monsterQuery015;
        NativeArray<MonsterResult64> monsterResult015;
        JobHandle monsterReaders015;
        uint monsterSnapshot015,monsterSequence015,monsterAcceptedSequence015;
        bool monsterCollisionReady015,monsterQueryBusy015;
        MonsterReload015 monsterReload015;
        bool monsterReloadSubscribed015;
        readonly RaycastHit[] monsterWorldSweep015=new RaycastHit[256];
        readonly Collider[] monsterWorldOverlap015=new Collider[128];
        public int MonsterCollisionSaturations015{get;private set;}
        public bool NumericMonsterCollisionReady015=>monsterCollisionReady015;

        void InitializeMonsterCollision015()
        {
            if(monsterCollisionReady015)return;
            try
            {
                monsterRaw015=new NativeArray<MonsterRawPose80>(48,Allocator.Persistent);
                monsterTargets015=new NativeArray<MonsterTarget64>(48,Allocator.Persistent);
                monsterCells015=new NativeParallelMultiHashMap<ulong,int>(48,Allocator.Persistent);
                monsterBound015=new NativeArray<double>(1,Allocator.Persistent);monsterStatus015=new NativeArray<int>(1,Allocator.Persistent);
                monsterQuery015=new NativeArray<MonsterQuery80>(1,Allocator.Persistent);monsterResult015=new NativeArray<MonsterResult64>(1,Allocator.Persistent);
                // Compile/warm numeric paths before admitted actions. Empty data cannot apply damage.
                monsterReaders015=new BuildMonsterSnapshot015{Raw=monsterRaw015,Targets=monsterTargets015,Cells=monsterCells015,MaximumBound=monsterBound015,Status=monsterStatus015}.Schedule();
                monsterReaders015=new SweepMonsters015{Targets=monsterTargets015,Cells=monsterCells015,MaximumBound=monsterBound015,SnapshotStatus=monsterStatus015,
                    Queries=monsterQuery015,Results=monsterResult015}.Schedule(monsterReaders015);
                CompleteMonsterReaders015();monsterCollisionReady015=true;
                if(monsterReload015==null)monsterReload015=DisposeMonsterCollision015;
                LightPoolLifecycle.SubscribeBeforeReload(monsterReload015);monsterReloadSubscribed015=true;
            }
            catch{DisposeMonsterCollision015();throw;}
        }
        void CompleteMonsterReaders015(){monsterReaders015.Complete();monsterReaders015=default;}
        internal OracleEnemy ResolveMonsterContact015(in ProjectileContact015 contact)
        {
            if(!contact.HasMonster||!monsterCollisionReady015)return null;
            MonsterResult64 h=contact.Monster;
            if(h.Slot<0||h.Slot>=pooledEnemies015.Length||h.Sequence<=monsterAcceptedSequence015)return null;
            OracleEnemy actor=pooledEnemies015[h.Slot];
            if(actor==null||!actor.PoolLeased015||!MonsterContactMath015.TryConsume(ref monsterAcceptedSequence015,h,h.Slot,actor.TargetId,actor.PoolGeneration015,
                monsterSnapshot015,monsterSequence015,actor.Targetable))return null;
            return actor;
        }
        // Existing callers retain accepted launch, damage, status, replica and pierce ownership.
        // True+Blocked is a conservative stop, never an invented target or damage permission.
        internal bool SweepProjectileContact015(Vector3 origin,float radius,Vector3 direction,float distance,
            bool includeMonsters,bool ignorePlayers,bool ignoreShrines,int[] ignoredIds,int ignoredCount,out ProjectileContact015 contact)
        {
            contact=default;
            Vector3 end=origin+direction*distance;
            if(!monsterCollisionReady015||monsterQueryBusy015||!FinitePosition(origin)||!FinitePosition(direction)||
                !MonsterContactMath015.Position(end.x,end.y,end.z)||
                float.IsNaN(radius)||float.IsInfinity(radius)||radius<0||radius>2||float.IsNaN(distance)||float.IsInfinity(distance)||distance<0||
                ignoredCount<0||ignoredCount>3||ignoredCount>0&&(ignoredIds==null||ignoredIds.Length<ignoredCount)||
                distance>0&&Mathf.Abs(direction.sqrMagnitude-1f)>.0001f)
            {contact=new ProjectileContact015{Blocked=true,Point=origin,Normal=-direction};return true;}
            bool worldFound=WorldSweep015(origin,radius,direction,distance,ignorePlayers,ignoreShrines,out contact);
            if(contact.Blocked||!includeMonsters)return worldFound;
            if(monsterSnapshot015==uint.MaxValue||monsterSequence015==uint.MaxValue)
            {contact=new ProjectileContact015{Blocked=true,Point=origin,Normal=-direction};return true;}
            monsterQueryBusy015=true;
            try
            {
                CompleteMonsterReaders015();
                // Fixed48 Object-copy adapters. No shape/contact/grid math is performed in this loop.
                // Refresh per query: do not reuse a frame cache across movement, replica updates or slot rerentals.
                for(int i=0;i<48;i++)monsterRaw015[i]=pooledEnemies015[i]!=null?pooledEnemies015[i].CaptureMonsterPose015():default;
                monsterSnapshot015++;monsterSequence015++;
                monsterQuery015[0]=new MonsterQuery80{X=origin.x,Y=origin.y,Z=origin.z,Radius=radius,EndX=end.x,EndY=end.y,EndZ=end.z,
                    WorldFound=worldFound?1u:0,WorldFraction=worldFound&&distance>0?Mathf.Clamp01(contact.Distance/distance):worldFound?0:1,
                    RequiredFlags=MonsterContactMath015.Active,SnapshotGeneration=monsterSnapshot015,Sequence=monsterSequence015,
                    Ignore0=ignoredCount>0?ignoredIds[0]:0,Ignore1=ignoredCount>1?ignoredIds[1]:0,Ignore2=ignoredCount>2?ignoredIds[2]:0,IgnoreCount=ignoredCount};
                monsterReaders015=new BuildMonsterSnapshot015{Raw=monsterRaw015,Targets=monsterTargets015,Cells=monsterCells015,MaximumBound=monsterBound015,Status=monsterStatus015}.Schedule();
                monsterReaders015=new SweepMonsters015{Targets=monsterTargets015,Cells=monsterCells015,MaximumBound=monsterBound015,SnapshotStatus=monsterStatus015,
                    Queries=monsterQuery015,Results=monsterResult015}.Schedule(monsterReaders015);
                CompleteMonsterReaders015();MonsterResult64 hit=monsterResult015[0];
                if(hit.Kind==MonsterContactMath015.Invalid){contact=new ProjectileContact015{Blocked=true,Point=origin,Normal=-direction};return true;}
                if(hit.Kind!=MonsterContactMath015.Hit)return worldFound;
                contact=new ProjectileContact015{HasMonster=true,Monster=hit,Distance=hit.Distance,Point=new Vector3(hit.X,hit.Y,hit.Z),Normal=new Vector3(hit.NormalX,hit.NormalY,hit.NormalZ)};
                return true;
            }
            finally{monsterQueryBusy015=false;}
        }
        bool WorldSweep015(Vector3 origin,float radius,Vector3 direction,float distance,bool ignorePlayers,bool ignoreShrines,out ProjectileContact015 contact)
        {
            contact=default;float best=float.PositiveInfinity;EntityId bestId=default;
            int overlaps=Physics.OverlapSphereNonAlloc(origin,radius,monsterWorldOverlap015,~0,QueryTriggerInteraction.Ignore);
            if(overlaps==monsterWorldOverlap015.Length){MonsterCollisionSaturations015++;contact=new ProjectileContact015{Blocked=true,Point=origin,Normal=-direction};return true;}
            for(int i=0;i<overlaps;i++)
            {
                Collider c=monsterWorldOverlap015[i];if(!WorldColliderBlocks015(c,ignorePlayers,ignoreShrines))continue;
                EntityId id=c.GetEntityId();if(best==0&&id.CompareTo(bestId)>=0)continue;best=0;bestId=id;
                contact=new ProjectileContact015{WorldCollider=c,Distance=0,Point=c.ClosestPoint(origin),Normal=-direction};
            }
            if(best==0)return true;
            if(distance==0)return false;
            int count=radius==0?Physics.RaycastNonAlloc(origin,direction,monsterWorldSweep015,distance,~0,QueryTriggerInteraction.Ignore):
                Physics.SphereCastNonAlloc(origin,radius,direction,monsterWorldSweep015,distance,~0,QueryTriggerInteraction.Ignore);
            if(count==monsterWorldSweep015.Length){MonsterCollisionSaturations015++;contact=new ProjectileContact015{Blocked=true,Point=origin,Normal=-direction};return true;}
            for(int i=0;i<count;i++)
            {
                RaycastHit hit=monsterWorldSweep015[i];Collider c=hit.collider;if(!WorldColliderBlocks015(c,ignorePlayers,ignoreShrines))continue;
                EntityId id=c.GetEntityId();if(hit.distance>best||hit.distance==best&&id.CompareTo(bestId)>=0)continue;
                best=hit.distance;bestId=id;contact=new ProjectileContact015{WorldCollider=c,Distance=hit.distance,Point=hit.point,Normal=hit.normal};
            }
            return best<float.PositiveInfinity;
        }
        bool WorldColliderBlocks015(Collider c,bool ignorePlayers,bool ignoreShrines)=>c!=null&&
            c.GetComponentInParent<OracleProjectile>()==null&&c.GetComponentInParent<OracleEnemy>()==null&&
            (!ignorePlayers||!IsPlayerCollider(c))&&(!ignoreShrines||c.GetComponentInParent<OracleShrine>()==null);
        void DisposeMonsterCollision015()
        {
            if(monsterReloadSubscribed015&&monsterReload015!=null)LightPoolLifecycle.UnsubscribeBeforeReload(monsterReload015);
            monsterReloadSubscribed015=false;
            monsterCollisionReady015=false;CompleteMonsterReaders015();
            if(monsterRaw015.IsCreated)monsterRaw015.Dispose();if(monsterTargets015.IsCreated)monsterTargets015.Dispose();if(monsterCells015.IsCreated)monsterCells015.Dispose();
            if(monsterBound015.IsCreated)monsterBound015.Dispose();if(monsterStatus015.IsCreated)monsterStatus015.Dispose();
            if(monsterQuery015.IsCreated)monsterQuery015.Dispose();if(monsterResult015.IsCreated)monsterResult015.Dispose();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current world-metre segment and admission-cached actor-local box, wall/player/shrine contacts retain engine-world authority.
// Array Formats: Fixed48 raw/target/map, one80B query/64B result, bounded256World hits/128start overlaps; owned persistent native storage and one reader fence.
// Dependencies/Pointers: Main-thread Object/Physics boundary only; numeric grid/contact jobs never damage. Existing launch/damage/status/peer/lab owners consume generation-fenced results once. All canonical trigger consumers must switch atomically before colliders are disabled; actual native/Burst/GC acceptance pending.
