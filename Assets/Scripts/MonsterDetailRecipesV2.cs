using System;

namespace OdysseyOracle.Art
{
    // # ARCHITECTURE SNAPSHOT
    // Original, campaign-neutral cold geometry recipes. Unit boxes occupy an
    // existing source box's [-.5,.5] frame; they cannot move a pivot or add a
    // renderer, material, light, collider, actor, clock or animation owner.
    // Runtime/export adapters own scale, borrowed palette, meshes and lifecycle.
    public enum MonsterDetailStyleV2 { GuardianGolem, Stalker, Shade, Spider }
    public enum MonsterDetailCutV2 { SteppedEdges, FrontRecesses, DorsalRecesses }

    public readonly struct MonsterDetailBoxV2
    {
        public readonly float X,Y,Z,Width,Height,Depth;
        public MonsterDetailBoxV2(float x,float y,float z,float width,float height,float depth)
        {X=x;Y=y;Z=z;Width=width;Height=height;Depth=depth;}
    }

    public readonly struct MonsterDetailOperationV2
    {
        public readonly string Part;
        public readonly int BoxIndex,Panels;
        public readonly MonsterDetailCutV2 Cut;
        public readonly float Inset,Gap;
        public MonsterDetailOperationV2(string part,int boxIndex,MonsterDetailCutV2 cut,float inset,int panels=0,float gap=0f)
        {Part=part;BoxIndex=boxIndex;Cut=cut;Inset=inset;Panels=panels;Gap=gap;}
        public int ReplacementBoxes => Cut==MonsterDetailCutV2.SteppedEdges?5:Panels+1;
    }

    public sealed class MonsterDetailRecipeV2
    {
        readonly MonsterDetailOperationV2[] operations;
        public string Name {get;}
        public string Design {get;}
        public int BaseParts {get;}
        public int BaseTriangles {get;}
        public int OperationCount => operations.Length;
        public int Triangles {get;}
        internal MonsterDetailRecipeV2(string name,string design,int baseParts,int baseTriangles,params MonsterDetailOperationV2[] operations)
        {
            Name=name;Design=design;BaseParts=baseParts;BaseTriangles=baseTriangles;this.operations=operations;
            int triangles=baseTriangles;
            for(int i=0;i<operations.Length;i++)triangles+=12*(operations[i].ReplacementBoxes-1);
            Triangles=triangles;
        }
        public MonsterDetailOperationV2 GetOperation(int index) => operations[index];
    }

    public static class MonsterDetailRecipesV2
    {
        public const int StyleCount=4,MaximumReplacementBoxes=5;
        public const int MaximumAdditionalTriangles=240,MaximumTrianglesPerStyle=804;
        public const int MaximumDrawnPartMeshes=52,AdditionalRenderers=0,AdditionalMaterials=0,AdditionalLights=0;
        static MonsterDetailOperationV2 Edge(string part,int index,float inset=.065f) =>
            new MonsterDetailOperationV2(part,index,MonsterDetailCutV2.SteppedEdges,inset);
        static MonsterDetailOperationV2 Front(string part,int index,int panels=3) =>
            new MonsterDetailOperationV2(part,index,MonsterDetailCutV2.FrontRecesses,.13f,panels,.045f);
        static readonly MonsterDetailRecipeV2[] recipes={
            new MonsterDetailRecipeV2("Guardian Golem","Quarried brow, fluted breastplate and stepped stone wrist cuffs.",11,408,
                Edge("Stone brow",0),Front("Torso",0),Edge("Torso",1),
                Edge("Left stone arm",1),Edge("Right stone arm",1)),
            new MonsterDetailRecipeV2("Stalker","Narrow cut brow, divided jaw and scored breastplate; existing long claws remain clear.",11,372,
                Edge("Stone brow",0,.075f),Edge("Stone brow",2,.08f),Front("Torso",0),
                Edge("Left stone arm",3,.07f),Edge("Right stone arm",3,.07f)),
            new MonsterDetailRecipeV2("Shade","Faceted crown and held focus, a stepped spine and recessed shadow grille; open ribs remain open.",13,564,
                Edge("Stone brow",0,.06f),Edge("Torso",0,.055f),Edge("Shade crown",0,.085f),
                Edge("Shade focus",0,.09f),Front("Face shadow",0)),
            new MonsterDetailRecipeV2("Spider","Layered dorsal shell, faceted low head and three jaw courses; all eight planted legs remain exact.",17,528,
                Edge("Torso",0,.055f),
                new MonsterDetailOperationV2("Torso",1,MonsterDetailCutV2.DorsalRecesses,.20f,4,.05f),
                Edge("Stone brow",0,.06f),Front("Stone brow",2))
        };
        public static MonsterDetailRecipeV2 Get(MonsterDetailStyleV2 style)
        {
            int index=(int)style;if(index<0||index>=recipes.Length)throw new ArgumentOutOfRangeException(nameof(style));
            return recipes[index];
        }

        // Caller supplies a reusable five-element scratch array. Cold-only: the
        // operation is complete before the original mesh/footprint cache builds.
        public static int WriteNormalizedBoxes(MonsterDetailOperationV2 op,MonsterDetailBoxV2[] destination,int offset=0)
        {
            int count=op.ReplacementBoxes;
            if(destination==null||offset<0||count<1||count>MaximumReplacementBoxes||offset>destination.Length-count)
                throw new ArgumentException("A complete bounded destination is required.");
            if(float.IsNaN(op.Inset)||float.IsInfinity(op.Inset)||op.Inset<=0f||op.Inset>=.25f)
                throw new ArgumentException("Invalid detail inset.");
            float d=op.Inset;
            if(op.Cut==MonsterDetailCutV2.SteppedEdges)
            {
                destination[offset]=new MonsterDetailBoxV2(0,0,0,1,1-2*d,1-2*d);
                destination[offset+1]=new MonsterDetailBoxV2(0,.5f-d*.5f,0,1-2*d,d,1-2*d);
                destination[offset+2]=new MonsterDetailBoxV2(0,-.5f+d*.5f,0,1-2*d,d,1-2*d);
                destination[offset+3]=new MonsterDetailBoxV2(0,0,.5f-d*.5f,1-2*d,1-2*d,d);
                destination[offset+4]=new MonsterDetailBoxV2(0,0,-.5f+d*.5f,1-2*d,1-2*d,d);
                return count;
            }
            if(op.Panels<2||op.Panels>4||float.IsNaN(op.Gap)||float.IsInfinity(op.Gap)||op.Gap<=0f||op.Gap>=.1f||
                op.Cut!=MonsterDetailCutV2.FrontRecesses&&op.Cut!=MonsterDetailCutV2.DorsalRecesses)
                throw new ArgumentException("Invalid panel detail.");
            float width=(1-(op.Panels-1)*op.Gap)/op.Panels;
            bool top=op.Cut==MonsterDetailCutV2.DorsalRecesses;
            destination[offset]=top?new MonsterDetailBoxV2(0,-d*.5f,0,1,1-d,1):new MonsterDetailBoxV2(0,0,-d*.5f,1,1,1-d);
            for(int i=0;i<op.Panels;i++)
            {
                float along=-.5f+width*.5f+i*(width+op.Gap);
                destination[offset+1+i]=top?new MonsterDetailBoxV2(0,.5f-d*.5f,along,1,d,width):
                    new MonsterDetailBoxV2(along,0,.5f-d*.5f,width,1,d);
            }
            return count;
        }
    }
}
