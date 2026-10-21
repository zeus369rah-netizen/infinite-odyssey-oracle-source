using System;
namespace OdysseyOracle
{
    public readonly struct LandingPoint020
    {
        public readonly float X,Y,Z;
        public LandingPoint020(float x,float y,float z){X=x;Y=y;Z=z;}
    }
    public readonly struct LandingBox020
    {
        public readonly float MinX,MinY,MinZ,MaxX,MaxY,MaxZ,Yaw,Cos,Sin;
        public readonly uint Key,Morton;
        public readonly byte Kind,Endpoint;
        public LandingBox020(float ax,float ay,float az,float bx,float by,float bz,uint key,byte kind,byte endpoint,uint morton,float yaw,float cosine,float sine)
        {MinX=ax;MinY=ay;MinZ=az;MaxX=bx;MaxY=by;MaxZ=bz;Key=key;Kind=kind;Endpoint=endpoint;Morton=morton;Yaw=yaw;Cos=cosine;Sin=sine;}
        public LandingPoint020 Local(LandingPoint020 p){float x=(MinX+MaxX)*.5f,z=(MinZ+MaxZ)*.5f,dx=p.X-x,dz=p.Z-z;return new LandingPoint020(x+dx*Cos-dz*Sin,p.Y,z+dx*Sin+dz*Cos);}
    }
    public struct LandingAdmission020
    {
        public uint Kind,Generation,StoryRevision,UnlockedEndpoints;
        public byte Ready,Authority;
    }
    public readonly struct LandingEndpoint020
    {
        public readonly int Id;
        public readonly uint Destination;
        public readonly LandingPoint020 Request,Target;
        public LandingEndpoint020(int id,uint destination,LandingPoint020 request,LandingPoint020 target)
        {Id=id;Destination=destination;Request=request;Target=target;}
    }
    public static class PortalLandingLayout020
    {
        public const uint ContextKind=0x504c3032u;
        public const int Version=2,SurfaceCount=69,ObstacleCount=CampaignLandingObstacleSet020.Count,EndpointCount=2,AnchorCount=11;
        public const float BodyRadius=.325f,BodyHeight=1.85f,MaximumStep=.30f,MaximumDrop=.45f;
        public static bool Active(in LandingAdmission020 context)=>context.Kind==ContextKind&&context.Generation!=0&&context.StoryRevision!=0&&context.Ready==1&&(context.UnlockedEndpoints&~3u)==0;
        public static bool Unlocked(in LandingAdmission020 context,int endpoint)=>Active(context)&&endpoint>=1&&endpoint<=2&&(context.UnlockedEndpoints&(1u<<(endpoint-1)))!=0;
        public static string EndpointLabel(int endpoint)=>endpoint==1?"Lantern Town":endpoint==2?"Bloodstorm Gardens":"";
        public static string EndpointState(in LandingAdmission020 context,int endpoint)=>Unlocked(context,endpoint)?"Unlocked":"Locked";
        public static bool TryEndpoint(int endpoint,out LandingEndpoint020 value)
        {
            value=default;if(endpoint<1||endpoint>2)return false;
            float sign=endpoint==1?-1f:1f;
            value=new LandingEndpoint020(endpoint,(uint)endpoint,new LandingPoint020(sign*91f*.9510565f,0f,-sign*91f*.309017f),new LandingPoint020(sign*93.3f*.9510565f,1.35f,-sign*93.3f*.309017f));return true;
        }
        public static bool TryMorton(float x,float z,out uint key)
        {
            key=0;if(!Finite(x)||!Finite(z)||x< -128f||x>=128f||z< -128f||z>=128f)return false;
            uint a=(uint)((x+128f)*4f),b=(uint)((z+128f)*4f);
            for(int bit=0;bit<10;bit++)key|=((a>>bit)&1u)<<(bit*2)|((b>>bit)&1u)<<(bit*2+1);
            return true;
        }
        public static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static bool Finite(LandingPoint020 value)=>Finite(value.X)&&Finite(value.Y)&&Finite(value.Z);
        public static bool TrySurface(int index,out LandingBox020 value)
        {
            switch(index)
            {
                case 0: return PortalLandingContour020.TryFloorBand(15,out value);
                case 1: return PortalLandingContour020.TryFloorBand(0,out value);
                case 2: return PortalLandingContour020.TryFloorBand(1,out value);
                case 3: return PortalLandingContour020.TryFloorBand(4,out value);
                case 4: return PortalLandingContour020.TryFloorBand(12,out value);
                case 5: return PortalLandingContour020.TryFloorBand(13,out value);
                case 6: return PortalLandingContour020.TryFloorBand(14,out value);
                case 7: return PortalLandingContour020.TryFloorBand(16,out value);
                case 8: return PortalLandingContour020.TryFloorBand(17,out value);
                case 9: value=new LandingBox020(-12f,-6.8f,-44f,12f,-6f,-43f,423u,1,0,401928u,0f,1f,0f);return true;
                case 10: value=new LandingBox020(-12f,-6.8f,-43f,12f,-5.75f,-42f,422u,1,0,401960u,0f,1f,0f);return true;
                case 11: value=new LandingBox020(-12f,-6.8f,-42f,12f,-5.5f,-41f,421u,1,0,402056u,0f,1f,0f);return true;
                case 12: value=new LandingBox020(-12f,-6.8f,-41f,12f,-5.25f,-40f,420u,1,0,402088u,0f,1f,0f);return true;
                case 13: value=new LandingBox020(-12f,-6.8f,-40f,12f,-5f,-39f,419u,1,0,403464u,0f,1f,0f);return true;
                case 14: value=new LandingBox020(-12f,-6.8f,-39f,12f,-4.75f,-38f,418u,1,0,403496u,0f,1f,0f);return true;
                case 15: value=new LandingBox020(-12f,-6.8f,-38f,12f,-4.5f,-37f,417u,1,0,403592u,0f,1f,0f);return true;
                case 16: value=new LandingBox020(-12f,-6.8f,-37f,12f,-4.25f,-36f,416u,1,0,403624u,0f,1f,0f);return true;
                case 17: value=new LandingBox020(-12f,-6.8f,-36f,12f,-4f,-35f,415u,1,0,403976u,0f,1f,0f);return true;
                case 18: value=new LandingBox020(-12f,-6.8f,-35f,12f,-3.75f,-34f,414u,1,0,404008u,0f,1f,0f);return true;
                case 19: value=new LandingBox020(-12f,-6.8f,-34f,12f,-3.5f,-33f,413u,1,0,404104u,0f,1f,0f);return true;
                case 20: value=new LandingBox020(-12f,-6.8f,-33f,12f,-3.25f,-32f,412u,1,0,404136u,0f,1f,0f);return true;
                case 21: value=new LandingBox020(-12f,-6.8f,-32f,12f,-3f,-31f,411u,1,0,425992u,0f,1f,0f);return true;
                case 22: value=new LandingBox020(-12f,-6.8f,-31f,12f,-2.75f,-30f,410u,1,0,426024u,0f,1f,0f);return true;
                case 23: value=new LandingBox020(-12f,-6.8f,-30f,12f,-2.5f,-29f,409u,1,0,426120u,0f,1f,0f);return true;
                case 24: value=new LandingBox020(-12f,-6.8f,-29f,12f,-2.25f,-28f,408u,1,0,426152u,0f,1f,0f);return true;
                case 25: value=new LandingBox020(-12f,-6.8f,-28f,12f,-2f,-27f,407u,1,0,426504u,0f,1f,0f);return true;
                case 26: value=new LandingBox020(-12f,-6.8f,-27f,12f,-1.75f,-26f,406u,1,0,426536u,0f,1f,0f);return true;
                case 27: value=new LandingBox020(-12f,-6.8f,-26f,12f,-1.5f,-25f,405u,1,0,426632u,0f,1f,0f);return true;
                case 28: value=new LandingBox020(-12f,-6.8f,-25f,12f,-1.25f,-24f,404u,1,0,426664u,0f,1f,0f);return true;
                case 29: value=new LandingBox020(-12f,-6.8f,-24f,12f,-1f,-23f,403u,1,0,428040u,0f,1f,0f);return true;
                case 30: value=new LandingBox020(-12f,-6.8f,-23f,12f,-0.75f,-22f,402u,1,0,428072u,0f,1f,0f);return true;
                case 31: value=new LandingBox020(-12f,-6.8f,-22f,12f,-0.5f,-21f,401u,1,0,428168u,0f,1f,0f);return true;
                case 32: value=new LandingBox020(-12f,-6.8f,-21f,12f,-0.25f,-20f,400u,1,0,428200u,0f,1f,0f);return true;
                case 33: return PortalLandingContour020.TryFloorBand(3,out value);
                case 34: return PortalLandingContour020.TryFloorBand(2,out value);
                case 35: value=new LandingBox020(58.13346f,-0.8f,-28.91234f,92.13346f,0f,-19.91234f,6u,1,0,493304u,18f,0.9510565f,0.309017f);return true;
                case 36: value=new LandingBox020(-92.13346f,-0.8f,19.91234f,-58.13346f,0f,28.91234f,5u,1,0,555271u,18f,0.9510565f,0.309017f);return true;
                case 37: return PortalLandingContour020.TryFloorBand(8,out value);
                case 38: return PortalLandingContour020.TryFloorBand(7,out value);
                case 39: return PortalLandingContour020.TryFloorBand(10,out value);
                case 40: return PortalLandingContour020.TryFloorBand(5,out value);
                case 41: return PortalLandingContour020.TryFloorBand(6,out value);
                case 42: value=new LandingBox020(-10f,-0.8f,20f,10f,0.25f,20.75f,100u,1,0,795138u,0f,1f,0f);return true;
                case 43: value=new LandingBox020(-10f,-0.8f,20.75f,10f,0.5f,21.5f,101u,1,0,795168u,0f,1f,0f);return true;
                case 44: value=new LandingBox020(-10f,-0.8f,21.5f,10f,0.75f,22.25f,102u,1,0,795178u,0f,1f,0f);return true;
                case 45: value=new LandingBox020(-10f,-0.8f,22.25f,10f,1f,23f,103u,1,0,795272u,0f,1f,0f);return true;
                case 46: value=new LandingBox020(-10f,-0.8f,23f,10f,1.25f,23.75f,104u,1,0,795298u,0f,1f,0f);return true;
                case 47: value=new LandingBox020(-10f,-0.8f,23.75f,10f,1.5f,24.5f,105u,1,0,796672u,0f,1f,0f);return true;
                case 48: value=new LandingBox020(-10f,-0.8f,24.5f,10f,1.75f,25.25f,106u,1,0,796682u,0f,1f,0f);return true;
                case 49: value=new LandingBox020(-10f,-0.8f,25.25f,10f,2f,26f,107u,1,0,796712u,0f,1f,0f);return true;
                case 50: value=new LandingBox020(-10f,-0.8f,26f,10f,2.25f,26.75f,108u,1,0,796802u,0f,1f,0f);return true;
                case 51: value=new LandingBox020(-10f,-0.8f,26.75f,10f,2.5f,27.5f,109u,1,0,796832u,0f,1f,0f);return true;
                case 52: value=new LandingBox020(-10f,-0.8f,27.5f,10f,2.75f,28.25f,110u,1,0,796842u,0f,1f,0f);return true;
                case 53: value=new LandingBox020(-10f,-0.8f,28.25f,10f,3f,29f,111u,1,0,797192u,0f,1f,0f);return true;
                case 54: value=new LandingBox020(-10f,-0.8f,29f,10f,3.25f,29.75f,112u,1,0,797218u,0f,1f,0f);return true;
                case 55: value=new LandingBox020(-10f,-0.8f,29.75f,10f,3.5f,30.5f,113u,1,0,797312u,0f,1f,0f);return true;
                case 56: value=new LandingBox020(-10f,-0.8f,30.5f,10f,3.75f,31.25f,114u,1,0,797322u,0f,1f,0f);return true;
                case 57: value=new LandingBox020(-10f,-0.8f,31.25f,10f,4f,32f,115u,1,0,797352u,0f,1f,0f);return true;
                case 58: return PortalLandingContour020.TryFloorBand(9,out value);
                case 59: value=new LandingBox020(-10f,-0.8f,32f,10f,4.25f,32.75f,116u,1,0,819202u,0f,1f,0f);return true;
                case 60: value=new LandingBox020(-10f,-0.8f,32.75f,10f,4.5f,33.5f,117u,1,0,819232u,0f,1f,0f);return true;
                case 61: value=new LandingBox020(-10f,-0.8f,33.5f,10f,4.75f,34.25f,118u,1,0,819242u,0f,1f,0f);return true;
                case 62: value=new LandingBox020(-10f,-0.8f,34.25f,10f,5f,35f,119u,1,0,819336u,0f,1f,0f);return true;
                case 63: value=new LandingBox020(-10f,-0.8f,35f,10f,5.25f,35.75f,120u,1,0,819362u,0f,1f,0f);return true;
                case 64: value=new LandingBox020(-10f,-0.8f,35.75f,10f,5.5f,36.5f,121u,1,0,819712u,0f,1f,0f);return true;
                case 65: value=new LandingBox020(-10f,-0.8f,36.5f,10f,5.75f,37.25f,122u,1,0,819722u,0f,1f,0f);return true;
                case 66: value=new LandingBox020(-10f,-0.8f,37.25f,10f,6f,38f,123u,1,0,819752u,0f,1f,0f);return true;
                case 67: value=new LandingBox020(-10f,-0.8f,38f,10f,6f,54f,4u,1,0,821888u,0f,1f,0f);return true;
                case 68: return PortalLandingContour020.TryFloorBand(11,out value);
                default:value=default;return false;
            }
        }
        public static bool TryObstacle(int index,out LandingBox020 value)=>CampaignLandingObstacleSet020.TryObstacle(index,out value);
        internal static bool TryOriginalObstacle020(int index,out LandingBox020 value)
        {
            value=default;
            if(!CampaignPortalRecipe020.TryMergedObstacleSlot(index,out int previous,out int column))return false;
            return column>=0?CampaignPortalRecipe020.TryColumnObstacle(column,out value):TryBaseObstacle020(previous,out value);
        }
        static bool TryBaseObstacle020(int index,out LandingBox020 value)
        {
            value=default;
            if(!PortalLandingFixtures020.TryMergedObstacleSlot(index,out int core,out int fixture))return false;
            return fixture>=0?PortalLandingFixtures020.TryObstacle(fixture,out value):TryCoreObstacle020(core,out value);
        }
        static bool TryCoreObstacle020(int index,out LandingBox020 value)
        {
            switch(index)
            {
                case 0: value=new LandingBox020(-38f,-6f,-74.4f,-31f,-5f,-74f,1025u,5,0,112446u,0f,1f,0f);return true;
                case 1: value=new LandingBox020(-38.4f,-6f,-74f,-38f,-5f,-66f,1023u,5,0,113813u,0f,1f,0f);return true;
                case 2: value=new LandingBox020(-40f,-6f,-66.4f,-38f,-5f,-66f,1002u,5,0,114234u,0f,1f,0f);return true;
                case 3: value=new LandingBox020(-31f,-6f,-80.4f,-20f,-5f,-80f,1021u,5,0,117742u,0f,1f,0f);return true;
                case 4: value=new LandingBox020(-20.4f,-6f,-84f,-20f,-5f,-80f,1016u,5,0,118485u,0f,1f,0f);return true;
                case 5: value=new LandingBox020(-31.4f,-6f,-80f,-31f,-5f,-74f,1019u,5,0,123045u,0f,1f,0f);return true;
                case 6: value=new LandingBox020(-40.4f,-6f,-66f,-40f,-5f,-58f,1000u,5,0,201173u,0f,1f,0f);return true;
                case 7: value=new LandingBox020(-40f,-6f,-58f,-37f,-5f,-57.6f,1003u,5,0,202388u,0f,1f,0f);return true;
                case 8: value=new LandingBox020(-37.4f,-6f,-58f,-37f,-5f,-50f,1028u,5,0,203973u,0f,1f,0f);return true;
                case 9: value=new LandingBox020(-37f,-6f,-50f,-30f,-5f,-49.6f,1031u,5,0,204740u,0f,1f,0f);return true;
                case 10: value=new LandingBox020(-30.4f,-6f,-50f,-30f,-5f,-44f,1033u,5,0,221237u,0f,1f,0f);return true;
                case 11: value=new LandingBox020(-30f,-6f,-44f,-12f,-5f,-43.6f,1035u,5,0,222800u,0f,1f,0f);return true;
                case 12: value=new LandingBox020(-12.4f,-6f,-44f,-12f,-4.75f,-42f,1064u,5,0,225909u,0f,1f,0f);return true;
                case 13: value=new LandingBox020(-12.4f,-5.5f,-42f,-12f,-4.25f,-40f,1065u,5,0,226037u,0f,1f,0f);return true;
                case 14: value=new LandingBox020(-12.4f,-5f,-40f,-12f,-3.75f,-38f,1066u,5,0,227445u,0f,1f,0f);return true;
                case 15: value=new LandingBox020(-12.4f,-4.5f,-38f,-12f,-3.25f,-36f,1067u,5,0,227573u,0f,1f,0f);return true;
                case 16: value=new LandingBox020(-12.4f,-4f,-36f,-12f,-2.75f,-34f,1068u,5,0,227957u,0f,1f,0f);return true;
                case 17: value=new LandingBox020(-12.4f,-3.5f,-34f,-12f,-2.25f,-32f,1069u,5,0,228085u,0f,1f,0f);return true;
                case 18: value=new LandingBox020(-58.4f,0f,-30f,-58f,1f,-24f,1004u,5,0,230197u,0f,1f,0f);return true;
                case 19: value=new LandingBox020(-60f,0f,-24.4f,-58f,1f,-24f,1009u,5,0,230330u,0f,1f,0f);return true;
                case 20: value=new LandingBox020(-60.4f,0f,-24f,-60f,1f,-20f,1007u,5,0,231637u,0f,1f,0f);return true;
                case 21: value=new LandingBox020(-58f,0f,-30.4f,-32f,1f,-30f,1006u,5,0,233594u,0f,1f,0f);return true;
                case 22: value=new LandingBox020(-52f,0f,-24f,-38f,10f,-12f,303u,4,0,236240u,0f,1f,0f);return true;
                case 23: value=new LandingBox020(-60.4f,0f,-20f,-60f,1f,-12f,1037u,5,0,237653u,0f,1f,0f);return true;
                case 24: value=new LandingBox020(-62f,0f,-12.4f,-60f,1f,-12f,1013u,5,0,237818u,0f,1f,0f);return true;
                case 25: value=new LandingBox020(-62.4f,0f,-12f,-62f,1f,-4f,1011u,5,0,239637u,0f,1f,0f);return true;
                case 26: value=new LandingBox020(-62f,0f,-4f,-60f,1f,-3.6f,1015u,5,0,240208u,0f,1f,0f);return true;
                case 27: value=new LandingBox020(-32f,0f,-30f,-31.6f,1f,-24f,1005u,5,0,246304u,0f,1f,0f);return true;
                case 28: value=new LandingBox020(-32f,0f,-24.4f,-30f,1f,-24f,1010u,5,0,246458u,0f,1f,0f);return true;
                case 29: value=new LandingBox020(-30f,0f,-24f,-29.6f,1f,-20f,1008u,5,0,248000u,0f,1f,0f);return true;
                case 30: value=new LandingBox020(-30f,0f,-20.4f,-12f,1f,-20f,1039u,5,0,249082u,0f,1f,0f);return true;
                case 31: value=new LandingBox020(-12.4f,-3f,-32f,-12f,-1.75f,-30f,1070u,5,0,249973u,0f,1f,0f);return true;
                case 32: value=new LandingBox020(-12.4f,-2.5f,-30f,-12f,-1.25f,-28f,1071u,5,0,250101u,0f,1f,0f);return true;
                case 33: value=new LandingBox020(-12.4f,-2f,-28f,-12f,-0.75f,-26f,1072u,5,0,250485u,0f,1f,0f);return true;
                case 34: value=new LandingBox020(-12.4f,-1.5f,-26f,-12f,-0.25f,-24f,1073u,5,0,250613u,0f,1f,0f);return true;
                case 35: value=new LandingBox020(-12.4f,-1f,-24f,-12f,0.25f,-22f,1074u,5,0,252021u,0f,1f,0f);return true;
                case 36: value=new LandingBox020(-12.4f,-0.5f,-22f,-12f,0.75f,-20f,1075u,5,0,252149u,0f,1f,0f);return true;
                case 37: value=new LandingBox020(-32.5317f,0f,-13.27051f,-24.5317f,4f,-5.27051f,301u,4,0,254681u,0f,1f,0f);return true;
                case 38: value=new LandingBox020(-20f,-6f,-84.4f,22f,-5f,-84f,1018u,5,0,297146u,0f,1f,0f);return true;
                case 39: value=new LandingBox020(22f,-6f,-84f,22.4f,-5f,-80f,1017u,5,0,302016u,0f,1f,0f);return true;
                case 40: value=new LandingBox020(22f,-6f,-80.4f,32f,-5f,-80f,1022u,5,0,302842u,0f,1f,0f);return true;
                case 41: value=new LandingBox020(32f,-6f,-80f,32.4f,-5f,-74f,1020u,5,0,319648u,0f,1f,0f);return true;
                case 42: value=new LandingBox020(32f,-6f,-74.4f,40f,-5f,-74f,1026u,5,0,320298u,0f,1f,0f);return true;
                case 43: value=new LandingBox020(39f,-6f,-66f,40f,-5f,-65.6f,1027u,5,0,322516u,0f,1f,0f);return true;
                case 44: value=new LandingBox020(40f,-6f,-74f,40.4f,-5f,-66f,1024u,5,0,322688u,0f,1f,0f);return true;
                case 45: value=new LandingBox020(12f,-6f,-44f,12.4f,-4.75f,-42f,1076u,5,0,403232u,0f,1f,0f);return true;
                case 46: value=new LandingBox020(12f,-5.5f,-42f,12.4f,-4.25f,-40f,1077u,5,0,403360u,0f,1f,0f);return true;
                case 47: value=new LandingBox020(12f,-5f,-40f,12.4f,-3.75f,-38f,1078u,5,0,404768u,0f,1f,0f);return true;
                case 48: value=new LandingBox020(12f,-4.5f,-38f,12.4f,-3.25f,-36f,1079u,5,0,404896u,0f,1f,0f);return true;
                case 49: value=new LandingBox020(12f,-4f,-36f,12.4f,-2.75f,-34f,1080u,5,0,405280u,0f,1f,0f);return true;
                case 50: value=new LandingBox020(12f,-3.5f,-34f,12.4f,-2.25f,-32f,1081u,5,0,405408u,0f,1f,0f);return true;
                case 51: value=new LandingBox020(12f,-6f,-44f,32f,-5f,-43.6f,1036u,5,0,406336u,0f,1f,0f);return true;
                case 52: value=new LandingBox020(39f,-6f,-66f,39.4f,-5f,-58f,1001u,5,0,410064u,0f,1f,0f);return true;
                case 53: value=new LandingBox020(39f,-6f,-58.4f,40f,-5f,-58f,1030u,5,0,410494u,0f,1f,0f);return true;
                case 54: value=new LandingBox020(32f,-6f,-50f,40f,-5f,-49.6f,1032u,5,0,412544u,0f,1f,0f);return true;
                case 55: value=new LandingBox020(40f,-6f,-58f,40.4f,-5f,-50f,1029u,5,0,412800u,0f,1f,0f);return true;
                case 56: value=new LandingBox020(32f,-6f,-50f,32.4f,-5f,-44f,1034u,5,0,417824u,0f,1f,0f);return true;
                case 57: value=new LandingBox020(40f,0f,-34.4f,60f,1f,-34f,1043u,5,0,424554u,0f,1f,0f);return true;
                case 58: value=new LandingBox020(12f,-3f,-32f,12.4f,-1.75f,-30f,1082u,5,0,427296u,0f,1f,0f);return true;
                case 59: value=new LandingBox020(12f,-2.5f,-30f,12.4f,-1.25f,-28f,1083u,5,0,427424u,0f,1f,0f);return true;
                case 60: value=new LandingBox020(12f,-2f,-28f,12.4f,-0.75f,-26f,1084u,5,0,427808u,0f,1f,0f);return true;
                case 61: value=new LandingBox020(12f,-1.5f,-26f,12.4f,-0.25f,-24f,1085u,5,0,427936u,0f,1f,0f);return true;
                case 62: value=new LandingBox020(12f,-1f,-24f,12.4f,0.25f,-22f,1086u,5,0,429344u,0f,1f,0f);return true;
                case 63: value=new LandingBox020(12f,-0.5f,-22f,12.4f,0.75f,-20f,1087u,5,0,429472u,0f,1f,0f);return true;
                case 64: value=new LandingBox020(12f,0f,-20.4f,40f,1f,-20f,1040u,5,0,433386u,0f,1f,0f);return true;
                case 65: value=new LandingBox020(-4f,0f,-12f,4f,1.3f,-8f,305u,4,0,434816u,0f,1f,0f);return true;
                case 66: value=new LandingBox020(39.6f,0f,-34f,40f,1f,-20f,1041u,5,0,443253u,0f,1f,0f);return true;
                case 67: value=new LandingBox020(60f,0f,-34f,60.4f,1f,-24.6f,1042u,5,0,447880u,0f,1f,0f);return true;
                case 68: value=new LandingBox020(60f,0f,-14.6f,60.4f,1f,-12f,1038u,5,0,456072u,0f,1f,0f);return true;
                case 69: value=new LandingBox020(60f,0f,-12.4f,61f,1f,-12f,1014u,5,0,456110u,0f,1f,0f);return true;
                case 70: value=new LandingBox020(61f,0f,-12f,61.4f,1f,-4f,1012u,5,0,458000u,0f,1f,0f);return true;
                case 71: value=new LandingBox020(61f,0f,-4.4f,62f,1f,-4f,1050u,5,0,458174u,0f,1f,0f);return true;
                case 72: value=new LandingBox020(56.35018f,0f,-29.27741f,90.95018f,1f,-28.67741f,1088u,5,0,492724u,18f,0.9510565f,0.309017f);return true;
                case 73: value=new LandingBox020(59.31675f,0f,-20.14727f,93.91675f,1f,-19.54727f,1089u,5,0,495364u,18f,0.9510565f,0.309017f);return true;
                case 74: value=new LandingBox020(89.09931f,0f,-33.8476f,89.69931f,4f,-24.2476f,229u,3,2,496795u,18f,0.9510565f,0.309017f);return true;
                case 75: value=new LandingBox020(91.28674f,0f,-34.55834f,91.88674f,4f,-24.95834f,228u,2,0,496852u,18f,0.9510565f,0.309017f);return true;
                case 76: value=new LandingBox020(-91.88674f,0f,24.95834f,-91.28674f,4f,34.55834f,208u,2,0,551723u,18f,0.9510565f,0.309017f);return true;
                case 77: value=new LandingBox020(-89.69931f,0f,24.2476f,-89.09931f,4f,33.8476f,209u,3,1,551780u,18f,0.9510565f,0.309017f);return true;
                case 78: value=new LandingBox020(-93.91675f,0f,19.54727f,-59.31675f,1f,20.14727f,1090u,5,0,553211u,18f,0.9510565f,0.309017f);return true;
                case 79: value=new LandingBox020(-90.95018f,0f,28.67741f,-56.35018f,1f,29.27741f,1091u,5,0,555851u,18f,0.9510565f,0.309017f);return true;
                case 80: value=new LandingBox020(-60.4f,0f,-4f,-60f,1f,4f,1048u,5,0,589909u,0f,1f,0f);return true;
                case 81: value=new LandingBox020(-60.4f,0f,4f,-60f,1f,14f,1052u,5,0,591989u,0f,1f,0f);return true;
                case 82: value=new LandingBox020(-60.4f,0f,14f,-60f,1f,14.6f,1055u,5,0,592599u,0f,1f,0f);return true;
                case 83: value=new LandingBox020(-60.4f,0f,24.6f,-60f,1f,30f,1044u,5,0,600311u,0f,1f,0f);return true;
                case 84: value=new LandingBox020(-60f,0f,30f,-54f,1f,30.4f,1045u,5,0,601040u,0f,1f,0f);return true;
                case 85: value=new LandingBox020(-32f,0f,24f,-18f,13.3f,36f,302u,4,0,617424u,0f,1f,0f);return true;
                case 86: value=new LandingBox020(-10.6f,0.25f,20f,-10f,1.5f,21.5f,1092u,5,0,619294u,0f,1f,0f);return true;
                case 87: value=new LandingBox020(-10.6f,0.75f,21.5f,-10f,2f,23f,1093u,5,0,619414u,0f,1f,0f);return true;
                case 88: value=new LandingBox020(-10.6f,1.25f,23f,-10f,2.5f,24.5f,1094u,5,0,619454u,0f,1f,0f);return true;
                case 89: value=new LandingBox020(-10.6f,1.75f,24.5f,-10f,3f,26f,1095u,5,0,620854u,0f,1f,0f);return true;
                case 90: value=new LandingBox020(-10.6f,2.25f,26f,-10f,3.5f,27.5f,1096u,5,0,620958u,0f,1f,0f);return true;
                case 91: value=new LandingBox020(-10.6f,2.75f,27.5f,-10f,4f,29f,1097u,5,0,621334u,0f,1f,0f);return true;
                case 92: value=new LandingBox020(-10.6f,3.25f,29f,-10f,4.5f,30.5f,1098u,5,0,621374u,0f,1f,0f);return true;
                case 93: value=new LandingBox020(-10.6f,3.75f,30.5f,-10f,5f,32f,1099u,5,0,621494u,0f,1f,0f);return true;
                case 94: value=new LandingBox020(-54.4f,0f,30f,-54f,1f,38f,1046u,5,0,623765u,0f,1f,0f);return true;
                case 95: value=new LandingBox020(-54f,0f,38f,-10f,1f,38.4f,1047u,5,0,639616u,0f,1f,0f);return true;
                case 96: value=new LandingBox020(-10.6f,4.25f,32f,-10f,5.5f,33.5f,1100u,5,0,643358u,0f,1f,0f);return true;
                case 97: value=new LandingBox020(-10.6f,4.75f,33.5f,-10f,6f,35f,1101u,5,0,643478u,0f,1f,0f);return true;
                case 98: value=new LandingBox020(-10.6f,5.25f,35f,-10f,6.5f,36.5f,1102u,5,0,643518u,0f,1f,0f);return true;
                case 99: value=new LandingBox020(-10.6f,5.75f,36.5f,-10f,7f,38f,1103u,5,0,643894u,0f,1f,0f);return true;
                case 100: value=new LandingBox020(-10.4f,6f,38f,-10f,7f,54f,1059u,5,0,646037u,0f,1f,0f);return true;
                case 101: value=new LandingBox020(-7.5f,6f,49.1f,-6.1f,14f,50.9f,306u,4,0,652432u,0f,1f,0f);return true;
                case 102: value=new LandingBox020(10f,0.25f,20f,10.6f,1.5f,21.5f,1104u,5,0,796235u,0f,1f,0f);return true;
                case 103: value=new LandingBox020(10f,0.75f,21.5f,10.6f,2f,23f,1105u,5,0,796355u,0f,1f,0f);return true;
                case 104: value=new LandingBox020(10f,1.25f,23f,10.6f,2.5f,24.5f,1106u,5,0,796395u,0f,1f,0f);return true;
                case 105: value=new LandingBox020(10f,1.75f,24.5f,10.6f,3f,26f,1107u,5,0,797795u,0f,1f,0f);return true;
                case 106: value=new LandingBox020(10f,2.25f,26f,10.6f,3.5f,27.5f,1108u,5,0,797899u,0f,1f,0f);return true;
                case 107: value=new LandingBox020(10f,2.75f,27.5f,10.6f,4f,29f,1109u,5,0,798275u,0f,1f,0f);return true;
                case 108: value=new LandingBox020(10f,3.25f,29f,10.6f,4.5f,30.5f,1110u,5,0,798315u,0f,1f,0f);return true;
                case 109: value=new LandingBox020(10f,3.75f,30.5f,10.6f,5f,32f,1111u,5,0,798435u,0f,1f,0f);return true;
                case 110: value=new LandingBox020(19f,0f,16f,31f,14.2f,28f,304u,4,0,800400u,0f,1f,0f);return true;
                case 111: value=new LandingBox020(62f,0f,-4f,62.4f,1f,4f,1049u,5,0,808256u,0f,1f,0f);return true;
                case 112: value=new LandingBox020(60f,0f,4f,62f,1f,4.4f,1051u,5,0,808720u,0f,1f,0f);return true;
                case 113: value=new LandingBox020(60f,0f,4f,60.4f,1f,14f,1053u,5,0,810272u,0f,1f,0f);return true;
                case 114: value=new LandingBox020(59f,0f,14f,60f,1f,14.4f,1054u,5,0,810708u,0f,1f,0f);return true;
                case 115: value=new LandingBox020(59f,0f,14f,59.4f,1f,20f,1056u,5,0,816240u,0f,1f,0f);return true;
                case 116: value=new LandingBox020(59f,0f,20f,59.4f,1f,30f,1057u,5,0,818288u,0f,1f,0f);return true;
                case 117: value=new LandingBox020(56f,0f,30f,59f,1f,30.4f,1058u,5,0,818836u,0f,1f,0f);return true;
                case 118: value=new LandingBox020(10f,4.25f,32f,10.6f,5.5f,33.5f,1112u,5,0,820299u,0f,1f,0f);return true;
                case 119: value=new LandingBox020(10f,4.75f,33.5f,10.6f,6f,35f,1113u,5,0,820419u,0f,1f,0f);return true;
                case 120: value=new LandingBox020(10f,5.25f,35f,10.6f,6.5f,36.5f,1114u,5,0,820459u,0f,1f,0f);return true;
                case 121: value=new LandingBox020(10f,5.75f,36.5f,10.6f,7f,38f,1115u,5,0,820835u,0f,1f,0f);return true;
                case 122: value=new LandingBox020(10f,6f,38f,10.4f,7f,54f,1060u,5,0,822976u,0f,1f,0f);return true;
                case 123: value=new LandingBox020(6.1f,6f,49.1f,7.5f,14f,50.9f,307u,4,0,827845u,0f,1f,0f);return true;
                case 124: value=new LandingBox020(-10f,6f,54f,10f,7f,54.4f,1061u,5,0,828032u,0f,1f,0f);return true;
                case 125: value=new LandingBox020(10f,0f,38f,56f,1f,38.4f,1063u,5,0,836240u,0f,1f,0f);return true;
                case 126: value=new LandingBox020(56f,0f,30f,56.4f,1f,38f,1062u,5,0,840832u,0f,1f,0f);return true;
                default:value=default;return false;
            }
        }
        public static bool TryAnchor(int index,out LandingPoint020 feet,out float yaw)
        {
            switch(index)
            {
                case 0:feet=new LandingPoint020(0f,6.05f,47f);yaw=180f;return true;
                case 1:feet=new LandingPoint020(0f,0.05f,4f);yaw=0f;return true;
                case 2:feet=new LandingPoint020(-21.5317f,0.05f,-9.27051f);yaw=270f;return true;
                case 3:feet=new LandingPoint020(-25f,0.05f,20f);yaw=0f;return true;
                case 4:feet=new LandingPoint020(-34f,0.05f,-18f);yaw=270f;return true;
                case 5:feet=new LandingPoint020(15f,0.05f,22f);yaw=90f;return true;
                case 6:feet=new LandingPoint020(0f,0.05f,-4f);yaw=180f;return true;
                case 7:feet=new LandingPoint020(-86.54614f,0.05f,28.12055f);yaw=288f;return true;
                case 8:feet=new LandingPoint020(86.54614f,0.05f,-28.12055f);yaw=108f;return true;
                case 9:feet=new LandingPoint020(0f,0.05f,19f);yaw=0f;return true;
                case 10:feet=new LandingPoint020(0f,6.05f,40f);yaw=180f;return true;
                default:feet=default;yaw=0;return false;
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Two organic world-metre/Y-up islands: townY0 with preserved bridge endpoints, portalY6, substantial ruinsY-6 reached by24 one-metre treads.
// Array Formats:69floors and137Morton-sorted obstacles: unchanged127core/eight-fixture135base slots plus two collision-only portal columns. Contour bands and kind5 rail decomposition retain their original owners.
// Dependencies/Pointers: Work-only guarded candidate; root must bind matching guard rendering, table/tree relocation, bounds and edge recovery atomically. No story/unlock or existing mode authority changes.
