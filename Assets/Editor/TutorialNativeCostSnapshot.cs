#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit root-run Editor observation. Scene metadata is sampled cold once; optional frame
    // capture reuses its fixed renderer/light arrays and at most240 value samples. Two owned
    // ProfilerRecorders are disposed on finish/cancel/reload. No scene/prefs/render policy writes.
    public static class TutorialNativeCostSnapshot
    {
        [Serializable] public sealed class Geometry
        {
            public int Renderers,EnabledRenderers,MeshRenderers,SkinnedRenderers,UniqueMeshes,UniqueMaterials;
            public int SubmeshReferences,MaterialSlots,MissingMeshRenderers,UnsupportedRenderers;
            public long ReferencedVertices,ReferencedTrianglePrimitives,UniqueVertices,UniqueTrianglePrimitives,NonTriangleIndices;
        }
        [Serializable] public sealed class RendererRecord
        {
            public string Name,Mesh;public bool Active,Enabled,ForceRenderingOff,InWorld,ShadowCaster,ReceivesShadows;
            public int Vertices,Submeshes,MaterialSlots;public long TrianglePrimitives;public Vector3 BoundsCenter,BoundsSize,LocalBoundsSize;
        }
        [Serializable] public struct GraphicsMode
        {
            public string Preset,ResolutionMode;public int PresetIndex,AdditionalLightsPerObject,FrameCap,ParticleQuality,ShadowMapResolution;
            public float ResolutionScale,ShadowDistance,RenderDistanceMetres;public bool Bloom,OpaqueTexture;
        }
        [Serializable] public struct FrameSample
        {
            public int Frame;public double Realtime;public float IntervalMs;public bool Focused;
            public int EnabledRenderers,FrustumCandidates,EnabledLights,PositiveIntensityLights,ShadowedLights;
            public long LatestEngineDrawCalls,LatestEngineSetPassCalls;public GraphicsMode Graphics;
        }
        [Serializable] public sealed class Receipt
        {
            public string Status="snapshot",Utc,Scope="Current session scene, including inactive objects; world totals are a subset.",Error="";
            public string Meaning="Structural counts include inactive objects and are NOT draw calls. Frame observations reuse the initial renderer/light inventory; later spawned objects require a new capture. Frustum candidates are bounds tests only, without occlusion or per-pass selection. Engine counters include all cameras/shadows/UI and may be unavailable. Frame intervals include pacing/Editor/diagnostic overhead; no GPU bottleneck or FPS acceptance is inferred.";
            public string CounterFrameRelationship="Latest available completed engine counter at sampling time; not an isolated Game-camera draw count or proven same-frame pairing.";
            public bool NativeCapture=true,SceneOrPreferencesChanged=false,Complete=true;public ulong SceneHandle;public int Frame,PartitionCells,PartitionCellsOverFourMetres,UnknownMaterialSlots;
            public int Lights,EnabledLights,PositiveIntensityLights,ShadowedLights,PermanentFixtureLights,ActivePermanentFixtureLights,LegacyMovingLightsEnabled;
            public double SnapshotCpuMilliseconds;public Geometry Scene=new Geometry(),World=new Geometry();public GraphicsMode Graphics;
            public RendererRecord[] Renderers;public FrameSample[] Frames=Array.Empty<FrameSample>();
        }
        sealed class CaptureWork
        {
            public GameSession Session;public DungeonWorld World;public Renderer[] Renderers;public Light[] Lights;public Camera Camera;public Plane[] Planes=new Plane[6];
            public Receipt Receipt;public FrameSample[] Samples;public int Count,LastFrame=-1;public string Path;public double Started;
            public ProfilerRecorder Draws,Passes;
        }
        static CaptureWork pending;static string lastStatus="NOT STARTED";
        public static bool IsRunning=>pending!=null;
        public static string GetStatus()=>pending==null?lastStatus:"RUNNING "+pending.Count+"/"+pending.Samples.Length+" distinct engine frames";
        public static string Capture()
        {GameSession session=RequireSession();return JsonUtility.ToJson(Snapshot(session,out _,out _),true);}
        public static string CaptureToFile(string absoluteNewJsonPath)
        {
            string path=ValidatePath(absoluteNewJsonPath);GameSession session=RequireSession();var receipt=Snapshot(session,out _,out _);
            Write(path,receipt);return "WROTE structural snapshot: "+path+"; no GPU/FPS acceptance claim.";
        }
        public static string BeginFrameCapture(string absoluteNewJsonPath,int frames=60)
        {
            if(pending!=null)return "REFUSED: capture already running.";
            string path=ValidatePath(absoluteNewJsonPath);GameSession session=RequireSession();var report=Snapshot(session,out Renderer[] renderers,out Light[] lights);
            Camera camera=session.Opening!=null?session.Opening.ActiveCamera:null;if(camera==null&&session.Player!=null)camera=session.Player.ViewCamera;
            var work=new CaptureWork {Session=session,World=session.World,Renderers=renderers,Lights=lights,Camera=camera,Receipt=report,Path=path,
                Samples=new FrameSample[Mathf.Clamp(frames,1,240)],Started=EditorApplication.timeSinceStartup};
            work.Draws=StartCounter("Draw Calls Count");work.Passes=StartCounter("SetPass Calls Count");pending=work;
            EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Reload;EditorApplication.playModeStateChanged+=PlayState;
            return "STARTED: fixed-array frame observation; root should keep scene/camera/preset context deliberate. Poll GetStatus().";
        }
        public static void Cancel(){if(pending!=null)Finish(false,"cancelled");}
        static void Reload(){if(pending!=null)Finish(false,"assembly reload");}
        static void PlayState(PlayModeStateChange x){if(pending!=null&&(x==PlayModeStateChange.ExitingPlayMode||x==PlayModeStateChange.EnteredEditMode))Finish(false,"Play ended");}
        static GameSession RequireSession()
        {if(!Application.isPlaying||GameSession.Instance==null||GameSession.Instance.World==null)throw new InvalidOperationException("Native prepared session required.");return GameSession.Instance;}
        static string ValidatePath(string path)
        {
            if(string.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path))throw new ArgumentException("New absolute .json path outside Assets required.");
            path=Path.GetFullPath(path);string assets=Path.GetFullPath(Application.dataPath)+Path.DirectorySeparatorChar;
            if(path.StartsWith(assets,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".json",StringComparison.OrdinalIgnoreCase)||File.Exists(path)||!Directory.Exists(Path.GetDirectoryName(path)))throw new ArgumentException("Existing directory and new .json filename outside Assets required.");
            return path;
        }
        static Receipt Snapshot(GameSession session,out Renderer[] renderers,out Light[] lights)
        {
            var watch=Stopwatch.StartNew();var result=new Receipt {Utc=DateTime.UtcNow.ToString("o"),SceneHandle=session.gameObject.scene.handle.GetRawData(),Frame=Time.frameCount,Graphics=Mode(session)};
            var sceneRenderers=new List<Renderer>();var records=new List<RendererRecord>();var sceneLights=new List<Light>();
            var sceneMeshes=new HashSet<Mesh>();var worldMeshes=new HashSet<Mesh>();var sceneMaterials=new HashSet<Material>();var worldMaterials=new HashSet<Material>();
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if(renderer.gameObject.scene!=session.gameObject.scene)continue;sceneRenderers.Add(renderer);bool world=renderer.transform.IsChildOf(session.World.transform);
                Mesh mesh=null;if(renderer is MeshRenderer){var filter=renderer.GetComponent<MeshFilter>();if(filter!=null)mesh=filter.sharedMesh;}
                else if(renderer is SkinnedMeshRenderer skin)mesh=skin.sharedMesh;
                Material[] materials=renderer.sharedMaterials;Count(result.Scene,sceneMeshes,sceneMaterials,renderer,mesh,materials);
                if(world)Count(result.World,worldMeshes,worldMaterials,renderer,mesh,materials);
                var row=new RendererRecord {Name=renderer.name,Mesh=mesh!=null?mesh.name:"<none>",Active=renderer.gameObject.activeInHierarchy,Enabled=renderer.enabled,
                    ForceRenderingOff=renderer.forceRenderingOff,InWorld=world,ShadowCaster=renderer.shadowCastingMode!=ShadowCastingMode.Off,
                    ReceivesShadows=renderer.receiveShadows,MaterialSlots=materials.Length,BoundsCenter=renderer.bounds.center,BoundsSize=renderer.bounds.size};
                foreach(var material in materials)if(material==null)result.UnknownMaterialSlots++;
                if(mesh!=null){row.Vertices=mesh.vertexCount;row.Submeshes=mesh.subMeshCount;row.LocalBoundsSize=mesh.bounds.size;row.TrianglePrimitives=Triangles(mesh,out _);}
                if(world&&renderer.name.StartsWith("Tutorial light cell ",StringComparison.Ordinal)){
                    result.PartitionCells++;if(mesh==null||mesh.bounds.size.x>4.0001f||mesh.bounds.size.z>4.0001f)result.PartitionCellsOverFourMetres++;
                }
                records.Add(row);
            }
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if(light.gameObject.scene!=session.gameObject.scene)continue;sceneLights.Add(light);result.Lights++;
                if(light.isActiveAndEnabled){result.EnabledLights++;if(light.intensity>0)result.PositiveIntensityLights++;if(light.shadows!=LightShadows.None)result.ShadowedLights++;}
                if(light.name.StartsWith("Permanent tutorial fire spill ",StringComparison.Ordinal)){result.PermanentFixtureLights++;if(light.isActiveAndEnabled)result.ActivePermanentFixtureLights++;}
                if(light.name.StartsWith("Reused tutorial fire spill ",StringComparison.Ordinal)&&light.isActiveAndEnabled)result.LegacyMovingLightsEnabled++;
            }
            result.Renderers=records.ToArray();renderers=sceneRenderers.ToArray();lights=sceneLights.ToArray();result.SnapshotCpuMilliseconds=watch.Elapsed.TotalMilliseconds;return result;
        }
        static void Count(Geometry count,HashSet<Mesh> meshes,HashSet<Material> uniqueMaterials,Renderer renderer,Mesh mesh,Material[] materials)
        {
            count.Renderers++;if(renderer.enabled&&renderer.gameObject.activeInHierarchy&&!renderer.forceRenderingOff)count.EnabledRenderers++;
            if(renderer is MeshRenderer)count.MeshRenderers++;else if(renderer is SkinnedMeshRenderer)count.SkinnedRenderers++;else count.UnsupportedRenderers++;
            count.MaterialSlots+=materials.Length;foreach(var material in materials)if(material!=null&&uniqueMaterials.Add(material))count.UniqueMaterials++;
            if(mesh==null){if(renderer is MeshRenderer||renderer is SkinnedMeshRenderer)count.MissingMeshRenderers++;return;}
            count.SubmeshReferences+=mesh.subMeshCount;count.ReferencedVertices+=mesh.vertexCount;long triangles=Triangles(mesh,out long other);
            count.ReferencedTrianglePrimitives+=triangles;count.NonTriangleIndices+=other;
            if(meshes.Add(mesh)){count.UniqueMeshes++;count.UniqueVertices+=mesh.vertexCount;count.UniqueTrianglePrimitives+=triangles;}
        }
        static long Triangles(Mesh mesh,out long nonTriangles)
        {long count=0;nonTriangles=0;for(int i=0;i<mesh.subMeshCount;i++){long indices=(long)mesh.GetIndexCount(i);if(mesh.GetTopology(i)==MeshTopology.Triangles)count+=indices/3;else nonTriangles+=indices;}return count;}
        static GraphicsMode Mode(GameSession session)
        {
            var x=session!=null?session.Graphics:null;if(x==null)return new GraphicsMode {Preset="unavailable",ResolutionMode="unavailable"};
            return new GraphicsMode {Preset=x.PresetName,PresetIndex=x.Preset,ResolutionMode=x.ResolutionMode,ResolutionScale=x.EffectiveResolutionScale,
                AdditionalLightsPerObject=x.AdditionalLightsPerObject,FrameCap=x.FrameRateCap,ParticleQuality=x.ParticleQuality,ShadowDistance=x.ShadowDistance,
                ShadowMapResolution=x.ShadowMapResolution,Bloom=x.BloomEnabled,OpaqueTexture=x.OpaqueTextureEnabled,RenderDistanceMetres=x.EffectiveRenderDistanceMetres};
        }
        static ProfilerRecorder StartCounter(string name)
        {try{return ProfilerRecorder.StartNew(ProfilerCategory.Render,name,1);}catch{return default;}}
        static long Read(ProfilerRecorder x)=>x.Valid&&x.IsRunning&&x.Count>0?Math.Max(-1,x.LastValue):-1;
        static void Tick()
        {
            var w=pending;if(w==null)return;
            try{
                if(!Application.isPlaying||w.Session==null||w.World==null||w.Session.World!=w.World||w.Session.gameObject.scene.handle.GetRawData()!=w.Receipt.SceneHandle){Finish(false,"session/world/scene changed");return;}
                if(EditorApplication.timeSinceStartup-w.Started>60){Finish(false,"60-second budget exhausted; engine frames may not advance while backgrounded");return;}
                if(Time.frameCount==w.LastFrame)return;w.LastFrame=Time.frameCount;
                bool camera=w.Camera!=null;if(camera)GeometryUtility.CalculateFrustumPlanes(w.Camera,w.Planes);
                var sample=new FrameSample {Frame=Time.frameCount,Realtime=Time.realtimeSinceStartupAsDouble,IntervalMs=Time.unscaledDeltaTime*1000,Focused=Application.isFocused,
                    LatestEngineDrawCalls=Read(w.Draws),LatestEngineSetPassCalls=Read(w.Passes),Graphics=Mode(w.Session)};
                foreach(var renderer in w.Renderers)if(renderer!=null&&renderer.enabled&&renderer.gameObject.activeInHierarchy&&!renderer.forceRenderingOff){
                    sample.EnabledRenderers++;if(camera&&(w.Camera.cullingMask&(1<<renderer.gameObject.layer))!=0&&GeometryUtility.TestPlanesAABB(w.Planes,renderer.bounds))sample.FrustumCandidates++;}
                foreach(var light in w.Lights)if(light!=null&&light.isActiveAndEnabled){sample.EnabledLights++;if(light.intensity>0)sample.PositiveIntensityLights++;if(light.shadows!=LightShadows.None)sample.ShadowedLights++;}
                w.Samples[w.Count++]=sample;if(w.Count==w.Samples.Length)Finish(true,"");
            }catch(Exception error){Finish(false,error.GetType().Name+": "+error.Message);}
        }
        static void Finish(bool complete,string error)
        {
            var w=pending;if(w==null)return;pending=null;EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=Reload;EditorApplication.playModeStateChanged-=PlayState;
            try{w.Receipt.Frames=new FrameSample[w.Count];Array.Copy(w.Samples,w.Receipt.Frames,w.Count);w.Receipt.Complete=complete;w.Receipt.Error=error;
                w.Receipt.Status=complete?"frame-observation-complete":"frame-observation-incomplete";Write(w.Path,w.Receipt);lastStatus=w.Receipt.Status+": "+w.Path;
            }catch(Exception failure){lastStatus="WRITE FAILED: "+failure.Message;}
            finally{if(w.Draws.Valid)w.Draws.Dispose();if(w.Passes.Valid)w.Passes.Dispose();}
        }
        static void Write(string path,Receipt receipt)
        {using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))using(var writer=new StreamWriter(file))writer.Write(JsonUtility.ToJson(receipt,true));}
    }
}
#endif
