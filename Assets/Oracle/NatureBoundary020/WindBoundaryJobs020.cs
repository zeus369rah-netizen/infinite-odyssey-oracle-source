using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace OracleNatureBoundary020
{
    [BurstCompile]
    public unsafe struct WindBoundaryBuildJob020:IJob
    {
        [ReadOnly]public NativeArray<BoundaryQuad020> Quads;
        public NativeArray<BoundaryEdge020> Edges;
        public NativeArray<BoundaryNode020> Nodes;
        public NativeArray<double> Cuts;
        public NativeArray<BoundaryPoint020> Points;
        public NativeArray<BoundaryHeader020> Header;
        public BoundaryScope020 Scope;
        public void Execute()
        {
            if(Header.Length!=1)return;
            Header[0]=new BoundaryHeader020{Scope=Scope,Status=BoundaryStatus020.Input};
            if(Quads.Length<1||Quads.Length>WindBoundaryMath020.Quads||Edges.Length!=WindBoundaryMath020.Edges||
                Nodes.Length!=WindBoundaryMath020.Nodes||Cuts.Length!=WindBoundaryMath020.Cuts||Points.Length!=WindBoundaryMath020.Points)return;
            WindBoundaryMath020.Build((BoundaryQuad020*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Quads),Quads.Length,
                (BoundaryEdge020*)NativeArrayUnsafeUtility.GetUnsafePtr(Edges),Edges.Length,
                (BoundaryNode020*)NativeArrayUnsafeUtility.GetUnsafePtr(Nodes),Nodes.Length,
                (double*)NativeArrayUnsafeUtility.GetUnsafePtr(Cuts),(BoundaryPoint020*)NativeArrayUnsafeUtility.GetUnsafePtr(Points),
                in Scope,out var header);
            Header[0]=header;
        }
    }
    [BurstCompile]
    public unsafe struct WindBoundaryNearestJob020:IJobParallelFor
    {
        [ReadOnly]public NativeArray<BoundaryHeader020> Header;
        [ReadOnly]public NativeArray<BoundaryEdge020> Edges;
        [ReadOnly]public NativeArray<BoundaryNode020> Nodes;
        [ReadOnly]public NativeArray<BoundaryQuery020> Queries;
        public NativeArray<BoundaryResult020> Results;
        public void Execute(int index)
        {
            if(index<0||index>=Results.Length)return;
            var failure=new BoundaryResult020{Status=BoundaryStatus020.Input};
            if(Header.Length!=1||Edges.Length!=WindBoundaryMath020.Edges||Nodes.Length!=WindBoundaryMath020.Nodes||
                Queries.Length<1||Queries.Length>2||Results.Length!=Queries.Length||index>=Queries.Length){Results[index]=failure;return;}
            var query=Queries[index];failure.Scope=query.Scope;failure.Player=query.Player;failure.Step=query.Step;
            for(int i=0;i<Queries.Length;i++)if(i!=index&&Queries[i].Accepted==1&&Queries[i].Player==query.Player){Results[index]=failure;return;}
            var header=Header[0];
            Results[index]=WindBoundaryMath020.Nearest((BoundaryEdge020*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Edges),
                (BoundaryNode020*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Nodes),in header,in query);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Value-only accepted scope tokens, exact World-XZ boundary segments and at most2actual player/listener positions.
// Array Formats: Fixed69/512/1023/140/1024/1cold NativeArrays and1..2query/result arrays;32entry stack inside numeric search.
// Dependencies/Pointers: No UnityEngine.Object calls, allocations or disposal in jobs. Root owns source reference admission, staging/live isolation, scheduling, fencing, failure quarantine and actual local-listener audio dispatch; source compilation is not Burst/native execution proof.
