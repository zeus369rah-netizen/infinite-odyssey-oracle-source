using Unity.Mathematics;

namespace OdysseyOracle
{
    public interface ICampaignBuildingSink020
    {
        void Box(float3 center,float3 size,quaternion rotation,float4 color,float emission);
    }

    public struct CampaignBuildingAnchor020
    {
        public float3 Position,MaximumSize;
        public quaternion Rotation;
        // Kind:0 service approach,1 table,2/3 chairs,4 cup,5 sign.
        // Support:0 ground,1 accepted table top,2 wall. Parent:-1 or table anchor1.
        public int Kind,Support,Parent,RequiresExternalClearance;
    }

    // Cold numeric recipe. The caller owns native storage, culling, collision and upload.
    public static class CampaignHubBuildingRecipe020
    {
        public const int RoleCount=3,AnchorCount=6,MaximumCommands=362;
        static readonly float4 Stone=new float4(.245f,.226f,.215f,1);
        static readonly float4 EdgeStone=new float4(.365f,.335f,.302f,1);
        static readonly float4 DeepStone=new float4(.105f,.112f,.142f,1);
        static readonly float4 Slate=new float4(.15f,.175f,.225f,1);
        static readonly float4 SlateEdge=new float4(.205f,.225f,.275f,1);
        static readonly float4 Wood=new float4(.215f,.112f,.055f,1);
        static readonly float4 PaleWood=new float4(.31f,.17f,.075f,1);
        static readonly float4 Iron=new float4(.105f,.10f,.105f,1);
        static readonly float4 Brass=new float4(.56f,.33f,.105f,1);
        static readonly float4 Warm=new float4(1f,.52f,.145f,1);
        static readonly float4 Cyan=new float4(.10f,.62f,1f,1);
        static readonly float4 Leaf=new float4(.12f,.24f,.10f,1),Petal=new float4(.56f,.08f,.08f,1);

        public static int CommandCount(int role)=>role==0?354:role==1?160:role==2?177:0;
        public static float3 Dimensions(int role)=>role==0?new float3(14,13.3f,12):role==1?new float3(12,14.2f,12):role==2?new float3(12,12,14):float3.zero;

        public static void WorldBounds(int role,float3 center,float yaw,out float3 minimum,out float3 maximum)
        {
            if(role<0||role>=RoleCount){minimum=maximum=center;return;}
            float3 dimensions=Dimensions(role);
            float height=dimensions.y*.5f;float3 half=new float3(dimensions.x*.5f+.3f,height,dimensions.z*.5f+(role==0?.8f:.3f));
            var matrix=new float3x3(quaternion.RotateY(math.radians(yaw)));
            float3 extent=math.abs(matrix.c0)*half.x+math.abs(matrix.c1)*half.y+math.abs(matrix.c2)*half.z;
            float3 middle=center+new float3(0,height,0);minimum=middle-extent;maximum=middle+extent;
        }

