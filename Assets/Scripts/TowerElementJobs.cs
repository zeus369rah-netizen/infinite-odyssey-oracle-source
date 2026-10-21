using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [BurstCompile]
    public struct TowerElementMeshJob:IJob
    {
        public TowerElementRecipe64 Recipe;
        public uint Shot;
        public NativeArray<TowerElementVertex32> Vertices;
        public NativeArray<ushort> Indices;
        public NativeArray<int2> Counts;
        int v,t;
        public void Execute()
        {
            v=t=0;
            if(Shot!=0)
            {
                Octa(10,0);for(int i=0;i<6;i++)Quad(11,i,0,1);
                for(int i=0;i<8;i++)Cube(12,i);for(int i=0;i<6;i++)Quad(13,i,0,1);
            }
            else if(Recipe.Type==0)
            {
                for(int i=0;i<Recipe.Detail.x;i++)Octa(3,i);
                for(int i=0;i<Recipe.Detail.y;i++)Octa(6,i);
                for(int i=0;i<Recipe.Detail.z;i++)Octa(4,i);
                for(int i=0;i<Recipe.Detail.w;i++)Quad(5,i,0,1);
            }
            else if(Recipe.Type==1)
            {
                for(int stream=0;stream<Recipe.Detail.x;stream++)for(int segment=0;segment<8;segment++)Quad(0,stream,segment,8);
                for(int i=0;i<Recipe.Detail.y;i++)Octa(7,i);
            }
            else if(Recipe.Type==2)
            {for(int i=0;i<Recipe.Detail.y;i++){Cube(2,i);Quad(9,i,0,1);}}
            else
            {
                for(int band=0;band<Recipe.Detail.x;band++)for(int segment=0;segment<16;segment++)Quad(1,band,segment,16);
                for(int i=0;i<Recipe.Detail.y;i++)Cube(8,i);
            }
            Counts[0]=new int2(v,t);
        }
        void Push(float3 point,float2 uv,int kind,int id){Vertices[v++]=new TowerElementVertex32{Position=point,UV=uv,Tag=new float3(kind,id,id*.713f+.31f)};}
        void Tri(int a,int b,int c){Indices[t++]=(ushort)a;Indices[t++]=(ushort)b;Indices[t++]=(ushort)c;}
        void Quad(int kind,int id,int segment,int segments)
        {
            int start=v;float low=(float)segment/segments,high=(float)(segment+1)/segments;
            Push(new float3(-.5f,low,0),new float2(0,low),kind,id);Push(new float3(.5f,low,0),new float2(1,low),kind,id);
            Push(new float3(.5f,high,0),new float2(1,high),kind,id);Push(new float3(-.5f,high,0),new float2(0,high),kind,id);
            Tri(start,start+1,start+2);Tri(start,start+2,start+3);Tri(start,start+2,start+1);Tri(start,start+3,start+2);
        }
        void Octa(int kind,int id)
        {
            int start=v;Push(new float3(0,1,0),new float2(.5f,1),kind,id);Push(new float3(0,-1,0),new float2(.5f,0),kind,id);
            Push(new float3(1,0,0),new float2(1,.5f),kind,id);Push(new float3(0,0,1),new float2(.5f,.5f),kind,id);Push(new float3(-1,0,0),new float2(0,.5f),kind,id);Push(new float3(0,0,-1),new float2(.5f,.5f),kind,id);
            for(int i=0;i<4;i++){int a=start+2+i,b=start+2+(i+1)%4;Tri(start,b,a);Tri(start+1,a,b);}
        }
        void Cube(int kind,int id)
        {
            for(int face=0;face<6;face++)
            {
                int start=v;
                for(int corner=0;corner<4;corner++)
                {
                    float a=corner==0||corner==3?-.5f:.5f,b=corner<2?-.5f:.5f;float3 point;
                    switch(face){case 0:point=new float3(.5f,a,b);break;case 1:point=new float3(-.5f,a,-b);break;case 2:point=new float3(a,.5f,-b);break;case 3:point=new float3(a,-.5f,b);break;case 4:point=new float3(a,b,.5f);break;default:point=new float3(-a,b,-.5f);break;}
                    Push(point,new float2((a+.5f),(b+.5f)),kind,id);
                }
                Tri(start,start+1,start+2);Tri(start,start+2,start+3);
            }
        }
    }
    [BurstCompile]
    public struct TowerElementStepJob:IJob
    {
        public NativeArray<TowerElementClock32> State;
        public NativeArray<TowerElementPose64> Pose;
        public TowerElementRecipe64 Recipe;
        public float Delta,AmbientDelta;
        public float3 Origin,Hit;
        public float AcceptedRadius;
        public void Execute()
        {
            var state=State[0];state.Clock=math.fmod(state.Clock+AmbientDelta,4096);state.Age=math.min(10,state.Age+Delta);
            float pulse=state.Age<.24f?math.sin(state.Age/.24f*math.PI):0;
            float3 min=new float3(-.95f,-.1f,-.95f),max=new float3(.95f,2.3f,.95f);
            if(state.Serial!=0&&state.Age<state.Duration)
            {
                float extent=math.max(.55f,AcceptedRadius+.1f);min=math.min(min,math.min(Origin,Hit)-extent);max=math.max(max,math.max(Origin,Hit)+extent);
            }
            Pose[0]=new TowerElementPose64{Time=new float4(state.Clock,state.Age,state.Travel,pulse),Tint=new float4(Recipe.Shape.w,Recipe.Shape.w,Recipe.Shape.w,1),Minimum=new float4(min,0),Maximum=new float4(max,0)};State[0]=state;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure Head-local normalized geometry and explicit clock/accepted local shot endpoints. No engine/Object/Physics API in any job or loop.
// Array Formats: Burst cold Mesh IJob writes Vertex32[512]/ushort[1024]/int2[1]; Step IJob owns Clock32[1]/Pose64[1] and scalar recipe/endpoints. Fixed caps cover all12 recipes.
// Dependencies/Pointers: Library/instance owners complete their JobHandle before Mesh upload, MPB/bounds writes, serial acceptance or dispose. Cosmetic bounds use the actual accepted area radius only; jobs never inflict damage or retarget.
