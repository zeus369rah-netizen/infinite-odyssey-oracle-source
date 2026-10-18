using System;
using System.Runtime.InteropServices;

namespace OdysseyOracle
{
    [StructLayout(LayoutKind.Sequential,Pack=4,Size=28)]
    public readonly struct OracleAvatarBox
    {
        public readonly float X,Y,Z,Width,Height,Depth;
        public readonly int Slot;
        public OracleAvatarBox(int slot,float x,float y,float z,float width,float height,float depth)
        {Slot=slot;X=x;Y=y;Z=z;Width=width;Height=height;Depth=depth;}
    }
    [StructLayout(LayoutKind.Sequential,Pack=1,Size=4)]
    public readonly struct OracleAvatarMeshSlot
    {
        // Bone:0 body,1 left arm,2 right arm,3 wand. Palette:0 robe,1 hat,2 hair,3 gold,4 dark,5 gem,6 paper.
        // Variant:0 common,1 hat style,2 clothing,3 shoulders,4 book visibility.
        public readonly byte Bone,Palette,Variant,Value;
        public OracleAvatarMeshSlot(byte bone,byte palette,byte variant=0,byte value=0)
        {Bone=bone;Palette=palette;Variant=variant;Value=value;}
        public bool Visible(OracleAvatarAppearance appearance)=>Variant==0||Variant==1&&appearance.HatStyle==Value||
            Variant==2&&appearance.ClothingStyle==Value||Variant==3&&appearance.ShoulderStyle==Value||Variant==4&&appearance.BookVisible;
    }
    // Cold presentation recipes reuse the saved avatar's cube, four-point sigil and wrapped wand vocabulary.
    // Numeric box traversal is engine-independent; no GameObject, Mesh, Object or Physics API occurs here.
    public static class OracleAvatarRecipe
    {
        public const int MaximumBoxes=128,MeshSlots=29,VerticesPerBox=24,IndicesPerBox=36;
        public static OracleAvatarMeshSlot Slot(int index)
        {
            switch(index)
            {
                case 0:return new OracleAvatarMeshSlot(0,0);case 1:return new OracleAvatarMeshSlot(0,3);
                case 2:return new OracleAvatarMeshSlot(0,4);case 3:return new OracleAvatarMeshSlot(0,2);
                case 4:return new OracleAvatarMeshSlot(0,5);
                case 5:case 6:case 7:return new OracleAvatarMeshSlot(0,1,1,(byte)(index-5));
                case 8:case 9:case 10:return new OracleAvatarMeshSlot(0,3,1,(byte)(index-8));
                case 11:case 12:case 13:return new OracleAvatarMeshSlot(0,0,2,(byte)(index-11));
                case 14:case 15:case 16:return new OracleAvatarMeshSlot(0,3,2,(byte)(index-14));
                case 17:case 18:return new OracleAvatarMeshSlot(0,3,3,(byte)(index-16));
                case 19:return new OracleAvatarMeshSlot(1,0);case 20:return new OracleAvatarMeshSlot(1,3);
                case 21:return new OracleAvatarMeshSlot(2,0);case 22:return new OracleAvatarMeshSlot(2,3);
                case 23:return new OracleAvatarMeshSlot(0,4,4);case 24:return new OracleAvatarMeshSlot(0,3,4);
                case 25:return new OracleAvatarMeshSlot(0,6,4);case 26:return new OracleAvatarMeshSlot(3,3);
                case 27:return new OracleAvatarMeshSlot(3,4);case 28:return new OracleAvatarMeshSlot(3,5);
                default:throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
        public static int Fill(OracleAvatarBox[] output)
        {
            if(output==null||output.Length<MaximumBoxes)throw new ArgumentException("Avatar recipe requires fixed128 box storage.");
            int n=0;
            Add(output,ref n,0,0,1.02f,0,.55f,.60f,.35f); // fitted torso
            Add(output,ref n,0,0,.95f,-.202f,.59f,.75f,.075f); // layered back mantle, structural overlap
            Add(output,ref n,2,0,.62f,0,.50f,.38f,.32f);
            Add(output,ref n,2,-.17f,.43f,0,.18f,.56f,.24f);Add(output,ref n,2,.17f,.43f,0,.18f,.56f,.24f);
            Add(output,ref n,2,-.17f,.12f,.06f,.22f,.24f,.37f);Add(output,ref n,2,.17f,.12f,.06f,.22f,.24f,.37f); // soleY0
            Add(output,ref n,1,0,.76f,.014f,.586f,.07f,.38f); // gold belt
            Add(output,ref n,1,-.245f,1.03f,.179f,.028f,.51f,.024f);Add(output,ref n,1,.245f,1.03f,.179f,.028f,.51f,.024f);
            Add(output,ref n,2,0,1.45f,-.006f,.37f,.38f,.34f); // dark hood/hidden face
            Add(output,ref n,3,-.193f,1.40f,.01f,.058f,.33f,.30f);Add(output,ref n,3,.193f,1.40f,.01f,.058f,.33f,.30f);
            Add(output,ref n,3,0,1.42f,-.173f,.34f,.30f,.058f); // visible rear silver hair in first look
            Add(output,ref n,1,-.158f,1.43f,.174f,.024f,.23f,.024f);Add(output,ref n,1,.158f,1.43f,.174f,.024f,.23f,.024f);
            Add(output,ref n,4,-.072f,1.47f,.171f,.045f,.022f,.024f);Add(output,ref n,4,.072f,1.47f,.171f,.045f,.022f,.024f);
            Sigil(output,ref n,1,0,1.13f,.183f,.11f); // offset-depth cross avoids overlapping exterior coplanar faces
            // Three reusable stepped pointed-hat silhouettes; style3 is hood-only.
            Hat(output,ref n,5,8,.87f,0,1.73f,.42f,.20f,0);Add(output,ref n,5,0,1.90f,-.03f,.25f,.17f,.24f);Add(output,ref n,5,-.04f,2.02f,-.04f,.12f,.09f,.12f);
            Hat(output,ref n,6,9,.94f,0,1.73f,.43f,.20f,0);Add(output,ref n,6,.06f,1.90f,-.03f,.25f,.16f,.24f);Add(output,ref n,6,.19f,1.97f,-.03f,.20f,.10f,.13f);
            Hat(output,ref n,7,10,1.03f,0,1.72f,.46f,.18f,0);Add(output,ref n,7,0,1.86f,-.02f,.27f,.14f,.25f);
            // Robe hems: long, split front coat, and shorter tabard over boots.
            Add(output,ref n,11,0,.46f,0,.65f,.57f,.42f);Add(output,ref n,14,0,.195f,0,.68f,.045f,.45f);
            Add(output,ref n,12,-.185f,.46f,.02f,.32f,.57f,.44f);Add(output,ref n,12,.185f,.46f,.02f,.32f,.57f,.44f);
            Add(output,ref n,15,-.033f,.46f,.244f,.024f,.57f,.022f);Add(output,ref n,15,.033f,.46f,.244f,.024f,.57f,.022f);
            Add(output,ref n,13,0,.58f,0,.60f,.34f,.40f);Add(output,ref n,16,0,.42f,0,.63f,.045f,.43f);
            // Shoulders use overlapping stair-step gold plates, with face-depth offsets.
            Shoulder(output,ref n,17,-1,false);Shoulder(output,ref n,17,1,false);
            Shoulder(output,ref n,18,-1,true);Shoulder(output,ref n,18,1,true);
            // Arm coordinates are local to pivotX+/-0.35/Y1.15; rigid transforms replace skinning.
            Add(output,ref n,19,-.035f,-.18f,.015f,.24f,.39f,.24f);Add(output,ref n,20,-.035f,-.40f,.19f,.16f,.14f,.18f);
            Add(output,ref n,21,.035f,-.18f,.015f,.24f,.39f,.24f);Add(output,ref n,22,.035f,-.40f,.19f,.16f,.14f,.18f);
            Add(output,ref n,23,-.355f,.66f,-.015f,.22f,.34f,.13f);Add(output,ref n,25,-.355f,.66f,.024f,.185f,.29f,.08f);
            Add(output,ref n,23,-.355f,.66f,.073f,.23f,.35f,.024f);
            Add(output,ref n,24,-.46f,.66f,.087f,.025f,.35f,.018f);Add(output,ref n,24,-.25f,.66f,.087f,.025f,.35f,.018f);
            Add(output,ref n,24,-.355f,.82f,.089f,.23f,.025f,.018f);Add(output,ref n,24,-.355f,.50f,.091f,.23f,.025f,.018f);
            Sigil(output,ref n,24,-.355f,.66f,.089f,.095f);
            Add(output,ref n,27,0,.12f,0,.055f,.70f,.055f);Add(output,ref n,26,0,.47f,0,.13f,.16f,.11f);
            Add(output,ref n,28,0,.49f,.058f,.065f,.09f,.024f);
            return n;
        }
        static void Hat(OracleAvatarBox[] p,ref int n,int cloth,int gold,float brim,float x,float y,float width,float height,float z)
        {Add(p,ref n,cloth,0,1.625f,0,brim,.07f,.66f);Add(p,ref n,cloth,x,y,z,width,height,.37f);Add(p,ref n,gold,x,1.658f,.004f,width+.026f,.045f,.393f);Sigil(p,ref n,gold,x,1.745f,.193f,.085f);}
        static void Shoulder(OracleAvatarBox[] p,ref int n,int slot,int side,bool layers)
        {Add(p,ref n,slot,side*.355f,1.225f,0,.25f,.10f,.41f);if(layers){Add(p,ref n,slot,side*.40f,1.17f,.002f,.26f,.085f,.435f);Add(p,ref n,slot,side*.44f,1.115f,.004f,.25f,.08f,.46f);}}
        static void Sigil(OracleAvatarBox[] p,ref int n,int slot,float x,float y,float z,float size)
        {Add(p,ref n,slot,x,y,z,.022f,size,.022f);Add(p,ref n,slot,x,y,z+.003f,size*.8f,.022f,.022f);}
        static void Add(OracleAvatarBox[] p,ref int n,int slot,float x,float y,float z,float w,float h,float d)
        {if(n>=MaximumBoxes||slot<0||slot>=MeshSlots||w<=0||h<=0||d<=0)throw new InvalidOperationException("Avatar recipe bound.");p[n++]=new OracleAvatarBox(slot,x,y,z,w,h,d);}
    }
}

// # ARCHITECTURE SNAPSHOT
// Presentation-only28-byte numeric boxes and4-byte mesh-slot descriptors. Fixed128 boxes/29 slots.
// Metres Y-up/+Z front; body origin is validated feetY0, arm pivots±.35/1.15, wand remains existing rigid owner.
// No entity tracking/native ownership is moved; numeric recipes are cold immutable input, not world voxels.
