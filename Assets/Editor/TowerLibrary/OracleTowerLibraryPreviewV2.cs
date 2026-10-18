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
    // # ARCHITECTURE SNAPSHOT
    // Root-run native preview only. Public URP RenderRequest + isolated Editor preview Scene.
    // The saved neutral prefabs remain free of scripts/lights/colliders/audio. Temporary studio
    // camera/lights live only in the preview Scene, which closes in finally without scene saves.
    public static class OracleTowerLibraryPreviewV2
    {
        const int Width=320,Height=400,Label=32,Header=64;
        [Serializable] sealed class Capture
        {public int type,tier,width,height,visiblePixels,magentaPixels;public string file,sha256;}
        [Serializable] sealed class Receipt
        {
            public string status,version,pipeline,renderApi,sheet,sha256,utc;
            public int width,height;public bool savedAssetsValidated,mainSceneSetupPreserved;
            public string[] captions;public Capture[] captures;
        }
        public static string CapturePreviewSheet(string absoluteOutputDirectory)
        {
            OracleTowerAssetLibraryV2.AssertEditorIdle();
            if(String.IsNullOrWhiteSpace(absoluteOutputDirectory)||!Path.IsPathRooted(absoluteOutputDirectory))throw new ArgumentException("Supply an absolute root-approved output folder outside Assets.");
            string output=Path.GetFullPath(absoluteOutputDirectory),assets=Path.GetFullPath(Application.dataPath);
            if(output==assets||output.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Preview receipts stay outside Assets.");
            if(!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))throw new InvalidOperationException("The installed active URP renderer is required; no Camera.Render/internal fallback.");
            OracleTowerAssetLibraryV2.ValidateVersionedLibrary(output);
            string before=SceneSignature();Scene scene=EditorSceneManager.NewPreviewScene();GameObject root=null,current=null;
            RenderTexture target=null;Texture2D readable=null,sheet=null;RenderTexture previous=RenderTexture.active;
            var captures=new List<Capture>(12);int sheetWidth=Width*4,sheetHeight=(Height+Label)*3+Header;
            Color32[] sheetPixels=new Color32[sheetWidth*sheetHeight];for(int i=0;i<sheetPixels.Length;i++)sheetPixels[i]=new Color32(13,24,22,255);
            try
            {
                root=new GameObject("Temporary tower asset studio");SceneManager.MoveGameObjectToScene(root,scene);
                var cameraObject=new GameObject("Temporary asset preview camera");cameraObject.transform.SetParent(root.transform,false);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cameraType=CameraType.Preview;camera.scene=scene;
                camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
                if(camera.overrideSceneCullingMask==0)throw new InvalidOperationException("Preview Scene has no isolated culling mask.");
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.065f,.055f,1);
                camera.orthographic=true;camera.orthographicSize=3.30f;camera.nearClipPlane=.1f;camera.farClipPlane=35;
                camera.allowHDR=false;camera.allowMSAA=false;camera.transform.position=new Vector3(8.2f,6.3f,10.2f);camera.transform.LookAt(new Vector3(0,2.95f,0));
                var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.volumeLayerMask=0;data.renderType=CameraRenderType.Base;
                StudioLight(root.transform,"Preview key",new Color(1,.94f,.82f),1.65f,new Vector3(48,-35,0));
                StudioLight(root.transform,"Preview fill",new Color(.58f,.76f,1),.75f,new Vector3(25,135,0));
                StudioLight(root.transform,"Preview rim",new Color(.70f,1,.84f),.40f,new Vector3(-15,0,0));
                target=RenderTexture.GetTemporary(Width,Height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,1);
                readable=new Texture2D(Width,Height,TextureFormat.RGBA32,false,false);
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("Renderer does not support the public SingleCameraRequest; no fallback attempted.");
                for(int tier=1;tier<=3;tier++)for(int type=0;type<4;type++)
                {
                    GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OracleTowerAssetLibraryV2.PrefabPath(type,tier));
                    current=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);current.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    RenderTexture.active=target;GL.Clear(true,true,Color.magenta);RenderPipeline.SubmitRenderRequest(camera,request);
                    RenderTexture.active=target;readable.ReadPixels(new Rect(0,0,Width,Height),0,0,false);readable.Apply(false,false);
                    Color32[] pixels=readable.GetPixels32();int visible=0,magenta=0;Color32 background=pixels[0];
                    foreach(Color32 pixel in pixels)
                    {
                        if(Math.Abs(pixel.r-background.r)+Math.Abs(pixel.g-background.g)+Math.Abs(pixel.b-background.b)>24)visible++;
                        if(pixel.r>200&&pixel.g<70&&pixel.b>150)magenta++;
                    }
                    if(visible<2000||magenta>Math.Max(100,visible/10))throw new InvalidOperationException("Empty/error-shader native preview for type"+type+" tier"+tier+".");
                    string file=new[]{"fire","water","earth","air"}[type]+"_t"+tier+".png";byte[] png=readable.EncodeToPNG();Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,file),png);
                    captures.Add(new Capture{type=type,tier=tier,width=Width,height=Height,file=file,sha256=Hash(png),visiblePixels=visible,magentaPixels=magenta});
                    int y=sheetHeight-Header-tier*(Height+Label);
                    for(int row=0;row<Height;row++)Array.Copy(pixels,row*Width,sheetPixels,(y+Label+row)*sheetWidth+type*Width,Width);
                    WriteLabel(sheetPixels,sheetWidth,type*Width+20,y+9,new[]{"FIRE","WATER","EARTH","AIR"}[type]+" T"+tier,2,new Color32(226,218,178,255));
                    UnityEngine.Object.DestroyImmediate(current);current=null;
                }
                WriteLabel(sheetPixels,sheetWidth,22,sheetHeight-35,"TOWER LIBRARY V2",3,new Color32(226,218,178,255));
                sheet=new Texture2D(sheetWidth,sheetHeight,TextureFormat.RGBA32,false,false);sheet.SetPixels32(sheetPixels);sheet.Apply(false,false);
                byte[] sheetPng=sheet.EncodeToPNG();string sheetFile="tower-library-v2-native.png";File.WriteAllBytes(Path.Combine(output,sheetFile),sheetPng);
                var receipt=new Receipt{status="native-URP-preview-captured",version=OracleTowerAssetLibraryV2.Version,pipeline=GraphicsSettings.currentRenderPipeline.name,
                    renderApi="RenderPipeline.SubmitRenderRequest / UniversalRenderPipeline.SingleCameraRequest",savedAssetsValidated=true,sheet=sheetFile,sha256=Hash(sheetPng),width=sheetWidth,height=sheetHeight,
                    captures=captures.ToArray(),utc=DateTime.UtcNow.ToString("o"),captions=new[]{"Actual Unity URP studio preview of saved neutral assets.","Editor studio lighting; not a gameplay screenshot, multiplayer test or browser performance result."}};
                // Close the isolated scene before recording setup preservation below.
                if(root!=null){UnityEngine.Object.DestroyImmediate(root);root=null;}EditorSceneManager.ClosePreviewScene(scene);scene=default;
                receipt.mainSceneSetupPreserved=before==SceneSignature();if(!receipt.mainSceneSetupPreserved)throw new InvalidOperationException("Preview changed main Scene setup.");
                string json=JsonUtility.ToJson(receipt,true);OracleTowerAssetLibraryV2.WriteReceipt(output,"preview-receipt.json",json);return json;
            }
            finally
            {
                RenderTexture.active=previous;if(current!=null)UnityEngine.Object.DestroyImmediate(current);if(root!=null)UnityEngine.Object.DestroyImmediate(root);
                if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
                if(readable!=null)UnityEngine.Object.DestroyImmediate(readable);if(sheet!=null)UnityEngine.Object.DestroyImmediate(sheet);if(target!=null)RenderTexture.ReleaseTemporary(target);
            }
        }
        static void StudioLight(Transform parent,string name,Color color,float intensity,Vector3 euler)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.rotation=Quaternion.Euler(euler);var light=go.AddComponent<Light>();light.type=LightType.Directional;light.color=color;light.intensity=intensity;light.shadows=LightShadows.None;}
        static string SceneSignature()
        {var text=new StringBuilder();foreach(var scene in EditorSceneManager.GetSceneManagerSetup())text.Append(scene.path).Append('|').Append(scene.isLoaded).Append('|').Append(scene.isActive).Append(';');return text.ToString();}
        static string Hash(byte[] bytes){using(var sha=SHA256.Create()){var text=new StringBuilder();foreach(byte b in sha.ComputeHash(bytes))text.Append(b.ToString("x2"));return text.ToString();}}
        static void WriteLabel(Color32[] pixels,int width,int x,int y,string text,int scale,Color32 color)
        {
            foreach(char c in text)
            {
                string bits=Glyph(c);
                for(int row=0;row<7;row++)for(int col=0;col<5;col++)if(bits[row*5+col]=='1')
                    for(int sy=0;sy<scale;sy++)for(int sx=0;sx<scale;sx++)pixels[(y+(6-row)*scale+sy)*width+x+col*scale+sx]=color;
                x+=6*scale;
            }
        }
        static string Glyph(char c)
        {
            switch(c)
            {
                case 'A':return "01110100011000111111100011000110001";
                case 'B':return "11110100011000111110100011000111110";
                case 'E':return "11111100001000011110100001000011111";
                case 'F':return "11111100001000011110100001000010000";
                case 'H':return "10001100011000111111100011000110001";
                case 'I':return "11111001000010000100001000010011111";
                case 'L':return "10000100001000010000100001000011111";
                case 'O':return "01110100011000110001100011000101110";
                case 'R':return "11110100011000111110101001001010001";
                case 'T':return "11111001000010000100001000010000100";
                case 'V':return "10001100011000110001100010101000100";
                case 'W':return "10001100011000110101101011101110001";
                case 'Y':return "10001100010101000100001000010000100";
                case '1':return "00100011000010000100001000010001110";
                case '2':return "01110100010000100010001000100011111";
                case '3':return "11110000010000101110000010000111110";
                default:return "00000000000000000000000000000000000";
            }
        }
    }
}
#endif
