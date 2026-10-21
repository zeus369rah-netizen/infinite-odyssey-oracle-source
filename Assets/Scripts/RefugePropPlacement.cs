using System;
using UnityEngine;
using PropId=OdysseyOracle.TutorialRoomV2Layout.PropId;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // One cold, main-thread prop pass. Borrows the original six saved meshes and
    // eight materials through explicit serialized catalog references. Instances
    // and five conservative floor-prop BoxColliders belong to the tutorial root;
    // no Update, lights, audio, mesh/material clones, Resources or authority.
    public static class RefugePropPlacement
    {
        public static void ValidateCatalog(TutorialRoomV2Catalog catalog)
        {
            if(catalog==null)throw new ArgumentNullException(nameof(catalog),"Assign the real saved OraclePropKit_v1 catalog before selecting008 refuge.");
            for(int id=0;id<6;id++)ValidatePrefab(catalog.Get((PropId)id),(PropId)id);
        }
        static void ValidatePrefab(GameObject prefab,PropId id)
        {
            if(prefab==null)throw new InvalidOperationException("Original saved refuge prop missing: "+id);
            if(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length!=0||prefab.GetComponentsInChildren<Collider>(true).Length!=0||
                prefab.GetComponentsInChildren<Light>(true).Length!=0||prefab.GetComponentsInChildren<AudioSource>(true).Length!=0||
                prefab.GetComponentsInChildren<ParticleSystem>(true).Length!=0)
                throw new InvalidOperationException("Borrow only neutral render-only original prop prefabs: "+id);
            MeshFilter[] filters=prefab.GetComponentsInChildren<MeshFilter>(true);MeshRenderer[] renderers=prefab.GetComponentsInChildren<MeshRenderer>(true);
            if(filters.Length!=1||renderers.Length!=1||filters[0].transform!=prefab.transform||renderers[0].transform!=prefab.transform||filters[0].sharedMesh==null||
                prefab.transform.localPosition.sqrMagnitude>1e-10f||Quaternion.Angle(prefab.transform.localRotation,Quaternion.identity)>.001f||
                (prefab.transform.localScale-Vector3.one).sqrMagnitude>1e-10f)
                throw new InvalidOperationException("Saved prop identity transform/combined mesh contract changed: "+id);
            Bounds actual=filters[0].sharedMesh.bounds;var expected=RefugePlacementPolicy.Expected(id);
            if((actual.min-V(expected.Min)).sqrMagnitude>1e-8f||(actual.max-V(expected.Max)).sqrMagnitude>1e-8f)
                throw new InvalidOperationException("Saved prop bounds differ from the pinned original library: "+id);
            foreach(Material material in renderers[0].sharedMaterials)if(material==null||material.shader==null)throw new InvalidOperationException("Saved prop has a missing shared material: "+id);
            if(id==PropId.WallLantern){Transform mount=prefab.transform.Find("Anchor_Mount");
                if(mount==null||(mount.localPosition-V(RefugePlacementPolicy.LanternMount)).sqrMagnitude>1e-8f)throw new InvalidOperationException("Saved lantern mount changed.");}
        }
        public static TutorialRoomV2Catalog.PlacementReport Place(Transform parent,TutorialRoomV2Catalog catalog,TutorialRoomV2Layout layout)
            =>Place(parent,catalog,layout,out _);
        public static TutorialRoomV2Catalog.PlacementReport Place(Transform parent,TutorialRoomV2Catalog catalog,TutorialRoomV2Layout layout,out LanternSwingOwner swings)
        {
            swings=null;
            if(parent==null||layout==null)throw new ArgumentNullException("Refuge ownership and layout required.");
            ValidateCatalog(catalog);DungeonWorld world=parent.GetComponentInParent<DungeonWorld>();var report=new TutorialRoomV2Catalog.PlacementReport{requested=layout.PropPlacements.Length,missing=Array.Empty<string>()};
            foreach(TutorialRoomV2Layout.Placement placement in layout.PropPlacements)
            {
                GameObject instance=UnityEngine.Object.Instantiate(catalog.Get(placement.Id),parent,false);
                instance.name="008 refuge original "+placement.Id+" "+report.placed;
                instance.transform.localPosition=V(RefugePlacementPolicy.RootPosition(placement));instance.transform.localRotation=Quaternion.Euler(0,placement.Yaw,0);
                // Neutral library assets stay collider-free. These independently
                // owned floor solids prevent walking through dressing in this room.
                if(placement.Id!=PropId.WallLantern){var e=RefugePlacementPolicy.Expected(placement.Id);BoxCollider solid=instance.AddComponent<BoxCollider>();
                    solid.center=(V(e.Min)+V(e.Max))*.5f;solid.size=V(e.Max)-V(e.Min);}
                if(placement.Id==PropId.BrazierRing)world?.BindRefugeBrazier020(instance.transform,report.placed);
                if(placement.Id==PropId.WallLantern)
                {
                    if(LanternSwingOwner.TryPrepare(instance.transform,parent,catalog.ReactiveLantern,ref swings))report.renderers++;
                    else world?.BindRefugeLanternStatic020(instance.transform);
                }
                report.placed++;report.renderers++;
            }return report;
        }
        static Vector3 V(TutorialRoomV2Layout.P3 p)=>new Vector3(p.X,p.Y,p.Z);
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: saved original prop instance and authored fire anchor; original collider/placement unchanged.
// Array Formats: one cold existing flame presentation and reaction binding per brazier; no live prefab mutation.
// Dependencies/Pointers: World owns animated fire/fuel; neutral catalog assets unchanged, exact missing brazier path repaired; native visible flame/fallback acceptance pending.
