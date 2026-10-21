using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using TowerFxReload=UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using TowerFxReload=System.Action;
#endif

namespace OdysseyOracle
{
    // Plain owned adapter, allocated from the existing Mono.Initialize method, never a Mono constructor.
    public sealed class TowerElementVisualInstance:IDisposable
    {
        readonly TowerElementVisualLibrary library;
        readonly Transform parent;
        readonly GameObject root;
        readonly MeshFilter ambientFilter,shotFilter;
        readonly MeshRenderer ambient,shot;
        readonly MaterialPropertyBlock block;
        NativeArray<TowerElementClock32> state;
        NativeArray<TowerElementPose64> pose;
        JobHandle fence;
        TowerElementRecipe64 recipe;
        float3 origin,hit;
        float acceptedRadius;
        bool disposed,visible=true,subscribed;
        TowerFxReload reload;
        readonly int clockId,ageId,travelId,typeTierId,colorRadiusId,detailId,originId,hitId,shapeId;
        public bool Ready=>!disposed&&library!=null&&library.Ready&&state.IsCreated&&pose.IsCreated;
        public int Type=>(int)recipe.Type;
        public int Tier=>(int)recipe.Tier;
        public TowerElementClock32 ClockSnapshot{get{fence.Complete();return state.IsCreated?state[0]:default;}}
        public TowerElementPose64 PoseSnapshot{get{fence.Complete();return pose.IsCreated?pose[0]:default;}}
        public TowerElementVisualInstance(Transform acceptedHead,TowerElementVisualLibrary shared,int type,int tier)
        {
            if(acceptedHead==null||shared==null||!shared.Ready||!TowerElementRecipes.Valid(type,tier))throw new ArgumentException();
            parent=acceptedHead;library=shared;recipe=TowerElementRecipes.Get(type,tier);root=new GameObject("Original bounded elemental tower visuals");root.transform.SetParent(parent,false);root.layer=parent.gameObject.layer;
            block=new MaterialPropertyBlock();clockId=Shader.PropertyToID("_Clock");ageId=Shader.PropertyToID("_ShotAge");travelId=Shader.PropertyToID("_Travel");typeTierId=Shader.PropertyToID("_TypeTier");colorRadiusId=Shader.PropertyToID("_ColorRadius");detailId=Shader.PropertyToID("_Detail");originId=Shader.PropertyToID("_ShotOrigin");hitId=Shader.PropertyToID("_ShotHitRadius");shapeId=Shader.PropertyToID("_Shape");
            try
            {
                ambientFilter=Create("Original elemental living surface",out ambient);shotFilter=Create("Original accepted elemental flight and contact",out shot);
                state=new NativeArray<TowerElementClock32>(1,Allocator.Persistent);pose=new NativeArray<TowerElementPose64>(1,Allocator.Persistent);
                state[0]=new TowerElementClock32{Age=10,Travel=.08f,Duration=.5f,Type=(uint)type,Tier=(uint)tier,Alive=1};
                SetTier(tier);shot.enabled=false;reload=Dispose;LightPoolLifecycle.SubscribeBeforeReload(reload);subscribed=true;
            }
            catch{Dispose();throw;}
        }
        MeshFilter Create(string name,out MeshRenderer renderer)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);go.layer=parent.gameObject.layer;
            renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=library.Material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            var source=parent.GetComponent<MeshRenderer>();if(source!=null)renderer.renderingLayerMask=source.renderingLayerMask;
            return go.GetComponent<MeshFilter>();
        }
        public void SetTier(int tier)
        {
            if(disposed||!TowerElementRecipes.Valid(Type,tier))return;fence.Complete();var template=library.Get(Type,tier);recipe=template.Recipe;
            ambientFilter.sharedMesh=template.Ambient;shotFilter.sharedMesh=template.Shot;var current=state[0];current.Tier=(uint)tier;state[0]=current;Tick(0,true);
        }
        public bool AcceptedShot(uint serial,Vector3 acceptedOrigin,Vector3 acceptedHit,float actualAreaRadius=0,float acceptedElapsed=0)
        {
            if(!Ready||serial==0||!TowerElementRecipes.Point(acceptedOrigin)||!TowerElementRecipes.Point(acceptedHit)||!TowerElementRecipes.Finite(actualAreaRadius)||actualAreaRadius<0||actualAreaRadius>4.5f||!TowerElementRecipes.Finite(acceptedElapsed)||acceptedElapsed<0||acceptedElapsed>1)return false;
            fence.Complete();var current=state[0];if(serial<=current.Serial)return false;
            origin=parent.InverseTransformPoint(acceptedOrigin);hit=parent.InverseTransformPoint(acceptedHit);acceptedRadius=actualAreaRadius;
            current.Serial=serial;current.Age=acceptedElapsed;current.Travel=TowerElementRecipes.FlightSeconds(math.distance(origin,hit));current.Duration=current.Travel+.38f;state[0]=current;Tick(0,true);return true;
        }
        public void SetVisible(bool value){visible=value;if(root!=null)root.SetActive(value);}
        public void Tick(float delta,bool animateAmbient)
        {
            if(!Ready||!TowerElementRecipes.Finite(delta)||delta<0||delta>1)return;fence.Complete();
            fence=new TowerElementStepJob{State=state,Pose=pose,Recipe=recipe,Delta=delta,AmbientDelta=OracleBookAccessibility.PrefersReducedMotion?0:delta,Origin=origin,Hit=hit,AcceptedRadius=acceptedRadius}.Schedule(fence);fence.Complete();
            TowerElementPose64 frame=pose[0];var current=state[0];ambient.enabled=visible&&animateAmbient;shot.enabled=visible&&current.Serial!=0&&current.Age<current.Duration;
            block.SetFloat(clockId,frame.Time.x);block.SetFloat(ageId,frame.Time.y);block.SetFloat(travelId,frame.Time.z);block.SetVector(typeTierId,new Vector4(Type,Tier,current.Serial!=0?1:0,GameSession.Instance!=null&&GameSession.Instance.Graphics!=null?GameSession.Instance.Graphics.ParticleQuality:1));block.SetVector(colorRadiusId,new Vector4(recipe.ColorRadius.x,recipe.ColorRadius.y,recipe.ColorRadius.z,recipe.ColorRadius.w));block.SetVector(shapeId,new Vector4(recipe.Shape.x,recipe.Shape.y,recipe.Shape.z,recipe.Shape.w));block.SetVector(detailId,new Vector4(recipe.Detail.x,recipe.Detail.y,recipe.Detail.z,recipe.Detail.w));block.SetVector(originId,new Vector4(origin.x,origin.y,origin.z,0));block.SetVector(hitId,new Vector4(hit.x,hit.y,hit.z,acceptedRadius));
            ambient.SetPropertyBlock(block);shot.SetPropertyBlock(block);
            Vector3 min=new Vector3(frame.Minimum.x,frame.Minimum.y,frame.Minimum.z),max=new Vector3(frame.Maximum.x,frame.Maximum.y,frame.Maximum.z);shot.localBounds=new Bounds((min+max)*.5f,max-min);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;fence.Complete();if(subscribed&&reload!=null)LightPoolLifecycle.UnsubscribeBeforeReload(reload);subscribed=false;
            if(pose.IsCreated)pose.Dispose();if(state.IsCreated)state.Dispose();if(root!=null){root.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(root);else UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted Head-local +Z/Y-up ambient metadata; accepted world muzzle/hit converted once outside jobs. No Physics, retarget or authority writes; current instant damage remains canonical.
// Array Formats: One Clock32[1]/Pose64[1]/JobHandle, two cached mesh renderers/filters and one MPB; meshes/material borrowed from12-template owned library. One short flight slot is bounded below the current minimum1.3s tower cadence.
// Dependencies/Pointers: Existing accepted shot/tier consumer alone calls these methods; preview may tick ambient with no accepted shot. Fence completes before serial mutation/presentation/dispose/reload. Constructors execute from Initialize on main thread, not Mono fields; no per-particle Objects or hot arrays.
