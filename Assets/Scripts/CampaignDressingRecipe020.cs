using Unity.Mathematics;

namespace OdysseyOracle
{
    public enum CampaignDressingKind020 : byte { Barrel, Banner, Table, Chair, Cup, Planter, StoneTrim, BridgeUnderside, BrokenPillar, RuinArch }

    public struct CampaignDressingPlacement020
    {
        public CampaignDressingKind020 Kind;
        public float3 Origin, GuardOrigin, GuardMinimum, GuardMaximum;
        public float Yaw, GuardYaw;
        public uint Seed;
        // Guards are conservative original closed-footprint/plinth/table/underside envelopes, not floor or collision authority.
    }

    public static class CampaignDressingRecipe020
    {
        public const int RoleCount=10,PrototypeCount=16,MaximumRoleCommands=80,MaximumPartyCommands=85;
        public const float TableTopHeight=.88f;
        static readonly float4 Stone=new float4(.31f,.30f,.29f,1),Pale=new float4(.43f,.41f,.37f,1),Dark=new float4(.075f,.075f,.07f,1);
        static readonly float4 Wood=new float4(.26f,.13f,.065f,1),Timber=new float4(.16f,.078f,.035f,1),Iron=new float4(.12f,.12f,.13f,1);
        static readonly float4 Bronze=new float4(.58f,.34f,.10f,1),Moss=new float4(.14f,.21f,.10f,1),Leaf=new float4(.13f,.25f,.12f,1),Red=new float4(.48f,.055f,.075f,1);

        public static int CommandCount(CampaignDressingKind020 kind)
        {
            switch(kind)
            {
                case CampaignDressingKind020.Barrel:return 24;
                case CampaignDressingKind020.Banner:return 13;
                case CampaignDressingKind020.Table:return 9;
                case CampaignDressingKind020.Chair:return 10;
                case CampaignDressingKind020.Cup:return 9;
                case CampaignDressingKind020.Planter:return 15;
                case CampaignDressingKind020.StoneTrim:return 10;
                case CampaignDressingKind020.BridgeUnderside:return 80;
                case CampaignDressingKind020.BrokenPillar:return 12;
                case CampaignDressingKind020.RuinArch:return 23;
                default:return 0;
            }
        }
        static bool Valid(float3 origin,float yaw,float limit=1024f)=>math.all(math.isfinite(origin))&&math.cmax(math.abs(origin))<=limit&&math.isfinite(yaw)&&math.abs(yaw)<=36000f;
        static quaternion Turn(float yaw)=>quaternion.RotateY(math.radians(yaw%360f));
        static uint Mix(uint value){unchecked{value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;return value^(value>>16);}}
        static void Put<T>(ref T sink,float3 origin,quaternion rotation,float3 local,float3 size,float4 color,float roll=0) where T:struct,ICampaignBuildingSink020
        {sink.Box(origin+math.rotate(rotation,local),size,math.mul(rotation,quaternion.RotateZ(math.radians(roll))),color,0f);}

