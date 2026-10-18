using System;
using P3=OdysseyOracle.TutorialRoomV2Layout.P3;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Cold scalar metadata from the six ACTUAL saved OraclePropKit_v1 mesh bounds.
    // Metres, neutral bottom-centre pivot. No geometry generation or Unity objects.
    // Source-base.json pins the saved meshes/prefabs; changing that library requires
    // an explicit metadata/version review rather than silently accepting new bounds.
    public static class RefugePlacementPolicy
    {
        public readonly struct Extent
        {
            public readonly P3 Min,Max;
            public Extent(P3 min,P3 max){Min=min;Max=max;}
            public bool Intersects(Extent b)=>Min.X<b.Max.X&&Max.X>b.Min.X&&Min.Y<b.Max.Y&&Max.Y>b.Min.Y&&Min.Z<b.Max.Z&&Max.Z>b.Min.Z;
        }
        public static Extent Expected(TutorialRoomV2Layout.PropId id)
        {
            switch(id){
                case TutorialRoomV2Layout.PropId.RunicBookstand:return new Extent(new P3(-.405f,0,-.264601f),new P3(.405f,1.1893448f,.264601f));
                case TutorialRoomV2Layout.PropId.ForgeWorkbench:return new Extent(new P3(-.78f,0,-.47125f),new P3(.78f,1.2399999f,.47125f));
                case TutorialRoomV2Layout.PropId.SuppliesRack:return new Extent(new P3(-.67499995f,0,-.32f),new P3(.67499995f,1.76f,.32f));
                case TutorialRoomV2Layout.PropId.BrazierRing:return new Extent(new P3(-.512f,0,-.512f),new P3(.512f,.59749997f,.512f));
                case TutorialRoomV2Layout.PropId.WallLantern:return new Extent(new P3(-.245f,0,-.3485881f),new P3(.245f,.8434299f,.3485881f));
                case TutorialRoomV2Layout.PropId.CrystalReliquary:return new Extent(new P3(-.4f,0,-.4f),new P3(.4f,1.25f,.4f));
                default:throw new ArgumentOutOfRangeException(nameof(id));
            }
        }
        public static P3 LanternMount=>new P3(0,.4175f,-.3535881f);
        public static P3 RootPosition(TutorialRoomV2Layout.Placement placement)
        {
            if(string.IsNullOrEmpty(placement.AlignAnchor))return placement.Position;
            if(placement.Id!=TutorialRoomV2Layout.PropId.WallLantern||placement.AlignAnchor!="Anchor_Mount")throw new ArgumentException("Unsupported library mount contract.");
            return placement.Position-Rotate(LanternMount,placement.Yaw);
        }
        public static Extent At(TutorialRoomV2Layout.Placement placement)
        {
            Extent local=Expected(placement.Id);P3 root=RootPosition(placement);
            float minX=float.PositiveInfinity,minY=minX,minZ=minX,maxX=float.NegativeInfinity,maxY=maxX,maxZ=maxX;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++){
                P3 p=Rotate(new P3(x==0?local.Min.X:local.Max.X,y==0?local.Min.Y:local.Max.Y,z==0?local.Min.Z:local.Max.Z),placement.Yaw)+root;
                minX=Math.Min(minX,p.X);minY=Math.Min(minY,p.Y);minZ=Math.Min(minZ,p.Z);maxX=Math.Max(maxX,p.X);maxY=Math.Max(maxY,p.Y);maxZ=Math.Max(maxZ,p.Z);
            }return new Extent(new P3(minX,minY,minZ),new P3(maxX,maxY,maxZ));
        }
        static P3 Rotate(P3 p,float yaw)
        {double a=yaw*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);return new P3((float)(p.X*c+p.Z*s),p.Y,(float)(-p.X*s+p.Z*c));}
        // Pose data only. The existing NPC owner retains animation/visibility and
        // authority; neutral asset recipes must never bake these world positions.
        public static bool TryGetNpcPose(int role,out P3 feet,out float yaw)
        {
            if(role==2){feet=new P3(-6.35f,0,9);yaw=90;return true;}
            if(role==3){feet=new P3(4.3f,0,19);yaw=270;return true;}
            feet=default;yaw=0;return false;
        }
        public static P3 NpcApproach(int role)
        {
            if(role==2)return new P3(-4.55f,0,9);
            if(role==3)return new P3(2.5f,0,19);
            throw new ArgumentOutOfRangeException(nameof(role));
        }
    }
}
