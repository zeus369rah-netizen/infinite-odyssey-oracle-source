using System;
using P3=OdysseyOracle.TutorialRoomV2Layout.P3;
namespace OdysseyOracle
{
    internal static class InteriorFloraRecipe020
    {
        internal readonly struct Tint
        {
            internal readonly float R,G,B;
            internal Tint(float r,float g,float b){R=r;G=g;B=b;}
        }
        internal readonly struct Stem
        {
            internal readonly P3 Center,Size;
            internal Stem(P3 center,P3 size){Center=center;Size=size;}
        }
        internal readonly struct Leaf
        {
            internal readonly P3 A,B,C,D,Ridge;
            internal readonly int Side;
            internal Leaf(P3 a,P3 b,P3 c,P3 d,P3 ridge,int side){A=a;B=b;C=c;D=d;Ridge=ridge;Side=side;}
        }
        static uint Hash(int x,int y,int seed)
        {unchecked{uint n=(uint)(x*374761393+y*668265263+seed*982451653);n=(n^(n>>13))*1274126177u;return n^(n>>16);}}
        internal static Tint MossPixel(int x,int y)
        {
            float coarse=(Hash(x/4,y/4,79)&255u)/255f,fine=(Hash(x,y,113)&255u)/255f;
            float grain=.94f+fine*.10f,growth=Math.Max(0,(coarse-.58f)*2.2f)*(fine>.18f?1f:0f);
            return new Tint((.50f*(1-growth)+.22f*growth)*grain,
                (.49f*(1-growth)+.31f*growth)*grain,(.43f*(1-growth)+.14f*growth)*grain);
        }
        internal static bool CanAttach(TutorialRoomV2Layout layout,int side,float z)
        {
            if(layout==null||side!=1&&side!=-1||float.IsNaN(z)||float.IsInfinity(z))return false;
            const float half=.28f,bottom=5.65f,top=8.0f,apron=.35f;
            if(z-half<TutorialRoomV2Layout.StartZ+.2f||z+half>TutorialRoomV2Layout.EndZ-.2f||top>layout.CeilingY-.4f)return false;
            foreach(float c in layout.AlcoveCenters)
                if(z+half>=c-TutorialRoomV2Layout.DoorHalfWidth-apron&&z-half<=c+TutorialRoomV2Layout.DoorHalfWidth+apron)return false;
            float x=side*layout.HalfWidth,minX=x-.05f,maxX=x+.05f;
            foreach(var placement in layout.PropPlacements)
            {
                var bounds=RefugePlacementPolicy.At(placement);
                float loX=bounds.Min.X-.2f,hiX=bounds.Max.X+.2f,loY=bounds.Min.Y-.2f,hiY=bounds.Max.Y+.2f;
                float loZ=bounds.Min.Z-apron,hiZ=bounds.Max.Z+apron;
                if(placement.Id==TutorialRoomV2Layout.PropId.WallLantern)
                {
                    float chain=placement.Position.X+(placement.Position.X<0?1:-1)*.49f;
                    loX=Math.Min(loX,chain-.2f);hiX=Math.Max(hiX,chain+.2f);hiY=layout.CeilingY;
                }
                if(minX<hiX&&maxX>loX&&bottom<hiY&&top>loY&&z-half<hiZ&&z+half>loZ)return false;
            }
            return true;
        }
        internal static int Build(TutorialRoomV2Layout layout,Action<Stem> stem,Action<Leaf> leaf)
        {
            if(layout==null||stem==null||leaf==null)throw new ArgumentException("Cold supported layout and writers required.");
            int count=0;
            for(int side=-1;side<=1;side+=2)
            foreach(float z in layout.PierRows)
            {
                if(!CanAttach(layout,side,z))continue;
                float plane=side*layout.HalfWidth;
                stem(new Stem(new P3(plane-side*.011f,6.82f,z),new P3(.026f,2.20f,.024f)));
                for(int index=0;index<7;index++)
                {
                    float y=7.70f-index*.30f,along=z+(index%2==0?-.085f:.085f),edge=plane+side*.001f;
                    leaf(new Leaf(new P3(edge,y-.14f,along-.02f),new P3(edge,y,along-.105f),
                        new P3(edge,y+.14f,along+.02f),new P3(edge,y,along+.105f),new P3(plane-side*.026f,y,along),side));
                }
                count++;
            }
            return count;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge inner hall wall planes, Y-up metres; 1mm embedded leaf boundary and26mm supported relief.
// Array Formats: Pure bounded12stem/84leaf value descriptors and deterministic32x32 texture samples; no Unity/native/frame owner.
// Dependencies/Pointers: Current layout doors/planes and exact saved-prop bounds plus lantern chain keepouts; World sink owns batching/resources. No structural mesh, collider, arena, actor, light or gameplay mutation.
