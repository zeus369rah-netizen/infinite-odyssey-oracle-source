#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace OdysseyOracle.Diagnostics
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit, finite Editor observation of the EXISTING006 arena. Owns only
    // one temporary camera, RT and readback texture. No world build, player move,
    // clock/prefs/network/profile change or added Light/material/mesh. Only the
    // two exact lava spill enabled flags change synchronously and restore finally.
    public static class Release006VisualCapture
    {
        /// <summary>Only the diagnostic lava camera changes; original roads/default capture remain available.</summary>
        public sealed class LavaViewOptions
        {
            public Vector3 Position,LookAt;
            public bool CaptureRoads=true;
        }
        public static LavaViewOptions ExteriorReceiverView()
        {
            Vector3 d=Direction(1),t=new Vector3(-d.z,0,d.x);
            return new LavaViewOptions{Position=d*77f-t*8f+Vector3.up*15f,
                LookAt=d*82f-t*16f+Vector3.up*4.5f,CaptureRoads=false};
        }
        const string Primary="Lava caldera primary actual spill",Secondary="Lava caldera secondary actual spill";
        [Serializable] public sealed class Image
        {
            public string name,file,sha256;public Vector3 position,lookAt;
            public int frame,magentaPixels,frustumCandidateRenderers;public long frustumCandidateTriangles;
            public double synchronousSubmitAndReadbackMilliseconds;
        }
        [Serializable] public sealed class Delta
        {public int pixels,changedPixels;public float meanAbsoluteRgbDelta,maximumAbsoluteRgbDelta,meanSignedLumaDelta;}
        [Serializable] public sealed class Receiver
        {public string renderer,shader,material;public Vector3 point,normal;public Vector2Int pixel;public float onMinusOffLuma,emissionMaximum;
            public bool withinPrimaryRange,withinSecondaryRange;public float primaryDistance,secondaryDistance,primaryFacing,secondaryFacing;}
        [Serializable] public sealed class ReceiverProbe
        {public string renderer,shader,material,reason;public Vector3 point;public Vector2Int pixel;public bool eligible;}
        [Serializable] public sealed class SurfaceEligibility
        {public string shader,reason;public int submeshes;}
        [Serializable] public sealed class ShaderRecord
        {public string material,shader;public bool supported,compileErrors;}
        [Serializable] public sealed class Spill
        {public string name;public Vector3 position;public Color color;public float range,intensity;public bool originallyEnabled;}
        [Serializable] public sealed class Receipt
        {
            public string status="NOT_RUN",utc,unityVersion,graphicsApi,pipeline,renderer,outputDirectory;
            public string renderApi="Public URP SingleCameraRequest; no Camera.Render fallback";
            public string costScope="Structural counts/frustum candidates and synchronous submit+CPU readback wall time. No ProfilerRecorder frame sampling, isolated GPU time, draw-call or FPS claim.";
            public string imageScope="Fixed temporary camera/AA disabled; existing ambient, main light, fog, current render scale and postprocess remain. Absolute LDR image differences are not lux or perceptual acceptance.";
            public bool nativeExecuted,allOriginalSpillFlagsRestored,mainCameraPreserved,sameFrame,allOwnedObjectsDisposed;
            public bool worldBuilt,settingsChanged,profileChanged,networkChanged;
            public int frameStart,frameEnd,width,height,activeRenderers,uniqueMeshes,uniqueMaterials,uniqueShaders,shaderProblems,nonReadableReceiverMeshes,editorMeshReadbacks;
            public int actualWorldPointLights,actualWorldEnabledPointLights;public long activeIndexedTriangles,uniqueMeshVertices;
            public int perObjectAdditionalLimit;public float renderScale;public string additionalLightsMode;
            public Spill[] lavaLights;public ShaderRecord[] shaders;public Image[] captures;public Delta lavaOnMinusOff,lavaOnRepeatNoise;
            public Receiver[] actualNonEmissiveLitMeshSamples;public ReceiverProbe[] nearestReceiverProbes;public SurfaceEligibility[] receiverEligibility;
            public int receiverProbeRays,receiverProbeMisses,receiverProbeIneligible,receiverProbeEligible;
            public bool lavaCameraOverride;public string error,receiverReadbackStatus;
        }
        sealed class Surface
        {internal Renderer Renderer;internal string Shader,Material,Reason;internal float Emission;internal bool Eligible;internal Vector3[] Vertices;internal int[] Triangles;internal Bounds Bounds;}

        /// <summary>Root calls once between frames in a paused Play session with actual006 already built.</summary>
        public static string Capture(GameSession session,string absoluteOutputDirectory,int width=960,int height=540,LavaViewOptions lavaView=null)
        {
            if(!EditorApplication.isPlaying||session==null||session.World==null||!session.World.IsCoopArena||!session.Paused||session.Player==null||session.Player.ViewCamera==null)
                throw new InvalidOperationException("Root must supply an already-built006 arena in a paused Play session. This helper does not prepare or pause it.");
            if(lavaView!=null&&(!Finite(lavaView.Position)||!Finite(lavaView.LookAt)||(lavaView.LookAt-lavaView.Position).sqrMagnitude<.01f||
                lavaView.Position.sqrMagnitude>512f*512f||lavaView.LookAt.sqrMagnitude>512f*512f))
                throw new ArgumentException("Finite, distinct, bounded lava diagnostic camera positions are required.");
            if(string.IsNullOrWhiteSpace(absoluteOutputDirectory)||!Path.IsPathRooted(absoluteOutputDirectory))throw new ArgumentException("Absolute work diagnostic directory required.");
            string output=Path.GetFullPath(absoluteOutputDirectory),assets=Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(output.StartsWith(assets,StringComparison.OrdinalIgnoreCase)||string.Equals(output.TrimEnd(Path.DirectorySeparatorChar),assets.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Diagnostic outputs must stay outside Assets.");
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline==null||session.World.VignetteBuildResult==null||Mathf.Abs(session.World.MapBounds.width-153f)>.01f)
                throw new InvalidOperationException("Actual006 R76/P64 arena and URP required.");
            Transform arena=null;foreach(Transform child in session.World.transform)if(child.name=="Cooperative elemental wheel environment")arena=child;
            if(arena==null||!arena.gameObject.activeInHierarchy)throw new InvalidOperationException("Active authored006 arena root missing.");
            Light first=null,second=null;int firstCount=0,secondCount=0;
            foreach(var light in arena.GetComponentsInChildren<Light>(true))
            {if(light.name==Primary){first=light;firstCount++;}if(light.name==Secondary){second=light;secondCount++;}}
            if(firstCount!=1||secondCount!=1||first.type!=LightType.Point||second.type!=LightType.Point||
                !first.gameObject.activeInHierarchy||!second.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Exactly the two active authored lava spill Lights are required; no zero-contribution result is inferred from missing lights.");
            bool firstEnabled=first.enabled,secondEnabled=second.enabled;
            Camera source=session.Player.ViewCamera;Vector3 sourcePosition=source.transform.position;Quaternion sourceRotation=source.transform.rotation;
            RenderTexture sourceTarget=source.targetTexture;bool sourceEnabled=source.enabled;
            var report=new Receipt{utc=DateTime.UtcNow.ToString("o"),unityVersion=Application.unityVersion,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),
                pipeline=pipeline.name,outputDirectory=output,frameStart=Time.frameCount,width=Mathf.Clamp(width,320,1280),height=Mathf.Clamp(height,180,720),
                additionalLightsMode=pipeline.additionalLightsRenderingMode.ToString(),perObjectAdditionalLimit=pipeline.maxAdditionalLightsCount,renderScale=pipeline.renderScale,
                lavaLights=new[]{Describe(first),Describe(second)},lavaCameraOverride=lavaView!=null};
            var active=arena.GetComponentsInChildren<MeshRenderer>(false);var surfaces=Inventory(arena,active,report);
            var images=new List<Image>(6);GameObject cameraObject=null;RenderTexture rt=null,previous=RenderTexture.active;Texture2D readable=null;
            try
            {
                Directory.CreateDirectory(output);cameraObject=new GameObject("Owned006 finite diagnostic camera"){hideFlags=HideFlags.HideAndDontSave};
                SceneManager.MoveGameObjectToScene(cameraObject,session.gameObject.scene);
                Camera camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;camera.cameraType=CameraType.Game;
                camera.tag="Untagged";camera.aspect=report.width/(float)report.height;camera.allowDynamicResolution=false;camera.allowMSAA=false;
                camera.farClipPlane=Mathf.Max(source.farClipPlane,190f);camera.stereoTargetEye=StereoTargetEyeMask.None;
                var data=camera.GetUniversalAdditionalCameraData();var original=source.GetComponent<UniversalAdditionalCameraData>();
                data.renderType=CameraRenderType.Base;data.SetRenderer(-1);data.antialiasing=AntialiasingMode.None;
                data.renderPostProcessing=original!=null&&original.renderPostProcessing;
                if(original!=null){data.volumeLayerMask=original.volumeLayerMask;data.volumeTrigger=original.volumeTrigger;}
                if(original!=null&&data.scriptableRenderer!=original.scriptableRenderer)throw new InvalidOperationException("Temporary default renderer differs from actual player renderer; no incomparable fallback capture.");
                report.renderer=data.scriptableRenderer.GetType().FullName;
                rt=RenderTexture.GetTemporary(report.width,report.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,1);
                readable=new Texture2D(report.width,report.height,TextureFormat.RGBA32,false,false);
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Current URP renderer does not support public SingleCameraRequest.");
                report.nativeExecuted=true;
                int[] lanes={0,4,2};string[] names={"fire-road-and-giant-trees","water-road-and-giant-trees","earth-road-and-giant-trees"};
                for(int i=0;i<(lavaView==null||lavaView.CaptureRoads?3:0);i++)
                {Vector3 d=Direction(lanes[i]),t=new Vector3(-d.z,0,d.x);Take(camera,request,rt,readable,names[i],d*29f-t*9f+Vector3.up*7f,d*58f+t*9f+Vector3.up*14f,active,report,images);}
                Vector3 lava=Direction(1),tangent=new Vector3(-lava.z,0,lava.x);
                Vector3 position=lavaView!=null?lavaView.Position:lava*65f-tangent*18f+Vector3.up*10f,look=lavaView!=null?lavaView.LookAt:lava*79f+Vector3.up*3f;
                first.enabled=second.enabled=true;
                Color32[] on=Take(camera,request,rt,readable,"lava-spill-on",position,look,active,report,images);
                first.enabled=second.enabled=false;
                Color32[] off=Take(camera,request,rt,readable,"lava-spill-off",position,look,active,report,images);
                first.enabled=second.enabled=true;
                Color32[] repeat=Take(camera,request,rt,readable,"lava-spill-on-repeat",position,look,active,report,images);
                report.lavaOnMinusOff=Compare(on,off);report.lavaOnRepeatNoise=Compare(on,repeat);
                report.actualNonEmissiveLitMeshSamples=report.nonReadableReceiverMeshes==0?Samples(camera,surfaces,on,off,report.width,report.height,report,first,second):new Receiver[0];
                var heat=new Color32[on.Length];for(int i=0;i<heat.Length;i++){int value=Math.Min(255,4*Math.Max(Math.Abs(on[i].r-off[i].r),Math.Max(Math.Abs(on[i].g-off[i].g),Math.Abs(on[i].b-off[i].b))));heat[i]=new Color32((byte)value,(byte)(value/2),0,255);}
                readable.SetPixels32(heat);readable.Apply(false,false);File.WriteAllBytes(Path.Combine(output,"lava-on-minus-off-4x.png"),readable.EncodeToPNG());
                report.captures=images.ToArray();report.status=report.shaderProblems==0?"CAPTURED_REQUIRES_ROOT_IMAGE_REVIEW":"CAPTURED_SHADER_PROBLEMS";
            }
            catch(Exception e){report.status="INCOMPLETE_CAPTURE";report.error=e.ToString();}
            finally
            {
                if(first!=null)first.enabled=firstEnabled;if(second!=null)second.enabled=secondEnabled;
                report.allOriginalSpillFlagsRestored=first!=null&&second!=null&&first.enabled==firstEnabled&&second.enabled==secondEnabled;
                RenderTexture.active=previous;
                if(cameraObject!=null){var camera=cameraObject.GetComponent<Camera>();if(camera!=null)camera.targetTexture=null;Object.DestroyImmediate(cameraObject);}
                if(readable!=null)Object.DestroyImmediate(readable);if(rt!=null){RenderTexture.ReleaseTemporary(rt);rt=null;}
                report.allOwnedObjectsDisposed=cameraObject==null&&readable==null&&rt==null;
                report.mainCameraPreserved=source!=null&&source.transform.position==sourcePosition&&source.transform.rotation==sourceRotation&&source.targetTexture==sourceTarget&&source.enabled==sourceEnabled;
                report.frameEnd=Time.frameCount;report.sameFrame=report.frameStart==report.frameEnd;
            }
            if(!report.allOriginalSpillFlagsRestored||!report.mainCameraPreserved||!report.allOwnedObjectsDisposed||!report.sameFrame)report.status="FAIL_RESTORE_OR_STATE_CHANGED";
            string json=JsonUtility.ToJson(report,true);File.WriteAllText(Path.Combine(output,"capture-receipt.json"),json);return json;
        }
        static Vector3 Direction(int lane){float a=lane*Mathf.PI/4f;return new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));}
        static Spill Describe(Light l)=>new Spill{name=l.name,position=l.transform.position,color=l.color,range=l.range,intensity=l.intensity,originallyEnabled=l.enabled};
        static List<Surface> Inventory(Transform arena,MeshRenderer[] active,Receipt r)
        {
            var meshes=new HashSet<Mesh>();var materials=new HashSet<Material>();var shaders=new HashSet<Shader>();var descriptions=new List<ShaderRecord>();var surfaces=new List<Surface>();
            foreach(var light in arena.GetComponentsInChildren<Light>(false))if(light.type==LightType.Point){r.actualWorldPointLights++;if(light.enabled)r.actualWorldEnabledPointLights++;}
            foreach(var renderer in active)
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;r.activeRenderers++;
                var filter=renderer.GetComponent<MeshFilter>();Mesh mesh=filter!=null?filter.sharedMesh:null;if(mesh==null)continue;
                if(meshes.Add(mesh))r.uniqueMeshVertices+=mesh.vertexCount;
                Material[] shared=renderer.sharedMaterials;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    r.activeIndexedTriangles+=(long)mesh.GetIndexCount(sub)/3;
                    Material material=sub<shared.Length?shared[sub]:null;Shader shader=material!=null?material.shader:null;
                    if(material==null){r.shaderProblems++;continue;}
                    if(materials.Add(material)){bool errors=shader!=null&&ShaderUtil.ShaderHasError(shader);descriptions.Add(new ShaderRecord{material=material.name,shader=shader!=null?shader.name:"<missing>",supported=shader!=null&&shader.isSupported,compileErrors=errors});if(shader==null||!shader.isSupported||errors)r.shaderProblems++;if(shader!=null)shaders.Add(shader);}
                    // Editor-only public readback accepts the already-uploaded mesh.
                    // It never changes importer/read-write flags or owned runtime data.
                    Vector3[] vertices;int[] indices;
                    if(!ReadMesh(mesh,sub,out vertices,out indices)){r.nonReadableReceiverMeshes++;continue;}
                    r.editorMeshReadbacks++;
                    for(int i=0;i<vertices.Length;i++)vertices[i]=renderer.localToWorldMatrix.MultiplyPoint3x4(vertices[i]);
                    string reason=ReceiverEligibility(material,renderer.HasPropertyBlock(),out float emission);
                    surfaces.Add(new Surface{Renderer=renderer,Shader=shader!=null?shader.name:"<missing>",Material=material.name,
                        Reason=reason,Emission=emission,Eligible=reason=="NON_EMISSIVE_OPAQUE_PBR",Vertices=vertices,Triangles=indices,Bounds=renderer.bounds});
                }
            }
            var eligibility=new Dictionary<string,SurfaceEligibility>();
            foreach(var surface in surfaces)
            {string key=surface.Shader+"|"+surface.Reason;if(!eligibility.TryGetValue(key,out var row))
                {row=new SurfaceEligibility{shader=surface.Shader,reason=surface.Reason};eligibility.Add(key,row);}row.submeshes++;}
            r.receiverEligibility=new List<SurfaceEligibility>(eligibility.Values).ToArray();
            r.receiverReadbackStatus=r.nonReadableReceiverMeshes==0?"PUBLIC_EDITOR_READBACK_COMPLETE":"UNAVAILABLE_READBACK_FAILURE_NO_RECEIVER_ATTRIBUTION";
            r.uniqueMeshes=meshes.Count;r.uniqueMaterials=materials.Count;r.uniqueShaders=shaders.Count;r.shaders=descriptions.ToArray();return surfaces;
        }
        // The custom retained shader is opaque, calls UniversalFragmentPBR, and ALWAYS reads
        // _EmissionColor; its material emission is not conditional on an _EMISSION keyword.
        static string ReceiverEligibility(Material material,bool propertyBlock,out float emission)
        {
            emission=0;string shader=material!=null&&material.shader!=null?material.shader.name:"";
            bool detail=shader=="OdysseyOracle/VoxelDistanceLit",urp=shader=="Universal Render Pipeline/Lit";
            if(!detail&&!urp)return "UNSUPPORTED_RECEIVER_SHADER";
            if(propertyBlock)return "UNINSPECTED_PROPERTY_BLOCK";
            if(material.renderQueue>2500||urp&&(material.HasProperty("_Surface")&&material.GetFloat("_Surface")>.5f||
                material.HasProperty("_AlphaClip")&&material.GetFloat("_AlphaClip")>.5f))return "NOT_OPAQUE_UNCLIPPED";
            if(material.HasProperty("_EmissionColor")&&(detail||material.IsKeywordEnabled("_EMISSION")))
                emission=material.GetColor("_EmissionColor").maxColorComponent;
            return emission>.001f?"SELF_EMISSIVE":"NON_EMISSIVE_OPAQUE_PBR";
        }
        static bool Finite(Vector3 value)=>!float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&
            !float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);
        static bool ReadMesh(Mesh mesh,int submesh,out Vector3[] vertices,out int[] indices)
        {
            vertices=null;indices=null;
            try
            {
                using(var views=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh))
                {
                    var data=views[0];
                    using(var positions=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp,NativeArrayOptions.UninitializedMemory))
                    {data.GetVertices(positions);vertices=positions.ToArray();}
                    int count=data.GetSubMesh(submesh).indexCount;
                    using(var triangles=new NativeArray<int>(count,Allocator.Temp,NativeArrayOptions.UninitializedMemory))
                    {data.GetIndices(triangles,submesh,true);indices=triangles.ToArray();}
                }
                return vertices!=null&&indices!=null&&indices.Length%3==0;
            }
            catch(Exception){vertices=null;indices=null;return false;}
        }
        static Color32[] Take(Camera camera,UniversalRenderPipeline.SingleCameraRequest request,RenderTexture target,Texture2D readable,string name,Vector3 position,Vector3 look,MeshRenderer[] renderers,Receipt r,List<Image> records)
        {
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(look-position,Vector3.up));
            var planes=GeometryUtility.CalculateFrustumPlanes(camera);int count=0;long triangles=0;
            foreach(var renderer in renderers)if(renderer.enabled&&renderer.gameObject.activeInHierarchy&&GeometryUtility.TestPlanesAABB(planes,renderer.bounds))
            {count++;var f=renderer.GetComponent<MeshFilter>();if(f!=null&&f.sharedMesh!=null)for(int sub=0;sub<f.sharedMesh.subMeshCount;sub++)triangles+=(long)f.sharedMesh.GetIndexCount(sub)/3;}
            var clock=Stopwatch.StartNew();RenderTexture.active=target;GL.Clear(true,true,Color.magenta);RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=target;readable.ReadPixels(new Rect(0,0,r.width,r.height),0,0,false);readable.Apply(false,false);Color32[] pixels=readable.GetPixels32();clock.Stop();
            int magenta=0;foreach(var c in pixels)if(c.r>200&&c.g<60&&c.b>150)magenta++;
            if(magenta>pixels.Length*.95)throw new InvalidOperationException("Unrendered/error-colored image: "+name);
            byte[] png=readable.EncodeToPNG();string file=name+".png";File.WriteAllBytes(Path.Combine(r.outputDirectory,file),png);
            records.Add(new Image{name=name,file=file,sha256=Hash(png),position=position,lookAt=look,frame=Time.frameCount,magentaPixels=magenta,
                frustumCandidateRenderers=count,frustumCandidateTriangles=triangles,synchronousSubmitAndReadbackMilliseconds=clock.Elapsed.TotalMilliseconds});return pixels;
        }
        static Delta Compare(Color32[] a,Color32[] b)
        {
            double sum=0,luma=0;float maximum=0;int changed=0;
            for(int i=0;i<a.Length;i++){int dr=Math.Abs(a[i].r-b[i].r),dg=Math.Abs(a[i].g-b[i].g),db=Math.Abs(a[i].b-b[i].b);int max=Math.Max(dr,Math.Max(dg,db));if(max>1)changed++;maximum=Mathf.Max(maximum,max/255f);sum+=(dr+dg+db)/(255d*3);luma+=Luma(a[i])-Luma(b[i]);}
            return new Delta{pixels=a.Length,changedPixels=changed,meanAbsoluteRgbDelta=(float)(sum/a.Length),maximumAbsoluteRgbDelta=maximum,meanSignedLumaDelta=(float)(luma/a.Length)};
        }
        static Receiver[] Samples(Camera camera,List<Surface> surfaces,Color32[] on,Color32[] off,int width,int height,Receipt receipt,Light first,Light second)
        {
            var result=new List<Receiver>(48);var probes=new List<ReceiverProbe>(48);
            for(int y=1;y<=6;y++)for(int x=1;x<=8;x++)
            {
                receipt.receiverProbeRays++;float u=x/9f,v=y/7f;Ray ray=camera.ViewportPointToRay(new Vector3(u,v,0));float nearest=camera.farClipPlane;Surface found=null;Vector3 normal=Vector3.zero;
                foreach(var surface in surfaces)
                {
                    if(!surface.Bounds.IntersectRay(ray,out float box)||box>nearest)continue;
                    for(int j=0;j<surface.Triangles.Length;j+=3)
                    {Vector3 a=surface.Vertices[surface.Triangles[j]],b=surface.Vertices[surface.Triangles[j+1]],c=surface.Vertices[surface.Triangles[j+2]];
                        if(Triangle(ray,a,b,c,out float distance)&&distance<nearest){nearest=distance;found=surface;normal=Vector3.Cross(b-a,c-a).normalized;}}
                }
                int px=Mathf.Clamp((int)(u*width),0,width-1),py=Mathf.Clamp((int)(v*height),0,height-1),index=py*width+px;
                if(found==null){receipt.receiverProbeMisses++;probes.Add(new ReceiverProbe{reason="NO_WORLD_TRIANGLE",pixel=new Vector2Int(px,py)});continue;}
                Vector3 point=ray.GetPoint(nearest);
                probes.Add(new ReceiverProbe{renderer=found.Renderer.name,shader=found.Shader,material=found.Material,reason=found.Reason,
                    eligible=found.Eligible,point=point,pixel=new Vector2Int(px,py)});
                if(!found.Eligible){receipt.receiverProbeIneligible++;continue;}
                receipt.receiverProbeEligible++;Vector3 deltaA=first.transform.position-point,deltaB=second.transform.position-point;
                float distanceA=deltaA.magnitude,distanceB=deltaB.magnitude;
                result.Add(new Receiver{renderer=found.Renderer.name,shader=found.Shader,material=found.Material,point=point,normal=normal,
                    pixel=new Vector2Int(px,py),onMinusOffLuma=Luma(on[index])-Luma(off[index]),emissionMaximum=found.Emission,
                    primaryDistance=distanceA,secondaryDistance=distanceB,withinPrimaryRange=distanceA<first.range,withinSecondaryRange=distanceB<second.range,
                    primaryFacing=distanceA>.0001f?Vector3.Dot(normal,deltaA/distanceA):0,secondaryFacing=distanceB>.0001f?Vector3.Dot(normal,deltaB/distanceB):0});
            }
            receipt.nearestReceiverProbes=probes.ToArray();return result.ToArray();
        }
        static bool Triangle(Ray ray,Vector3 a,Vector3 b,Vector3 c,out float distance)
        {
            distance=0;Vector3 e=b-a,f=c-a,p=Vector3.Cross(ray.direction,f);float det=Vector3.Dot(e,p);if(Mathf.Abs(det)<.0000001f)return false;
            float inverse=1f/det;Vector3 t=ray.origin-a;float u=Vector3.Dot(t,p)*inverse;if(u<0||u>1)return false;
            Vector3 q=Vector3.Cross(t,e);float v=Vector3.Dot(ray.direction,q)*inverse;if(v<0||u+v>1)return false;
            distance=Vector3.Dot(f,q)*inverse;return distance>cameraNear;
        }
        const float cameraNear=.03f;
        static float Luma(Color32 c)=>(.2126f*c.r+.7152f*c.g+.0722f*c.b)/255f;
        static string Hash(byte[] data){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
    }
}
#endif
