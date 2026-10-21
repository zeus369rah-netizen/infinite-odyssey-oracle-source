using UnityEngine;
using OdysseyOracle.CampaignNpcs;

namespace OdysseyOracle
{
    public static class OriginalRuneSpearhead009
    {
        public static Mesh Build()
        {
            var part=new NpcPart();var model=new NpcModelBuilder(part);
            // New original24triangle gold point/collar retains the emerald crown underneath.
            // Point base1.366 overlaps original crystal top1.3825 by16.5mm, concealed.
            model.Crystal(0,1.45f,0,.055f,.24f,NpcModelBuilder.Gold);
            model.Box(0,1.36f,0,.085f,.06f,.085f,NpcModelBuilder.Gold);
            var p=new Vector3[part.Positions.Count];var n=new Vector3[p.Length];var uv=new Vector2[p.Length];
            for(int i=0;i<p.Length;i++){var v=part.Positions[i];var normal=part.Normals[i];p[i]=new Vector3(v.X,v.Y,v.Z);n[i]=new Vector3(normal.X,normal.Y,normal.Z);uv[i]=new Vector2((part.Paints[i]+.5f)/16,.5f);}
            var mesh=new Mesh{name="Original Ilyra rune-guard spearhead009"};
            try{mesh.vertices=p;mesh.normals=n;mesh.uv=uv;mesh.SetTriangles(part.Indices,0);mesh.RecalculateBounds();mesh.UploadMeshData(false);return mesh;}
            catch{if(Application.isPlaying)Object.Destroy(mesh);else Object.DestroyImmediate(mesh);throw;}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original Ilyra held staff-local Y/up, same hand/tool parent and palette; concealed positive intersection retained.
// Array Formats: Cold24triangle original spearhead/collar mesh from project-authored numeric primitives; no copied external asset or runtime density loop.
// Dependencies/Pointers: Borrowed existing palette/identity/staff remain intact; runtime owner releases only this new mesh. Root accepts visual/clearance/support in Unity before publication.
