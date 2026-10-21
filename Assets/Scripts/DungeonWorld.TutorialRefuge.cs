using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    public sealed partial class DungeonWorld
    {
        // # ARCHITECTURE SNAPSHOT
        // Opt-in008 tutorial/refuge ONLY. Pure RoomV2 records feed the existing
        // World-owned4m render batches and simple structural colliders. Borrowed
        // original saved props have explicit references; no asset lookup per frame.
        // Existing controller, combat, arena, NPC/dialogue/campaign owners remain.
        private TutorialRoomV2Layout tutorialRefuge;
        private TutorialRoomV2Catalog refugeCatalog;
        private float TutorialPierAnchorX=>tutorialRefuge!=null?tutorialRefuge.PierX:4.72f;
        private float TutorialWindowLightX=>tutorialRefuge!=null?tutorialRefuge.OuterX-1.05f:8.35f;
        public bool IsTutorialRefuge=>worldMode020==WorldMode020.TutorialRefuge&&tutorialRefuge!=null;
        public TutorialRoomV2Layout RoomV2Layout=>tutorialRefuge;
        public TutorialRoomV2Catalog.PlacementReport RoomV2PropReport {get;private set;}

        public void BuildTutorialRefuge(TutorialRoomV2Catalog catalog,float width=16f,float vaultHeight=10f)
        {
            if(built)throw new InvalidOperationException("Select008 refuge before the first World.Build; never replace a live World.");
            var layout=new TutorialRoomV2Layout(width,vaultHeight);
            RefugePropPlacement.ValidateCatalog(catalog); // Fail before mutating/building a World.
            tutorialRefuge=layout;refugeCatalog=catalog;MapRooms=RefugeRooms(layout);MapBounds=RefugeBounds(layout);Build();
        }
        // Retain the earlier frozen room opt-in signature for explicit future callers.
        public void BuildTutorialRoomV2(TutorialRoomV2Catalog catalog=null,float width=16f,float vaultHeight=10f)
        {BuildTutorialRefuge(catalog,width,vaultHeight);}
        public bool TryGetRefugeNpcPose(int role,out Vector3 feet,out float yaw)
        {
            feet=default;yaw=0;if(!IsTutorialRefuge||!RefugePlacementPolicy.TryGetNpcPose(role,out var p,out yaw))return false;
            feet=new Vector3(p.X,p.Y,p.Z);return true;
        }
        private void BuildTutorialRefugeEnvironment()
        {
            BeginRefugeDressing();
            BeginInteriorFlora020();
            TutorialRoomV2Geometry.Build(tutorialRefuge,new RefugeWorldSink(this),true,true);
            BuildEntrance();BuildTutorialApproach();BuildSanctum();DressHall(tutorialRefuge.HalfWidth-.43f);
            BuildRefugeActivities();
            BuildLighting();BuildWindowBackdrop();
            RoomV2PropReport=RefugePropPlacement.Place(geometryRoot,refugeCatalog,tutorialRefuge,out LanternSwings);
            FinishInteriorFlora020();
            FinishRefugeDressing();
            BuildRefugeWindowWind015();
        }
        private static Rect[] RefugeRooms(TutorialRoomV2Layout layout)
        {
            var result=new Rect[layout.Rooms.Length];for(int i=0;i<result.Length;i++){var r=layout.Rooms[i];result[i]=new Rect(r.MinU,r.MinV,r.MaxU-r.MinU,r.MaxV-r.MinV);}return result;
        }
        private static Rect RefugeBounds(TutorialRoomV2Layout layout)
        {var r=layout.Bounds;return new Rect(r.MinU,r.MinV,r.MaxU-r.MinU,r.MaxV-r.MinV);}
        private sealed class RefugeWorldSink:ITutorialRoomV2Sink
        {
            readonly DungeonWorld world;public RefugeWorldSink(DungeonWorld owner){world=owner;}
            static Vector3 V(TutorialRoomV2Layout.P3 p)=>new Vector3(p.X,p.Y,p.Z);
            static Surface Paint(RoomV2Paint p)
            {
                switch(p){case RoomV2Paint.Stone:return Surface.Stone;case RoomV2Paint.PaleStone:return Surface.PaleStone;case RoomV2Paint.DeepStone:return Surface.DarkStone;
                    case RoomV2Paint.Moss:return Surface.Moss;case RoomV2Paint.Floor:return Surface.Floor;case RoomV2Paint.Wood:return Surface.Wood;case RoomV2Paint.Iron:return Surface.Iron;
                    case RoomV2Paint.Brass:return Surface.Brass;case RoomV2Paint.Green:return Surface.Green;case RoomV2Paint.Azure:return Surface.Azure;case RoomV2Paint.Wax:return Surface.Wax;
                    case RoomV2Paint.FlameCore:return Surface.FlameCore;default:return Surface.Cloth;}
            }
            public void Box(RoomV2Paint p,TutorialRoomV2Layout.P3 center,TutorialRoomV2Layout.P3 size,float yaw=0,float pitch=0,float roll=0)
            {
                if(IsLegacyBlockVine020(p,size))return;
                if(p==RoomV2Paint.FlameCore && world.TryRegisterRefugeChandelierFlame(V(center),V(size),yaw,pitch,roll))return;
                if(p==RoomV2Paint.Moss)world.interiorMossStone020.Box(V(center),V(size),Quaternion.Euler(pitch,yaw,roll));
                else world.Cube(Paint(p),V(center),V(size),Quaternion.Euler(pitch,yaw,roll));
            }
            public void Collider(string name,TutorialRoomV2Layout.P3 center,TutorialRoomV2Layout.P3 size){world.BoxCollider(name,V(center),V(size));}
            static Vector2 RefugeFaceUv(Vector3 position,Vector3 normal,RoomV2Paint paint)
            {
                if(paint!=RoomV2Paint.Beam&&Mathf.Abs(normal.y)<.5f)
                    return Mathf.Abs(normal.x)>=Mathf.Abs(normal.z)?new Vector2(position.z,position.y)/.8f:new Vector2(position.x,position.y)/.8f;
                return new Vector2(position.x,position.z); // Existing floor/beam projection remains exact.
            }
            public void Triangle(RoomV2Paint p,TutorialRoomV2Layout.P3 pa,TutorialRoomV2Layout.P3 pb,TutorialRoomV2Layout.P3 pc)
            {
                Vector3 a=V(pa),b=V(pb),c=V(pc);Batch batch;
                if(p==RoomV2Paint.Beam){
                    if(world.windowBeams==null){
                        Shader shader=Resources.Load<Shader>("Shaders/OracleWindowShaft020");
                        if(shader==null||!shader.isSupported)shader=Resources.Load<Shader>("Shaders/LaneGateGrid")??Shader.Find("Universal Render Pipeline/Unlit");
                        Material material=new Material(shader){name="008 refuge restrained blue window shafts"};Color tint=new Color(.25f,.55f,.9f,.045f);
                        material.SetColor("_BaseColor",tint);
                        if(material.HasProperty("_WindowCenters020")){
                            var layout=world.tutorialRefuge;
                            material.SetVector("_WindowCenters020",new Vector4(layout.AlcoveCenters[0],layout.AlcoveCenters[1],layout.AlcoveCenters[2],0f));
                            material.SetVector("_WindowAperture020",new Vector4(TutorialRoomV2Layout.WindowHalfWidth-TutorialRoomV2Layout.RevealInset,TutorialRoomV2Layout.WindowBottom+TutorialRoomV2Layout.RevealInset,TutorialRoomV2Layout.WindowTop-TutorialRoomV2Layout.RevealInset,0f));
                        }
                        else if(material.HasProperty("_GridColor"))material.SetColor("_GridColor",tint);
                        else {material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3025;}
                        world.resources.Add(material);world.windowBeams=new Batch(material);
                    }batch=world.windowBeams;
                }else batch=p==RoomV2Paint.Moss?world.interiorMossStone020:world.batches[Paint(p)];
                Vector3 normal=Vector3.Cross(b-a,c-a);float squared=normal.sqrMagnitude;if(squared<1e-16f)return;normal/=Mathf.Sqrt(squared);int start=batch.Vertices.Count;
                batch.Vertices.Add(a);batch.Vertices.Add(b);batch.Vertices.Add(c);batch.Normals.Add(normal);batch.Normals.Add(normal);batch.Normals.Add(normal);
                batch.UVs.Add(RefugeFaceUv(a,normal,p));batch.UVs.Add(RefugeFaceUv(b,normal,p));batch.UVs.Add(RefugeFaceUv(c,normal,p));
                batch.Triangles.Add(start);batch.Triangles.Add(start+1);batch.Triangles.Add(start+2);
                if(p==RoomV2Paint.Beam){batch.Triangles.Add(start);batch.Triangles.Add(start+2);batch.Triangles.Add(start+1);}
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Y-up refuge/Canvas and true floor contact.
// Array Formats: Fixed activity anchors, existing bounded combat/room owners and event-only UI.
// Dependencies/Pointers: Root-integrated tutorial activities retain the connected party and use the existing Skip/Resume authority.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing anchored presentation receives the same bounded passive world-XZ field; geometry/physics/accepted room fuel stay canonical.
// Array Formats: existing fixed mesh/vertex/material owner and one shared float4; no hot collections, per-window objects or mutable gameplay alias.
// Dependencies/Pointers: work-only shared-breeze adaptation; original shaders and fallback lifetimes remain. Root alone imports/tests actual native/WebGL visuals, culling, allocation and lifecycle.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing supported refuge wall/floor planes and sinks; structural geometry, windows, doorway and actor poses unchanged.
// Array Formats: One cold material/batch hook and exact two legacy decorative-box signatures; existing combined resource lifetime.
// Dependencies/Pointers: Geometry helper retains TutorialRoomV2Geometry/BuildVault ownership. New presentation partial routes moss/vines without changing colliders, light/reaction authority, arena palettes or frame owners. Root actual verification pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing current-WEB graphics settings and RoomV2-local light presentation only; no structural/camera/gameplay position changes.
// Array Formats: One Off/Soft preference and existing scalar UI snapshot/private URP/light cache; one primary directional light/one cascade/25m1024cap, existing three-window mesh batch.
// Dependencies/Pointers: Current root dirty/dialogue/guide/flora hooks preserved; existing owners manage cold settings/resource updates. No new per-frame CPU/light/shadow-map/render-feature owner; root verifies native/WebGL shader/visual/frame behavior.
