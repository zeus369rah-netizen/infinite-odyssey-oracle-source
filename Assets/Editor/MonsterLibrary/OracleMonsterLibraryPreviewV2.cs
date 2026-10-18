#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OdysseyOracle.EditorAssets
{
    // Root-run public URP render requests in an isolated preview scene. All
    // temporary camera/lights/textures are closed; saved prefabs gain none.
    public static class OracleMonsterLibraryPreviewV2
    {
        const int Width=360,Height=420;
        [Serializable] sealed class Capture
        {public int kind,visiblePixels,magentaPixels;public string file,sha256;}
        [Serializable] sealed class Receipt
        {
            public string status,version,renderApi,sheet,sha256,utc;
            public int width,height;
            public bool savedAssetsValidated,mainScenesPreserved;
            public string[] columns,limitations;
            public Capture[] captures;
        }

        public static string CapturePreviewSheet(string absoluteOutputDirectory)
        {
            OracleMonsterAssetLibraryV2.AssertEditorIdle();
            if(string.IsNullOrWhiteSpace(absoluteOutputDirectory))throw new ArgumentException("An absolute output directory outside Assets is required.");
            OracleMonsterAssetLibraryV2.CheckReceiptDirectory(absoluteOutputDirectory);
            if(!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))throw new InvalidOperationException("Active URP is required; no internal renderer or Camera.Render fallback.");
            OracleMonsterAssetLibraryV2.ValidateVersionedLibrary(absoluteOutputDirectory);
            string before=OracleMonsterAssetLibraryV2.MainSceneSignature();
            Scene scene=EditorSceneManager.NewPreviewScene();GameObject studio=null,current=null;
            RenderTexture target=null,previous=RenderTexture.active;Texture2D readable=null,sheet=null;
            var captures=new List<Capture>(4);var pixels=new Color32[Width*4*Height];
            try
            {
                studio=new GameObject("Temporary original monster asset studio");SceneManager.MoveGameObjectToScene(studio,scene);
                var cameraObject=new GameObject("Temporary native preview camera");cameraObject.transform.SetParent(studio.transform,false);
                Camera camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cameraType=CameraType.Preview;camera.scene=scene;
                camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
                if(camera.overrideSceneCullingMask==0)throw new InvalidOperationException("No isolated preview-scene culling mask.");
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.065f,.075f,1);
                camera.orthographic=true;camera.orthographicSize=1.62f;camera.nearClipPlane=.1f;camera.farClipPlane=20;
                camera.allowHDR=false;camera.allowMSAA=false;camera.transform.position=new Vector3(4.6f,2.7f,7f);camera.transform.LookAt(new Vector3(0,1.23f,0));
                var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.volumeLayerMask=0;data.renderType=CameraRenderType.Base;
                StudioLight(studio.transform,"Preview key",new Color(1,.94f,.86f),1.55f,new Vector3(45,-32,0));
                StudioLight(studio.transform,"Preview fill",new Color(.64f,.79f,1),.70f,new Vector3(25,135,0));
                target=RenderTexture.GetTemporary(Width,Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,1);
                readable=new Texture2D(Width,Height,TextureFormat.RGBA32,false,false);
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Active renderer does not support the public SingleCameraRequest.");
                for(int kind=0;kind<4;kind++)
                {
                    GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OracleMonsterAssetLibraryV2.PrefabPath(kind));
                    current=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);current.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    RenderTexture.active=target;GL.Clear(true,true,Color.magenta);RenderPipeline.SubmitRenderRequest(camera,request);
                    RenderTexture.active=target;readable.ReadPixels(new Rect(0,0,Width,Height),0,0,false);readable.Apply(false,false);
                    Color32[] frame=readable.GetPixels32();Color32 background=frame[0];int visible=0,magenta=0;
                    foreach(Color32 pixel in frame)
                    {if(Math.Abs(pixel.r-background.r)+Math.Abs(pixel.g-background.g)+Math.Abs(pixel.b-background.b)>24)visible++;if(pixel.r>200&&pixel.g<70&&pixel.b>150)magenta++;}
                    if(visible<1200||magenta>Math.Max(100,visible/10))
                    {
                        Directory.CreateDirectory(absoluteOutputDirectory);
                        File.WriteAllBytes(Path.Combine(absoluteOutputDirectory,"failed_kind"+kind+".png"),readable.EncodeToPNG());
                        throw new InvalidOperationException("Empty/error-shader native preview for monster kind"+kind+": visible="+visible+", magenta="+magenta+".");
                    }
                    string file=new[]{"guardian_golem","stalker","shade","spider"}[kind]+".png";byte[] png=readable.EncodeToPNG();
                    Directory.CreateDirectory(absoluteOutputDirectory);File.WriteAllBytes(Path.Combine(absoluteOutputDirectory,file),png);
                    captures.Add(new Capture{kind=kind,file=file,sha256=Hash(png),visiblePixels=visible,magentaPixels=magenta});
                    for(int row=0;row<Height;row++)Array.Copy(frame,row*Width,pixels,row*Width*4+kind*Width,Width);
                    UnityEngine.Object.DestroyImmediate(current);current=null;
                }
                sheet=new Texture2D(Width*4,Height,TextureFormat.RGBA32,false,false);sheet.SetPixels32(pixels);sheet.Apply(false,false);
                byte[] bytes=sheet.EncodeToPNG();string sheetName="monster-library-v1-native.png";File.WriteAllBytes(Path.Combine(absoluteOutputDirectory,sheetName),bytes);
                UnityEngine.Object.DestroyImmediate(studio);studio=null;EditorSceneManager.ClosePreviewScene(scene);scene=default;
                var receipt=new Receipt{status="native-URP-preview-captured",version=OracleMonsterAssetLibraryV2.Version,renderApi="RenderPipeline.SubmitRenderRequest / UniversalRenderPipeline.SingleCameraRequest",
                    sheet=sheetName,sha256=Hash(bytes),width=Width*4,height=Height,savedAssetsValidated=true,mainScenesPreserved=before==OracleMonsterAssetLibraryV2.MainSceneSignature(),
                    columns=new[]{"GuardianGolem: tutorial-only","Stalker","Shade: original mint runes","Spider: eight articulated legs"},captures=captures.ToArray(),utc=DateTime.UtcNow.ToString("o"),
                    limitations=new[]{"Actual saved neutral assets in an Editor studio, not gameplay or browser screenshots.","One neutral pose; motion/retargeting, authority, FPS and delivery LOD remain untested."}};
                if(!receipt.mainScenesPreserved)throw new InvalidOperationException("Native preview changed main-scene setup or dirty state.");
                string json=JsonUtility.ToJson(receipt,true);OracleMonsterAssetLibraryV2.WriteReceipt(absoluteOutputDirectory,"preview-receipt.json",json);return json;
            }
            finally
            {
                RenderTexture.active=previous;
                if(current!=null)UnityEngine.Object.DestroyImmediate(current);if(studio!=null)UnityEngine.Object.DestroyImmediate(studio);
                if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
                if(readable!=null)UnityEngine.Object.DestroyImmediate(readable);if(sheet!=null)UnityEngine.Object.DestroyImmediate(sheet);if(target!=null)RenderTexture.ReleaseTemporary(target);
            }
        }
        static void StudioLight(Transform parent,string name,Color color,float intensity,Vector3 euler)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.rotation=Quaternion.Euler(euler);var light=go.AddComponent<Light>();light.type=LightType.Directional;light.color=color;light.intensity=intensity;light.shadows=LightShadows.None;}
        static string Hash(byte[] bytes){using(var hash=SHA256.Create()){var s=new StringBuilder();foreach(byte b in hash.ComputeHash(bytes))s.Append(b.ToString("x2"));return s.ToString();}}
    }
}
#endif
