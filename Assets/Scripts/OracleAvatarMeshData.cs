using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4,Size=12)]
    public readonly struct OracleAvatarFloat3
    {
        public readonly float X,Y,Z;
        public OracleAvatarFloat3(float x,float y,float z){X=x;Y=y;Z=z;}
    }
    public sealed class OracleAvatarMeshData
    {
        public readonly OracleAvatarFloat3[] Positions,Normals;
        public readonly int[] Indices;
        public int VertexCount=>Positions.Length;
        internal OracleAvatarMeshData(int boxes)
        {Positions=new OracleAvatarFloat3[boxes*24];Normals=new OracleAvatarFloat3[boxes*24];Indices=new int[boxes*36];}
    }
    // Cold numeric recipe expansion. Pure math is independent of engine object publication.
    public static class OracleAvatarMeshBuilder
    {
        // Bits0/1/2 select positive X/Y/Z. Quad corners wind outward on all six sides.
        static readonly byte[] Corners={0,4,6,2, 5,1,3,7, 4,0,1,5, 2,6,7,3, 1,0,2,3, 4,5,7,6};
        public static OracleAvatarMeshData Build(OracleAvatarBox[] boxes,int count,int slot)
        {
            if(boxes==null||count<0||count>OracleAvatarRecipe.MaximumBoxes||count>boxes.Length||slot<0||slot>=OracleAvatarRecipe.MeshSlots)
                throw new ArgumentException("Bounded avatar mesh input required.");
            int matches=0;
            for(int i=0;i<count;i++)
            {
                var p=boxes[i];
                if(p.Slot<0||p.Slot>=OracleAvatarRecipe.MeshSlots||!Finite(p.X)||!Finite(p.Y)||!Finite(p.Z)||
                    !Finite(p.Width)||!Finite(p.Height)||!Finite(p.Depth)||p.Width<=0||p.Height<=0||p.Depth<=0||
                    Math.Abs(p.X)>4||Math.Abs(p.Y)>4||Math.Abs(p.Z)>4||p.Width>4||p.Height>4||p.Depth>4)
                    throw new ArgumentException("Invalid avatar box.");
                if(p.Slot==slot)matches++;
            }
            var data=new OracleAvatarMeshData(matches);int vertex=0,index=0;
            for(int i=0;i<count;i++)
            {
                var p=boxes[i];if(p.Slot!=slot)continue;
                for(int face=0;face<6;face++)
                {
                    var normal=new OracleAvatarFloat3(face==0?-1:face==1?1:0,face==2?-1:face==3?1:0,face==4?-1:face==5?1:0);
                    for(int corner=0;corner<4;corner++)
                    {
                        int bits=Corners[face*4+corner];
                        data.Positions[vertex+corner]=new OracleAvatarFloat3(p.X+((bits&1)!=0?.5f:-.5f)*p.Width,
                            p.Y+((bits&2)!=0?.5f:-.5f)*p.Height,p.Z+((bits&4)!=0?.5f:-.5f)*p.Depth);
                        data.Normals[vertex+corner]=normal;
                    }
                    data.Indices[index++]=vertex;data.Indices[index++]=vertex+1;data.Indices[index++]=vertex+2;
                    data.Indices[index++]=vertex;data.Indices[index++]=vertex+2;data.Indices[index++]=vertex+3;vertex+=4;
                }
            }
            return data;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}

// # ARCHITECTURE SNAPSHOT
// Cold bounded mesh adapter: position/normal arrays use12-byte XYZ values; indices int32,24 vertices/36 indices per box.
// Y-up metres; recipe slot/bone-local coordinates are preserved. Engine-free loops; no native memory, Physics or jobs scheduled.
