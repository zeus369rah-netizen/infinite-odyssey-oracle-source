#if UNITY_EDITOR
using System;
using UnityEngine;

namespace OdysseyOracle.CampaignLibrary.Authoring.Editor
{
    // Editor provenance asset only. No prefab references this class or needs a runtime script.
    public sealed class CampaignPropKitManifest : ScriptableObject
    {
        public string generatorId, geometryFingerprint, createdUtc, provenance;
        public int recipeVersion, paletteCount, totalTriangles;
        public CampaignPropRecord[] props = Array.Empty<CampaignPropRecord>();
        public CampaignOwnedAssetRecord[] ownedAssets = Array.Empty<CampaignOwnedAssetRecord>();
    }
    [Serializable] public sealed class CampaignPropRecord
    {
        public string name, description, meshPath, prefabPath;
        public int triangles, vertices, submeshes, primitives, closedPrimitiveEdges;
        public Vector3 minimum, maximum;
        public string[] anchorNames = Array.Empty<string>();
    }
    [Serializable] public sealed class CampaignOwnedAssetRecord
    {
        public string path, guid, dependencyHash;
    }
}
#endif
