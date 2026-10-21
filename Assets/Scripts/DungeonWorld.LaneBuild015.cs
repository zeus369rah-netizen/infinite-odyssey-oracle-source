using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using OdysseyOracle.Foundation.World;
using OdysseyOracle.Foundation.Pools015;
#if UNITY_EDITOR
using LaneWorldReload015=UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using LaneWorldReload015=System.Action;
#endif

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        NativeArray<LaneBuildTerrain8> laneTerrain015;
        NativeArray<LaneBuildStamp32> laneStamps015;
        NativeArray<LaneBuildBody32> laneBodies015;
        NativeArray<LaneBuildPoint16> lanePoint015,laneHighlights015;
        NativeArray<int> laneHighlightCount015,laneSolidResult015;
        NativeArray<uint> laneSignature015;
        JobHandle laneBuildReader015;
        int laneStampCount015;bool laneCapturing015,laneRoadCapture015,laneMaskOverflow015;
        UnityEngine.BoxCollider laneFoundation015;
        LaneWorldReload015 laneWorldReload015;bool laneWorldSubscribed015;
        public uint LaneBuildSemantic015=>IsCoopArena&&transform.localToWorldMatrix==Matrix4x4.identity&&laneSignature015.IsCreated&&!laneMaskOverflow015?laneSignature015[0]:0;
        // Registered support only; canonical host roots stay Y0. Supplies cosmetic height.
        internal bool TryGetLaneBuildFloor020(out float height)
        {
            height=0;if(LaneBuildSemantic015==0||laneFoundation015==null||!laneFoundation015.enabled||!laneFoundation015.gameObject.activeInHierarchy||laneFoundation015.transform.up.y<.9999f)return false;
            var bounds=laneFoundation015.bounds;
            if(!math.all(math.isfinite(new float3(bounds.min.x,bounds.min.y,bounds.min.z)))||!math.all(math.isfinite(new float3(bounds.max.x,bounds.max.y,bounds.max.z)))||bounds.size.x<=0||bounds.size.z<=0)return false;
            height=bounds.max.y;return math.abs(height)<=LaneBuildPreviewRules020.SurfaceTolerance;
        }
        internal NativeArray<LaneBuildPoint16> LaneBuildHighlights015=>laneHighlights015;
        public int LaneBuildHighlightCount015=>laneHighlightCount015.IsCreated?laneHighlightCount015[0]:0;
        public int LaneBuildStampCount015=>laneStampCount015;
        public bool InspectLaneBuild015(Vector3 point,Vector3 feet,int lane,int type,out uint reason)
        {bool legal=EvaluateLaneBuild015(point,feet,lane,type,0,false,out var value);reason=(uint)value;return legal;}
        internal int PickOwnedLaneTower015(Vector3 eye,Vector3 forward)
        {
            if(!laneBodies015.IsCreated)return 0;var session=GameSession.Instance;if(session==null||session.Defense==null)return 0;
            laneBuildReader015.Complete();int count=session.Defense.GatherLaneBuildBodies015(laneBodies015);if(count<0)return 0;
            laneBuildReader015=new LaneBuildAimOwned015{Bodies=laneBodies015,Count=count,Eye=new float3(eye.x,eye.y,eye.z),Direction=new float3(forward.x,forward.y,forward.z),Result=laneSolidResult015}.Schedule();
            laneBuildReader015.Complete();int id=laneSolidResult015[0];
            return id>0&&PlacedElementShrine.TryGetRegistered(id,out var tower)&&session.Combat.HasLineOfSight(eye,tower.Position+Vector3.up*1.4f)?id:0;
        }
        void BeginLaneBuild015()
        {
            DisposeLaneBuild015();
            try
            {
            laneTerrain015=new NativeArray<LaneBuildTerrain8>(LaneBuildMath015.TerrainCount,Allocator.Persistent);
            laneStamps015=new NativeArray<LaneBuildStamp32>(LaneBuildMath015.StampCapacity,Allocator.Persistent);
            laneBodies015=new NativeArray<LaneBuildBody32>(LaneBuildMath015.BodyCapacity,Allocator.Persistent);
            lanePoint015=new NativeArray<LaneBuildPoint16>(1,Allocator.Persistent);
            laneHighlights015=new NativeArray<LaneBuildPoint16>(LaneBuildMath015.HighlightCapacity,Allocator.Persistent);
            laneHighlightCount015=new NativeArray<int>(1,Allocator.Persistent);laneSolidResult015=new NativeArray<int>(1,Allocator.Persistent);
            laneSignature015=new NativeArray<uint>(1,Allocator.Persistent);laneCapturing015=true;
            laneWorldReload015=DisposeLaneBuild015;LightPoolLifecycle.SubscribeBeforeReload(laneWorldReload015);laneWorldSubscribed015=true;
            }
            catch{DisposeLaneBuild015();throw;}
        }
        void RecordLaneGround015(int x,int z,bool water)
        {
            if(!laneCapturing015)return;int a=x+128,b=z+128;
            if(a<0||b<0||a>=256||b>=256){laneMaskOverflow015=true;return;}
            laneTerrain015[(int)LaneBuildMath015.Morton(a,b,8)]=new LaneBuildTerrain8{Flags=water?LaneBuildMath015.Water:LaneBuildMath015.Land,SupportY=0};
        }
        void RecordLaneStamp015(Vector3 p,Vector3 size,Quaternion q,uint flags)
        {
            if(!laneCapturing015)return;
            if(laneStampCount015>=laneStamps015.Length){laneMaskOverflow015=true;return;}
            Vector3 right=q*Vector3.right;
            laneStamps015[laneStampCount015++]=new LaneBuildStamp32{Center=new float2(p.x,p.z),Half=new float2(size.x,size.z)*.5f,Axis=new float2(right.x,right.z),Flags=flags};
        }
        void FinishLaneBuild015()
        {
            laneCapturing015=laneRoadCapture015=false;if(laneMaskOverflow015||laneFoundation015==null)return;
            // Explicit approach reservations, separate from rendering and terrain colour.
            // All8portal roots and the exact Smith route use the same canonical World APIs.
            laneBuildReader015=new LaneBuildReservations015{Stamps=laneStamps015,Start=laneStampCount015,Result=laneSolidResult015}.Schedule();laneBuildReader015.Complete();
            laneStampCount015=laneSolidResult015[0];if(laneStampCount015<0){laneMaskOverflow015=true;return;}
            var hash=new LaneBuildHash015{Terrain=laneTerrain015,Stamps=laneStamps015,Count=laneStampCount015,Result=laneSignature015};
            laneBuildReader015=hash.Schedule();laneBuildReader015.Complete();
        }
        internal bool LaneBuildEnvelope015(int type,out float radius,out float top)
        {
            radius=LaneBuildMath015.SoleRadius;top=0;
            for(int tier=1;tier<=3;tier++)
            {if(!TowerModelClearanceProfiles.TryEnvelope015(type,tier,out float r,out _,out float hi))return false;radius=Mathf.Max(radius,r);top=Mathf.Max(top,hi);}
            return radius<=4&&top>0&&top<=12;
        }
        internal bool EvaluateLaneBuild015(Vector3 point,Vector3 feet,int lane,int type,int ignoredTower,bool enumerate,out LaneBuildReason015 reason)
        {
            reason=LaneBuildReason015.Unavailable;
            if(enumerate&&laneHighlightCount015.IsCreated){laneBuildReader015.Complete();laneHighlightCount015[0]=0;}
            if(LaneBuildSemantic015==0||!LaneBuildEnvelope015(type,out float radius,out float top))return false;
            var session=GameSession.Instance;if(session==null||session.World!=this||session.Defense==null)return false;
            laneBuildReader015.Complete();
            int count=session.Defense.GatherLaneBuildBodies015(laneBodies015);if(count<0)return false;
            var query=new LaneBuildTerrainQuery015{Terrain=laneTerrain015,Stamps=laneStamps015,Bodies=laneBodies015,StampCount=laneStampCount015,
                BodyCount=count,Point=new float3(point.x,point.y,point.z),Feet=new float3(feet.x,feet.y,feet.z),Lane=lane,
                EnvelopeRadius=radius,EnvelopeTop=top,IgnoreTower=(uint)Mathf.Max(0,ignoredTower),Output=lanePoint015};
            laneBuildReader015=query.Schedule();laneBuildReader015.Complete();reason=(LaneBuildReason015)lanePoint015[0].Reason;
            uint generation=CaptureXpClearance015();if(generation==0){reason=LaneBuildReason015.Blocked;return false;}
            if(enumerate)
            {
                laneBuildReader015=new LaneBuildEnumerate015{Query=query,Points=laneHighlights015,Count=laneHighlightCount015}.Schedule();
                laneBuildReader015.Complete();
                if(laneHighlightCount015[0]<0){reason=LaneBuildReason015.Capacity;return false;}
                // Complete the same structural predicate for every visible candidate.
                laneBuildReader015=ScheduleLaneBuildSolids015(laneHighlights015,laneHighlightCount015[0],radius,top,ignoredTower,generation,laneBuildReader015);
                laneBuildReader015.Complete();
            }
            if(reason!=LaneBuildReason015.Legal)return false;
            laneBuildReader015=ScheduleLaneBuildSolids015(lanePoint015,1,radius,top,ignoredTower,generation,laneBuildReader015);
            laneBuildReader015.Complete();reason=(LaneBuildReason015)lanePoint015[0].Reason;
            return reason==LaneBuildReason015.Legal&&generation==XpClearanceGeneration015;
        }
        JobHandle ScheduleLaneBuildSolids015(NativeArray<LaneBuildPoint16> points,int count,float radius,float top,int ignoredTower,uint generation,JobHandle dependency)
        {
            // Membership is captured cold by World. Only this exact foundation reference is support.
            int floor=-1;for(int i=0;i<xpSolidColliderCount015;i++)if(ReferenceEquals(xpSolidBoxes015[i],laneFoundation015)){floor=i;break;}
            return xpSolidReader015=new LaneBuildSolidJob015{Points=points,Count=count,Solids=xpSolidCurrent015,Cells=xpSolidCells015,
                GridStatus=xpSolidGridStatus015,Floor=floor,Radius=radius,Top=top,Generation=generation,
                IgnoredTower=ignoredTower,TowerIds=LaneBuildIgnoredSolid015(ignoredTower)}.Schedule(count,32,JobHandle.CombineDependencies(dependency,xpSolidReader015));
        }
        // Tower identity exclusion is supplied as its numeric solid slot; never pass a managed reference to jobs.
        int LaneBuildIgnoredSolid015(int id)
        {
            if(id<1||!PlacedElementShrine.TryGetRegistered(id,out var tower)||tower==null)return -1;
            ulong identity=EntityId.ToULong(tower.GetEntityId());
            for(int i=0;i<xpSolidTowerIds015.Length;i++)if(xpSolidTowerIds015[i]==identity)return XpSolidLimits015.Colliders+i;
            return -1;
        }
        void DisposeLaneBuild015()
        {
            laneBuildReader015.Complete();xpSolidReader015.Complete();laneCapturing015=laneRoadCapture015=false;
            if(laneWorldSubscribed015){LightPoolLifecycle.UnsubscribeBeforeReload(laneWorldReload015);laneWorldSubscribed015=false;}
            if(laneTerrain015.IsCreated)laneTerrain015.Dispose();if(laneStamps015.IsCreated)laneStamps015.Dispose();if(laneBodies015.IsCreated)laneBodies015.Dispose();
            if(lanePoint015.IsCreated)lanePoint015.Dispose();if(laneHighlights015.IsCreated)laneHighlights015.Dispose();if(laneHighlightCount015.IsCreated)laneHighlightCount015.Dispose();
            if(laneSolidResult015.IsCreated)laneSolidResult015.Dispose();if(laneSignature015.IsCreated)laneSignature015.Dispose();laneFoundation015=null;laneStampCount015=0;laneMaskOverflow015=false;
        }
    }
    [BurstCompile]
    struct LaneBuildReservations015:IJob
    {
        public NativeArray<LaneBuildStamp32> Stamps;public NativeArray<int> Result;public int Start;
        public void Execute()
        {
            if(Start<0||Start>Stamps.Length-10){Result[0]=-1;return;}
            Stamps[Start]=new LaneBuildStamp32{Half=new float2(4),Axis=new float2(1,0),Flags=LaneBuildMath015.Reserved};
            Stamps[Start+1]=new LaneBuildStamp32{Center=new float2(5.6f,-5.6f),Half=new float2(3),Axis=new float2(1,0),Flags=LaneBuildMath015.Reserved};
            for(int i=0;i<8;i++)Stamps[Start+2+i]=new LaneBuildStamp32{Center=LaneBuildMath015.Direction(i)*64,Half=new float2(5),Axis=new float2(1,0),Flags=LaneBuildMath015.Reserved};
            Result[0]=Start+10;
        }
    }
    [BurstCompile]
    struct LaneBuildAimOwned015:IJob
    {
        [ReadOnly]public NativeArray<LaneBuildBody32> Bodies;public int Count;public float3 Eye,Direction;public NativeArray<int> Result;
        public void Execute()
        {
            int id=0;float best=12.25f;
            for(int i=0;i<Count;i++){var b=Bodies[i];if(b.Kind!=3)continue;float3 delta=b.Center+new float3(0,1.4f,0)-Eye;float squared=math.lengthsq(delta);
                if(squared<=.001f||squared>best||math.dot(Direction,delta/math.sqrt(squared))<.82f||squared==best&&id>0&&b.Identity>=(uint)id)continue;best=squared;id=(int)b.Identity;}Result[0]=id;
        }
    }
    [BurstCompile]
    struct LaneBuildHash015:IJob
    {
        [ReadOnly]public NativeArray<LaneBuildTerrain8> Terrain;[ReadOnly]public NativeArray<LaneBuildStamp32> Stamps;
        public NativeArray<uint> Result;public int Count;
        public void Execute()
        {
            uint h=2166136261;for(int i=0;i<Terrain.Length;i++){h=(h^Terrain[i].Flags)*16777619;h=(h^math.asuint(Terrain[i].SupportY))*16777619;}
            for(int i=0;i<Count;i++){var s=Stamps[i];h=(h^math.asuint(s.Center.x))*16777619;h=(h^math.asuint(s.Center.y))*16777619;h=(h^math.asuint(s.Half.x))*16777619;h=(h^math.asuint(s.Half.y))*16777619;h=(h^math.asuint(s.Axis.x))*16777619;h=(h^math.asuint(s.Axis.y))*16777619;h=(h^s.Flags)*16777619;}Result[0]=h==0?1:h;
        }
    }
    [BurstCompile]
    struct LaneBuildSolidJob015:IJobParallelFor
    {
        public NativeArray<LaneBuildPoint16> Points;
        [ReadOnly]public NativeArray<XpSolid32> Solids;[ReadOnly]public NativeParallelMultiHashMap<ulong,int> Cells;
        [ReadOnly]public NativeArray<int> GridStatus;public int Count,Floor,IgnoredTower,TowerIds;public float Radius,Top;public uint Generation;
        public void Execute(int index)
        {
            if(index>=Count)return;var value=Points[index];if(value.Reason!=0)return;
            if(Generation==0||Floor<0||GridStatus[0]!=1){value.Reason=(uint)LaneBuildReason015.Blocked;Points[index]=value;return;}
            float3 low=value.Position+new float3(-Radius,0,-Radius),high=value.Position+new float3(Radius,Top,Radius);
            int3 a=(int3)math.floor(low/8),b=(int3)math.floor(high/8);int visits=0;
            for(int z=a.z;z<=b.z;z++)for(int y=a.y;y<=b.y;y++)for(int x=a.x;x<=b.x;x++)
            {
                if(!PoolXpMath015.TryMortonCoordinates(x,y,z,out ulong key)){value.Reason=(uint)LaneBuildReason015.Blocked;Points[index]=value;return;}
                if(!Cells.TryGetFirstValue(key,out int solid,out var cursor))continue;
                do
                {
                    if(++visits>XpSolidLimits015.Visits){value.Reason=(uint)LaneBuildReason015.Blocked;Points[index]=value;return;}
                    var box=Solids[solid];
                    if(solid==Floor&&box.Active==1&&box.Maximum.y==.001f&&math.all(low.xz>box.Minimum.xz)&&math.all(high.xz<box.Maximum.xz))continue;
                    if(solid==TowerIds&&IgnoredTower>0)continue;
                    if(box.Active!=1||math.all(high>box.Minimum)&&math.all(low<box.Maximum))
                    {value.Reason=(uint)LaneBuildReason015.Blocked;Points[index]=value;return;}
                }while(Cells.TryGetNextValue(out solid,ref cursor));
            }
            Points[index]=value;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual chosen1m ground cells, exact road/lava rectangle stamps and current unscaled world support/solid envelopes; no rendered-colour inference.
// Array Formats: Fixed Terrain8[65536],Stamp32[4096],Body32[54],Point16[8192+1], scalar result/hash; borrowed existing4104 solids/Morton grid, no competing geometry inventory.
// Dependencies/Pointers: World owns all buffers and both job fences; exact registered foundation alone is supporting floor. Complete before read/reuse/dispose. Host permissions/transactions remain Director-owned; unsupported/overflow/stale fails closed.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Y-up/XZ canonical placement roots and local active-play input lifetime; no terrain or host permission changes.
// Array Formats: Existing fixed preview records, scalar context/request state and cached HUD controls; no new runtime store or hot allocation.
// Dependencies/Pointers: Local commands require the current play/input owner. Highlight count is invalidated behind its existing job fence before refresh; host acceptance remains authoritative.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact registered arena foundation upper world plane; semantic identity and canonical hostY0 unchanged.
// Array Formats: One borrowed BoxCollider bounds read outside numeric loops; no added store, job, allocation or terrain authority.
// Dependencies/Pointers: Read-only adapter rejects disabled/tilted/non-finite/unexpected-height foundation; existing full-footprint and host owners remain authoritative.
