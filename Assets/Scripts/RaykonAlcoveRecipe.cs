using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace OdysseyOracle.VisualDressing
{
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct RaykonAlcoveBox32
    {
        public int3 MinimumMillimetres,MaximumMillimetres;
        public uint ColorTag,SupportAnchor;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct RaykonAlcoveAnchor32
    {
        public float3 Position;public uint Id;
        public float3 Normal;public uint ParentId;
    }
    [StructLayout(LayoutKind.Sequential,Pack=4)]
    public struct RaykonAlcoveStrand32
    {
        public float3 A;public float HalfWidth;
        public float3 B;public uint AnchorId;
    }

    // Original project-specific cold recipe. Every Object binding lives in the separate adapter.
    [BurstCompile]
    public struct RaykonAlcoveRecipeJob : IJob
    {
        public const int MaximumBoxes=384,MaximumAnchors=32,MaximumStrands=64;
        public const uint Pointed=256,Torch=512;
        public int Variant;
        public NativeArray<RaykonAlcoveBox32> Boxes;
        public NativeArray<RaykonAlcoveAnchor32> Anchors;
        public NativeArray<RaykonAlcoveStrand32> Strands;
        // box count, anchor count, strand count, invalid/capacity flag. Caller completes before reading.
        public NativeArray<int> Counts;
        private int boxes,anchors,strands,invalid;
        public void Execute()
        {
            boxes=anchors=strands=invalid=0;
            Box(-700,0,-250,700,100,250,0);
            Box(-700,2700,-250,700,2800,250,0);
            Box(-650,80,-250,650,2730,-210,1);
            Box(-700,80,-250,-620,2730,250,0);
            Box(620,80,-250,700,2730,250,0);
            for(int shelf=0;shelf<4;shelf++)
            {
                int top=540+shelf*560;
                Box(-640,top-50,-230,640,top,240,0);
                Anchor(new float3(0,top*.001f,0),(uint)(100+shelf),0);
            }
            for(int slot=0;slot<24;slot++)
            {
                int shelf=slot/6,x=-500+(slot%6)*200,y=540+shelf*560;
                uint support=(uint)(1000+slot);
                Anchor(new float3(x*.001f,y*.001f,0),support,(uint)(100+shelf));
                if(slot==3||slot==20)Skull(x,y,support);
                else if(slot==8||slot==16)Crystal(x,y,support);
                else if(slot==10||slot==22)Potion(x,y,support,(uint)(4+(slot+Variant)%4));
                else Book(x,y,support,slot);
            }
            // The hanger joins the real cornice by15mm; its cup is above the last shelf.
            Box(-35,2700,235,35,2750,405,2);
            Box(-20,2260,365,20,2730,405,2);
            Box(-120,2240,320,120,2300,500,2);
            Box(-160,2550,290,160,2610,530,3);
            Box(-70,2299,350,70,2540,470,Pointed|Torch|8);
            Box(-110,2230,340,110,2250,480,3);
            Anchor(new float3(0,2.41f,.55f),2000,0);
            for(int side=-1;side<=1;side+=2)
            {
                float3 corner=new float3(side*.62f,2.70f,.251f);
                uint anchor=(uint)(side<0?2100:2101);
                Anchor(corner,anchor,0);
                float3 top=corner+new float3(-side*.32f,0,0),post=corner+new float3(0,-.32f,0);
                for(int spoke=0;spoke<5;spoke++)Thread(corner,math.lerp(top,post,spoke*.25f),anchor);
                for(int ring=0;ring<3;ring++)
                {
                    float radius=.35f+ring*.30f;
                    for(int segment=0;segment<4;segment++)
                    {
                        float3 a=math.lerp(corner,math.lerp(top,post,segment*.25f),radius);
                        float3 b=math.lerp(corner,math.lerp(top,post,(segment+1)*.25f),radius);
                        Thread(a,b,anchor);
                    }
                }
            }
            Counts[0]=boxes;Counts[1]=anchors;Counts[2]=strands;Counts[3]=invalid;
        }
        private void Book(int x,int y,uint support,int slot)
        {
            int height=210+(slot*17+Variant*29)%135;
            uint color=(uint)(4+(slot+Variant)%4);
            Box(x-58,y,-120,x+58,y+height,140,color,support);
            Box(x-50,y+35,139,x+50,y+47,145,9,support);
            Box(x-50,y+height-45,139,x+50,y+height-33,145,9,support);
        }
        private void Skull(int x,int y,uint support)
        {
            Box(x-60,y,-50,x+60,y+40,100,10,support);
            Box(x-75,y+35,-70,x+75,y+170,80,10,support);
            Box(x-53,y+95,79,x-17,y+125,88,11,support);
            Box(x+17,y+95,79,x+53,y+125,88,11,support);
            Box(x-10,y+75,79,x+10,y+105,89,11,support);
            for(int tooth=0;tooth<4;tooth++)
                Box(x-45+tooth*25,y+8,99,x-30+tooth*25,y+31,107,10,support);
        }
        private void Crystal(int x,int y,uint support)
        {
            for(int spike=0;spike<3;spike++)
            {
                int cx=x-48+spike*48;
                Box(cx-25,y,-50,cx+25,y+190+(spike==1?95:0),55,Pointed|(uint)(8+spike%2),support);
            }
        }
        private void Potion(int x,int y,uint support,uint color)
        {
            Box(x-55,y,-60,x+55,y+150,60,color,support);
            Box(x-40,y+149,-45,x+40,y+180,45,color,support);
            Box(x-25,y+179,-28,x+25,y+230,28,color,support);
            Box(x-30,y+229,-32,x+30,y+255,32,0,support);
        }
        private void Box(int x0,int y0,int z0,int x1,int y1,int z1,uint color,uint support=0)
        {
            if(boxes>=Boxes.Length||boxes>=MaximumBoxes||x1<=x0||y1<=y0||z1<=z0){invalid=1;return;}
            Boxes[boxes++]=new RaykonAlcoveBox32{MinimumMillimetres=new int3(x0,y0,z0),MaximumMillimetres=new int3(x1,y1,z1),ColorTag=color,SupportAnchor=support};
        }
        private void Anchor(float3 p,uint id,uint parent)
        {
            if(anchors>=Anchors.Length||anchors>=MaximumAnchors){invalid=1;return;}
            Anchors[anchors++]=new RaykonAlcoveAnchor32{Position=p,Normal=new float3(0,1,0),Id=id,ParentId=parent};
        }
        private void Thread(float3 a,float3 b,uint support)
        {
            if(strands>=Strands.Length||strands>=MaximumStrands){invalid=1;return;}
            Strands[strands++]=new RaykonAlcoveStrand32{A=a,B=b,HalfWidth=.003f,AnchorId=support};
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original case-local +Z front/Y-up integer millimetres; prop assembly bases equal exact named shelf-top Y. Web endpoints join case cornice/posts.
// Array Formats: Box32(int3 min/max,uint colorTag/support), Anchor32(float3 position/normal,uint id/parent), Strand32(float3 endpoints,float halfwidth,uint anchor); fixed384/32/64 plus int4 counts per case.
// Dependencies/Pointers: Pure numeric Burst IJob writes caller-owned TempJob arrays; complete its fence before validation/mesh binding and dispose on every path. No Object, physics, UI, audio, quest or native actor authority.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.
