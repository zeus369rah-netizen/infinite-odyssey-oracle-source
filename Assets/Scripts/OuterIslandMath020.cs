using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential)] public struct OuterIslandLayout020
    {
        public float3 Center,Extents;
        public float Ground;
        public float3 BoundsCenter,BoundsExtents;
    }
    [StructLayout(LayoutKind.Sequential)] public struct OuterIslandVertex020
    {
        public float3 Position,Normal;
        public uint Color;
    }
    [StructLayout(LayoutKind.Sequential)] public struct OuterIslandBounds020
    {
        public float3 Center,Extents;
    }
    [StructLayout(LayoutKind.Sequential)] public struct OuterIslandInput020
    {
        public float3 Center,Extents;
        public float4x4 Matrix;
    }
    public static class OuterIslandMath020
    {
        public const int Pieces=6,Sections=12,Faces=5;
        public const int VertexCount=Pieces*(Sections*Faces*4+Faces*6);
        public const int IndexCount=Pieces*(Sections*Faces*6+Faces*6);
        public const float Gap=10f,MaximumWidth=24f,HalfAngle=22f;

        public static bool TryLayout(float3 center,float3 extents,float ground,out OuterIslandLayout020 value)
        {
            value=default;
            if(!math.all(math.isfinite(center))||!math.all(math.isfinite(extents))||!math.isfinite(ground)||
                math.any(extents<0)||math.cmax(math.abs(center))>2048f||math.abs(ground)>2048f||
                math.cmin(extents.xz)<16f||math.cmax(extents.xz)>128f)return false;
            float outer=math.length(extents.xz)+Gap+.25f+MaximumWidth+.025f;
            value=new OuterIslandLayout020{Center=center,Extents=extents,Ground=ground,
                BoundsCenter=new float3(center.x,ground-4.5f,center.z),
                BoundsExtents=new float3(outer,11f,outer)};
            return true;
        }
        public static float3 Point(OuterIslandLayout020 value,int piece,int section,int face)
        {
            float phase=section*(1f/Sections),angle=math.radians(15f+piece*60f-HalfAngle+phase*(HalfAngle*2f));
            float2 direction=new float2(math.cos(angle),math.sin(angle));
            // A circumscribed map circle plus a chord allowance keeps complete triangles clear, not just vertices.
            float inner=math.length(value.Extents.xz)+Gap+.25f;
            float wave=math.sin(phase*math.PI),width=14f+10f*wave*wave;
            float top=value.Ground+4.5f+.6f*math.sin(piece*(math.PI/3f))+.35f*wave;
            float radial=0,height=0;
            if(face==1)radial=width;
            else if(face==2){radial=width*.82f;height=-5f;}
            else if(face==3){radial=width*.40f;height=-18f;}
            else if(face==4){radial=width*.12f;height=-5f;}
            return new float3(value.Center.x+direction.x*(inner+radial),top+height,value.Center.z+direction.y*(inner+radial));
        }
        public static uint Tint(int face,int section)
        {
            if(face==0)return section%3==0?0x7aa18affu:0x709b86ffu;
            if(face==1)return 0x4b6a66ffu;
            if(face==2)return 0x314d50ffu;
            if(face==3)return 0x29404affu;
            return 0x526c66ffu;
        }
    }
    [BurstCompile] public struct OuterIslandBoundsJob020:IJob
    {
        [ReadOnly] public NativeArray<OuterIslandInput020> Input;
        public int Count;
        public float4x4 RootInverse;
        public OuterIslandBounds020 Initial;
        [WriteOnly] public NativeArray<OuterIslandBounds020> Output;
        public void Execute()
        {
            if(Output.Length!=1||Count<0||Count>Input.Length)return;
            float3 minimum=Initial.Center-Initial.Extents,maximum=Initial.Center+Initial.Extents;
            for(int i=0;i<Count;i++)
            {
                var value=Input[i];float4x4 relative=math.mul(RootInverse,value.Matrix);
                for(int corner=0;corner<8;corner++)
                {
                    float3 sign=new float3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                    float3 point=math.transform(relative,value.Center+value.Extents*sign);
                    minimum=math.min(minimum,point);maximum=math.max(maximum,point);
                }
            }
            Output[0]=new OuterIslandBounds020{Center=(minimum+maximum)*.5f,Extents=(maximum-minimum)*.5f};
        }
    }
    [BurstCompile] public struct OuterIslandBuildJob020:IJob
    {
        public OuterIslandLayout020 Layout;
        [WriteOnly] public NativeArray<OuterIslandVertex020> Vertices;
        [WriteOnly] public NativeArray<int> Indices,Written;
        public void Execute()
        {
            if(Written.Length!=1)return;
            Written[0]=0;
            if(Vertices.Length!=OuterIslandMath020.VertexCount||Indices.Length!=OuterIslandMath020.IndexCount)return;
            int vertex=0,index=0;
            for(int piece=0;piece<OuterIslandMath020.Pieces;piece++)
            {
                for(int section=0;section<OuterIslandMath020.Sections;section++)
                for(int face=0;face<OuterIslandMath020.Faces;face++)
                {
                    int next=(face+1)%OuterIslandMath020.Faces;
                    float3 a=OuterIslandMath020.Point(Layout,piece,section,face),b=OuterIslandMath020.Point(Layout,piece,section+1,face);
                    float3 c=OuterIslandMath020.Point(Layout,piece,section+1,next),d=OuterIslandMath020.Point(Layout,piece,section,next);
                    float3 normal=math.normalizesafe(math.cross(b-a,c-a),new float3(0,1,0));
                    uint color=OuterIslandMath020.Tint(face,section);
                    Vertices[vertex]=new OuterIslandVertex020{Position=a,Normal=normal,Color=color};
                    Vertices[vertex+1]=new OuterIslandVertex020{Position=b,Normal=normal,Color=color};
                    Vertices[vertex+2]=new OuterIslandVertex020{Position=c,Normal=normal,Color=color};
                    Vertices[vertex+3]=new OuterIslandVertex020{Position=d,Normal=normal,Color=color};
                    Indices[index]=vertex;Indices[index+1]=vertex+1;Indices[index+2]=vertex+2;
                    Indices[index+3]=vertex;Indices[index+4]=vertex+2;Indices[index+5]=vertex+3;
                    vertex+=4;index+=6;
                }
                for(int end=0;end<2;end++)
                {
                    int section=end*OuterIslandMath020.Sections;
                    float3 center=float3.zero;
                    for(int face=0;face<OuterIslandMath020.Faces;face++)center+=OuterIslandMath020.Point(Layout,piece,section,face);
                    center*=1f/OuterIslandMath020.Faces;
                    for(int face=0;face<OuterIslandMath020.Faces;face++)
                    {
                        int next=(face+1)%OuterIslandMath020.Faces;
                        float3 a=OuterIslandMath020.Point(Layout,piece,section,end==0?face:next);
                        float3 b=OuterIslandMath020.Point(Layout,piece,section,end==0?next:face);
                        float3 normal=math.normalizesafe(math.cross(a-center,b-center),new float3(0,0,1));
                        uint color=OuterIslandMath020.Tint(2,section);
                        Vertices[vertex]=new OuterIslandVertex020{Position=center,Normal=normal,Color=color};
                        Vertices[vertex+1]=new OuterIslandVertex020{Position=a,Normal=normal,Color=color};
                        Vertices[vertex+2]=new OuterIslandVertex020{Position=b,Normal=normal,Color=color};
                        Indices[index++]=vertex;Indices[index++]=vertex+1;Indices[index++]=vertex+2;
                        vertex+=3;
                    }
                }
            }
            if(index==Indices.Length)Written[0]=vertex;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original environment-local metres/Y-up; six disjoint44degree sectors with16degree void gaps; full triangles stay beyond original XZ circumscribed circle plus10m.
// Array Formats: fixed blittable layout, sequential28B vertex[1620], int32 index[2340], int32 written[1];54724B mesh scratch. Optional cold source-bound pass uses input88B[<=4096]+output24B[1]; no persistent native storage.
// Dependencies/Pointers: pure cold numeric IJob; no Object API, spatial lookup store, collider, path or actor state. Existing main-thread owners complete before immutable mesh upload/disposal; native Burst and pixels pending.
