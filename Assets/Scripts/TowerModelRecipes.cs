using System;
using System.Collections.Generic;

namespace OdysseyOracle
{
    public enum TowerModelGroup { Foundation, Head, Recoil, Core, Rotor, Bolt }
    public enum TowerModelPrimitive { Box, BeveledPrism, Crystal }
    public readonly struct TowerModelVector
    {
        public readonly double X,Y,Z;
        public TowerModelVector(double x,double y,double z){X=x;Y=y;Z=z;}
    }
    public readonly struct TowerModelPiece
    {
        public readonly TowerModelGroup Group;
        public readonly TowerModelPrimitive Primitive;
        public readonly int Palette;
        public readonly TowerModelVector Center,Size,Euler;
        public readonly double Taper;
        public TowerModelPiece(TowerModelGroup group,TowerModelPrimitive primitive,int palette,
            TowerModelVector center,TowerModelVector size,TowerModelVector euler,double taper=1)
        {Group=group;Primitive=primitive;Palette=palette;Center=center;Size=size;Euler=euler;Taper=taper;}
        public int Triangles=>Primitive==TowerModelPrimitive.Box?12:Primitive==TowerModelPrimitive.Crystal?16:32;
    }
    public sealed class TowerModelRecipe
    {
        readonly List<TowerModelPiece> pieces;
        public readonly int Type,Tier,Triangles;
        public readonly string ArtName;
        public readonly TowerModelVector HeadPivot,Muzzle,CoreSocket,SoundSocket;
        internal TowerModelRecipe(int type,int tier,string name,List<TowerModelPiece> geometry,
            TowerModelVector pivot,TowerModelVector muzzle,TowerModelVector core)
        {
            Type=type;Tier=tier;ArtName=name;pieces=geometry;HeadPivot=pivot;Muzzle=muzzle;CoreSocket=core;SoundSocket=muzzle;
            int triangles=0;foreach(var p in pieces)triangles+=p.Triangles;Triangles=triangles;
        }
        public int Count=>pieces.Count;
        public TowerModelPiece Piece(int index)=>pieces[index];
    }

