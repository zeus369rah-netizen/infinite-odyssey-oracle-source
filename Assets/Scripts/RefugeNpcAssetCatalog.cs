using System;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit references to the two validated ORIGINAL neutral prefabs/18 meshes.
    // No Resources lookup, runtime mesh/material generation, arbitrary behaviours,
    // palette edits, authority, animation state or world pose in this saved asset.
    [CreateAssetMenu(menuName="Infinite Odyssey/Original refuge NPC catalog")]
    public sealed class RefugeNpcAssetCatalog : ScriptableObject
    {
        public const string Ownership="odyssey.oracle.refuge-npc-runtime-catalog.v1";
        public const int ModelCount=2,PartsPerModel=9;
        [SerializeField] string ownership;
        [SerializeField] GameObject ilyra,bram;
        [SerializeField] Material sharedPaint;
        [SerializeField] Mesh[] originalMeshes;
        [SerializeField] Vector3[] originalPositions,originalScales;
        [SerializeField] Quaternion[] originalRotations;
        public GameObject Prefab(int role)=>role==2?ilyra:role==3?bram:null;
        public Material SharedPaint=>sharedPaint;
        public static string PartPath(int role,int part)
        {
            if(role<2||role>3||part<0||part>=9)throw new ArgumentOutOfRangeException();
            switch(part)
            {
                case 0:return "Grounded boots and layered skirt";
                case 1:return "Breathing fitted torso";
                case 2:return "Breathing fitted torso/Gaze head";
                case 3:return "Breathing fitted torso/Gaze head/Voice RMS jaw opening";
                case 4:return "Breathing fitted torso/Gaze head/Blink skin lids";
                case 5:return "Breathing fitted torso/Left shoulder and grip";
                case 6:return "Breathing fitted torso/Right shoulder and grip";
                case 7:return role==2?"Breathing fitted torso/Right shoulder and grip/Split crown emerald staff":"Breathing fitted torso/Left shoulder and grip/Forked copper watch spear";
                default:return role==2?"Breathing fitted torso/Left shoulder and grip/Bound ivory scroll and seal":"Breathing fitted torso/Right shoulder and grip/Caged teal watch lantern";
            }
        }
        public static string ToolSocketPath(int role)=>PartPath(role,7)+"/HandTool";
        public bool TryValidate(out string reason)
        {
            reason="Original refuge NPC catalog is not bound or has drifted.";
            if(ownership!=Ownership||ilyra==null||bram==null||ilyra==bram||sharedPaint==null||sharedPaint.shader==null||
                sharedPaint.shader.name!="Universal Render Pipeline/Lit"||originalMeshes==null||originalMeshes.Length!=18||
                originalPositions==null||originalPositions.Length!=18||originalScales==null||originalScales.Length!=18||
                originalRotations==null||originalRotations.Length!=18)return false;
            for(int role=2;role<=3;role++)
            {
                GameObject prefab=Prefab(role);
                if(prefab.transform.localScale!=Vector3.one||prefab.GetComponentsInChildren<MeshFilter>(true).Length!=9||
                    prefab.GetComponentsInChildren<MeshRenderer>(true).Length!=9)return false;
                foreach(Component component in prefab.GetComponentsInChildren<Component>(true))
                    if(component==null||!(component is Transform||component is MeshFilter||component is MeshRenderer))return false;
                Transform feet=prefab.transform.Find("Feet"),interact=prefab.transform.Find("Interact"),voice=prefab.transform.Find("Voice");
                if(feet==null||interact==null||voice==null||prefab.transform.Find(ToolSocketPath(role))==null||
                    feet.localPosition.sqrMagnitude>1e-8f||(interact.localPosition-new Vector3(0,1.52f,0)).sqrMagnitude>1e-8f||
                    (voice.localPosition-new Vector3(0,1.56f,.18f)).sqrMagnitude>1e-8f)return false;
                long triangles=0;int vertices=0;
                for(int part=0;part<9;part++)
                {
                    Transform node=prefab.transform.Find(PartPath(role,part));if(node==null)return false;
                    var filter=node.GetComponent<MeshFilter>();var renderer=node.GetComponent<MeshRenderer>();
                    Mesh expected=originalMeshes[(role-2)*9+part];
                    int index=(role-2)*9+part;
                    if(filter==null||renderer==null||expected==null||filter.sharedMesh!=expected||expected.subMeshCount!=1||
                        renderer.sharedMaterials.Length!=1||renderer.sharedMaterial!=sharedPaint||node.localPosition!=originalPositions[index]||
                        node.localScale!=originalScales[index]||Quaternion.Angle(node.localRotation,originalRotations[index])>.001f)return false;
                    triangles+=(long)expected.GetIndexCount(0)/3;vertices+=expected.vertexCount;
                    for(int previous=0;previous<(role-2)*9+part;previous++)if(originalMeshes[previous]==expected)return false;
                }
                if(triangles!=(role==2?1548:1468)||vertices!=(role==2?3204:3048))return false;
            }
            reason="Original two-NPC saved catalog ready.";return true;
        }
#if UNITY_EDITOR
        public void ConfigureValidatedOriginals(GameObject rune,GameObject gate)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Bind original saved NPCs with Play stopped.");
            if(ownership!=null&&ownership!=""&&ownership!=Ownership)throw new InvalidOperationException("Foreign catalog ownership.");
            ownership=Ownership;ilyra=rune;bram=gate;originalMeshes=new Mesh[18];sharedPaint=null;
            originalPositions=new Vector3[18];originalScales=new Vector3[18];originalRotations=new Quaternion[18];
            for(int role=2;role<=3;role++)for(int part=0;part<9;part++)
            {
                Transform node=Prefab(role)!=null?Prefab(role).transform.Find(PartPath(role,part)):null;
                if(node==null)throw new InvalidOperationException("Missing original named NPC part.");
                originalMeshes[(role-2)*9+part]=node.GetComponent<MeshFilter>()?.sharedMesh;
                originalPositions[(role-2)*9+part]=node.localPosition;originalScales[(role-2)*9+part]=node.localScale;
                originalRotations[(role-2)*9+part]=node.localRotation;
                if(sharedPaint==null)sharedPaint=node.GetComponent<MeshRenderer>()?.sharedMaterial;
            }
            if(!TryValidate(out string reason))throw new InvalidOperationException(reason);
        }
#endif
    }
}
