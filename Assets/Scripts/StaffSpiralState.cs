using System;
namespace OdysseyOracle
{
    public struct StaffSpiralState
    {
        public const float SelectionDuration=.34f,CastDuration=.46f,NovaDuration=.72f;
        public int SelectionMask{get;private set;}
        public int PulseMask{get;private set;}
        public float Age{get;private set;}
        public bool Accepted{get;private set;}
        public float Duration=>Accepted?(PulseMask==15?NovaDuration:CastDuration):SelectionDuration;
        public bool Active=>PulseMask!=0&&Age<Duration;
        public bool AcceptedActive=>Accepted&&Active;
        public float Strength=>Active?(Accepted?(PulseMask==15?1f:.8f):.36f):0f;
        public static bool ValidMask(int mask)
        {
            if(mask<1||mask>15)return false;int rest=mask&(mask-1);
            return mask==15||rest==0||(rest&(rest-1))==0;
        }
        public bool Select(int mask)
        {
            mask=ValidMask(mask)?mask:0;if(mask==SelectionMask)return false;SelectionMask=mask;
            if(AcceptedActive)return false;
            if(mask==0){PulseMask=0;Age=0;Accepted=false;return false;}
            PulseMask=mask;Age=0;Accepted=false;return true;
        }
        // Caller is the existing own accepted-launch feedback, never input/preview.
        public bool Launch(int usedMask)
        {
            if(!ValidMask(usedMask))return false;PulseMask=usedMask;Age=0;Accepted=true;return true;
        }
        public void Step(float dt)
        {
            if(!Active||float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0)return;
            Age=Math.Min(Duration,Age+Math.Min(dt,.05f));
        }
        public void Clear(){SelectionMask=PulseMask=0;Age=0;Accepted=false;}
        public static float Packet(float along,float normalizedAge)
        {
            if(float.IsNaN(along)||float.IsInfinity(along)||float.IsNaN(normalizedAge)||float.IsInfinity(normalizedAge)||along<0||along>1||normalizedAge<=0||normalizedAge>=1)return 0;
            float head=Math.Min(1f,normalizedAge/.78f);
            return Smooth(0,.045f,normalizedAge)*(1-Smooth(.78f,1,normalizedAge))*(1-Smooth(head,head+.035f,along))*Smooth(head-.26f,head-.20f,along);
        }
        static float Smooth(float low,float high,float value){float t=Math.Max(0,Math.Min(1,(value-low)/(high-low)));return t*t*(3-2*t);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: scalar presentation event state only; original staff-local base-to-lens shader path has no gameplay owner.
// Array Formats: two4-bit masks,float age,bool accepted; selection.34s/accepted.46s/Nova.72s visual durations cannot change combat4/5recovery or.5cadence.
// Dependencies/Pointers: existing confirmed-selection and accepted-own-launch hooks only. Three-bit masks rejected; accepted pulse snapshots its used mask and has repaint priority; no Unity objects/heap/transport/save ownership.