    // # ARCHITECTURE SNAPSHOT
    // Pure original presentation recipes. IDs0/1/2/3 match Fire/Water/Earth/Air.
    // Forge/Director own damage, cadence, rank acceptance and currency; no gameplay numbers here.
    // The separate World platform owns its three courses; localY0 is its canonical top centre.
    public static class TowerModelRecipes
    {
        public const int Types=4,Tiers=3,GroupCount=6,MaximumTriangles=2500;
        public const double MaximumSoleWidth=1.6,MaximumUpperDiameter=2.0;
        static readonly string[] ArtNames={"Furnace Fire","Shell Water","Stonebow Earth","Sky Relay Air"};
        public static TowerModelRecipe Build(int type,int tier)
        {
            if(type<0||type>=Types||tier<1||tier>Tiers)throw new ArgumentOutOfRangeException();
            var c=new RecipeBuilder(type,tier);c.Foundation();
            if(type==0)c.Furnace();else if(type==1)c.Shell();else if(type==2)c.Stonebow();else c.SkyRelay();
            var result=c.Finish(ArtNames[type]);
            if(result.Triangles>MaximumTriangles)throw new InvalidOperationException("Tower triangle cap exceeded.");
            return result;
        }
        sealed class RecipeBuilder
        {
            readonly List<TowerModelPiece> pieces=new List<TowerModelPiece>(120);
            readonly int type,tier;
            readonly double head;
            TowerModelVector muzzle,core;
            public RecipeBuilder(int type,int tier){this.type=type;this.tier=tier;head=2.75+(tier-1)*.4;}
            void Part(TowerModelGroup group,TowerModelPrimitive kind,int palette,double x,double y,double z,
                double w,double h,double d,double pitch=0,double yaw=0,double roll=0,double taper=1)
            {
                if(palette<0||palette>=8||w<=0||h<=0||d<=0)throw new InvalidOperationException("Invalid original voxel piece.");
                pieces.Add(new TowerModelPiece(group,kind,palette,new TowerModelVector(x,y,z),new TowerModelVector(w,h,d),new TowerModelVector(pitch,yaw,roll),taper));
            }
            void Box(TowerModelGroup group,int p,double x,double y,double z,double w,double h,double d,double pitch=0,double yaw=0,double roll=0)
                =>Part(group,TowerModelPrimitive.Box,p,x,y,z,w,h,d,pitch,yaw,roll);
            void Facet(TowerModelGroup group,int p,double x,double y,double z,double w,double h,double d,double taper=1)
                =>Part(group,TowerModelPrimitive.BeveledPrism,p,x,y,z,w,h,d,taper:taper);
            void Gem(TowerModelGroup group,int p,double x,double y,double z,double w,double h,double d)
                =>Part(group,TowerModelPrimitive.Crystal,p,x,y,z,w,h,d);
            void Line(TowerModelGroup group,int p,TowerModelVector a,TowerModelVector b,double width)
            {
                double x=b.X-a.X,y=b.Y-a.Y,z=b.Z-a.Z,flat=Math.Sqrt(x*x+z*z),length=Math.Sqrt(flat*flat+y*y);
                Box(group,p,(a.X+b.X)*.5,(a.Y+b.Y)*.5,(a.Z+b.Z)*.5,width,width,length,
                    -Math.Atan2(y,flat)*180/Math.PI,Math.Atan2(x,z)*180/Math.PI);
            }
            public void Foundation()
            {
                var g=TowerModelGroup.Foundation;
                Facet(g,0,0,.12,0,1.56,.24,1.56,.93);
                Facet(g,1,0,.335,0,1.36,.19,1.36);
                Facet(g,2,0,.54,0,1.08,.22,1.08,.92);
                Facet(g,0,0,1.10,0,.89,.94,.89,.90);
                Facet(g,3,0,.785,0,1.03,.065,1.03);
                Facet(g,3,0,1.515,0,1.01,.075,1.01);
                Facet(g,1,0,1.70,0,1.16,.22,1.16);
                Facet(g,2,0,(1.81+head-.12)*.5,0,.73,head-.12-1.81,.73,.90);
                Facet(g,3,0,head-.07,0,1.10,.14,1.10);
                for(int side=-1;side<=1;side+=2)
                for(int depth=-1;depth<=1;depth+=2)
                {
                    Box(g,4,side*.57,.89,depth*.57,.16,.88,.16);
                    Facet(g,3,side*.57,1.36,depth*.57,.24,.12,.24);
                    Box(g,1,side*.47,1.98,depth*.47,.15,.50,.15);
                }
                // Inset face and distinct original relief, not a label or a borrowed emblem.
                Box(g,0,0,1.17,.465,.65,.53,.075);
                Box(g,3,0,.895,.485,.69,.055,.075);
                Box(g,3,0,1.445,.485,.69,.055,.075);
                Sigil(g,1.16,.515);
                for(int band=0;band<tier;band++)
                {
                    Box(g,3,-.29+band*.29,1.82,.485,.16,.09,.065);
                    Box(g,7,-.29+band*.29,1.82,.526,.065,.035,.025);
                }
                if(tier>=2)
                    for(int side=-1;side<=1;side+=2)
                    {
                        Box(g,1,side*.60,1.92,0,.15,1.48,.36);
                        Box(g,3,side*.60,2.68,0,.22,.08,.43);
                        Box(g,6,side*.685,2.04,0,.035,.67,.18);
                    }
                if(tier==3)
                    for(int depth=-1;depth<=1;depth+=2)
                    {
                        Box(g,4,0,2.17,depth*.59,.41,1.58,.15);
                        Facet(g,3,0,2.99,depth*.59,.49,.10,.24);
                        Box(g,7,0,2.31,depth*.675,.18,.44,.025);
                    }
            }
            void Sigil(TowerModelGroup group,double y,double z)
            {
                if(type==0)
                {
                    for(int i=0;i<3;i++)Box(group,7,(i-1)*.16,y+(i==1?.065:0),z,.12,i==1?.37:.23,.045,roll:(i-1)*-12);
                }
                else if(type==1)
                {Gem(group,7,0,y+.04,z,.30,.41,.055);Box(group,6,-.17,y-.15,z,.13,.05,.045);Box(group,6,.15,y-.12,z,.14,.05,.045);}
                else if(type==2)
                {
                    Box(group,6,0,y-.17,z,.51,.065,.045);
                    Box(group,7,-.13,y-.005,z,.12,.33,.045,roll:-28);Box(group,7,.06,y+.025,z,.12,.40,.045,roll:28);
                    Box(group,6,.23,y-.08,z,.10,.20,.045,roll:25);
                }
                else
                    for(int ray=0;ray<4;ray++)
                    {
                        double a=ray*Math.PI*.5;Box(group,7,Math.Cos(a)*.15,y+Math.Sin(a)*.15,z,.25,.055,.045,roll:ray*90+25);
                    }
            }
            public void Furnace()
            {
                var h=TowerModelGroup.Head;var r=TowerModelGroup.Recoil;var c=TowerModelGroup.Core;
                Facet(h,0,0,head+.24,0,1.42,.62,1.22,.90);
                Facet(h,3,0,head+.59,0,1.49,.09,1.27);
                for(int side=-1;side<=1;side+=2)
                {
                    Box(h,4,side*.54,head+1.07,-.30,.20,1.00,.40);
                    Facet(h,3,side*.54,head+1.60,-.30,.30,.10,.48);
                    Box(h,0,side*.54,head+1.35,-.30,.25,.50,.43);
                    for(int slit=0;slit<3;slit++)Box(h,7,side*.674,head+.83+slit*.17,-.25,.022,.08,.20);
                    Box(r,3,side*.27,head+.27,.61,.26,.30,.46);
                    Box(r,4,side*.27,head+.27,.855,.19,.20,.027);
                    Box(r,7,side*.27,head+.27,.874,.10,.11,.018);
                }
                for(int bar=0;bar<5;bar++)Box(r,4,(bar-2)*.17,head+.45,.65,.055,.59,.075);
                Gem(c,7,0,head+.99,0,.54,.97,.54);
                for(int flame=0;flame<3;flame++)
                    Box(c,flame==1?7:6,(flame-1)*.19,head+1.37+(flame==1?.10:0),.04,.16,flame==1?.49:.31,.21,roll:(flame-1)*-9);
                if(tier>=2)
                    for(int side=-1;side<=1;side+=2)
                    {Box(h,3,side*.72,head+1.25,.02,.16,.49,.69);Box(h,0,side*.69,head+.83,.02,.20,.24,.72);}
                if(tier==3)
                {
                    Facet(h,3,0,head+1.80,-.08,1.70,.14,1.13);
                    for(int side=-1;side<=1;side+=2)for(int depth=-1;depth<=1;depth+=2)
                        Box(h,0,side*.54,head+2.02,depth*.34,.18,.32,.20);
                }
                muzzle=new TowerModelVector(0,head+.27,.895);core=new TowerModelVector(0,head+.99,0);
            }
            public void Shell()
            {
                var h=TowerModelGroup.Head;var r=TowerModelGroup.Recoil;var c=TowerModelGroup.Core;
                Facet(h,1,0,head+.18,0,1.62,.36,1.28,.82);
                Facet(h,3,0,head+.40,0,1.53,.09,1.20);
                for(int rib=-2;rib<=2;rib++)
                {
                    double height=1.38-Math.Abs(rib)*.29;
                    Box(h,1,rib*.30,head+.42+height*.5,-.46,.20,height,.34);
                    Box(h,3,rib*.30,head+.45+height,-.46,.25,.075,.39);
                    Box(h,6,rib*.30,head+.47+height*.45,-.267,.075,height*.64,.035);
                }
                Gem(c,7,0,head+.99,.04,.49,.91,.49);
                for(int spill=-1;spill<=1;spill++)
                {
                    Box(r,6,spill*.30,head+.10,.63,.16,.50,.075);
                    Box(c,7,spill*.30,head+.44,.674,.12,.13,.04);
                }
                for(int side=-1;side<=1;side+=2)
                {Box(h,3,side*.56,head+.65,.25,.13,.42,.30);Box(h,1,side*.56,head+.91,.25,.24,.12,.34);}
                if(tier>=2)
                    for(int side=-1;side<=1;side+=2)
                    {Box(h,1,side*.755,head+1.05,-.24,.17,1.62,.34);Box(h,3,side*.755,head+1.90,-.24,.23,.09,.39);}
                if(tier==3)
                {
                    Facet(h,1,0,head+2.01,-.26,1.75,.19,.98);
                    for(int step=-1;step<=1;step++)
                        Gem(h,step==0?7:6,step*.43,head+2.04,-.20,.16,.40-Math.Abs(step)*.07,.16);
                }
                muzzle=new TowerModelVector(0,head+.47,.72);core=new TowerModelVector(0,head+.99,.04);
            }
            public void Stonebow()
            {
                var h=TowerModelGroup.Head;var r=TowerModelGroup.Recoil;var b=TowerModelGroup.Bolt;
                Facet(h,0,0,head+.34,0,1.12,.67,1.05,.86);
                Facet(h,3,0,head+.73,0,1.22,.12,1.11);
                Box(r,5,0,head+1.08,-.02,.41,.26,1.54);
                Box(r,4,0,head+1.245,.03,.28,.07,1.43);
                for(int side=-1;side<=1;side+=2)
                {
                    for(int segment=0;segment<3;segment++)
                    {
                        double x=side*(.30+segment*.245),z=.18-segment*.13;
                        Box(h,segment%2==0?1:4,x,head+1.17+segment*.045,z,.30,.20,.24,roll:side*(7+segment*4));
                        Box(h,3,x,head+1.295+segment*.045,z,.18,.05,.28);
                    }
                    Line(r,4,new TowerModelVector(side*.87,head+1.27,-.09),new TowerModelVector(0,head+1.27,-.61),.025);
                    Box(h,1,side*.50,head+.90,-.25,.20,.37,.36);
                }
                Box(b,5,0,head+1.29,.02,.06,.065,1.26);
                Part(b,TowerModelPrimitive.Crystal,7,0,head+1.29,.77,.17,.27,.17,pitch:90);
                for(int side=-1;side<=1;side+=2)Box(b,6,side*.08,head+1.29,-.51,.15,.035,.19,yaw:side*15);
                Gem(TowerModelGroup.Core,7,0,head+.45,.03,.34,.49,.34);
                Box(h,1,0,head+1.53,-.34,.42,.25,.35);
                Box(h,3,0,head+1.69,-.34,.49,.07,.41);
                if(tier>=2)
                    for(int side=-1;side<=1;side+=2)
                    {
                        Box(h,1,side*.70,head+1.02,-.17,.21,.20,.51);
                        Box(h,6,side*.54,head+1.47,.12,.23,.075,.22);
                        Box(r,3,side*.23,head+1.06,-.19,.055,.34,.86);
                    }
                if(tier==3)
                {
                    Facet(h,1,0,head+1.87,-.34,.73,.28,.42,.70);
                    Gem(h,7,0,head+2.06,-.34,.27,.28,.20);
                    for(int side=-1;side<=1;side+=2)Box(h,3,side*.36,head+1.75,-.34,.16,.39,.17,roll:side*18);
                }
                muzzle=new TowerModelVector(0,head+1.29,.91);core=new TowerModelVector(0,head+.45,.03);
            }
            public void SkyRelay()
            {
                var h=TowerModelGroup.Head;var o=TowerModelGroup.Rotor;
                Facet(h,1,0,head+.23,0,1.24,.46,1.24,.78);
                Facet(h,3,0,head+.51,0,1.31,.10,1.31);
                for(int side=-1;side<=1;side+=2)
                {
                    Box(h,1,side*.56,head+.98,0,.17,.78,.32);
                    Box(h,3,side*.49,head+1.38,0,.33,.09,.38);
                    Box(h,6,side*.652,head+1.00,0,.025,.46,.16);
                }
                Gem(TowerModelGroup.Core,7,0,head+1.02,0,.40,.94,.40);
                for(int vane=0;vane<4;vane++)
                {
                    double a=vane*Math.PI*.5;
                    Box(o,vane%2==0?1:3,Math.Sin(a)*.54,head+.90,Math.Cos(a)*.54,.17,.12,.66,yaw:vane*90);
                    Box(o,6,Math.Sin(a)*.77,head+1.10,Math.Cos(a)*.77,.13,.33,.19,yaw:vane*90);
                }
                Gem(h,6,0,head+1.56,0,.21,.36,.21);
                if(tier>=2)
                    for(int side=-1;side<=1;side+=2)
                    {
                        Box(o,3,side*.54,head+1.53,0,.10,.08,1.12);
                        Box(o,1,0,head+1.53,side*.54,1.12,.08,.10);
                    }
                if(tier==3)
                {
                    for(int vane=0;vane<4;vane++)
                    {
                        double a=vane*Math.PI*.5;
                        Box(o,1,Math.Sin(a)*.55,head+1.83,Math.Cos(a)*.55,.15,.45,.62,yaw:vane*90);
                    }
                    Gem(h,7,0,head+2.13,0,.22,.38,.22);
                }
                muzzle=new TowerModelVector(0,head+1.02,.75);core=new TowerModelVector(0,head+1.02,0);
            }
            public TowerModelRecipe Finish(string name)=>new TowerModelRecipe(type,tier,name,pieces,
                new TowerModelVector(0,head,0),muzzle,core);
        }
    }
}
