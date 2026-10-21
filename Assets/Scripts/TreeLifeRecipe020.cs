using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TreeLifeVertex020
    {
        public float3 Position,Normal;
        public float4 Color;
        public float2 UV;
        public float4 Anchor;
    }
    public static class TreeLifeRecipe020
    {
        public const int MaximumVertices=6000,MaximumIndices=9000,VertexStride=64;
        public const float Scale=3f,RootRadius=4.25f,MaximumCrownRadius=22f,WindExpansion=.8f;
        public static void Counts(int lod,out int trunk,out int roots,out int branches,out int canopy,out int cards)
        {
            trunk=lod==2?5:7;roots=lod==0?8:lod==1?6:4;branches=lod==0?12:lod==1?8:4;
            canopy=lod==0?128:lod==1?64:24;cards=lod==0?384:lod==1?128:24;
        }
    }
    [BurstCompile]
    public struct TreeLifeMeshJob020:IJob
    {
        public float FinalHeight;
        public int Lod;
        public uint Seed;
        public NativeArray<TreeLifeVertex020> Vertices;
        public NativeArray<int> Indices,Written;
        int vertex,index;bool overflow;
        static uint Mix(uint v){unchecked{v^=v>>16;v*=0x7feb352du;v^=v>>15;v*=0x846ca68bu;return v^(v>>16);}}
        float Random(int slot)=>((Mix(unchecked(Seed+(uint)slot*747796405u))>>8)&65535u)/65535f;
        static float4 Leaves(float angle,int tier)=>TreeLifeTownFloraContribution020.CanopyColor(angle,tier);
        void Point(float3 p,float3 n,float4 color,float2 uv,float3 anchor,float mask)
        {
            // This is the single3x conversion; the owning root remains unit scale.
            Vertices[vertex++]=new TreeLifeVertex020{Position=p*TreeLifeRecipe020.Scale,Normal=n,Color=color,UV=uv,Anchor=new float4(anchor*TreeLifeRecipe020.Scale,mask)};
        }
        void Quad(int start){Indices[index++]=start;Indices[index++]=start+1;Indices[index++]=start+2;Indices[index++]=start;Indices[index++]=start+2;Indices[index++]=start+3;}
        void Box(float3 center,float3 size,quaternion rotation,float4 color,float mask)
        {
            if(vertex+24>Vertices.Length||index+36>Indices.Length){overflow=true;return;}
            float3 half=size*.5f;
            for(int face=0;face<6;face++)
            {
                int axis=face/2;float sign=(face&1)==0?1f:-1f;
                float3 n=axis==0?new float3(sign,0,0):axis==1?new float3(0,sign,0):new float3(0,0,sign);
                float3 u=axis==0?new float3(0,1,0):axis==1?new float3(0,0,1):new float3(1,0,0),v=math.cross(n,u);
                float3 middle=center+math.rotate(rotation,n*math.dot(math.abs(n),half));int start=vertex;
                for(int corner=0;corner<4;corner++)
                {
                    float x=corner==0||corner==3?-1f:1f,y=corner<2?-1f:1f;
                    Point(middle+math.rotate(rotation,u*(math.dot(math.abs(u),half)*x)+v*(math.dot(math.abs(v),half)*y)),math.rotate(rotation,n),color,new float2(-1,0),center,mask);
                }
                Quad(start);
            }
        }
        void Beam(float3 a,float3 b,float width,float4 color)
        {float3 delta=b-a;Box((a+b)*.5f,new float3(width,width,math.length(delta)),quaternion.LookRotationSafe(delta,new float3(0,1,0)),color,0);}
        void Card(float3 anchor,float width,float height,float yaw,float4 color)
        {
            if(vertex+4>Vertices.Length||index+6>Indices.Length){overflow=true;return;}
            float3 right=new float3(math.cos(yaw),0,-math.sin(yaw)),up=new float3(0,1,0),normal=math.cross(right,up);int start=vertex;
            Point(anchor-right*width*.5f,normal,color,new float2(0,0),anchor,0);
            Point(anchor+right*width*.5f,normal,color,new float2(1,0),anchor,0);
            Point(anchor+right*width*.5f+up*height,normal,color,new float2(1,1),anchor,1);
            Point(anchor-right*width*.5f+up*height,normal,color,new float2(0,1),anchor,1);Quad(start);
        }
        public void Execute()
        {
            Written[0]=Written[1]=-1;vertex=index=0;overflow=false;
            if(!math.isfinite(FinalHeight)||FinalHeight<36||FinalHeight>48||Lod<0||Lod>2)return;
            float h=FinalHeight/TreeLifeRecipe020.Scale;
            TreeLifeRecipe020.Counts(Lod,out int trunks,out int roots,out int branches,out int canopy,out int cards);
            float4 bark=new float4(.16f,.105f,.05f,1);
            for(int i=0;i<trunks;i++)
            {
                float t=i/(float)(trunks-1),length=h*.7f/trunks,radius=h*.035f*(1f-.6f*t);
                Box(new float3(math.sin(i*1.2f)*h*.012f,(i+.5f)*length,0),new float3(radius*2,length+.025f,radius*1.65f),quaternion.identity,bark,0);
            }
            for(int i=0;i<roots;i++)
            {
                float a=i*(math.PI*2/roots);float3 end=new float3(math.cos(a)*3.65f,.38f,math.sin(a)*3.65f)/3f;
                Beam(new float3(0,.85f,0)/3f,end,.72f/3f,bark);
            }
            for(int i=0;i<branches;i++)
            {
                float a=i*(math.PI*2/branches)+.3f,r=h*(.13f+Random(i+1)*.08f);
                Beam(new float3(0,h*(.35f+.022f*(i%6)),0),new float3(math.cos(a)*r,h*(.62f+.02f*(i%4)),math.sin(a)*r),h*.018f,bark);
            }
            for(int i=0;i<canopy;i++)
            {
                int layer=i%4;float a=i*2.39996323f,r=h*.27f*(.35f+.65f*Random(i+31))*(layer==3?.55f:1f);
                float3 center=new float3(math.cos(a)*r,h*(.60f+layer*(.3625f/3f)),math.sin(a)*r);
                float w=h*.09f*(.8f+.4f*Random(i+173));
                Box(center,new float3(w,h*.075f,w*(.75f+.30f*Random(i+301))),quaternion.RotateY((i%3-1)*.16f),Leaves(a,i),.3f);
            }
            int sprayAnchors=cards/2;
            for(int i=0;i<sprayAnchors;i++)
            {
                float a=i*2.39996323f,r=h*.34f*(.35f+.65f*Random(i+701));
                float3 anchor=new float3(math.cos(a)*r,h*(.59f+.35f*Random(i+1103)),math.sin(a)*r);
                float width=h*.045f,height=h*.035f*(.7f+.6f*Random(i+1409));
                float4 color=Leaves(a,i+1);
                Card(anchor,width,height,a,color);
                Card(anchor,width,height,a+math.PI*.5f,color);
            }
            if(!TreeLifeTownFloraContribution020.Append(Lod,Seed,Vertices,Indices,ref vertex,ref index))overflow=true;
            if(!overflow){Written[0]=vertex;Written[1]=index;}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original metre root; final36..48m height divided by3 in numeric recipe and multiplied exactly once at emitted position/anchor; rigid root transform stays unit.
// Array Formats:64B Position/Normal/Color/UV/Anchor vertices, fixed6000vertices/9000indices/2written counters; combined LOD0/1/2=5960/2904/1160vertices and2980/1452/580triangles. Existing 384/128/24 leaf cards remain, paired into crossed sprays; one berm,24/12/8 flowers and four brush tufts share each mesh.
// Dependencies/Pointers: one cold completed Burst job per LOD; lease owns native scratch/immutable meshes/shared material. Physics owns accepted root reserve/support; GPU deforms only canopy, flower and brush masks. This candidate is work-only; native import, visual clearance and live collision behavior remain unverified.
