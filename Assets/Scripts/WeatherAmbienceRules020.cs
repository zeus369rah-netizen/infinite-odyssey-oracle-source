// PSEUDOCODE: validate exact scalar scope/time; gate storm audio; fade bounded envelopes; permute all256 four-take codes without allocating a playlist.
using System;

namespace OdysseyOracle
{
    public struct WeatherAmbienceScope020
    {
        public ulong Session,World,Root,Epoch;
        public uint Generation,Revision;
        public bool Valid => Session!=0&&World!=0&&Root!=0&&Epoch!=0&&Generation!=0&&Revision!=0;
        public bool Same(in WeatherAmbienceScope020 other) => Valid&&other.Valid&&Session==other.Session&&World==other.World&&Root==other.Root&&Epoch==other.Epoch&&Generation==other.Generation&&Revision==other.Revision;
    }

    public static class WeatherAmbienceRules020
    {
        public const float CrossfadeSeconds=3f,FadeSeconds=2f,OpenGain=.14f,ShelterGain=.38f;
        public const double ProbeSeconds=.25d;
        public static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        public static bool Enabled(bool night,bool storm,bool effects,bool enabled,float volume)=>night&&storm&&effects&&enabled&&Finite(volume)&&volume>0f;
        public static bool TryDelta(double before,double now,bool held,out float delta)
        {
            delta=0;
            if(held||!Finite(before)||!Finite(now)||before<0||now<before||now-before>.151d)return false;
            delta=(float)(now-before);return true;
        }
        public static float Fade(float value,bool target,float delta)
        {
            if(!Finite(value)||!Finite(delta)||delta<0||delta>.151f)return 0f;
            value=Math.Max(0,Math.Min(1,value));float step=delta/FadeSeconds;
            return target?Math.Min(1,value+step):Math.Max(0,value-step);
        }
        public static float Approach(float value,float target,float delta,float seconds)
        {
            if(!Finite(target))return 0;
            if(!Finite(value)||!Finite(delta)||delta<0||!Finite(seconds)||seconds<=0)return Math.Max(0,Math.Min(1,target));
            float step=Math.Min(.151f,delta)/seconds;
            return value<target?Math.Min(target,value+step):Math.Max(target,value-step);
        }
        public static float Blend(double clock,double began)
            =>!Finite(clock)||!Finite(began)||clock<began?0f:(float)Math.Min(1d,(clock-began)/CrossfadeSeconds);
        public static float EqualPower(float value)=>Finite(value)?(float)Math.Sqrt(Math.Max(0,Math.Min(1,value))):0;
        public static double NextProbe(double now)=>Finite(now)&&now>=0?now+ProbeSeconds:double.PositiveInfinity;
        public static uint Mix(uint value){value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;return value^(value>>16);}
        // Four-round balanced Feistel over8 bits is bijective for every key; no random object or shuffled array is needed.
        public static byte Permute(int ordinal,uint key)
        {
            uint left=(uint)(ordinal>>4)&15,right=(uint)ordinal&15;
            for(uint round=0;round<4;round++){uint next=left^(Mix(right^(key+round*0x9e3779b9u))&15);left=right;right=next;}
            return (byte)((left<<4)|right);
        }
        public static byte Code(int ordinal,uint seed,ulong bag,int previous)
        {
            uint key=Mix(seed^(uint)bag^(uint)(bag>>32));
            int first=Permute(0,key);int index=ordinal&255;
            if(previous==first){if(index==0)index=1;else if(index==1)index=0;}
            return Permute(index,key);
        }
        public static int Take(byte code,int slot)=>(code>>((slot&3)*2))&3;
        public static bool ValidClip(float seconds,int samples,int frequency)=>Finite(seconds)&&seconds>CrossfadeSeconds*2&&seconds<=180&&samples>0&&frequency>0;
        public static float Shelter(int mask,int blocked)
        {
            // Untested directions remain conservatively sheltered, so admission does not invent open sky.
            int open=0;for(int i=0;i<3;i++)if((mask&(1<<i))!=0&&(blocked&(1<<i))==0)open++;
            return 1f-open/3f;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Scalar current world identity, held atmosphere seconds and finite sequence space.
// Array Formats: Blittable scope; value arithmetic and four-round8bit Feistel permutation.
// Dependencies/Pointers: No Unity Object, physics, storage, clock integration, allocation or audio API; adapters retain authority.
