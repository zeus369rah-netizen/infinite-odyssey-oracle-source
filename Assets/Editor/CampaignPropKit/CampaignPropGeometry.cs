using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace OdysseyOracle.CampaignLibrary.Authoring
{
    // # ARCHITECTURE SNAPSHOT
    // Original pure-math authoring data. Metres, +Y up, +Z presentation front.
    // No Unity objects, gameplay, import, Resources or external models/images.
    // Editor baking is separate; each part stays a closed faceted solid and all
    // parts are combined into one mesh with shared palette submeshes per prop.
    public static class CampaignPropGeometry
    {
        public const string GeneratorId = "ZeusRa.Oracle.OriginalCampaignProps";
        public const int RecipeVersion = 1, PaletteCount = 8, PropTriangleLimit = 2500, KitTriangleLimit = 12000;
        public const int Stone = 0, Wood = 1, Iron = 2, Brass = 3, Paper = 4, Cloth = 5, CrystalPaint = 6, Ember = 7;

        public struct V3
        {
            public float X, Y, Z;
            public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
            public float LengthSquared { get { return X * X + Y * Y + Z * Z; } }
            public V3 Normalized { get { float length = (float)Math.Sqrt(LengthSquared); return length > 1e-10f ? this * (1f / length) : new V3(); } }
            public static V3 operator +(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
            public static V3 operator -(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
            public static V3 operator *(V3 a, float b) { return new V3(a.X * b, a.Y * b, a.Z * b); }
            public static float Dot(V3 a, V3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
            public static V3 Cross(V3 a, V3 b) { return new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X); }
        }
        public struct V2 { public float X, Y; public V2(float x, float y) { X = x; Y = y; } }
        public struct Rotation
        {
            public V3 X, Y, Z;
            public V3 Apply(V3 point) { return X * point.X + Y * point.Y + Z * point.Z; }
            public static Rotation Identity { get { return Euler(0, 0, 0); } }
            public static Rotation Euler(float x, float y, float z)
            {
                // Fixed Rx then Ry then Rz authoring order, independent of Unity Euler conventions.
                double a = x * Math.PI / 180d, b = y * Math.PI / 180d, c = z * Math.PI / 180d;
                float sx = (float)Math.Sin(a), cx = (float)Math.Cos(a), sy = (float)Math.Sin(b), cy = (float)Math.Cos(b), sz = (float)Math.Sin(c), cz = (float)Math.Cos(c);
                return new Rotation {
                    X = new V3(cz * cy, sz * cy, -sy),
                    Y = new V3(cz * sy * sx - sz * cx, sz * sy * sx + cz * cx, cy * sx),
                    Z = new V3(cz * sy * cx + sz * sx, sz * sy * cx - cz * sx, cy * cx)
                };
            }
            public static Rotation Along(V3 direction)
            {
                V3 y = direction.Normalized;
                V3 hint = Math.Abs(y.Z) < .9f ? new V3(0, 0, 1) : new V3(1, 0, 0);
                V3 x = V3.Cross(y, hint).Normalized;
                return new Rotation { X = x, Y = y, Z = V3.Cross(x, y).Normalized };
            }
        }
        public sealed class Paint
        {
            public string Name;
            public float R, G, B, Metallic, Smoothness, Emission;
        }
        public sealed class Anchor
        {
            public string Name;
            public V3 Position;
            public Rotation Rotation;
        }
        public sealed class Primitive
        {
            public string Kind;
            public int Material, FirstIndex, IndexCount;
        }
        public sealed class Recipe
        {
            public string Name, Description;
            public readonly List<V3> Vertices = new List<V3>(4096);
            public readonly List<V3> Normals = new List<V3>(4096);
            public readonly List<V2> UV = new List<V2>(4096);
            public readonly List<int>[] Triangles = NewTriangles();
            public readonly List<Anchor> Anchors = new List<Anchor>();
            public readonly List<Primitive> Primitives = new List<Primitive>();
            public V3 Minimum, Maximum;
            public int TriangleCount { get { int count = 0; foreach (var indices in Triangles) count += indices.Count / 3; return count; } }
            static List<int>[] NewTriangles() { var result = new List<int>[PaletteCount]; for (int i = 0; i < result.Length; i++) result[i] = new List<int>(); return result; }
        }

        public static Paint[] Palette()
        {
            return new[] {
                Color("SageStone", .30f,.34f,.29f, .03f,.28f,0), Color("OldTimber", .15f,.085f,.038f, .02f,.24f,0),
                Color("ForgedIron", .12f,.15f,.15f, .68f,.36f,0), Color("AstralBrass", .72f,.51f,.19f, .72f,.43f,0),
                Color("IvoryPages", .83f,.75f,.56f, 0,.22f,0), Color("WineCloth", .29f,.07f,.065f,0,.22f,0),
                Color("JadeCrystal", .085f,.62f,.34f, .18f,.56f,.55f), Color("BankedEmber", .78f,.20f,.035f,.10f,.35f,.65f)
            };
        }
        static Paint Color(string name, float r,float g,float b,float metal,float smooth,float glow) { return new Paint { Name=name,R=r,G=g,B=b,Metallic=metal,Smoothness=smooth,Emission=glow }; }

        public static Recipe[] BuildAll()
        {
            var result = new[] { Bookstand(), Workbench(), SuppliesRack(), Brazier(), Lantern(), Reliquary() };
            int total = 0;
            foreach (var recipe in result) { NormalizePivot(recipe); Validate(recipe); total += recipe.TriangleCount; }
            if (total > KitTriangleLimit) throw new InvalidOperationException("Kit exceeds 12000 triangles: " + total);
            return result;
        }

        static Recipe Bookstand()
        {
            var b = new Builder("RunicBookstand", "Open ivory folio on a tilted brass-trimmed timber lectern; rune seal, stepped base and braced feet.");
            b.Box(V(0,.055f,0),V(.60f,.11f,.46f),.025f,Stone);
            b.Box(V(0,.13f,0),V(.45f,.06f,.34f),.014f,Brass);
            b.Box(V(0,.51f,0),V(.16f,.72f,.16f),.018f,Wood);
            for(int side=-1;side<=1;side+=2) {
                b.Box(V(side*.24f,.075f,.15f),V(.14f,.15f,.22f),.018f,Wood);
                b.Rod(V(0,.34f,0),V(side*.25f,.87f,0),.065f,Brass);
            }
            var tilt=Rotation.Euler(25,0,0); V3 top=V(0,.96f,0);
            b.Box(top,V(.79f,.075f,.56f),.020f,Wood,tilt);
            b.Box(top+tilt.Apply(V(0,.048f,.225f)),V(.81f,.050f,.048f),.009f,Brass,tilt);
            b.Box(top+tilt.Apply(V(0,.048f,-.245f)),V(.81f,.025f,.035f),.006f,Brass,tilt);
            for(int side=-1;side<=1;side+=2) {
                b.Box(top+tilt.Apply(V(side*.355f,.035f,0)),V(.026f,.028f,.47f),.005f,Brass,tilt);
                var leaf = Rotation.Euler(25,0,side*5);
                V3 cover=top+tilt.Apply(V(side*.181f,.080f,0));
                b.Box(cover,V(.35f,.035f,.44f),.009f,Cloth,leaf);
                b.Box(cover+tilt.Apply(V(0,.028f,0)),V(.326f,.045f,.409f),.007f,Paper,leaf);
                for(int edge=-1;edge<=1;edge+=2)
                    b.Box(cover+tilt.Apply(V(side*.135f,.053f,edge*.165f)),V(.050f,.010f,.045f),.003f,Brass,leaf);
                b.Box(top+tilt.Apply(V(side*.15f,.146f,.01f)),V(.012f,.007f,.22f),.002f,Iron,tilt);
            }
            b.Box(top+tilt.Apply(V(0,.12f,0)),V(.045f,.060f,.46f),.009f,Cloth,tilt);
            b.Ring(V(0,.66f,.096f),.080f,.046f,.012f,8,.004f,Brass,Rotation.Euler(90,0,0));
            b.Diamond(V(0,.66f,.113f),V(.060f,.085f,.020f),CrystalPaint);
            b.StandardAnchors(V(0,0,0),V(0,.93f,.45f),V(0,.97f,0));
            b.Anchor("Anchor_Book",top+tilt.Apply(V(0,.16f,0)),tilt);
            return b.Data;
        }

        static Recipe Workbench()
        {
            var b=new Builder("ForgeWorkbench","Layered timber forge bench with iron-banded legs, diagonal braces, shelf, vise and a resting hammer.");
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2) {
                b.Box(V(x*.65f,.055f,z*.28f),V(.22f,.11f,.22f),.022f,Iron);
                b.Box(V(x*.65f,.49f,z*.28f),V(.15f,.88f,.15f),.016f,Wood);
                b.Box(V(x*.65f,.19f,z*.28f),V(.172f,.07f,.172f),.009f,Brass);
                b.Diamond(V(x*.65f,.82f,z*.365f),V(.028f,.028f,.014f),Brass);
            }
            for(int plank=-1;plank<=1;plank++) {
                b.Box(V(0,.975f,plank*.235f),V(1.56f,.14f,.225f),.016f,Wood);
                b.Box(V(0,.31f,plank*.18f),V(1.30f,.055f,.165f),.010f,Wood);
            }
            for(int side=-1;side<=1;side+=2) {
                b.Box(V(0,.86f,side*.31f),V(1.38f,.16f,.065f),.010f,Iron);
                b.Rod(V(-.55f,.27f,side*.28f),V(.55f,.80f,side*.28f),.055f,Wood);
            }
            b.Box(V(-.34f,1.057f,0),V(.46f,.025f,.49f),.012f,Iron);
            b.Box(V(.49f,1.09f,.29f),V(.22f,.15f,.16f),.014f,Iron);
            b.Box(V(.49f,1.18f,.33f),V(.28f,.12f,.075f),.012f,Brass);
            b.Box(V(.49f,1.12f,.435f),V(.28f,.17f,.07f),.012f,Iron);
            b.Rod(V(.49f,1.10f,.39f),V(.49f,1.10f,.55f),.037f,Brass);
            b.Rod(V(.39f,1.10f,.54f),V(.59f,1.10f,.54f),.025f,Iron);
            b.Rod(V(-.40f,1.09f,-.21f),V(.14f,1.09f,-.12f),.037f,Wood);
            b.Box(V(.15f,1.12f,-.12f),V(.16f,.13f,.25f),.021f,Iron,Rotation.Euler(0,-10,0));
            b.Box(V(0,.68f,-.36f),V(.43f,.10f,.018f),.009f,Cloth);
            b.StandardAnchors(V(0,0,0),V(0,.88f,.70f),V(-.34f,1.08f,0));
            b.Anchor("Anchor_WorkSurface",V(-.34f,1.08f,0));
            b.Anchor("Anchor_Vise",V(.49f,1.26f,.33f));
            return b.Data;
        }

        static Recipe SuppliesRack()
        {
            var b=new Builder("SuppliesRack","Braced timber rack with three inset shelves, iron feet, a framed crate, cloth rolls and stoppered jars.");
            for(int side=-1;side<=1;side+=2)for(int z=-1;z<=1;z+=2) {
                b.Box(V(side*.58f,.055f,z*.22f),V(.19f,.11f,.20f),.020f,Iron);
                b.Box(V(side*.58f,.90f,z*.22f),V(.105f,1.72f,.105f),.013f,Wood);
            }
            for(int shelf=0;shelf<3;shelf++) {
                float y=.22f+shelf*.56f;
                for(int plank=-1;plank<=1;plank++) b.Box(V(0,y,plank*.145f),V(1.16f,.065f,.137f),.009f,Wood);
                for(int side=-1;side<=1;side+=2) b.Box(V(side*.55f,y-.06f,0),V(.07f,.09f,.46f),.010f,Brass);
            }
            b.Rod(V(-.56f,.28f,-.265f),V(.56f,1.64f,-.265f),.045f,Iron);
            b.Rod(V(.56f,.28f,-.265f),V(-.56f,1.64f,-.265f),.045f,Iron);
            b.Box(V(-.23f,.36f,0),V(.47f,.23f,.35f),.018f,Wood);
            for(int edge=-1;edge<=1;edge+=2) b.Box(V(-.23f+edge*.19f,.36f,.185f),V(.045f,.28f,.028f),.006f,Iron);
            b.Box(V(-.23f,.385f,.187f),V(.47f,.045f,.03f),.005f,Brass);
            for(int roll=0;roll<2;roll++) {
                V3 p=V(-.25f+roll*.24f,.88f,0);
                b.Prism(p,.105f,.105f,.32f,8,Cloth,Rotation.Euler(90,0,0));
                b.Ring(p,.109f,.094f,.035f,8,.004f,Brass,Rotation.Euler(90,0,0));
            }
            for(int jar=0;jar<3;jar++) {
                V3 p=V(-.31f+jar*.30f,1.48f,0);
                b.Prism(p,.087f,.068f,.20f,6,jar==1?CrystalPaint:Paper);
                b.Prism(p+V(0,.135f,0),.043f,.043f,.075f,6,Iron);
                b.Diamond(p+V(0,.19f,0),V(.05f,.035f,.05f),Brass);
            }
            b.StandardAnchors(V(0,0,0),V(0,1.0f,.54f),V(0,.78f,0));
            b.Anchor("Anchor_ShelfLower",V(0,.255f,0));b.Anchor("Anchor_ShelfMiddle",V(0,.815f,0));b.Anchor("Anchor_ShelfUpper",V(0,1.375f,0));
            return b.Data;
        }

        static Recipe Brazier()
        {
            var b=new Builder("BrazierRing","Three-footed iron fire bowl with a bevelled brass rim, radial ribs, vent stones and banked faceted coals.");
            b.Prism(V(0,.055f,0),.30f,.265f,.11f,12,Stone);
            b.Ring(V(0,.12f,0),.29f,.20f,.055f,12,.010f,Brass);
            for(int leg=0;leg<3;leg++) {
                float angle=leg*120; V3 outward=Radial(angle);
                b.Box(outward*.31f+V(0,.065f,0),V(.16f,.13f,.22f),.022f,Iron,Rotation.Euler(0,-angle,0));
                b.Rod(outward*.29f+V(0,.12f,0),outward*.38f+V(0,.49f,0),.072f,Iron);
                b.Rod(outward*.15f+V(0,.17f,0),outward*.32f+V(0,.35f,0),.038f,Brass);
            }
            b.Ring(V(0,.46f,0),.46f,.39f,.18f,12,.012f,Iron);
            b.Ring(V(0,.565f,0),.49f,.37f,.065f,12,.015f,Brass);
            b.Prism(V(0,.39f,0),.365f,.385f,.055f,12,Iron);
            for(int rib=0;rib<12;rib++) {
                V3 outward=Radial(rib*30);
                b.Rod(outward*.398f+V(0,.39f,0),outward*.455f+V(0,.54f,0),.026f,Brass);
                b.Diamond(outward*.477f+V(0,.565f,0),V(.035f,.026f,.035f),Iron);
            }
            for(int coal=0;coal<7;coal++) {
                V3 p=coal==0?V(0,.46f,0):Radial((coal-1)*60)*.21f+V(0,.455f,0);
                b.Crystal(p,.075f,.095f,5,coal%3==0?Ember:Iron,Rotation.Euler(0,coal*19,0));
            }
            b.StandardAnchors(V(0,0,0),V(0,.49f,.68f),V(0,.54f,0));
            b.Anchor("Anchor_Fire",V(0,.55f,0));
            return b.Data;
        }

        static Recipe Lantern()
        {
            var b=new Builder("WallLantern","Wall-mounted faceted jade lantern with a stepped backplate, curved brass hook, iron cage and layered roof.");
            b.Box(V(0,.57f,-.20f),V(.24f,.72f,.070f),.028f,Stone);
            b.Box(V(0,.57f,-.155f),V(.14f,.57f,.035f),.013f,Iron);
            for(int bolt=-1;bolt<=1;bolt+=2) b.Diamond(V(0,.57f+bolt*.25f,-.127f),V(.041f,.041f,.022f),Brass);
            b.Rod(V(0,.80f,-.16f),V(0,.90f,.16f),.055f,Iron);
            b.Rod(V(0,.58f,-.16f),V(0,.86f,.12f),.035f,Brass);
            b.Arc(V(0,.87f,.20f),.13f,.095f,.035f,8,35,285,Brass,Rotation.Euler(90,0,0));
            V3 body=V(0,.43f,.25f);
            b.Prism(body+V(0,-.24f,0),.21f,.18f,.075f,6,Iron);
            b.Ring(body+V(0,-.18f,0),.205f,.145f,.035f,6,.006f,Brass);
            b.Prism(body+V(0,.24f,0),.23f,.075f,.12f,6,Iron);
            b.Ring(body+V(0,.17f,0),.245f,.17f,.045f,6,.008f,Brass);
            b.Prism(body+V(0,.32f,0),.072f,.035f,.07f,6,Brass);
            for(int bar=0;bar<6;bar++) {
                V3 radial=Radial(bar*60)*.18f;
                b.Rod(body+radial+V(0,-.19f,0),body+radial+V(0,.17f,0),.025f,Iron);
                b.Diamond(body+radial+V(0,-.155f,0),V(.028f,.025f,.028f),Brass);
            }
            b.Crystal(body,.095f,.30f,6,CrystalPaint,Rotation.Euler(0,30,0));
            b.Crystal(body+V(0,-.07f,0),.050f,.12f,5,Ember);
            b.StandardAnchors(V(0,.57f,-.24f),V(0,.45f,.58f),body);
            b.Anchor("Anchor_Fire",body+V(0,.01f,0));
            b.Anchor("Anchor_HangingLoop",V(0,.79f,.25f));
            return b.Data;
        }

        static Recipe Reliquary()
        {
            var b=new Builder("CrystalReliquary","Layered stone and brass reliquary with four inward braces, rune seal and a three-crystal jade focus.");
            b.Prism(V(0,.075f,0),.40f,.37f,.15f,8,Stone);
            b.Prism(V(0,.18f,0),.31f,.27f,.08f,8,Iron);
            b.Ring(V(0,.235f,0),.32f,.22f,.06f,12,.012f,Brass);
            b.Prism(V(0,.31f,0),.255f,.205f,.12f,8,Stone);
            for(int corner=0;corner<4;corner++) {
                float angle=45+corner*90; V3 r=Radial(angle);
                b.Box(r*.31f+V(0,.065f,0),V(.16f,.13f,.16f),.019f,Iron,Rotation.Euler(0,-angle,0));
                b.Rod(r*.28f+V(0,.25f,0),r*.36f+V(0,.67f,0),.055f,Brass);
                b.Rod(r*.36f+V(0,.67f,0),r*.25f+V(0,1.03f,0),.050f,Brass);
                b.Rod(r*.25f+V(0,1.03f,0),r*.12f+V(0,1.18f,0),.040f,Brass);
                b.Diamond(r*.12f+V(0,1.18f,0),V(.055f,.070f,.055f),CrystalPaint);
            }
            b.Ring(V(0,.71f,0),.365f,.325f,.035f,12,.006f,Iron);
            b.Crystal(V(0,.76f,0),.145f,.79f,6,CrystalPaint,Rotation.Euler(0,30,0));
            b.Crystal(V(-.15f,.53f,.08f),.070f,.35f,5,CrystalPaint,Rotation.Euler(0,-10,15));
            b.Crystal(V(.14f,.54f,-.07f),.065f,.39f,5,CrystalPaint,Rotation.Euler(0,20,-12));
            b.Box(V(0,.18f,.335f),V(.15f,.11f,.026f),.009f,Iron);
            b.Diamond(V(0,.18f,.357f),V(.078f,.078f,.018f),Brass);
            b.StandardAnchors(V(0,0,0),V(0,.73f,.62f),V(0,.76f,0));
            b.Anchor("Anchor_Focus",V(0,.76f,0));
            return b.Data;
        }

        static V3 V(float x,float y,float z) { return new V3(x,y,z); }
        static V3 Radial(float degrees) { double a=degrees*Math.PI/180d; return V((float)Math.Cos(a),0,(float)Math.Sin(a)); }
        static bool Finite(float x) { return !float.IsNaN(x)&&!float.IsInfinity(x); }
        static bool Finite(V3 p) { return Finite(p.X)&&Finite(p.Y)&&Finite(p.Z); }
        static float Component(V3 p,int axis) { return axis==0?p.X:axis==1?p.Y:p.Z; }
        static V3 Axis(int axis) { return axis==0?V(1,0,0):axis==1?V(0,1,0):V(0,0,1); }

        sealed class Builder
        {
            public readonly Recipe Data;
            public Builder(string name,string description) { Data=new Recipe{Name=name,Description=description}; }
            void Begin(string kind,int material,out Primitive primitive) { primitive=new Primitive{Kind=kind,Material=material,FirstIndex=Data.Triangles[material].Count}; }
            void End(Primitive primitive) { primitive.IndexCount=Data.Triangles[primitive.Material].Count-primitive.FirstIndex;Data.Primitives.Add(primitive); }
            void Face(V3[] points,V3 expected,int material,V3 center,Rotation rotation)
            {
                V3 cross=V3.Cross(points[1]-points[0],points[2]-points[0]);
                bool reverse=V3.Dot(cross,expected)<0;
                V3 normal=(reverse?cross*-1:cross).Normalized;
                int first=Data.Vertices.Count;
                for(int i=0;i<points.Length;i++) {
                    V3 point=points[reverse?points.Length-1-i:i];
                    Data.Vertices.Add(center+rotation.Apply(point));Data.Normals.Add(rotation.Apply(normal));
                    int major=Math.Abs(normal.X)>Math.Abs(normal.Y)?0:1;major=Math.Abs(Component(normal,major))>Math.Abs(normal.Z)?major:2;
                    Data.UV.Add(major==0?new V2(point.Z,point.Y):major==1?new V2(point.X,point.Z):new V2(point.X,point.Y));
                }
                for(int i=1;i<points.Length-1;i++){Data.Triangles[material].Add(first);Data.Triangles[material].Add(first+i);Data.Triangles[material].Add(first+i+1);}
            }
            public void Box(V3 c,V3 size,float bevel,int material,Rotation? rotation=null)
            {
                Begin("bevel-box",material,out Primitive primitive); Rotation q=rotation??Rotation.Identity;
                V3 h=size*.5f;float b=Math.Min(bevel,Math.Min(h.X,Math.Min(h.Y,h.Z))*.7f);if(b<=0)throw new ArgumentException("Bevel must be positive.");
                V3 inner=h-V(b,b,b);
                for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2) {
                    int u=(axis+1)%3,v=(axis+2)%3;V3 m=Axis(axis)*(Component(h,axis)*sign),du=Axis(u)*Component(inner,u),dv=Axis(v)*Component(inner,v);
                    Face(new[]{m-du-dv,m+du-dv,m+du+dv,m-du+dv},Axis(axis)*sign,material,c,q);
                }
                for(int a=0;a<3;a++)for(int d=a+1;d<3;d++)for(int sa=-1;sa<=1;sa+=2)for(int sd=-1;sd<=1;sd+=2) {
                    int e=3-a-d;V3 p=Axis(a)*(Component(h,a)*sa)+Axis(d)*(Component(inner,d)*sd),r=Axis(a)*(Component(inner,a)*sa)+Axis(d)*(Component(h,d)*sd),v=Axis(e)*Component(inner,e);
                    Face(new[]{p-v,r-v,r+v,p+v},Axis(a)*sa+Axis(d)*sd,material,c,q);
                }
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    Face(new[]{V(h.X*x,inner.Y*y,inner.Z*z),V(inner.X*x,h.Y*y,inner.Z*z),V(inner.X*x,inner.Y*y,h.Z*z)},V(x,y,z),material,c,q);
                End(primitive);
            }
            public void Rod(V3 a,V3 b,float width,int material) { V3 delta=b-a;Box((a+b)*.5f,V(width,(float)Math.Sqrt(delta.LengthSquared),width),width*.16f,material,Rotation.Along(delta)); }
            public void Prism(V3 c,float bottom,float top,float height,int segments,int material,Rotation? rotation=null)
            {
                Begin("faceted-prism",material,out Primitive primitive);Rotation q=rotation??Rotation.Identity;var low=new V3[segments];var high=new V3[segments];
                for(int i=0;i<segments;i++){V3 radial=Radial(i*360f/segments);low[i]=radial*bottom+V(0,-height*.5f,0);high[i]=radial*top+V(0,height*.5f,0);}
                Face(low,V(0,-1,0),material,c,q);Face(high,V(0,1,0),material,c,q);
                for(int i=0;i<segments;i++){int next=(i+1)%segments;Face(new[]{low[i],low[next],high[next],high[i]},Radial((i+.5f)*360f/segments),material,c,q);}
                End(primitive);
            }
            public void Ring(V3 c,float outer,float inner,float height,int segments,float bevel,int material,Rotation? rotation=null) { Arc(c,outer,inner,height,segments,0,360,material,rotation,bevel); }
            public void Arc(V3 c,float outer,float inner,float height,int segments,float start,float sweep,int material,Rotation? rotation=null,float bevel=0)
            {
                Begin(sweep>=359.999f?"faceted-ring":"faceted-arc",material,out Primitive primitive); Rotation q=rotation??Rotation.Identity;
                float low=-height*.5f,high=height*.5f,b=Math.Min(bevel,Math.Min((outer-inner)*.3f,height*.3f));
                V2[] profile=b>0?new[]{new V2(outer-b,low),new V2(outer,low+b),new V2(outer,high-b),new V2(outer-b,high),new V2(inner+b,high),new V2(inner,high-b),new V2(inner,low+b),new V2(inner+b,low)}
                    :new[]{new V2(outer,low),new V2(outer,high),new V2(inner,high),new V2(inner,low)};
                for(int segment=0;segment<segments;segment++) {
                    float a=start+sweep*segment/segments,z=start+sweep*(segment+1)/segments;V3 r0=Radial(a),r1=Radial(z),rm=Radial((a+z)*.5f);
                    for(int side=0;side<profile.Length;side++){V2 p=profile[side],n=profile[(side+1)%profile.Length];
                        Face(new[]{r0*p.X+V(0,p.Y,0),r1*p.X+V(0,p.Y,0),r1*n.X+V(0,n.Y,0),r0*n.X+V(0,n.Y,0)},rm*(n.Y-p.Y)+V(0,p.X-n.X,0),material,c,q);}
                }
                if(sweep<359.999f)for(int cap=0;cap<2;cap++) {
                    float angle=start+(cap==0?0:sweep);V3 radial=Radial(angle),tangent=V(-radial.Z,0,radial.X)*(cap==0?-1:1);var points=new V3[profile.Length];
                    for(int i=0;i<profile.Length;i++)points[i]=radial*profile[i].X+V(0,profile[i].Y,0);Face(points,tangent,material,c,q);
                }
                End(primitive);
            }
            public void Crystal(V3 c,float radius,float height,int segments,int material,Rotation? rotation=null)
            {
                Begin("faceted-crystal",material,out Primitive primitive);Rotation q=rotation??Rotation.Identity;
                for(int i=0;i<segments;i++){V3 r0=Radial(i*360f/segments),r1=Radial((i+1)%segments*360f/segments),normal=Radial((i+.5f)*360f/segments);
                    V3 a=r0*radius*.78f+V(0,-height*.25f,0),b=r1*radius*.78f+V(0,-height*.25f,0),d=r0*radius+V(0,height*.18f,0),e=r1*radius+V(0,height*.18f,0);
                    Face(new[]{V(0,-height*.5f,0),b,a},normal+V(0,-1,0),material,c,q);Face(new[]{a,b,e,d},normal,material,c,q);Face(new[]{d,e,V(0,height*.5f,0)},normal+V(0,1,0),material,c,q);
                }
                End(primitive);
            }
            public void Diamond(V3 c,V3 halfSize,int material)
            {
                Begin("octahedral-inlay",material,out Primitive primitive);
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    Face(new[]{V(halfSize.X*x,0,0),V(0,halfSize.Y*y,0),V(0,0,halfSize.Z*z)},V(x,y,z),material,c,Rotation.Identity);
                End(primitive);
            }
            public void Anchor(string name,V3 position,Rotation? rotation=null) { Data.Anchors.Add(new Anchor{Name=name,Position=position,Rotation=rotation??Rotation.Identity}); }
            public void StandardAnchors(V3 mount,V3 interaction,V3 sfx) { Anchor("Anchor_Mount",mount);Anchor("Anchor_Interaction",interaction);Anchor("Anchor_SFX",sfx); }
        }

        static void NormalizePivot(Recipe recipe)
        {
            Bounds(recipe,out V3 min,out V3 max);V3 offset=V((min.X+max.X)*.5f,min.Y,(min.Z+max.Z)*.5f);
            for(int i=0;i<recipe.Vertices.Count;i++)recipe.Vertices[i]=recipe.Vertices[i]-offset;
            foreach(var anchor in recipe.Anchors)anchor.Position=anchor.Position-offset;
            Bounds(recipe,out recipe.Minimum,out recipe.Maximum);
        }
        static void Bounds(Recipe recipe,out V3 min,out V3 max)
        {
            min=V(float.MaxValue,float.MaxValue,float.MaxValue);max=V(float.MinValue,float.MinValue,float.MinValue);
            foreach(var p in recipe.Vertices){min=V(Math.Min(min.X,p.X),Math.Min(min.Y,p.Y),Math.Min(min.Z,p.Z));max=V(Math.Max(max.X,p.X),Math.Max(max.Y,p.Y),Math.Max(max.Z,p.Z));}
        }
        public static void Validate(Recipe recipe)
        {
            if(recipe.Vertices.Count==0||recipe.Vertices.Count!=recipe.Normals.Count||recipe.Vertices.Count!=recipe.UV.Count||recipe.TriangleCount>PropTriangleLimit)throw new InvalidOperationException(recipe.Name+": vertex/triangle budget invalid.");
            Bounds(recipe,out V3 actualMin,out V3 actualMax);
            if((actualMin-recipe.Minimum).LengthSquared>1e-10f||(actualMax-recipe.Maximum).LengthSquared>1e-10f)throw new InvalidOperationException(recipe.Name+": recorded bounds do not match actual vertices.");
            if(Math.Abs(recipe.Minimum.Y)>1e-5f||Math.Abs(recipe.Minimum.X+recipe.Maximum.X)>1e-5f||Math.Abs(recipe.Minimum.Z+recipe.Maximum.Z)>1e-5f)throw new InvalidOperationException(recipe.Name+": bottom-centre pivot invalid.");
            for(int i=0;i<recipe.Vertices.Count;i++)if(!Finite(recipe.Vertices[i])||!Finite(recipe.Normals[i])||Math.Abs(recipe.Normals[i].LengthSquared-1)>1e-4f||!Finite(recipe.UV[i].X)||!Finite(recipe.UV[i].Y))throw new InvalidOperationException(recipe.Name+": nonfinite data or nonunit normal.");
            for(int material=0;material<PaletteCount;material++) {
                var indices=recipe.Triangles[material];if(indices.Count%3!=0)throw new InvalidOperationException("Non-triangle index list.");
                for(int t=0;t<indices.Count;t+=3) {
                    int a=indices[t],b=indices[t+1],c=indices[t+2];if(a<0||b<0||c<0||a>=recipe.Vertices.Count||b>=recipe.Vertices.Count||c>=recipe.Vertices.Count)throw new InvalidOperationException("Index out of range.");
                    V3 normal=V3.Cross(recipe.Vertices[b]-recipe.Vertices[a],recipe.Vertices[c]-recipe.Vertices[a]);
                    if(normal.LengthSquared<1e-12f||V3.Dot(normal.Normalized,recipe.Normals[a])<.99f)throw new InvalidOperationException(recipe.Name+": degenerate triangle or winding mismatch.");
                }
            }
            var names=new HashSet<string>(StringComparer.Ordinal);foreach(var anchor in recipe.Anchors)if(!names.Add(anchor.Name)||!Finite(anchor.Position)||!ValidRotation(anchor.Rotation))throw new InvalidOperationException(recipe.Name+": invalid anchor.");
            if(!names.Contains("Anchor_Mount")||!names.Contains("Anchor_Interaction")||!names.Contains("Anchor_SFX"))throw new InvalidOperationException("Required neutral anchors missing.");
        }
        static bool ValidRotation(Rotation rotation)
        {
            return Finite(rotation.X)&&Finite(rotation.Y)&&Finite(rotation.Z)&&Math.Abs(rotation.X.LengthSquared-1)<1e-4f&&Math.Abs(rotation.Y.LengthSquared-1)<1e-4f&&Math.Abs(rotation.Z.LengthSquared-1)<1e-4f&&
                Math.Abs(V3.Dot(rotation.X,rotation.Y))<1e-4f&&Math.Abs(V3.Dot(rotation.X,rotation.Z))<1e-4f&&Math.Abs(V3.Dot(rotation.Y,rotation.Z))<1e-4f&&V3.Dot(V3.Cross(rotation.X,rotation.Y),rotation.Z)>.9999f;
        }
        public static int ValidateClosedPrimitives(Recipe recipe)
        {
            int checkedEdges=0;
            foreach(var primitive in recipe.Primitives) {
                var counts=new Dictionary<string,int>(StringComparer.Ordinal);var indices=recipe.Triangles[primitive.Material];
                for(int t=primitive.FirstIndex;t<primitive.FirstIndex+primitive.IndexCount;t+=3)for(int side=0;side<3;side++) {
                    string a=PointKey(recipe.Vertices[indices[t+side]]),b=PointKey(recipe.Vertices[indices[t+(side+1)%3]]);
                    string edge=string.CompareOrdinal(a,b)<0?a+"|"+b:b+"|"+a;counts.TryGetValue(edge,out int count);counts[edge]=count+1;
                }
                foreach(var count in counts.Values){checkedEdges++;if(count!=2)throw new InvalidOperationException(recipe.Name+": open/nonmanifold "+primitive.Kind+" primitive.");}
            }
            return checkedEdges;
        }
        static string PointKey(V3 p) { return CoordinateKey(p.X)+","+CoordinateKey(p.Y)+","+CoordinateKey(p.Z); }
        static string CoordinateKey(float value) { double rounded=Math.Round(value*100000d);return rounded==0?"0":rounded.ToString(CultureInfo.InvariantCulture); }
        public static string Fingerprint(Recipe[] recipes,Paint[] palette)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
                writer.Write(GeneratorId);writer.Write(RecipeVersion);writer.Write(palette.Length);writer.Write(recipes.Length);
                foreach(var paint in palette){writer.Write(paint.Name);writer.Write(paint.R);writer.Write(paint.G);writer.Write(paint.B);writer.Write(paint.Metallic);writer.Write(paint.Smoothness);writer.Write(paint.Emission);}
                foreach(var recipe in recipes){writer.Write(recipe.Name);writer.Write(recipe.Description);writer.Write(recipe.Vertices.Count);foreach(var p in recipe.Vertices)Write(writer,p);foreach(var n in recipe.Normals)Write(writer,n);foreach(var uv in recipe.UV){writer.Write(uv.X);writer.Write(uv.Y);}foreach(var list in recipe.Triangles){writer.Write(list.Count);foreach(int index in list)writer.Write(index);}writer.Write(recipe.Anchors.Count);foreach(var anchor in recipe.Anchors){writer.Write(anchor.Name);Write(writer,anchor.Position);Write(writer,anchor.Rotation.X);Write(writer,anchor.Rotation.Y);Write(writer,anchor.Rotation.Z);}}
                writer.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","");
            }
        }
        static void Write(BinaryWriter writer,V3 point){writer.Write(point.X);writer.Write(point.Y);writer.Write(point.Z);}
    }
}
