using System;
using OdysseyOracle.Foundation.Presentation;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OdysseyOracle
{
    // One cold presentation lease. No game tower components, colliders, shots, audio, input or economy.
    public sealed class TowerAnvilPreviewAtlas:IDisposable
    {
        public const int StageLayer=29;
        const uint RenderingMask=1u<<7;
        readonly Transform[] tiles=new Transform[12],workshopAssemblies015=new Transform[12],workshopAnchors015=new Transform[12];
        readonly int[] workshopTiers015=new int[4];WorkshopBuildPool015 workshop015;bool workshopCue015;uint workshopSequence015;
        public bool WorkshopReady015=>workshop015!=null;
        public bool ConsumeWorkshopCue015(out uint revision){revision=workshopSequence015;bool result=workshopCue015;workshopCue015=false;return result;}
        readonly MeshRenderer[] renderers=new MeshRenderer[72];
        readonly Material[] normalMaterials=new Material[4];
        SavedTowerModelLibrary library;GameObject root;Camera camera;Material silhouette;
        RenderPipeline.StandardRequest renderRequest;
        NativeArray<AnvilPreviewFrame32> frames;NativeArray<AnvilPreviewPose32> poses;JobHandle fence;
        bool scheduled,visible,disposed,dirty=true,staticRendered,lastMotion;
        float clock,timer;uint unlocked;
        public RenderTexture Target{get;private set;}
        public bool Visible=>visible&&!disposed;
        public float PresentationSeconds=>clock;
        public int RenderCount{get;private set;}
        public bool WaitingForAcceptedState{get;private set;}=true;
        public Transform PresentationRoot=>root!=null?root.transform:null;
        public AnvilPreviewFrame32 Frame(int index){Complete();return frames[index];}
        public AnvilPreviewPose32 Pose(int index){Complete();return poses[index];}
        public bool IsUnlocked(int index)=>index>=0&&index<12&&(unlocked&(1u<<index))!=0;

        public TowerAnvilPreviewAtlas(TowerModelAssetCatalog assets)
        {
            if((RenderingLayerMask.GetDefinedRenderingLayersCombinedMaskValue()&RenderingMask)==0)throw new InvalidOperationException("Anvil preview requires existing defined rendering layer7.");
            if(!SavedTowerModelLibrary.TryCreate(assets,out library,out string reason))throw new InvalidOperationException(reason);
            NativeArray<AnvilPreviewBounds40> input=default;
            try
            {
                Shader shader=Resources.Load<Shader>("Shaders/OracleAnvilLockedSilhouette");
                if(shader==null||!shader.isSupported)throw new InvalidOperationException("Original locked-tower silhouette shader unavailable.");
                silhouette=new Material(shader){name="Owned shared Tower Anvil locked silhouette"};
                frames=new NativeArray<AnvilPreviewFrame32>(12,Allocator.Persistent);
                poses=new NativeArray<AnvilPreviewPose32>(12,Allocator.Persistent);
                input=new NativeArray<AnvilPreviewBounds40>(72,Allocator.TempJob);
                for(int type=0;type<4;type++)
                {
                    normalMaterials[type]=library.GetMaterial(type);
                    for(int tier=1;tier<=3;tier++)
                    {
                        var template=library.Get(type,tier);int tile=type*3+tier-1;
                        var pivot=template.Recipe.HeadPivot;
                        for(int group=0;group<6;group++)
                        {
                            Mesh mesh=template.Groups[group];if(mesh==null)continue;Bounds bounds=mesh.bounds;
                            input[tile*6+group]=new AnvilPreviewBounds40{Center=F(bounds.center),Extents=F(bounds.extents),Pivot=group==0?float3.zero:new float3((float)pivot.X,(float)pivot.Y,(float)pivot.Z),Present=1};
                        }
                    }
                }
                fence=new TowerAnvilFrameJob{Bounds=input,Frames=frames}.Schedule(12,1);scheduled=true;Complete();
                for(int tile=0;tile<12;tile++)if(frames[tile].Status!=1)throw new InvalidOperationException("Saved tower preview mesh frame failed.");
                root=new GameObject("Tower Anvil private twelve-model atlas");root.SetActive(false);root.layer=StageLayer;
                for(int type=0;type<4;type++)for(int tier=1;tier<=3;tier++)
                {
                    int tile=type*3+tier-1;var template=library.Get(type,tier);
                    Transform turntable=Node("Actual saved tower type"+type+" tier"+tier,root.transform);tiles[tile]=turntable;
                    Transform content=Node("Bounds-centered borrowed model",turntable);float3 center=frames[tile].Center;content.localPosition=new Vector3(-center.x,-center.y,-center.z);
                    workshopAnchors015[tile]=content;Transform assembly=Node("Original workshop cosmetic assembly",content);workshopAssemblies015[tile]=assembly;
                    var pivot=template.Recipe.HeadPivot;
                    for(int group=0;group<6;group++)
                    {
                        Transform child=Node("Saved mesh group"+group,assembly);child.localPosition=group==0?Vector3.zero:new Vector3((float)pivot.X,(float)pivot.Y,(float)pivot.Z);
                        var filter=child.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=template.Groups[group];
                        var renderer=child.gameObject.AddComponent<MeshRenderer>();renderers[tile*6+group]=renderer;renderer.sharedMaterial=silhouette;renderer.enabled=template.Groups[group]!=null;
                        renderer.renderingLayerMask=RenderingMask;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                        renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
                    }
                }
                camera=Node("Single Tower Anvil atlas camera",root.transform).gameObject.AddComponent<Camera>();
                camera.cameraType=CameraType.Preview;camera.transform.localPosition=new Vector3(0,0,-30);camera.transform.localRotation=Quaternion.identity;
                camera.orthographic=true;camera.orthographicSize=8;camera.aspect=.75f;camera.nearClipPlane=.1f;camera.farClipPlane=65;camera.cullingMask=1<<StageLayer;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.18f,1);camera.allowHDR=false;camera.allowMSAA=false;camera.useOcclusionCulling=false;camera.enabled=false;
                var additional=camera.GetUniversalAdditionalCameraData();additional.renderPostProcessing=false;additional.renderShadows=false;additional.requiresDepthOption=CameraOverrideOption.Off;additional.requiresColorOption=CameraOverrideOption.Off;
                Target=new RenderTexture(768,1024,24,RenderTextureFormat.ARGB32){name="Owned Tower Anvil three-tier atlas",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,antiAliasing=1,useMipMap=false,autoGenerateMips=false};
                if(!Target.Create())throw new InvalidOperationException("Tower Anvil atlas render texture unavailable.");camera.targetTexture=Target;
                renderRequest=new RenderPipeline.StandardRequest{destination=Target};
                if(!RenderPipeline.SupportsRenderRequest(camera,renderRequest))throw new InvalidOperationException("URP does not support the private anvil atlas request.");
                Fill(new Vector3(-8,10,-12),new Color(1,.88f,.70f),450);Fill(new Vector3(10,-7,-9),new Color(.68f,.83f,1),300);
                try{workshop015=new WorkshopBuildPool015(root.transform,library,StageLayer,RenderingMask);}catch(Exception error){Debug.LogWarning("Original anvil workshop visuals unavailable: "+error.Message);}
                ApplyPoses();root.transform.position=new Vector3(0,-22000,0);
                RenderPipelineManager.endCameraRendering+=Rendered;
            }
            catch{Dispose();throw;}
            finally{if(input.IsCreated)input.Dispose();}
        }
        static Transform Node(string name,Transform parent){var node=new GameObject(name).transform;node.gameObject.layer=StageLayer;node.SetParent(parent,false);return node;}
        void Fill(Vector3 position,Color color,float intensity)
        {
            var light=Node("Private anvil preview shadowless fill",root.transform).gameObject.AddComponent<Light>();
            light.type=LightType.Point;light.transform.localPosition=position;light.color=color;light.range=50;light.intensity=intensity;light.shadows=LightShadows.None;light.bounceIntensity=0;
            light.cullingMask=1<<StageLayer;light.renderingLayerMask=unchecked((int)RenderingMask);light.gameObject.AddComponent<UniversalAdditionalLightData>().renderingLayers=RenderingMask;
        }
        public void SetAcceptedTiers(bool ready,int fire,int water,int earth,int air)
        {
            if(disposed)return;ready=ready&&fire>=1&&fire<=3&&water>=1&&water<=3&&earth>=1&&earth<=3&&air>=1&&air<=3;uint next=0;
            for(int type=0;type<4;type++)
            {
                int accepted=type==0?fire:type==1?water:type==2?earth:air;
                for(int tier=1;tier<=3;tier++)if(TowerAnvilPreviewMath.Unlocked(tier,accepted,ready))next|=1u<<(type*3+tier-1);
            }
            if(unlocked==next&&WaitingForAcceptedState==!ready)return;
            Complete();bool hadAccepted=!WaitingForAcceptedState;bool decreased=ready&&(fire<workshopTiers015[0]||water<workshopTiers015[1]||earth<workshopTiers015[2]||air<workshopTiers015[3]);
            if(!ready||!hadAccepted||decreased){workshop015?.Clear();workshopCue015=false;}
            unlocked=next;WaitingForAcceptedState=!ready;
            for(int tile=0;tile<12;tile++)for(int group=0;group<6;group++)
                renderers[tile*6+group].sharedMaterial=IsUnlocked(tile)?normalMaterials[tile/3]:silhouette;
            for(int type=0;type<4;type++)
            {
                int accepted=type==0?fire:type==1?water:type==2?earth:air;int tile=type*3+accepted-1;
                if(ready&&hadAccepted&&!decreased&&visible&&accepted>workshopTiers015[type]&&workshop015!=null&&workshop015.Bind(type+1,accepted,type,accepted,null,workshopAssemblies015[tile],workshopAnchors015[tile],true,Time.realtimeSinceStartupAsDouble))
                {workshopCue015=true;if(workshopSequence015<uint.MaxValue)workshopSequence015++;}
                workshopTiers015[type]=ready?accepted:0;
            }
            dirty=true;
        }
        public void SetVisible(bool value)
        {
            value=value&&!disposed;if(value==visible)return;Complete();visible=value;
            if(!value){workshop015?.Clear();workshopCue015=false;}
            if(camera!=null)camera.enabled=false;if(root!=null)root.SetActive(value);if(value){dirty=true;staticRendered=false;timer=.05f;}
        }
        public void Tick(float delta,bool motion)
        {
            if(disposed||!visible||camera==null||Target==null)return;
            bool workshopActive015=workshop015!=null&&workshop015.Active;workshop015?.Tick(Time.realtimeSinceStartupAsDouble,OracleBookAccessibility.PrefersReducedMotion);if(workshopActive015)dirty=true;
            if(lastMotion!=motion){lastMotion=motion;dirty=true;}clock=TowerAnvilPreviewMath.Advance(clock,delta,motion);
            if(!motion&&staticRendered&&!dirty)return;timer+=float.IsNaN(delta)||float.IsInfinity(delta)?0:Mathf.Clamp(delta,0,.1f);
            if(timer<.05f)return;timer=0;ApplyPoses();int before=RenderCount;RenderPipeline.SubmitRenderRequest(camera,renderRequest);
            if(RenderCount==before)throw new InvalidOperationException("Anvil atlas did not receive its matching camera render event.");dirty=false;
        }
        void ApplyPoses()
        {
            fence=new TowerAnvilPoseJob{Frames=frames,Poses=poses,Time=clock}.Schedule(12,1);scheduled=true;Complete();
            for(int tile=0;tile<12;tile++)
            {
                var pose=poses[tile];tiles[tile].SetLocalPositionAndRotation(V(pose.Position),new Quaternion(pose.Rotation.value.x,pose.Rotation.value.y,pose.Rotation.value.z,pose.Rotation.value.w));
                tiles[tile].localScale=Vector3.one*pose.Scale;
            }
        }
        void Rendered(ScriptableRenderContext context,Camera value){if(value==camera&&!disposed){RenderCount++;staticRendered=true;camera.enabled=false;}}
        void Complete(){if(scheduled){try{fence.Complete();}finally{scheduled=false;}}}
        static Vector3 V(float3 p)=>new Vector3(p.x,p.y,p.z);
        static float3 F(Vector3 p)=>new float3(p.x,p.y,p.z);
        public void Dispose()
        {
            if(disposed)return;disposed=true;Complete();workshop015?.Dispose();workshop015=null;RenderPipelineManager.endCameraRendering-=Rendered;visible=false;
            if(camera!=null){camera.enabled=false;camera.targetTexture=null;}if(root!=null){root.SetActive(false);UnityEngine.Object.Destroy(root);root=null;}
            renderRequest=null;if(Target!=null){Target.Release();UnityEngine.Object.Destroy(Target);Target=null;}
            if(silhouette!=null){UnityEngine.Object.Destroy(silhouette);silhouette=null;}
            if(poses.IsCreated)poses.Dispose();if(frames.IsCreated)frames.Dispose();library?.Dispose();library=null;
            // Only the wrapper is owned. Saved catalog Mesh/Material assets remain alive and untouched.
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: one offworldY-22000/GameObjectlayer29 atlas; definedrendering/lightlayer7, centered borrowed towergroups+HeadPivot,3columns×4rows4m tiles. Preview camera is orthographic8mhalfheight/far65; one RT768×1024.
// Array Formats: cold72mesh-bound records->Burst12frames and12poses; persistent32B[12] arrays complete before every transform adapter/visibility/disposal. One original silhouette material,72cached renderers,12turntables; no hot objects/materials/mesh assets.
// Dependencies/Pointers: library wrapper borrows saved immutable meshes/materials and alone is disposed; final catalog assets are never Destroyed. One cold cached StandardRequest supplies synchronous matched20Hz renders, camera remains disabled. HUD alone gates visible/focused rendering; accepted state read-only, reduced-motion static, no gameplay/economy/focus/input/audio authority.
