#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit Editor diagnostic, invoked only by root on Unity's main thread outside rendering callbacks.
    // Reads the live camera and metre-space environment. Five bounded offscreen URP requests temporarily
    // toggle existing point-Light enabled flags only; a finally block restores every original flag.
    // No runtime component, player/world mutation, scene/asset save, new Light, physics object or private API.
    public static class TutorialLightContributionChecks
    {
        static readonly string[] ChandelierNames={"Arrival torch pool","Far east torch pool","Far chandelier centered spill"};
        [Serializable] public sealed class LightRecord
        {
            public string name,group,bakeType,shadows,renderMode,cullingMask,renderingLayerMask;
            public Vector3 position;
            public Color color;
            public float intensity,range;
            public bool enabled,active;
        }
        [Serializable] public sealed class RendererRecord
        {
            public string name,shader,renderingLayerMask;
            public Vector3 center,size;
            public bool receiveShadows,shaderSupported;
        }
        [Serializable] public sealed class CaptureRecord
        {
            public string group,file;
            public int enabledPointLights;
            public bool clusterKeyword,additionalPixelKeyword,additionalVertexKeyword;
        }
        [Serializable] public sealed class Difference
        {
            public string name,meaning;
            public int pixelCount,changedPixels;
            public float meanAbsoluteRgbDelta,maximumAbsoluteRgbDelta;
        }
        [Serializable] public sealed class SurfaceSample
        {
            public string collider,orientation;
            public Vector3 point,normal;
            public Vector2Int pixel;
            public float allMinusNoPointLuma,poolMinusNoPointLuma,chandelierMinusNoPointLuma,campfireMinusNoPointLuma;
        }
        [Serializable] public sealed class Report
        {
            public string utc,status,graphicsApi,pipeline,rendererType,additionalMode,outputDirectory;
            public string interpretation="Images quantify this camera's current LDR response, not physical illuminance, browser performance or visual acceptance. Emission, ambient/main light, fog and postprocessing stay unchanged.";
            public Vector3 cameraPosition,cameraForward;
            public bool computeSupported,allOriginalLightFlagsRestored;
            public int frame,width,height,perObjectAdditionalLimit,activeMeshRenderers;
            public LightRecord[] lights;
            public RendererRecord[] largestMeshBounds;
            public CaptureRecord[] captures;
            public Difference[] differences;
            public SurfaceSample[] surfaceSamples;
        }
        sealed class Entry { public Light Light; public bool Enabled; public string Group; }

        /// <summary>Optional root-selected framing/intensity probe. Restores camera pose and existing chandelier intensities even if capture fails.</summary>
        public static string CaptureFramed(Camera camera,Transform environmentRoot,string outputDirectory,Vector3 position,Vector3 lookAt,
            float chandelierIntensity=-1f,int width=960,int height=540)
        {
            if(camera==null||environmentRoot==null)throw new ArgumentException("Live camera/environment required.");
            if(!Finite(position)||!Finite(lookAt)||(lookAt-position).sqrMagnitude<.001f)
                throw new ArgumentException("Finite, distinct camera position and look-at are required.");
            if(chandelierIntensity!=-1f&&chandelierIntensity!=18f&&chandelierIntensity!=30f)
                throw new ArgumentException("Only unchanged (-1), 18 or 30 are permitted for this bounded existing-light probe.");
            Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
            var lamps=new List<Light>();var intensities=new List<float>();
            foreach(var light in environmentRoot.GetComponentsInChildren<Light>(true))if(light!=null&&Group(light.name)=="chandelier")
            {lamps.Add(light);intensities.Add(light.intensity);}
            try
            {
                camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(lookAt-position,Vector3.up));
                if(chandelierIntensity>0f)foreach(var light in lamps)light.intensity=chandelierIntensity;
                return Capture(camera,environmentRoot,outputDirectory,width,height);
            }
            finally
            {
                for(int i=0;i<lamps.Count;i++)if(lamps[i]!=null)lamps[i].intensity=intensities[i];
                if(camera!=null)camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
            }
        }

        /// <summary>Root supplies the actual active base camera and World transform. Call between frames, never from an SRP render callback.</summary>
        public static string Capture(Camera camera,Transform environmentRoot,string outputDirectory,int width=960,int height=540)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Requires the real running tutorial, not an edit-time synthetic scene.");
            if(camera==null||!camera.isActiveAndEnabled||environmentRoot==null||!environmentRoot.gameObject.activeInHierarchy)
                throw new ArgumentException("Supply an enabled live camera and its active environment root.");
            if(camera.cameraType!=CameraType.Game)throw new ArgumentException("Use the actual Game camera.");
            var cameraData=camera.GetComponent<UniversalAdditionalCameraData>();
            if(cameraData==null||cameraData.renderType!=CameraRenderType.Base)
                throw new ArgumentException("Use the existing URP base camera; no camera component is added by this fixture.");
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline==null)throw new InvalidOperationException("The active pipeline is not URP.");
            if(string.IsNullOrWhiteSpace(outputDirectory)||!Path.IsPathRooted(outputDirectory))
                throw new ArgumentException("Supply an absolute root-approved diagnostic output folder.");
            width=Mathf.Clamp(width,320,1280);height=Mathf.Clamp(height,180,720);
            var request=new UniversalRenderPipeline.SingleCameraRequest();
            if(!RenderPipeline.SupportsRenderRequest(camera,request))
                throw new InvalidOperationException("This live renderer does not support the public SingleCameraRequest; no fallback Camera.Render is attempted.");
            string directory=Path.GetFullPath(outputDirectory);Directory.CreateDirectory(directory);
            var entries=new List<Entry>();var lightRecords=new List<LightRecord>();
            foreach(Light light in environmentRoot.GetComponentsInChildren<Light>(true))
            {
                if(light==null||light.type!=LightType.Point)continue;
                string group=Group(light.name);entries.Add(new Entry {Light=light,Enabled=light.enabled,Group=group});
                lightRecords.Add(new LightRecord {name=light.name,group=group,position=light.transform.position,color=light.color,
                    intensity=light.intensity,range=light.range,enabled=light.enabled,active=light.gameObject.activeInHierarchy,
                    bakeType=light.lightmapBakeType.ToString(),shadows=light.shadows.ToString(),renderMode=light.renderMode.ToString(),
                    cullingMask="0x"+light.cullingMask.ToString("X8"),renderingLayerMask="0x"+light.renderingLayerMask.ToString("X8")});
            }
            if(entries.Count==0)throw new InvalidOperationException("No actual environment point lights were found.");
            foreach(string expected in ChandelierNames)
            {
                bool found=false;foreach(var entry in entries)if(entry.Light.name==expected&&entry.Enabled&&entry.Light.gameObject.activeInHierarchy)found=true;
                if(!found)throw new InvalidOperationException("Expected live chandelier Light missing or disabled: "+expected+". No zero-contribution conclusion is made.");
            }
            var renderers=new List<MeshRenderer>();
            foreach(var renderer in environmentRoot.GetComponentsInChildren<MeshRenderer>(false))
                if(renderer!=null&&renderer.enabled&&renderer.gameObject.activeInHierarchy)renderers.Add(renderer);
            renderers.Sort((a,b)=>b.bounds.size.sqrMagnitude.CompareTo(a.bounds.size.sqrMagnitude));
            var rendererRecords=new List<RendererRecord>();
            for(int i=0;i<Math.Min(16,renderers.Count);i++)
            {
                var renderer=renderers[i];var material=renderer.sharedMaterial;var shader=material!=null?material.shader:null;
                rendererRecords.Add(new RendererRecord {name=renderer.name,shader=shader!=null?shader.name:"<missing>",
                    center=renderer.bounds.center,size=renderer.bounds.size,receiveShadows=renderer.receiveShadows,
                    shaderSupported=shader!=null&&shader.isSupported,renderingLayerMask="0x"+renderer.renderingLayerMask.ToString("X8")});
            }
            var report=new Report {utc=DateTime.UtcNow.ToString("o"),status="capturing",frame=Time.frameCount,
                width=width,height=height,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),computeSupported=SystemInfo.supportsComputeShaders,
                pipeline=pipeline.name,rendererType=cameraData.scriptableRenderer.GetType().FullName,
                additionalMode=pipeline.additionalLightsRenderingMode.ToString(),perObjectAdditionalLimit=pipeline.maxAdditionalLightsCount,
                cameraPosition=camera.transform.position,cameraForward=camera.transform.forward,outputDirectory=directory,
                activeMeshRenderers=renderers.Count,lights=lightRecords.ToArray(),largestMeshBounds=rendererRecords.ToArray()};
            var captureRecords=new List<CaptureRecord>();var images=new Color32[5][];
            string[] groups={"all","no-point","fire-pool-only","chandelier-only","campfire-only"};
            RenderTexture target=null;Texture2D readable=null;RenderTexture previousActive=RenderTexture.active;
            RenderTexture previousCameraTarget=camera.targetTexture;CameraType previousCameraType=camera.cameraType;
            try
            {
                target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,1);
                readable=new Texture2D(width,height,TextureFormat.RGBA32,false,false);
                request.destination=target;
                for(int group=0;group<groups.Length;group++)
                {
                    int enabled=0;
                    foreach(Entry entry in entries)
                    {
                        if(entry.Light==null)throw new InvalidOperationException("An environment light was destroyed during synchronous capture.");
                        bool include=group==0||group==2&&entry.Group=="fire-pool"||group==3&&entry.Group=="chandelier"||group==4&&entry.Group=="campfire";
                        entry.Light.enabled=entry.Enabled&&include;
                        if(entry.Light.enabled&&entry.Light.gameObject.activeInHierarchy)enabled++;
                    }
                    RenderTexture.active=target;GL.Clear(true,true,new Color(1f,0f,1f,1f));
                    RenderPipeline.SubmitRenderRequest(camera,request);
                    RenderTexture.active=target;
                    readable.ReadPixels(new Rect(0,0,width,height),0,0,false);readable.Apply(false,false);
                    images[group]=readable.GetPixels32();
                    ValidateRenderedImage(images[group],groups[group]);
                    string file=groups[group]+".png";File.WriteAllBytes(Path.Combine(directory,file),readable.EncodeToPNG());
                    captureRecords.Add(new CaptureRecord {group=groups[group],file=file,enabledPointLights=enabled,
                        clusterKeyword=Shader.IsKeywordEnabled("_CLUSTER_LIGHT_LOOP"),
                        additionalPixelKeyword=Shader.IsKeywordEnabled("_ADDITIONAL_LIGHTS"),
                        additionalVertexKeyword=Shader.IsKeywordEnabled("_ADDITIONAL_LIGHTS_VERTEX")});
                }
                report.captures=captureRecords.ToArray();
                var differences=new Difference[4];
                for(int i=0;i<4;i++)
                {
                    int image=i==0?0:i+1;differences[i]=Compare(images[image],images[1],groups[image]+" vs no-point");
                }
                report.differences=differences;
                var heat=new Color32[width*height];
                for(int i=0;i<heat.Length;i++)
                {
                    float d=MaxDelta(images[0][i],images[1][i]);byte value=(byte)Mathf.Clamp(Mathf.RoundToInt(d*255f*4f),0,255);
                    heat[i]=new Color32(value,(byte)(value/2),0,255);
                }
                readable.SetPixels32(heat);readable.Apply(false,false);
                File.WriteAllBytes(Path.Combine(directory,"all-minus-no-point-4x.png"),readable.EncodeToPNG());
                report.surfaceSamples=SampleActualSurfaces(camera,environmentRoot,images,width,height);
                report.status="captured; native contribution and visual interpretation require root review";
            }
            finally
            {
                foreach(Entry entry in entries)if(entry.Light!=null)entry.Light.enabled=entry.Enabled;
                if(camera!=null){camera.targetTexture=previousCameraTarget;camera.cameraType=previousCameraType;}
                RenderTexture.active=previousActive;
                if(readable!=null)UnityEngine.Object.DestroyImmediate(readable);
                if(target!=null)RenderTexture.ReleaseTemporary(target);
                report.allOriginalLightFlagsRestored=true;
                foreach(Entry entry in entries)if(entry.Light==null||entry.Light.enabled!=entry.Enabled)report.allOriginalLightFlagsRestored=false;
            }
            if(!report.allOriginalLightFlagsRestored)throw new InvalidOperationException("The original light enabled flags could not all be restored.");
            string json=JsonUtility.ToJson(report,true);File.WriteAllText(Path.Combine(directory,"light-contribution.json"),json);
            return json;
        }

        static string Group(string name)
        {
            if(name.StartsWith("Reused tutorial fire spill ",StringComparison.Ordinal)||name.StartsWith("Permanent tutorial fire spill ",StringComparison.Ordinal))return "fire-pool";
            if(name=="Arrival torch pool"||name=="Far east torch pool"||name=="Far chandelier centered spill")return "chandelier";
            return name=="Campfire and crypt torch pool"?"campfire":"other";
        }
        static bool Finite(Vector3 v) {return !float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);}
        static void ValidateRenderedImage(Color32[] pixels,string group)
        {
            int nonSentinel=0;float minimum=1f,maximum=0f;
            foreach(Color32 pixel in pixels)
            {
                if(pixel.r!=255||pixel.g!=0||pixel.b!=255)nonSentinel++;
                float value=Luma(pixel);minimum=Mathf.Min(minimum,value);maximum=Mathf.Max(maximum,value);
            }
            if(nonSentinel<128||maximum-minimum<.005f)
                throw new InvalidOperationException("RenderRequest output is sentinel/flat or unusable for "+group+"; no zero-light-contribution claim is made.");
        }
        static float MaxDelta(Color32 a,Color32 b)
        {return Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b))/255f;}
        static float Luma(Color32 a) {return (.2126f*a.r+.7152f*a.g+.0722f*a.b)/255f;}
        static Difference Compare(Color32[] a,Color32[] b,string name)
        {
            double total=0;float maximum=0;int changed=0;
            for(int i=0;i<a.Length;i++)
            {
                float d=MaxDelta(a[i],b[i]);maximum=Mathf.Max(maximum,d);if(d>1f/255f)changed++;
                total+=(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b))/(255.0*3.0);
            }
            return new Difference {name=name,meaning="Absolute LDR RGB differences; includes lit objects, bloom and tone mapping. Not a direct lux measurement.",
                pixelCount=a.Length,changedPixels=changed,meanAbsoluteRgbDelta=(float)(total/a.Length),maximumAbsoluteRgbDelta=maximum};
        }
        static SurfaceSample[] SampleActualSurfaces(Camera camera,Transform root,Color32[][] images,int width,int height)
        {
            var result=new List<SurfaceSample>();
            for(int row=1;row<=6;row++)for(int column=1;column<=12;column++)
            {
                float u=column/13f,v=row/7f;
                if(!Physics.Raycast(camera.ViewportPointToRay(new Vector3(u,v,0)),out RaycastHit hit,60f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||
                    hit.collider==null||!hit.collider.transform.IsChildOf(root))continue;
                string orientation=Mathf.Abs(hit.normal.y)>.85f?"horizontal world collider":"vertical/angled world collider";
                int x=Mathf.Clamp(Mathf.FloorToInt(u*width),0,width-1),y=Mathf.Clamp(Mathf.FloorToInt(v*height),0,height-1),index=y*width+x;
                float baseline=Luma(images[1][index]);
                result.Add(new SurfaceSample {collider=hit.collider.name,orientation=orientation,point=hit.point,normal=hit.normal,pixel=new Vector2Int(x,y),
                    allMinusNoPointLuma=Luma(images[0][index])-baseline,poolMinusNoPointLuma=Luma(images[2][index])-baseline,
                    chandelierMinusNoPointLuma=Luma(images[3][index])-baseline,campfireMinusNoPointLuma=Luma(images[4][index])-baseline});
            }
            return result.ToArray();
        }
    }
}
#endif
