using System;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Cold serialized asset references, not prefab execution or geometry generation.
    // Root's Editor bake validates the immutable neutral library before binding.
    // Runtime borrowers never mutate/destroy these Mesh/Material assets.
    [CreateAssetMenu(menuName="Infinite Odyssey/Validated saved tower catalog")]
    public sealed class TowerModelAssetCatalog : ScriptableObject
    {
        public const string LibraryVersion="v2_0_0";
        public const string LibraryFingerprint="0fb77036d7e64188c8310d13704088fb52497a739fe754fe555a52ce9eac94ed";
        [SerializeField] int schema=1;
        [SerializeField] string libraryVersion,sourceFingerprint,validatedManifestHash;
        [SerializeField] Mesh[] meshes;
        [SerializeField] Material[] materials;
        public string SourceFingerprint=>sourceFingerprint;
        public string ValidatedManifestHash=>validatedManifestHash;
        public bool HeaderValid=>schema==1&&libraryVersion==LibraryVersion&&sourceFingerprint==LibraryFingerprint&&
            validatedManifestHash!=null&&validatedManifestHash.Length==64&&
            meshes!=null&&meshes.Length==72&&materials!=null&&materials.Length==4;

        public Mesh GetMesh(int type,int tier,int group)
        {
            if(!HeaderValid||type<0||type>3||tier<1||tier>3||group<0||group>5)throw new ArgumentOutOfRangeException();
            return meshes[(type*3+tier-1)*6+group];
        }
        public Material GetMaterial(int type)
        {if(!HeaderValid||type<0||type>3)throw new ArgumentOutOfRangeException();return materials[type];}

        public bool TryValidate(out string reason)
        {
            if(!HeaderValid){reason="Saved tower catalog header/references do not match the validated library.";return false;}
            for(int type=0;type<4;type++)
            {
                Material material=materials[type];
                if(material==null||material.shader==null||material.shader.name!="Universal Render Pipeline/Lit")
                {reason="Saved tower palette material is missing or incompatible.";return false;}
                for(int tier=1;tier<=3;tier++)
                {
                    TowerModelRecipe recipe=TowerModelRecipes.Build(type,tier);
                    int total=0;
                    for(int group=0;group<6;group++)
                    {
                        int expected=0;
                        for(int p=0;p<recipe.Count;p++)if((int)recipe.Piece(p).Group==group)expected+=recipe.Piece(p).Triangles;
                        Mesh mesh=GetMesh(type,tier,group);
                        if(expected==0){if(mesh!=null){reason="Unexpected saved tower mesh group.";return false;}continue;}
                        if(mesh==null||mesh.subMeshCount!=1||mesh.vertexCount<=0||mesh.GetIndexCount(0)!=(uint)(expected*3)||
                            !Finite(mesh.bounds.center)||!Finite(mesh.bounds.size)||mesh.bounds.size.sqrMagnitude>256f)
                        {reason="Saved tower mesh envelope does not match its validated recipe.";return false;}
                        total+=expected;
                    }
                    if(total!=recipe.Triangles||total>TowerModelRecipes.MaximumTriangles)
                    {reason="Saved tower triangle budget is invalid.";return false;}
                }
            }
            reason=string.Empty;return true;
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
#if UNITY_EDITOR
        public void SetBakedReferences(string fingerprint,string manifestHash,Mesh[] savedMeshes,Material[] savedMaterials)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Bind the catalog while Play is stopped.");
            schema=1;libraryVersion=LibraryVersion;sourceFingerprint=fingerprint;validatedManifestHash=manifestHash;meshes=savedMeshes;materials=savedMaterials;
            if(!TryValidate(out string reason))throw new InvalidOperationException(reason);
        }
#endif
    }
}
