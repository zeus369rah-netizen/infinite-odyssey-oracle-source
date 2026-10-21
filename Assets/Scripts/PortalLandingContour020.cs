using System;
namespace OdysseyOracle
{
    public readonly struct LandingGuardPart020
    {
        public readonly LandingPoint020 Center,Size;
        public readonly byte Material;
        public LandingGuardPart020(LandingPoint020 center,LandingPoint020 size,byte material){Center=center;Size=size;Material=material;}
    }
    public static class PortalLandingContour020
    {
        public const int Revision=2,FloorBandCount=18,UpperBandCount=12,LowerBandCount=6,MaximumGuardParts=32;
        public const byte GuardKind=5;
        public const float GuardHeight=1f,MaximumPostSpacing=6f,RecoveryPlaneY=-18f;
        public static LandingPoint020 TreeFeet=>new LandingPoint020(0,-6,-62);
        public static LandingPoint020 PartyTableFeet=>new LandingPoint020(0,0,-10);
        public static bool TryFloorBand(int index,out LandingBox020 value)
        {
            switch(index)
            {
                case 0: value=new LandingBox020(-58f,-0.8f,-30f,-32f,0f,-24f,10u,1,0,234096u,0f,1f,0f);return true;
                case 1: value=new LandingBox020(-60f,-0.8f,-24f,-30f,0f,-20f,11u,1,0,235728u,0f,1f,0f);return true;
                case 2: value=new LandingBox020(40f,-0.8f,-34f,60f,0f,-20f,12u,1,0,447072u,0f,1f,0f);return true;
                case 3: value=new LandingBox020(-60f,-0.8f,-20f,60f,0f,-12f,13u,1,0,434176u,0f,1f,0f);return true;
                case 4: value=new LandingBox020(-62f,-0.8f,-12f,61f,0f,-4f,14u,1,0,261460u,0f,1f,0f);return true;
                case 5: value=new LandingBox020(-60f,-0.8f,-4f,62f,0f,4f,15u,1,0,786448u,0f,1f,0f);return true;
                case 6: value=new LandingBox020(-60f,-0.8f,4f,60f,0f,14f,16u,1,0,788512u,0f,1f,0f);return true;
                case 7: value=new LandingBox020(-60f,-0.8f,14f,59f,0f,20f,17u,1,0,619892u,0f,1f,0f);return true;
                case 8: value=new LandingBox020(-60f,-0.8f,20f,-10f,0f,30f,18u,1,0,605488u,0f,1f,0f);return true;
                case 9: value=new LandingBox020(10f,-0.8f,20f,59f,0f,30f,19u,1,0,813156u,0f,1f,0f);return true;
                case 10: value=new LandingBox020(-54f,-0.8f,30f,-10f,0f,38f,20u,1,0,639104u,0f,1f,0f);return true;
                case 11: value=new LandingBox020(10f,-0.8f,30f,56f,0f,38f,21u,1,0,835728u,0f,1f,0f);return true;
                case 12: value=new LandingBox020(-20f,-6.8f,-84f,22f,-6f,-80f,30u,1,0,297616u,0f,1f,0f);return true;
                case 13: value=new LandingBox020(-31f,-6.8f,-80f,32f,-6f,-74f,31u,1,0,303268u,0f,1f,0f);return true;
                case 14: value=new LandingBox020(-38f,-6.8f,-74f,40f,-6f,-66f,32u,1,0,305296u,0f,1f,0f);return true;
                case 15: value=new LandingBox020(-40f,-6.8f,-66f,39f,-6f,-58f,33u,1,0,218580u,0f,1f,0f);return true;
                case 16: value=new LandingBox020(-37f,-6.8f,-58f,40f,-6f,-50f,34u,1,0,395412u,0f,1f,0f);return true;
                case 17: value=new LandingBox020(-30f,-6.8f,-50f,32f,-6f,-44f,35u,1,0,401456u,0f,1f,0f);return true;
                default:value=default;return false;
            }
        }
        public static int GuardPartCount(in LandingBox020 box)
        {
            if(box.Kind!=GuardKind)return 0;
            float length=Math.Max(box.MaxX-box.MinX,box.MaxZ-box.MinZ);
            return 3+Math.Max(2,(int)Math.Ceiling(length/MaximumPostSpacing)+1);
        }
        // Part centers are envelope-local before its yaw rotation; sizes stay inside the physical envelope.
        // Material0=stone kerb/post,1=timber/metal rail; caller emits these parts instead of the opaque envelope box.
        public static bool TryGuardPart(in LandingBox020 box,int index,out LandingGuardPart020 part)
        {
            part=default;int count=GuardPartCount(box);if(index<0||index>=count||count>MaximumGuardParts)return false;
            float sx=box.MaxX-box.MinX,sz=box.MaxZ-box.MinZ,h=box.MaxY-box.MinY;bool alongX=sx>=sz;
            float length=alongX?sx:sz,cross=alongX?sz:sx;
            float y,sizeY,sizeAlong,sizeCross;byte material;
            if(index==0){y=-h*.5f+.08f;sizeY=.16f;sizeAlong=length;sizeCross=cross;material=0;}
            else if(index==1){y=0;sizeY=.08f;sizeAlong=length;sizeCross=Math.Min(.16f,cross);material=1;}
            else if(index==2){y=h*.5f-.05f;sizeY=.10f;sizeAlong=length;sizeCross=Math.Min(.18f,cross);material=1;}
            else
            {
                int posts=count-3;float width=Math.Min(.24f,Math.Min(length,cross));float t=posts<=1?.5f:(float)(index-3)/(posts-1);
                float offset=(-length*.5f+width*.5f)+(length-width)*t;
                part=new LandingGuardPart020(new LandingPoint020(alongX?offset:0,0,alongX?0:offset),new LandingPoint020(width,h,width),0);return true;
            }
            part=new LandingGuardPart020(new LandingPoint020(0,y,0),new LandingPoint020(alongX?sizeAlong:sizeCross,sizeY,alongX?sizeCross:sizeAlong),material);return true;
        }
        // These are positively query-validated fallback candidates, never an automatic movement or teleport command.
        public static bool TryRecoveryAnchor(int layer,out LandingPoint020 feet)
        {feet=layer==0?new LandingPoint020(0,.05f,4):new LandingPoint020(8,-5.95f,-50);return layer>=0&&layer<=1;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original asymmetric worldXZ row union; top/undersideY0/-.8 and-6/-6.8, no hidden backing slab.
// Array Formats: Eighteen immutable nonoverlapping floor bands and bounded scalar guard decomposition; no Object, heap or frame storage.
// Dependencies/Pointers: Layout owns floors/colliders; geometry must emit guard parts inside their same envelope. Recovery caller must retain accepted generation/revision and actual support authority; this helper never teleports.
