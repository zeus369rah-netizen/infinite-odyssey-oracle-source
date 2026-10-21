using System;
namespace OdysseyOracle
{
    public readonly struct LandingFixturePose020
    {
        public readonly LandingPoint020 Flame;
        public readonly float FloorY,LowFloorY,Yaw,Cos,Sin;
        public LandingFixturePose020(LandingPoint020 flame,float floor,float low,float yaw,float c,float s){Flame=flame;FloorY=floor;LowFloorY=low;Yaw=yaw;Cos=c;Sin=s;}
    }
    public readonly struct LandingFixturePart020
    {
        public readonly LandingPoint020 Center,Size;
        public readonly float Yaw;
        public readonly byte Material;
        public LandingFixturePart020(LandingPoint020 center,LandingPoint020 size,float yaw,byte material){Center=center;Size=size;Yaw=yaw;Material=material;}
    }
    public static class PortalLandingFixtures020
    {
        public const int Count=8,RigidPartCount=4,CoreObstacleCount=127,CombinedObstacleCount=135;
        public const byte ColliderKind=6;
        public const int ExpectedLayoutVersion=2,ExpectedContourRevision=2;
        public static bool TryPose(int index,out LandingFixturePose020 pose)
        {
            pose=default;switch(index)
            {
                case 0: pose=new LandingFixturePose020(new LandingPoint020(-9.3f,2.9f,19.6f),0f,0f,0f,1f,0f);return true;
                case 1: pose=new LandingFixturePose020(new LandingPoint020(9.3f,2.9f,19.6f),0f,0f,0f,1f,0f);return true;
                case 2: pose=new LandingFixturePose020(new LandingPoint020(-9.3f,5.7f,29f),3.25f,3f,0f,1f,0f);return true;
                case 3: pose=new LandingFixturePose020(new LandingPoint020(9.3f,5.7f,29f),3.25f,3f,0f,1f,0f);return true;
                case 4: pose=new LandingFixturePose020(new LandingPoint020(-6.8f,9.2f,48.65f),6f,6f,0f,1f,0f);return true;
                case 5: pose=new LandingFixturePose020(new LandingPoint020(6.8f,9.2f,48.65f),6f,6f,0f,1f,0f);return true;
                case 6: pose=new LandingFixturePose020(new LandingPoint020(-70f*.9510565f+4.2f*.309017f,2.4f,70f*.309017f+4.2f*.9510565f),0f,0f,18f,.9510565f,.309017f);return true;
                case 7: pose=new LandingFixturePose020(new LandingPoint020(70f*.9510565f-4.2f*.309017f,2.4f,-70f*.309017f-4.2f*.9510565f),0f,0f,18f,.9510565f,.309017f);return true;
                default:return false;
            }
        }
        // The same exact Flame point is consumed by mesh flames, actual lights and reflections.
        public static bool TryTorch(int index,out LandingPoint020 point)
        {point=default;if(!TryPose(index,out var pose))return false;point=pose.Flame;return true;}
        static uint Morton(int index){switch(index){case 0:return 618988u;case 1:return 795833u;case 2:return 621412u;case 3:return 798257u;case 4:return 652312u;case 5:return 827725u;case 6:return 556396u;case 7:return 492179u; default:return 0;}}
        // One conservative post/plinth envelope only. Bowl and fire have no separate colliders.
        public static bool TryObstacle(int index,out LandingBox020 box)
        {
            box=default;if(!TryPose(index,out var p))return false;
            box=new LandingBox020(p.Flame.X-.30f,p.LowFloorY,p.Flame.Z-.27f,p.Flame.X+.30f,p.Flame.Y-.56f,p.Flame.Z+.27f,(uint)(4000+index),ColliderKind,0,Morton(index),p.Yaw,p.Cos,p.Sin);return true;
        }
        // Keeps all127 frozen core records unchanged and Morton-sorted when the eight records are added.
        public static bool TryMergedObstacleSlot(int index,out int core,out int fixture)
        {
            core=fixture=-1;if(index<0||index>=CombinedObstacleCount)return false;int before=0;
            if(index==72){fixture=7;return true;}if(index>72)before++;
            if(index==81){fixture=6;return true;}if(index>81)before++;
            if(index==88){fixture=0;return true;}if(index>88)before++;
            if(index==96){fixture=2;return true;}if(index>96)before++;
            if(index==105){fixture=4;return true;}if(index>105)before++;
            if(index==107){fixture=1;return true;}if(index>107)before++;
            if(index==113){fixture=3;return true;}if(index>113)before++;
            if(index==130){fixture=5;return true;}if(index>130)before++;
            core=index-before;return core>=0&&core<CoreObstacleCount;
        }
        // Cold-only support admission on the actual value floor source. No Unity object, Physics or storage allocation.
        public static bool ValidateSupport(int index)
        {
            if(PortalLandingLayout020.Version!=ExpectedLayoutVersion||PortalLandingContour020.Revision!=ExpectedContourRevision||!TryPose(index,out var p))return false;
            if(!PortalLandingQueries020.TryFloor(p.Flame.X,p.Flame.Z,p.Flame.Y,out float center)||Math.Abs(center-p.FloorY)>.002f)return false;
            for(int corner=0;corner<4;corner++)
            {
                float x=(corner&1)==0?-.30f:.30f,z=(corner&2)==0?-.27f:.27f;
                // A mid-stair base is a nosing footing on the low half plus one full cap on the upper tread.
                float expected=p.FloorY;if(p.LowFloorY<p.FloorY){z=(corner&2)==0?-.27f:-.0001f;expected=p.LowFloorY;}
                float wx=p.Flame.X+x*p.Cos+z*p.Sin,wz=p.Flame.Z-x*p.Sin+z*p.Cos;
                if(!PortalLandingQueries020.TryFloor(wx,wz,p.Flame.Y,out float floor)||Math.Abs(floor-expected)>.002f)return false;
            }
            return true;
        }
        public static bool ValidateAllSupport(out int rejected)
        {rejected=-1;for(int i=0;i<Count;i++)if(!ValidateSupport(i)){rejected=i;return false;}return true;}
        public static bool TryRigidPart(int index,int part,out LandingFixturePart020 value)
        {
            value=default;if(part<0||part>=RigidPartCount||!TryPose(index,out var p))return false;
            float x=p.Flame.X,z=p.Flame.Z,y,h,sx,sz,yaw=p.Yaw;byte material=0;
            if(part==0)
            {
                sx=.60f;sz=.54f;
                if(p.LowFloorY<p.FloorY){z-=.135f;sz=.27f;y=(p.LowFloorY+p.FloorY)*.5f;h=p.FloorY-p.LowFloorY;}
                else{y=p.FloorY+.04f;h=.08f;}
            }
            else if(part==1){sx=.52f;sz=.46f;h=.08f;y=p.FloorY+(p.LowFloorY<p.FloorY?.04f:.12f);}
            else if(part==2){sx=sz=.44f;h=p.Flame.Y-.56f-p.FloorY;y=p.FloorY+h*.5f;material=1;}
            else{sx=sz=.75f;h=.22f;y=p.Flame.Y-.45f;yaw=0;material=1;}
            value=new LandingFixturePart020(new LandingPoint020(x,y,z),new LandingPoint020(sx,h,sz),yaw,material);return h>0;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual frozen two-floor world metres, grounded fixture bases; bridge post/plinth yaw18 uses the real deck axes, fire/bowl remain original world-aligned.
// Array Formats: Eight immutable poses, four cold rigid parts each, eight scalar post/plinth collider records and a stable135-slot Morton merge; no frame heap or per-flame collider.
// Dependencies/Pointers: Root binds the same TryTorch poses to geometry/lights/reflections and skips opaque colliderKind6 rendering. Eight cold support checks and exact127-core hash guard are required before integration; no actual source/Asset/provider authority.
