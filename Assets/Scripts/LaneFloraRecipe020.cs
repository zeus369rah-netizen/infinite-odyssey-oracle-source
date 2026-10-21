using System;

namespace OdysseyOracle
{
    // Cold pure value emission. World/layout retain all placement and resource ownership.
    internal static class LaneFloraRecipe020
    {
        internal const double GrassRadius=.55, BushRadius=.90;
        internal const int MaximumBoxesPerLane=24;

        static int Appearance(int lane)=>lane==0?0:lane==1?7:lane==2?6:lane==3?5:lane==4?3:lane==5?4:lane==6?2:1;
        static void Validate(int lane,VignettePoint root,double yaw,LaneVignetteWriter write)
        {
            if(lane<0||lane>=8||write==null||!Finite(root.X)||!Finite(root.Y)||!Finite(root.Z)||!Finite(yaw))
                throw new ArgumentException("Finite admitted flora descriptor required.");
        }
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        static VignettePoint Position(VignettePoint root,double yaw,double x,double y,double z)
        {
            double a=(90-yaw)*Math.PI/180,dx=Math.Cos(a),dz=Math.Sin(a);
            return new VignettePoint(root.X-dz*x+dx*z,root.Y+y,root.Z+dx*x+dz*z);
        }
        static void Part(int lane,VignettePoint root,double yaw,LaneVignetteWriter write,int tier,
            double x,double y,double z,double w,double h,double d,double totalHeight,double angle=0,bool blossom=false)
        {
            int theme=Appearance(lane);
            LaneVignetteRecipes.TreePalette(theme,tier,blossom,out double r,out double g,out double b,out double glow);
            write(new LaneVignettePiece(lane,VignettePrimitive.CanopyBox,LaneVignetteRecipes.PlantSurface(theme),
                Position(root,yaw,x,y,z),new VignettePoint(w,h,d),yaw+angle,root,totalHeight,r,g,b,glow*.25));
        }
        static void Base(int lane,VignettePoint root,double yaw,LaneVignetteWriter write,VignetteSurface surface,
            double width,double height,double depth)
        {
            write(new LaneVignettePiece(lane,VignettePrimitive.Box,surface,
                Position(root,yaw,0,height*.5,0),new VignettePoint(width,height,depth),yaw,root));
        }

        internal static void WriteGrass(int lane,int index,VignettePoint root,double yaw,LaneVignetteWriter write)
        {
            Validate(lane,root,yaw,write);
            if(index<0||index>=12)throw new ArgumentOutOfRangeException(nameof(index));
            int family=lane/2;
            double width=family==0?.13:family==1?.16:family==2?.10:.08;
            double depth=family==0?.15:family==1?.18:family==2?.11:.09;
            for(int blade=0;blade<2;blade++)
            {
                double height=.45+blade*.30;
                Part(lane,root,yaw,write,(index+blade)%3,(blade-.5)*.24,height*.5,(blade-.5)*.08,
                    width,height,depth,height,blade==0?-24:24,lane==2&&index==3);
            }
        }

        internal static void WriteBush(int lane,double height,VignettePoint root,double yaw,LaneVignetteWriter write)
        {
            Validate(lane,root,yaw,write);
            if(!Finite(height)||height<.45||height>.85)throw new ArgumentOutOfRangeException(nameof(height));
            switch(lane/2)
            {
                case 0: // Fire: compact charred foot, red body and asymmetric ember tips.
                    Base(lane,root,yaw,write,VignetteSurface.DarkStone,.46,height*.22,.46);
                    Part(lane,root,yaw,write,0,0,height*.40,0,.64,height*.48,.56,height);
                    Part(lane,root,yaw,write,1,-.20,height*.75,.03,.24,height*.50,.28,height);
                    Part(lane,root,yaw,write,2,.18,height*.70,-.05,.25,height*.44,.26,height);
                    break;
                case 1: // Earth: broad rooted mass with two supported overlapping leaf layers.
                    Base(lane,root,yaw,write,VignetteSurface.Wood,.56,height*.16,.56);
                    Part(lane,root,yaw,write,0,0,height*.37,0,.76,height*.50,.65,height);
                    Part(lane,root,yaw,write,1,-.10,height*.73,.02,.62,height*.54,.50,height);
                    Part(lane,root,yaw,write,2,.28,height*.43,-.06,.38,height*.50,.42,height);
                    break;
                case 2: // Water: rounded blue body and upright paired cyan reeds.
                    Base(lane,root,yaw,write,VignetteSurface.DarkStone,.54,height*.18,.54);
                    Part(lane,root,yaw,write,0,0,height*.42,0,.68,height*.56,.56,height);
                    Part(lane,root,yaw,write,1,-.19,height*.74,.02,.25,height*.52,.30,height);
                    Part(lane,root,yaw,write,2,.18,height*.66,-.08,.30,height*.52,.32,height);
                    break;
                default: // Air: pale stone foot and narrow separated ivory fronds.
                    Base(lane,root,yaw,write,VignetteSurface.PaleStone,.50,height*.16,.50);
                    Part(lane,root,yaw,write,0,0,height*.38,0,.70,height*.52,.38,height);
                    Part(lane,root,yaw,write,1,-.23,height*.75,.02,.22,height*.50,.23,height);
                    Part(lane,root,yaw,write,2,.23,height*.66,-.03,.22,height*.48,.23,height);
                    break;
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing road-local metre roots/yaw transformed to Y-up/XZ world pieces; grass .45/.75m and bushes .45..85m.
// Array Formats: Two grass boxes or four bush boxes in the existing LaneVignettePiece value stream; shared palette/fallback and GPU wind anchors, no native/store owner added.
// Dependencies/Pointers: Cold pure emission only after layout reserves the full corner+wind envelope. World/renderer own mesh/material lifetime; no plant Objects, colliders, lights, frame callbacks or per-frame allocations. Tree3x and gameplay authority untouched.
