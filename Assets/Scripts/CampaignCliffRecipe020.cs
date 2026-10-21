using Unity.Mathematics;

namespace OdysseyOracle
{
    public static class CampaignCliffRecipe020
    {
        public const int MaximumCommands=256,MaximumEmitted=224;
        const int MaximumMoss=16,MaximumMinerals=8;
        const float Join=.025f,Gap=.04f;
        struct CountSink:ICampaignBuildingSink020
        {
            public int Count;
            public void Box(float3 center,float3 size,quaternion rotation,float4 color,float emission){Count++;}
        }
        static bool Contract(int revision,int bands,int upper,int lower)=>revision==2&&bands==18&&upper==12&&lower==6;
        static bool Finite(float value)=>math.isfinite(value)&&math.abs(value)<=4096f;
        static uint Mix(uint value){unchecked{value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;return value^(value>>16);}}
        static float Unit(uint seed,uint key,int slot)=>((Mix(unchecked(seed^(key*747796405u+(uint)slot*2891336453u)))>>8)&65535u)/65535f;
        // centerXZ/halfXZ and original basis, copied exactly from final contour exclusion constants.
        static bool Mask(int index,out float4 value,out float2 basis)
        {
            basis=new float2(1,0);
            switch(index)
            {
                case 0:value=new float4(75.13346f,-24.41234f,17f,4.5f);basis=new float2(.9510565f,.309017f);return true;
                case 1:value=new float4(-75.13346f,24.41234f,17f,4.5f);basis=new float2(.9510565f,.309017f);return true;
                case 2:value=new float4(0,37,10,17);return true;
                case 3:value=new float4(0,-32,12,12);return true;
                default:value=default;return false;
            }
        }
        static bool Hit(float4 a,float4 b)=>a.x<b.z&&b.x<a.z&&a.y<b.w&&b.y<a.w;
        static void Choose(float4 candidate,ref float4 selected,ref float best)
        {
            float width=candidate.z-candidate.x,depth=candidate.w-candidate.y;
            if(width<.5f||depth<.5f)return;float area=width*depth;if(area>best){best=area;selected=candidate;}
        }
        static bool Fit(in LandingBox020 box,out float4 rectangle)
        {
            rectangle=new float4(box.MinX,box.MinZ,box.MaxX,box.MaxZ);
            float originalArea=(box.MaxX-box.MinX)*(box.MaxZ-box.MinZ);
            for(int i=0;i<4;i++)
            {
                if(!Mask(i,out float4 mask,out float2 basis))return false;
                float2 extent=new float2(math.abs(basis.x)*mask.z+math.abs(basis.y)*mask.w,math.abs(basis.y)*mask.z+math.abs(basis.x)*mask.w);
                float4 excluded=new float4(mask.x-extent.x-Gap,mask.y-extent.y-Gap,mask.x+extent.x+Gap,mask.y+extent.y+Gap);
                if(!Hit(rectangle,excluded))continue;
                float best=0;float4 next=default,candidate=rectangle;
                candidate.z=math.min(rectangle.z,excluded.x);Choose(candidate,ref next,ref best);
                candidate=rectangle;candidate.x=math.max(rectangle.x,excluded.z);Choose(candidate,ref next,ref best);
                candidate=rectangle;candidate.w=math.min(rectangle.w,excluded.y);Choose(candidate,ref next,ref best);
                candidate=rectangle;candidate.y=math.max(rectangle.y,excluded.w);Choose(candidate,ref next,ref best);
                if(best<originalArea*.5f)return false;rectangle=next;
            }
            return true;
        }
        static bool Band(in LandingBox020 box,int index)
        {
            float width=box.MaxX-box.MinX,depth=box.MaxZ-box.MinZ;
            float surface=index<12?0f:-6f,ceiling=index<12?-.8f:-6.8f;
            return Finite(box.MinX)&&Finite(box.MaxX)&&Finite(box.MinZ)&&Finite(box.MaxZ)&&Finite(box.MinY)&&Finite(box.MaxY)&&
                width>=.5f&&depth>=.5f&&width<=512&&depth<=512&&box.Key!=0&&box.Kind==1&&box.Endpoint==0&&
                math.abs(box.MaxY-surface)<.0001f&&math.abs(box.MinY-ceiling)<.0001f&&box.Yaw==0&&box.Cos==1&&box.Sin==0;
        }
        public static bool TryCount(out int required)
        {
            required=0;
            if(!Contract(PortalLandingContour020.Revision,PortalLandingContour020.FloorBandCount,PortalLandingContour020.UpperBandCount,PortalLandingContour020.LowerBandCount))return false;
            int moss=0,minerals=0;uint previous=0;
            for(int i=0;i<PortalLandingContour020.FloorBandCount;i++)
            {
                if(!PortalLandingContour020.TryFloorBand(i,out var box)||!Band(box,i)||box.Key<=previous||!Fit(box,out _)){required=0;return false;}
                previous=box.Key;
                for(int j=0;j<i;j++)
                {
                    if(!PortalLandingContour020.TryFloorBand(j,out var other)||Hit(new float4(box.MinX,box.MinZ,box.MaxX,box.MaxZ),new float4(other.MinX,other.MinZ,other.MaxX,other.MaxZ))){required=0;return false;}
                }
                required+=6;
                if((box.Key&1)==0&&moss<MaximumMoss){moss++;required++;}
                if((box.Key&3)==2&&minerals<MaximumMinerals){minerals++;required+=2;}
            }
            return required<=MaximumEmitted&&required<=MaximumCommands;
        }
        static bool Intersects(float3 center,float3 size,float4 mask,float2 basis)
        {
            float dx=center.x-mask.x,dz=center.z-mask.y,hx=size.x*.5f,hz=size.z*.5f,c=math.abs(basis.x),s=math.abs(basis.y);
            if(math.abs(dx)>=hx+mask.z*c+mask.w*s||math.abs(dz)>=hz+mask.z*s+mask.w*c)return false;
            return math.abs(dx*basis.x-dz*basis.y)<mask.z+hx*c+hz*s&&math.abs(dx*basis.y+dz*basis.x)<mask.w+hx*s+hz*c;
        }
        static bool Box<T>(ref T sink,ref int emitted,in LandingBox020 row,float depthLimit,float x,float top,float z,float width,float height,float depth,float3 rgb,float emission=0) where T:struct,ICampaignBuildingSink020
        {
            float3 center=new float3(x,top-height*.5f,z),size=new float3(width,height,depth);
            if(emitted>=MaximumCommands||!math.all(math.isfinite(center))||!math.all(math.isfinite(size))||!math.all(size>0)||!math.all(math.isfinite(rgb))||!math.all(rgb>=0)||!math.all(rgb<=1)||!math.isfinite(emission)||emission<0||emission>.35f)return false;
            float3 low=center-size*.5f,high=center+size*.5f;
            if(low.x<row.MinX-.0001f||high.x>row.MaxX+.0001f||low.z<row.MinZ-.0001f||high.z>row.MaxZ+.0001f||high.y>row.MinY-.0198f||low.y<row.MinY-depthLimit-.0002f)return false;
            for(int i=0;i<4;i++)if(!Mask(i,out var mask,out var basis)||Intersects(center,size,mask,basis))return false;
            sink.Box(center,size,quaternion.identity,new float4(rgb,1),emission);emitted++;return true;
        }
        static bool Emit<T>(ref T sink,uint seed,out int emitted) where T:struct,ICampaignBuildingSink020
        {
            emitted=0;int moss=0,minerals=0;
            for(int i=0;i<PortalLandingContour020.FloorBandCount;i++)
            {
                if(!PortalLandingContour020.TryFloorBand(i,out var row)||!Fit(row,out var rect))return false;
                float width=rect.z-rect.x,depth=rect.w-rect.y,x=(rect.x+rect.z)*.5f,z=(rect.y+rect.w)*.5f,depthLimit=i<12?18f:16f;
                float unit=math.min(math.clamp(math.sqrt(width*depth)*.18f,1.8f,4.4f),(depthLimit-.02f)/3.8f),top=row.MinY-.02f,bottom=top-unit;
                float tint=.90f+Unit(seed,row.Key,1)*.14f;
                if(!Box(ref sink,ref emitted,row,depthLimit,x,top,z,width,unit,depth,new float3(.285f,.292f,.26f)*tint))return false;
                float part=width/3f,firstZ=0,firstMin=0,firstDepth=0;
                for(int column=0;column<3;column++)
                {
                    float cx=rect.x+(column+.5f)*part+(Unit(seed,row.Key,10+column)-.5f)*.08f*part,cz=z+(Unit(seed,row.Key,20+column)-.5f)*.06f*depth;
                    float cw=part*.82f,cd=depth*.86f,ch=unit*(1f+.7f*Unit(seed,row.Key,30+column)),shade=.84f+.2f*Unit(seed,row.Key,40+column);
                    if(!Box(ref sink,ref emitted,row,depthLimit,cx,bottom+Join,cz,cw,ch,cd,new float3(.25f,.258f,.236f)*shade))return false;
                    if(column!=1)
                    {
                        float tipHeight=unit*(.55f+.55f*Unit(seed,row.Key,50+column));
                        if(!Box(ref sink,ref emitted,row,depthLimit,cx,bottom+Join-ch+Join,cz,cw*.48f,tipHeight,cd*.60f,new float3(.21f,.225f,.205f)*shade))return false;
                    }
                    if(column==0){firstZ=cz;firstMin=cx-cw*.5f;firstDepth=cd;}
                }
                float gutter=firstMin-rect.x;
                if((row.Key&1)==0&&moss<MaximumMoss)
                {
                    moss++;if(!Box(ref sink,ref emitted,row,depthLimit,rect.x+gutter*.5f,bottom+Join,firstZ,gutter*.55f,math.min(.5f,unit*.18f),firstDepth*.26f,new float3(.23f,.32f,.13f)))return false;
                }
                if((row.Key&3)==2&&minerals<MaximumMinerals)
                {
                    minerals++;float px=rect.x+gutter*.5f,pw=math.min(.9f,gutter*.65f),pd=math.min(.65f,firstDepth*.20f),ph=math.min(2f,unit*.62f);
                    if(!Box(ref sink,ref emitted,row,depthLimit,px,bottom+Join,firstZ,pw,ph,pd,new float3(.96f,.07f,.57f),.28f)||
                        !Box(ref sink,ref emitted,row,depthLimit,px,bottom+Join-ph+Join,firstZ,pw*.55f,ph*.45f,pd*.62f,new float3(1f,.14f,.66f),.35f))return false;
                }
            }
            return emitted<=MaximumEmitted;
        }
        // Call from the owning cold recipe. Its existing overflow flag must also stay clear before publication.
        public static bool Build<T>(ref T sink,uint seed,int available,out int emitted) where T:struct,ICampaignBuildingSink020
        {
            emitted=0;if(!TryCount(out int required)||available<required)return false;
            var probe=default(CountSink);
            if(!Emit(ref probe,seed,out int tested)||tested!=required||probe.Count!=required)return false;
            if(!Emit(ref sink,seed,out int written)||written!=required)return false;
            emitted=written;return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: exact final TryFloorBand worldXZ/underside, unchanged18degree bridge OBBs and portal/south-stair masks; no invented world anchor or collision/support/route owner.
// Array Formats: scalar float2/3/4, readonly LandingBox, closed generic struct sink and one count probe; no arrays/Span/Object API/resource owner,≤224commands inside256budget.
// Dependencies/Pointers: root calls from existing cold Burst/native owner with actual remaining2048draw headroom and checks its receiver overflow before publishing; full scalar preflight precedes real sink calls. Actual Burst compilation/native render/union acceptance remain root-owned.
