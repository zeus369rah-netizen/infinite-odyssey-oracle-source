using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    public sealed partial class NpcBlacksmith
    {
        public bool SmallFurnaceReady{get;private set;}
        public bool SmallFurnaceFlameReady{get;private set;}
        public int SmallFurnaceQuadCount{get;private set;}
        public bool SmallFurnaceReactionReady015{get;private set;}
        public Transform FurnaceRoot015=>smithFurnaceRoot015;
        const int SmithFurnaceEmitter015=2147482991;
        DungeonWorld smithFurnaceWorld015;Transform smithFurnaceRoot015;GameAudioDirector smithFurnaceAudio015;bool smithFurnaceAudioRegistered015;
        void BuildSmallRefugeFurnace(Material[] borrowedPaints)
        {
            var session=GameSession.Instance;var world=session!=null?session.World:null;
            if(world==null||!world.IsTutorialRefuge||world.RoomV2Layout==null)return;
            SmithFurnacePose32 placement=SmithFurnacePlacementCold015.Resolve(world,transform);
            Mesh mesh=SmithFurnaceMeshBuilder.Build(out int quads);resources.Add(mesh);
            var root=new GameObject("Original supported full-height workshop furnace");root.transform.SetParent(visual.transform,false);
            root.transform.localPosition=transform.InverseTransformPoint(new Vector3(placement.X,placement.Y,placement.Z));
            root.transform.localRotation=Quaternion.Euler(0,placement.Yaw,0);root.transform.localScale=Vector3.one*placement.Scale;
            root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();
            var furnacePaints=new[]{borrowedPaints[Leather],borrowedPaints[Coal],borrowedPaints[Steel],borrowedPaints[Ember],borrowedPaints[Gold]};renderer.sharedMaterials=furnacePaints;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            // Physical solid remains outside talk/walk lanes. IgnoreRaycast prevents expanding the NPC's shop-interaction target to the furnace.
            Vector3 center=root.transform.localPosition+Vector3.up*(placement.Height*.5f);
            var solid=AddCollider("Full-height Smith furnace solid",center,new Vector3(placement.HalfX*2,placement.Height,placement.HalfZ*2));solid.gameObject.layer=2;
            SmallFurnaceFlameReady=LivingCampfirePresentation.TryCreate(root.transform,new Vector3(0,.34f,0),.28f,out var flame);
            SmallFurnaceReactionReady015=SmallFurnaceFlameReady&&world.BindSmithFurnace015(flame,renderer,furnacePaints,root.transform.TransformPoint(new Vector3(0,.438f,0)),.40f);
            if(!SmallFurnaceReactionReady015){flame?.SetRoomReactionFuel(0);RoomReactionGlowBinding.Create(renderer,null,furnacePaints).Apply(0);}
            smithFurnaceWorld015=world;smithFurnaceRoot015=root.transform;smithFurnaceAudio015=session.Audio;
            smithFurnaceAudioRegistered015=smithFurnaceAudio015!=null&&smithFurnaceAudio015.SetAmbientEmitter(SmithFurnaceEmitter015,"torch_crackle",root.transform.TransformPoint(new Vector3(0,.34f,0)),true,9f,0f);
            SmallFurnaceQuadCount=quads;SmallFurnaceReady=true;
        }
        void TickSmithFurnace015()
        {
            if(!smithFurnaceAudioRegistered015||smithFurnaceAudio015==null||smithFurnaceRoot015==null)return;
            var session=GameSession.Instance;
            bool active=Application.isFocused&&isActiveAndEnabled&&smithFurnaceRoot015.gameObject.activeInHierarchy&&session!=null&&session.HasStarted&&!session.Paused&&!session.IsDead&&!session.IsDowned&&!session.Victory&&session.World==smithFurnaceWorld015&&smithFurnaceWorld015!=null&&smithFurnaceWorld015.IsTutorialRefuge&&!smithFurnaceWorld015.IsCoopArena&&SmallFurnaceReactionReady015;
            float fuel=active?smithFurnaceWorld015.SmithFurnaceFuel015:0f;
            if(!smithFurnaceAudio015.UpdateSmithFurnaceAmbient015(SmithFurnaceEmitter015,smithFurnaceRoot015.TransformPoint(new Vector3(0,.34f,0)),fuel))smithFurnaceAudioRegistered015=false;
        }
        void OnDisable(){if(smithFurnaceAudioRegistered015&&smithFurnaceAudio015!=null)smithFurnaceAudio015.UpdateSmithFurnaceAmbient015(SmithFurnaceEmitter015,Vector3.zero,0);}
        void DisposeSmithFurnace015()
        {
            if(smithFurnaceAudioRegistered015&&smithFurnaceAudio015!=null)smithFurnaceAudio015.RemoveAmbientEmitter(SmithFurnaceEmitter015);
            smithFurnaceAudioRegistered015=false;smithFurnaceAudio015=null;smithFurnaceRoot015=null;smithFurnaceWorld015=null;SmallFurnaceReactionReady015=false;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: First actual east-alcove midpoint and downward certified bottomY0, mouth toward hall-X; original Smith/anvil/coal positions retained.
// Array Formats: One reused greedy mesh/five borrowed materials, one conservative collider and existing424vertex flame/ember/smoke; one cold ambient record, no new source/light/hot allocation.
// Dependencies/Pointers: Completed native layout/floor proofs precede scene publication. Existing host room node/snapshot owns Fire-Wind fuel; Smith retirement owns resources/audio, original voice/economy unchanged.
