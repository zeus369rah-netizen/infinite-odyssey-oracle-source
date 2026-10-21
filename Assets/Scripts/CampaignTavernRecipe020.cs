using Unity.Mathematics;

namespace OdysseyOracle
{
    // Original role0's exterior detail/roof remains in its existing owner; only hollow shell,
    // permanently open retained-style door and finite interior furniture are emitted here.
    public static class CampaignTavernRecipe020
    {
        public const int ShellCommands=11,OpenDoorCommands=13,InteriorCommands=84;
        static readonly float4 Stone=new float4(.245f,.226f,.215f,1),EdgeStone=new float4(.365f,.335f,.302f,1);
        static readonly float4 Wood=new float4(.215f,.112f,.055f,1),PaleWood=new float4(.31f,.17f,.075f,1);
        static readonly float4 Iron=new float4(.105f,.10f,.105f,1),Brass=new float4(.56f,.33f,.105f,1);
        static void Put<T>(ref T sink,float3 origin,quaternion turn,float3 local,float3 size,float4 color,float emission=0) where T:struct,ICampaignBuildingSink020
        {sink.Box(origin+math.rotate(turn,local),size,turn,color,emission);}

        public static void EmitShell<T>(ref T sink,float3 origin,quaternion turn) where T:struct,ICampaignBuildingSink020
        {
            // Actual stone body is a6-box union: clear interiorX[-5.8,5.8],Z[-4.5,4.5].
            Put(ref sink,origin,turn,new float3(-6.1f,4.3f,0),new float3(.6f,8.6f,10.2f),Stone,-1);
            Put(ref sink,origin,turn,new float3(6.1f,4.3f,0),new float3(.6f,8.6f,10.2f),Stone,-1);
            Put(ref sink,origin,turn,new float3(0,4.3f,4.8f),new float3(11.6f,8.6f,.6f),Stone,-1);
            for(int side=-1;side<=1;side+=2)
                Put(ref sink,origin,turn,new float3(side*3.57f,4.3f,-4.8f),new float3(4.46f,8.6f,.6f),Stone,-1);
            Put(ref sink,origin,turn,new float3(0,6.075f,-4.8f),new float3(2.68f,5.05f,.6f),Stone,-1);
            // Retain the original raised foundation as a ring, with no stone across the doorway/floor.
            Put(ref sink,origin,turn,new float3(-6.15f,.23f,0),new float3(.7f,.46f,10.4f),EdgeStone,-1);
            Put(ref sink,origin,turn,new float3(6.15f,.23f,0),new float3(.7f,.46f,10.4f),EdgeStone,-1);
            Put(ref sink,origin,turn,new float3(0,.23f,4.85f),new float3(11.6f,.46f,.7f),EdgeStone,-1);
            for(int side=-1;side<=1;side+=2)
                Put(ref sink,origin,turn,new float3(side*3.57f,.23f,-4.85f),new float3(4.46f,.46f,.7f),EdgeStone,-1);
        }

        public static void EmitOpenDoor<T>(ref T sink,float3 origin,quaternion turn,float3 face) where T:struct,ICampaignBuildingSink020
        {
            float3 front=origin+math.rotate(turn,face);
            // Original frame remains. An opaque backplate would close the actual opening, so it is omitted.
            for(int side=-1;side<=1;side+=2)
                Put(ref sink,front,turn,new float3(side*1.34f,1.8f,-.10f),new float3(.30f,3.6f,.34f),EdgeStone,-1);
            Put(ref sink,front,turn,new float3(0,3.70f,-.10f),new float3(2.98f,.30f,.34f),EdgeStone,-1);
            float3 hinge=front+math.rotate(turn,new float3(-1.40f,0,-.14f));
            quaternion leaf=math.mul(turn,quaternion.RotateY(-math.PI*.5f));
            // Inward fixed leaf stays inside the original footprint and left of the2m clear lane.
            for(int board=0;board<5;board++)
                Put(ref sink,hinge,leaf,new float3(.215f+board*.45f,1.7f,0),new float3(.43f,3.4f,.10f),board%2==0?Wood:PaleWood);
            for(int level=0;level<2;level++)
            {
                float y=level==0?.85f:2.50f;
                Put(ref sink,hinge,leaf,new float3(1.115f,y,-.09f),new float3(2.20f,.11f,.16f),Iron);
                Put(ref sink,hinge,leaf,new float3(.095f,y,-.11f),new float3(.18f,.24f,.18f),Iron);
            }
            Put(ref sink,hinge,leaf,new float3(1.815f,1.7f,-.14f),new float3(.16f,.36f,.18f),Brass);
        }

        public static void EmitInterior<T>(ref T sink,float3 origin,float yaw) where T:struct,ICampaignBuildingSink020
        {
            quaternion turn=quaternion.RotateY(math.radians(yaw));
            // Existing original low-count shared dressing recipes, cold emitted into the one world mesh.
            for(int table=0;table<2;table++)
            {
                float z=table==0?-1.5f:2.3f;
                float3 p=origin+math.rotate(turn,new float3(-3.75f,0,z));
                CampaignDressingRecipe020.Emit(ref sink,CampaignDressingKind020.Table,p,yaw,(uint)(71+table));
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side<0?-5.15f:-2f;
                    p=origin+math.rotate(turn,new float3(x,0,z));
                    CampaignDressingRecipe020.Emit(ref sink,CampaignDressingKind020.Chair,p,yaw+(side<0?90:-90),(uint)(81+table));
                }
                p=origin+math.rotate(turn,new float3(-3.75f,CampaignDressingRecipe020.TableTopHeight,z));
                CampaignDressingRecipe020.Emit(ref sink,CampaignDressingKind020.Cup,p,yaw,(uint)(91+table));
            }
            Put(ref sink,origin,turn,new float3(3.6f,.6f,1.6f),new float3(3.8f,1.2f,1),Wood);
            Put(ref sink,origin,turn,new float3(3.6f,1.265f,1.6f),new float3(3.92f,.13f,1.12f),PaleWood);
            for(int level=0;level<2;level++)
                Put(ref sink,origin,turn,new float3(3.6f,level==0?.14f:1.09f,1.1f),new float3(3.64f,.12f,.08f),Iron);
            for(int strip=0;strip<4;strip++)
                Put(ref sink,origin,turn,new float3(2.25f+strip*.9f,.61f,1.1f),new float3(.10f,.90f,.06f),PaleWood);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing origin/front-Z/Y-up tavern at scale1;11.6x9m hall with original wall/roof palette and inward fixed open leaf.
// Array Formats:11 shell,13 retained-style open-door and84 interior cold Box commands; existing table/chair/cup flyweights, no Object/heap/frame owner.
// Dependencies/Pointers: Root applies matching Morton wall/furniture collision decomposition and omits old body302/opaque door/central step lips. Original exterior/roof stays in BuildingRecipe; shared TownY0 floor is reused, and existing NPC/audio owners alone admit real Tovin talk.
