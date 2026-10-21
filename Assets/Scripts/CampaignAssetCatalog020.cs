// PSEUDOCODE: retain accepted cooked meshes through one Resources catalogue; validate exact vertex channels and finite metadata cold; expose numeric flyweights only to jobs.
using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace OdysseyOracle
{
    [Serializable] public sealed class CampaignAssetEntry020
    {
        public uint Key; public string SourceId,RecipeSha256,WitnessSha256;
        public Mesh Lod0,Lod1,Lod2; public Vector3 Thresholds; public byte AllowedLods;
        public Mesh MeshAt(int lod)=>lod==0?Lod0:lod==1?Lod1:Lod2;
    }
    [Serializable] public struct CampaignAssetPlacement020
    {public uint Key,AssetKey,FloorKey;public Vector3 Feet;public float Yaw;}
    public sealed class CampaignAssetCatalog020:ScriptableObject
    {
        public const int MaximumAssets=8,MaximumInstances=2048,MaximumBuckets=256,PageSize=128,PilotCount=39;
        public const string LoadKey="ArtBible/CampaignAssetCatalog020";
        public uint Revision=1;public string CookSha256,CaptureSha256;public Material SharedMaterial;
        public CampaignAssetEntry020[] Entries;public CampaignAssetPlacement020[] Placements;
        public bool ValidateCold(out string failure)
        {
            failure="Catalogue contract unavailable";
            if(Revision==0||CookSha256==null||CookSha256.Length!=64||CaptureSha256==null||CaptureSha256.Length!=64||
                Entries==null||Entries.Length!=3||Placements==null||Placements.Length!=PilotCount||SharedMaterial==null||
                SharedMaterial.shader==null||SharedMaterial.shader.name!="Oracle/StageA Voxel Wet020"||!SharedMaterial.shader.isSupported)return false;
            bool paving=false,lamp=false,tree=false;
            for(int i=0;i<Entries.Length;i++)
            {
                var row=Entries[i];if(row==null||row.RecipeSha256==null||row.RecipeSha256.Length!=64||row.WitnessSha256==null||row.WitnessSha256.Length!=64)return false;
                if(row.Key==5){if(paving||row.AllowedLods!=7)return false;paving=true;}
                else if(row.Key==42){if(lamp||row.AllowedLods!=7)return false;lamp=true;}
                else if(row.Key==35){if(tree||row.AllowedLods!=3)return false;tree=true;}else return false;
                if(!Finite(row.Thresholds)||row.Thresholds.x<=row.Thresholds.y||row.Thresholds.y<=row.Thresholds.z||row.Thresholds.z<=0)return false;
                for(int lod=0;lod<3;lod++)
                {
                    var mesh=row.MeshAt(lod);if(mesh==null||mesh.vertexCount<4||mesh.vertexCount>65532||mesh.subMeshCount!=1||mesh.GetIndexCount(0)<6||mesh.GetIndexCount(0)>98298||mesh.GetIndexCount(0)%3!=0||mesh.GetTopology(0)!=MeshTopology.Triangles||mesh.GetVertexBufferStride(0)!=92)return false;
                    if(mesh.GetVertexAttributeDimension(VertexAttribute.Position)!=3||mesh.GetVertexAttributeFormat(VertexAttribute.Position)!=VertexAttributeFormat.Float32||mesh.GetVertexAttributeDimension(VertexAttribute.Normal)!=3||mesh.GetVertexAttributeFormat(VertexAttribute.Normal)!=VertexAttributeFormat.Float32||mesh.GetVertexAttributeDimension(VertexAttribute.Color)!=4||mesh.GetVertexAttributeFormat(VertexAttribute.Color)!=VertexAttributeFormat.UNorm8)return false;
                    for(int uv=0;uv<8;uv++)if(mesh.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+uv))!=2||mesh.GetVertexAttributeFormat((VertexAttribute)((int)VertexAttribute.TexCoord0+uv))!=VertexAttributeFormat.Float32)return false;
                    if(!Finite(mesh.bounds.center)||!Finite(mesh.bounds.extents)||mesh.bounds.extents.x<=0||mesh.bounds.extents.y<=0||mesh.bounds.extents.z<=0)return false;
                    if(row.Key==5&&((mesh.bounds.center-new Vector3(1,-.0625f,1)).sqrMagnitude>1e-8f||(mesh.bounds.extents-new Vector3(1,.0625f,1)).sqrMagnitude>1e-8f))return false;
                    for(int a=0;a<=i;a++)for(int l=0;l<3;l++)if(a!=i||l!=lod)if(Entries[a]!=null&&ReferenceEquals(Entries[a].MeshAt(l),mesh))return false;
                }
            }
            for(int i=0;i<PilotCount;i++)
            {
                var p=Placements[i];int x=i%13,z=i/13;
                if(p.Key!=(uint)(i+1)||p.AssetKey!=5||p.FloorKey!=10||p.Yaw!=0||p.Feet!=new Vector3(-58+2*x,0,-30+2*z))return false;
            }
            failure="";return paving&&lamp&&tree;
        }
        static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Canonical local meshes/Y-up metres, explicit scale1 root rows.
// Array Formats:3accepted archetypes/9borrowed immutable meshes,39serialized paving rows; tree allowed mask3.
// Dependencies/Pointers: One Resources asset retains build dependencies; cold validation only, no Editor/runtime file access or borrowed asset destruction.
