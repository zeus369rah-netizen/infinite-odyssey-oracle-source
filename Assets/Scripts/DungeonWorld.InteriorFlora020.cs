using UnityEngine;
using P3=OdysseyOracle.TutorialRoomV2Layout.P3;
namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        Batch interiorMossStone020;
        void BeginInteriorFlora020()
        {
            // Reuse the supported stone shader/state. This material is refuge-only.
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,true){name="020 limestone with sparse moss",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat,anisoLevel=1};
            resources.Add(texture);
            var pixels=new Color[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {var tint=InteriorFloraRecipe020.MossPixel(x,y);pixels[x+y*32]=new Color(tint.R,tint.G,tint.B,1);}
            texture.SetPixels(pixels);texture.Apply(true,true);
            var material=new Material(batches[Surface.Stone].Material){name="020 refuge moss on limestone"};
            resources.Add(material);material.SetTexture("_BaseMap",texture);material.mainTexture=texture;material.SetColor("_BaseColor",Color.white);
            interiorMossStone020=new Batch(material);
        }
        static bool IsLegacyBlockVine020(RoomV2Paint paint,P3 size)
        {
            if(paint!=RoomV2Paint.Moss&&paint!=RoomV2Paint.Green)return false;
            return size.X==.065f&&size.Y==.22f&&size.Z==.16f||size.X==.10f&&size.Y==.24f&&size.Z==.12f;
        }
        void FinishInteriorFlora020()
        {
            InteriorFloraRecipe020.Build(tutorialRefuge,AppendInteriorStem020,AppendInteriorLeaf020);
            if(interiorMossStone020.Vertices.Count>0)FinishTutorialBatch(Surface.Moss,interiorMossStone020);
            else interiorMossStone020.Release();
            interiorMossStone020=null;
        }
        void AppendInteriorStem020(InteriorFloraRecipe020.Stem stem)
        {batches[Surface.Wood].Box(InteriorPoint020(stem.Center),InteriorPoint020(stem.Size),Quaternion.identity);}
        void AppendInteriorLeaf020(InteriorFloraRecipe020.Leaf leaf)
        {
            InteriorTriangle020(leaf.A,leaf.B,leaf.Ridge,leaf.Side);
            InteriorTriangle020(leaf.B,leaf.C,leaf.Ridge,leaf.Side);
            InteriorTriangle020(leaf.C,leaf.D,leaf.Ridge,leaf.Side);
            InteriorTriangle020(leaf.D,leaf.A,leaf.Ridge,leaf.Side);
        }
        void InteriorTriangle020(P3 pa,P3 pb,P3 pc,int side)
        {
            Vector3 a=InteriorPoint020(side>0?pb:pa),b=InteriorPoint020(side>0?pa:pb),c=InteriorPoint020(pc);
            Vector3 normal=Vector3.Cross(b-a,c-a);normal.Normalize();
            Batch batch=batches[Surface.Green];int first=batch.Vertices.Count;
            batch.Vertices.Add(a);batch.Vertices.Add(b);batch.Vertices.Add(c);
            batch.Normals.Add(normal);batch.Normals.Add(normal);batch.Normals.Add(normal);
            batch.UVs.Add(new Vector2(a.z,a.y)*2f);batch.UVs.Add(new Vector2(b.z,b.y)*2f);batch.UVs.Add(new Vector2(c.z,c.y)*2f);
            batch.Triangles.Add(first);batch.Triangles.Add(first+1);batch.Triangles.Add(first+2);
        }
        static Vector3 InteriorPoint020(P3 p)=>new Vector3(p.X,p.Y,p.Z);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Supported local refuge hall wall planes; existing4m texture UV/chunk projection and unit/yaw root contract.
// Array Formats: Existing combined stone/wood/green batches, one owned refuge-only Lit material/1024pixel texture and bounded cold pure descriptors.
// Dependencies/Pointers: All Unity resource creation occurs at cold World build, registered in existing resources; final batching/release uses TutorialRenderPartition. No shader, collider, plant Object, frame callback, global random or arena palette owner added. Root native/browser validation pending.
