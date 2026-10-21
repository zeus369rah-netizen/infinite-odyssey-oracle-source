using System;

namespace OdysseyOracle
{
    public struct CauldronAudioMix009
    {
        public float Bubbles, Stir, BubblePitch, Hearth;
    }

    // Scalar presentation math only. No spatial scan or sound/engine ownership.
    public static class CauldronAudioMath009
    {
        // Root imports peak-bounded linear derivatives, preserving raw originals:
        // bubble target-32dBFS; stir target-34dBFS limited by.85peak. Runtime mix
        // stays <=1 per source; listening/musical balance remain native gates.
        public const float Range = 11f, BubbleGain = .3f, StirGain = .6f, HearthGain = .022f;
        public static bool AllowsPause(bool paused,bool acceptedRaykonMentorVisible) => !paused||acceptedRaykonMentorVisible;
        public static float Unit(float value) => float.IsNaN(value)||float.IsInfinity(value)?0f:Math.Max(0f,Math.Min(1f,value));
        public static CauldronAudioMix009 Target(bool audible, float talk, float fuel)
        {
            float stir=1f-Unit(talk);
            return new CauldronAudioMix009 {
                Bubbles=audible?BubbleGain*(.72f+.28f*stir):0f,
                Stir=audible?StirGain*stir:0f,
                BubblePitch=.94f+.10f*stir,
                Hearth=audible?HearthGain*Unit(fuel):0f
            };
        }
        public static float Approach(float current,float target,float dt,float seconds)
        {
            if(float.IsNaN(current)||float.IsInfinity(current))current=0f;
            if(float.IsNaN(target)||float.IsInfinity(target))target=0f;
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0f)return current;
            dt=Math.Min(dt,.1f);seconds=Math.Max(.02f,seconds);
            return current+(target-current)*(1f-(float)Math.Exp(-dt/seconds));
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Scalar presentation gains/pitch only; no geometry or spatial query.
// Array Formats: Four-float16B mix value; finite clamped scalar math, no arrays/native allocation.
// Dependencies/Pointers: Audio owner supplies actual TalkBlend and authoritative room fuel; math never plays audio, modifies gameplay or allocates engine Objects.

