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
        public bool IsTutorialRefuge=>tutorialRefuge!=null&&!IsCoopArena;
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
            TutorialRoomV2Geometry.Build(tutorialRefuge,new RefugeWorldSink(this),true,true);
            BuildEntrance();BuildTutorialApproach();BuildSanctum();DressHall(tutorialRefuge.HalfWidth-.43f);
            BuildRefugeActivities();
            BuildLighting();BuildWindowBackdrop();
            RoomV2PropReport=RefugePropPlacement.Place(geometryRoot,refugeCatalog,tutorialRefuge);
            FinishRefugeDressing();
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
                if(p==RoomV2Paint.FlameCore && world.TryRegisterRefugeChandelierFlame(V(center),V(size),yaw,pitch,roll))return;
                world.Cube(Paint(p),V(center),V(size),Quaternion.Euler(pitch,yaw,roll));
            }
            public void Collider(string name,TutorialRoomV2Layout.P3 center,TutorialRoomV2Layout.P3 size){world.BoxCollider(name,V(center),V(size));}
            public void Triangle(RoomV2Paint p,TutorialRoomV2Layout.P3 pa,TutorialRoomV2Layout.P3 pb,TutorialRoomV2Layout.P3 pc)
            {
                Vector3 a=V(pa),b=V(pb),c=V(pc);Batch batch;
                if(p==RoomV2Paint.Beam){
                    if(world.windowBeams==null){
                        Shader shader=Resources.Load<Shader>("Shaders/LaneGateGrid")??Shader.Find("Universal Render Pipeline/Unlit");
                        Material material=new Material(shader){name="008 refuge restrained blue window shafts"};Color tint=new Color(.25f,.55f,.9f,.045f);
                        material.SetColor("_BaseColor",tint);
                        if(material.HasProperty("_GridColor"))material.SetColor("_GridColor",tint);
                        else {material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3025;}
                        world.resources.Add(material);world.windowBeams=new Batch(material);
                    }batch=world.windowBeams;
                }else batch=world.batches[Paint(p)];
                Vector3 normal=Vector3.Cross(b-a,c-a);float squared=normal.sqrMagnitude;if(squared<1e-16f)return;normal/=Mathf.Sqrt(squared);int start=batch.Vertices.Count;
                batch.Vertices.Add(a);batch.Vertices.Add(b);batch.Vertices.Add(c);batch.Normals.Add(normal);batch.Normals.Add(normal);batch.Normals.Add(normal);
                batch.UVs.Add(new Vector2(a.x,a.z));batch.UVs.Add(new Vector2(b.x,b.z));batch.UVs.Add(new Vector2(c.x,c.z));
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
