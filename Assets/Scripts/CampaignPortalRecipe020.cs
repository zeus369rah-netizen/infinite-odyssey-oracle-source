using Unity.Mathematics;

namespace OdysseyOracle
{
    // Original cold portal recipe; shared column values are consumed by the existing World collision owner.
    public static class CampaignPortalRecipe020
    {
        public const int CommandCount=169;
        public const int ColumnCount=2,ColumnRows=4,BaseObstacleCount=135,CombinedObstacleCount=137;
        public const byte ColumnColliderKind=7;
        const uint FirstColumnKey=5000u;
        const float ColumnX=5.18f,ColumnFirstY=6.625f,ColumnStepY=1.25f,ColumnBodyZ=50f,ColumnTrimZ=49.23f;
        static readonly float3 ColumnBodySize=new float3(.86f,1.23f,1.3f),ColumnTrimSize=new float3(.66f,1.02f,.16f);
        static readonly float4 Stone=new float4(.29f,.27f,.25f,1),Trim=new float4(.43f,.40f,.35f,1),DarkStone=new float4(.20f,.205f,.21f,1);
        static readonly float4 Pink=new float4(1f,.055f,.72f,1),PalePink=new float4(1f,.29f,.88f,1),DeepPink=new float4(.58f,.025f,.54f,1);
        static readonly float3 Center=new float3(0,11,50);

        public static bool TryColumnRow(int column,int row,out float3 body,out float3 trim)
        {
            body=trim=default;if(column<0||column>=ColumnCount||row<0||row>=ColumnRows)return false;
            float x=(column==0?-1f:1f)*ColumnX,y=ColumnFirstY+row*ColumnStepY;
            body=new float3(x,y,ColumnBodyZ);trim=new float3(x,y,ColumnTrimZ);return true;
        }
        // One conservative union per stone/trim column; the tiny course/trim gaps have no walk-through opening.
        public static bool TryColumnObstacle(int column,out LandingBox020 value)
        {
            value=default;
            if(!TryColumnRow(column,0,out var first,out var firstTrim)||!TryColumnRow(column,ColumnRows-1,out var last,out var lastTrim))return false;
            float3 low=math.min(first-ColumnBodySize*.5f,firstTrim-ColumnTrimSize*.5f);
            float3 high=math.max(last+ColumnBodySize*.5f,lastTrim+ColumnTrimSize*.5f);
            float3 center=(low+high)*.5f;
            if(!PortalLandingLayout020.TryMorton(center.x,center.z,out uint morton))return false;
            value=new LandingBox020(low.x,low.y,low.z,high.x,high.y,high.z,FirstColumnKey+(uint)column,ColumnColliderKind,0,morton,0f,1f,0f);return true;
        }
        public static bool IsColumnObstacle(in LandingBox020 value)=>value.Kind==ColumnColliderKind&&value.Key>=FirstColumnKey&&value.Key<FirstColumnKey+(uint)ColumnCount;
        // Outer merge only: the existing127-core/eight-fixture135-slot mapping remains byte-identical.
        public static bool TryMergedObstacleSlot(int index,out int previous,out int column)
        {
            previous=column=-1;if(index<0||index>=CombinedObstacleCount)return false;int before=0;
            if(index==106){column=0;return true;}if(index>106)before++;
            if(index==131){column=1;return true;}if(index>131)before++;
            previous=index-before;return previous>=0&&previous<BaseObstacleCount;
        }
        public static void Emit<T>(ref T sink) where T:struct,ICampaignBuildingSink020
        {
            // Forty-seven arch commands, separate from the original pylon solids.
            for(int column=0;column<ColumnCount;column++)for(int row=0;row<ColumnRows;row++)
            {
                TryColumnRow(column,row,out var body,out var trim);
                sink.Box(body,ColumnBodySize,quaternion.identity,row%2==0?Stone:DarkStone,0);
                sink.Box(trim,ColumnTrimSize,quaternion.identity,Trim,0);
            }
            for(int i=0;i<15;i++)
            {
                float angle=(i+.5f)*(math.PI/15f),s=math.sin(angle),c=math.cos(angle);
                float3 point=new float3(5.18f*c,11+5.65f*s,50+.018f*(1+i%3));
                quaternion turn=quaternion.RotateZ(math.atan2(5.65f*c,-5.18f*s));
                sink.Box(point,new float3(1.18f,.86f,1.3f),turn,i%3==0?DarkStone:Stone,0);
                sink.Box(point+new float3(0,0,-.77f),new float3(1.02f,.40f,.16f),turn,Trim,0);
            }
            sink.Box(new float3(0,17.30f,50),new float3(.85f,.36f,1.35f),quaternion.identity,Trim,0);

            // Eighteen pylon details: trim stands in front, capstones/crystals strictly above their old Y14 top.
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*6.8f;
                for(int edge=-1;edge<=1;edge+=2)
                    sink.Box(new float3(x+edge*.58f,10.85f,48.94f),new float3(.16f,5.5f,.16f),quaternion.identity,Trim,0);
                sink.Box(new float3(x,8.1f,48.94f),new float3(1.56f,.16f,.16f),quaternion.identity,Trim,0);
                sink.Box(new float3(x,13.82f,48.94f),new float3(1.56f,.24f,.16f),quaternion.identity,Trim,0);
                sink.Box(new float3(x,14.15f,50),new float3(1.82f,.24f,2.12f),quaternion.identity,Stone,0);
                sink.Box(new float3(x,14.40f,50),new float3(1.40f,.18f,1.60f),quaternion.identity,Trim,0);
                Crystal(ref sink,new float3(x,15.45f,50),.72f,1.10f,quaternion.identity);
            }

