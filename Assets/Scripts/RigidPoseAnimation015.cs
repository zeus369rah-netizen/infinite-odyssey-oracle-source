using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace OdysseyOracle
{
    public sealed class RigidPoseAnimation015:IDisposable
    {
        public const int Capacity=2,BindingCount=6,MaximumBindingCount=SpiderPoseRecipe015.BindingCount;
        static readonly string[] Paths={"Left arm pivot","Right arm pivot","Left leg","Right leg","Procedural torso pivot","Procedural torso pivot/Procedural head pivot"};
        static readonly int Speed=Animator.StringToHash("Speed01"),Cycle=Animator.StringToHash("Cycle01"),Action=Animator.StringToHash("Action01"),Phase=Animator.StringToHash("Phase01"),Combat=Animator.StringToHash("InCombat"),Locomotion=Animator.StringToHash("Locomotion"),Flying=Animator.StringToHash("Flying01");
        static int instances;
        readonly Transform[] pivots=new Transform[MaximumBindingCount];
        readonly Vector3[] positions=new Vector3[MaximumBindingCount];
        readonly Quaternion[] rotations=new Quaternion[MaximumBindingCount];
        readonly OracleEnemy actor;
        readonly Transform body;
        readonly RigidPosePilot015 stalkerProfile,shadeProfile,spiderProfile,guardianProfile;
        RigidPosePilot015 profile;
        readonly AnimatorControllerPlayable stalkerController,shadeController,spiderController,guardianController;
        AnimationPlayableOutput output;
        Animator animator;PlayableGraph graph;AnimatorControllerPlayable controller;
        bool disposed,ownsAnimator,countsPilot,active,bindingsReady;
        uint generation;float idleCycle;int capturedBindings;
        public bool Active=>active&&!disposed&&graph.IsValid();
        public ulong EvaluationCount{get;private set;}
        public uint Generation=>generation;
        public bool IsShadeProfile=>ReferenceEquals(profile,shadeProfile)&&shadeProfile!=null;
        public bool IsSpiderProfile=>ReferenceEquals(profile,spiderProfile)&&spiderProfile!=null;
        public bool IsGuardianProfile=>ReferenceEquals(profile,guardianProfile)&&guardianProfile!=null;
        AnimatorControllerPlayable SelectedController015=>IsGuardianProfile?guardianController:IsSpiderProfile?spiderController:IsShadeProfile?shadeController:stalkerController;
        public static int InstanceCount=>instances;
        public bool HasBoundNativeGraph=>!disposed&&graph.IsValid()&&animator!=null&&animator.hasBoundPlayables;
        public static string BindingPath(int index)=>index>=0&&index<BindingCount?Paths[index]:null;

        RigidPoseAnimation015(OracleEnemy actor,Transform body,RigidPosePilot015 stalkerProfile,RigidPosePilot015 shadeProfile,RigidPosePilot015 spiderProfile,RigidPosePilot015 guardianProfile,bool factory)
        {
            this.actor=actor;this.body=body;this.stalkerProfile=stalkerProfile;this.shadeProfile=shadeProfile;this.spiderProfile=spiderProfile;this.guardianProfile=guardianProfile;
            profile=actor!=null&&actor.NativeGuardianStyle015?guardianProfile??stalkerProfile??shadeProfile??spiderProfile:
                actor!=null&&actor.Archetype==OracleEnemy.MonsterArchetype.Spider?spiderProfile??stalkerProfile??shadeProfile:
                actor!=null&&actor.Archetype==OracleEnemy.MonsterArchetype.Shade?shadeProfile??stalkerProfile??spiderProfile:stalkerProfile??shadeProfile??spiderProfile??guardianProfile;
            if(profile==null)throw new InvalidOperationException("An enabled verified pose profile is required.");
            // Universal pooled roots may currently expose another style; keep this cold graph dormant until a matching life binds it.
            if(!CaptureBindings()&&factory)throw new InvalidOperationException("Rigid bindings missing.");
            if(body.GetComponent<Animator>()!=null)throw new InvalidOperationException("Body already has an Animator owner.");
            try
            {
                animator=body.gameObject.AddComponent<Animator>();ownsAnimator=true;animator.avatar=profile.Avatar;animator.applyRootMotion=false;animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create("Original rigid pose015");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                if(stalkerProfile!=null)stalkerController=AnimatorControllerPlayable.Create(graph,stalkerProfile.Controller);
                if(shadeProfile!=null)shadeController=AnimatorControllerPlayable.Create(graph,shadeProfile.Controller);
                if(spiderProfile!=null)spiderController=AnimatorControllerPlayable.Create(graph,spiderProfile.Controller);
                if(guardianProfile!=null)guardianController=AnimatorControllerPlayable.Create(graph,guardianProfile.Controller);
                controller=SelectedController015;
                output=AnimationPlayableOutput.Create(graph,"Original rigid pose015",animator);output.SetSourcePlayable(controller);graph.Play();
                if(!factory){instances++;countsPilot=true;}
            }
            catch{Dispose();throw;}
        }
        internal static RigidPoseAnimation015 TryCreate(OracleEnemy actor,Transform body)
        {
            if(instances>=Capacity||actor==null||body==null)return null;
            var stalker=Resources.Load<RigidPosePilot015>(RigidPosePilot015.ResourcePath);
            var shade=Resources.Load<RigidPosePilot015>(ShadePoseRecipe015.ResourcePath);
            var spider=Resources.Load<RigidPosePilot015>(SpiderPoseRecipe015.ResourcePath);
            var guardian=Resources.Load<RigidPosePilot015>(GuardianPoseRecipe015.ResourcePath);
            if(stalker==null||!stalker.PilotEnabled||!stalker.Ready)stalker=null;
            if(shade==null||!shade.PilotEnabled||!shade.Ready)shade=null;
            if(spider==null||!spider.PilotEnabled||!spider.Ready)spider=null;
            if(guardian==null||!guardian.PilotEnabled||!guardian.Ready)guardian=null;
            if(stalker==null&&shade==null&&spider==null&&guardian==null)return null;
            try{return new RigidPoseAnimation015(actor,body,stalker,shade,spider,guardian,false);}catch(Exception){return null;}
        }
#if UNITY_EDITOR
        public static RigidPoseAnimation015 CreateForFactory(Transform body,RigidPosePilot015 profile)
        {if(Application.isPlaying||body==null||profile==null||profile.Controller==null||profile.Avatar==null||!profile.Avatar.isValid)throw new InvalidOperationException("Cold native factory rig/controller required.");return new RigidPoseAnimation015(null,body,profile,null,null,null,true);}
        public static RigidPoseAnimation015 CreateShadeForFactory(Transform body,RigidPosePilot015 profile)
        {if(Application.isPlaying||body==null||profile==null||profile.Controller==null||profile.Avatar==null||!profile.Avatar.isValid)throw new InvalidOperationException("Cold native Shade factory rig/controller required.");return new RigidPoseAnimation015(null,body,null,profile,null,null,true);}
        public static RigidPoseAnimation015 CreateProfilesForFactory(Transform body,RigidPosePilot015 stalker,RigidPosePilot015 shade)
        {if(Application.isPlaying||body==null||stalker==null||shade==null||!stalker.Ready||!shade.Ready)throw new InvalidOperationException("Cold verified pair required.");return new RigidPoseAnimation015(null,body,stalker,shade,null,null,true);}
        // Cold scratch stress only: identical three-controller construction to an all-enabled production graph.
        public static RigidPoseAnimation015 CreateAllProfilesForFactory(Transform body,RigidPosePilot015 stalker,RigidPosePilot015 shade,RigidPosePilot015 spider)
        {if(Application.isPlaying||body==null||stalker==null||shade==null||spider==null||!stalker.Ready||!shade.Ready||!spider.Ready)throw new InvalidOperationException("Cold verified three-profile rig required.");return new RigidPoseAnimation015(null,body,stalker,shade,spider,null,true);}
        public static RigidPoseAnimation015 CreateAllProfilesForFactory(Transform body,RigidPosePilot015 stalker,RigidPosePilot015 shade,RigidPosePilot015 spider,RigidPosePilot015 guardian)
        {if(Application.isPlaying||body==null||stalker==null||shade==null||spider==null||guardian==null||!stalker.Ready||!shade.Ready||!spider.Ready||!guardian.Ready)throw new InvalidOperationException("Cold verified four-profile rig required.");return new RigidPoseAnimation015(null,body,stalker,shade,spider,guardian,true);}
        public static RigidPoseAnimation015 CreateGuardianForFactory(Transform body,RigidPosePilot015 profile)
        {if(Application.isPlaying||body==null||profile==null||profile.Controller==null||profile.Avatar==null||!profile.Avatar.isValid)throw new InvalidOperationException("Cold native Guardian factory required.");return new RigidPoseAnimation015(null,body,null,null,null,profile,true);}
        public float FactoryGuardianUpperWeight015=>IsGuardianProfile?controller.GetLayerWeight(1):0f;
        public bool FactoryGuardianUpperCombat015=>IsGuardianProfile&&controller.GetCurrentAnimatorStateInfo(1).shortNameHash==Animator.StringToHash("Combat");
        //0Stalker/1Shade/2Spider/3Guardian; select only precreated playables and capture the authored new life.
        public void SelectFactoryProfileLife(uint value,int profileKind)
        {if(Application.isPlaying||actor!=null||value<=generation||profileKind<0||profileKind>3)throw new InvalidOperationException("Cold scratch new generation and profile0..3 required.");var next=profileKind==3?guardianProfile:profileKind==2?spiderProfile:profileKind==1?shadeProfile:stalkerProfile;if(next==null||!next.Ready)throw new InvalidOperationException("Verified profile was not precreated.");profile=next;controller=SelectedController015;ResetForLife(value);}
        public void SelectFactoryProfileLife(uint value,bool shade)
        {if(Application.isPlaying||actor!=null||value<=generation)throw new InvalidOperationException("Cold scratch new generation required.");var next=shade?shadeProfile:stalkerProfile;if(next==null)throw new InvalidOperationException("Profile was not precreated.");profile=next;controller=shade?shadeController:stalkerController;ResetForLife(value);}
        public static RigidPoseAnimation015 CreateSpiderForFactory(Transform body,RigidPosePilot015 profile)
        {if(Application.isPlaying||body==null||profile==null||profile.Controller==null||profile.Avatar==null||!profile.Avatar.isValid)throw new InvalidOperationException("Cold native Spider factory rig/controller required.");return new RigidPoseAnimation015(null,body,null,null,profile,null,true);}
        public bool FactoryInTransition015=>controller.IsInTransition(0)||(IsGuardianProfile&&controller.IsInTransition(1));
        public void EvaluateFactoryTransition015(RigidPoseFrame015 frame,float dt)
        {if(Application.isPlaying||actor!=null)throw new InvalidOperationException("Cold scratch factory lease required.");graph.Play();Set(frame);graph.Evaluate(dt);EvaluationCount++;}
        public void ResetFactoryLife(uint value)
        {if(Application.isPlaying||actor!=null)throw new InvalidOperationException("Cold scratch factory lease required.");ResetForLife(value);}
        public void EvaluateFactory(RigidPoseFrame015 frame,float dt,string state)
        {if(IsGuardianProfile){controller.Play(Locomotion,0,0f);controller.Play(state,1,0f);}else controller.Play(state,0,0f);Set(frame);graph.Evaluate(dt);EvaluationCount++;}
#endif
        internal void SynchronizeOwner()
        {
            bool next=!disposed&&bindingsReady&&graph.IsValid()&&profile!=null&&profile.PilotEnabled&&profile.Ready&&actor!=null&&(IsGuardianProfile?actor.NativeGuardianPoseEligible015:IsSpiderProfile?actor.NativeSpiderPoseEligible015:IsShadeProfile?actor.NativeShadePoseEligible015:actor.NativePoseEligible015);
            if(active==next)return;
            active=next;
            if(active){graph.Play();ResetControllerLayers015();}
            else{graph.Stop();RestoreBindings();}
        }
        bool SelectProfileForActor015()
        {
            var next=actor.NativeGuardianStyle015?guardianProfile:
                actor.Archetype==OracleEnemy.MonsterArchetype.Spider?spiderProfile:
                actor.Archetype==OracleEnemy.MonsterArchetype.Shade?shadeProfile:
                actor.Archetype==OracleEnemy.MonsterArchetype.Stalker?stalkerProfile:null;
            if(next==null||!next.PilotEnabled||!next.Ready){bindingsReady=false;return false;}
            profile=next;controller=SelectedController015;return true;
        }
        internal void ResetForLife(uint value)
        {
            if(disposed)return;bool newLife=value!=generation;
            active=false;generation=value;idleCycle=0f;EvaluationCount=0;graph.Stop();
            // Retirement uses the same generation; do not capture its last animated pose.
            if(!newLife){if(bindingsReady)RestoreBindings();return;}
            // Select only precreated playables; the new style is already authored.
            if(actor!=null&&!SelectProfileForActor015())return;
            // Activation already applied the new style. Capture it before any restore/rebind.
            if(!CaptureBindings())return;
            Vector3 position=body.localPosition,scale=body.localScale;Quaternion rotation=body.localRotation;
            animator.avatar=profile.Avatar;output.SetSourcePlayable(controller);
            animator.Rebind();body.localPosition=position;body.localRotation=rotation;body.localScale=scale;
            RestoreBindings();
            Set(new RigidPoseFrame015{Generation=value});ResetControllerLayers015();
        }
        void ResetControllerLayers015()
        {controller.Play(Locomotion,0,0f);if(IsGuardianProfile)controller.Play(Locomotion,1,0f);}
        internal void Evaluate(float dt,float weight,float cycle,OracleEnemy.GuardianAttackPhase phase,float progress)
        {
            if(!Active||dt<=0f||float.IsNaN(dt)||float.IsInfinity(dt))return;
            if(IsGuardianProfile&&phase==OracleEnemy.GuardianAttackPhase.Recovery&&actor!=null&&!actor.TryGuardianRecoveryProgress015(progress,out progress)){active=false;graph.Stop();RestoreBindings();return;}
            idleCycle=weight<=.001f?Mathf.Repeat(idleCycle+Mathf.Min(dt,.1f)*(IsGuardianProfile?GuardianPoseRecipe015.IdleHertz:.25f),1f):Mathf.Repeat(cycle*(IsGuardianProfile?GuardianPoseRecipe015.UnwrappedCycleScale:1f),1f);
            var frame=new RigidPoseFrame015{Speed01=Mathf.Clamp01(weight),Cycle01=idleCycle,Action01=phase==OracleEnemy.GuardianAttackPhase.Strike?.5f:phase==OracleEnemy.GuardianAttackPhase.Recovery?1f:0f,Phase01=Mathf.Clamp01(progress),Generation=generation,Sequence=actor!=null?(uint)Mathf.Max(0,actor.AttackPresentationSequence):0u,Flags=((phase==OracleEnemy.GuardianAttackPhase.Windup||phase==OracleEnemy.GuardianAttackPhase.Strike||phase==OracleEnemy.GuardianAttackPhase.Recovery)?1u:0u)|(actor!=null&&actor.IsFlying?2u:0u)};
            Set(frame);graph.Evaluate(Mathf.Min(dt,.1f));EvaluationCount++;
        }
        void Set(RigidPoseFrame015 value)
        {controller.SetFloat(Speed,value.Speed01);controller.SetFloat(Cycle,value.Cycle01);controller.SetFloat(Action,value.Action01);controller.SetFloat(Phase,value.Phase01);controller.SetBool(Combat,(value.Flags&1u)!=0);if(IsShadeProfile)controller.SetFloat(Flying,(value.Flags&2u)!=0?1f:0f);if(IsGuardianProfile)controller.SetLayerWeight(1,GuardianPoseRecipe015.UpperWeight(value.Action01,value.Phase01,(value.Flags&1u)!=0));}
        void RestoreBindings(){for(int i=0;i<capturedBindings;i++)if(pivots[i]!=null){pivots[i].localPosition=positions[i];pivots[i].localRotation=rotations[i];}}
        bool CaptureBindings()
        {bindingsReady=false;capturedBindings=0;int count=IsSpiderProfile?SpiderPoseRecipe015.BindingCount:BindingCount;for(int i=0;i<count;i++){var value=body.Find(IsSpiderProfile?SpiderPoseRecipe015.BindingPath(i):Paths[i]);if(value==null)return false;if(IsGuardianProfile){var rest=GuardianPoseRecipe015.RestPosition(i);if(Vector3.Distance(value.localPosition,new Vector3(rest.x,rest.y,rest.z))>.0001f||Quaternion.Angle(value.localRotation,Quaternion.identity)>.01f)return false;}pivots[i]=value;positions[i]=value.localPosition;rotations[i]=value.localRotation;}capturedBindings=count;bindingsReady=true;return true;}
        public void Dispose()
        {
            if(disposed)return;disposed=true;active=false;if(graph.IsValid())graph.Destroy();
            if(ownsAnimator&&animator!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(animator);else UnityEngine.Object.DestroyImmediate(animator);}animator=null;
            if(countsPilot){instances=Mathf.Max(0,instances-1);countsPilot=false;}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Body-local rigid curves only; world actor/body-root/collider transforms are never graph targets.
// Array Formats: Existing32B input and max10cached bindings; six for Guardian. Shared max2manual graphs, optional fourth precreated controller; no hot graph/profile creation.
// Dependencies/Pointers: Borrowed root-native verified assets; style-first Guardian never aliases Stalker; accepted owner duration remap/two-layer reset only. Existing generation/root/HP/damage owners unchanged; defaultOFF, native/Web/performance pending.
