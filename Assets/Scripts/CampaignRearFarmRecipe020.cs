// PSEUDOCODE: validate the unchanged supported key10 apron and receiver capacity;
// reconstruct98 fixed commands in world-Morton order; reject any finite/support/obstacle failure before emitting;
// dress two thin soil beds while keeping existing pavers visible in both walking lanes.
using Unity.Mathematics;
namespace OdysseyOracle
{
    public struct CampaignRearFarmCommand020
    {
        public float3 Minimum,Maximum;
        public float4 Color;
        public uint Morton;
        public int StableId;
        public byte Kind;
    }
    public static class CampaignRearFarmRecipe020
    {
        public const int Revision=1,CommandCount=98,MaximumCommands=100,FloorSlot=1;
        public const uint FloorKey=10u;
        public const float SoilTop=.024f,FurrowTop=.028f,MaximumLowDressingTop=.028f,MaximumDecorationTop=.403f;
        public const float CenterPathWidth=1.625f,HousePathWidth=1.21875f;
        static readonly float4 Soil=new float4(.30f,.145f,.052f,1f),DarkSoil=new float4(.255f,.112f,.035f,1f),LightSoil=new float4(.34f,.17f,.063f,1f);
        static float4 Stone(int variant)=>variant==0?new float4(.39f,.365f,.30f,1f):new float4(.335f,.31f,.255f,1f);
        static float4 Leaf(int variant)=>variant==0?new float4(.16f,.32f,.075f,1f):variant==1?new float4(.245f,.43f,.10f,1f):new float4(.31f,.49f,.135f,1f);
        static float4 Petal(int variant)=>variant==0?new float4(.67f,.30f,.08f,1f):variant==1?new float4(.72f,.55f,.24f,1f):variant==2?new float4(.49f,.065f,.09f,1f):new float4(.72f,.37f,.15f,1f);
        static float BedMinimum(int bed)=>bed==0?-55.375f:-44.1875f;
        public static bool TryBed(int bed,out float3 minimum,out float3 maximum)
        {
            minimum=maximum=default;if(bed<0||bed>=2)return false;
            float x=BedMinimum(bed);minimum=new float3(x,0f,-28.5f);maximum=new float3(x+9.5625f,MaximumDecorationTop,-26.125f);return true;
        }
        public static bool TryLane(int lane,out float4 rectangle)
        {
            rectangle=default;
            if(lane==0){rectangle=new float4(-45.8125f,-29.25f,-44.1875f,-24.5f);return true;}
            if(lane==1){rectangle=new float4(-56.25f,-25.71875f,-33.5f,-24.5f);return true;}
            return false;
        }
        static int Address(int ordinal)
        {
            switch(ordinal)
            {
                case 0:return 10;
                case 1:return 38;
                case 2:return 39;
                case 3:return 11;
                case 4:return 2;
                case 5:return 40;
                case 6:return 41;
                case 7:return 12;
                case 8:return 18;
                case 9:return 86;
                case 10:return 87;
                case 11:return 88;
                case 12:return 30;
                case 13:return 14;
                case 14:return 44;
                case 15:return 45;
                case 16:return 50;
                case 17:return 51;
                case 18:return 56;
                case 19:return 57;
                case 20:return 31;
                case 21:return 3;
                case 22:return 46;
                case 23:return 47;
                case 24:return 0;
                case 25:return 4;
                case 26:return 52;
                case 27:return 53;
                case 28:return 15;
                case 29:return 5;
                case 30:return 58;
                case 31:return 59;
                case 32:return 32;
                case 33:return 16;
                case 34:return 13;
                case 35:return 42;
                case 36:return 43;
                case 37:return 20;
                case 38:return 62;
                case 39:return 63;
                case 40:return 21;
                case 41:return 33;
                case 42:return 48;
                case 43:return 49;
                case 44:return 54;
                case 45:return 55;
                case 46:return 60;
                case 47:return 61;
                case 48:return 17;
                case 49:return 89;
                case 50:return 90;
                case 51:return 91;
                case 52:return 19;
                case 53:return 28;
                case 54:return 92;
                case 55:return 93;
                case 56:return 94;
                case 57:return 34;
                case 58:return 68;
                case 59:return 69;
                case 60:return 74;
                case 61:return 75;
                case 62:return 24;
                case 63:return 80;
                case 64:return 81;
                case 65:return 35;
                case 66:return 25;
                case 67:return 6;
                case 68:return 64;
                case 69:return 65;
                case 70:return 22;
                case 71:return 66;
                case 72:return 67;
                case 73:return 23;
                case 74:return 7;
                case 75:return 70;
                case 76:return 71;
                case 77:return 1;
                case 78:return 8;
                case 79:return 76;
                case 80:return 77;
                case 81:return 9;
                case 82:return 82;
                case 83:return 83;
                case 84:return 36;
                case 85:return 26;
                case 86:return 72;
                case 87:return 73;
                case 88:return 78;
                case 89:return 79;
                case 90:return 84;
                case 91:return 85;
                case 92:return 37;
                case 93:return 29;
                case 94:return 95;
                case 95:return 96;
                case 96:return 97;
                case 97:return 27;
                default:return -1;
            }
        }
        static bool Bounds(int id,byte kind,float3 minimum,float3 maximum,float4 color,out CampaignRearFarmCommand020 value)
        {
            value=default;
            if(!math.all(math.isfinite(minimum))||!math.all(math.isfinite(maximum))||math.any(maximum<=minimum)||
                !math.all(math.isfinite(color))||math.any(color<0f)||math.any(color>1f)||color.w!=1f||
                !PortalLandingLayout020.TryMorton((minimum.x+maximum.x)*.5f,(minimum.z+maximum.z)*.5f,out uint morton))return false;
            value=new CampaignRearFarmCommand020{Minimum=minimum,Maximum=maximum,Color=color,Morton=morton,StableId=id,Kind=kind};return true;
        }
        static bool Center(int id,byte kind,float3 center,float3 size,float4 color,out CampaignRearFarmCommand020 value)=>
            Bounds(id,kind,center-size*.5f,center+size*.5f,color,out value);
        public static bool TryCommand(int ordinal,out CampaignRearFarmCommand020 value)
        {
            value=default;int id=Address(ordinal);if(id<0)return false;
            int bed,part;float ax,bx,z;
            if(id<2)
            {
                ax=BedMinimum(id);bx=ax+9.5625f;
                return Bounds(id,0,new float3(ax+.125f,.004f,-28.375f),new float3(bx-.125f,SoilTop,-26.25f),Soil,out value);
            }
            if(id<10)
            {
                int n=id-2;bed=n/4;part=n%4;ax=BedMinimum(bed);bx=ax+9.5625f;z=-28.1875f+part*.5625f;
                return Bounds(id,1,new float3(ax+.25f,SoilTop,z-.15625f),new float3(bx-.25f,FurrowTop,z+.15625f),(part&1)==0?DarkSoil:LightSoil,out value);
            }
            if(id<30)
            {
                int n=id-10;bed=n/10;part=n%10;ax=BedMinimum(bed);bx=ax+9.5625f;
                if(part<8)
                {
                    float x=ax+.125f+(part%4)*2.34375f;z=part<4?-28.5f:-26.25f;
                    return Bounds(id,2,new float3(x,.001f,z),new float3(x+2.28125f,MaximumLowDressingTop,z+.125f),Stone((part+bed)&1),out value);
                }
                float edge=part==8?ax:bx-.125f;
                return Bounds(id,2,new float3(edge,.001f,-28.5f),new float3(edge+.125f,MaximumLowDressingTop,-26.125f),Stone((part+bed)&1),out value);
            }
            if(id<38)
            {
                int n=id-30;bed=n/4;part=n%4;float x=BedMinimum(bed)+4.78125f+(part-1.5f)*2.375f;
                z=-27.90625f+(part%3)*.5625f;float width=.25f+(part&1)*.0625f;
                return Bounds(id,3,new float3(x-width*.5f,SoilTop,z-.078125f),new float3(x+width*.5f,.027f,z+.078125f),
                    new float4(.215f+part*.035f,.095f+part*.014f,.030f+part*.006f,1f),out value);
            }
            if(id<86)
            {
                int n=id-38;bed=n/24;int local=n%24,plant=local/2;part=local%2;
                float x=BedMinimum(bed)+4.78125f+((plant%3)-1)*3.09375f;z=-28.1875f+(plant/3)*.5625f;int variant=(plant+bed*5)%3;
                float3 size=part==0?new float3(1.0625f+variant*.0625f,.25f,.21875f):new float3(.25f,.3125f+variant*.03125f,.5f);
                return Center(id,4,new float3(x,FurrowTop+size.y*.5f,z),size,Leaf((variant+part)%3),out value);
            }
            {
                int n=id-86;bed=n/6;int local=n%6,plant=local/3;part=local%3;ax=BedMinimum(bed);bx=ax+9.5625f;
                float x=plant==0?ax+.65625f:bx-.65625f;z=plant==0?-27.90625f:-26.78125f;
                float3 size=part==0?new float3(.03125f,.28125f,.03125f):part==1?new float3(.3125f,.09375f,.09375f):new float3(.09375f,.09375f,.3125f);
                float y=SoilTop+(part==0?.140625f:.296875f);
                return Center(id,5,new float3(x,y,z),size,part==0?new float4(.13f,.27f,.07f,1f):Petal(bed*2+plant),out value);
            }
        }
        static bool OverlapsObstacle(in CampaignRearFarmCommand020 command,in LandingBox020 obstacle)
        {
            if(obstacle.MaxY<=command.Minimum.y||obstacle.MinY>=command.Maximum.y)return false;
            float hx=(obstacle.MaxX-obstacle.MinX)*.5f,hz=(obstacle.MaxZ-obstacle.MinZ)*.5f;
            float ex=math.abs(obstacle.Cos)*hx+math.abs(obstacle.Sin)*hz,ez=math.abs(obstacle.Sin)*hx+math.abs(obstacle.Cos)*hz;
            float x=(obstacle.MinX+obstacle.MaxX)*.5f,z=(obstacle.MinZ+obstacle.MaxZ)*.5f;
            return command.Minimum.x<x+ex&&command.Maximum.x>x-ex&&command.Minimum.z<z+ez&&command.Maximum.z>z-ez;
        }
        public static bool ValidateCurrentSupport()
        {
            if(PortalLandingLayout020.SurfaceCount!=69||PortalLandingLayout020.ObstacleCount!=162||
                !PortalLandingLayout020.TrySurface(FloorSlot,out var floor)||floor.Key!=FloorKey||floor.Kind!=1||
                floor.MinX!=-58f||floor.MaxX!=-32f||floor.MinZ!=-30f||floor.MaxZ!=-24f||floor.MaxY!=0f||floor.Yaw!=0f)return false;
            uint previous=0;int previousId=-1;
            for(int ordinal=0;ordinal<CommandCount;ordinal++)
            {
                if(!TryCommand(ordinal,out var command)||command.Minimum.x<floor.MinX||command.Maximum.x>floor.MaxX||
                    command.Minimum.z<floor.MinZ||command.Maximum.z>floor.MaxZ||command.Minimum.y<=floor.MaxY||
                    command.Maximum.y>MaximumDecorationTop+.00001f||command.Kind<4&&command.Maximum.y>.030001f)return false;
                if(ordinal>0&&(command.Morton<previous||command.Morton==previous&&command.StableId<=previousId))return false;
                previous=command.Morton;previousId=command.StableId;
                for(int obstacle=0;obstacle<PortalLandingLayout020.ObstacleCount;obstacle++)
                    if(!PortalLandingLayout020.TryObstacle(obstacle,out var box)||OverlapsObstacle(command,box))return false;
            }
            return true;
        }
        public static bool Emit<T>(ref T sink,int remainingCommands,out int emittedCommands) where T:struct,ICampaignBuildingSink020
        {
            emittedCommands=0;
            if(remainingCommands<CommandCount||!ValidateCurrentSupport())return false;
            for(int ordinal=0;ordinal<CommandCount;ordinal++)
            {
                TryCommand(ordinal,out var command);
                sink.Box((command.Minimum+command.Maximum)*.5f,command.Maximum-command.Minimum,quaternion.identity,command.Color,0f);
                emittedCommands++;
            }
            return emittedCommands==CommandCount;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing key10 west-house south apron/Y0, two inset beds; cap tops.024m and low dressing<=.028m leave actual paver paths visible.
// Array Formats:98 fixed world-Morton command addresses reconstruct bounded blittable box records; existing shared3072-command writer/mesh and69floor/162obstacle streams unchanged.
// Dependencies/Pointers: Cold pure numeric helper runs inside the existing recipe IJob; receiver/World owns buffers, one combined mesh/material, support, culling and cleanup. No frame work, collider, asset-mask, clock, dynamic garden anchor or native/visual acceptance is added.
