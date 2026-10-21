using System;

namespace OdysseyOracle
{
    // Independently derived closed slab test. Invalid input is blocked, never free.
    public static class XpClearanceMath015
    {
        public const float CenterY=.655f, Half=.581f, Cell=8f;
        public static bool Axis(float a,float b,float lower,float upper,ref float enter,ref float exit)
        {
            if(!Finite(a)||!Finite(b)||!Finite(lower)||!Finite(upper)||lower>upper)return true;
            float direction=b-a;
            if(direction==0f)return a>=lower&&a<=upper;
            float first=(lower-a)/direction,last=(upper-a)/direction;
            if(first>last){float swap=first;first=last;last=swap;}
            enter=Math.Max(enter,first);exit=Math.Min(exit,last);
            return enter<=exit;
        }
        public static bool Blocked(float ax,float ay,float az,float bx,float by,float bz,
            float lx,float ly,float lz,float ux,float uy,float uz)
        {
            if(!Finite(ax)||!Finite(ay)||!Finite(az)||!Finite(bx)||!Finite(by)||!Finite(bz)||
                !Finite(lx)||!Finite(ly)||!Finite(lz)||!Finite(ux)||!Finite(uy)||!Finite(uz)||lx>ux||ly>uy||lz>uz)return true;
            float enter=0f,exit=1f;
            return Axis(ax,bx,lx-Half,ux+Half,ref enter,ref exit)&&
                Axis(ay+CenterY,by+CenterY,ly-Half,uy+Half,ref enter,ref exit)&&
                Axis(az,bz,lz-Half,uz+Half,ref enter,ref exit);
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original world anchors; centerY .655, closed halfextent .581 includes complete max-scale octahedron, bob, floor lift and arithmetic margin.
// Array Formats: Scalar float closed segment/AABB Minkowski slab; no native or Object ownership.
// Dependencies/Pointers: Used directly by production Burst query job and independent CPU numeric tests; touching/inside/invalid are blocked. Floors are included.