            // Sixteen opaque stepped strips form a dark visual field, without a render target or shader.
            const float fieldHeight=9f,fieldWidth=3.97f;
            for(int i=0;i<16;i++)
            {
                float y=-fieldHeight*.5f+(i+.5f)*(fieldHeight/16f);
                float edge=math.max(0,math.abs(y)-fieldHeight/32f);
                float width=2f*fieldWidth*math.sqrt(math.max(0,1f-edge*edge/(fieldHeight*fieldHeight*.25f)));
                float shade=(i%3)*.004f;
                sink.Box(Center+new float3(0,y,.40f),new float3(width,fieldHeight/16f,.035f),quaternion.identity,new float4(.029f+shade,.004f,.060f+shade,1),.12f);
            }

            // Sixty energy segments, recessed successively to produce a readable nested tunnel.
            Contour(ref sink,24,4f,4.6f,-.62f,.16f,.13f,Pink,3f,true);
            Contour(ref sink,20,3.43f,4.02f,-.35f,.09f,.09f,DeepPink,1.7f,false);
            Contour(ref sink,16,2.82f,3.38f,-.10f,.065f,.075f,Pink,.9f,false);

            // Twenty-one pendant crystal commands; local Y points along the upper ring radius.
            for(int i=0;i<7;i++)
            {
                float angle=(i+1)*(math.PI/8f);
                Crystal(ref sink,Center+new float3(4.0f*math.cos(angle),4.85f*math.sin(angle),-1.25f),.42f,.65f,quaternion.RotateZ(angle-math.PI*.5f));
            }
            // A seven-command original central diamond and crystal, distinct from the recessed field.
            for(int i=0;i<4;i++)
            {
                float a=math.PI*.5f*i,b=math.PI*.5f*(i+1);
                float3 from=Center+new float3(math.sin(a)*1.25f,math.cos(a)*1.65f,-1.40f);
                float3 to=Center+new float3(math.sin(b)*1.25f,math.cos(b)*1.65f,-1.40f);
                Beam(ref sink,from,to,.18f,.16f,PalePink,2f);
            }
            Crystal(ref sink,Center+new float3(0,0,-1.55f),.68f,1.05f,quaternion.identity);
        }
        static void Contour<T>(ref T sink,int count,float radiusX,float radiusY,float depth,float width,float thickness,float4 color,float emission,bool stepped)
            where T:struct,ICampaignBuildingSink020
        {
            for(int i=0;i<count;i++)
            {
                float a=i*(math.PI*2f/count),b=(i+1)*(math.PI*2f/count);
                float ra=stepped&&i%3==0?1.025f:1f,rb=stepped&&(i+1)%3==0?1.025f:1f;
                float3 from=Center+new float3(math.cos(a)*radiusX*ra,math.sin(a)*radiusY*ra,depth);
                float3 to=Center+new float3(math.cos(b)*radiusX*rb,math.sin(b)*radiusY*rb,depth);
                Beam(ref sink,from,to,width,thickness,color,emission);
            }
        }
        static void Beam<T>(ref T sink,float3 from,float3 to,float width,float depth,float4 color,float emission) where T:struct,ICampaignBuildingSink020
        {
            float3 delta=to-from;
            sink.Box((from+to)*.5f,new float3(math.length(delta)+.012f,width,depth),quaternion.RotateZ(math.atan2(delta.y,delta.x)),color,emission);
        }
        static void Crystal<T>(ref T sink,float3 point,float width,float height,quaternion turn) where T:struct,ICampaignBuildingSink020
        {
            quaternion prism=math.mul(turn,quaternion.RotateY(math.PI*.25f));
            sink.Box(point,new float3(width,height,width*.8f),prism,Pink,3f);
            quaternion tip=math.mul(prism,quaternion.RotateZ(math.PI*.25f));
            sink.Box(point+math.rotate(turn,new float3(0,height*.5f,0)),new float3(width*.68f,width*.68f,width*.56f),tip,PalePink,3f);
            sink.Box(point-math.rotate(turn,new float3(0,height*.5f,0)),new float3(width*.68f,width*.68f,width*.56f),tip,DeepPink,2.4f);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original world-metre portal centered(0,11,50), courtY6; shared column stone/trim poses produce two axis-aligned conservative physical envelopes.
// Array Formats: Exactly169 unchanged visual commands, two scalar kind7/key5000..5001 collision-only records and a137-slot outer Morton merge around the unchanged135-slot fixture/core stream; no buffers or allocations.
// Dependencies/Pointers: Numeric value recipe only; Layout dispatches the outer merge and World owns the two extra cold colliders and their existing lifecycle. Geometry skips these two collision-only records; no visual duplication, new native ownership, floor, gate, endpoint or teleport authority.
