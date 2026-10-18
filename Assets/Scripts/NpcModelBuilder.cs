using System;
using System.Collections.Generic;

namespace OdysseyOracle.CampaignNpcs
{
    // # ARCHITECTURE SNAPSHOT
    // Pure cold model recipes: metres, +Y up/+Z face, flat normals, palette-index UVs.
    // No Unity objects, external images/models, authority, audio or native ownership.
    public enum RefugeNpcRole { Smith=1,Runekeeper=2,Gatekeeper=3 }
    public struct NpcVector
    {
        public float X,Y,Z;
        public NpcVector(float x,float y,float z){X=x;Y=y;Z=z;}
        public static NpcVector operator +(NpcVector a,NpcVector b)=>new NpcVector(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static NpcVector operator -(NpcVector a,NpcVector b)=>new NpcVector(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static NpcVector operator *(NpcVector a,float n)=>new NpcVector(a.X*n,a.Y*n,a.Z*n);
        public static float Dot(NpcVector a,NpcVector b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        public static NpcVector Cross(NpcVector a,NpcVector b)=>new NpcVector(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        public float Square=>Dot(this,this);
        public NpcVector Unit=>Square>1e-18f?this*(1f/(float)Math.Sqrt(Square)):new NpcVector();
    }
    public sealed class NpcPart
    {
        public string Name;public int Parent=-1;public NpcVector Pivot;
        public readonly List<NpcVector> Positions=new List<NpcVector>(),Normals=new List<NpcVector>();
        public readonly List<byte> Paints=new List<byte>();public readonly List<int> Indices=new List<int>();
    }
    public sealed class NpcRecipe
    {
        public RefugeNpcRole Role;public string Name,RoleLabel,Identity;
        public readonly List<NpcPart> Parts=new List<NpcPart>();
        public int Torso,Head,LeftArm,RightArm,Mouth,Eyelids;
        public int Triangles {get{int n=0;foreach(var part in Parts)n+=part.Indices.Count/3;return n;}}
    }
    public sealed class NpcModelBuilder
    {
        public const int PaletteWidth=16,TriangleLimit=2500;
        public const byte Charcoal=0,Skin=1,Ivory=2,Blue=3,Emerald=4,Copper=5,Teal=6,Hair=7,PaleSkin=8,Gold=9,EyeWhite=10,Ink=11,JadeLight=12,LanternLight=13;
        readonly NpcPart part;
        public NpcModelBuilder(NpcPart target){part=target??throw new ArgumentNullException(nameof(target));}
        void Face(NpcVector[] points,byte paint,NpcVector outward)
        {
            if(paint>=PaletteWidth)throw new ArgumentOutOfRangeException(nameof(paint));
            NpcVector cross=NpcVector.Cross(points[1]-points[0],points[2]-points[0]);bool reverse=NpcVector.Dot(cross,outward)<0;
            int start=part.Positions.Count;for(int i=0;i<points.Length;i++){
                part.Positions.Add(points[reverse?points.Length-1-i:i]);part.Normals.Add(outward.Unit);part.Paints.Add(paint);}
            for(int i=1;i+1<points.Length;i++){part.Indices.Add(start);part.Indices.Add(start+i);part.Indices.Add(start+i+1);}
        }
        public void Box(float x,float y,float z,float sx,float sy,float sz,byte paint)
        {
            var center=new NpcVector(x,y,z);float[] h={sx*.5f,sy*.5f,sz*.5f};
            if(!(sx>0&&sy>0&&sz>0))throw new ArgumentException("Positive solid dimensions required.");
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2){
                var normal=Axis(axis)*sign;var mid=center+normal*h[axis];var u=Axis((axis+1)%3)*h[(axis+1)%3];var v=Axis((axis+2)%3)*h[(axis+2)%3];
                Face(new[]{mid-u-v,mid+u-v,mid+u+v,mid-u+v},paint,normal);}
        }
        public void Bevel(float x,float y,float z,float sx,float sy,float sz,float bevel,byte paint)
        {
            var center=new NpcVector(x,y,z);float[] h={sx*.5f,sy*.5f,sz*.5f};float b=Math.Min(bevel,Math.Min(h[0],Math.Min(h[1],h[2]))*.6f);
            if(!(b>0))throw new ArgumentException("Positive bevel required.");float[] inset={h[0]-b,h[1]-b,h[2]-b};
            for(int a=0;a<3;a++)for(int sign=-1;sign<=1;sign+=2){
                int u=(a+1)%3,v=(a+2)%3;NpcVector mid=center+Axis(a)*(h[a]*sign),du=Axis(u)*inset[u],dv=Axis(v)*inset[v];
                Face(new[]{mid-du-dv,mid+du-dv,mid+du+dv,mid-du+dv},paint,Axis(a)*sign);}
            for(int a=0;a<3;a++)for(int d=a+1;d<3;d++)for(int sa=-1;sa<=1;sa+=2)for(int sd=-1;sd<=1;sd+=2){
                int v=3-a-d;NpcVector p=center+Axis(a)*(h[a]*sa)+Axis(d)*(inset[d]*sd),r=center+Axis(a)*(inset[a]*sa)+Axis(d)*(h[d]*sd),e=Axis(v)*inset[v];
                Face(new[]{p-e,r-e,r+e,p+e},paint,Axis(a)*sa+Axis(d)*sd);}
            for(int x1=-1;x1<=1;x1+=2)for(int y1=-1;y1<=1;y1+=2)for(int z1=-1;z1<=1;z1+=2)
                Face(new[]{center+new NpcVector(h[0]*x1,inset[1]*y1,inset[2]*z1),center+new NpcVector(inset[0]*x1,h[1]*y1,inset[2]*z1),center+new NpcVector(inset[0]*x1,inset[1]*y1,h[2]*z1)},paint,new NpcVector(x1,y1,z1));
        }
        public void Rod(NpcVector a,NpcVector b,float width,byte paint)
        {
            NpcVector axis=(b-a).Unit,hint=Math.Abs(axis.Z)<.8f?new NpcVector(0,0,1):new NpcVector(1,0,0);
            NpcVector x=NpcVector.Cross(axis,hint).Unit*(width*.5f),z=NpcVector.Cross(x.Unit,axis)*(width*.5f);
            if((b-a).Square<1e-10f||width<=0)throw new ArgumentException("Nonzero rod required.");
            Face(new[]{a-x-z,a+x-z,a+x+z,a-x+z},paint,axis*-1);
            Face(new[]{b-x-z,b+x-z,b+x+z,b-x+z},paint,axis);
            var baseRing=new[]{a-x-z,a+x-z,a+x+z,a-x+z};var topRing=new[]{b-x-z,b+x-z,b+x+z,b-x+z};
            for(int i=0;i<4;i++){int next=(i+1)%4;Face(new[]{baseRing[i],baseRing[next],topRing[next],topRing[i]},paint,((baseRing[i]+baseRing[next])*.5f-a).Unit);}
        }
        public void Crystal(float x,float y,float z,float radius,float height,byte paint)
        {
            var center=new NpcVector(x,y,z);var top=center+new NpcVector(0,height*.65f,0);var bottom=center-new NpcVector(0,height*.35f,0);
            for(int i=0;i<6;i++){double a=i*Math.PI/3,b=(i+1)*Math.PI/3;var p=center+new NpcVector((float)Math.Cos(a)*radius,0,(float)Math.Sin(a)*radius);var q=center+new NpcVector((float)Math.Cos(b)*radius,0,(float)Math.Sin(b)*radius);
                var upper=NpcVector.Cross(q-p,top-p);if(NpcVector.Dot(upper,(p+q+top)*(1f/3)-center)<0)upper=upper*-1;
                var lower=NpcVector.Cross(p-q,bottom-q);if(NpcVector.Dot(lower,(p+q+bottom)*(1f/3)-center)<0)lower=lower*-1;
                Face(new[]{p,q,top},paint,upper);Face(new[]{q,p,bottom},paint,lower);}
        }
        static NpcVector Axis(int a)=>a==0?new NpcVector(1,0,0):a==1?new NpcVector(0,1,0):new NpcVector(0,0,1);
        public static void Validate(NpcRecipe recipe)
        {
            if(recipe.Triangles>TriangleLimit||recipe.Triangles<=0)throw new InvalidOperationException("NPC triangle budget exceeded or empty.");
            for(int p=0;p<recipe.Parts.Count;p++){
                var part=recipe.Parts[p];if(part.Parent>=p||part.Parent< -1)throw new InvalidOperationException("Parent must precede child.");
                if(part.Positions.Count!=part.Normals.Count||part.Positions.Count!=part.Paints.Count||part.Indices.Count%3!=0)throw new InvalidOperationException("Misaligned geometry channels.");
                for(int v=0;v<part.Positions.Count;v++){var point=part.Positions[v];if(!Finite(point.X)||!Finite(point.Y)||!Finite(point.Z)||Math.Abs(part.Normals[v].Square-1)>.0001f)throw new InvalidOperationException("Nonfinite geometry or nonunit normal.");}
                for(int i=0;i<part.Indices.Count;i+=3){int a=part.Indices[i],b=part.Indices[i+1],c=part.Indices[i+2];if(a<0||b<0||c<0||a>=part.Positions.Count||b>=part.Positions.Count||c>=part.Positions.Count)throw new InvalidOperationException("Invalid index.");
                    var cross=NpcVector.Cross(part.Positions[b]-part.Positions[a],part.Positions[c]-part.Positions[a]);if(cross.Square<1e-16f||NpcVector.Dot(cross,part.Normals[a])<=0)throw new InvalidOperationException("Degenerate/reversed face.");}
            }
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
