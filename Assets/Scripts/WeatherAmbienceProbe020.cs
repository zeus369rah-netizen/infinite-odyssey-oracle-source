// PSEUDOCODE: produce one upward/fan ray request with a Morton cell and exact scope; native collision stays outside the job.
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    public struct WeatherAmbienceProbe020
    {
        public WeatherAmbienceScope020 Scope;
        public float3 Origin,Direction;
        public uint Morton,Sequence;
        public int Height,Sample;
        public byte Valid;
    }
    [BurstCompile]
    public struct WeatherAmbienceProbeJob020:IJob
    {
        public NativeArray<WeatherAmbienceProbe020> Result;
        public WeatherAmbienceScope020 Scope;
        public float3 Position;
        public uint Sequence;
        public int Sample;
        public void Execute()
        {
            var item=new WeatherAmbienceProbe020{Scope=Scope,Sequence=Sequence,Sample=Sample};
            if(!Scope.Valid||Sequence==0||Sample<0||Sample>2||!math.all(math.isfinite(Position))||
                Position.x< -128||Position.x>=128||Position.z< -128||Position.z>=128||Position.y< -64||Position.y>128){Result[0]=item;return;}
            uint x=(uint)math.floor((Position.x+128)*.5f),z=(uint)math.floor((Position.z+128)*.5f),morton=0;
            for(int i=0;i<7;i++){morton|=((x>>i)&1u)<<(2*i);morton|=((z>>i)&1u)<<(2*i+1);}
            item.Morton=morton;item.Height=(int)math.floor(Position.y);
            item.Origin=Position+new float3(0,.12f,0);
            item.Direction=Sample==0?new float3(0,1,0):Sample==1?math.normalize(new float3(.28f,1,.18f)):math.normalize(new float3(-.28f,1,-.18f));
            item.Valid=1;Result[0]=item;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World ray origins and2m XZ Morton cells,1m height bands.
// Array Formats: One persistent NativeArray value request; pure Burst job with fixed7bit interleave.
// Dependencies/Pointers: No Unity Object/Physics/job allocation. Main-thread owned adapter validates scope before and after every actual ray.
