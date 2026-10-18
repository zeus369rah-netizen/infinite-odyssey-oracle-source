using System;
using UnityEngine;

namespace OdysseyOracle
{
    // One cold lease per customization menu; retained while closed, released with its HUD.
    // Existing gameplay cameras are bounded to110m and worldY-25..100. This visual-only
    // layer31 stage is10000m below that world; no world camera/input/physics state is changed.
    public sealed class OracleAvatarPreviewLease:IDisposable
    {
        public const int Width=512,Height=640,PreviewLayer=31;
        const uint PreviewRenderingMask=1u<<31;
        readonly GameObject root;
        readonly Camera camera;
        public RenderTexture Target {get;private set;}
        public RemoteOracleAvatar Avatar {get;private set;}
        bool disposed;
        public OracleAvatarPreviewLease()
        {
            root=new GameObject("Isolated Oracle appearance preview");root.transform.position=new Vector3(0,-10000,0);
            try
            {
                Target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){name="Oracle appearance512x640",antiAliasing=1,useMipMap=false,autoGenerateMips=false};
                if(!Target.Create())throw new InvalidOperationException("Appearance preview texture unavailable.");
                var model=new GameObject("Preview Oracle");model.transform.SetParent(root.transform,false);
                Avatar=model.AddComponent<RemoteOracleAvatar>();Avatar.Initialize(ulong.MaxValue,root.transform.position,-18f);Avatar.enabled=false;
                var view=new GameObject("Appearance-only camera");view.transform.SetParent(root.transform,false);view.transform.localPosition=new Vector3(-2.5f,1.5f,4.2f);
                view.transform.LookAt(root.transform.position+new Vector3(0,1.04f,0));camera=view.AddComponent<Camera>();
                camera.targetTexture=Target;camera.cullingMask=1<<PreviewLayer;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.025f,.04f,.045f,1);camera.orthographic=true;camera.orthographicSize=1.18f;
                camera.nearClipPlane=.05f;camera.farClipPlane=15;camera.allowHDR=false;camera.allowMSAA=false;camera.depth=-25;
                Light(root.transform,"Preview key",new Vector3(-1.5f,2.8f,2.2f),12f);
                Light(root.transform,"Preview fill",new Vector3(1.7f,1.8f,1.8f),6f);
                Layer(root.transform);
                root.SetActive(false);
            }
            catch{Dispose();throw;}
        }
        static void Light(Transform parent,string name,Vector3 position,float intensity)
        {
            var node=new GameObject(name);node.transform.SetParent(parent,false);node.transform.localPosition=position;
            var light=node.AddComponent<UnityEngine.Light>();light.type=LightType.Point;light.range=6f;light.intensity=intensity;light.cullingMask=1<<PreviewLayer;light.shadows=LightShadows.None;
            light.renderingLayerMask=unchecked((int)PreviewRenderingMask);
            node.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>().renderingLayers=PreviewRenderingMask;
        }
        static void Layer(Transform node)
        {node.gameObject.layer=PreviewLayer;if(node.TryGetComponent<Renderer>(out var renderer))renderer.renderingLayerMask=PreviewRenderingMask;for(int i=0;i<node.childCount;i++)Layer(node.GetChild(i));}
        public void SetVisible(bool visible){if(!disposed&&root!=null)root.SetActive(visible);}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(camera!=null){camera.enabled=false;camera.targetTexture=null;}
            if(root!=null){root.SetActive(false);UnityEngine.Object.Destroy(root);}
            Avatar=null;
            if(Target!=null){Target.Release();UnityEngine.Object.Destroy(Target);Target=null;}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Main-thread visual-only512x640 RT/depth24 lease; layer31 stage atY-10000. Fixed1 camera/2 scoped lights/1 rigid avatar.
// Existing feet origin, shared rigid template/material owners and gameplay cameras/input/native ownership remain unchanged.
