using System;

namespace OdysseyOracle
{
    public readonly struct MusicCrossfadeGains009
    {
        public readonly float Outgoing,Incoming;
        public MusicCrossfadeGains009(float outgoing,float incoming){Outgoing=outgoing;Incoming=incoming;}
    }
    public static class MusicCrossfadeMath009
    {
        public const float MaximumSeconds=3f,StartupSeconds=3f,TriggerSeconds=3.15f;
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static float Clamp01(float value)=>Math.Max(0,Math.Min(1,value));
        public static bool ValidLength(float length)=>Finite(length)&&length>=.05f;
        public static float Duration(float outgoingRemaining,float incomingLength)
        {
            if(!Finite(outgoingRemaining)||outgoingRemaining<=0||!Finite(incomingLength)||incomingLength<=0)return 0;
            float tail=outgoingRemaining>.08f?outgoingRemaining-.04f:outgoingRemaining*.5f;
            return Math.Max(0,Math.Min(MaximumSeconds,Math.Min(tail,incomingLength*.45f)));
        }
        public static float StartupDuration(float length)=>Finite(length)&&length>0?Math.Min(StartupSeconds,length*.45f):0;
        public static float IntroOffset(float length,bool subsequent,bool enabled)=>ValidLength(length)&&subsequent&&enabled?Math.Min(2.5f,length*.1f):0;
        public static float SampleElapsed(int current,int start,int frequency)
        {return frequency>0?Math.Max(0,(float)(((long)current-start)/(double)frequency)):0;}
        public static float Remaining(int clipSamples,int playedSamples,int frequency)
        {return frequency>0?Math.Max(0,(float)(((long)clipSamples-playedSamples)/(double)frequency)):0;}
        public static MusicCrossfadeGains009 Gains(float elapsed,float duration,bool sameAsset)
        {
            float t=duration>0&&Finite(duration)&&Finite(elapsed)?Clamp01(elapsed/duration):1;
            if(t<=0)return new MusicCrossfadeGains009(1,0);if(t>=1)return new MusicCrossfadeGains009(0,1);
            double angle=t*Math.PI*.5;float a=(float)Math.Cos(angle),b=(float)Math.Sin(angle);
            if(sameAsset){float sum=a+b;a/=sum;b/=sum;}
            return new MusicCrossfadeGains009(a,b);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pure non-spatial actual playback sample positions/frequency and gain/time scalar inputs; no PCM access or wall-clock/global state.
// Array Formats: Two-float gain value,3s maximum duration with outgoing-tail/incoming-remaining bounds and requested2.5s subsequent intro offset; no arrays/native/entity allocation.
// Dependencies/Pointers: Existing two-source GameAudioDirector adapter supplies actual samples. Equal power is mathematical, not measured loudness/seamlessness; same-asset repeats normalize sum to1. Root listens/profiles/native-verifies.
