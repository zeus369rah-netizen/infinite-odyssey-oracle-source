using OdysseyOracle.Foundation.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.Foundation.Presentation
{
    public static class RefugeTrailMath
    {
        public const int MaximumMarkers=96,VerticesPerMarker=35,IndicesPerMarker=51;
        public static float Lane(int station)=>-2.375f+station*.25f;
        public static bool ValidEndpoint(int station,float2 endpoint)=>station>=0&&station<4&&math.all(math.isfinite(endpoint))&&
            math.all(math.abs(endpoint)<=32f)&&endpoint.y>=-9f;
        public static float4 Sample(int station,float2 endpoint,float distance)
        {
            float lane=Lane(station),vertical=endpoint.y+10f;
            if(distance<vertical)return new float4(lane,-10f+distance,0,1);
            float sign=endpoint.x>=lane?1f:-1f;
            return new float4(lane+sign*(distance-vertical),endpoint.y,sign,0);
        }
        public static float2 Outline(int i)
        {
            switch(i){case 0:return new float2(-.04f,-.17f);case 1:return new float2(.04f,-.17f);
                case 2:return new float2(.04f,0);case 3:return new float2(.08f,0);case 4:return new float2(0,.17f);
                case 5:return new float2(-.08f,0);default:return new float2(-.04f,0);}
        }
        public static float3 Point(float4 frame,float2 local,float surface,float height)
        {
            float2 forward=frame.zw,right=new float2(forward.y,-forward.x);
            float2 point=frame.xy+right*local.x+forward*local.y;
            return new float3(point.x,surface+height,point.y);
        }
    }
    [BurstCompile]
    public struct RefugeTrailLayoutJob:IJob
    {
        public int Station;public float2 Endpoint;
        [WriteOnly] public NativeArray<float4> Frames;
        [WriteOnly] public NativeArray<RefugeFloorQuery32> Queries;
        [WriteOnly] public NativeArray<int> Count;
        public void Execute()
        {
            Count[0]=0;if(!RefugeTrailMath.ValidEndpoint(Station,Endpoint))return;
            float length=Endpoint.y+10f+math.abs(Endpoint.x-RefugeTrailMath.Lane(Station));
            int count=(int)math.floor(length/.65f)+1;if(count<1||count>RefugeTrailMath.MaximumMarkers)return;
            for(int i=0;i<count;i++)
            {
                float4 frame=RefugeTrailMath.Sample(Station,Endpoint,i*.65f);Frames[i]=frame;
                Queries[i]=new RefugeFloorQuery32{Origin=new float3(frame.x,2,frame.y),MaximumDistance=4,
                    HalfFootprintXZ=new float2(.19f),Flags=RefugeFloorSupportMath.CheckFootprintCorners,QueryId=(uint)i};
            }
            Count[0]=count;
        }
    }
    [BurstCompile]
    public struct RefugeTrailMeshJob:IJob
    {
        [ReadOnly] public NativeArray<float4> Frames;
        [ReadOnly] public NativeArray<RefugeFloorResult32> Supports;
        public NativeList<float3> Vertices,Normals;
        public NativeList<float2> Uvs;
        public NativeList<int> Indices;
        [WriteOnly] public NativeArray<int> Valid;
        float3 World(int marker,float2 local,float height)
        {
            return RefugeTrailMath.Point(Frames[marker],local,Supports[marker].Position.y,height);
        }
        void Vertex(float3 point,float3 normal,float2 uv){Vertices.Add(point);Normals.Add(normal);Uvs.Add(uv);}
        void Triangle(int a,int b,int c){Indices.Add(a);Indices.Add(b);Indices.Add(c);}
        public void Execute()
        {
            Valid[0]=0;
            if(Supports.Length<1||Supports.Length>RefugeTrailMath.MaximumMarkers||Frames.Length<Supports.Length)return;
            for(int i=0;i<Supports.Length;i++)if(Supports[i].Status!=1u||Supports[i].QueryId!=(uint)i||Supports[i].Position.y!=0f)return;
            for(int marker=0;marker<Supports.Length;marker++)
            {
                int first=Vertices.Length;
                for(int i=0;i<7;i++){float2 p=RefugeTrailMath.Outline(i);Vertex(World(marker,p,.012f),new float3(0,1,0),p);}
                Triangle(first,first+2,first+1);Triangle(first,first+6,first+2);Triangle(first+3,first+5,first+4);
                for(int i=0;i<7;i++)
                {
                    float2 a=RefugeTrailMath.Outline(i),b=RefugeTrailMath.Outline((i+1)%7);float3 lowA=World(marker,a,0),lowB=World(marker,b,0);
                    float3 edge=lowB-lowA,normal=math.normalize(new float3(edge.z,0,-edge.x));int side=Vertices.Length;
                    Vertex(lowA,normal,new float2(0,0));Vertex(lowB,normal,new float2(1,0));
                    Vertex(lowB+new float3(0,.012f,0),normal,new float2(1,1));Vertex(lowA+new float3(0,.012f,0),normal,new float2(0,1));
                    Triangle(side,side+2,side+1);Triangle(side,side+3,side+2);
                }
            }
            Valid[0]=1;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original shallow arrow prisms in shared refuge metres; exact support vertexY0 is their bottom, topY.012 is physical thickness, no hidden floor-coplanar bottom faces.
// Array Formats: bounded96 float4 frames/query32/result32, one count/valid; preallocated NativeList position12/normal12/uv8/index4,35verts/51indices per marker maximum.
// Dependencies/Pointers: caller-owned cold buffers; support job fence precedes mesh, all readers complete before disposal. No UnityEngine.Object API, new World authority or per-frame allocation; original procedural geometry, no external art/audio.
