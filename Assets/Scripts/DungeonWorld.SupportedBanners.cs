using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        Material refugeBannerCloth,refugeBannerTrim,refugeBannerGlyph;
        private bool TryResolveRefugeBannerClearance(float x,ref float z,bool alongWall)
        {
            if(!IsTutorialRefuge||!alongWall)return true;
            const float rodHalfWidth=.775f,clearance=.06f;
            if(x>0&&Mathf.Abs(x-tutorialRefuge.OuterX)<1.3f)
                foreach(float center in tutorialRefuge.AlcoveCenters)
                    if(Mathf.Abs(z-center)<TutorialRoomV2Layout.WindowHalfWidth+rodHalfWidth+clearance)
                        return false;
            float depth=tutorialRefuge.HalfWidth-Mathf.Abs(x);
            if(depth<0||depth>1f)return false;
            if(Mathf.Abs(Mathf.Abs(x)-tutorialRefuge.HalfWidth)<.8f)
                foreach(float center in tutorialRefuge.AlcoveCenters){
                    float minimum=TutorialRoomV2Layout.DoorHalfWidth+rodHalfWidth+clearance;
                    if(Mathf.Abs(z-center)<minimum)z=center+(z<center?-minimum:minimum);
                }
            // Optional scenery is skipped before emitting anything when a complete supported flag
            // cannot fit. A future bad window anchor must never abort the structural World build.
            return true;
        }
        private void BuildRefugeBannerSupport(Vector3 p,Quaternion q,bool alongWall)
        {
            if(alongWall){
                float depth=tutorialRefuge.HalfWidth-Mathf.Abs(p.x);
                if(depth<0||depth>1f)return; // Caller resolves this before any flag emission.
                Cube(Surface.Iron,p+q*new Vector3(0,-.10f,depth*.5f),new Vector3(.12f,.12f,depth+.18f),q);
                Cube(Surface.Iron,p+q*new Vector3(0,-.19f,depth+.08f),new Vector3(.24f,.6f,.12f),q);
                Vector3 a=p+q*new Vector3(0,-.45f,depth+.08f),b=p+q*new Vector3(0,-.04f,0);
                Cube(Surface.Iron,(a+b)*.5f,new Vector3(.065f,(b-a).magnitude+.02f,.065f),Quaternion.FromToRotation(Vector3.up,b-a));
            }else{
                float side=p.x<0?-1f:1f;
                // Current portal outer arch spans |x|2.52..3.22 at this height/depth.
                Vector3 a=new Vector3(side*3.10f,p.y,p.z),b=p;
                Cube(Surface.Iron,(a+b)*.5f,new Vector3(Mathf.Abs(b.x-a.x)+.04f,.12f,.12f));
                Cube(Surface.Iron,a,new Vector3(.18f,.32f,.24f));
                Vector3 low=a+Vector3.down*.60f,high=p+new Vector3(-side*.20f,0,0);
                Cube(Surface.Iron,(low+high)*.5f,new Vector3(.075f,(high-low).magnitude+.02f,.075f),Quaternion.FromToRotation(Vector3.up,high-low));
            }
        }
        private bool TryBuildRefugePortalBanner(Vector3 p,Quaternion q)
        {
            Shader shader=Resources.Load<Shader>("Shaders/OracleBannerFlutter");
            if(shader==null||!shader.isSupported)return false; // Mechanically supported static fallback remains.
            if(refugeBannerCloth==null){
                refugeBannerCloth=MakeRefugeBannerMaterial(Surface.PortalCloth,shader);
                refugeBannerTrim=MakeRefugeBannerMaterial(Surface.Brass,shader);
                refugeBannerGlyph=MakeRefugeBannerMaterial(Surface.Azure,shader);
            }
            Cube(Surface.Iron,p,new Vector3(1.55f,.09f,.11f),q);
            Transform mount=new GameObject("Supported portal standard").transform;mount.SetParent(geometryRoot,false);mount.localPosition=p;mount.localRotation=q;
            var cloth=new Batch(refugeBannerCloth);var trim=new Batch(refugeBannerTrim);var glyph=new Batch(refugeBannerGlyph);
            // One closed stepped silhouette, with no coplanar overlap between the former three boxes.
            BannerBand(cloth,.56f,-.01f,-2.11f);BannerBand(cloth,.365f,-2.11f,-2.335f);BannerBand(cloth,.165f,-2.335f,-2.49f);
            Vector2[] outline={new Vector2(-.56f,-.01f),new Vector2(.56f,-.01f),new Vector2(.56f,-2.11f),new Vector2(.365f,-2.11f),new Vector2(.365f,-2.335f),new Vector2(.165f,-2.335f),new Vector2(.165f,-2.49f),new Vector2(-.165f,-2.49f),new Vector2(-.165f,-2.335f),new Vector2(-.365f,-2.335f),new Vector2(-.365f,-2.11f),new Vector2(-.56f,-2.11f)};
            for(int i=0;i<outline.Length;i++){
                Vector2 a=outline[i],b=outline[(i+1)%outline.Length];
                cloth.Quad(new Vector3(a.x,a.y,-.04f),new Vector3(a.x,a.y,.04f),new Vector3(b.x,b.y,.04f),new Vector3(b.x,b.y,-.04f));
            }
            foreach(float side in new[]{-1f,1f}){
                trim.Box(new Vector3(side*.47f,-1.04f,-.049f),new Vector3(.035f,2.02f,.025f),Quaternion.identity);
                for(int n=0;n<3;n++){
                    trim.Box(new Vector3(side*(.13f+n*.10f),-.83f-n*.13f,-.061f),new Vector3(.13f,.08f,.035f),Quaternion.identity);
                    trim.Box(new Vector3(side*(.13f+n*.10f),-1.35f+n*.13f,-.061f),new Vector3(.13f,.08f,.035f),Quaternion.identity);
                }
            }
            glyph.Box(new Vector3(0,-1.09f,-.066f),new Vector3(.13f,.25f,.04f),Quaternion.identity);
            FinishRefugeBannerBatch("Violet cloth",cloth,mount);FinishRefugeBannerBatch("Original relief trim",trim,mount);FinishRefugeBannerBatch("Original blue eye",glyph,mount);
            return true;
        }
        private Material MakeRefugeBannerMaterial(Surface surface,Shader shader)
        {
            var material=new Material(batches[surface].Material){shader=shader,name="Supported portal flag "+surface};
            resources.Add(material);return material;
        }
        private static void BannerBand(Batch batch,float halfWidth,float top,float bottom)
        {
            batch.Quad(new Vector3(-halfWidth,bottom,-.04f),new Vector3(-halfWidth,top,-.04f),new Vector3(halfWidth,top,-.04f),new Vector3(halfWidth,bottom,-.04f));
            batch.Quad(new Vector3(-halfWidth,bottom,.04f),new Vector3(halfWidth,bottom,.04f),new Vector3(halfWidth,top,.04f),new Vector3(-halfWidth,top,.04f));
        }
        private void FinishRefugeBannerBatch(string label,Batch batch,Transform parent)
        {
            var mesh=new Mesh{name=label+" bounded shader cloth",indexFormat=IndexFormat.UInt32};resources.Add(mesh);
            mesh.SetVertices(batch.Vertices);mesh.SetNormals(batch.Normals);mesh.SetUVs(0,batch.UVs);mesh.SetTriangles(batch.Triangles,0);mesh.RecalculateBounds();
            Bounds bounds=mesh.bounds;bounds.Expand(new Vector3(.002f,.002f,.074f));mesh.bounds=bounds;mesh.UploadMeshData(true);
            var go=new GameObject(label);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            MeshRenderer renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=batch.Material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;batch.Release();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing Y-up refuge world support bars; local Y0 rod anchors for two portal cloth assemblies.
// Array Formats: cold original closed cloth bands and relief meshes; borrowed authored texture palette; shared shader-only displacement.
// Dependencies/Pointers: World owns resources and cleanup. Hall doorway/window clearance is checked cold; top80mm pinned, flutter <=35mm, no Update/bones/material writes. Floor, gameplay, lamps and floortrail remain unchanged.
