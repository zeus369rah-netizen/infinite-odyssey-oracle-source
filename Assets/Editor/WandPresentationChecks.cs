#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace OdysseyOracle.Editor
{
    // Explicit root-only read-only native checks: actual mesh vertices and live camera transforms.
    // No private reflection, recipe reconstruction, input, Physics fixture, object creation or state mutation.
    public static class WandPresentationChecks
    {
        [Serializable] public sealed class Report
        {
            public string utc,status,exception,meaning="Actual native geometry observation only; no visual approval or browser/FPS/GC claim.";
            public int assertions,visibleVertices,gripVertices,uniqueMaterials,arcSubmeshes;
            public float nearestVisibleDepth,nearClip,clearance,gripMinimumRadius,wrapMaximumRadius;
            public Vector3 gripMinimum,gripMaximum;
        }
        public static string Run(OracleViewmodel viewmodel,Camera camera)
        {
            var r=new Report {utc=DateTime.UtcNow.ToString("o"),status="running",nearestVisibleDepth=float.MaxValue,
                gripMinimumRadius=float.MaxValue};
            try
            {
                Require(r,Application.isPlaying&&viewmodel!=null&&camera!=null,"Actual running viewmodel and camera required.");
                MeshFilter body=null,arcs=null;var filters=camera.GetComponentsInChildren<MeshFilter>(true);
                foreach(var filter in filters)
                {
                    if(filter.name=="Combined Oracle viewmodel")body=filter;
                    if(filter.name=="Bounded green focus electricity")arcs=filter;
                }
                Require(r,body!=null&&body.sharedMesh!=null&&body.sharedMesh.isReadable,"The real combined hand/wand mesh must be readable for checks.");
                Require(r,arcs!=null&&arcs.sharedMesh!=null,"Actual pooled arc mesh required.");
                r.arcSubmeshes=arcs.sharedMesh.subMeshCount;
                Require(r,r.arcSubmeshes==4,"Four separately tinted arc submeshes must exist.");
                var arcMaterials=arcs.GetComponent<MeshRenderer>().sharedMaterials;
                Require(r,arcMaterials.Length==4&&arcMaterials[0]!=null&&arcMaterials[0]==arcMaterials[1]&&arcMaterials[0]==arcMaterials[2]&&arcMaterials[0]==arcMaterials[3],
                    "Four arc slots borrow one actual material; no per-mask material family.");
                var materials=new HashSet<Material>();
                foreach(var filter in filters)
                {
                    if(filter==null||filter.sharedMesh==null||!filter.transform.IsChildOf(body.transform.parent))continue;
                    var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null)continue;
                    foreach(var material in renderer.sharedMaterials)if(material!=null)materials.Add(material);
                    if(!renderer.enabled||!filter.gameObject.activeInHierarchy)continue;
                    Require(r,filter.sharedMesh.isReadable,"Each visible native presentation mesh must remain readable for inspection.");
                    foreach(Vector3 vertex in filter.sharedMesh.vertices)
                    {
                        float depth=camera.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)).z;
                        Require(r,!float.IsNaN(depth)&&!float.IsInfinity(depth),"Actual camera-space depth must be finite.");
                        r.nearestVisibleDepth=Mathf.Min(r.nearestVisibleDepth,depth);r.visibleVertices++;
                    }
                }
                r.uniqueMaterials=materials.Count;r.clearance=viewmodel.CameraClearanceScale;r.nearClip=camera.nearClipPlane;
                Require(r,r.uniqueMaterials==15,"Retain the actual thirteen body paints + existing spark/arc materials.");
                Require(r,r.clearance>=.28f&&r.clearance<=1f,"Clearance remains a visible positive uniform scale.");
                if(r.visibleVertices==0){r.nearestVisibleDepth=0f;r.status="deferred: root must select a real weapon-preview lesson or unlocked combat";return JsonUtility.ToJson(r,true);}
                Require(r,r.nearestVisibleDepth>=camera.nearClipPlane+.001f,"No active hand, focus, glyph, spark or arc vertex may cross the actual camera near plane.");
                Vector3[] bodyVertices=body.sharedMesh.vertices;var paints=body.GetComponent<MeshRenderer>().sharedMaterials;
                for(int slot=0;slot<paints.Length;slot++)
                {
                    bool glove=paints[slot]!=null&&paints[slot].name.Contains("Charcoal glove");
                    bool wrap=paints[slot]!=null&&paints[slot].name.Contains("Wrapped wand grip");
                    if(!glove&&!wrap)continue;
                    foreach(int index in body.sharedMesh.GetTriangles(slot))
                    {
                        Vector3 p=bodyVertices[index];if(p.y<-.10f||p.y>.10f)continue;
                        float radius=Mathf.Sqrt(p.x*p.x+p.z*p.z);
                        if(wrap)r.wrapMaximumRadius=Mathf.Max(r.wrapMaximumRadius,radius);
                        if(glove){r.gripMinimum=r.gripVertices==0?p:Vector3.Min(r.gripMinimum,p);r.gripMaximum=r.gripVertices==0?p:Vector3.Max(r.gripMaximum,p);r.gripMinimumRadius=Mathf.Min(r.gripMinimumRadius,radius);r.gripVertices++;}
                    }
                }
                Require(r,r.gripVertices>0&&r.wrapMaximumRadius>0f,"Measure actual glove and wrapped-shaft submesh vertices at the grip.");
                Require(r,r.gripMinimum.x<0f&&r.gripMaximum.x>0f&&r.gripMinimum.z<0f&&r.gripMaximum.z>0f,"The existing grip surrounds both lateral and front/back sides of the actual shaft.");
                Require(r,r.gripMinimumRadius<=r.wrapMaximumRadius+.006f,"Actual glove geometry contacts the wrapped-shaft radial envelope.");
                r.status="passed native read-only geometry checks";
            }
            catch(Exception e){r.status="failed";r.exception=e.ToString();}
            return JsonUtility.ToJson(r,true);
        }
        static void Require(Report report,bool condition,string message){report.assertions++;Assert.IsTrue(condition,message);}
    }
}
#endif
