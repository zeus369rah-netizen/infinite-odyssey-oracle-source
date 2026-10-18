#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit root-only Editor entry points. Inspect reads one supplied imported asset and its referenced
    // meshes/materials/textures using public APIs; no generation, importer change, replacement or scene mutation.
    // Optional preview owns only temporary transform/mesh-renderer copies in PreviewRenderUtility's scene.
    // Shared imported resources remain asset-owned. All preview state/objects are cleaned in finally.
    public static class GeneratedPropChecks
    {
        const int Limit=256;
        const string PlaceholderPath="Packages/com.unity.ai.assistant/Modules/Unity.AI.Mesh/Meshes/cube.fbx";
        [Serializable] public sealed class SubmeshRecord {public int index;public string topology;public long indices,triangles;}
        [Serializable] public sealed class MeshRecord
        {
            public string name,path,guid,indexFormat;public long localFileId,runtimeReportedBytes,estimatedVertexIndexBufferBytes;
            public int vertices,submeshes,rendererReferences;public long triangles;public bool readable,hasNormals,hasUv0,installedPlaceholderMesh,submeshInventoryTruncated;
            public Vector3 boundsCenter,boundsSize;public SubmeshRecord[] sections;
        }
        [Serializable] public sealed class TextureRecord
        {
            public string name,path,guid,type,dimension,graphicsFormat,sourceSha256;
            public long localFileId,runtimeReportedBytes,sourceFileBytes;
            public int width,height,mipCount;public bool cpuReadable,readabilityKnown;
        }
        [Serializable] public sealed class TextureBinding {public string property,texturePath,textureName;}
        [Serializable] public sealed class MaterialRecord
        {
            public string name,path,guid,shader,renderPipelineTag,urpAssessment;public long localFileId,runtimeReportedBytes;
            public int shaderPasses;public bool shaderSupported,shaderHasErrors;public string[] passLightModes;public TextureBinding[] textures;
        }
        [Serializable] public sealed class RendererRecord
        {
            public string path,type,meshName,meshPath;public int vertices,submeshes,materialSlots,bones;public long triangles;
            public bool enabled,activeSelf,placeholderGeometry,placeholderMaterial;public Vector3 localPosition,localScale,localEuler;
        }
        [Serializable] public sealed class Report
        {
            public string utc,status,exception,assetPath,assetGuid,assetType,sourceSha256,importerType,modelCompression,unityVersion;
            public string inspectionMethod="Public imported AssetDatabase/mesh/material/texture metadata and transformed stored mesh bounds; no generation, reimport, retopology or scene placement.";
            public string generationEvidence="Inspection does not prove a generation job completed or credits were charged. Provider/API completion and actual usage must be recorded separately.";
            public string boundsMeaning="RootLocal bounds exclude the root's own TRS; AuthoredPlacement bounds include stored root TRS at its authored origin. No live scene object was placed.";
            public long sourceFileBytes,uniqueMeshTriangles,rendererInstanceTriangles,meshRuntimeReportedBytes,materialRuntimeReportedBytes,textureRuntimeReportedBytes,distinctTextureSourceBytes;
            public int rendererCount,uniqueMeshes,uniqueMaterials,uniqueTextures,embeddedMeshes,vertices,placeholderMeshReferences;
            public bool truncated,assetLoaded,exactInstalledPlaceholder,modelReadable,containsSkinnedMesh;
            public float importerGlobalScale;
            public Vector3 rootPosition,rootEuler,rootScale,rootLocalBoundsCenter,rootLocalBoundsSize,authoredPlacementBoundsCenter,authoredPlacementBoundsSize;
            public Vector3 rootLocalPivotToBottomCenter,authoredPlacementPivotToBottomCenter;
            public string[] labels,warnings;
            public MeshRecord[] meshes;public MaterialRecord[] materials;public TextureRecord[] textures;public RendererRecord[] renderers;
            public MeshRecord installedPlaceholder;
        }
        [Serializable] public sealed class PreviewReport
        {
            public string utc,status,exception,assetPath,pngPath,pngSha256;
            public bool cleaned,preservedSharedMaterials,convertedLinearToSrgb,allCopiesInPreviewScene;
            public int width,height,staticRenderers,contentPixels,rgbContentPixels,activeRenderers,frustumRenderers,cameraCullingMask;public string previewSceneHandle,cameraSceneHandle;public int[] cornerAlpha;
            public Vector3 authoredBoundsSize,cameraPosition,copiedBoundsCenter,copiedBoundsSize;public float cameraFieldOfView,cameraNearClip,cameraFarClip;public string targetFormat,targetGraphicsFormat;
        }

        public static string Inspect(string assetPath) {return JsonUtility.ToJson(InspectReport(assetPath),true);}

        /// <summary>Optional stopped-Editor preview only. Output must be an explicit PNG under this repository's work/ directory.</summary>
        public static string RenderPreview(string assetPath,string outputPng,int size=512)
        {
            var result=new PreviewReport {utc=DateTime.UtcNow.ToString("o"),assetPath=assetPath,status="not rendered"};
            PreviewRenderUtility preview=null;GameObject container=null;Texture2D image=null;
            RenderTexture previousTarget=RenderTexture.active;bool begun=false;
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
                    throw new InvalidOperationException("Optional isolated preview requires Play stopped and compilation/importing settled.");
                string path=NormalizeAssetPath(assetPath);var inventory=InspectReport(path);
                if(!inventory.assetLoaded||!string.IsNullOrEmpty(inventory.exception)||inventory.truncated)
                    throw new InvalidOperationException("Inspect the exact returned asset successfully before preview; partial/error inventory is not accepted.");
                if(inventory.exactInstalledPlaceholder)throw new InvalidOperationException("The exact installed blank cube remains; this is not a completed anvil preview.");
                if(inventory.containsSkinnedMesh)throw new InvalidOperationException("This static-prop preview refuses skinned meshes; no bind-pose image is presented as deformation validation.");
                GameObject asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(asset==null)throw new ArgumentException("Optional preview requires a static imported prefab/model root.");
                if(string.IsNullOrWhiteSpace(outputPng)||!Path.IsPathRooted(outputPng)||!outputPng.EndsWith(".png",StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Supply an absolute .png destination under work/.");
                string work=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..","work"));
                string output=Path.GetFullPath(outputPng);
                if(!output.StartsWith(work+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Preview output must stay under the current repository work/ directory; no Assets import is performed.");
                size=Mathf.Clamp(size,256,1024);result.width=result.height=size;result.pngPath=output;
                Vector3 center=inventory.authoredPlacementBoundsCenter,extent=inventory.authoredPlacementBoundsSize*.5f;
                if(!Finite(center)||!Finite(extent)||extent.sqrMagnitude<.00000001f)throw new InvalidOperationException("Finite nonempty authored placement bounds are required.");
                preview=new PreviewRenderUtility {ambientColor=new Color(.35f,.37f,.40f,1f)};
                container=new GameObject("Generated prop inspection preview only") {hideFlags=HideFlags.HideAndDontSave};
                container.transform.position=-center;
                var copied=new Dictionary<Transform,Transform>();int transforms=0;
                CopyTransformBranch(asset.transform,container.transform,copied,ref transforms);
                foreach(var source in asset.GetComponentsInChildren<Renderer>(true))
                {
                    if(!(source is MeshRenderer))throw new InvalidOperationException("Only static MeshRenderer components are previewed; imported behavior/particles/skins are never instantiated.");
                    var filter=source.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)throw new InvalidOperationException("Static renderer has no mesh.");
                    Material[] paints=source.sharedMaterials;foreach(var paint in paints)if(paint==null)throw new InvalidOperationException("Missing shared material; no automatic replacement is applied.");
                    var node=copied[source.transform];node.gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                    var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterials=paints;renderer.enabled=source.enabled;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    result.staticRenderers++;
                }
                if(result.staticRenderers==0)throw new InvalidOperationException("No static renderers to preview.");
                // Register the COMPLETE branch. The installed AI preview sample likewise
                // adds the completed instance; no later renderer is left outside that scene.
                preview.AddSingleGO(container);
                result.previewSceneHandle=container.scene.handle.GetRawData().ToString();result.cameraSceneHandle=preview.camera.gameObject.scene.handle.GetRawData().ToString();
                result.allCopiesInPreviewScene=true;
                foreach(var node in copied.Values)
                    if(node.gameObject.scene!=container.scene)result.allCopiesInPreviewScene=false;
                if(!result.allCopiesInPreviewScene)throw new InvalidOperationException("A copied transform is outside the owned preview scene; no empty image accepted.");
                result.preservedSharedMaterials=true;result.authoredBoundsSize=inventory.authoredPlacementBoundsSize;
                Camera camera=preview.camera;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                camera.allowHDR=false;camera.allowMSAA=false;camera.fieldOfView=35f;camera.nearClipPlane=.001f;
                camera.cullingMask=1;camera.useOcclusionCulling=false; // Only owned layer0 copies exist in this isolated scene.
                float distance=Mathf.Max(.02f,extent.magnitude/Mathf.Sin(camera.fieldOfView*.5f*Mathf.Deg2Rad)*1.25f);
                camera.farClipPlane=Mathf.Max(10f,distance+extent.magnitude*3f);
                camera.transform.position=new Vector3(1.35f,.90f,-1.70f).normalized*distance;camera.transform.LookAt(Vector3.zero,Vector3.up);
                result.cameraPosition=camera.transform.position;result.cameraFieldOfView=camera.fieldOfView;
                result.cameraNearClip=camera.nearClipPlane;result.cameraFarClip=camera.farClipPlane;result.cameraCullingMask=camera.cullingMask;
                var planes=GeometryUtility.CalculateFrustumPlanes(camera);bool hasCopiedBounds=false;Bounds copiedBounds=default;
                foreach(var renderer in container.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(!renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
                    result.activeRenderers++;
                    if(!hasCopiedBounds){copiedBounds=renderer.bounds;hasCopiedBounds=true;}else copiedBounds.Encapsulate(renderer.bounds);
                    if((camera.cullingMask&(1<<renderer.gameObject.layer))!=0 && GeometryUtility.TestPlanesAABB(planes,renderer.bounds))result.frustumRenderers++;
                }
                result.copiedBoundsCenter=copiedBounds.center;result.copiedBoundsSize=copiedBounds.size;
                if(result.activeRenderers==0 || result.frustumRenderers==0)throw new InvalidOperationException("Copied renderers are inactive or outside the actual preview frustum/mask; camera/bounds evidence retained.");
                var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=false;
                cameraData.antialiasing=AntialiasingMode.None;cameraData.requiresColorTexture=cameraData.requiresDepthTexture=false;
                preview.lights[0].transform.rotation=Quaternion.Euler(35,-35,0);preview.lights[0].intensity=1.25f;
                preview.lights[1].transform.rotation=Quaternion.Euler(320,145,0);preview.lights[1].intensity=.65f;
                foreach(var light in preview.lights)light.shadows=LightShadows.None;
                float points=size/Mathf.Max(.01f,EditorGUIUtility.pixelsPerPoint);
                preview.BeginPreview(new Rect(0,0,points,points),GUIStyle.none);begun=true;
                camera.pixelRect=new Rect(0,0,size,size);preview.Render(true,false);
                var target=preview.EndPreview() as RenderTexture;begun=false;
                if(target==null||target.width!=size||target.height!=size)throw new InvalidOperationException("Preview did not return the requested rendered target; no image accepted.");
                result.targetFormat=target.format.ToString();result.targetGraphicsFormat=target.graphicsFormat.ToString();
                RenderTexture.active=target;image=new Texture2D(size,size,TextureFormat.RGBA32,false,true) {hideFlags=HideFlags.HideAndDontSave};
                image.ReadPixels(new Rect(0,0,size,size),0,0,false);image.Apply(false,false);Color32[] pixels=image.GetPixels32();
                result.cornerAlpha=new[]{(int)pixels[0].a,pixels[size-1].a,pixels[(size-1)*size].a,pixels[size*size-1].a};
                result.convertedLinearToSrgb=QualitySettings.activeColorSpace==ColorSpace.Linear&&!target.sRGB;
                for(int i=0;i<pixels.Length;i++)
                {
                    Color32 pixel=pixels[i];if(pixel.a>0)result.contentPixels++;if(pixel.r>2||pixel.g>2||pixel.b>2)result.rgbContentPixels++;
                    if(result.convertedLinearToSrgb){pixel.r=GammaByte(pixel.r);pixel.g=GammaByte(pixel.g);pixel.b=GammaByte(pixel.b);pixels[i]=pixel;}
                }
                foreach(int alpha in result.cornerAlpha)if(alpha!=0)throw new InvalidOperationException("Preview corners are not transparent; no alpha-preserving PNG accepted.");
                if(result.contentPixels<128)throw new InvalidOperationException(result.rgbContentPixels>=128?"Preview has RGB content but no usable alpha; no alpha is fabricated and no PNG accepted.":"Preview has no significant RGBA content despite visible cloned bounds; native pipeline/render evidence remains unresolved.");
                if(result.convertedLinearToSrgb){image.SetPixels32(pixels);image.Apply(false,false);}
                Directory.CreateDirectory(Path.GetDirectoryName(output));byte[] png=image.EncodeToPNG();File.WriteAllBytes(output,png);
                using(var hash=SHA256.Create())result.pngSha256=BitConverter.ToString(hash.ComputeHash(png)).Replace("-","").ToLowerInvariant();
                result.cameraPosition=camera.transform.position;result.cameraFieldOfView=camera.fieldOfView;
                result.status="isolated static preview rendered; provenance, cost, scale/pivot and root visual acceptance still required";
            }
            catch(Exception e){result.status="preview failed; no asset accepted";result.exception=e.ToString();}
            finally
            {
                try{if(begun&&preview!=null)preview.EndPreview();}
                finally
                {
                    RenderTexture.active=previousTarget;
                    try{preview?.Cleanup();}
                    finally
                    {
                        if(container!=null)Object.DestroyImmediate(container);if(image!=null)Object.DestroyImmediate(image);
                        result.cleaned=container==null&&image==null;
                    }
                }
            }
            return JsonUtility.ToJson(result,true);
        }

        public static Report InspectReport(string assetPath)
        {
            var report=new Report {utc=DateTime.UtcNow.ToString("o"),status="not inspected",assetPath=assetPath,unityVersion=Application.unityVersion};
            var warnings=new List<string>();
            try
            {
                string path=NormalizeAssetPath(assetPath);report.assetPath=path;
                if(EditorApplication.isCompiling||EditorApplication.isUpdating)throw new InvalidOperationException("Wait for compilation/importing to settle; this fixture never forces an import.");
                Object main=AssetDatabase.LoadMainAssetAtPath(path);
                if(main==null)throw new InvalidOperationException("No imported asset exists at the supplied path. A UI submit is not completion evidence.");
                report.assetLoaded=true;report.assetType=main.GetType().FullName;report.assetGuid=AssetDatabase.AssetPathToGUID(path);
                report.labels=AssetDatabase.GetLabels(main);SourceInfo(path,out report.sourceFileBytes,out report.sourceSha256);
                var importer=AssetImporter.GetAtPath(path);report.importerType=importer!=null?importer.GetType().FullName:"<none>";
                if(importer is ModelImporter model){report.importerGlobalScale=model.globalScale;report.modelReadable=model.isReadable;report.modelCompression=model.meshCompression.ToString();}
                GameObject root=main as GameObject;Mesh direct=main as Mesh;
                if(root==null&&direct==null)throw new InvalidOperationException("Imported main asset is not a model prefab or Mesh. Raw GLTF/GLB without a suitable importer is not treated as generated geometry.");
                var cube=AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPath);
                var cubeFilter=cube!=null?cube.GetComponentInChildren<MeshFilter>(true):null;
                var cubeRenderer=cube!=null?cube.GetComponentInChildren<MeshRenderer>(true):null;
                Mesh placeholder=cubeFilter!=null?cubeFilter.sharedMesh:null;
                Material placeholderMaterial=cubeRenderer!=null?cubeRenderer.sharedMaterial:null;
                if(placeholder!=null)report.installedPlaceholder=DescribeMesh(placeholder,placeholder);
                else warnings.Add("Installed AI placeholder mesh was not available; no count-based placeholder guess is substituted.");
                var meshes=new Dictionary<Mesh,MeshRecord>();var materials=new Dictionary<Material,MaterialRecord>();var textures=new Dictionary<Texture,TextureRecord>();
                var rendererRecords=new List<RendererRecord>();bool hasBounds=false,hasPlacementBounds=false;Bounds localBounds=default,placementBounds=default;
                bool everyRendererPlaceholder=true;
                if(root!=null)
                {
                    report.rootPosition=root.transform.localPosition;report.rootEuler=root.transform.localEulerAngles;report.rootScale=root.transform.localScale;
                    Renderer[] renderers=root.GetComponentsInChildren<Renderer>(true);report.rendererCount=renderers.Length;
                    if(renderers.Length>Limit){report.truncated=true;warnings.Add("Renderer inventory exceeded256; no full acceptance can be inferred.");}
                    for(int i=0;i<Math.Min(Limit,renderers.Length);i++)
                    {
                        Renderer renderer=renderers[i];Mesh mesh=null;int bones=0;
                        if(renderer is SkinnedMeshRenderer skinned){mesh=skinned.sharedMesh;bones=skinned.bones.Length;report.containsSkinnedMesh=true;}
                        else if(renderer is MeshRenderer){var filter=renderer.GetComponent<MeshFilter>();if(filter!=null)mesh=filter.sharedMesh;}
                        if(mesh==null){warnings.Add("Renderer has no inspectable static/skinned mesh: "+HierarchyPath(renderer.transform,root.transform));everyRendererPlaceholder=false;continue;}
                        MeshRecord mr=AddMesh(mesh,placeholder,meshes,report);mr.rendererReferences++;
                        Material[] paints=renderer.sharedMaterials;bool cubePaint=placeholderMaterial!=null&&paints.Length==1&&paints[0]==placeholderMaterial;
                        bool cubeGeometry=placeholder!=null&&mesh==placeholder;everyRendererPlaceholder&=cubeGeometry&&cubePaint;
                        if(cubeGeometry)report.placeholderMeshReferences++;
                        foreach(Material paint in paints)if(paint!=null)AddMaterial(paint,materials,textures,report);
                        var rr=new RendererRecord {path=HierarchyPath(renderer.transform,root.transform),type=renderer.GetType().Name,meshName=mesh.name,
                            meshPath=AssetDatabase.GetAssetPath(mesh),vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,materialSlots=paints.Length,bones=bones,
                            triangles=mr.triangles,enabled=renderer.enabled,activeSelf=renderer.gameObject.activeSelf,placeholderGeometry=cubeGeometry,placeholderMaterial=cubePaint,
                            localPosition=renderer.transform.localPosition,localEuler=renderer.transform.localEulerAngles,localScale=renderer.transform.localScale};
                        rendererRecords.Add(rr);report.rendererInstanceTriangles+=mr.triangles;
                        if(paints.Length!=mesh.subMeshCount)warnings.Add(rr.path+": material slots differ from submesh count; inspect mapping/draw behavior.");
                        Matrix4x4 localFromMesh=root.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                        Matrix4x4 authoredFromMesh=Matrix4x4.TRS(report.rootPosition,root.transform.localRotation,report.rootScale)*localFromMesh;
                        Encapsulate(mesh.bounds,localFromMesh,ref localBounds,ref hasBounds);
                        Encapsulate(mesh.bounds,authoredFromMesh,ref placementBounds,ref hasPlacementBounds);
                    }
                    report.exactInstalledPlaceholder=rendererRecords.Count>0&&everyRendererPlaceholder&&!report.truncated;
                }
                else
                {
                    MeshRecord mr=AddMesh(direct,placeholder,meshes,report);report.rendererInstanceTriangles=mr.triangles;
                    localBounds=placementBounds=direct.bounds;hasBounds=true;report.rootScale=Vector3.one;
                    report.exactInstalledPlaceholder=placeholder!=null&&direct==placeholder;
                }
                Object[] embedded=AssetDatabase.LoadAllAssetsAtPath(path);
                foreach(Object value in embedded)if(value is Mesh mesh){report.embeddedMeshes++;AddMesh(mesh,placeholder,meshes,report);}
                if(hasBounds)
                {
                    report.rootLocalBoundsCenter=localBounds.center;report.rootLocalBoundsSize=localBounds.size;
                    report.authoredPlacementBoundsCenter=placementBounds.center;report.authoredPlacementBoundsSize=placementBounds.size;
                    report.rootLocalPivotToBottomCenter=new Vector3(localBounds.center.x,localBounds.min.y,localBounds.center.z);
                    report.authoredPlacementPivotToBottomCenter=new Vector3(placementBounds.center.x,placementBounds.min.y,placementBounds.center.z)-report.rootPosition;
                }
                report.meshes=Values(meshes);report.materials=Values(materials);report.textures=Values(textures);report.renderers=rendererRecords.ToArray();
                report.uniqueMeshes=meshes.Count;report.uniqueMaterials=materials.Count;report.uniqueTextures=textures.Count;
                foreach(MeshRecord mesh in report.meshes){report.vertices+=mesh.vertices;report.uniqueMeshTriangles+=mesh.triangles;report.meshRuntimeReportedBytes+=mesh.runtimeReportedBytes;}
                foreach(MaterialRecord material in report.materials)report.materialRuntimeReportedBytes+=material.runtimeReportedBytes;
                var textureFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(TextureRecord texture in report.textures){report.textureRuntimeReportedBytes+=texture.runtimeReportedBytes;if(textureFiles.Add(texture.path))report.distinctTextureSourceBytes+=texture.sourceFileBytes;}
                if(report.exactInstalledPlaceholder)warnings.Add("Exact installed AI blank cube mesh+material references remain. Do not accept this as a completed anvil.");
                else if(report.placeholderMeshReferences>0)warnings.Add("Some renderer geometry still references the installed blank cube; mixed/partial state requires inspection.");
                if(report.containsSkinnedMesh)warnings.Add("Skinned mesh present in a proposed static anvil. Bind-pose inventory is not deformation validation; optional static preview refuses it.");
                if(report.rendererCount==0&&direct==null)warnings.Add("No model renderers found; embedded mesh counts alone do not prove a visible prop.");
                report.status=report.exactInstalledPlaceholder?"loaded installed placeholder; generation completion unverified":report.truncated?"loaded partial inventory; review required":"loaded for inspection; completion/provenance/cost and visual acceptance unverified";
            }
            catch(Exception e){report.status="inspection failed; no asset accepted";report.exception=e.ToString();}
            report.warnings=warnings.ToArray();return report;
        }

        static MeshRecord AddMesh(Mesh mesh,Mesh placeholder,Dictionary<Mesh,MeshRecord> records,Report report)
        {
            if(records.TryGetValue(mesh,out MeshRecord existing))return existing;
            if(records.Count>=Limit){report.truncated=true;return DescribeMesh(mesh,placeholder);}
            var result=DescribeMesh(mesh,placeholder);if(result.submeshInventoryTruncated)report.truncated=true;records.Add(mesh,result);return result;
        }
        static MeshRecord DescribeMesh(Mesh mesh,Mesh placeholder)
        {
            var result=new MeshRecord {name=mesh.name,path=AssetDatabase.GetAssetPath(mesh),vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,
                readable=mesh.isReadable,indexFormat=mesh.indexFormat.ToString(),boundsCenter=mesh.bounds.center,boundsSize=mesh.bounds.size,
                runtimeReportedBytes=Profiler.GetRuntimeMemorySizeLong(mesh),hasNormals=mesh.HasVertexAttribute(VertexAttribute.Normal),
                hasUv0=mesh.HasVertexAttribute(VertexAttribute.TexCoord0),installedPlaceholderMesh=placeholder!=null&&mesh==placeholder,submeshInventoryTruncated=mesh.subMeshCount>1024};
            Identity(mesh,out result.guid,out result.localFileId);var sections=new List<SubmeshRecord>();long indexEnd=0;
            for(int i=0;i<Math.Min(1024,mesh.subMeshCount);i++)
            {
                long indices=mesh.GetIndexCount(i);var topology=mesh.GetTopology(i);long triangles=topology==MeshTopology.Triangles?indices/3:0;
                sections.Add(new SubmeshRecord {index=i,topology=topology.ToString(),indices=indices,triangles=triangles});result.triangles+=triangles;
                indexEnd=Math.Max(indexEnd,(long)mesh.GetIndexStart(i)+indices);
            }
            result.sections=sections.ToArray();long vertexBytes=0;
            for(int stream=0;stream<mesh.vertexBufferCount;stream++)vertexBytes+=(long)mesh.vertexCount*mesh.GetVertexBufferStride(stream);
            result.estimatedVertexIndexBufferBytes=vertexBytes+indexEnd*(mesh.indexFormat==IndexFormat.UInt32?4:2);
            return result;
        }
        static void AddMaterial(Material material,Dictionary<Material,MaterialRecord> materials,Dictionary<Texture,TextureRecord> textures,Report report)
        {
            if(materials.ContainsKey(material))return;if(materials.Count>=Limit){report.truncated=true;return;}
            Shader shader=material.shader;var tags=new List<string>();if(material.passCount>128)report.truncated=true;
            if(shader!=null)for(int pass=0;pass<Math.Min(128,material.passCount);pass++)tags.Add(shader.FindPassTagValue(pass,new ShaderTagId("LightMode")).name);
            string pipelineTag=material.GetTag("RenderPipeline",false,"");bool supported=shader!=null&&shader.isSupported,errors=shader==null||ShaderUtil.ShaderHasError(shader);
            var record=new MaterialRecord {name=material.name,path=AssetDatabase.GetAssetPath(material),shader=shader!=null?shader.name:"<missing>",
                renderPipelineTag=pipelineTag,shaderPasses=material.passCount,shaderSupported=supported,shaderHasErrors=errors,passLightModes=tags.ToArray(),runtimeReportedBytes=Profiler.GetRuntimeMemorySizeLong(material),
                urpAssessment=pipelineTag=="UniversalPipeline"&&supported&&!errors?"URP tagged/support/error checks pass; actual preview still required":"URP compatibility unverified or shader unsupported/errors; no automatic conversion"};
            Identity(material,out record.guid,out record.localFileId);var bindings=new List<TextureBinding>();
            string[] textureProperties=material.GetTexturePropertyNames();if(textureProperties.Length>Limit)report.truncated=true;
            for(int propertyIndex=0;propertyIndex<Math.Min(Limit,textureProperties.Length);propertyIndex++)
            {
                string property=textureProperties[propertyIndex];
                Texture texture=material.GetTexture(property);if(texture==null)continue;
                bindings.Add(new TextureBinding {property=property,textureName=texture.name,texturePath=AssetDatabase.GetAssetPath(texture)});
                if(textures.ContainsKey(texture))continue;if(textures.Count>=Limit){report.truncated=true;continue;}
                var tr=new TextureRecord {name=texture.name,path=AssetDatabase.GetAssetPath(texture),type=texture.GetType().Name,
                    width=texture.width,height=texture.height,dimension=texture.dimension.ToString(),graphicsFormat=texture.graphicsFormat.ToString(),
                    runtimeReportedBytes=Profiler.GetRuntimeMemorySizeLong(texture)};
                if(texture is Texture2D t2){tr.cpuReadable=t2.isReadable;tr.mipCount=t2.mipmapCount;tr.readabilityKnown=true;}
                else if(texture is Cubemap cube){tr.cpuReadable=cube.isReadable;tr.mipCount=cube.mipmapCount;tr.readabilityKnown=true;}
                Identity(texture,out tr.guid,out tr.localFileId);SourceInfo(tr.path,out tr.sourceFileBytes,out tr.sourceSha256);textures.Add(texture,tr);
            }
            record.textures=bindings.ToArray();materials.Add(material,record);
        }
        static T[] Values<K,T>(Dictionary<K,T> dictionary){var result=new T[dictionary.Count];dictionary.Values.CopyTo(result,0);return result;}
        static void CopyTransformBranch(Transform source,Transform parent,Dictionary<Transform,Transform> copied,ref int count)
        {
            if(++count>512)throw new InvalidOperationException("Preview transform limit512 exceeded; no partial render accepted.");
            var clone=new GameObject(source.name) {hideFlags=HideFlags.HideAndDontSave,layer=0};Transform transform=clone.transform;
            transform.SetParent(parent,false);transform.localPosition=source.localPosition;transform.localRotation=source.localRotation;transform.localScale=source.localScale;
            clone.SetActive(source.gameObject.activeSelf);copied.Add(source,transform);
            for(int i=0;i<source.childCount;i++)CopyTransformBranch(source.GetChild(i),transform,copied,ref count);
        }
        static byte GammaByte(byte value){return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.LinearToGammaSpace(value/255f)*255f),0,255);}
        static bool Finite(Vector3 v){return !float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);}
        static void Identity(Object value,out string guid,out long localId){if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out guid,out localId)){guid="";localId=0;}}
        static string NormalizeAssetPath(string path)
        {
            if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("An exact returned imported asset path is required.");
            path=path.Replace('\\','/');
            if(!(path.StartsWith("Assets/",StringComparison.Ordinal)||path.StartsWith("Packages/",StringComparison.Ordinal))||path.Contains("/../")||path.EndsWith("/..",StringComparison.Ordinal))
                throw new ArgumentException("Use an exact project-relative Assets/ or Packages/ path, not an account URL or guessed result.");
            return path;
        }
        static void SourceInfo(string assetPath,out long bytes,out string sha)
        {
            bytes=0;sha="";if(string.IsNullOrEmpty(assetPath))return;
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));string resolved=null;
            if(assetPath.StartsWith("Assets/",StringComparison.Ordinal))resolved=Path.GetFullPath(Path.Combine(project,assetPath));
            // Package virtual paths are reported but not guessed into a PackageCache directory.
            if(resolved==null||!resolved.StartsWith(project+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(resolved))return;
            bytes=new FileInfo(resolved).Length;using(var stream=File.OpenRead(resolved))using(var hash=SHA256.Create())sha=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        static string HierarchyPath(Transform item,Transform root)
        {
            string path=item.name;for(Transform parent=item.parent;parent!=null&&item!=root;parent=parent.parent){path=parent.name+"/"+path;if(parent==root)break;}return path;
        }
        static void Encapsulate(Bounds bounds,Matrix4x4 matrix,ref Bounds total,ref bool has)
        {
            for(int i=0;i<8;i++){Vector3 p=matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(!has){total=new Bounds(p,Vector3.zero);has=true;}else total.Encapsulate(p);}
        }
    }
}
#endif
