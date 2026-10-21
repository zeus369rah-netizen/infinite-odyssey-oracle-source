using System;
using OdysseyOracle.CampaignNpcs;

namespace OdysseyOracle
{
    public struct NibwickRotation020 {public float X,Y,Z,W;}
    public struct NibwickLegPose020
    {
        public NpcVector Hip,Knee,Ankle;
        public NibwickRotation020 Upper,Lower,Boot;
        public byte Valid;
    }
    public static class NibwickRigidLeg020
    {
        public const float UpperLength=.21f,LowerLength=.21f;
        public static readonly float OriginalUpperLength=(float)Math.Sqrt(.14f*.14f+.025f*.025f);
        public static readonly float UpperScaleY=UpperLength/OriginalUpperLength,LowerScaleY=LowerLength/.095f;
        public static NpcVector Rotate(NibwickRotation020 q,NpcVector v)
        {
            var a=new NpcVector(q.X,q.Y,q.Z);var t=NpcVector.Cross(a,v)*2;
            return v+t*q.W+NpcVector.Cross(a,t);
        }
        public static NibwickRotation020 Align(NpcVector a,NpcVector b)
        {
            a=a.Unit;b=b.Unit;var c=NpcVector.Cross(a,b);float w=1+NpcVector.Dot(a,b),n=(float)Math.Sqrt(c.Square+w*w);
            return n>1e-6f?new NibwickRotation020{X=c.X/n,Y=c.Y/n,Z=c.Z/n,W=w/n}:new NibwickRotation020{W=1};
        }
        public static NibwickRotation020 Yaw(float degrees)
        {float a=degrees*.008726646259971648f;return new NibwickRotation020{Y=(float)Math.Sin(a),W=(float)Math.Cos(a)};}
        public static NibwickLegPose020 Solve(NpcVector root,float yaw,bool right,NpcVector foot,float footYaw)
        {
            var basis=Yaw(yaw);var boot=Yaw(footYaw);
            var hip=root+Rotate(basis,new NpcVector(right?.09f:-.09f,.335f,0));
            var ankle=foot-Rotate(boot,new NpcVector(0,-.10f,.04f));
            var delta=ankle-hip;float d=(float)Math.Sqrt(delta.Square);
            float a=UpperLength,b=LowerLength;
            if(!NibwickTourMath020.Finite(d)||d<=Math.Abs(a-b)+.0001f||d>=a+b-.0001f)return new NibwickLegPose020{Hip=hip,Ankle=ankle};
            var axis=delta*(1/d);var pole=Rotate(basis,new NpcVector(right?1:-1,0,0));
            var bend=pole-axis*NpcVector.Dot(pole,axis);
            if(bend.Square<1e-8f){pole=new NpcVector(0,1,0);bend=pole-axis*NpcVector.Dot(pole,axis);}
            if(bend.Square<1e-8f)return default;
            float along=(a*a-b*b+d*d)/(2*d),height=(float)Math.Sqrt(Math.Max(0,a*a-along*along));
            var knee=hip+axis*along+bend.Unit*height;
            return new NibwickLegPose020{Hip=hip,Knee=knee,Ankle=ankle,Upper=Align(new NpcVector(0,-1,0),knee-hip),Lower=Align(new NpcVector(0,-1,0),ankle-knee),Boot=boot,Valid=1};
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: World hip and planted/swing boot sole -> standard analytic two-link rigid knee-out solve; boots retain independent world yaw/size and flat soles.
// Array Formats: Two bounded value joint solutions, equal.21m links and fixed longitudinal scales; independent flat boots retain their original size. No skeleton, skin, Object API or heap allocation.
// Dependencies/Pointers: Original13-part recipe partitions into17 rigid groups with identical triangle/vertex totals. Unreachable/nonfinite joints fail closed rather than dragging/teleporting a foot; native appearance remains a root gate.
