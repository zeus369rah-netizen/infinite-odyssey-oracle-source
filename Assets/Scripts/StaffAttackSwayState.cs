using System;
namespace OdysseyOracle
{
    public struct StaffAttackSwayState
    {
        public const float MaximumRate=1.4f,MaximumPitch=.07853982f,MaximumYaw=.02617994f,MaximumRoll=.04363323f;
        public float Pitch{get;private set;}public float Yaw{get;private set;}public float Roll{get;private set;}
        public float PitchVelocity{get;private set;}public float YawVelocity{get;private set;}public float RollVelocity{get;private set;}
        public float Rate{get;private set;}public float SinceAccepted{get;private set;}
        float targetRate;bool hasAccepted,alternate;
        public float PitchDegrees=>Pitch*57.2957795f;public float YawDegrees=>Yaw*57.2957795f;public float RollDegrees=>Roll*57.2957795f;
        public bool Kick(float strength)
        {
            if(!Finite(strength)||strength<=0)return false;
            EnsureFinite();strength=Math.Min(1.35f,strength);
            targetRate=hasAccepted&&SinceAccepted<=.70f?MaximumRate:1f;SinceAccepted=0;hasAccepted=true;alternate=!alternate;
            float sign=alternate?1f:-1f;
            PitchVelocity=Clamp(PitchVelocity+2.0f*strength*targetRate,3.2f);
            YawVelocity=Clamp(YawVelocity+sign*.55f*strength*targetRate,1.4f);
            RollVelocity=Clamp(RollVelocity-sign*1.05f*strength*targetRate,2.1f);
            // Angles intentionally unchanged at accepted input; only velocity changes.
            return true;
        }
        public void Step(float dt)
        {
            EnsureFinite();if(!Finite(dt)||dt<=0)return;dt=Math.Min(dt,.05f);
            if(hasAccepted)SinceAccepted=Math.Min(10,SinceAccepted+dt);
            if(SinceAccepted>.80f)targetRate=1;
            Rate+=(targetRate-Rate)*(1-(float)Math.Exp(-dt*7));
            float omega=15*Rate;
            Spring(Pitch,PitchVelocity,omega,dt,MaximumPitch,out float pitch,out float pv);Pitch=pitch;PitchVelocity=pv;
            Spring(Yaw,YawVelocity,omega,dt,MaximumYaw,out float yaw,out float yv);Yaw=yaw;YawVelocity=yv;
            Spring(Roll,RollVelocity,omega,dt,MaximumRoll,out float roll,out float rv);Roll=roll;RollVelocity=rv;
        }
        static void Spring(float x,float velocity,float omega,float dt,float maximum,out float next,out float nextVelocity)
        {
            float change=(velocity+omega*x)*dt,decay=(float)Math.Exp(-omega*dt);
            next=(x+change)*decay;nextVelocity=(velocity-omega*change)*decay;
            if(next>maximum){next=maximum;if(nextVelocity>0)nextVelocity=0;}else if(next< -maximum){next= -maximum;if(nextVelocity<0)nextVelocity=0;}
            if(Math.Abs(next)<.000001f&&Math.Abs(nextVelocity)<.000001f){next=nextVelocity=0;}
        }
        void EnsureFinite()
        {
            if(!Finite(Pitch)||!Finite(Yaw)||!Finite(Roll)||!Finite(PitchVelocity)||!Finite(YawVelocity)||!Finite(RollVelocity)||
                !Finite(Rate)||Rate<1||Rate>MaximumRate||!Finite(SinceAccepted)||!Finite(targetRate)||targetRate<1||targetRate>MaximumRate)Clear();
        }
        public void Clear(){Pitch=Yaw=Roll=PitchVelocity=YawVelocity=RollVelocity=SinceAccepted=0;Rate=targetRate=1;hasAccepted=alternate=false;}
        static float Clamp(float value,float maximum)=>Math.Max(-maximum,Math.Min(maximum,value));
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original player staff-local cosmetic angles only, radians in numeric state/degrees only at existing Transform adapter; camera/aim unchanged.
// Array Formats: fixed scalar angles/velocities/elapsed/rate and two booleans; exact critically damped spring; maxpitch4.5/yaw1.5/roll2.5degrees,visualrate1..1.4.
// Dependencies/Pointers: existing accepted-own PulseCast adds velocity without pose snap. Rapid accepted spacing<=.70s raises visual settling rate only; no cast clocks/recovery/damage/input/network ownership, heap or Unity references.
