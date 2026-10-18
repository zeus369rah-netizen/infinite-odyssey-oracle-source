using System;

namespace OdysseyOracle
{
    public enum TowerPadDisplayState { Available, Selected, Occupied, Locked }

    // # ARCHITECTURE SNAPSHOT
    // Pure geometry/presentation policy. Metres, pad-local XZ; pad origin is the canonical top centre.
    // World owns meshes/colliders, Builder owns input/hover, Director remains the only production authority.
    // Capacity24 bounds presentation IDs for future8-lane layouts; current networking retains its6-pad cap.
    public static class TowerPadPolicy
    {
        public const int MaximumPads=24,CourseCount=3;
        public const float CourseHeight=.12f,TopHeight=.36f,HighlightHeight=.045f;
        public const float BottomWidth=2.5f,MiddleWidth=2.1f,TopWidth=1.7f;
        public static float CourseWidth(int course)
        {
            if(course<0||course>=CourseCount)throw new ArgumentOutOfRangeException(nameof(course));
            return course==0?BottomWidth:course==1?MiddleWidth:TopWidth;
        }
        public static float CourseCenterBelowTop(int course)
        {
            if(course<0||course>=CourseCount)throw new ArgumentOutOfRangeException(nameof(course));
            return (course+.5f)*CourseHeight-TopHeight;
        }
        public static float SurfaceAboveFloor(float x,float z)
        {
            float edge=Math.Max(Math.Abs(x),Math.Abs(z));
            if(!Finite(edge))return 0;
            return edge<=TopWidth*.5f?TopHeight:edge<=MiddleWidth*.5f?CourseHeight*2:edge<=BottomWidth*.5f?CourseHeight:0;
        }
        public static bool IsPlatformHit(float x,float yBelowTop,float z)
        {
            return SurfaceAboveFloor(x,z)>0 && Finite(yBelowTop) && yBelowTop>=-TopHeight-.025f && yBelowTop<=.055f;
        }
        public static bool IsStandingOver(float x,float yBelowTop,float z)
        {
            float surface=SurfaceAboveFloor(x,z);
            return surface>0 && Finite(yBelowTop) && Math.Abs(yBelowTop-(surface-TopHeight))<=.12f;
        }
        public static bool MatchesTop(float suppliedY,float canonicalY)
            => Finite(suppliedY)&&Finite(canonicalY)&&Math.Abs(suppliedY-canonicalY)<=.025f;
        public static TowerPadDisplayState State(bool active,bool occupied,bool ready,bool hasTokens,bool capacity,bool inRange,bool selected)
        {
            if(occupied)return TowerPadDisplayState.Occupied;
            if(!active||!ready||!hasTokens||!capacity||!inRange)return TowerPadDisplayState.Locked;
            return selected?TowerPadDisplayState.Selected:TowerPadDisplayState.Available;
        }
        public static bool CanOpen(TowerPadDisplayState state,bool inputEligible,bool validatedGround)
            => inputEligible&&validatedGround&&(state==TowerPadDisplayState.Available||state==TowerPadDisplayState.Selected);
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