        public static bool Emit<T>(ref T sink,CampaignDressingKind020 kind,float3 origin,float yaw,uint seed) where T:struct,ICampaignBuildingSink020
        {
            if(CommandCount(kind)==0||!Valid(origin,yaw))return false;
            var rotation=Turn(yaw);uint detail=Mix(seed);float shift=((detail&255u)/255f-.5f)*.10f;
            switch(kind)
            {
                case CampaignDressingKind020.Barrel:
                    Put(ref sink,origin,rotation,new float3(0,.55f,0),new float3(.75f,1.1f,.75f),Timber);
                    for(int side=-1;side<=1;side+=2)for(int strip=0;strip<3;strip++)
                    {
                        float p=(strip-1)*.25f;float4 color=strip==1?Wood:Timber;
                        Put(ref sink,origin,rotation,new float3(p,.55f,side*.38f),new float3(.24f,1.04f,.08f),color);
                        Put(ref sink,origin,rotation,new float3(side*.38f,.55f,p),new float3(.08f,1.04f,.24f),color);
                    }
                    for(int band=0;band<2;band++)for(int side=-1;side<=1;side+=2)
                    {
                        float y=band==0?.23f:.86f;
                        Put(ref sink,origin,rotation,new float3(0,y,side*.415f),new float3(.86f,.10f,.045f),Iron);
                        Put(ref sink,origin,rotation,new float3(side*.415f,y,0),new float3(.045f,.10f,.86f),Iron);
                    }
                    Put(ref sink,origin,rotation,new float3(0,1.1f,0),new float3(.84f,.04f,.84f),Wood);
                    for(int seam=0;seam<2;seam++)Put(ref sink,origin,rotation,new float3((seam-.5f)*.28f,1.123f,0),new float3(.012f,.008f,.75f),Timber);
                    break;
                case CampaignDressingKind020.Banner:
                    var cloth=(seed&1u)==0?new float4(.045f,.24f,.24f,1):new float4(.39f,.045f,.09f,1);
                    Put(ref sink,origin,rotation,new float3(0,1.5f,.1f),new float3(.10f,3,.10f),Timber);
                    Put(ref sink,origin,rotation,new float3(0,2.55f,.1f),new float3(1.15f,.10f,.13f),Wood);
                    Put(ref sink,origin,rotation,new float3(0,1.52f,.025f),new float3(.92f,2,.04f),cloth);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Put(ref sink,origin,rotation,new float3(side*.28f,.42f,.025f),new float3(.36f,.24f,.04f),cloth);
                        Put(ref sink,origin,rotation,new float3(side*.43f,1.52f,0),new float3(.035f,1.9f,.025f),Bronze);
                        Put(ref sink,origin,rotation,new float3(0,1.52f+side*.95f,0),new float3(.88f,.035f,.025f),Bronze);
                    }
                    Put(ref sink,origin,rotation,new float3(0,1.65f,-.004f),new float3(.08f,.72f,.02f),Bronze);
                    Put(ref sink,origin,rotation,new float3(0,1.70f,-.004f),new float3(.36f,.08f,.02f),Bronze);
                    for(int mount=0;mount<2;mount++)Put(ref sink,origin,rotation,new float3(0,1.05f+mount*1.5f,.14f),new float3(.30f,.12f,.28f),Iron);
                    break;
                case CampaignDressingKind020.Table:
                    Put(ref sink,origin,rotation,new float3(0,.82f,0),new float3(2,.12f,1.1f),Wood);
                    for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Put(ref sink,origin,rotation,new float3(x*.79f,.381f,z*.36f),new float3(.14f,.762f,.14f),Timber);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Put(ref sink,origin,rotation,new float3(0,.56f,side*.36f),new float3(1.72f,.13f,.10f),Timber);
                        Put(ref sink,origin,rotation,new float3(side*.79f,.56f,0),new float3(.10f,.13f,.85f),Timber);
                    }
                    break;
                case CampaignDressingKind020.Chair:
                    Put(ref sink,origin,rotation,new float3(0,.48f,0),new float3(.62f,.10f,.62f),Wood);
                    for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Put(ref sink,origin,rotation,new float3(x*.25f,.216f,z*.25f),new float3(.10f,.432f,.10f),Timber);
                    for(int side=-1;side<=1;side+=2)Put(ref sink,origin,rotation,new float3(side*.25f,.82f,.25f),new float3(.10f,.70f,.10f),Timber);
                    for(int rail=0;rail<3;rail++)Put(ref sink,origin,rotation,new float3(0,.71f+rail*.19f,.25f),new float3(.60f,.11f,.08f),Wood);
                    break;
                case CampaignDressingKind020.Cup:
                    Put(ref sink,origin,rotation,new float3(0,.0125f,0),new float3(.18f,.025f,.18f),Bronze);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Put(ref sink,origin,rotation,new float3(side*.0775f,.1025f,0),new float3(.025f,.18f,.18f),Bronze);
                        Put(ref sink,origin,rotation,new float3(0,.1025f,side*.0775f),new float3(.13f,.18f,.025f),Bronze);
                    }
                    Put(ref sink,origin,rotation,new float3(0,.171f,0),new float3(.13f,.008f,.13f),Dark);
                    Put(ref sink,origin,rotation,new float3(.145f,.11f,0),new float3(.025f,.11f,.045f),Bronze);
                    for(int side=-1;side<=1;side+=2)Put(ref sink,origin,rotation,new float3(.115f,.11f+side*.055f,0),new float3(.085f,.025f,.045f),Bronze);
                    break;
                case CampaignDressingKind020.Planter:
                    Put(ref sink,origin,rotation,new float3(0,.23f,0),new float3(1,.46f,.60f),Stone);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Put(ref sink,origin,rotation,new float3(side*.46f,.47f,0),new float3(.08f,.10f,.60f),Pale);
                        Put(ref sink,origin,rotation,new float3(0,.47f,side*.26f),new float3(.84f,.10f,.08f),Pale);
                    }
                    Put(ref sink,origin,rotation,new float3(0,.455f,0),new float3(.82f,.04f,.42f),Timber);
                    for(int plant=0;plant<3;plant++)
                    {
                        float x=(plant-1)*.28f;
                        Put(ref sink,origin,rotation,new float3(x,.69f,0),new float3(.045f,.44f,.045f),Moss);
                        Put(ref sink,origin,rotation,new float3(x,.73f,.02f),new float3(.28f,.18f,.34f),Leaf);
                        Put(ref sink,origin,rotation,new float3(x+shift,.92f,0),new float3(.15f,.15f,.15f),Red);
                    }
                    break;
                case CampaignDressingKind020.StoneTrim:
                    Put(ref sink,origin,rotation,new float3(0,.20f,0),new float3(1.2f,.40f,.30f),Stone);
                    Put(ref sink,origin,rotation,new float3(0,.39f,0),new float3(1.24f,.08f,.33f),Pale);
                    for(int crack=0;crack<3;crack++)Put(ref sink,origin,rotation,new float3((crack-1)*.23f,.18f,-.151f),new float3(.025f,.24f,.004f),Dark,(crack-1)*21f);
                    for(int moss=0;moss<5;moss++)Put(ref sink,origin,rotation,new float3((moss-2)*.21f,.31f+(moss&1)*.04f,-.165f),new float3(.16f,.06f,.035f),Moss);
                    break;
                case CampaignDressingKind020.BridgeUnderside:
                    // Five bearings, four open stepped arches; unchanged34x9m deck is a separate owner.
                    for(int pier=0;pier<5;pier++)
                    {
                        float x=(pier-2)*7.8f;
                        Put(ref sink,origin,rotation,new float3(x,-1.075f,0),new float3(2.6f,.55f,8.6f),Pale);
                        Put(ref sink,origin,rotation,new float3(x,-3.95f,0),new float3(1.8f,5.20f,7.8f),Stone);
                        for(int band=0;band<2;band++)
                            Put(ref sink,origin,rotation,new float3(x,-2.2f-band*2.8f,0),new float3(2.05f,.30f,8.1f),Pale);
                        Put(ref sink,origin,rotation,new float3(x,-7.075f,0),new float3(1.5f,1.05f,6.6f),Stone);
                        Put(ref sink,origin,rotation,new float3(x,-8.0f,0),new float3(.95f,.80f,4.8f),Stone);
                        for(int side=-1;side<=1;side+=2)
                        {
                            float length=1.25f+((detail>>(pier*3))&3u)*.15f;
                            Put(ref sink,origin,rotation,new float3(x+side*.36f,-3.6f,side*3.94f),new float3(.22f,length,.08f),Moss);
                        }
                    }
                    for(int bay=0;bay<4;bay++)
                    {
                        float x=(bay-1.5f)*7.8f;
                        for(int step=0;step<8;step++)
                        {
                            int rank=math.min(step,7-step);float depth=3.6f-rank*.8f;
                            Put(ref sink,origin,rotation,new float3(x+(step-3.5f)*.8f,-.8f-depth*.5f,0),new float3(.80f,depth,8.0f),rank==3?Pale:Stone);
                        }
                        for(int side=-1;side<=1;side+=2)
                            Put(ref sink,origin,rotation,new float3(x,-1.65f,side*4.065f),new float3(1.1f,.70f,.13f),Pale);
                    }
                    break;
                case CampaignDressingKind020.BrokenPillar:
                    Put(ref sink,origin,rotation,new float3(0,.10f,0),new float3(.90f,.20f,.90f),Stone);
                    Put(ref sink,origin,rotation,new float3(0,.28f,0),new float3(.76f,.16f,.76f),Pale);
                    for(int course=0;course<4;course++)Put(ref sink,origin,rotation,new float3(0,.49f+course*.26f,0),new float3(.56f-course*.025f,.26f,.56f-course*.025f),course%2==0?Stone:Pale);
                    for(int fracture=0;fracture<3;fracture++)
                    {
                        float height=.13f+fracture*.11f;
                        Put(ref sink,origin,rotation,new float3((fracture-1)*.18f,1.4f+height*.5f,shift),new float3(.18f,height,.43f),Stone);
                        Put(ref sink,origin,rotation,new float3((fracture-1)*.19f,.42f,-.289f),new float3(.14f,.13f,.035f),Moss);
                    }
                    break;
                case CampaignDressingKind020.RuinArch:
                    for(int side=-1;side<=1;side+=2)
                    {
                        Put(ref sink,origin,rotation,new float3(side*2.3f,.10f,0),new float3(1.2f,.20f,.70f),Stone);
                        for(int course=0;course<4;course++)Put(ref sink,origin,rotation,new float3(side*2.3f,.25f+course*.42f,0),new float3(.70f,.42f,.60f),course%2==0?Pale:Stone);
                        for(int step=0;step<3;step++)Put(ref sink,origin,rotation,new float3(side*(2.05f-step*.40f),1.83f+step*.23f,0),new float3(1,.24f,.60f),Stone);
                        Put(ref sink,origin,rotation,new float3(side*.65f,2.519f,0),new float3(.95f,.222f,.58f),Pale);
                        Put(ref sink,origin,rotation,new float3(side*2.3f,1.20f,-.303f),new float3(.028f,.30f,.004f),Dark,side*18);
                    }
                    for(int moss=0;moss<3;moss++)Put(ref sink,origin,rotation,new float3(-2.3f+(moss&1)*.22f,.5f+moss*.24f,-.325f),new float3(.18f,.10f,.035f),Moss);
                    break;
            }
            return true;
        }

        public static bool EmitPartySet<T>(ref T sink,float3 origin,float yaw,uint seed) where T:struct,ICampaignBuildingSink020
        {
            if(!Valid(origin,yaw,1000f))return false;yaw%=360f;var q=Turn(yaw);
            Emit(ref sink,CampaignDressingKind020.Table,origin,yaw,seed);
            for(int side=-1;side<=1;side+=2)
            {
                Emit(ref sink,CampaignDressingKind020.Chair,origin+math.rotate(q,new float3(side*1.4f,0,0)),yaw+side*90,seed);
                Emit(ref sink,CampaignDressingKind020.Chair,origin+math.rotate(q,new float3(0,0,side*1.05f)),yaw+(side<0?180:0),seed);
                for(int row=-1;row<=1;row+=2)Emit(ref sink,CampaignDressingKind020.Cup,origin+math.rotate(q,new float3(side*.45f,TableTopHeight,row*.25f)),yaw,seed);
            }
            return true;
        }

        static CampaignDressingPlacement020 Place(CampaignDressingKind020 kind,float3 origin,float yaw,uint seed,float3 guardOrigin,float guardYaw,float3 minimum,float3 maximum)=>
            new CampaignDressingPlacement020{Kind=kind,Origin=origin,Yaw=yaw,Seed=seed,GuardOrigin=guardOrigin,GuardYaw=guardYaw,GuardMinimum=minimum,GuardMaximum=maximum};
        public static bool TryPrototype(int index,out CampaignDressingPlacement020 value)
        {
            value=default;float3 house=new float3(-25,0,30),smith=new float3(-45,0,-18),shrine=new float3(-30*.9510565f,0,-30*.309017f);
            float3 houseMin=new float3(-7,0,-6),houseMax=new float3(7,13.3f,6),plinthMin=new float3(-3.5f,0,-3.5f),plinthMax=new float3(3.5f,4,3.5f);
            switch(index)
            {
                case 0:value=Place(CampaignDressingKind020.Barrel,new float3(-28,0,24.45f),0,11,house,0,houseMin,houseMax);return true;
                case 1:value=Place(CampaignDressingKind020.Barrel,new float3(-38.45f,0,-21),-90,29,smith,0,houseMin,houseMax);return true;
                case 2:value=Place(CampaignDressingKind020.Banner,new float3(-29.7f,4.2f,24.78f),0,0,house,0,houseMin,houseMax);return true;
                case 3:value=Place(CampaignDressingKind020.Banner,new float3(-38.78f,4.2f,-14.5f),-90,1,smith,0,houseMin,houseMax);return true;
                case 4:value=Place(CampaignDressingKind020.Planter,new float3(-22,0,24.4f),0,5,house,0,houseMin,houseMax);return true;
                case 5:value=Place(CampaignDressingKind020.Planter,new float3(-38.4f,0,-15),-90,13,smith,0,houseMin,houseMax);return true;
                case 6:value=Place(CampaignDressingKind020.StoneTrim,new float3(-30.6f,0,24.75f),0,17,house,0,houseMin,houseMax);return true;
                case 7:value=Place(CampaignDressingKind020.StoneTrim,new float3(-38.75f,0,-22.7f),-90,19,smith,0,houseMin,houseMax);return true;
                case 8:value=Place(CampaignDressingKind020.BrokenPillar,shrine+new float3(-2.8f,0,-2.8f),0,31,shrine,0,plinthMin,plinthMax);return true;
                case 9:value=Place(CampaignDressingKind020.BrokenPillar,shrine+new float3(2.8f,0,-2.8f),0,37,shrine,0,plinthMin,plinthMax);return true;
                case 10:value=Place(CampaignDressingKind020.RuinArch,shrine+new float3(0,0,2.8f),0,41,shrine,0,plinthMin,plinthMax);return true;
                case 11:case 12:case 13:value=Place(CampaignDressingKind020.Cup,new float3((index-12)*2,1.32f,-10),0,(uint)index,new float3(0,0,-10),0,new float3(-4,1.32f,-2),new float3(4,2,2));return true;
                case 14:case 15:
                    float sign=index==14?-1:1;float3 bridge=new float3(sign*79*.9510565f,0,-sign*79*.309017f);
                    value=Place(CampaignDressingKind020.BridgeUnderside,bridge,18,43,bridge,18,new float3(-17,-8.5f,-4.5f),new float3(17,-.8f,4.5f));return true;
                default:return false;
            }
        }
        public static int PrototypeCommandCount
        {get{int count=0;for(int index=0;index<PrototypeCount;index++){TryPrototype(index,out var value);count+=CommandCount(value.Kind);}return count;}}
        public static void EmitPrototype<T>(ref T sink) where T:struct,ICampaignBuildingSink020
        {for(int index=0;index<PrototypeCount;index++){TryPrototype(index,out var value);Emit(ref sink,value.Kind,value.Origin,value.Yaw,value.Seed);}}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original metre/Y-up rigid local recipes/front-Z and finite provisional hub placements; bridge supports stay below existing deck bottoms, cups start on declared solid tops.
// Array Formats: Ten fixed-role scalar recipes, sixteen value placements and at most38 commands/role or85/party set through the existing generic struct Box sink; shared palette, deterministic integer seeds, no Object/heap/container/frame loop.
// Dependencies/Pointers: Receiving native sink owns capacity/overflow, completed jobs and upload; World owns mesh/material/colliders. Prototype envelopes are conservative placement metadata requiring root support/clearance acceptance; no floor/deck/collider/region mutation or license/visual guarantee is implied. Full party set must replace prior visual mass explicitly rather than stack onto it.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing localX34m/Z9m bridge footprint, both original yaw18/origins/guards unchanged; stoneworkY-8.4..-.8 only.
// Array Formats: Fixed80 Box commands/bridge: five6-piece piers, four8-step arches, eight face keys and ten attached moss strips; existing shared palette/seed, no Object/heap/frame loop.
// Dependencies/Pointers: Presentation-only replacement of BridgeUnderside; deck/fence/wall/torch/support/colliders/travel remain their existing owners. Receiver owns capacity/overflow/Burst mesh/resource lifetime;1006 combined commands is current-source arithmetic, not native/visual acceptance.
