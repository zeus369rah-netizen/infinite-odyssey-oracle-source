using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OdysseyOracle
{
    /// <summary>A deterministic, original voxel catacomb built into a small set of material meshes.</summary>
    public sealed partial class DungeonWorld : MonoBehaviour
    {
        public Vector3 PlayerSpawn { get; private set; } = new Vector3(0f, 0.2f, -10f);
        public Vector3 ShrinePosition { get; private set; } = new Vector3(0f, 0f, 2f);
        public Vector3[] EnemySpawns { get; private set; } = MakeEnemySpawns();
        public Vector3 PortalPosition { get; private set; } = new Vector3(0f, 0.68f, 25.2f);
        public Vector3[] InvasionPortals { get; private set; } = new Vector3[0];
        public bool IsCoopArena { get; private set; }
        public Vector3[] TowerBuildPads { get; private set; } = new Vector3[0];
        public TowerPlatformGeometry TowerPlatforms { get; private set; }
        public Vector3 MatchForgePosition => TowerPlatformGeometry.MatchForgePosition;
        public float MatchForgeYaw => TowerPlatformGeometry.MatchForgeYaw;
        public Vector2[] MapBoundary { get; private set; } = new Vector2[0];
        public int ArenaChunkMeshCount { get; private set; }
        public int ArenaSpatialChunkCount { get; private set; }
        public int ArenaVertexCount { get; private set; }
        public float ArenaMaximumChunkSpan { get; private set; }
        public string[] LaneThemes { get; } = new[] { "Fire", "Fire", "Earth", "Earth", "Water", "Water", "Air", "Air" };
        public Vector3[] LanePlatformPositions { get; private set; } = new Vector3[0];
        // Rect.x is world X, Rect.y is world Z. These are the actual walkable room footprints.
        public Rect[] MapRooms { get; private set; } = new[]
        {
            new Rect(-5.6f, -14f, 11.2f, 42f),
            new Rect(-9.8f, -5.2f, 4.5f, 4.4f), new Rect(5.3f, -5.2f, 4.5f, 4.4f),
            new Rect(-9.8f, 5.8f, 4.5f, 4.4f), new Rect(5.3f, 5.8f, 4.5f, 4.4f),
            new Rect(-9.8f, 15.8f, 4.5f, 4.4f), new Rect(5.3f, 15.8f, 4.5f, 4.4f)
        };
        public Rect MapBounds { get; private set; } = new Rect(-9.8f, -14f, 19.6f, 42f);

        private enum Surface { Stone, PaleStone, DarkStone, Moss, Floor, Wood, WoodEnd, Iron, Brass, Bone, Cloth, Wax, Flame, FlameCore, Azure, Portal, Soil, Green, RuneGreen, PortalCrystal, PortalCloth, Sand, Snow, Ember, Water, Blossom, BlossomPale, Pine, Autumn, Sky, Cloud, Moon }
        private readonly Dictionary<Surface, Batch> batches = new Dictionary<Surface, Batch>();
        // Texture tints stay available for original vertex-colored actors; _BaseColor stays white.
        private readonly Dictionary<Surface, Color> authoredSurfaceTints = new Dictionary<Surface, Color>(48);
        private const float ArenaRadius = 76f;
        private const float ArenaPortalRadius = 64f;
        private const float SpatialChunkSize = 10f;
        private readonly Dictionary<ChunkKey, Batch> spatialBatches = new Dictionary<ChunkKey, Batch>();
        private bool buildingSpatialChunks;
        private Dictionary<Surface,Batch> portalAssemblyBatches;
        private readonly GameObject[] invasionGateRoots = new GameObject[8];
        private readonly Light[] invasionGateLights = new Light[8];
        private readonly LaneGateVolume[] laneBarriers = new LaneGateVolume[8];
        private Batch windowBeams;
        private readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();
        private readonly float[] alcoves = { -3f, 8f, 18f };
        // One authored set owns both structural piers and their mounted tutorial torches.
        private static readonly float[] TutorialPierRows = { -11.8f, -6.2f, 3f, 13f, 22f, 27.3f };
        private readonly System.Random random = new System.Random(671904);
        private Material portalMaterial;
        private GameObject portalSeal;
        private Transform portalSpiral;
        private Material portalCrystalMaterial;
        private Material nightSkybox,tutorialSkybox,previousSkybox;
        private bool skyCaptured;
        private Transform geometryRoot, tutorialRoot, arenaRoot;
        private TutorialFireLightPool tutorialFireLights;
        private Rect[] tutorialRooms;
        private readonly List<Transform> invasionSpirals = new List<Transform>();
        private bool built;
        private bool portalUnlocked;

        public void Build()
        {
            ConfigureContinuousNightSky(true);
            if (built)
            {
                if (IsCoopArena)
                {
                    if (arenaRoot != null) arenaRoot.gameObject.SetActive(false);
                    tutorialRoot.gameObject.SetActive(true);
                    geometryRoot = tutorialRoot;
                    IsCoopArena = false;
                    ShrinePosition = new Vector3(0, 0, 2);
                    PlayerSpawn = new Vector3(0, .2f, -10);
                    PortalPosition = new Vector3(0, .68f, 25.2f);
                    EnemySpawns = MakeEnemySpawns();
                    InvasionPortals = new Vector3[0];
                    TowerBuildPads = new Vector3[0];
                    LanePlatformPositions = new Vector3[0];
                    arenaLanes.Reset(0);
                    MapRooms = tutorialRooms;
                    MapBounds = tutorialRefuge!=null?RefugeBounds(tutorialRefuge):new Rect(-9.8f, -14f, 19.6f, 42f);
                    MapBoundary = new Vector2[0];
                    LockPortal();
                }
                return;
            }
            built = true;
            geometryRoot = new GameObject("Tutorial catacomb environment").transform;
            geometryRoot.SetParent(transform, false);
            tutorialRoot = geometryRoot;
            tutorialFireLights = tutorialRoot.gameObject.AddComponent<TutorialFireLightPool>();
            tutorialFireLights.Initialize(tutorialRoot, GameSession.Instance);
            tutorialRooms = MapRooms;
            MakeMaterials();
            if(tutorialRefuge!=null)BuildTutorialRefugeEnvironment();
            else
            {
                BuildFloors();
                BuildHallWalls();
                BuildRoofAndArches();
                BuildAlcoves();
                BuildTutorialMasonryBacking();
                BuildTutorialStoneJointBeds();
                BuildEntrance();
                BuildTutorialApproach();
                BuildSanctum();
                DressHall();
                BuildLighting();
                BuildWindowBackdrop();
            }
            foreach (KeyValuePair<Surface, Batch> pair in batches)
                if (pair.Value.Vertices.Count > 0) FinishTutorialBatch(pair.Key, pair.Value);
            if(windowBeams!=null)FinishBatch("Cool moonlit window shafts",windowBeams,geometryRoot,false);
        }

        public void UnlockPortal()
        {
            portalUnlocked = true;
            if (portalSeal != null) portalSeal.SetActive(false);
            if (portalMaterial != null)
            {
                portalMaterial.SetColor("_BaseColor", Color.white);
                portalMaterial.SetColor("_EmissionColor", new Color(0.62f, 0.025f, 1.4f));
            }
            if (portalCrystalMaterial != null) portalCrystalMaterial.SetColor("_EmissionColor", new Color(1.5f, 0.12f, 2.8f));
        }

        public void LockPortal()
        {
            portalUnlocked = false;
            if (portalSeal != null) portalSeal.SetActive(true);
            if (portalMaterial != null)
            {
                portalMaterial.SetColor("_BaseColor", Color.white);
                portalMaterial.SetColor("_EmissionColor", new Color(0.20f, 0.015f, 0.43f));
            }
            if (portalCrystalMaterial != null) portalCrystalMaterial.SetColor("_EmissionColor", new Color(0.7f, 0.045f, 1.15f));
        }

        private static Vector3[] MakeEnemySpawns()
        {
            // One hundred authored clear-floor locations; early prefixes span the room rather than one crowd.
            Vector3[] spawns = new Vector3[100];
            for (int i = 0; i < spawns.Length; i++)
            {
                int column = i % 5;
                int row = ((i / 5) * 7 + column * 3) % 20;
                spawns[i] = new Vector3(-2.8f + column * 1.4f, 0.15f, 5.2f + row * 0.8f);
            }
            return spawns;
        }

        public void BuildCoopArena()
        {
            if (!built) Build();
            if (IsCoopArena && arenaRoot != null) return;
            ConfigureContinuousNightSky(false);
            tutorialRoot.gameObject.SetActive(false);
            IsCoopArena = true;
            ShrinePosition = Vector3.zero;
            PlayerSpawn = new Vector3(0, .2f, -6.7f);
            PortalPosition = new Vector3(0, 0, ArenaRingRadius);
            arenaLanes.Reset(0);
            InvasionPortals = new Vector3[0];
            LanePlatformPositions = new Vector3[8];
            for(int lane=0;lane<8;lane++)LanePlatformPositions[lane]=LaneDirection(lane)*ArenaPortalRadius;
            // Four SHARED bastions between the eight roads; stable global IDs0..3.
            TowerBuildPads = arenaRoot != null && TowerPlatforms != null
                ? TowerPlatforms.TopCenters : ElementalTowerPlatforms.CreateTopCenters();
            MapBounds = new Rect(-ArenaRadius-.5f, -ArenaRadius-.5f, (ArenaRadius+.5f)*2f, (ArenaRadius+.5f)*2f);
            MapRooms = new[] { MapBounds };
            MapBoundary = new Vector2[64];
            for (int i = 0; i < MapBoundary.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / MapBoundary.Length;
                MapBoundary[i] = new Vector2(Mathf.Cos(angle) * ArenaRadius, Mathf.Sin(angle) * ArenaRadius);
            }
            EnemySpawns = new Vector3[100];
            for (int i = 0; i < EnemySpawns.Length; i++)
            {
                int lane=i%8,row=i/8;Vector3 direction=LaneDirection(lane);
                Vector3 tangent=new Vector3(-direction.z,0,direction.x);
                EnemySpawns[i]=direction*(ArenaPortalRadius-3f-row*1.8f)+
                    tangent*(row%2==0?-.95f:.95f)+Vector3.up*.15f;
            }
            if (arenaRoot != null)
            {
                arenaRoot.gameObject.SetActive(true);
                geometryRoot = arenaRoot;
                portalCrystalMaterial.SetColor("_EmissionColor", new Color(1.5f,.12f,2.8f));
                portalMaterial.SetColor("_EmissionColor", new Color(.62f,.025f,1.4f));
                ResetArenaLaneGeometry();
                return;
            }
            geometryRoot = new GameObject("Cooperative elemental wheel environment").transform;
            geometryRoot.SetParent(transform, false);
            arenaRoot = geometryRoot;
            buildingSpatialChunks = true;
            try
            {
            vignetteKeepouts.Clear();vignetteKeepoutOverflow=false;captureVignetteKeepouts=true;
            BuildArenaGround();
            BuildArenaPaths();
            TowerPlatforms = new TowerPlatformGeometry(geometryRoot,TowerBuildPads);
            BuildArenaTowerPads();
            BuildArenaBiomes();
            captureVignetteKeepouts=false;
            BuildCurrentLaneVignettes();
            for(int lane=0;lane<8;lane++)
                BuildArenaInvasionGate(lane,LanePlatformPositions[lane],90f-lane*45f);
            invasionGateLights[4]=PointLight("West invasion amethyst glow", new Vector3(-ArenaPortalRadius+1.2f,2.3f,0),new Color(.65f,.14f,1f),13f,12f,false);
            invasionGateLights[0]=PointLight("East invasion amethyst glow", new Vector3(ArenaPortalRadius-1.2f,2.3f,0),new Color(.65f,.14f,1f),13f,12f,false);
            foreach (float x in new[] { -36f, 36f })
            foreach (float z in new[] { -36f, 36f })
                PointLight("Ruined court brazier glow",new Vector3(x,2.5f,z),new Color(1f,.61f,.27f),18f,10f,false);
            PointLight("Open sky soft fill",new Vector3(0,24f,0),new Color(.67f,.85f,.76f),112f,60f,false);
            FinishSpatialChunks();
            if(laneVignetteRenderer!=null){ArenaChunkMeshCount+=laneVignetteRenderer.MeshCount;ArenaVertexCount+=laneVignetteRenderer.VertexCount;
                ArenaMaximumChunkSpan=Mathf.Max(ArenaMaximumChunkSpan,laneVignetteRenderer.MaximumChunkSpan);}
            BuildInactiveLaneBarriers();
            }
            finally { captureVignetteKeepouts=false;vignetteKeepouts.Clear();buildingSpatialChunks=false;spatialBatches.Clear(); }
            portalCrystalMaterial.SetColor("_EmissionColor", new Color(1.5f,.12f,2.8f));
            portalMaterial.SetColor("_EmissionColor", new Color(.62f,.025f,1.4f));
            ResetArenaLaneGeometry();
        }

        // Canonical mask/admission/drain API is in DungeonWorld.Lanes.cs.

        // Canonical mask/admission/drain API is in DungeonWorld.Lanes.cs.

        // Canonical mask/admission/drain API is in DungeonWorld.Lanes.cs.

        private static int LaneThemeAt(Vector3 position)
        {
            return (Mathf.RoundToInt(Mathf.Atan2(position.z,position.x)*Mathf.Rad2Deg/45f)+8)%8;
        }

        private void BuildInactiveLaneBarriers()
        {
            Shader shader=Resources.Load<Shader>("Shaders/LaneGateGrid")??Shader.Find("OdysseyOracle/LaneGateGrid");
            bool fallback=shader==null;
            if(fallback)shader=Shader.Find("Universal Render Pipeline/Unlit");
            Material grid=new Material(shader){name="Inactive elemental corridor red grid"};
            grid.SetColor("_BaseColor",new Color(.8f,.015f,.008f,.15f));
            if(!fallback)
            {
                grid.SetColor("_GridColor",new Color(1.4f,.045f,.025f,.72f));
                grid.SetFloat("_GridSpacing",.7f);grid.SetFloat("_LineWidth",.025f);
            }
            else
            {
                grid.SetFloat("_Surface",1);grid.SetFloat("_ZWrite",0);
                grid.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);grid.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                grid.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");grid.renderQueue=3025;
            }
            resources.Add(grid);
            for(int lane=0;lane<8;lane++)
            {
                Vector3 direction=LaneDirection(lane);
                GameObject go=new GameObject("Blocked "+LaneThemes[lane]+" corridor grid");
                go.transform.SetParent(geometryRoot,false);
                LaneGateVolume barrier=go.AddComponent<LaneGateVolume>();
                float start=ElementalTowerPlatforms.BarrierStart,end=ElementalTowerPlatforms.BarrierEnd;
                barrier.Initialize(lane,direction*((start+end)*.5f)+Vector3.up*(ElementalTowerPlatforms.BarrierHeight*.5f),
                    Quaternion.LookRotation(direction,Vector3.up),new Vector3(ElementalTowerPlatforms.BarrierWidth,
                    ElementalTowerPlatforms.BarrierHeight,end-start),grid);
                laneBarriers[lane]=barrier;
            }
        }

        // Arena environment implementation is in DungeonWorld.ArenaEnvironment.cs.

        // Arena environment implementation is in DungeonWorld.ArenaEnvironment.cs.

        // Arena environment implementation is in DungeonWorld.ArenaEnvironment.cs.

        private void ArenaRing(Surface surface,float radius,float y,float depth,float height,int segments)
        {ArenaRingAt(surface,Vector3.zero,radius,y,depth,height,segments);}
        private void ArenaRingAt(Surface surface,Vector3 center,float radius,float y,float depth,float height,int segments)
        {
            for(int i=0;i<segments;i++)
            {
                Quaternion q=Quaternion.Euler(0,i*360f/segments,0);
                Cube(surface,center+q*new Vector3(0,y,radius),new Vector3(2*Mathf.PI*radius/segments*1.035f,height,depth),q);
            }
        }

        private void ElementFloorDevice(Vector3 center,int element)
        {
            Surface glow=element==0?Surface.Flame:element==1||element==3||element==5?Surface.Azure:
                element==2?Surface.PaleStone:element==4?Surface.Snow:element==6?Surface.RuneGreen:Surface.FlameCore;
            Quaternion q=Quaternion.Euler(0,element*45f,0);
            for(int i=0;i<4;i++)
            {
                float x=(i-1.5f)*.25f;
                Cube(glow,center+q*new Vector3(x,0,.2f-Mathf.Abs(x)*.45f),new Vector3(.08f,.025f,.45f),q);
            }
            Cube(glow,center+q*new Vector3(0,0,-.28f),new Vector3(.8f,.025f,.07f),q);
        }

        // Arena environment implementation is in DungeonWorld.ArenaEnvironment.cs.

        private void DecorateArenaTheme(Vector3 p,int theme)
        {
            switch(theme)
            {
                case 0:
                    Campfire(p);ArenaTree(p+new Vector3(.8f,0,1.1f),Surface.Autumn,false);break;
                case 1:
                    Obelisk(p);PlaceCrystal(Surface.Azure,p+Vector3.up*2.65f,.13f,.85f,Quaternion.Euler(0,30,0));break;
                case 2:
                    ArenaSmallRuin(p,0);break;
                case 3:
                    ArenaDock(p);ArenaTree(p+new Vector3(.7f,0,1.0f),Surface.Green,false);break;
                case 4:
                    ArenaTree(p,Surface.Pine,true);break;
                case 5:
                    Cube(Surface.DarkStone,p+Vector3.up*.13f,new Vector3(1.1f,.26f,1.1f));
                    PlaceCrystal(Surface.Azure,p+Vector3.up*1.45f,.38f,2.8f,Quaternion.Euler(0,15,0));
                    PlaceCrystal(Surface.Azure,p+new Vector3(.45f,.8f,.2f),.20f,1.5f,Quaternion.Euler(0,30,-15));break;
                case 6:
                    ArenaTree(p,Surface.Blossom,false);Fern(p+new Vector3(.9f,.02f,-.65f),.9f);break;
                case 7:
                    Campfire(p);Rubble(p+new Vector3(.9f,0,.8f),12);
                    Cube(Surface.Ember,p+new Vector3(-.65f,.3f,-.3f),new Vector3(.7f,.6f,.7f));
                    for(int crack=0;crack<4;crack++)Cube(Surface.Flame,p+new Vector3((crack-1.5f)*.3f,.025f,crack%2*.15f),new Vector3(.36f,.025f,.06f));break;
            }
        }

        // Arena environment implementation is in DungeonWorld.ArenaEnvironment.cs.

        private void BuildArenaTowerPads()
        {
            // # ARCHITECTURE SNAPSHOT
            // Three matching visible/solid12cm courses, canonical topY.36.
            // Existing spatial/material batches retain all rendered geometry.
            // World owns12 child colliders; no per-pad Update/material/light.
            for(int pad=0;pad<TowerBuildPads.Length;pad++)
            {
                Vector3 p=TowerBuildPads[pad];
                for(int course=0;course<TowerPadPolicy.CourseCount;course++)
                {
                    TowerPlatformGeometry.GetCourse(p,course,out Vector3 center,out Vector3 size);
                    Cube(course==0?Surface.DarkStone:course==1?Surface.Stone:Surface.PaleStone,center,size);
                    TowerPlatforms.CreateCourseCollider(pad,course);
                }
                foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})
                {
                    Cube(Surface.Brass,p+new Vector3(x*.78f,.012f,z*.64f),new Vector3(.13f,.014f,.4f));
                    Cube(Surface.Brass,p+new Vector3(x*.64f,.012f,z*.78f),new Vector3(.4f,.014f,.13f));
                    Cube(Surface.Azure,p+new Vector3(x*.62f,.026f,z*.62f),new Vector3(.10f,.012f,.10f));
                }
                Cube(Surface.Iron,p+Vector3.up*.006f,new Vector3(.34f,.012f,.34f),Quaternion.Euler(0,45,0));
            }
        }

        private void ArenaTree(Vector3 p,Surface leaves,bool snow)
        {
            float height=snow?4.5f:3.4f+Range(-.3f,.5f);
            Cube(Surface.Wood,p+Vector3.up*height*.48f,new Vector3(.44f,height*.96f,.44f));
            BoxCollider("Solid tree trunk",p+Vector3.up*height*.45f,new Vector3(.55f,height*.9f,.55f));
            if(snow)
            {
                for(int i=0;i<4;i++)
                {
                    float width=2.0f-i*.4f;
                    Cube(leaves,p+Vector3.up*(1.9f+i*.65f),new Vector3(width,.8f,width));
                    Cube(Surface.Snow,p+Vector3.up*(2.33f+i*.65f),new Vector3(width+.07f,.12f,width+.07f));
                }
            }
            else
            {
                foreach(float side in new[]{-1f,1f})
                    Cube(Surface.Wood,p+new Vector3(side*.52f,height*.73f,0),new Vector3(1.1f,.28f,.28f),Quaternion.Euler(0,side*20f,side*30f));
                for(int i=0;i<15;i++)
                {
                    Vector3 c=p+new Vector3(Range(-1.35f,1.35f),height+Range(-.35f,.65f),Range(-1.1f,1.1f));
                    float size=Range(.65f,1.12f);
                    Cube(i%4==0&&leaves==Surface.Blossom?Surface.BlossomPale:leaves,c,new Vector3(size,size*.7f,size));
                }
            }
        }

        private void ArenaSmallRuin(Vector3 p,int variation)
        {
            Surface material=variation%3==2?Surface.Sand:Surface.Stone;
            foreach(float side in new[]{-1f,1f})
            {
                float height=side<0?2.5f:1.7f;
                for(int y=0;y<(side<0?5:3);y++)Cube(material,p+new Vector3(side*.72f,.28f+y*.51f,.2f),new Vector3(.58f,.48f,.62f));
                Cube(Surface.PaleStone,p+new Vector3(side*.72f,height+.12f,.2f),new Vector3(.85f,.19f,.85f));
                BoxCollider("Ruined pillar",p+new Vector3(side*.72f,height*.5f,.2f),new Vector3(.62f,height,.66f));
            }
            Cube(material,p+new Vector3(.2f,.24f,1.0f),new Vector3(1.2f,.44f,.65f),Quaternion.Euler(0,25,0));
        }

        private void ArenaChest(Vector3 p,float yaw)
        {
            Quaternion q=Quaternion.Euler(0,yaw,0);
            Cube(Surface.Wood,p+Vector3.up*.35f,new Vector3(1.18f,.7f,.76f),q);
            Cube(Surface.WoodEnd,p+Vector3.up*.79f,new Vector3(1.18f,.2f,.76f),q);
            Cube(Surface.WoodEnd,p+Vector3.up*.94f,new Vector3(1.1f,.12f,.56f),q);
            foreach(float side in new[]{-1f,1f})
                Cube(Surface.Brass,p+q*new Vector3(side*.40f,.5f,0),new Vector3(.105f,1.03f,.81f),q);
            Cube(Surface.Iron,p+q*new Vector3(0,.48f,-.4f),new Vector3(1.18f,.08f,.06f),q);
            Cube(Surface.Brass,p+q*new Vector3(0,.57f,-.44f),new Vector3(.2f,.28f,.08f),q);
            BoxCollider("Treasure chest",p+Vector3.up*.48f,new Vector3(1.23f,.96f,.81f),q);
        }

        private void ArenaDock(Vector3 p)
        {
            for(int i=0;i<5;i++)Cube(Surface.WoodEnd,p+new Vector3(0,.05f,(i-2)*.3f),new Vector3(1.8f,.10f,.27f));
            foreach(float x in new[]{-.76f,.76f})foreach(float z in new[]{-.75f,.75f})
                Cube(Surface.Wood,p+new Vector3(x,.29f,z),new Vector3(.14f,.65f,.14f));
        }

        private void BuildArenaInvasionGate(int lane,Vector3 position,float yaw)
        {
            Transform previousRoot=geometryRoot;
            Transform gateRoot=new GameObject(LaneThemes[lane]+" invasion portal assembly").transform;
            gateRoot.SetParent(previousRoot,false);
            invasionGateRoots[lane]=gateRoot.gameObject;
            geometryRoot=gateRoot;
            portalAssemblyBatches=new Dictionary<Surface,Batch>();
            try
            {
            Quaternion q=Quaternion.Euler(0,yaw,0);
            foreach(float side in new[]{-1f,1f})
            {
                for(int y=0;y<6;y++)Cube(y%2==0?Surface.Stone:Surface.PaleStone,position+q*new Vector3(side*1.82f,.35f+y*.55f,0),new Vector3(.64f,.52f,.84f),q);
                for(int n=0;n<3;n++)Cube(Surface.PaleStone,position+q*new Vector3(side*(1.52f-n*.43f),3.65f+n*.22f,0),new Vector3(.56f,.61f,.84f),q);
                PlaceCrystal(Surface.PortalCrystal,position+q*new Vector3(side*1.82f,3.6f,0),.15f,.75f,q);
                BoxCollider("Invasion portal frame",position+q*new Vector3(side*1.82f,1.7f,0),new Vector3(.64f,3.4f,.84f),q);
            }
            Transform aperture=new GameObject("Active invasion current").transform;
            aperture.SetParent(geometryRoot,false);aperture.localPosition=position+q*new Vector3(0,1.87f,.08f);aperture.localRotation=q;aperture.localScale=new Vector3(1,1.5f,1);
            bool anomalyReady=AttachPortalAnomaly(aperture,1f);
            if(!anomalyReady)
            {
            Cube(Surface.Portal,position+q*new Vector3(0,1.85f,.22f),new Vector3(2.15f,3.22f,.075f),q);
            Batch swirl=new Batch(batches[Surface.PortalCrystal].Material);
            for(int arm=0;arm<3;arm++)for(int i=0;i<22;i++)
            {
                float a=arm*Mathf.PI*2f/3f+i*.31f,r=.12f+i*.038f;
                swirl.Box(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,-.015f),new Vector3(.06f,.12f,.035f),Quaternion.Euler(0,0,a*Mathf.Rad2Deg));
            }
            invasionSpirals.Add(FinishBatch("Violet invasion spiral",swirl,aperture,false).transform);
            }
            foreach(KeyValuePair<Surface,Batch> pair in portalAssemblyBatches)
            {
                bool glow=pair.Value.Material.HasProperty("_EmissionColor")&&pair.Value.Material.GetColor("_EmissionColor").maxColorComponent>0;
                FinishBatch(LaneThemes[lane]+" gate "+pair.Key,pair.Value,gateRoot,!glow);
            }
            }
            finally {portalAssemblyBatches=null;geometryRoot=previousRoot;}
        }

        private void MakeMaterials()
        {
            MakeMaterial(Surface.Stone, "Warm weathered limestone", new Color(0.53f, 0.52f, 0.45f), 0);
            MakeMaterial(Surface.PaleStone, "Sun worn limestone edges", new Color(0.66f, 0.64f, 0.55f), 0);
            MakeMaterial(Surface.DarkStone, "Green slate recesses", new Color(0.31f, 0.34f, 0.30f), 0);
            MakeMaterial(Surface.Moss, "Living moss masonry", new Color(0.34f, 0.43f, 0.25f), 3);
            MakeMaterial(Surface.Floor, "Worn warm flagstones", new Color(0.51f, 0.50f, 0.43f), 0);
            MakeMaterial(Surface.Wood, "Ancient oak grain", new Color(0.36f, 0.22f, 0.105f), 1);
            MakeMaterial(Surface.WoodEnd, "Oak ends", new Color(0.43f, 0.29f, 0.14f), 1);
            MakeMaterial(Surface.Iron, "Black wrought iron", new Color(0.12f, 0.135f, 0.14f), 0, 0.65f);
            MakeMaterial(Surface.Brass, "Worn oracle brass", new Color(0.59f, 0.40f, 0.135f), 0, 0.75f);
            MakeMaterial(Surface.Bone, "Old ivory", new Color(0.66f, 0.62f, 0.47f), 0);
            MakeMaterial(Surface.Cloth, "Oracle emerald cloth", new Color(0.085f, 0.27f, 0.14f), 2);
            MakeMaterial(Surface.Wax, "Wax candles", new Color(0.74f, 0.57f, 0.28f), 0);
            MakeMaterial(Surface.Flame, "Amber fire", new Color(1f, 0.28f, 0.028f), 0, 0f, new Color(3.8f, 0.48f, 0.035f));
            MakeMaterial(Surface.FlameCore, "Golden fire core", new Color(1f, 0.84f, 0.27f), 0, 0f, new Color(5f, 2.5f, 0.25f));
            MakeMaterial(Surface.Azure, "Blue moonlight glass", new Color(0.10f, 0.40f, 0.54f), 2, 0f, new Color(0.10f, 1.1f, 1.8f));
            MakeMaterial(Surface.Portal, "Violet threshold depths", new Color(0.085f, 0.025f, 0.16f), 2, 0f, new Color(0.20f, 0.015f, 0.43f));
            MakeMaterial(Surface.Soil, "Cracked dark earth", new Color(0.11f, 0.105f, 0.08f), 0);
            MakeMaterial(Surface.Green, "Fern fronds", new Color(0.18f, 0.31f, 0.12f), 3);
            MakeMaterial(Surface.RuneGreen, "Living green wayfinding", new Color(0.35f, 0.85f, 0.08f), 2, 0f, new Color(0.10f, 0.55f, 0.018f));
            MakeMaterial(Surface.PortalCrystal, "Amethyst threshold shards", new Color(0.58f, 0.12f, 0.9f), 2, 0.05f, new Color(0.7f, 0.045f, 1.15f));
            MakeMaterial(Surface.PortalCloth, "Violet sanctum standards", new Color(0.24f, 0.065f, 0.30f), 2);
            MakeMaterial(Surface.Sand, "Warm dune limestone", new Color(.70f,.61f,.38f), 0);
            MakeMaterial(Surface.Snow, "Powder snow caps", new Color(.83f,.91f,.88f), 0);
            MakeMaterial(Surface.Ember, "Volcanic ochre rock", new Color(.37f,.21f,.14f), 0);
            MakeMaterial(Surface.Water, "Turquoise shallow mosaic", new Color(.055f,.43f,.49f), 2, 0f, new Color(.01f,.07f,.09f));
            MakeMaterial(Surface.Blossom, "Pink blossom blocks", new Color(.80f,.31f,.49f), 2);
            MakeMaterial(Surface.BlossomPale, "Pale blossom blocks", new Color(.97f,.58f,.70f), 2);
            MakeMaterial(Surface.Pine, "Blue pine needles", new Color(.10f,.27f,.19f), 3);
            MakeMaterial(Surface.Autumn, "Amber autumn leaves", new Color(.73f,.29f,.08f), 3);
            MakeMaterial(Surface.Cloud,"Distant midnight voxel clouds",new Color(.23f,.29f,.38f),0,0f,new Color(.012f,.018f,.030f));
            portalMaterial = batches[Surface.Portal].Material;
            portalCrystalMaterial = batches[Surface.PortalCrystal].Material;
        }

        private void MakeMaterial(Surface surface, string label, Color tint, int pattern, float metallic = 0f, Color emission = default(Color))
        {
            authoredSurfaceTints[surface] = tint; // Cold World-owned authored palette, before texture baking.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = label };
            Texture2D texture = PixelTexture(surface,tint,pattern,label);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.mainTexture = texture;
            material.SetFloat("_Smoothness", surface == Surface.Iron || surface == Surface.Brass ? 0.31f : 0.055f);
            material.SetFloat("_Metallic", metallic);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            resources.Add(material);
            batches.Add(surface, new Batch(material));
        }

        private Texture2D PixelTexture(Surface surface,Color tint,int pattern,string label)
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size,size,TextureFormat.RGBA32,true) {name=label+" original pixels",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat,anisoLevel=1};
            Color[] pixels = new Color[size * size];
            int seed=19+(int)surface*137;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Preserve the authored random stream: texture polish cannot move generated walls/trees.
                bool backdrop=surface==Surface.Sky||surface==Surface.Cloud||surface==Surface.Moon;
                float grain=(backdrop?PixelHash(x,y,seed):(float)random.NextDouble())-.5f;
                float detail=(pattern==1||pattern==2)?(backdrop?PixelHash(x,y,seed+2):(float)random.NextDouble()):.5f;
                float macro=(PixelHash(x/4,y/4,seed)-.5f)*.075f;
                float luminosity=1.025f+macro+grain*.025f;
                bool stone=surface==Surface.Stone||surface==Surface.PaleStone||surface==Surface.DarkStone||surface==Surface.Floor||surface==Surface.Moss||surface==Surface.Sand;
                if(stone)
                {
                    luminosity+=(PixelHash(x/2,y/2,seed+7)-.5f)*.025f;
                    if(PixelHash(x,y,seed+31)>.988f)luminosity-=.09f;
                    if((x+y/5+seed)%29==0&&y>12&&y<19)luminosity-=.075f;
                    if((x==1||y==1)&&PixelHash(x/3,y/3,seed+5)>.58f)luminosity+=.075f;
                }
                if(surface==Surface.Wood)
                {
                    int board=(x+(int)(PixelHash(0,y/8,seed)*2f))%8;
                    if(board==0)luminosity*=.77f;else if(board==1)luminosity*=1.08f;
                    if((x*3+y/8)%13==0)luminosity-=.055f;
                    float knot=(x-13)*(x-13)+(y-21)*(y-21)/3f;
                    if(knot>7&&knot<15)luminosity-=.095f;
                }
                if(surface==Surface.WoodEnd)
                {
                    int ring=(int)(Mathf.Sqrt((x-16)*(x-16)+(y-16)*(y-16))*.7f)%5;
                    if(ring==0)luminosity*=.86f;
                }
                if(surface==Surface.Cloth||surface==Surface.PortalCloth)
                    luminosity=1f+((x+y)%4==0?-.055f:.015f)+(detail-.5f)*.015f;
                if(surface==Surface.Iron||surface==Surface.Brass)
                    luminosity=1.025f+(PixelHash(x/5,y/5,seed)-.5f)*.11f+grain*.018f;
                Color pixel=new Color(tint.r*luminosity,tint.g*luminosity,tint.b*luminosity,1);
                if(surface==Surface.Moss)
                {
                    float growth=Mathf.Clamp01((PixelHash(x/4,y/4,seed+11)-.42f)*1.25f);
                    pixel=Color.Lerp(pixel,new Color(.27f,.37f,.16f)*luminosity,growth*.62f);
                    pixel.a=1;
                }
                pixels[x+y*size]=pixel;
            }
            texture.SetPixels(pixels);
            texture.Apply(true,true);
            resources.Add(texture);
            return texture;
        }

        private static float PixelHash(int x,int y,int seed)
        {
            unchecked
            {
                uint h=(uint)(x*374761393+y*668265263+seed*982451653);
                h=(h^(h>>13))*1274126177u;
                return (h&65535u)/65535f;
            }
        }

        private void BuildFloors()
        {
            // The continuous colliders eliminate tiny snagging gaps between visible flagstones.
            BoxCollider("Hall floor", new Vector3(0f, -0.32f, 7f), new Vector3(11.5f, 0.64f, 42.3f));
            // Flush authored tile tops; the matching foundation closes2mm joints2mm below them.
            Cube(Surface.Floor, new Vector3(0, -0.252f, 7), new Vector3(11.6f, 0.5f, 42.5f));
            for (int z = -14; z < 28; z++)
            for (int x = -5; x <= 5; x++)
            {
                Surface s = random.Next(18) == 0 ? Surface.Moss : (random.Next(8) == 0 ? Surface.DarkStone : Surface.Floor);
                _ = Range(0, 0.015f); // Preserve the downstream authored random stream; height is rigid.
                Cube(s, new Vector3(x, -0.065f, z + 0.5f), new Vector3(0.998f, 0.13f, 0.998f));
            }
            // Recessed slate runner and brass wayfinding inlays lead the eye toward the Oracle gate.
            for (int z = -12; z < 23; z++)
            {
                Cube(Surface.DarkStone, new Vector3(-3.45f, 0.004f, z + 0.5f), new Vector3(0.16f, 0.026f, 0.95f));
                Cube(Surface.DarkStone, new Vector3(3.45f, 0.004f, z + 0.5f), new Vector3(0.16f, 0.026f, 0.95f));
                if (z % 3 == 0)
                {
                    Cube(Surface.Brass, new Vector3(-3.45f, 0.023f, z + 0.5f), new Vector3(0.065f, 0.018f, 0.32f));
                    Cube(Surface.Brass, new Vector3(3.45f, 0.023f, z + 0.5f), new Vector3(0.065f, 0.018f, 0.32f));
                }
            }
            foreach (float c in alcoves)
            foreach (float side in new[] { -1f, 1f })
            {
                BoxCollider("Alcove floor", new Vector3(side * 7.6f, -0.3f, c), new Vector3(4.5f, 0.6f, 4.4f));
                Cube(Surface.Floor, new Vector3(side * 7.6f, -0.227f, c), new Vector3(4.5f, 0.45f, 4.4f));
                for (int x = 0; x < 5; x++)
                for (int z = 0; z < 4; z++)
                    {
                    // The fifth column fills the original footprint; retain exactly16 random draws/alcove.
                    bool moss=x<4?random.Next(6)==0:PixelHash(x,z,Mathf.RoundToInt(c*10)+127)>.84f;
                    Cube(moss?Surface.Moss:Surface.Floor, new Vector3(side * (5.8f + x * .9f), -0.05f, c - 1.65f + z * 1.1f), new Vector3(.898f, .1f, 1.098f));
                }
            }
        }

        // # ARCHITECTURE SNAPSHOT
        // Tutorial-only render backing closes mortar/join sightlines without changing collision or apertures.
        // Reuse the existing DarkStone batch/resource owner; no new objects, materials or runtime work.
        private void BuildTutorialMasonryBacking()
        {
            Cube(Surface.DarkStone, new Vector3(-5.99f, 3.65f, -9.65f), new Vector3(0.42f, 7.6f, 9.06f)); // Closed hall wall backing
            Cube(Surface.DarkStone, new Vector3(-5.99f, 3.65f, 2.5f), new Vector3(0.42f, 7.6f, 6.76f)); // Closed hall wall backing
            Cube(Surface.DarkStone, new Vector3(-5.99f, 3.65f, 13.0f), new Vector3(0.42f, 7.6f, 5.76f)); // Closed hall wall backing
            Cube(Surface.DarkStone, new Vector3(-5.99f, 3.65f, 24.15f), new Vector3(0.42f, 7.6f, 8.06f)); // Closed hall wall backing
            Cube(Surface.DarkStone, new Vector3(-5.99f, 5.85f, 7.0f), new Vector3(0.42f, 3.22f, 42.7f)); // Continuous hall header backing
            Cube(Surface.DarkStone, new Vector3(5.99f, 3.65f, -9.65f), new Vector3(0.42f, 7.6f, 9.06f)); // Closed hall wall backing
            Cube(Surface.DarkStone, new Vector3(5.99f, 3.65f, 2.5f), new Vector3(0.42f, 7.6f, 6.76f)); // Closed hall wall backing
            Cube(Surface.DarkStone, new Vector3(5.99f, 3.65f, 13.0f), new Vector3(0.42f, 7.6f, 5.76f)); // Closed hall wall backing
            Cube(Surface.DarkStone, new Vector3(5.99f, 3.65f, 24.15f), new Vector3(0.42f, 7.6f, 8.06f)); // Closed hall wall backing
            Cube(Surface.DarkStone, new Vector3(5.99f, 5.85f, 7.0f), new Vector3(0.42f, 3.22f, 42.7f)); // Continuous hall header backing
            Cube(Surface.DarkStone, new Vector3(0.0f, 3.65f, -14.4f), new Vector3(12.4f, 7.6f, 0.6f)); // Closed arrival wall behind the existing door
            Cube(Surface.DarkStone, new Vector3(0.0f, 3.65f, 28.3f), new Vector3(12.4f, 7.6f, 0.3f)); // Rear wall backing behind the portal chamber
            Cube(Surface.DarkStone, new Vector3(-5.99f, 2.15f, -5.23f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(-7.68f, 2.55f, -5.4f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(-5.99f, 2.15f, -0.77f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(-7.68f, 2.55f, -0.6f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(-10.0f, 2.55f, -3.0f), new Vector3(0.36f, 5.4f, 5.3f)); // Solid crypt outer wall backing
            Cube(Surface.DarkStone, new Vector3(5.99f, 2.15f, -5.23f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(7.68f, 2.55f, -5.4f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(5.99f, 2.15f, -0.77f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(7.68f, 2.55f, -0.6f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(10.0f, 0.775f, -3.0f), new Vector3(0.36f, 1.85f, 5.3f)); // Window wall below the sill
            Cube(Surface.DarkStone, new Vector3(10.0f, 4.835f, -3.0f), new Vector3(0.36f, 0.83f, 5.3f)); // Window wall above the lintel
            Cube(Surface.DarkStone, new Vector3(10.0f, 2.55f, -5.025f), new Vector3(0.36f, 5.4f, 1.25f)); // Window wall beside the reveal
            Cube(Surface.DarkStone, new Vector3(10.0f, 2.55f, -0.975f), new Vector3(0.36f, 5.4f, 1.25f)); // Window wall beside the reveal
            Cube(Surface.DarkStone, new Vector3(-5.99f, 2.15f, 5.77f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(-7.68f, 2.55f, 5.6f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(-5.99f, 2.15f, 10.23f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(-7.68f, 2.55f, 10.4f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(-10.0f, 2.55f, 8.0f), new Vector3(0.36f, 5.4f, 5.3f)); // Solid crypt outer wall backing
            Cube(Surface.DarkStone, new Vector3(5.99f, 2.15f, 5.77f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(7.68f, 2.55f, 5.6f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(5.99f, 2.15f, 10.23f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(7.68f, 2.55f, 10.4f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(10.0f, 0.775f, 8.0f), new Vector3(0.36f, 1.85f, 5.3f)); // Window wall below the sill
            Cube(Surface.DarkStone, new Vector3(10.0f, 4.835f, 8.0f), new Vector3(0.36f, 0.83f, 5.3f)); // Window wall above the lintel
            Cube(Surface.DarkStone, new Vector3(10.0f, 2.55f, 5.975f), new Vector3(0.36f, 5.4f, 1.25f)); // Window wall beside the reveal
            Cube(Surface.DarkStone, new Vector3(10.0f, 2.55f, 10.025f), new Vector3(0.36f, 5.4f, 1.25f)); // Window wall beside the reveal
            Cube(Surface.DarkStone, new Vector3(-5.99f, 2.15f, 15.77f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(-7.68f, 2.55f, 15.6f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(-5.99f, 2.15f, 20.23f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(-7.68f, 2.55f, 20.4f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(-10.0f, 2.55f, 18.0f), new Vector3(0.36f, 5.4f, 5.3f)); // Solid crypt outer wall backing
            Cube(Surface.DarkStone, new Vector3(5.99f, 2.15f, 15.77f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(7.68f, 2.55f, 15.6f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(5.99f, 2.15f, 20.23f), new Vector3(0.42f, 4.6f, 0.56f)); // Continuous alcove jamb back
            Cube(Surface.DarkStone, new Vector3(7.68f, 2.55f, 20.4f), new Vector3(4.9f, 5.4f, 0.5f)); // Alcove side wall backing
            Cube(Surface.DarkStone, new Vector3(10.0f, 0.775f, 18.0f), new Vector3(0.36f, 1.85f, 5.3f)); // Window wall below the sill
            Cube(Surface.DarkStone, new Vector3(10.0f, 4.835f, 18.0f), new Vector3(0.36f, 0.83f, 5.3f)); // Window wall above the lintel
            Cube(Surface.DarkStone, new Vector3(10.0f, 2.55f, 15.975f), new Vector3(0.36f, 5.4f, 1.25f)); // Window wall beside the reveal
            Cube(Surface.DarkStone, new Vector3(10.0f, 2.55f, 20.025f), new Vector3(0.36f, 5.4f, 1.25f)); // Window wall beside the reveal
        }

        // # ARCHITECTURE SNAPSHOT
        // Build-time render-only warm stone beds fill the narrowed joints near brick faces.
        // Preserve all51 original deep backing boxes and their intentional aperture silhouettes.
        // Existing Stone batch only: no new material, collider, light, object or Update work.
        private void BuildTutorialStoneJointBeds()
        {
            Cube(Surface.Stone, new Vector3(-5.532f, 3.65f, -9.65f), new Vector3(0.08f, 7.6f, 9.06f)); // Closed hall wall backing
            Cube(Surface.Stone, new Vector3(-5.532f, 3.65f, 2.5f), new Vector3(0.08f, 7.6f, 6.76f)); // Closed hall wall backing
            Cube(Surface.Stone, new Vector3(-5.532f, 3.65f, 13f), new Vector3(0.08f, 7.6f, 5.76f)); // Closed hall wall backing
            Cube(Surface.Stone, new Vector3(-5.532f, 3.65f, 24.15f), new Vector3(0.08f, 7.6f, 8.06f)); // Closed hall wall backing
            Cube(Surface.Stone, new Vector3(-5.532f, 5.85f, 7f), new Vector3(0.08f, 3.22f, 42.7f)); // Continuous hall header backing
            Cube(Surface.Stone, new Vector3(5.532f, 3.65f, -9.65f), new Vector3(0.08f, 7.6f, 9.06f)); // Closed hall wall backing
            Cube(Surface.Stone, new Vector3(5.532f, 3.65f, 2.5f), new Vector3(0.08f, 7.6f, 6.76f)); // Closed hall wall backing
            Cube(Surface.Stone, new Vector3(5.532f, 3.65f, 13f), new Vector3(0.08f, 7.6f, 5.76f)); // Closed hall wall backing
            Cube(Surface.Stone, new Vector3(5.532f, 3.65f, 24.15f), new Vector3(0.08f, 7.6f, 8.06f)); // Closed hall wall backing
            Cube(Surface.Stone, new Vector3(5.532f, 5.85f, 7f), new Vector3(0.08f, 3.22f, 42.7f)); // Continuous hall header backing
            Cube(Surface.Stone, new Vector3(0f, 3.65f, -13.992f), new Vector3(12.4f, 7.6f, 0.08f)); // Closed arrival wall behind the existing door
            Cube(Surface.Stone, new Vector3(0f, 3.65f, 27.892f), new Vector3(12.4f, 7.6f, 0.08f)); // Rear wall backing behind the portal chamber
            Cube(Surface.Stone, new Vector3(-5.12f, 2.15f, -5.23f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(-7.68f, 2.55f, -5.162f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(-5.12f, 2.15f, -0.77f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(-7.68f, 2.55f, -0.838f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(-9.612f, 2.55f, -3f), new Vector3(0.08f, 5.4f, 5.3f)); // Solid crypt outer wall backing
            Cube(Surface.Stone, new Vector3(5.12f, 2.15f, -5.23f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(7.68f, 2.55f, -5.162f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(5.12f, 2.15f, -0.77f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(7.68f, 2.55f, -0.838f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(9.612f, 0.775f, -3f), new Vector3(0.08f, 1.85f, 5.3f)); // Window wall below the sill
            Cube(Surface.Stone, new Vector3(9.612f, 4.835f, -3f), new Vector3(0.08f, 0.83f, 5.3f)); // Window wall above the lintel
            Cube(Surface.Stone, new Vector3(9.612f, 2.55f, -5.025f), new Vector3(0.08f, 5.4f, 1.25f)); // Window wall beside the reveal
            Cube(Surface.Stone, new Vector3(9.612f, 2.55f, -0.975f), new Vector3(0.08f, 5.4f, 1.25f)); // Window wall beside the reveal
            Cube(Surface.Stone, new Vector3(-5.12f, 2.15f, 5.77f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(-7.68f, 2.55f, 5.838f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(-5.12f, 2.15f, 10.23f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(-7.68f, 2.55f, 10.162f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(-9.612f, 2.55f, 8f), new Vector3(0.08f, 5.4f, 5.3f)); // Solid crypt outer wall backing
            Cube(Surface.Stone, new Vector3(5.12f, 2.15f, 5.77f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(7.68f, 2.55f, 5.838f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(5.12f, 2.15f, 10.23f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(7.68f, 2.55f, 10.162f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(9.612f, 0.775f, 8f), new Vector3(0.08f, 1.85f, 5.3f)); // Window wall below the sill
            Cube(Surface.Stone, new Vector3(9.612f, 4.835f, 8f), new Vector3(0.08f, 0.83f, 5.3f)); // Window wall above the lintel
            Cube(Surface.Stone, new Vector3(9.612f, 2.55f, 5.975f), new Vector3(0.08f, 5.4f, 1.25f)); // Window wall beside the reveal
            Cube(Surface.Stone, new Vector3(9.612f, 2.55f, 10.025f), new Vector3(0.08f, 5.4f, 1.25f)); // Window wall beside the reveal
            Cube(Surface.Stone, new Vector3(-5.12f, 2.15f, 15.77f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(-7.68f, 2.55f, 15.838f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(-5.12f, 2.15f, 20.23f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(-7.68f, 2.55f, 20.162f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(-9.612f, 2.55f, 18f), new Vector3(0.08f, 5.4f, 5.3f)); // Solid crypt outer wall backing
            Cube(Surface.Stone, new Vector3(5.12f, 2.15f, 15.77f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(7.68f, 2.55f, 15.838f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(5.12f, 2.15f, 20.23f), new Vector3(0.08f, 4.6f, 0.55f)); // Continuous alcove jamb back
            Cube(Surface.Stone, new Vector3(7.68f, 2.55f, 20.162f), new Vector3(4.9f, 5.4f, 0.05f)); // Alcove side wall backing
            Cube(Surface.Stone, new Vector3(9.612f, 0.775f, 18f), new Vector3(0.08f, 1.85f, 5.3f)); // Window wall below the sill
            Cube(Surface.Stone, new Vector3(9.612f, 4.835f, 18f), new Vector3(0.08f, 0.83f, 5.3f)); // Window wall above the lintel
            Cube(Surface.Stone, new Vector3(9.612f, 2.55f, 15.975f), new Vector3(0.08f, 5.4f, 1.25f)); // Window wall beside the reveal
            Cube(Surface.Stone, new Vector3(9.612f, 2.55f, 20.025f), new Vector3(0.08f, 5.4f, 1.25f)); // Window wall beside the reveal
        }

        private void BuildHallWalls()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                for (int row = 0; row < 12; row++)
                for (int n = 0; n < 42; n++)
                {
                    float z = -13.5f + n + (row % 2 == 0 ? 0f : 0.46f);
                    bool opening = IsAlcove(z, 2.3f) && row < 7;
                    if (opening) continue;
                    Surface material=StoneAt(row,z);
                    _ = Range(-0.006f,0.006f); // Keep later props deterministic; wall faces align.
                    Cube(material,new Vector3(side*5.78f,row*.61f+.30f,z),new Vector3(.58f,.608f,.998f));
                }
                // Physical wall segments match the three architectural openings.
                WallSegment(side, -14.1f, -5.2f);
                WallSegment(side, -0.8f, 5.8f);
                WallSegment(side, 10.2f, 15.8f);
                WallSegment(side, 20.2f, 28.1f);
                foreach (float c in alcoves)
                    BoxCollider("High alcove lintel", new Vector3(side * 5.8f, 5.85f, c), new Vector3(0.7f, 3.1f, 4.4f));
                for (int z = -14; z < 28; z++)
                {
                    if (!IsAlcove(z + 0.5f, 2.4f))
                    {
                        Cube(Surface.DarkStone, new Vector3(side * 5.38f, 0.25f, z + 0.5f), new Vector3(0.38f, 0.49f, 0.994f));
                        Cube(Surface.PaleStone, new Vector3(side * 5.38f, 1.12f, z + 0.5f), new Vector3(0.22f, 0.16f, 0.994f));
                    }
                }
            }
            BuildBrickEndWall(-14.25f, false);
            BuildBrickEndWall(28.15f, true);
        }

        private bool IsAlcove(float z, float halfWidth)
        {
            foreach (float c in alcoves) if (Mathf.Abs(z - c) < halfWidth) return true;
            return false;
        }

        private void WallSegment(float side, float min, float max)
        {
            BoxCollider("Hall masonry", new Vector3(side * 5.84f, 3.7f, (min + max) * 0.5f), new Vector3(0.72f, 7.4f, max - min));
        }

        private Surface StoneAt(int row, float z)
        {
            int chance = random.Next(100);
            if (chance < (row < 3 ? 22 : 7)) return Surface.Moss;
            if (chance < 36) return Surface.DarkStone;
            if (chance > 91) return Surface.PaleStone;
            return Surface.Stone;
        }

        private void BuildBrickEndWall(float z, bool rear)
        {
            for (int row = 0; row < 12; row++)
            for (int x = -5; x <= 5; x++)
                Cube(StoneAt(row, z), new Vector3(x + (row % 2) * 0.17f, row * 0.61f + 0.3f, z), new Vector3(.998f, .608f, .6f));
            BoxCollider(rear ? "Rear wall" : "Entry wall", new Vector3(0, 3.7f, z), new Vector3(11.8f, 7.4f, 0.7f));
        }

        private void BuildRoofAndArches()
        {
            // A warm solid slab sits behind close-fitting dark coffers; joints have shallow stone fill.
            Cube(Surface.Stone, new Vector3(0f, 7.23f, 7f), new Vector3(11.7f, 0.58f, 42.5f));
            for (int z = -13; z < 28; z += 2)
            for (int x = -5; x <= 5; x += 2)
                Cube(random.Next(4) == 0 ? Surface.Stone : Surface.DarkStone, new Vector3(x, 7.15f + Range(-0.012f, 0.012f), z), new Vector3(1.994f, 0.5f, 1.994f));
            foreach (float z in TutorialPierRows)
            {
                Pillar(-4.72f, z);
                Pillar(4.72f, z);
                ArchAcross(z);
            }
            foreach (float side in new[] { -1f, 1f })
            foreach (float c in alcoves)
                AlcoveArch(side, c);
            // All three iron fixtures hang on the corridor centerline; fixed light centers match.
            Chandelier(0f, -1.3f, 5.9f);
            Chandelier(0f, 10.8f, 5.7f);
            Chandelier(0f, 21.0f, 5.9f);
        }

        private void Pillar(float x, float z)
        {
            Cube(Surface.Stone, new Vector3(x, 2.83f, z), new Vector3(.951f, 4.52f, .996f));
            Cube(Surface.DarkStone, new Vector3(x, 0.18f, z), new Vector3(1.36f, 0.36f, 1.45f));
            Cube(Surface.PaleStone, new Vector3(x, 0.48f, z), new Vector3(1.17f, 0.24f, 1.25f));
            for (int n = 0; n < 8; n++)
                Cube(n < 2 && random.Next(3) == 0 ? Surface.Moss : (n % 3 == 0 ? Surface.PaleStone : Surface.Stone), new Vector3(x, 0.9f + n * 0.55f, z), new Vector3(.955f, .548f, 1f));
            Cube(Surface.DarkStone, new Vector3(x, 5.18f, z), new Vector3(1.15f, 0.22f, 1.25f));
            Cube(Surface.PaleStone, new Vector3(x, 5.43f, z), new Vector3(1.43f, 0.29f, 1.5f));
            BoxCollider("Stone pier", new Vector3(x, 2.7f, z), new Vector3(1.02f, 5.4f, 1.14f));
            // Tall inset panel and a short gilt band catch grazing torchlight.
            float face = x > 0 ? -1f : 1f;
            Cube(Surface.DarkStone, new Vector3(x + face * 0.48f, 2.7f, z), new Vector3(0.055f, 2.8f, 0.4f));
            Cube(Surface.Brass, new Vector3(x + face * 0.52f, 3.82f, z), new Vector3(0.055f, 0.07f, 0.5f));
        }

        private void ArchAcross(float z)
        {
            // Stair-stepped voussoirs preserve the chunky, deliberately voxel silhouette.
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 5; i++)
            {
                float x = side * (4.1f - i * 0.78f);
                float y = 5.62f + i * 0.31f;
                Cube(i % 2 == 0 ? Surface.Stone : Surface.PaleStone, new Vector3(x, y, z), new Vector3(0.83f, 0.68f, 1.15f));
                Cube(Surface.DarkStone, new Vector3(x, y + 0.46f, z), new Vector3(0.84f, 0.28f, 1.2f));
            }
            Cube(Surface.PaleStone, new Vector3(0, 6.91f, z), new Vector3(0.78f, 0.89f, 1.21f));
            Cube(Surface.Brass, new Vector3(0, 6.77f, z - 0.62f), new Vector3(0.17f, 0.3f, 0.055f));
        }

        private void AlcoveArch(float side, float z)
        {
            foreach (float end in new[] { -1f, 1f })
            {
                float zz = z + end * 2.23f;
                Cube(Surface.DarkStone, new Vector3(side * 5.42f, 0.2f, zz), new Vector3(0.88f, 0.4f, 0.74f));
                for (int row = 0; row < 7; row++)
                    Cube(row % 3 == 0 ? Surface.PaleStone : Surface.Stone, new Vector3(side * 5.45f, 0.66f + row * 0.5f, zz), new Vector3(0.8f, 0.494f, 0.55f));
                for (int step = 0; step < 3; step++)
                    Cube(Surface.PaleStone, new Vector3(side * 5.45f, 4.11f + step * 0.28f, z + end * (1.77f - step * 0.6f)), new Vector3(0.84f, 0.58f, 0.65f));
            }
            Cube(Surface.DarkStone, new Vector3(side * 5.45f, 4.97f, z), new Vector3(0.9f, 0.23f, 4.7f));
        }

        private void BuildAlcoves()
        {
            foreach (float c in alcoves)
            foreach (float side in new[] { -1f, 1f })
            {
                float outer = side * 9.85f;
                Cube(Surface.DarkStone, new Vector3(side * 7.7f, 5.15f, c), new Vector3(4.6f, 0.44f, 4.7f));
                foreach (float end in new[] { -1f, 1f })
                {
                    float zz = c + end * 2.38f;
                    for (int row = 0; row < 9; row++)
                    for (int x = 0; x < 5; x++)
                        Cube(StoneAt(row, zz), new Vector3(side * (5.7f + x * 0.92f), row * 0.56f + 0.28f, zz), new Vector3(.918f, .558f, .49f));
                    BoxCollider("Alcove side wall", new Vector3(side * 7.65f, 2.5f, zz), new Vector3(4.7f, 5f, 0.5f));
                }
                for (int row = 0; row < 9; row++)
                for (int z = 0; z < 5; z++)
                {
                    float zz = c - 1.9f + z * 0.95f;
                    bool window = side > 0 && row >= 3 && row <= 7 && z >= 1 && z <= 3;
                    if (!window)
                        Cube(StoneAt(row, zz), new Vector3(outer, row * 0.56f + 0.28f, zz), new Vector3(.56f, .558f, .948f));
                }
                BoxCollider("Alcove outer wall", new Vector3(outer, 2.5f, c), new Vector3(0.65f, 5f, 4.8f));
                if (side > 0) MoonWindow(c);
                else
                {
                    Sarcophagus(new Vector3(-8.0f, 0, c + 0.3f), 0f);
                    CandleCluster(new Vector3(-7.65f, 1.22f, c + 0.3f), 5);
                    Banner(-9.48f, c - 0.02f, true, 3.65f);
                    Bones(new Vector3(-6.55f, 0f, c - 1.3f),true);
                }
                Fern(new Vector3(side * 8.95f, 0.02f, c - 1.75f), 0.75f);
                Fern(new Vector3(side * 5.95f, 0.02f, c + 1.8f), 0.55f);
            }
        }

        private void MoonWindow(float z)
        {
            const float x = 9.52f;
            // Real visual openings: reveal sides frame the exterior rather than an opaque blue backboard.
            foreach(float side in new[]{-1f,1f})
                Cube(Surface.DarkStone,new Vector3(9.95f,3.08f,z+side*1.55f),new Vector3(1.7f,2.98f,.23f));
            Cube(Surface.DarkStone,new Vector3(9.95f,1.48f,z),new Vector3(1.7f,.25f,3.3f));
            Cube(Surface.DarkStone,new Vector3(9.95f,4.64f,z),new Vector3(1.7f,.25f,3.3f));
            for (int i = -2; i <= 2; i++)
                Cube(Surface.Iron, new Vector3(x - 0.08f, 3.1f, z + i * 0.49f), new Vector3(0.12f, 2.7f, 0.075f));
            foreach (float y in new[] { 2.35f, 3.5f })
                Cube(Surface.Iron, new Vector3(x - 0.14f, y, z), new Vector3(0.12f, 0.075f, 2.7f));
            Cube(Surface.PaleStone, new Vector3(x - 0.2f, 1.65f, z), new Vector3(0.6f, 0.28f, 3.05f));
            Cube(Surface.PaleStone, new Vector3(x - 0.18f, 4.52f, z), new Vector3(0.5f, 0.26f, 3.05f));
            foreach (float end in new[] { -1f, 1f })
                Cube(Surface.Stone, new Vector3(x - 0.18f, 3.06f, z + end * 1.42f), new Vector3(0.5f, 2.7f, 0.28f));
            AddWindowShafts(z);
            CandleCluster(new Vector3(7.9f, 0.04f, z - 1.35f), 3);
            Skull(new Vector3(8.2f, 0f, z + 1.25f), 0.38f, 180f);
            Crate(new Vector3(8.65f, 0.05f, z + 1.45f), new Vector3(0.82f, 0.72f, 0.7f), 0f);
        }

        private void AddWindowShafts(float z)
        {
            if(windowBeams==null)
            {
                // Reuse the bundled transparent unlit grid shader with identical colors: no grid or fog.
                Shader shader=Resources.Load<Shader>("Shaders/LaneGateGrid")??Shader.Find("Universal Render Pipeline/Unlit");
                Material material=new Material(shader){name="Restrained cool window light shafts"};
                Color color=new Color(.25f,.55f,.9f,.045f);
                material.SetColor("_BaseColor",color);
                if(material.HasProperty("_GridColor"))material.SetColor("_GridColor",color);
                else
                {
                    material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);
                    material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3025;
                }
                resources.Add(material);windowBeams=new Batch(material);
            }
            for(int strip=-1;strip<=1;strip++)
            {
                float start=z+strip*.78f;
                Vector3 a=new Vector3(9.24f,4.12f,start-.09f);
                Vector3 b=new Vector3(9.24f,2.04f,start+.09f);
                Vector3 c=new Vector3(5.48f,.045f,start+.91f);
                Vector3 d=new Vector3(5.48f,.045f,start+.55f);
                windowBeams.Quad(a,b,c,d,true);
            }
        }

        private void ConfigureContinuousNightSky(bool tutorial)
        {
            if(!skyCaptured){previousSkybox=RenderSettings.skybox;skyCaptured=true;}
            if(tutorialSkybox==null)tutorialSkybox=Resources.Load<Material>("OracleTutorialNightSky");
            if(tutorial && tutorialSkybox!=null && tutorialSkybox.shader!=null && tutorialSkybox.shader.isSupported
                && tutorialSkybox.GetTexture("_Tex") is Cubemap)
            {
                RenderSettings.skybox=tutorialSkybox;
                return;
            }
            if(nightSkybox==null)
            {
                Shader shader=Resources.Load<Shader>("Shaders/OracleNightSky");
                if(shader==null || !shader.isSupported)
                {
                    // A Skybox camera falls back to its dark-blue background color
                    // when this resource is unavailable. Never restore a finite plane.
                    Debug.LogWarning("Original night sky shader unavailable; use the camera's midnight-blue background.",this);
                    return;
                }
                nightSkybox=new Material(shader){name="Original continuous Oracle midnight sky"};
                nightSkybox.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
                resources.Add(nightSkybox);
            }
            RenderSettings.skybox=nightSkybox;
            // Preserve the root's explicit Trilight/probe lighting. No environment
            // capture, DynamicGI update, extra light or per-window camera is needed.
        }

        private void BuildWindowBackdrop()
        {
            // Retain the bounded non-colliding islands and voxel clouds. The sky
            // behind them is continuous at infinity, with no backplane edge or far-clip cut.
            Vector3[] islands={new Vector3(19.5f,3.2f,-5f),new Vector3(26f,5.2f,9f),new Vector3(22.5f,2.8f,23f)};
            for(int island=0;island<islands.Length;island++)
            {
                Vector3 p=islands[island];
                for(int tier=0;tier<4;tier++)
                {
                    float width=4.6f-tier*.95f;
                    Cube(tier==0?Surface.Moss:Surface.DarkStone,p+new Vector3(.14f*tier,-tier*.62f,0),new Vector3(width,.64f,width*.8f));
                }
                for(int stone=0;stone<4;stone++)
                {
                    float offset=PixelHash(stone,island,971);
                    Cube(Surface.PaleStone,p+new Vector3(-.9f+stone*.61f,.55f+offset*.45f,.3f),new Vector3(.46f,.8f+offset,.48f));
                }
                Cube(Surface.Wood,p+new Vector3(.7f,1.2f,-.6f),new Vector3(.27f,1.8f,.27f));
                Cube(Surface.Green,p+new Vector3(.7f,2.2f,-.6f),new Vector3(1.5f,.75f,1.35f));
                Cube(Surface.Green,p+new Vector3(.5f,2.7f,-.4f),new Vector3(.85f,.4f,.9f));
            }
            for(int cloud=0;cloud<8;cloud++)
            {
                Vector3 p=new Vector3(26f+cloud%3*4.1f,8.1f+cloud%3*2.1f,-13f+cloud*6.1f);
                Cube(Surface.Cloud,p,new Vector3(2.8f,.7f,3.7f));
                Cube(Surface.Cloud,p+new Vector3(.6f,.45f,.35f),new Vector3(1.6f,.45f,2.25f));
                Cube(Surface.Cloud,p+new Vector3(-.7f,-.12f,-1.4f),new Vector3(1.55f,.42f,1.5f));
            }
            // One restrained procedural moon belongs to the continuous sky;
            // no second nearby voxel disc or giant background geometry remains.
        }

        private void BuildEntrance()
        {
            // Framing at the player's back makes the arrival feel like a complete place.
            foreach (float x in new[] { -2.15f, 2.15f })
            {
                Cube(Surface.PaleStone, new Vector3(x, 2.2f, -13.75f), new Vector3(0.75f, 4.4f, 0.6f));
                Cube(Surface.DarkStone, new Vector3(x, 0.24f, -13.48f), new Vector3(1f, 0.48f, 1f));
            }
            Cube(Surface.Wood, new Vector3(0, 2.25f, -13.82f), new Vector3(3.5f, 4.5f, 0.2f));
            for (int i = -3; i <= 3; i++)
                Cube(Surface.Iron, new Vector3(i * 0.51f, 2.25f, -13.66f), new Vector3(0.055f, 4.5f, 0.07f));
            foreach (float y in new[] { 0.7f, 2.2f, 3.75f })
                Cube(Surface.Iron, new Vector3(0, y, -13.57f), new Vector3(3.6f, 0.15f, 0.11f));
            Cube(Surface.Brass, new Vector3(0.3f, 1.8f, -13.47f), new Vector3(0.18f, 0.25f, 0.08f));
            Crate(new Vector3(-4.1f, 0, -10.2f), new Vector3(1.03f, 1.0f, 1.0f), 0f);
            Crate(new Vector3(-4.22f, 1.02f, -10.15f), new Vector3(0.8f, 0.75f, 0.82f), 7f);
            Barrel(new Vector3(3.7f, 0, -10.45f), 0.53f, 1.28f);
            Bones(new Vector3(4.6f, 0.04f, -9.1f));
        }

        private void BuildTutorialApproach()
        {
            // A restrained green approach line makes the living shrine readable from the arrival door.
            for (int i = 0; i < 10; i++)
            {
                float z = -8.7f + i * 0.82f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Cube(Surface.Brass, new Vector3(side * 1.88f, 0.018f, z), new Vector3(0.14f, 0.025f, 0.48f));
                    Cube(Surface.RuneGreen, new Vector3(side * 1.88f, 0.037f, z), new Vector3(0.055f, 0.015f, 0.26f));
                }
            }
            foreach (float z in new[] { -5.4f, -2.5f })
            {
                Cube(Surface.Brass, new Vector3(0, 0.016f, z), new Vector3(0.85f, 0.025f, 0.09f));
                foreach (float side in new[] { -1f, 1f })
                    Cube(Surface.RuneGreen, new Vector3(side * 0.24f, 0.033f, z - 0.08f), new Vector3(0.08f, 0.018f, 0.38f), Quaternion.Euler(0, side * 45f, 0));
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Fern(new Vector3(side * 3.65f, 0.02f, -0.65f), 0.85f);
                Fern(new Vector3(side * 3.65f, 0.02f, 3.55f), 0.8f);
            }
        }

        private void BuildSanctum()
        {
            // Real shallow steps with solid colliders are traversable by the standard player controller.
            for (int n = 0; n < 3; n++)
            {
                float front = 21.9f + n * 0.7f;
                float height = (n + 1) * 0.22f;
                Cube(n == 2 ? Surface.Stone : Surface.DarkStone, new Vector3(0, height * 0.5f, (front + 27.8f) * 0.5f), new Vector3(7.15f, height, 27.8f - front));
                Cube(Surface.PaleStone, new Vector3(0, height - 0.045f, front + 0.09f), new Vector3(7.3f, 0.09f, 0.2f));
                BoxCollider("Sanctum step", new Vector3(0, height * 0.5f, (front + 27.8f) * 0.5f), new Vector3(7.15f, height, 27.8f - front));
            }
            for (int x = -3; x <= 3; x++)
            for (int z = 24; z < 28; z++)
                Cube(Surface.Floor, new Vector3(x, 0.665f, z + 0.3f), new Vector3(0.994f, 0.025f, 0.994f));
            foreach (float side in new[] { -1f, 1f })
            {
                // Two nested arches provide depth and broad silhouettes behind the finer crystal trim.
                for (int y = 0; y < 8; y++)
                {
                    Cube(y % 2 == 0 ? Surface.PaleStone : Surface.Stone, new Vector3(side * 2.23f, 1.0f + y * 0.53f, 25.58f), new Vector3(0.63f, 0.50f, 0.94f));
                    Cube(Surface.DarkStone, new Vector3(side * 2.87f, 1.02f + y * 0.61f, 26.18f), new Vector3(0.70f, 0.58f, 1.20f));
                }
                for (int n = 0; n < 4; n++)
                {
                    Cube(Surface.PaleStone, new Vector3(side * (1.94f - n * 0.55f), 5.15f + n * 0.30f, 25.58f), new Vector3(0.64f, 0.63f, 0.94f));
                    Cube(Surface.Stone, new Vector3(side * (2.52f - n * 0.69f), 5.88f + n * 0.25f, 26.18f), new Vector3(0.79f, 0.76f, 1.2f));
                }
                Cube(Surface.Brass, new Vector3(side * 1.88f, 2.88f, 25.08f), new Vector3(0.075f, 4.05f, 0.07f));
                Obelisk(new Vector3(side * 3.0f, 0.66f, 25.2f));
                CandleCluster(new Vector3(side * 2.85f, 0.68f, 23.75f), 7);
                Banner(side * 4.45f, 26.65f, false, 5.2f, true);
                batches[Surface.PortalCrystal].Crystal(new Vector3(side * 3.0f, 3.2f, 25.2f), 0.18f, 0.95f, Quaternion.Euler(0, side * 20f, side * -8f));
                RegisterRefugeCrystalTip(new Vector3(side * 3f, 3.2f, 25.2f) + Quaternion.Euler(0, side * 20f, side * -8f) * (Vector3.up * .475f));
            }
            Cube(Surface.PaleStone, new Vector3(0, 6.15f, 25.58f), new Vector3(0.66f, 0.8f, 1.04f));
            Cube(Surface.Brass, new Vector3(0, 6.2f, 25.025f), new Vector3(0.23f, 0.45f, 0.07f));
            // Only one animated surface is drawn; the original voxel surface remains a shader fallback.
            bool anomalyReady=BuildPortalSpiral();
            if(!anomalyReady)
            {
            Cube(Surface.Portal, new Vector3(0, 2.8f, 25.87f), new Vector3(3.72f, 4.12f, 0.10f));
            Cube(Surface.Portal, new Vector3(0, 5.02f, 25.87f), new Vector3(3.1f, 0.38f, 0.10f));
            Cube(Surface.Portal, new Vector3(0, 5.31f, 25.87f), new Vector3(2.2f, 0.26f, 0.10f));
            Cube(Surface.Portal, new Vector3(0, 5.54f, 25.87f), new Vector3(1.1f, 0.22f, 0.10f));
            }
            for (int i = 0; i <= 16; i++)
            {
                float angle = i * Mathf.PI / 16f;
                Vector3 p = new Vector3(Mathf.Cos(angle) * 2.15f, 3.88f + Mathf.Sin(angle) * 1.9f, 25.04f);
                batches[Surface.PortalCrystal].Crystal(p, 0.12f, i % 2 == 0 ? 0.64f : 0.42f, Quaternion.Euler(0, i * 23f, angle * Mathf.Rad2Deg - 90f));
            }
            // Pixel aperture marks and a suspended geometric seal identify the unopened exit.
            for (int i = -5; i <= 5; i++)
            {
                float x = i * 0.27f;
                float y = 1.1f + (Mathf.Abs(i) % 3) * 0.17f;
                Cube(Surface.PortalCrystal, new Vector3(x, y, 25.70f), new Vector3(0.035f, 0.22f, 0.025f));
                Cube(Surface.PortalCrystal, new Vector3(x, 5.15f - (Mathf.Abs(i) % 3) * 0.13f, 25.70f), new Vector3(0.045f, 0.1f, 0.025f));
            }
            Batch seal = new Batch(batches[Surface.Brass].Material);
            SealBox(seal, new Vector3(0.375f, 3.155f, 25.30f), new Vector3(0.13f, 1.08f, 0.16f), 45f);
            SealBox(seal, new Vector3(-0.375f, 3.155f, 25.30f), new Vector3(0.13f, 1.08f, 0.16f), -45f);
            SealBox(seal, new Vector3(-0.375f, 2.405f, 25.30f), new Vector3(0.13f, 1.08f, 0.16f), 45f);
            SealBox(seal, new Vector3(0.375f, 2.405f, 25.30f), new Vector3(0.13f, 1.08f, 0.16f), -45f);
            seal.Box(new Vector3(0f, 2.78f, 25.15f), new Vector3(0.44f, 0.44f, 0.2f), Quaternion.identity);
            portalSeal = FinishBatch("Sealed oracle sigil", seal, geometryRoot);
            if (portalUnlocked) UnlockPortal();
        }

        private bool BuildPortalSpiral()
        {
            Transform aperture = new GameObject("Recessed elliptical violet current").transform;
            aperture.SetParent(geometryRoot, false);
            aperture.localPosition = new Vector3(0, 3.04f, 25.65f);
            aperture.localScale = new Vector3(1f, 1.37f, 1f);
            if(AttachPortalAnomaly(aperture,1.5f)){portalSpiral=null;return true;}
            Batch spiral = new Batch(batches[Surface.PortalCrystal].Material);
            for (int arm = 0; arm < 4; arm++)
            for (int step = 0; step < 25; step++)
            {
                float angle = arm * Mathf.PI * 0.5f + step * 0.235f;
                float radius = 0.13f + step * 0.055f;
                Vector3 p = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.03f - (step % 3) * 0.009f);
                spiral.Box(p, new Vector3(0.06f + step * 0.0015f, 0.11f + step * 0.003f, 0.035f), Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg));
            }
            portalSpiral = FinishBatch("Original four-arm threshold current", spiral, aperture).transform;
            return false;
        }

        private bool AttachPortalAnomaly(Transform aperture,float radius)
        {
            GameObject art=new GameObject("Original portal anomaly surface");
            art.transform.SetParent(aperture,false);
            art.transform.localRotation=Quaternion.Euler(0,180,0);
            PortalAnomaly anomaly=art.AddComponent<PortalAnomaly>();
            anomaly.Initialize(aperture.position,radius);
            if(anomaly.IsReady)return true;
            Destroy(art);
            return false;
        }

        private static void SealBox(Batch batch, Vector3 p, Vector3 size, float angle)
        {
            batch.Box(p, size, Quaternion.Euler(0, 0, angle));
        }

        private void Obelisk(Vector3 p)
        {
            Cube(Surface.DarkStone, p + new Vector3(0, 0.15f, 0), new Vector3(0.85f, 0.3f, 0.85f));
            Cube(Surface.Stone, p + new Vector3(0, 1.15f, 0), new Vector3(0.51f, 2f, 0.51f));
            Cube(Surface.PaleStone, p + new Vector3(0, 2.2f, 0), new Vector3(0.35f, 0.22f, 0.35f));
            for (int i = 0; i < 5; i++)
            {
                Cube(Surface.Brass, p + new Vector3(-0.08f, 0.6f + i * 0.31f, -0.264f), new Vector3(0.12f, 0.055f, 0.025f));
                Cube(Surface.Azure, p + new Vector3(0.07f, 0.67f + i * 0.31f, -0.266f), new Vector3(0.05f, 0.12f, 0.025f));
            }
        }

        private void DressHall(float bannerWallX = 5.37f)
        {
            // Foreground, middle distance, and the rear chamber each have deliberately placed stories.
            Barrel(new Vector3(-4.65f, 0, -7.9f), 0.55f, 1.26f);
            Barrel(new Vector3(-3.7f, 0, -7.25f), 0.43f, 1.03f);
            Crate(new Vector3(4.45f, 0, -5.9f), new Vector3(1.02f, 0.85f, 0.95f), -8f);
            CandleCluster(new Vector3(4.4f, 0.9f, -5.92f), 5);
            Skull(new Vector3(4.1f, .85f, -5.9f), 0.29f, -25f);
            Banner(-bannerWallX, -8.7f, true, 4.55f);
            Banner(bannerWallX, 1.1f, true, 4.55f);
            Banner(-bannerWallX, 12.2f, true, 4.55f);
            Banner(bannerWallX, 21.0f, true, 4.55f);
            Sarcophagus(new Vector3(-4.35f, 0, 0.8f), 0f);
            CandleCluster(new Vector3(-4.35f, 1.22f, 0.3f), 5);
            Skull(new Vector3(-4.35f, 1.2275f, 1.2f), 0.35f, 25f);
            Campfire(new Vector3(-4.0f, 0, 5.25f));
            Barrel(new Vector3(4.35f, 0, 11.7f), 0.48f, 1.12f);
            Crate(new Vector3(4.55f, 0, 10.6f), new Vector3(0.82f, 0.75f, 0.8f), 4f);
            Crate(new Vector3(-4.35f, 0, 15.1f), new Vector3(0.98f, 0.9f, 0.85f), -4f);
            Crate(new Vector3(-4.43f, 0.92f, 15.1f), new Vector3(0.7f, 0.7f, 0.7f), 4f);
            Bones(new Vector3(-3.9f, 0.03f, 16.0f));
            CandleCluster(new Vector3(4.62f, 0.03f, 21.7f), 7);
            foreach (Vector3 p in new[] { new Vector3(-5.1f, 0, -5.7f), new Vector3(4.8f, 0, 4.9f), new Vector3(-4.8f, 0, 10.8f), new Vector3(4.9f, 0, 14.6f), new Vector3(-4.5f, 0, 20.4f) })
            {
                Rubble(p, 9);
                Fern(p + new Vector3(0.15f, 0.03f, -0.4f), 0.65f);
            }
            // Little pieces remain outside the combat lane and avoid creating trip colliders.
            Bones(new Vector3(3.95f, 0.03f, 6.0f));
            Rubble(new Vector3(-4.9f, 0.02f, -12.0f), 12);
            Rubble(new Vector3(4.9f, 0.02f, 26.8f), 11);
        }

        private void Barrel(Vector3 p, float radius, float height)
        {
            BuildUnionBarrel(p, radius, height);
        }

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing DungeonWorld geometry-root Y-up metres and original barrel args.
// Array Formats: Seven fixed cross-prism records handled by cold adapter, no base format rewrite.
// Dependencies/Pointers: Replace ONLY existing Barrel method; new partial owns cold adapter.
        private void Crate(Vector3 p, Vector3 size, float yaw)
        {
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            Cube(Surface.Wood, p + new Vector3(0, size.y / 2, 0), size, q);
            foreach (float a in new[] { -1f, 1f })
            foreach (float b in new[] { -1f, 1f })
            {
                Cube(Surface.WoodEnd, p + q * new Vector3(a * (size.x / 2f + 0.018f), size.y / 2f, b * (size.z / 2f - 0.055f)), new Vector3(0.13f, size.y + 0.09f, 0.13f), q);
                Cube(Surface.WoodEnd, p + q * new Vector3(0, size.y * (a + 1) / 2f, b * (size.z / 2f + 0.018f)), new Vector3(size.x + 0.08f, 0.13f, 0.10f), q);
                Cube(Surface.Iron, p + q * new Vector3(a * (size.x / 2f - 0.04f), size.y * 0.84f, b * (size.z / 2f + 0.075f)), new Vector3(0.065f, 0.065f, 0.025f), q);
            }
            Cube(Surface.WoodEnd, p + q * new Vector3(0, size.y * 0.5f, -size.z * 0.5f - 0.07f), new Vector3(0.105f, size.y * 1.12f, 0.07f), q * Quaternion.Euler(0, 0, -36f));
            BoxCollider("Supply crate", p + new Vector3(0, size.y * 0.5f, 0), size * 1.03f);
        }

        private void Sarcophagus(Vector3 p, float yaw)
        {
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            Cube(Surface.DarkStone, p + new Vector3(0, 0.16f, 0), new Vector3(1.22f, 0.32f, 2.3f), q);
            Cube(Surface.Stone, p + new Vector3(0, 0.64f, 0), new Vector3(0.98f, 0.76f, 2.08f), q);
            Cube(Surface.PaleStone, p + new Vector3(0, 1.09f, 0), new Vector3(1.15f, 0.18f, 2.27f), q);
            Cube(Surface.DarkStone, p + new Vector3(0, 1.2f, 0), new Vector3(0.74f, 0.055f, 1.8f), q);
            Cube(Surface.Brass, p + new Vector3(0, 1.24f, 0.1f), new Vector3(0.09f, 0.025f, 0.8f), q);
            Cube(Surface.Brass, p + new Vector3(0, 1.24f, -0.02f), new Vector3(0.46f, 0.025f, 0.09f), q);
            BoxCollider("Stone sarcophagus", p + new Vector3(0, 0.6f, 0), new Vector3(1.16f, 1.2f, 2.26f));
        }

                private void Skull(Vector3 p,float scale,float yaw)
        {
            // Supported jaw pivot, original layered cranium and recessed face; local-Z faces forward.
            Quaternion q=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<VoxelSkullRecipe.Count;i++)
            {
                VoxelSkullRecipe.Part part=VoxelSkullRecipe.Get(i);
                Vector3 center=new Vector3(part.X,part.Y,part.Z)*scale;
                Vector3 size=new Vector3(part.Width,part.Height,part.Depth)*scale;
                Cube(part.Recess?Surface.DarkStone:Surface.Bone,p+q*center,size,q);
            }
        }

        private void Bones(Vector3 p,bool stackedSkulls=false)
        {
            // # ARCHITECTURE SNAPSHOT
            // The three existing archive clusters gain one supported, differently faced skull each.
            // Reuse the same two random samples; no new prop location or downstream layout reshuffle.
            float scale=Range(.25f,.37f),yaw=Range(-45,45);
            Vector3 support=p;if(support.y<.1f)support.y=0f;
            Skull(support,scale,yaw);
            if(stackedSkulls)
            {
                float upperScale=scale*.68f,upperYaw=yaw+22f;
                Quaternion lower=Quaternion.Euler(0,yaw,0),upper=Quaternion.Euler(0,upperYaw,0);
                Vector3 crown=support+lower*new Vector3(0,scale,VoxelSkullRecipe.CrownCenterZ*scale);
                Vector3 upperPivot=crown-upper*new Vector3(0,0,VoxelSkullRecipe.JawCenterZ*upperScale);
                Skull(upperPivot,upperScale,upperYaw);
            }
            for (int n = 0; n < 4; n++)
            {
                Vector3 c = p + new Vector3(Range(-0.65f, 0.6f), 0.08f, Range(-0.45f, 0.55f));
                Quaternion q = Quaternion.Euler(0, Range(-180, 180), 0);
                Cube(Surface.Bone, c, new Vector3(0.065f, 0.07f, Range(0.38f, 0.7f)), q);
                Cube(Surface.Bone, c + q * new Vector3(0, 0, 0.2f), new Vector3(0.13f, 0.1f, 0.1f), q);
                Cube(Surface.Bone, c - q * new Vector3(0, 0, 0.2f), new Vector3(0.13f, 0.1f, 0.1f), q);
            }
        }

        private void Banner(float x, float z, bool alongWall, float top, bool violetBanner = false)
        {
            Quaternion q = Quaternion.Euler(0, alongWall ? (x < 0 ? -90f : 90f) : 0f, 0);
            Vector3 p = new Vector3(x, top, z);
            Surface cloth = violetBanner ? Surface.PortalCloth : Surface.Cloth;
            Cube(Surface.Iron, p, new Vector3(1.55f, 0.09f, 0.11f), q);
            Cube(cloth, p + Vector3.down * 1.06f, new Vector3(1.12f, 2.10f, 0.08f), q);
            Cube(cloth, p + Vector3.down * 2.21f, new Vector3(0.73f, 0.25f, 0.08f), q);
            Cube(cloth, p + Vector3.down * 2.40f, new Vector3(0.33f, 0.18f, 0.08f), q);
            foreach (float side in new[] { -1f, 1f })
                Cube(Surface.Brass, p + q * new Vector3(side * 0.47f, -1.04f, -0.049f), new Vector3(0.035f, 2.02f, 0.025f), q);
            // Original stepped diamond/eye device, built in relief rather than a borrowed image.
            foreach (float side in new[] { -1f, 1f })
            for (int n = 0; n < 3; n++)
            {
                Cube(Surface.Brass, p + q * new Vector3(side * (0.13f + n * 0.10f), -0.83f - n * 0.13f, -0.061f), new Vector3(0.13f, 0.08f, 0.035f), q);
                Cube(Surface.Brass, p + q * new Vector3(side * (0.13f + n * 0.10f), -1.35f + n * 0.13f, -0.061f), new Vector3(0.13f, 0.08f, 0.035f), q);
            }
            Cube(Surface.Azure, p + q * new Vector3(0, -1.09f, -0.066f), new Vector3(0.13f, 0.25f, 0.04f), q);
        }

        private void CandleCluster(Vector3 p, int count)
        {
            Cube(Surface.Iron, p + new Vector3(0, 0.025f, 0), new Vector3(0.55f, 0.05f, 0.46f));
            Vector3 flameCenter = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                float x = Range(-0.22f, 0.22f), z = Range(-0.19f, 0.19f), h = Range(0.15f, 0.4f);
                Cube(Surface.Wax, p + new Vector3(x, h * 0.5f + 0.05f, z), new Vector3(0.075f, h, 0.075f));
                flameCenter += p + new Vector3(x, h + .13f, z);
                if (!TryRegisterRefugeCandle(p + new Vector3(x, h + .05f, z), .18f, .027f))
                {
                    Cube(Surface.FlameCore, p + new Vector3(x, h + 0.105f, z), new Vector3(0.047f, 0.11f, 0.047f));
                    Cube(Surface.Flame, p + new Vector3(x + 0.02f, h + 0.16f, z), new Vector3(0.035f, 0.065f, 0.035f));
                }
            }
            if(count > 0) RegisterFireEmitter(flameCenter / count, TutorialFireLightPool.Fixture.CandleCluster, 200000 + Mathf.RoundToInt(p.z * 100f));
        }

        private void Fern(Vector3 p, float size)
        {
            if (TryRegisterRefugeGrass(p, size))
            {
                for (int spoke = 0; spoke < 5; ++spoke) Range(-10, 10); // Preserve the existing cold random stream.
                return;
            }
            Cube(Surface.Soil, p + Vector3.up * 0.015f, new Vector3(size * 0.85f, 0.025f, size * 0.8f));
            for (int spoke = 0; spoke < 5; spoke++)
            {
                float angle = spoke * 72f + Range(-10, 10);
                Quaternion q = Quaternion.Euler(0, angle, 0);
                for (int n = 0; n < 4; n++)
                {
                    float f = n / 3f;
                    Vector3 center = p + q * new Vector3(0, (0.16f + Mathf.Sin(f * 2.4f) * 0.3f) * size, f * 0.46f * size);
                    Cube(n % 2 == 0 ? Surface.Green : Surface.Moss, center, new Vector3((0.25f - f * 0.11f) * size, 0.055f * size, 0.14f * size), q);
                }
            }
        }

        private void Rubble(Vector3 p, int count)
        {
            for (int n = 0; n < count; n++)
            {
                float size = Range(0.1f, 0.31f);
                Cube(random.Next(4) == 0 ? Surface.Moss : Surface.Stone, p + new Vector3(Range(-0.45f, 0.45f), size * 0.2f, Range(-0.65f, 0.65f)), new Vector3(size, size * 0.5f, size * 0.8f), Quaternion.Euler(0, Range(-35f, 35f), 0));
            }
        }

        private void Campfire(Vector3 p)
        {
            Cube(Surface.Soil, p + Vector3.up * 0.025f, new Vector3(1.3f, 0.05f, 1.3f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Cube(Surface.DarkStone, p + new Vector3(Mathf.Cos(a) * 0.62f, 0.14f, Mathf.Sin(a) * 0.62f), new Vector3(0.3f, 0.27f, 0.3f));
            }
            Cube(Surface.Wood, p + new Vector3(0, 0.15f, 0), new Vector3(0.94f, 0.18f, 0.16f), Quaternion.Euler(0, 35f, 0));
            Cube(Surface.Wood, p + new Vector3(0, 0.21f, 0), new Vector3(0.94f, 0.18f, 0.16f), Quaternion.Euler(0, -35f, 0));
            if (!buildingSpatialChunks)
                AttachVoxelFire("Living campfire flame and stepped smoke", p + Vector3.up * .22f, .78f, true);
            else
                for (int i = 0; i < 7; i++)
                    Fire(p + new Vector3(Range(-.24f, .24f), .22f, Range(-.24f, .24f)), Range(.45f, .78f));
        }

        private void Chandelier(float x, float z, float y)
        {
            // The lower middle fixture needs one more link to enter the solid ceiling.
            for (int n = 0; n < (y < 5.8f ? 6 : 5); n++)
                Cube(Surface.Iron, new Vector3(x, y + n * 0.24f, z), n % 2 == 0 ? new Vector3(0.12f, 0.21f, 0.065f) : new Vector3(0.065f, 0.21f, 0.12f));
            Cube(Surface.Iron, new Vector3(x, y, z), new Vector3(1.52f, 0.09f, 0.14f));
            Cube(Surface.Iron, new Vector3(x, y, z), new Vector3(0.14f, 0.09f, 1.52f));
            foreach (Vector3 d in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
            {
                Cube(Surface.Brass, new Vector3(x, y - 0.02f, z) + d * 0.7f, new Vector3(0.23f, 0.09f, 0.23f));
                Cube(Surface.Wax, new Vector3(x, y + 0.14f, z) + d * 0.7f, new Vector3(0.085f, 0.25f, 0.085f));
                Fire(new Vector3(x, y + 0.28f, z) + d * 0.7f, 0.19f, false);
            }
        }

        private void RegisterFireEmitter(Vector3 localPosition, TutorialFireLightPool.Fixture kind, int flickerGroup)
        {
            if(geometryRoot == tutorialRoot && tutorialFireLights != null) tutorialFireLights.RegisterEmitter(localPosition, kind, flickerGroup);
        }

        private void Fire(Vector3 basePoint, float height, bool registerLight = true)
        {
            if(registerLight) RegisterFireEmitter(basePoint + Vector3.up * height * .45f, TutorialFireLightPool.Fixture.SmallFlame, 100000 + Mathf.RoundToInt(basePoint.z * 100f));
            if (height <= .3f && TryRegisterRefugeCandle(basePoint, height, Mathf.Min(.08f, height * .18f))) return;
            Cube(Surface.Flame, basePoint + Vector3.up * height * 0.4f, new Vector3(height * 0.35f, height * 0.75f, height * 0.34f));
            // Expose the hot center on four sides of the opaque outer flame.
            Cube(Surface.FlameCore, basePoint + new Vector3(0f, height * 0.29f, 0f), new Vector3(height * 0.39f, height * 0.48f, height * 0.40f));
            Cube(Surface.Flame, basePoint + new Vector3(height * 0.1f, height * 0.88f, 0), new Vector3(height * 0.16f, height * 0.28f, height * 0.17f));
        }

        private void Torch(float x, float z, float y = 2.65f)
        {
            // # ARCHITECTURE SNAPSHOT
            // Tutorial-only build-time meshes: x is the existing structural pier center, not a wall-tile anchor.
            // Its inset panel front is x+side*.5075. One embedded plate, arm and diagonal brace form continuous iron.
            // Reuse five existing material-batched boxes, one living flame and one registry slot per mount.
            float side = x < 0 ? 1f : -1f;
            float face = x + side * .5075f;
            float shaft = face + side * .53f;
            Cube(Surface.Iron, new Vector3(face + side * .02f, y - .26f, z), new Vector3(.11f, .76f, .36f));
            Cube(Surface.Iron, new Vector3(face + side * .265f, y - .35f, z), new Vector3(.60f, .09f, .10f));
            Vector3 lower = new Vector3(face + side * .02f, y - .57f, z);
            Vector3 upper = new Vector3(shaft, y - .35f, z);
            Vector3 brace = upper - lower;
            Cube(Surface.Iron, (lower + upper) * .5f, new Vector3(.075f, brace.magnitude + .03f, .075f), Quaternion.FromToRotation(Vector3.up, brace));
            Cube(Surface.Wood, new Vector3(shaft, y - .10f, z), new Vector3(.13f, .74f, .13f));
            Cube(Surface.Iron, new Vector3(shaft, y + .21f, z), new Vector3(.28f, .27f, .28f));
            AttachVoxelFire("Living wall torch flame and stepped smoke", new Vector3(shaft, y + .35f, z), .54f, true);
            RegisterFireEmitter(new Vector3(shaft, y + .57f, z), TutorialFireLightPool.Fixture.Torch, Mathf.RoundToInt(z * 100f));
        }

        private void AttachVoxelFire(string label, Vector3 basePoint, float height, bool smoke)
        {
            GameObject go = new GameObject(label);
            go.transform.SetParent(geometryRoot, false);
            go.transform.localPosition = basePoint;
            go.AddComponent<VoxelFireEffect>().Initialize(batches[Surface.Flame].Material, batches[Surface.FlameCore].Material, height, smoke);
        }

        private void BuildLighting()
        {
            foreach (float z in TutorialPierRows)
            {
                Torch(-TutorialPierAnchorX, z, z > 23f ? 3.1f : 2.65f);
                Torch(TutorialPierAnchorX, z, z > 23f ? 3.1f : 2.65f);
            }
            // Reuse existing fixed-light identities for gently breathing warm chandelier centers.
            PointLight("Arrival torch pool", new Vector3(0f, 5.62f, -1.3f), new Color(1f,.62f,.25f), 9f, 18f, false);
            WarmLight("Campfire and crypt torch pool", new Vector3(-3.65f, 2.0f, 5.1f), 10.1f, 12f, true);
            PointLight("Far east torch pool", new Vector3(0f, 5.42f, 10.8f), new Color(1f,.62f,.25f), 9f, 18f, false);
            // Only this third fixture adds a new fixed light; never four lights per chandelier.
            PointLight("Far chandelier centered spill", new Vector3(0f, 5.62f, 21f), new Color(1f,.62f,.25f), 9f, 18f, false);
            // Reuse the sanctum light budget for real violet spill around both crystal pedestals.
            PointLight("Amethyst portal crystal spill", new Vector3(0f, 3.1f, 24.6f), new Color(.62f, .12f, 1f), 6f, 7f, false);
            foreach (float z in alcoves)
                PointLight("Moon through barred window", new Vector3(TutorialWindowLightX, 3.1f, z), new Color(0.19f, 0.58f, 1f), 9.4f, 10.4f, false);
        }

        private void WarmLight(string label, Vector3 p, float range, float intensity, bool shadows)
        {
            PointLight(label, p, new Color(1f, 0.47f, 0.15f), range, intensity, shadows);
        }

        private Light PointLight(string label, Vector3 p, Color color, float range, float intensity, bool shadows)
        {
            GameObject go = new GameObject(label);
            go.transform.SetParent(geometryRoot, false);
            go.transform.localPosition = p;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity * (IsCoopArena ? 1f : .85f);
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier = 0;
            light.shadowBias = 0.04f;
            light.shadowNormalBias = 0.12f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.bounceIntensity = 0f;
            RegisterSpatialLightPulse(light, p);
            return light;
        }

        private void Update()
        {
            if (!built) return;
            float time = Time.time;
            TickRefugeRaykonTalk(time);
            TickRefugeMentorVisibility();
            TickSpatialLightPulse();
            if (!IsCoopArena && portalUnlocked && portalMaterial != null)
            {
                float pulse = SpatialLightPulseRules.Evaluate(SpatialLightPulseKind.Violet, SpatialLightPulseTime, 0f);
                portalMaterial.SetColor("_EmissionColor", new Color(0.62f, 0.025f, 1.4f) * pulse);
            }
            if (!IsCoopArena && portalSpiral != null) portalSpiral.localRotation = Quaternion.Euler(0, 0, -time * (portalUnlocked ? 16f : 7f));
            if(IsCoopArena)foreach(Transform spiral in invasionSpirals)if(spiral!=null&&spiral.gameObject.activeInHierarchy)spiral.localRotation=Quaternion.Euler(0,0,-time*18f);
        }

        private float Range(float min, float max) { return min + (float)random.NextDouble() * (max - min); }
        private void Cube(Surface surface, Vector3 p, Vector3 size) { Cube(surface, p, size, Quaternion.identity); }
        private void Cube(Surface surface, Vector3 p, Vector3 size, Quaternion rotation)
        {
            CaptureVignetteKeepout(p,size,rotation);
            Batch batch=GetGeometryBatch(surface,p,out Vector3 origin);
            batch.Box(p-origin,size,rotation);
        }

        private void TerrainTile(Surface surface,Vector3 p,float size)
        {
            Batch batch=GetGeometryBatch(surface,p,out Vector3 origin);
            batch.Top(p-origin,size,size);
        }

        private void PlaceCrystal(Surface surface,Vector3 p,float radius,float height,Quaternion rotation)
        {
            CaptureVignetteKeepout(p,new Vector3(radius*2,height,radius*2),rotation);
            Batch batch=GetGeometryBatch(surface,p,out Vector3 origin);
            batch.Crystal(p-origin,radius,height,rotation);
        }

        private Batch GetGeometryBatch(Surface surface,Vector3 position,out Vector3 origin)
        {
            if(portalAssemblyBatches!=null)
            {
                origin=Vector3.zero;
                if(!portalAssemblyBatches.TryGetValue(surface,out Batch assembly))
                {assembly=new Batch(batches[surface].Material);portalAssemblyBatches.Add(surface,assembly);}
                return assembly;
            }
            if(!buildingSpatialChunks){origin=Vector3.zero;return batches[surface];}
            ChunkKey key=new ChunkKey(Mathf.FloorToInt(position.x/SpatialChunkSize),Mathf.FloorToInt(position.z/SpatialChunkSize),surface);
            origin=key.Origin;
            if(!spatialBatches.TryGetValue(key,out Batch batch))
            {
                // No per-chunk material table or eager vertex arrays: allocate only a used material bucket.
                batch=new Batch(batches[surface].Material);
                spatialBatches.Add(key,batch);
            }
            return batch;
        }

        private void FinishSpatialChunks()
        {
            ArenaChunkMeshCount=ArenaVertexCount=0;
            ArenaMaximumChunkSpan=0;
            HashSet<Vector2Int> coordinates=new HashSet<Vector2Int>();
            foreach(KeyValuePair<ChunkKey,Batch> pair in spatialBatches)
            {
                if(pair.Value.Vertices.Count==0)continue;
                ChunkKey key=pair.Key;
                coordinates.Add(new Vector2Int(key.X,key.Z));
                ArenaVertexCount+=pair.Value.Vertices.Count;
                bool glow=pair.Value.Material.HasProperty("_EmissionColor")&&pair.Value.Material.GetColor("_EmissionColor").maxColorComponent>0;
                GameObject go=FinishBatch("Arena chunk "+key.X+","+key.Z+" "+key.Surface,pair.Value,geometryRoot,!glow);
                go.transform.localPosition=key.Origin;
                Bounds bounds=go.GetComponent<MeshFilter>().sharedMesh.bounds;
                ArenaMaximumChunkSpan=Mathf.Max(ArenaMaximumChunkSpan,Mathf.Max(bounds.size.x,bounds.size.z));
                ArenaChunkMeshCount++;
            }
            ArenaSpatialChunkCount=coordinates.Count;
            // These objects contain renderers only. Unity's renderer frustum culling cannot disable
            // the continuous foundation or the separate perimeter/tree/ruin physics colliders.
        }

        private void BoxCollider(string label, Vector3 center, Vector3 size)
        { BoxCollider(label, center, size, Quaternion.identity); }

        private void BoxCollider(string label, Vector3 center, Vector3 size, Quaternion rotation)
        {
            if(size.x<10f&&size.z<10f&&center.y+size.y*.5f>1f)CaptureVignetteKeepout(center,size,rotation);
            GameObject go = new GameObject(label);
            go.transform.SetParent(geometryRoot, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            UnityEngine.BoxCollider collider = go.AddComponent<UnityEngine.BoxCollider>();
            collider.size = size;
        }

        private void FinishTutorialBatch(Surface surface,Batch batch)
        {
            // # ARCHITECTURE SNAPSHOT
            // Clip only non-emissive Lit tutorial surfaces into true4m XZ renderer bounds.
            // This avoids room-wide per-object light selection in the supported Forward fallback.
            // Existing physics objects, all arena batches, shared materials and authored UVs stay owned here.
            bool backdrop=surface==Surface.Sky||surface==Surface.Cloud||surface==Surface.Moon;
            bool lit=batch.Material.shader!=null && batch.Material.shader.name=="Universal Render Pipeline/Lit";
            bool emissive=batch.Material.HasProperty("_EmissionColor") && batch.Material.GetColor("_EmissionColor").maxColorComponent>0;
            if(lit && !emissive && !backdrop) {
                TutorialRenderPartition.Build(surface.ToString(),batch.Material,geometryRoot,resources,true,
                    batch.Vertices,batch.Normals,batch.UVs,batch.Triangles);
                batch.Release();
            } else FinishBatch(surface.ToString(),batch,geometryRoot,!backdrop && !(tutorialRefuge!=null && emissive));
        }

        private GameObject FinishBatch(string label, Batch batch, Transform parent, bool castShadows = true)
        {
            GameObject go = new GameObject(label + " • combined voxel mesh");
            go.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = label + " combined geometry", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(batch.Vertices);
            mesh.SetNormals(batch.Normals);
            mesh.SetUVs(0, batch.UVs);
            mesh.SetTriangles(batch.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            resources.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.Material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            batch.Release();
            return go;
        }

        private void OnDestroy()
        {
            DisposeRefugeDressing();
            if(RenderSettings.skybox==nightSkybox || tutorialSkybox!=null && RenderSettings.skybox==tutorialSkybox)RenderSettings.skybox=previousSkybox;
            foreach (UnityEngine.Object resource in resources)
                if (resource != null) Destroy(resource);
        }

        private readonly struct ChunkKey : IEquatable<ChunkKey>
        {
            public readonly int X,Z;
            public readonly Surface Surface;
            public Vector3 Origin => new Vector3(X*SpatialChunkSize,0,Z*SpatialChunkSize);
            public ChunkKey(int x,int z,Surface surface){X=x;Z=z;Surface=surface;}
            public bool Equals(ChunkKey other){return X==other.X&&Z==other.Z&&Surface==other.Surface;}
            public override bool Equals(object obj){return obj is ChunkKey other&&Equals(other);}
            public override int GetHashCode(){unchecked{return X*73856093^Z*19349663^(int)Surface*83492791;}}
        }

        private sealed class Batch
        {
            public readonly Material Material;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> UVs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
            public Batch(Material material) { Material = material; }

            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool doubleSided=false)
            {
                int start=Vertices.Count;
                Face(Vector3.zero,Quaternion.identity,Vector3.Cross(b-a,c-a).normalized,a,b,c,d,(b-a).magnitude,(d-a).magnitude);
                if(doubleSided)
                {
                    Triangles.Add(start);Triangles.Add(start+2);Triangles.Add(start+1);
                    Triangles.Add(start);Triangles.Add(start+3);Triangles.Add(start+2);
                }
            }

            public void Top(Vector3 p,float width,float depth)
            {
                float x=width*.5f,z=depth*.5f;
                Face(p,Quaternion.identity,Vector3.up,new Vector3(-x,0,z),new Vector3(x,0,z),new Vector3(x,0,-z),new Vector3(-x,0,-z),width,depth);
            }

            public void Crystal(Vector3 p, float radius, float height, Quaternion rotation)
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f, b = (i + 1) * Mathf.PI / 3f;
                    Vector3 loA = new Vector3(Mathf.Cos(a) * radius, -height * 0.22f, Mathf.Sin(a) * radius);
                    Vector3 loB = new Vector3(Mathf.Cos(b) * radius, -height * 0.22f, Mathf.Sin(b) * radius);
                    Vector3 hiA = new Vector3(loA.x, height * 0.22f, loA.z);
                    Vector3 hiB = new Vector3(loB.x, height * 0.22f, loB.z);
                    Triangle(p, rotation, loA, hiA, hiB);
                    Triangle(p, rotation, loA, hiB, loB);
                    Triangle(p, rotation, hiA, Vector3.up * height * 0.5f, hiB);
                    Triangle(p, rotation, loB, Vector3.down * height * 0.5f, loA);
                }
            }

            private void Triangle(Vector3 p, Quaternion q, Vector3 a, Vector3 b, Vector3 c)
            {
                int start = Vertices.Count;
                Vector3 normal = q * Vector3.Cross(b - a, c - a).normalized;
                Vertices.Add(p + q * a); Vertices.Add(p + q * b); Vertices.Add(p + q * c);
                Normals.Add(normal); Normals.Add(normal); Normals.Add(normal);
                UVs.Add(Vector2.zero); UVs.Add(Vector2.up); UVs.Add(Vector2.one);
                Triangles.Add(start); Triangles.Add(start + 1); Triangles.Add(start + 2);
            }

            public void Box(Vector3 p, Vector3 size, Quaternion rotation)
            {
                Vector3 h = size * 0.5f;
                Face(p, rotation, Vector3.forward, new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), size.x, size.y);
                Face(p, rotation, Vector3.back, new Vector3(h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), size.x, size.y);
                Face(p, rotation, Vector3.right, new Vector3(h.x, -h.y, h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), size.z, size.y);
                Face(p, rotation, Vector3.left, new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), size.z, size.y);
                Face(p, rotation, Vector3.up, new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z), size.x, size.z);
                Face(p, rotation, Vector3.down, new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), size.x, size.z);
            }

            private void Face(Vector3 p, Quaternion q, Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float width, float height)
            {
                int n = Vertices.Count;
                Vertices.Add(p + q * a); Vertices.Add(p + q * b); Vertices.Add(p + q * c); Vertices.Add(p + q * d);
                Vector3 nn = q * normal;
                Normals.Add(nn); Normals.Add(nn); Normals.Add(nn); Normals.Add(nn);
                float u = width / 0.8f, v = height / 0.8f;
                UVs.Add(Vector2.zero); UVs.Add(new Vector2(u, 0)); UVs.Add(new Vector2(u, v)); UVs.Add(new Vector2(0, v));
                // Unity front faces wind clockwise from outside when using these outward normals.
                Triangles.Add(n); Triangles.Add(n + 1); Triangles.Add(n + 2);
                Triangles.Add(n); Triangles.Add(n + 2); Triangles.Add(n + 3);
            }

            public void Release()
            {
                Vertices.Clear(); Vertices.TrimExcess(); Normals.Clear(); Normals.TrimExcess();
                UVs.Clear(); UVs.TrimExcess(); Triangles.Clear(); Triangles.TrimExcess();
            }
        }
    }
}
