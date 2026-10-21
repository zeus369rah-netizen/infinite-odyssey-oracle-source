using System;

namespace OdysseyOracle
{
    public struct RefugePracticeCycle020
    {
        public const float DelaySeconds=2f;
        public bool Enabled {get;private set;}
        public bool FirstDefeated {get;private set;}
        public bool Repeat {get;private set;}
        public bool Waiting {get;private set;}
        public float Remaining {get;private set;}
        public int TargetId {get;private set;}
        public uint Generation {get;private set;}
        public bool Matches(int id,uint generation)=>Enabled&&id>0&&generation!=0&&id==TargetId&&generation==Generation;
        public void Reset(){this=default;}
        public bool Begin(int id,uint generation)
        {
            Reset();if(id<1||generation==0)return false;
            Enabled=true;TargetId=id;Generation=generation;return true;
        }
        // Result1 is the one original earned defeat;2 is rewardless repeat;0 rejects duplicates/stale leases.
        public int Defeat(int id,uint generation)
        {
            if(!Matches(id,generation)||Waiting)return 0;
            bool first=!FirstDefeated;FirstDefeated=true;Waiting=true;Remaining=DelaySeconds;
            return first?1:2;
        }
        public void Suspend()
        {
            if(!FirstDefeated){Reset();return;}
            Repeat=false;Waiting=true;Remaining=DelaySeconds; // Retain identity high-water across preemption.
        }
        public bool Step(float seconds,bool active)
        {
            if(!Enabled||!FirstDefeated||!Waiting||!active||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)return false;
            Remaining=Math.Max(0f,Remaining-Math.Min(seconds,.1f));return Remaining<=0f;
        }
        public bool BindRepeat(int id,uint generation)
        {
            if(!Enabled||!FirstDefeated||!Waiting||Remaining>0||id<=TargetId||generation==0)return false;
            TargetId=id;Generation=generation;Repeat=true;Waiting=false;return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: bounded active delta seconds and one composite stable-ID/nonwrapping-pool-generation identity; no world API.
// Array Formats: scalar value-state timer/phase/identity only, constant2s delay with.1s bounded steps; no managed/native container or event allocation.
// Dependencies/Pointers: real owner arms only after accepted death; first earned fact persists through Suspend and is cleared by explicit Reset. This type grants no HP, reward, guide, networking or spawn authority.
