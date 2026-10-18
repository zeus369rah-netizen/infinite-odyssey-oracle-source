using System;
using System.Collections.Generic;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit serialized prefab references, created by root's authoring baker. Never Resources/AssetDatabase
    // lookup at runtime. This catalog borrows kit assets; spawned instances belong to the room geometry root.
    // The kit is original ZeusRa/Codex procedural art; no CC0 or outside-image reuse claim is made.
    public sealed class TutorialRoomV2Catalog : ScriptableObject
    {
        public const string GeneratorId="ZeusRa.Oracle.TutorialRoomV2";
        public GameObject RunicBookstand,ForgeWorkbench,SuppliesRack,BrazierRing,WallLantern,CrystalReliquary;
        public string Provenance="Original ZeusRa design direction and Codex procedural implementation; reference images are design inspiration, not reused assets.";
        [Serializable] public sealed class PlacementReport {public int requested,placed,renderers;public string[] missing;}
        public GameObject Get(TutorialRoomV2Layout.PropId id)
        {
            switch(id){case TutorialRoomV2Layout.PropId.RunicBookstand:return RunicBookstand;case TutorialRoomV2Layout.PropId.ForgeWorkbench:return ForgeWorkbench;
                case TutorialRoomV2Layout.PropId.SuppliesRack:return SuppliesRack;case TutorialRoomV2Layout.PropId.BrazierRing:return BrazierRing;
                case TutorialRoomV2Layout.PropId.WallLantern:return WallLantern;case TutorialRoomV2Layout.PropId.CrystalReliquary:return CrystalReliquary;
                default:throw new ArgumentOutOfRangeException(nameof(id));}
        }
        public PlacementReport InstantiateDecorations(Transform parent,TutorialRoomV2Layout layout)
        {
            if(parent==null||layout==null)throw new ArgumentNullException("Room ownership/layout required.");
            var missing=new List<string>();var report=new PlacementReport{requested=layout.PropPlacements.Length};
            foreach(TutorialRoomV2Layout.Placement placement in layout.PropPlacements)
            {
                GameObject prefab=Get(placement.Id);if(prefab==null){missing.Add(placement.Id.ToString());continue;}
                if(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length!=0||prefab.GetComponentsInChildren<Collider>(true).Length!=0||prefab.GetComponentsInChildren<Light>(true).Length!=0||prefab.GetComponentsInChildren<AudioSource>(true).Length!=0)
                    throw new InvalidOperationException("RoomV2 kit props must remain the original render-only prefabs: "+prefab.name);
                GameObject instance=UnityEngine.Object.Instantiate(prefab,parent,false);instance.name="RoomV2 "+prefab.name+" "+report.placed;
                instance.transform.localRotation=Quaternion.Euler(0,placement.Yaw,0);instance.transform.localPosition=Vector3.zero;
                Vector3 desired=parent.TransformPoint(new Vector3(placement.Position.X,placement.Position.Y,placement.Position.Z));
                if(!string.IsNullOrEmpty(placement.AlignAnchor)){
                    Transform anchor=instance.transform.Find(placement.AlignAnchor);
                    if(anchor==null){instance.SetActive(false);UnityEngine.Object.Destroy(instance);throw new InvalidOperationException("Authored prop anchor missing: "+placement.AlignAnchor);}
                    instance.transform.position+=desired-anchor.position;
                }else instance.transform.position=desired;
                report.placed++;report.renderers+=instance.GetComponentsInChildren<MeshRenderer>(true).Length;
            }
            report.missing=missing.ToArray();return report;
        }
    }
}
