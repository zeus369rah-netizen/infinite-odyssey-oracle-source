using System;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleEnemy
    {
        internal bool NativePoseEligible015=>!NativeGuardianStyle015&&Alive&&!ranged&&!IsFlying&&!dormant&&seatedBlend<=.001f&&Archetype==MonsterArchetype.Stalker&&session!=null&&session.Defense!=null&&session.Defense.Running;
        internal bool NativeShadePoseEligible015=>Alive&&ranged&&!dormant&&seatedBlend<=.001f&&Archetype==MonsterArchetype.Shade&&session!=null&&((session.Defense!=null&&session.Defense.Running)||(TrainingLabBound&&trainingLabOwner!=null&&(trainingLabOwner.OwnsDummy(this)||(trainingLabOwner.SharedClient&&object.ReferenceEquals(trainingLabOwner.Dummy,this)))));
        internal bool NativeSpiderPoseEligible015=>Alive&&!ranged&&!IsFlying&&!dormant&&seatedBlend<=.001f&&Archetype==MonsterArchetype.Spider&&session!=null&&((session.Defense!=null&&session.Defense.Running)||(TrainingLabBound&&trainingLabOwner!=null&&(trainingLabOwner.OwnsDummy(this)||(trainingLabOwner.SharedClient&&object.ReferenceEquals(trainingLabOwner.Dummy,this)))));
        internal bool NativeGuardianStyle015=>pooledView015!=null&&pooledStyle015==MonsterVoxelCosmetics.Style.GuardianGolem;
        internal bool NativeGuardianPoseEligible015=>NativeGuardianStyle015&&PoolLeased015&&Alive&&!ranged&&!IsFlying&&!dormant&&seatedBlend<=.001f&&
            body!=null&&(body.localScale-Vector3.one*PresentationScale).sqrMagnitude<.0000000001f&&
            (transform.lossyScale-Vector3.one).sqrMagnitude<.0000000001f&&Vector3.Dot(transform.up,Vector3.up)>.99999f&&
            TryGuardianRecoveryProgress015(0f,out _)&&session!=null&&((session.Defense!=null&&session.Defense.Running)||
            (TrainingLabBound&&trainingLabOwner!=null&&(trainingLabOwner.OwnsDummy(this)||(trainingLabOwner.SharedClient&&object.ReferenceEquals(trainingLabOwner.Dummy,this)))));
        internal bool TryGuardianRecoveryProgress015(float progress,out float value)
        {
            bool remote=owner!=null&&owner.RemotePresentation&&remoteAttackPhase>=0;
            return GuardianPoseRecipe015.TryRecoveryProgress(progress,remote?remoteStrikeSeconds:AttackStrikeSeconds,
                remote?remoteRecoverySeconds:AttackRecoverySeconds,out value);
        }
        public bool NativeBlendTreeGuardian015=>NativeGuardianStyle015&&NativeBlendTreePoseActive015;
#if UNITY_EDITOR
        // Cold, read-only bridge to the SAME current V2 geometry/footprint authority.
        // The Editor baker reads actual saved meshes; it never builds substitute V1 meshes.
        public static void ReadGuardianAuthoringParts015(string[] labels,int[] parents,Vector3[] positions,
            int[] channels,Bounds[] bounds,int[] triangleCounts)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stopped native authoring only.");
            if(labels==null||parents==null||positions==null||channels==null||bounds==null||triangleCounts==null||
                labels.Length!=11||parents.Length!=11||positions.Length!=11||channels.Length!=11||bounds.Length!=11||triangleCounts.Length!=11)
                throw new ArgumentException("Exactly11 caller-owned descriptor slots required.");
            var parts=MonsterDetailAdapterV2.Apply(MonsterVoxelCosmetics.Style.GuardianGolem,MonsterDetailSourceV2.Build(MonsterVoxelCosmetics.Style.GuardianGolem));
            if(parts.Length!=11)throw new InvalidOperationException("Original Guardian part count changed.");
            for(int i=0;i<11;i++)
            {
                var part=parts[i];int anchor=part.Anchor;
                if(anchor==0){if(part.Label=="Torso"||part.Label=="Chest rune")anchor=3;else if(part.Label=="Stone brow"||part.Label=="Face shadow"||part.Label=="Left rune eye"||part.Label=="Right rune eye")anchor=4;}
                labels[i]=part.Label;parents[i]=anchor;channels[i]=part.Channel;
                positions[i]=part.Origin-(anchor==3?Vector3.up*.65f:anchor==4?Vector3.up*1.48f:Vector3.zero);
                bool first=true;Bounds result=default;
                for(int box=0;box<part.Boxes.Length;box++)for(int corner=0;corner<8;corner++)
                {
                    var b=part.Boxes[box];Vector3 sign=new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                    Vector3 p=b.Center-part.Origin+Quaternion.Euler(b.Euler)*Vector3.Scale(b.Size*.5f,sign);
                    if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);
                }
                if(first)throw new InvalidOperationException("Missing original Guardian boxes.");bounds[i]=result;triangleCounts[i]=checked(part.Boxes.Length*12);
            }
        }
        public static void MeasureGuardianAuthoring015(Transform root,Transform body,out Bounds hitbox,
            out float navigation,out float crowd,out float capsuleBottom,out float capsuleTop)
        {
            if(Application.isPlaying||root==null||body==null)throw new InvalidOperationException("Stopped original saved rig required.");
            var p=PresentationFootprint.Measure(root,body,body.Find("Left arm pivot"),body.Find("Right arm pivot"));
            hitbox=p.Hitbox;navigation=p.NavigationRadius;crowd=p.CrowdRadius;capsuleBottom=p.CapsuleBottom;capsuleTop=p.CapsuleTop;
        }
#endif
        public bool NativeBlendTreeSpider015=>bodyMotion!=null&&bodyMotion.NativeSpiderPose015;
        public bool NativeBlendTreeShade015=>bodyMotion!=null&&bodyMotion.NativeShadePose015;
        public bool NativeBlendTreePoseActive015=>bodyMotion!=null&&bodyMotion.NativePoseActive015;
        public ulong NativeBlendTreeEvaluations015=>bodyMotion!=null?bodyMotion.NativePoseEvaluations015:0ul;
        public uint NativeBlendTreeGeneration015=>bodyMotion!=null?bodyMotion.NativePoseGeneration015:0u;
        partial void ResetAdditionalPooledState015(){bodyMotion?.ResetNativePose015(PoolGeneration015);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing actor eligibility and presentation-only pool lifetime/generation.
// Array Formats: No new gameplay/native state; public diagnostics describe actual active native graph only.
// Dependencies/Pointers: One existing reset hook preserved; Guardian style/accepted local-or-replica recovery read-only. Editor bridge exposes actual V2 descriptors/current footprint only; no HP/root/clock/damage changes, defaultOFF/sharedcap2.
