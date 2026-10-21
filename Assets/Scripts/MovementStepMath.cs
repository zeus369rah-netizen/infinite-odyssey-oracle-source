using System;

namespace OdysseyOracle
{
    public struct MovementSweepStep
    {
        public float UnitX,UnitY,UnitZ,Distance;
    }
    public static class MovementStepMath
    {
        public static bool TryNormalize(float x,float y,float z,out MovementSweepStep step)
        {
            step=default;
            if(!Finite(x)||!Finite(y)||!Finite(z))return false;
            double distance=Math.Sqrt((double)x*x+(double)y*y+(double)z*z);
            if(distance<=0d||distance>float.MaxValue)return false;
            step=new MovementSweepStep{UnitX=(float)(x/distance),UnitY=(float)(y/distance),UnitZ=(float)(z/distance),Distance=(float)distance};
            return step.Distance>0f;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World-metre displacement to explicit unit direction and finite positive distance; no scene/Object API or collision decision.
// Array Formats: Blittable16byte MovementSweepStep value only, no arrays, allocation, native storage or persistent state.
// Dependencies/Pointers: Existing CanMove physics/filter/support owner consumes the record. Double intermediate avoids squared-float underflow; zero/nonfinite/unrepresentable lengths fail closed without a frame-distance floor.
