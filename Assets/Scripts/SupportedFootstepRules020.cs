// PSEUDOCODE: classify only authored support metadata; choose stair direction from proven support/feet deltas; permute four takes per finite bag without allocation.
using System;
namespace OdysseyOracle
{
    public enum SupportedStepFamily020:byte { Stone,WoodStep,WoodCreak,StairUp,StairDown }
    public struct SupportedStepScope020
    {
        public ulong Session,World,Root,Player,Epoch;
        public uint Generation,Revision;
        public bool Valid=>Session!=0&&World!=0&&Root!=0&&Player!=0&&Epoch!=0&&Generation!=0&&Revision!=0;
        public bool Same(in SupportedStepScope020 other)=>Valid&&other.Valid&&Session==other.Session&&World==other.World&&Root==other.Root&&Player==other.Player&&Epoch==other.Epoch&&Generation==other.Generation&&Revision==other.Revision;
    }
    public static class SupportedFootstepRules020
    {
        public const int Floors=69,WoodTops=3,Capacity=Floors+WoodTops;
        public static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        public static bool Metadata(in LandingBox020 box,out byte wood,out byte flight)
        {
            wood=flight=0;
            if(box.Kind==1)
            {
                if(box.Key>=100&&box.Key<=123)flight=1;
                else if(box.Key>=400&&box.Key<=423)flight=2;
                return true;
            }
            if(box.Kind!=CampaignTavernLayout020.ColliderKind||box.Key<8011||box.Key>8013)return false;
            if(box.Key==8011&&Math.Abs(box.MaxY-1.33f)>.0001f||box.Key!=8011&&Math.Abs(box.MaxY-CampaignDressingRecipe020.TableTopHeight)>.0001f)return false;
            wood=1;return true;
        }
        public static SupportedStepFamily020 Family(byte wood,byte flight,uint key,float top,float feetY,bool prior,byte previousFlight,uint previousKey,float previousTop,float previousFeet)
        {
            if(wood==1)return SupportedStepFamily020.WoodStep;
            if(!prior||flight<1||flight>2||flight!=previousFlight||key==previousKey||!Finite(top)||!Finite(feetY)||!Finite(previousTop)||!Finite(previousFeet))return SupportedStepFamily020.Stone;
            float height=top-previousTop,travel=feetY-previousFeet;
            uint first=flight==1?100u:400u;
            if(key<first||key>first+23||previousKey<first||previousKey>first+23)return SupportedStepFamily020.Stone;
            // A grounded stride can span several treads, especially descending. Use the exact authored .25m rise per key, not a frame-height heuristic.
            float expected=((int)key-(int)previousKey)*(flight==1?.25f:-.25f);
            if(Math.Abs(height-expected)>.005f||Math.Abs(height)<.12f||Math.Abs(height-travel)>.10f||height*travel<=0)return SupportedStepFamily020.Stone;
            return height>0?SupportedStepFamily020.StairUp:SupportedStepFamily020.StairDown;
        }
        public static uint Mix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
        public static int Take(ulong serial,uint seed)
        {
            // An odd affine map permutes all4 values in each bag. Repair an identical boundary by swapping first2 positions.
            ulong bag=serial>>2;int index=(int)(serial&3);uint key=Mix(seed^(uint)bag^(uint)(bag>>32));
            int odd=(int)((key&1)*2+1),offset=(int)((key>>1)&3);
            if(bag>0)
            {
                uint prior=Mix(seed^(uint)(bag-1)^(uint)((bag-1)>>32));int last=(3*(int)((prior&1)*2+1)+(int)((prior>>1)&3))&3;
                if(last==offset){if(index==0)index=1;else if(index==1)index=0;}
            }
            return (index*odd+offset)&3;
        }
        public static bool ClipValid(float length)=>Finite(length)&&length>0&&length<=3;
        public static bool StepValid(float speed,bool continuous)=>continuous&&Finite(speed)&&speed>.05f&&speed<=12f;
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Authored world/Y-up support planes and measured player feet; never camera bob or input direction.
// Array Formats: Scalar scope/family values,72authored support rows and four finite clip variants per family.
// Dependencies/Pointers: Existing immutable layout/tavern/dressing metadata is authority; new clips never make a stone floor wooden or change movement/physics.
