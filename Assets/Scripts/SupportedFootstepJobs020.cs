// PSEUDOCODE: cold-sort72owned collider metadata rows; at each real support hit binary-partition Morton keys and validate exact token/shape/plane in a pure job.
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace OdysseyOracle
{
    public struct SupportedStepRow020
    {
        public ulong Collider;
        public uint Key,Morton;
        public float3 Minimum,Maximum;
        public quaternion Rotation;
        public byte Wood,Flight;
    }
    public struct SupportedStepQuery020
    {
        public SupportedStepScope020 Scope;
        public ulong Collider;
        public uint Sequence;
        public float3 Point,Center,Size,Feet;
        public quaternion Rotation;
        public float NormalY;
    }
    public struct SupportedStepResult020
    {
        public SupportedStepScope020 Scope;
        public ulong Collider;
        public uint Sequence,Key;
        public float Top;
        public byte Valid,Wood,Flight;
    }
    [BurstCompile]
    public struct SupportedStepSortJob020:IJob
    {
        public NativeArray<SupportedStepRow020> Rows;
        public void Execute()
        {
            for(int i=1;i<Rows.Length;i++)
            {var value=Rows[i];int j=i-1;while(j>=0&&(Rows[j].Morton>value.Morton||Rows[j].Morton==value.Morton&&Rows[j].Key>value.Key)){Rows[j+1]=Rows[j];j--;}Rows[j+1]=value;}
        }
    }
    [BurstCompile]
    public struct SupportedStepQueryJob020:IJob
    {
        [ReadOnly]public NativeArray<SupportedStepRow020> Rows;
        public NativeArray<SupportedStepResult020> Result;
        public SupportedStepQuery020 Query;
        public void Execute()
        {
            var result=new SupportedStepResult020{Scope=Query.Scope,Collider=Query.Collider,Sequence=Query.Sequence};
            if(Rows.Length!=SupportedFootstepRules020.Capacity||!Query.Scope.Valid||Query.Sequence==0||Query.Collider==0||
                !math.all(math.isfinite(Query.Point))||!math.all(math.isfinite(Query.Feet))||!math.all(math.isfinite(Query.Center))||
                !math.all(math.isfinite(Query.Size))||!math.all(math.isfinite(Query.Rotation.value))||math.abs(math.lengthsq(Query.Rotation.value)-1)>.0001f||
                !math.isfinite(Query.NormalY)||Query.NormalY<.75f||Query.NormalY>1.0001f||math.abs(Query.Feet.y-Query.Point.y)>.18f||
                math.abs(Query.Feet.x-Query.Point.x)>.005f||math.abs(Query.Feet.z-Query.Point.z)>.005f||
                !PortalLandingLayout020.TryMorton(Query.Center.x,Query.Center.z,out uint morton))
            {Result[0]=result;return;}
            int low=0,high=Rows.Length;
            while(low<high){int middle=(low+high)>>1;if(Rows[middle].Morton<morton)low=middle+1;else high=middle;}
            for(int i=low;i<Rows.Length&&Rows[i].Morton==morton;i++)
            {
                var row=Rows[i];
                if(row.Collider!=Query.Collider||!math.all(math.isfinite(row.Minimum))||!math.all(math.isfinite(row.Maximum))||!math.all(math.isfinite(row.Rotation.value))||
                    math.any(row.Maximum<=row.Minimum)||math.abs(math.lengthsq(row.Rotation.value)-1)>.0001f||math.cmax(math.abs((row.Minimum+row.Maximum)*.5f-Query.Center))>.0001f||
                    math.cmax(math.abs(row.Maximum-row.Minimum-Query.Size))>.0001f||math.abs(math.dot(row.Rotation.value,Query.Rotation.value))<.999999f||
                    math.abs(Query.Point.y-row.Maximum.y)>.005f)continue;
                float3 center=(row.Minimum+row.Maximum)*.5f,local=math.rotate(math.conjugate(row.Rotation),Query.Point-center);
                float3 half=(row.Maximum-row.Minimum)*.5f;float inset=row.Wood==1?.025f:.001f;
                if(math.abs(local.x)>half.x-inset||math.abs(local.z)>half.z-inset)continue;
                result.Key=row.Key;result.Top=row.Maximum.y;result.Wood=row.Wood;result.Flight=row.Flight;result.Valid=1;break;
            }
            Result[0]=result;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Immutable current world identity and exact native support hit versus authored collider plane.
// Array Formats:72Morton-ordered blittable rows, one private query/result value and fixed binary partition.
// Dependencies/Pointers: Pure Burst jobs contain no Unity Object/Physics/audio; main-thread adapter must recheck exact current scope after the fence before committing a cue.
