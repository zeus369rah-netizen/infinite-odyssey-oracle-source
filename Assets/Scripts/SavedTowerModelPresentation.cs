using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main-thread presentation only. Existing PlacedElementShrine.Update supplies one Tick.
    // No self Update, per-voxel objects, damage, target query, audio source or gameplay collider.
    // Catalog owns saved mesh/material assets; provider only borrows them. This instance owns at most six animated groups.
    [DisallowMultipleComponent]
    public sealed partial class SavedTowerModelPresentation:MonoBehaviour
    {
        static readonly int EmissionId=Shader.PropertyToID("_EmissionColor");
        readonly Transform[] groups=new Transform[TowerModelRecipes.GroupCount];
        readonly MeshFilter[] filters=new MeshFilter[TowerModelRecipes.GroupCount];
        readonly MeshRenderer[] renderers=new MeshRenderer[TowerModelRecipes.GroupCount];
        MaterialPropertyBlock glowBlock;
        SavedTowerModelLibrary library;
        TowerModelLibrary.Template template;
        TowerModelPulseState state;
        Quaternion targetYaw=Quaternion.identity;
        bool initialized,destroyed,boltVisible=true;
        int towerId,type;
        public int TowerId=>towerId;
        public int AcceptedTier=>state!=null?state.Tier:1;
        public Vector3 VisualMuzzle=>initialized?groups[2].TransformPoint(TowerModelLibrary.Vector(template.Recipe.Muzzle)-TowerModelLibrary.Vector(template.Recipe.HeadPivot)):transform.position;
        public Vector3 CoreSocket=>initialized?groups[3].TransformPoint(TowerModelLibrary.Vector(template.Recipe.CoreSocket)-TowerModelLibrary.Vector(template.Recipe.HeadPivot)):transform.position;
        public Vector3 SoundSocket=>VisualMuzzle;
        public Vector3 ConstructionSocket=>transform.TransformPoint(new Vector3(0,.18f,0));

        public void Initialize(SavedTowerModelLibrary sharedLibrary,int id,int element,int acceptedTier,int acceptedRevision)
        {
            if(initialized||sharedLibrary==null||element<0||element>=4||id<0)throw new ArgumentException("One accepted four-base tower presentation per root.");
            glowBlock=new MaterialPropertyBlock();
            library=sharedLibrary;towerId=id;type=element;state=new TowerModelPulseState(element,id);
            if(!state.ApplyAcceptedTier(acceptedTier,acceptedRevision))throw new ArgumentException("Initial host-accepted tier required.");
            for(int i=0;i<TowerModelRecipes.GroupCount;i++)
            {
                var go=new GameObject("Tower "+(TowerModelGroup)i);groups[i]=go.transform;
                groups[i].SetParent(i>1?groups[1]:transform,false);
                filters[i]=go.AddComponent<MeshFilter>();renderers[i]=go.AddComponent<MeshRenderer>();
                renderers[i].sharedMaterial=library.GetMaterial(element);
                renderers[i].shadowCastingMode=i==0||i==1?ShadowCastingMode.On:ShadowCastingMode.Off;
                renderers[i].receiveShadows=true;renderers[i].lightProbeUsage=LightProbeUsage.BlendProbes;
                renderers[i].reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            initialized=true;ApplyTemplate();
        }
        void ApplyTemplate()
        {
            template=library.Get(type,state.Tier);
            groups[1].localPosition=TowerModelLibrary.Vector(template.Recipe.HeadPivot);
            for(int i=0;i<TowerModelRecipes.GroupCount;i++)
            {
                filters[i].sharedMesh=template.Groups[i];
                renderers[i].enabled=!destroyed&&template.Groups[i]!=null;
            }
            boltVisible=true;ApplyElementPresentationTier();
        }
        // Bind only after Director has committed/reflected the host-owned building rank.
        // A shop button or a pending client request must never call this method.
        public bool ApplyAcceptedTier(int tier,int revision)
        {
            if(!initialized||destroyed)return false;
            int previous=state.Tier;
            if(!state.ApplyAcceptedTier(tier,revision))return false;
            if(previous!=tier)ApplyTemplate();
            return true;
        }
        // Serial belongs to the accepted shot stream for this tower lifetime; it is not a client cast request.
        public bool OnAcceptedShot(uint serial,Vector3 acceptedPrimary,float acceptedInterval)
        {
            if(!initialized||destroyed||!Finite(acceptedPrimary)||!state.OnAcceptedShot(serial,acceptedInterval))return false;
            Vector3 local=transform.InverseTransformPoint(acceptedPrimary);local.y=0;
            if(local.sqrMagnitude>.0001f){targetYaw=Quaternion.LookRotation(local.normalized,Vector3.up);groups[1].localRotation=targetYaw;}
            elementalPresentation?.AcceptedShot(serial,VisualMuzzle,acceptedPrimary);
            return true;
        }
        public bool ApplyAcceptedCooldown(float secondsRemaining,float interval)
            =>initialized&&!destroyed&&state.ApplyAcceptedCooldown(secondsRemaining,interval);
        public void OnAcceptedDestroyed()
        {
            if(!initialized||destroyed)return;destroyed=true;StopElementPresentation();
            for(int i=0;i<renderers.Length;i++)renderers[i].enabled=false;
        }
        public void Tick(float deltaTime,bool simulationActive,bool nearbyDecorations)
        {
            if(!initialized||destroyed||!simulationActive||float.IsNaN(deltaTime)||float.IsInfinity(deltaTime)||deltaTime<0||deltaTime>1)return;
            TowerModelPose p=state.Tick(deltaTime,nearbyDecorations);
            TickElementPresentation(deltaTime,nearbyDecorations);
            groups[1].localRotation=Quaternion.Slerp(groups[1].localRotation,targetYaw,Mathf.Clamp01(deltaTime*9f));
            groups[2].localPosition=new Vector3(0,0,p.Recoil);groups[2].localRotation=Quaternion.Euler(p.Pitch,0,0);
            groups[3].localPosition=new Vector3(0,p.CoreLift,0);groups[3].localRotation=Quaternion.Euler(0,p.CoreYaw,0);groups[3].localScale=Vector3.one*p.CoreScale;
            groups[4].localRotation=Quaternion.Euler(0,p.RotorYaw,0);
            groups[5].localPosition=new Vector3(0,0,p.Recoil*1.35f);
            if(boltVisible!=p.BoltVisible){boltVisible=p.BoltVisible;renderers[5].enabled=boltVisible&&template.Groups[5]!=null;}
            glowBlock.SetColor(EmissionId,Color.white*(1.8f*p.Glow));
            renderers[2].SetPropertyBlock(glowBlock);renderers[3].SetPropertyBlock(glowBlock);renderers[4].SetPropertyBlock(glowBlock);
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing tower Head-local/Y-up pivots and accepted target world points remain canonical; foundation/pads/target lock/range unchanged.
// Array Formats: Existing saved72 meshes/four materials remain borrowed; additive12 original FX templates, two renderers and one fixed96-byte native state/pose pair per instance.
// Dependencies/Pointers: Existing accepted tier/shot owners feed cosmetics only; Burst fences finish before Object writes. No authority/economy/radius/status/audio/network rewrite; native acceptance pending.
