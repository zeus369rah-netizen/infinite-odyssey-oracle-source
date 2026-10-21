using System;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Session-cold adapter borrowing validated saved meshes/materials. Reuses the
    // original recipe and immutable Template shape without modifying the frozen
    // exporter/library family. No Resources.Load, mesh build or material clone.
    public sealed class SavedTowerModelLibrary : IDisposable
    {
        readonly TowerModelLibrary.Template[,] templates=new TowerModelLibrary.Template[4,3];
        readonly Material[] materials=new Material[4];
        bool disposed;
        TowerElementVisualLibrary elements;
        public TowerElementVisualLibrary Elements=>elements;
        SavedTowerModelLibrary(TowerModelAssetCatalog assets)
        {
            for(int type=0;type<4;type++)
            {
                materials[type]=assets.GetMaterial(type);
                for(int tier=1;tier<=3;tier++)
                {
                    var template=new TowerModelLibrary.Template(TowerModelRecipes.Build(type,tier));
                    for(int group=0;group<6;group++)template.Groups[group]=assets.GetMesh(type,tier,group);
                    templates[type,tier-1]=template;
                }
            }
            elements=TowerElementVisualLibrary.TryCreate();
        }
        public static bool TryCreate(TowerModelAssetCatalog assets,out SavedTowerModelLibrary library,out string reason)
        {
            library=null;
            if(assets==null){reason="Saved tower catalog is not bound.";return false;}
            if(!assets.TryValidate(out reason))return false;
            library=new SavedTowerModelLibrary(assets);return true;
        }
        public TowerModelLibrary.Template Get(int type,int tier)
        {if(disposed||type<0||type>3||tier<1||tier>3)throw new ArgumentOutOfRangeException();return templates[type,tier-1];}
        public Material GetMaterial(int type)
        {if(disposed||type<0||type>3)throw new ArgumentOutOfRangeException();return materials[type];}
        public void Dispose()
        {
            if(disposed)return;disposed=true;elements?.Dispose();elements=null;
            for(int type=0;type<4;type++)
            {materials[type]=null;for(int tier=0;tier<3;tier++)templates[type,tier]=null;}
            // AssetDatabase/scene owns every referenced asset. Never Destroy it.
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing tower Head-local/Y-up pivots and accepted target world points remain canonical; foundation/pads/target lock/range unchanged.
// Array Formats: Existing saved72 meshes/four materials remain borrowed; additive12 original FX templates, two renderers and one fixed96-byte native state/pose pair per instance.
// Dependencies/Pointers: Existing accepted tier/shot owners feed cosmetics only; Burst fences finish before Object writes. No authority/economy/radius/status/audio/network rewrite; native acceptance pending.
