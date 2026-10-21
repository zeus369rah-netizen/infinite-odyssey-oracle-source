using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct SmithFurnaceQuad32
    {
        public int X,Y,Z,Width,Height,Axis,Sign,Material;
    }
    public interface ISmithFurnaceStorage
    {
        int CellCapacity{get;} int WordCapacity{get;} int SummaryCapacity{get;} int MaskCapacity{get;} int QuadCapacity{get;}
        byte Cell(int index);void Cell(int index,byte value);
        ulong Word(int index);void Word(int index,ulong value);
        ulong Summary(int index);void Summary(int index,ulong value);
        int Mask(int index);void Mask(int index,int value);
        void Quad(int index,SmithFurnaceQuad32 value);
    }
    // Original cold20mm furnace union and greedy surface math; no Object/engine dependency.
    public static class SmithFurnaceMath
    {
        public const int SizeX=56,SizeY=78,SizeZ=42,ChunkCapacity=256;
        public const int Cells=ChunkCapacity*4096,Words=ChunkCapacity*64,MaskCells=6144,MaximumQuads=2048;
        public const float CellMetres=.02f,HalfWidth=.56f,HalfDepth=.42f,Height=1.56f,Behind=1.25f;
        public static int Morton(int x,int y,int z)
        {
            int key=0;for(int bit=0;bit<4;bit++)key|=((x>>bit)&1)<<(bit*3)|((y>>bit)&1)<<(bit*3+1)|((z>>bit)&1)<<(bit*3+2);
            return key;
        }
        public static int Address(int x,int y,int z)=>Morton(x>>4,y>>4,z>>4)*4096+Morton(x&15,y&15,z&15);
        public static int Generate<T>(ref T data) where T:struct,ISmithFurnaceStorage
        {
            if(data.CellCapacity<Cells||data.WordCapacity<Words||data.SummaryCapacity<ChunkCapacity||data.MaskCapacity<MaskCells||data.QuadCapacity<MaximumQuads)return -1;
            // Store is explicitly zero-initialized by its owner before this single generation.
            Box(ref data,0,0,0,56,5,42,2);Box(ref data,2,5,2,54,9,40,1);
            Box(ref data,4,9,2,52,45,8,1);
            Box(ref data,4,9,8,12,45,37,1);Box(ref data,44,9,8,52,45,37,1);
            Box(ref data,12,9,8,44,14,36,2);Box(ref data,14,14,12,42,16,34,2);
            Box(ref data,10,14,35,46,16,39,3);
            for(int coal=0;coal<12;coal++)
            {
                int x=16+(coal%4)*6,z=14+(coal/4)*6;
                Box(ref data,x,16,z,x+4,17+(coal&1),z+4,coal%3==0?2:4);
            }
            Box(ref data,4,39,8,52,45,39,3);
            Box(ref data,3,45,1,53,49,39,2);Box(ref data,8,49,4,48,54,34,1);Box(ref data,16,54,8,40,58,28,2);
            // Hollow flue and open iron cap; every underside/internal union face is culled.
            Box(ref data,20,58,10,24,76,26,1);Box(ref data,32,58,10,36,76,26,1);
            Box(ref data,24,58,10,32,76,14,1);Box(ref data,24,58,22,32,76,26,1);
            Box(ref data,18,76,8,22,78,28,3);Box(ref data,34,76,8,38,78,28,3);
            Box(ref data,22,76,8,34,78,12,3);Box(ref data,22,76,24,34,78,28,3);
            for(int row=0;row<5;row++)
            {
                int y=12+row*6;
                Box(ref data,4,y,36,12,y+1,37,2);Box(ref data,44,y,36,52,y+1,37,2);
            }
            Box(ref data,5,32,37,7,34,38,5);Box(ref data,49,32,37,51,34,38,5);
            int count=0;
            for(int axis=0;axis<3;axis++)
            {
                int u=(axis+1)%3,v=(axis+2)%3,nu=Dimension(u),nv=Dimension(v);
                for(int plane=0;plane<=Dimension(axis);plane++)
                {
                    for(int j=0;j<nv;j++)for(int i=0;i<nu;i++)
                    {
                        Coordinates(axis,plane-1,i,j,out int ax,out int ay,out int az);
                        Coordinates(axis,plane,i,j,out int bx,out int by,out int bz);
                        int a=Read(ref data,ax,ay,az),b=Read(ref data,bx,by,bz);
                        data.Mask(j*nu+i,a!=0&&b==0?a:a==0&&b!=0?-b:0);
                    }
                    for(int j=0;j<nv;j++)for(int i=0;i<nu;)
                    {
                        int tag=data.Mask(j*nu+i);if(tag==0){i++;continue;}
                        int width=1;while(i+width<nu&&data.Mask(j*nu+i+width)==tag)width++;
                        int height=1;bool full=true;
                        while(j+height<nv&&full)
                        {
                            for(int k=0;k<width;k++)if(data.Mask((j+height)*nu+i+k)!=tag){full=false;break;}
                            if(full)height++;
                        }
                        if(count>=data.QuadCapacity||count>=MaximumQuads)return -2;
                        Coordinates(axis,plane,i,j,out int x,out int y,out int z);
                        data.Quad(count++,new SmithFurnaceQuad32{X=x,Y=y,Z=z,Width=width,Height=height,Axis=axis,Sign=tag>0?1:-1,Material=Math.Abs(tag)});
                        for(int q=0;q<height;q++)for(int k=0;k<width;k++)data.Mask((j+q)*nu+i+k,0);
                        i+=width;
                    }
                }
            }
            return count;
        }
        static int Dimension(int axis)=>axis==0?SizeX:axis==1?SizeY:SizeZ;
        static void Coordinates(int axis,int d,int u,int v,out int x,out int y,out int z)
        {x=axis==0?d:axis==1?v:u;y=axis==0?u:axis==1?d:v;z=axis==0?v:axis==1?u:d;}
        public static int Read<T>(ref T data,int x,int y,int z) where T:struct,ISmithFurnaceStorage
        {
            if(x<0||y<0||z<0||x>=SizeX||y>=SizeY||z>=SizeZ)return 0;
            int index=Address(x,y,z),chunk=index>>12,word=(index&4095)>>6;
            if((data.Summary(chunk)&(1UL<<word))==0||(data.Word(chunk*64+word)&(1UL<<(index&63)))==0)return 0;
            return data.Cell(index);
        }
        static void Box<T>(ref T data,int x0,int y0,int z0,int x1,int y1,int z1,int material) where T:struct,ISmithFurnaceStorage
        {
            for(int z=z0;z<z1;z++)for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
            {
                int index=Address(x,y,z),chunk=index>>12,word=(index&4095)>>6;
                data.Cell(index,(byte)material);data.Word(chunk*64+word,data.Word(chunk*64+word)|(1UL<<(index&63)));
                data.Summary(chunk,data.Summary(chunk)|(1UL<<word));
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Fixed20mm local integer furnace lattice; local bottom-centre(+Z mouth/Y-up) offset applied only by main-thread adapter.
// Array Formats: Quad32; fixed1MiB Morton material bytes,16384 occupancy words+256 summaries,6144 mask ints,2048 output quads. Zero-initialized owner storage, single cold generation.
// Dependencies/Pointers: Pure constrained numeric storage interface; occupancy broadphase precedes material reads; no Object/API/managed allocation. Caller owns capacities and publication after completion.
