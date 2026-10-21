using Unity.Mathematics;
namespace OdysseyOracle
{
    public static class AcceptedImpactRules
    {
        public static bool Point(float3 p)=>math.all(math.isfinite(p))&&math.abs(p.x)<=2048f&&math.abs(p.y)<=256f&&math.abs(p.z)<=2048f;
        public static bool TryMark(ref uint bits,int ordinal,float3 origin,float3 point)
        {
            if((bits&~7u)!=0||ordinal<0||ordinal>2||!Point(origin)||!Point(point))return false;
            float squared=math.distancesq(origin,point);uint bit=1u<<ordinal;
            if(!math.isfinite(squared)||squared>400f||(bits&bit)!=0)return false;
            bits|=bit;return true;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: bounded actual WorldXZ/Y-up accepted-origin/contact values;20m maximum flight distance, never a new collision/damage authority.
// Array Formats: one caller-owned uint with three independent ordinal bits; no allocation, Object API, NativeArray ownership or runtime search.
// Dependencies/Pointers: existing authenticated run/epoch/caster/accepted-shot/expiry checks precede this pure math; no wrap/replay or unknown receipt is admitted here alone.
