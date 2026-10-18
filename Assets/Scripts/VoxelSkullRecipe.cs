using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Original reusable build-time voxel recipe. Pivot is the supported jaw sole atY0;
    // anatomical front is-Z. Scalar part data is independent of Unity object/material ownership.
    // World borrows its existing Bone/DarkStone batches: no per-skull object, collider, light or Update.
    public static class VoxelSkullRecipe
    {
        public readonly struct Part
        {
            public readonly float X,Y,Z,Width,Height,Depth;
            public readonly bool Recess;
            public Part(float x,float y,float z,float width,float height,float depth,bool recess=false)
            {X=x;Y=y;Z=z;Width=width;Height=height;Depth=depth;Recess=recess;}
        }
        public const float Width=.72f,Height=1f,Depth=.67f,Front=-.34f,Rear=.33f;
        public const float CrownCenterZ=.07f,JawCenterZ=-.14f,CrownWidth=.48f,CrownDepth=.42f;
        static readonly Part[] parts={
            new Part(0,.59f,.05f,.66f,.48f,.56f),
            new Part(0,.83f,.06f,.60f,.26f,.50f),
            new Part(0,.97f,.07f,.48f,.06f,.42f),
            new Part(0,.72f,-.245f,.62f,.12f,.12f),
            new Part(-.245f,.40f,-.22f,.23f,.20f,.20f),
            new Part(.245f,.40f,-.22f,.23f,.20f,.20f),
            new Part(-.17f,.58f,-.247f,.18f,.19f,.10f,true),
            new Part(.17f,.58f,-.247f,.18f,.19f,.10f,true),
            new Part(0,.58f,-.29f,.09f,.22f,.07f),
            new Part(0,.39f,-.255f,.075f,.13f,.12f,true),
            new Part(0,.205f,-.265f,.40f,.105f,.055f,true),
            new Part(-.144f,.225f,-.305f,.073f,.11f,.055f),
            new Part(-.048f,.225f,-.305f,.073f,.11f,.055f),
            new Part(.048f,.225f,-.305f,.073f,.11f,.055f),
            new Part(.144f,.225f,-.305f,.073f,.11f,.055f),
            new Part(0,.06f,-.14f,.48f,.12f,.40f),
            new Part(-.22f,.18f,-.04f,.07f,.24f,.24f),
            new Part(.22f,.18f,-.04f,.07f,.24f,.24f),
            new Part(-.12f,.145f,-.31f,.08f,.055f,.055f),
            new Part(0,.145f,-.31f,.08f,.055f,.055f),
            new Part(.12f,.145f,-.31f,.08f,.055f,.055f)
        };
        public static int Count=>parts.Length;
        public static Part Get(int index)
        {if(index<0||index>=parts.Length)throw new ArgumentOutOfRangeException(nameof(index));return parts[index];}
    }
}