        public static void Emit<T>(ref T sink,int role,float3 center,float yaw) where T:struct,ICampaignBuildingSink020
        {
            if(role<0||role>=RoleCount||!math.all(math.isfinite(center))||!math.isfinite(yaw))return;
            float3 dimensions=Dimensions(role);float w=dimensions.x,d=dimensions.z,bw=w-1.2f,bd=d-1.8f;
            quaternion rotation=quaternion.RotateY(math.radians(yaw));
            if(role==0)CampaignTavernRecipe020.EmitShell(ref sink,center,rotation);
            else
            {
                Put(ref sink,center,rotation,new float3(0,4.3f,0),new float3(bw,8.6f,bd),Stone);
                Put(ref sink,center,rotation,new float3(0,.23f,0),new float3(w-1f,.46f,d-1.6f),EdgeStone);
            }
            for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)
            {
                Put(ref sink,center,rotation,new float3(side*(bw*.5f-.17f),4.55f,end*(bd*.5f+.08f)),new float3(.27f,8.1f,.25f),Wood);
                for(int row=0;row<3;row++)
                {
                    float y=row==0?.85f:row==1?4.85f:7.75f;
                    Put(ref sink,center,rotation,new float3(side*(bw*.5f-.10f),y,end*bd*.5f),new float3(.38f,.52f,.38f),EdgeStone);
                }
            }
            for(int level=0;level<2;level++)
            {
                float y=level==0?4.35f:8.3f;
                for(int side=-1;side<=1;side+=2)
                {
                    Put(ref sink,center,rotation,new float3(0,y,side*(bd*.5f+.11f)),new float3(bw+.36f,.28f,.26f),Wood);
                    Put(ref sink,center,rotation,new float3(side*(bw*.5f+.10f),y,0),new float3(.26f,.28f,bd+.16f),Wood);
                }
            }
            if(role==1)TowerRoof(ref sink,center,rotation,w,d);else if(role==0)TavernRoof(ref sink,center,rotation,w,d);else Roof(ref sink,center,rotation,w,d,false);
            float3 front=new float3(0,0,-bd*.5f);
            if(role==0)CampaignTavernRecipe020.EmitOpenDoor(ref sink,center,rotation,front);else Door(ref sink,center,rotation,front);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*(w*.5f-.88f);
                Beam(ref sink,center,rotation,new float3(x,4.52f,front.z-.13f),new float3(x-side*1.10f,5.05f,front.z-.13f),.18f,Wood);
                Beam(ref sink,center,rotation,new float3(x,4.52f,-front.z+.13f),new float3(x-side*1.10f,5.05f,-front.z+.13f),.18f,Wood);
            }
            if(role==1)
            {
                Window(ref sink,center,rotation,front,0,0,6.2f,3.2f,4.25f,Cyan);
                for(int side=-1;side<=1;side+=2)
                    Window(ref sink,center,rotation,new float3(side*bw*.5f,0,0),-side*90,0,6.05f,2.05f,2.85f,Warm);
                Window(ref sink,center,rotation,new float3(0,0,bd*.5f),180,0,6.05f,2.05f,2.85f,Warm);
            }
            else
            {
                for(int level=0;level<2;level++)for(int side=-1;side<=1;side+=2)
                {
                    float y=level==0?2.55f:6.25f;
                    Window(ref sink,center,rotation,front,0,side*w*.27f,y,2.05f,2.10f,Warm);
                    Window(ref sink,center,rotation,new float3(side*bw*.5f,0,0),-side*90,0,y,2.05f,2.10f,Warm);
                }
                for(int side=-1;side<=1;side+=2)
                    Window(ref sink,center,rotation,new float3(0,0,bd*.5f),180,side*w*.27f,6.25f,2.05f,2.10f,Warm);
                if(role!=0)Window(ref sink,center,rotation,new float3(0,0,-d*.5f-.06f),0,0,9.65f,1.15f,1.15f,Warm,.38f);
            }
            Banner(ref sink,center,rotation,new float3(w*.5f-1.08f,0,front.z),role);
            for(int side=-1;side<=1;side+=2)
                Barrel(ref sink,center,rotation,new float3(side*(w*.5f-(role==2?1.10f:role==1?1.26f:1.60f)),0,-d*.5f+(role==2?.65f:.40f)),role==2?1f:role==1?.72f:.8f);
            if(role==0)CampaignTavernRecipe020.EmitInterior(ref sink,center,yaw);
            else
            {
                Put(ref sink,center,rotation,new float3(0,.10f,-d*.5f+.52f),new float3(3.3f,.20f,.66f),EdgeStone);
                Put(ref sink,center,rotation,new float3(0,.20f,front.z-.18f),new float3(2.95f,.40f,.40f),EdgeStone);
            }
            if(role!=2)Details(ref sink,role,center,rotation,w,d,bw,bd);
        }

        static void TowerRoof<T>(ref T sink,float3 origin,quaternion rotation,float width,float depth) where T:struct,ICampaignBuildingSink020
        {
            for(int row=0;row<14;row++)
                Put(ref sink,origin,rotation,new float3(0,8.775f+row*.35f,0),new float3(math.lerp(width+.6f,.9f,row/13f),.35f,depth+.12f),row%3==0?SlateEdge:Slate);
            Put(ref sink,origin,rotation,new float3(0,13.61f,0),new float3(.72f,.22f,depth+.25f),DeepStone);
            for(int end=-1;end<=1;end+=2)Put(ref sink,origin,rotation,new float3(0,13.92f,end*(depth*.5f-.18f)),new float3(.24f,.42f,.26f),EdgeStone);
        }

        static void TavernRoof<T>(ref T sink,float3 origin,quaternion rotation,float width,float depth) where T:struct,ICampaignBuildingSink020
        {
            // Main pitched mass: 14 contiguous courses terminate at a ridge whose top is Y=12.79.
            for(int row=0;row<14;row++)
            {
                float t=row/13f;
                Put(ref sink,origin,rotation,new float3(0,8.72f+row*.28f,0),new float3(math.lerp(width+.6f,.9f,t),.28f,depth+.12f),row%3==0?SlateEdge:Slate);
            }
            Put(ref sink,origin,rotation,new float3(0,12.64f,0),new float3(.72f,.30f,depth+.35f),DeepStone);
            for(int end=-1;end<=1;end+=2)
            {
                float z=end*(depth*.5f-.18f);
                Put(ref sink,origin,rotation,new float3(0,12.72f,z),new float3(.24f,.30f,.28f),EdgeStone);
                for(int side=-1;side<=1;side+=2)
                    Beam(ref sink,origin,rotation,new float3(side*(width*.5f-.35f),8.72f,end*(depth*.5f+.12f)),new float3(0,12.60f,end*(depth*.5f+.12f)),.16f,Wood);
            }

            for(int bay=0;bay<2;bay++)
            {
                float centerX=bay==0?-3.2f:3.2f,bodyWidth=bay==0?3.6f:3.0f;
                float peakY=bay==0?12.15f:11.95f,baseY=8.62f,shoulderY=10.72f,eaveY=10.64f;
                float frontZ=-6.48f,backZ=-3.12f,bodyDepth=backZ-frontZ,bodyCenterZ=(frontZ+backZ)*.5f;
                float run=bodyWidth*.5f+.12f,rise=peakY-eaveY,stepHeight=(peakY-shoulderY)/6f;
                float roofFront=frontZ-.15f,roofBack=backZ+.12f,roofDepth=roofBack-roofFront,roofCenterZ=(roofFront+roofBack)*.5f;

                // A filled rectangular house body projects0.42m beyond the main roof; its shallow gable stays enclosed at every step.
                Put(ref sink,origin,rotation,new float3(centerX,(baseY+shoulderY)*.5f,bodyCenterZ),new float3(bodyWidth,shoulderY-baseY,bodyDepth),Stone);
                for(int row=0;row<6;row++)
                {
                    float y=shoulderY+(row+.5f)*stepHeight;
                    float courseWidth=2f*run*(peakY-y)/rise;
                    Put(ref sink,origin,rotation,new float3(centerX,y,bodyCenterZ),new float3(courseWidth,stepHeight+.02f,bodyDepth),row%2==0?Stone:EdgeStone);
                }
                for(int side=-1;side<=1;side+=2)
                {
                    float eaveX=centerX+side*run,dx=centerX-eaveX;
                    float slopeLength=math.sqrt(dx*dx+rise*rise),angle=math.atan2(rise,dx);
                    // The0.16m half-thickness exceeds every half-course normal gap; both ends overhang their supporting walls.
                    Put(ref sink,origin,rotation,new float3((eaveX+centerX)*.5f,(eaveY+peakY)*.5f,roofCenterZ),new float3(slopeLength,.32f,roofDepth),Slate,0,quaternion.RotateZ(angle));
                    Beam(ref sink,origin,rotation,new float3(eaveX,eaveY,frontZ-.17f),new float3(centerX,peakY,frontZ-.17f),.14f,Wood);
                }
                Put(ref sink,origin,rotation,new float3(centerX,peakY+.03f,roofCenterZ),new float3(.40f,.24f,roofDepth+.12f),DeepStone);
                for(int side=-1;side<=1;side+=2)
                    Put(ref sink,origin,rotation,new float3(centerX+side*(bodyWidth*.5f-.08f),(baseY+shoulderY)*.5f,frontZ-.045f),new float3(.18f,shoulderY-baseY,.15f),PaleWood);
                Put(ref sink,origin,rotation,new float3(centerX,8.73f,frontZ-.06f),new float3(bodyWidth+.18f,.18f,.18f),PaleWood);
                Put(ref sink,origin,rotation,new float3(centerX,shoulderY,frontZ-.06f),new float3(bodyWidth+.18f,.16f,.18f),PaleWood);
                for(int side=-1;side<=1;side+=2)
                    Put(ref sink,origin,rotation,new float3(centerX+side*(bodyWidth*.5f-.26f),8.55f,frontZ+.30f),new float3(.34f,.50f,.86f),EdgeStone);
                DeepGableWindow(ref sink,origin,rotation,centerX,(baseY+shoulderY)*.5f,frontZ);
            }
        }

        static void Cornice<T>(ref T sink,float3 origin,quaternion rotation,float bw,float bd,float y) where T:struct,ICampaignBuildingSink020
        {
            for(int side=-1;side<=1;side+=2)
            {
                Put(ref sink,origin,rotation,new float3(0,y,side*(bd*.5f+.22f)),new float3(bw+.74f,.18f,.30f),EdgeStone);
                Put(ref sink,origin,rotation,new float3(side*(bw*.5f+.22f),y,0),new float3(.30f,.18f,bd+.74f),EdgeStone);
            }
        }

        static void FlowerBox<T>(ref T sink,float3 origin,quaternion rotation,float3 face,float faceYaw,float x,float y) where T:struct,ICampaignBuildingSink020
        {
            quaternion q=math.mul(rotation,quaternion.RotateY(math.radians(faceYaw)));float3 p=origin+math.rotate(rotation,face);
            Put(ref sink,p,q,new float3(x,y,-.31f),new float3(1.65f,.32f,.32f),PaleWood);
            Put(ref sink,p,q,new float3(x,y+.185f,-.31f),new float3(1.49f,.05f,.27f),Leaf);
            for(int side=-1;side<=1;side+=2)
            {
                Put(ref sink,p,q,new float3(x+side*.56f,y-.10f,-.06f),new float3(.14f,.56f,.36f),Wood);
                Put(ref sink,p,q,new float3(x+side*.50f,y-.10f,-.485f),new float3(.16f,.50f,.03f),Leaf);
            }
            for(int flower=-1;flower<=1;flower++)Put(ref sink,p,q,new float3(x+flower*.45f,y+.27f,-.31f),new float3(.15f,.12f,.14f),Petal);
        }

        static void Details<T>(ref T sink,int role,float3 origin,quaternion rotation,float w,float d,float bw,float bd) where T:struct,ICampaignBuildingSink020
        {
            Cornice(ref sink,origin,rotation,bw,bd,8.38f);
            for(int end=-1;end<=1;end+=2)for(int side=-1;side<=1;side+=2)
                Put(ref sink,origin,rotation,new float3(side*(w*.5f-.8f),8.07f,end*(bd*.5f+.19f)),new float3(.40f,.60f,.45f),Wood);
            if(role==0)
            {
                Cornice(ref sink,origin,rotation,bw,bd,4.15f);
                // Chimney bottom is buried into existing roof mass; cap/soot/brick faces touch their parent.
                Put(ref sink,origin,rotation,new float3(2.6f,11.35f,2.4f),new float3(1.2f,3.3f,1.3f),Stone);
                for(int band=0;band<2;band++)Put(ref sink,origin,rotation,new float3(2.6f,11.35f+band*1.30f,2.4f),new float3(1.4f,.24f,1.5f),EdgeStone);
                Put(ref sink,origin,rotation,new float3(2.6f,13.12f,2.4f),new float3(1.6f,.24f,1.7f),EdgeStone);
                Put(ref sink,origin,rotation,new float3(2.6f,13.25f,2.4f),new float3(1.05f,.02f,1.15f),DeepStone);
                for(int course=0;course<3;course++)Put(ref sink,origin,rotation,new float3(2.6f+(course%2==0?.10f:-.10f),10.8f+course*.70f,1.72f),new float3(.90f,.13f,.06f),EdgeStone);
                for(int side=-1;side<=1;side+=2)FlowerBox(ref sink,origin,rotation,new float3(0,0,-bd*.5f),0,side*w*.27f,6.25f-1.05f-.82f);
            }
            else
            {
                Window(ref sink,origin,rotation,new float3(0,0,-d*.5f-.06f),0,0,11.35f,1.30f,1.30f,Warm,.38f);
                for(int side=-1;side<=1;side+=2)Beam(ref sink,origin,rotation,new float3(side*(w*.5f-.5f),8.78f,-d*.5f-.13f),new float3(0,13.45f,-d*.5f-.13f),.18f,Wood);
                for(int row=0;row<3;row++)for(int column=-1;column<=1;column++)
                    Put(ref sink,origin,rotation,new float3(column*.82f,5.15f+row*1.15f,-bd*.5f-.121f),new float3(.42f,.42f,.014f),row%2==0?Cyan:SlateEdge,1,quaternion.RotateZ(math.PI*.25f));
                for(int side=-1;side<=1;side+=2)FlowerBox(ref sink,origin,rotation,new float3(side*bw*.5f,0,0),-side*90,0,6.05f-1.425f-.82f);
            }
        }

        static void Roof<T>(ref T sink,float3 origin,quaternion rotation,float width,float depth,bool tower,bool joined=false) where T:struct,ICampaignBuildingSink020
        {
            for(int row=0;row<10;row++)
            {
                float t=row/9f,w=math.lerp(width+.6f,.9f,t),d=tower?math.lerp(depth+.6f,.9f,t):depth+.12f;
                Put(ref sink,origin,rotation,new float3(0,8.75f+row*.30f,0),new float3(w,.30f,d),row%3==0?SlateEdge:Slate);
            }
            Put(ref sink,origin,rotation,new float3(0,11.69f,0),new float3(.72f,.18f,tower?.72f:depth+.45f),DeepStone);
            if(tower)Put(ref sink,origin,rotation,new float3(0,11.88f,0),new float3(.24f,.24f,.24f),EdgeStone);
            else for(int end=-1;end<=1;end+=2)
            {
                Put(ref sink,origin,rotation,new float3(0,11.78f,end*(depth*.5f-.18f)),new float3(.30f,.42f,.32f),EdgeStone);
                for(int side=-1;side<=1;side+=2)
                    Beam(ref sink,origin,rotation,new float3(side*(width*.5f-.35f),8.78f,end*(depth*.5f+(joined?.13f:.18f))),new float3(0,11.50f,end*(depth*.5f+(joined?.13f:.18f))),.18f,Wood);
            }
        }

        static void DeepGableWindow<T>(ref T sink,float3 origin,quaternion rotation,float x,float y,float faceZ) where T:struct,ICampaignBuildingSink020
        {
            // Opaque decorative glazing: the backing overlaps the wall, while the iron bars sit0.025m behind the frame front.
            Put(ref sink,origin,rotation,new float3(x,y,faceZ-.025f),new float3(1.48f,1.46f,.06f),DeepStone);
            Put(ref sink,origin,rotation,new float3(x,y,faceZ-.070f),new float3(1.32f,1.30f,.04f),Warm,1);
            for(int side=-1;side<=1;side+=2)
                Put(ref sink,origin,rotation,new float3(x+side*.72f,y,faceZ-.075f),new float3(.16f,1.62f,.20f),PaleWood);
            for(int side=-1;side<=1;side+=2)
                Put(ref sink,origin,rotation,new float3(x,y+side*.73f,faceZ-.075f),new float3(1.60f,.16f,.20f),PaleWood);
            Put(ref sink,origin,rotation,new float3(x,y,faceZ-.100f),new float3(.08f,1.30f,.10f),Iron);
            Put(ref sink,origin,rotation,new float3(x,y,faceZ-.100f),new float3(1.32f,.08f,.10f),Iron);
            Put(ref sink,origin,rotation,new float3(x,y-.80f,faceZ-.10f),new float3(1.84f,.14f,.32f),EdgeStone);
        }

        static void Window<T>(ref T sink,float3 origin,quaternion rotation,float3 face,float faceYaw,float x,float y,float w,float h,float4 glow,float depth=1) where T:struct,ICampaignBuildingSink020
        {
            quaternion q=math.mul(rotation,quaternion.RotateY(math.radians(faceYaw)));
            float3 p=origin+math.rotate(rotation,face);
            Put(ref sink,p,q,new float3(x,y,-.05f*depth),new float3(w+.28f,h+.28f,.08f*depth),DeepStone);
            Put(ref sink,p,q,new float3(x,y,-.10f*depth),new float3(w,h,.03f*depth),glow,1);
            for(int side=-1;side<=1;side+=2)
            {
                Put(ref sink,p,q,new float3(x+side*(w+.14f)*.5f,y,-.17f*depth),new float3(.14f,h+.28f,.18f*depth),PaleWood);
                Put(ref sink,p,q,new float3(x,y+side*(h+.14f)*.5f,-.17f*depth),new float3(w,.14f,.18f*depth),PaleWood);
            }
            Put(ref sink,p,q,new float3(x,y,-.24f*depth),new float3(.11f,h,.15f*depth),Wood);
            Put(ref sink,p,q,new float3(x,y,-.24f*depth),new float3(w,.11f,.15f*depth),Wood);
            Put(ref sink,p,q,new float3(x,y-h*.5f-.16f,-.23f*depth),new float3(w+.44f,.18f,.55f*depth),EdgeStone);
        }

        static void Door<T>(ref T sink,float3 origin,quaternion rotation,float3 face) where T:struct,ICampaignBuildingSink020
        {
            float3 p=origin+math.rotate(rotation,face);
            Put(ref sink,p,rotation,new float3(0,1.8f,-.05f),new float3(2.65f,3.6f,.10f),DeepStone);
            for(int board=0;board<5;board++)
                Put(ref sink,p,rotation,new float3((board-2)*.45f,1.7f,-.14f),new float3(.43f,3.4f,.10f),board%2==0?Wood:PaleWood);
            for(int level=0;level<2;level++)
            {
                float y=level==0?.85f:2.50f;
                Put(ref sink,p,rotation,new float3(0,y,-.23f),new float3(2.20f,.11f,.16f),Iron);
                Put(ref sink,p,rotation,new float3(-1.02f,y,-.25f),new float3(.18f,.24f,.18f),Iron);
            }
            for(int side=-1;side<=1;side+=2)
                Put(ref sink,p,rotation,new float3(side*1.34f,1.8f,-.10f),new float3(.30f,3.6f,.34f),EdgeStone);
            Put(ref sink,p,rotation,new float3(0,3.70f,-.10f),new float3(2.98f,.30f,.34f),EdgeStone);
            Put(ref sink,p,rotation,new float3(.70f,1.7f,-.28f),new float3(.16f,.36f,.18f),Brass);
        }

        static void Banner<T>(ref T sink,float3 origin,quaternion rotation,float3 face,int role) where T:struct,ICampaignBuildingSink020
        {
            float3 p=origin+math.rotate(rotation,face);
            float4 cloth=role==1?new float4(.055f,.18f,.20f,1):role==2?new float4(.29f,.105f,.07f,1):new float4(.245f,.065f,.16f,1);
            Put(ref sink,p,rotation,new float3(0,6.3f,-.20f),new float3(1.05f,.13f,.50f),Wood);
            Put(ref sink,p,rotation,new float3(0,5.25f+(role==2?0:.085f),-.22f),new float3(.84f,1.8f,.06f),cloth);
            for(int side=-1;side<=1;side+=2)
                Put(ref sink,p,rotation,new float3(side*.255f,4.26f+(role==2?0:.085f),-.22f),new float3(.33f,.18f,.06f),cloth);
            Put(ref sink,p,rotation,new float3(0,5.30f,-.265f),new float3(.12f,.75f,.04f),Brass);
            Put(ref sink,p,rotation,new float3(0,5.40f,-.265f),new float3(.48f,.12f,.04f),Brass);
            Put(ref sink,p,rotation,new float3(0,6.30f,-.47f),new float3(.13f,.13f,.10f),Iron);
        }

        static void Barrel<T>(ref T sink,float3 origin,quaternion rotation,float3 foot,float scale=1) where T:struct,ICampaignBuildingSink020
        {
            Put(ref sink,origin,rotation,foot+new float3(0,.60f,0)*scale,new float3(.85f,1.20f,.85f)*scale,PaleWood);
            Put(ref sink,origin,rotation,foot+new float3(0,.27f,0)*scale,new float3(.91f,.11f,.91f)*scale,Iron);
            Put(ref sink,origin,rotation,foot+new float3(0,.93f,0)*scale,new float3(.91f,.11f,.91f)*scale,Iron);
            Put(ref sink,origin,rotation,foot+new float3(0,1.20f,0)*scale,new float3(.82f,.06f,.82f)*scale,Wood);
        }

        static void Beam<T>(ref T sink,float3 origin,quaternion rotation,float3 a,float3 b,float thickness,float4 color) where T:struct,ICampaignBuildingSink020
        {
            float3 delta=b-a;float angle=math.atan2(delta.y,delta.x);
            Put(ref sink,origin,rotation,(a+b)*.5f,new float3(math.length(delta),thickness,thickness),color,0,quaternion.RotateZ(angle));
        }

        static void Put<T>(ref T sink,float3 origin,quaternion rotation,float3 local,float3 size,float4 color,float emission=0,quaternion extra=default) where T:struct,ICampaignBuildingSink020
        {
            if(math.lengthsq(extra.value)<.5f)extra=quaternion.identity;
            if(emission==0)
            {
                if(math.all(color==Stone)||math.all(color==EdgeStone))emission=-1;
                else if(math.all(color==Slate)||math.all(color==SlateEdge))emission=-2;
            }
            sink.Box(origin+math.rotate(rotation,local),size,math.mul(rotation,extra),color,emission);
        }

        public static bool TryAnchor(int role,int index,float3 center,float yaw,out CampaignBuildingAnchor020 anchor)
        {
            anchor=default;if(role<0||role>=RoleCount||index<0||index>=AnchorCount)return false;
            float3 dimensions=Dimensions(role);float w=dimensions.x,d=dimensions.z;
            float3 position,size;float turn=0;int support=0,parent=-1,external=0;
            switch(index)
            {
                case 0:position=new float3(0,0,-d*.5f-1.2f);size=new float3(2,2.2f,1);external=1;break;
                case 1:position=new float3(-w*.26f,0,-d*.5f+.42f);size=new float3(1.25f,1.2f,.65f);break;
                case 2:position=new float3(-w*.26f-.95f,0,-d*.5f+.42f);size=new float3(.55f,1.3f,.55f);turn=90;break;
                case 3:position=new float3(-w*.26f+.95f,0,-d*.5f+.42f);size=new float3(.55f,1.3f,.55f);turn=-90;break;
                case 4:position=new float3(-w*.26f+.25f,0,-d*.5f+.42f);size=new float3(.20f,.28f,.20f);support=1;parent=1;break;
                default:position=new float3(-w*.40f,4.05f,-d*.5f+.45f);size=new float3(.85f,.70f,.35f);support=2;break;
            }
            quaternion q=quaternion.RotateY(math.radians(yaw));
            anchor=new CampaignBuildingAnchor020{Kind=index,Position=center+math.rotate(q,position),Rotation=math.mul(q,quaternion.RotateY(math.radians(turn))),MaximumSize=size,Support=support,Parent=parent,RequiresExternalClearance=external};
            return true;
        }

        public static bool TryCaptureView(int role,int index,float3 center,float yaw,out float3 eye,out float3 target)
        {
            eye=target=default;if(role<0||role>=RoleCount||index<0||index>2)return false;
            float3 dimensions=Dimensions(role);quaternion rotation=quaternion.RotateY(math.radians(yaw));
            float3 local=index==0?new float3(0,1.75f,-dimensions.z*.5f-12):index==1?new float3(dimensions.x*.75f,3.2f,-dimensions.z*.5f-9):new float3(0,dimensions.y+1,-dimensions.z*.5f-14);
            eye=center+math.rotate(rotation,local);target=center+new float3(0,index==2?9.4f:5.6f,0);return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Y-up/front-Z/scale1 building metres. Tavern ground14x12 and height13.3m unchanged; conservative roof boundsX+/-7.3,Z+/-6.8; actual front decoration reachesZ=-6.74.
// Array Formats: Cold value-only Box sink, role0 exact354 commands within retainedMaximum362; tower160 and Smith177 unchanged; six anchors. Two filled27-command dormers replace thin31-command gables.
// Dependencies/Pointers: Unity.Mathematics and unchanged tavern shell/dressing. No Object/frame allocation owner, collider/world/material/shader/farm change. Root owns actual import/capture; opaque decorative glazing does not claim see-through apertures.
