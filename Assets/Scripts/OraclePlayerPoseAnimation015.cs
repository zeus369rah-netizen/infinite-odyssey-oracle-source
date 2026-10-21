using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace OdysseyOracle
{
    public sealed class OraclePlayerPoseAnimation015:IDisposable
    {
        public const int Capacity=2;
        static int instances;
        static readonly int Speed=Animator.StringToHash("Speed01"),Cycle=Animator.StringToHash("Cycle01"),Cast=Animator.StringToHash("Cast01"),Motion=Animator.StringToHash("Motion01"),Casting=Animator.StringToHash("Casting");
        readonly RigidPosePilot015 profile;readonly Transform[] pivots;readonly Vector3[] positions;readonly Quaternion[] rotations;
        Animator animator;PlayableGraph graph;AnimatorControllerPlayable controller;bool disposed,active,counted;float age=10f;
        public static int InstanceCount=>instances;
        public bool Active=>active&&!disposed&&graph.IsValid();
        public bool NativeBound=>!disposed&&graph.IsValid()&&animator!=null&&animator.hasBoundPlayables;
        public ulong EvaluationCount{get;private set;}public uint Sequence{get;private set;}
        public float CastProgress=>Mathf.Clamp01(age/OraclePlayerPoseRecipe015.CastSeconds);
        OraclePlayerPoseAnimation015(Transform root,bool hands,RigidPosePilot015 profile,bool factory)
        {
            this.profile=profile;int count=hands?1:5;
            pivots=new Transform[count];positions=new Vector3[count];rotations=new Quaternion[count];
            for(int i=0;i<count;i++){pivots[i]=root.Find(OraclePlayerPoseRecipe015.Path(hands,i));if(pivots[i]==null)throw new InvalidOperationException("Original Oracle pose binding missing.");positions[i]=pivots[i].localPosition;rotations[i]=pivots[i].localRotation;}
            if(root.GetComponent<Animator>()!=null)throw new InvalidOperationException("Existing Oracle Animator owner.");
            try
            {
                animator=root.gameObject.AddComponent<Animator>();animator.avatar=profile.Avatar;animator.applyRootMotion=false;animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create(hands?"Original Oracle hands015":"Original Oracle body015");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);controller=AnimatorControllerPlayable.Create(graph,profile.Controller);
                var output=AnimationPlayableOutput.Create(graph,"Original Oracle pose015",animator);output.SetSourcePlayable(controller);graph.Play();graph.Stop();
                if(!factory){instances++;counted=true;}
            }
            catch{Dispose();throw;}
        }
        internal static OraclePlayerPoseAnimation015 PrepareCold(Transform root,bool hands)
        {
            if(root==null||instances>=Capacity)return null;
            var profile=Resources.Load<RigidPosePilot015>(hands?OraclePlayerPoseRecipe015.HandsPath:OraclePlayerPoseRecipe015.BodyPath);
            if(profile==null||!profile.PilotEnabled||!profile.Ready)return null;
            try{return new OraclePlayerPoseAnimation015(root,hands,profile,false);}catch(Exception){return null;}
        }
#if UNITY_EDITOR
        public static OraclePlayerPoseAnimation015 CreateForFactory(Transform root,bool hands,RigidPosePilot015 profile)
        {if(Application.isPlaying||root==null||profile==null||profile.Controller==null||profile.Avatar==null||!profile.Avatar.isValid)throw new InvalidOperationException("Cold original Oracle factory rig required.");return new OraclePlayerPoseAnimation015(root,hands,profile,true);}
        public void EvaluateFactory(OraclePlayerPoseFrame015 value,string state){controller.Play(state,0,0);Set(value);graph.Evaluate(.1f);EvaluationCount++;}
#endif
        internal void PulseAccepted()
        {if(disposed||Sequence==uint.MaxValue)return;Sequence++;age=0f;}
        internal bool Evaluate(float dt,float speed,float cycle,float motion,bool permitted)
        {
            bool next=!disposed&&profile.PilotEnabled&&profile.Ready&&permitted&&graph.IsValid();
            if(next!=active){active=next;if(active)graph.Play();else{graph.Stop();Restore();}}
            if(!active)return false;
            if(!Finite(dt)||dt<=0f)return true;dt=Mathf.Min(dt,.1f);
            age=Mathf.Min(10f,age+dt);
            Set(new OraclePlayerPoseFrame015{Speed01=Finite(speed)?Mathf.Clamp01(speed):0,Cycle01=Finite(cycle)?Mathf.Repeat(cycle,1):0,Cast01=CastProgress,Motion01=Finite(motion)?Mathf.Clamp01(motion):0,Flags=age<OraclePlayerPoseRecipe015.CastSeconds?1u:0u,Sequence=Sequence});
            graph.Evaluate(dt);EvaluationCount++;return true;
        }
        void Set(OraclePlayerPoseFrame015 value){controller.SetFloat(Speed,value.Speed01);controller.SetFloat(Cycle,value.Cycle01);controller.SetFloat(Cast,value.Cast01);controller.SetFloat(Motion,value.Motion01);controller.SetBool(Casting,(value.Flags&1)!=0);}
        internal void Reset(){if(disposed||(!active&&age>=10f&&Sequence==0&&EvaluationCount==0))return;active=false;age=10f;Sequence=0;EvaluationCount=0;graph.Stop();Restore();}
        void Restore(){for(int i=0;i<pivots.Length;i++)if(pivots[i]!=null){pivots[i].localPosition=positions[i];pivots[i].localRotation=rotations[i];}}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public void Dispose(){if(disposed)return;disposed=true;active=false;if(graph.IsValid())graph.Destroy();Restore();if(animator!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(animator);else UnityEngine.Object.DestroyImmediate(animator);}animator=null;if(counted){instances=Mathf.Max(0,instances-1);counted=false;}}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Five original body-local children or one camera-local child; animation root transforms never targeted.
// Array Formats: Fixed5or1cached rest bindings, one borrowed profile/controller/avatar per graph, cap2player graphs independent monster cap2.
// Dependencies/Pointers: Explicit cold startup only creates graphs. Original Player/Combat/Rooms own all gameplay; accepted launch only resets private visual age; graph disposal retains shared assets.
