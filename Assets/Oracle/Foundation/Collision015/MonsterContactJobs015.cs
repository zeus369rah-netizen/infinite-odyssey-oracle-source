using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace OdysseyOracle.Foundation.Collision015
{
    [BurstCompile]
    public struct BuildMonsterSnapshot015 : IJob
    {
        [ReadOnly] public NativeArray<MonsterRawPose80> Raw;
        public NativeArray<MonsterTarget64> Targets;
        public NativeParallelMultiHashMap<ulong,int> Cells;
        public NativeArray<double> MaximumBound;
        public NativeArray<int> Status;
        public void Execute()
        {
            Status[0]=0;MaximumBound[0]=0;Cells.Clear();
            if(Raw.Length!=48||Targets.Length!=48){Status[0]=2;return;}
            for(int i=0;i<48;i++)
            {
                MonsterRawPose80 p=Raw[i];Targets[i]=default;
                if(!MonsterContactMath015.TryBuild(p,out MonsterTarget64 t)){Status[0]=2;return;}
                if(t.Flags==0)continue;
                // Uniqueness is checked ONCE per published snapshot, not inside every target consideration.
                for(int j=0;j<i;j++)if(Targets[j].Flags!=0&&Targets[j].StableId==t.StableId){Status[0]=2;return;}
                Targets[i]=t;MaximumBound[0]=Math.Max(MaximumBound[0],MonsterContactMath015.Bound(t));
                if(!MonsterContactMath015.Morton((int)Math.Floor(t.X/2d),(int)Math.Floor(t.Y/2d),(int)Math.Floor(t.Z/2d),out ulong key)){Status[0]=2;return;}
                Cells.Add(key,i);
            }
            Status[0]=1;
        }
    }

    [BurstCompile]
    public struct SweepMonsters015 : IJob
    {
        [ReadOnly] public NativeArray<MonsterTarget64> Targets;
        [ReadOnly] public NativeParallelMultiHashMap<ulong,int> Cells;
        [ReadOnly] public NativeArray<double> MaximumBound;
        [ReadOnly] public NativeArray<int> SnapshotStatus;
        [ReadOnly] public NativeArray<MonsterQuery80> Queries;
        public NativeArray<MonsterResult64> Results;
        public void Execute()
        {
            for(int qIndex=0;qIndex<Queries.Length;qIndex++)
            {
                MonsterQuery80 q=Queries[qIndex];
                MonsterResult64 best=new MonsterResult64{Kind=MonsterContactMath015.Miss,SnapshotGeneration=q.SnapshotGeneration,Sequence=q.Sequence};
                if(SnapshotStatus[0]!=1||Targets.Length!=48||Results.Length!=Queries.Length||!MonsterContactMath015.QueryValid(q))
                {best.Kind=MonsterContactMath015.Invalid;Results[qIndex]=best;continue;}
                double expand=MaximumBound[0]+q.Radius;
                int minX=(int)Math.Floor((Math.Min(q.X,q.EndX)-expand)/2d),maxX=(int)Math.Floor((Math.Max(q.X,q.EndX)+expand)/2d);
                int minY=(int)Math.Floor((Math.Min(q.Y,q.EndY)-expand)/2d),maxY=(int)Math.Floor((Math.Max(q.Y,q.EndY)+expand)/2d);
                int minZ=(int)Math.Floor((Math.Min(q.Z,q.EndZ)-expand)/2d),maxZ=(int)Math.Floor((Math.Max(q.Z,q.EndZ)+expand)/2d);
                long count=(long)(maxX-minX+1)*(maxY-minY+1)*(maxZ-minZ+1);
                if(count>MonsterContactMath015.MaximumCells)
                {
                    for(int i=0;i<48;i++)Consider(q,i,ref best);
                }
                else
                {
                    for(int x=minX;x<=maxX;x++)for(int y=minY;y<=maxY;y++)for(int z=minZ;z<=maxZ;z++)
                    {
                        if(!MonsterContactMath015.Morton(x,y,z,out ulong key)){best.Kind=MonsterContactMath015.Invalid;break;}
                        if(!Cells.TryGetFirstValue(key,out int slot,out var iterator))continue;
                        int visited=0;
                        do{if(++visited>48){best.Kind=MonsterContactMath015.Invalid;break;}Consider(q,slot,ref best);}
                        while(Cells.TryGetNextValue(out slot,ref iterator));
                    }
                }
                Results[qIndex]=best;
            }
        }
        void Consider(in MonsterQuery80 q,int slot,ref MonsterResult64 best)
        {
            if(best.Kind==MonsterContactMath015.Invalid)return;
            if(slot<0||slot>=48){best.Kind=MonsterContactMath015.Invalid;return;}
            MonsterTarget64 t=Targets[slot];if(t.Flags==0)return;
            if(MonsterContactMath015.TryHit(q,t,slot,out MonsterResult64 hit)&&MonsterContactMath015.Earlier(hit,best))best=hit;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Validated world-space center keys and exact local contact math; Morton63 signed3D cells at2metres.
// Array Formats: Owned48raw/48target/map48, one bound/status; query/result native arrays; all storage leased through the final job fence.
// Dependencies/Pointers: Numeric Burst IJob publishers/readers only; fixed4096cell query budget falls back to48 numeric records. No Physics/Object/damage calls, dynamic capacity or allocation.
