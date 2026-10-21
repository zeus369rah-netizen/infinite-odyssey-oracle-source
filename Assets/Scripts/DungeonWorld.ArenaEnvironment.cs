using System;
using System.Collections.Generic;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    //007 source candidate: World owns metres, floor/perimeter/barriers and four shared raised bastions.
    // Host owns stable-slot admissions and drain; eight cosmetic vistas grant no admission.
    // Cold pure recipes feed existing10m shared-material chunks. New canopy/lava/steam have3 shared materials.
    //16 trunk plus8 nonwalkable landmark proxies; no canopy physics, per-tree Update or new hazards.
    public sealed partial class DungeonWorld
    {
        private const float ArenaRingRadius=70f,ArenaLaneWidth=5.6f;
        private readonly List<VignetteKeepout> vignetteKeepouts=new List<VignetteKeepout>(1024);
        private VignetteKeepout[] vignetteKeepoutSnapshot;
        private bool captureVignetteKeepouts,vignetteKeepoutOverflow;
        private LaneVignetteRenderer laneVignetteRenderer;
        public LaneVignetteBuildResult VignetteBuildResult { get; private set; }
        // Primary roads E/N/W/S=Fire/Earth/Water/Air; diagonals retain companion landscapes.
        private static int ArenaElementAppearance(int lane)=>lane==0?0:lane==1?7:lane==2?6:lane==3?5:lane==4?3:lane==5?4:lane==6?2:1;

        private void BuildArenaGround()
        {
            // The floor is physically continuous. Water is a shallow turquoise floor treatment, not a pit.
            var dryGround015=PrepareDryBuildPockets015();
            try
            {
            BoxCollider("Continuous arena foundation",new Vector3(0,-.35f,0),new Vector3((ArenaRadius + 1f) * 2f, .7f, (ArenaRadius + 1f) * 2f));
            for(int z=-(int)ArenaRadius;z<=(int)ArenaRadius;z++)
            for(int x=-(int)ArenaRadius;x<=(int)ArenaRadius;x++)
            {
                float radius=Mathf.Sqrt(x*x+z*z);
                if(radius>ArenaRadius+.2f)continue;
                int sector=LaneThemeAt(new Vector3(x,0,z));
                Surface s=Surface.Moss;
                switch(sector)
                {
                    case 0:s=random.Next(5)==0?Surface.Autumn:Surface.Ember;break;
                    case 1:s=random.Next(4)==0?Surface.Soil:Surface.Ember;break;
                    case 2:s=Surface.Moss;break;
                    case 3:s=random.Next(4)==0?Surface.Moss:Surface.DarkStone;break;
                    case 4:s=random.Next(5)==0?Surface.Sand:Surface.Water;break;
                    case 5:s=random.Next(4)==0?Surface.Water:Surface.Snow;break;
                    case 6:s=Surface.PaleStone;break;
                    case 7:s=random.Next(4)==0?Surface.Azure:Surface.PaleStone;break;
                }
                if(radius<8.5f||radius>ArenaRingRadius-2.5f)s=Surface.Floor;
                s=SelectDryBuildPocketSurface015(s,x,z,sector,in dryGround015);
                TerrainTile(s,new Vector3(x,0,z),1f);
                RecordLaneGround015(x,z,s==Surface.Water);
            }
            CompleteDryBuildPocketGround015(in dryGround015);
            }
            finally {dryGround015.Dispose();}
            ArenaRing(Surface.DarkStone,ArenaRadius-.7f,.35f,.9f,.7f,256);
            ArenaRing(Surface.PaleStone,ArenaRadius-.7f,.79f,1.0f,.18f,256);
            ArenaRing(Surface.Brass,ArenaRadius-.7f,.905f,.08f,.055f,256);
            // Continuous visible stone/iron parapets, with matching solid collision, keep actors inside the disc.
            for(int i=0;i<192;i++)
            {
                float a=i*360f/192f;
                Quaternion q=Quaternion.Euler(0,a,0);
                Vector3 p=q*new Vector3(0,0,ArenaRadius-.6f);
                BoxCollider("Arena perimeter",p+Vector3.up*1.55f,new Vector3(2.65f,3.1f,.7f),q);
                Cube(Surface.Iron,p+Vector3.up*1.48f,new Vector3(2.6f,.085f,.08f),q);
                for(int bar=-1;bar<=1;bar++)
                {
                    Vector3 b=p+q*new Vector3(bar*.80f,1.75f,0);
                    Cube(Surface.Iron,b,new Vector3(.08f,1.7f,.08f),q);
                    Cube(Surface.Brass,b+Vector3.up*.87f,new Vector3(.16f,.18f,.16f),q);
                }
                if(i%4==0)
                {
                    Cube(Surface.Stone,p+Vector3.up*1.1f,new Vector3(.75f,2.2f,.8f),q);
                    Cube(Surface.PaleStone,p+Vector3.up*2.3f,new Vector3(.9f,.25f,.95f),q);
                }
            }
        }

        private void BuildArenaPaths()
        {
            ArenaRing(Surface.Stone, ArenaRingRadius, .018f, 4.4f, .075f, 320);
            ArenaRing(Surface.Brass, ArenaRingRadius-2.2f, .065f, .06f, .03f, 320);
            ArenaRing(Surface.PaleStone, ArenaRingRadius+2.24f, .07f, .12f, .08f, 320);
            ArenaRing(Surface.DarkStone, 3.25f, .06f, 1.2f, .12f, 48);
            ArenaRing(Surface.Brass, 3.86f, .11f, .07f, .05f, 64);
            ArenaRing(Surface.RuneGreen, 2.72f, .135f, .055f, .035f, 64);
            ArenaRing(Surface.PaleStone, 7.9f, .018f, 1.1f, .06f, 80);
            for (int lane = 0; lane < 8; lane++)
            {
                Vector3 direction = LaneDirection(lane), tangent = new Vector3(-direction.z,0,direction.x);
                Quaternion facing = Quaternion.LookRotation(direction,Vector3.up);
                Vector3 end = direction * ArenaPortalRadius;
                ArenaPath(direction*7.8f, direction*ArenaRingRadius, ArenaLaneWidth, .055f);
                for (int radius = 10; radius < 69; radius += 4)
                {
                    Vector3 p = direction*radius+Vector3.up*.12f;
                    Cube(Surface.Brass,p,new Vector3(.43f,.04f,.43f),facing);
                    Cube(Surface.RuneGreen,p+Vector3.up*.025f,new Vector3(.19f,.025f,.19f),facing);
                }
                ArenaRingAt(Surface.PaleStone,end,3.7f,.032f,.95f,.095f,48);
                ArenaRingAt(Surface.Brass,end,4.17f,.10f,.055f,.035f,56);
                ElementFloorDevice(end+Vector3.up*.12f,ArenaElementAppearance(lane));
                // Gate rear and chest sit off the uninterrupted portal-to-shrine center line.
                if(lane==0||lane==4)
                {
                    ArenaChest(end+tangent*5.2f+direction*.35f,90f-lane*45f);
                    Vector3 banner=end-tangent*5.2f+direction*.6f;
                    Banner(banner.x,banner.z,false,4.1f,lane==0);
                }
            }
            for(int pad=0;pad<ElementalTowerPlatforms.PadCount;pad++)
            for(int side=0;side<2;side++)
            {
                Vector3 top=TowerBuildPads[pad];top.y=0;
                ArenaPath(ElementalTowerPlatforms.ConnectorAnchor(pad,side),top,1.6f,.035f);
            }
            Vector3[] corners = { new Vector3(-ArenaPortalRadius,0,0),new Vector3(0,0,ArenaPortalRadius),
                new Vector3(ArenaPortalRadius,0,0),new Vector3(0,0,-ArenaPortalRadius) };
            for(int i=0;i<4;i++)ArenaPath(corners[i],corners[(i+1)%4],3.0f,.025f);
        }

        private void ArenaPath(Vector3 from,Vector3 to,float width,float y)
        {
            // At most8m per road piece: bounded spatial envelopes, shared repeating textures,
            // and no buried half-metre box tessellation. Physics stays on the separate foundation.
            Vector3 delta=to-from;
            int count=Mathf.CeilToInt(delta.magnitude/8f);
            Quaternion q=Quaternion.LookRotation(delta.normalized,Vector3.up);
            for(int i=0;i<count;i++)
            {
                Vector3 p=Vector3.Lerp(from,to,(i+.5f)/count)+Vector3.up*y;
                Cube(i%4==0?Surface.PaleStone:Surface.Floor,p,new Vector3(width,.07f,delta.magnitude/count),q);
                foreach(float side in new[]{-1f,1f})
                    Cube(Surface.DarkStone,p+q*new Vector3(side*(width*.5f+.08f),.025f,0),new Vector3(.13f,.08f,delta.magnitude/count),q);
            }
        }

        private void BuildArenaBiomes()
        {
            // Replace the old80-cluster decorations; the bounded reusable recipe owns the new vistas.
            // Geometry and its four retained lights share the same footprint-checked anchors.
            for(int index=0;index<4;index++)
            {
                Vector3 p=ArenaCampfirePosition020(index);
                if(!ArenaPositionReserved(p,ArenaCampfireRadius020))Campfire(p);
            }
        }

        private const float ArenaCampfireRadius020=1.25f;
        private static Vector3 ArenaCampfirePosition020(int index)
        {
            if(index<0||index>=4)throw new ArgumentOutOfRangeException(nameof(index));
            return new Vector3(index<2?-43.5f:43.5f,0,(index&1)==0?-36f:36f);
        }

        private bool ArenaPositionReserved(Vector3 p,float radius=0f)
        {
            // Cold whole-footprint guard: reserve every future-open road, independent of active masks.
            if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.z)||float.IsInfinity(p.z)||
                float.IsNaN(radius)||float.IsInfinity(radius)||radius<0f)return true;
            foreach(Vector3 pad in TowerBuildPads){Vector3 d=p-pad;d.y=0;float gap=3f+radius;if(d.sqrMagnitude<gap*gap)return true;}
            float inner=10f+radius,outer=ArenaRingRadius-2.1f-radius,planar=p.x*p.x+p.z*p.z;
            if(outer<=inner||planar<inner*inner||planar>outer*outer)return true;
            for(int lane=0;lane<8;lane++)
            {
                Vector3 direction=LaneDirection(lane),tangent=new Vector3(-direction.z,0,direction.x);
                if(Vector3.Dot(p,direction)>=-radius&&Mathf.Abs(Vector3.Dot(p,tangent))<ArenaLaneWidth*.5f+1.1f+radius)return true;
                Vector3 d=p-direction*ArenaPortalRadius;d.y=0;float gap=5f+radius;
                if(d.sqrMagnitude<gap*gap)return true;
            }
            return Mathf.Abs(Mathf.Abs(p.x)+Mathf.Abs(p.z)-ArenaPortalRadius)*.7071068f<2.4f+radius;
        }

        // # ARCHITECTURE SNAPSHOT
        // Existing World retains floor/routes/spawns/pads/non-tree collisions and resource lifetime.
        // Pure planning completes before any added geometry/collider is committed.
        // Curated trees/vistas replace the old cluster dressing; four fire fixtures stay explicit.
        public LaneVignetteLayout CreateCurrentVignetteLayout()
        {
            if(!IsCoopArena || vignetteKeepoutOverflow || vignetteKeepoutSnapshot==null)throw new InvalidOperationException("Vignette keepout capture unavailable.");
            var pads=new VignettePoint[TowerBuildPads.Length];
            var spawns=new VignettePoint[EnemySpawns.Length];
            for(int i=0;i<pads.Length;i++)pads[i]=new VignettePoint(TowerBuildPads[i].x,TowerBuildPads[i].y,TowerBuildPads[i].z);
            for(int i=0;i<spawns.Length;i++)spawns[i]=new VignettePoint(EnemySpawns[i].x,EnemySpawns[i].y,EnemySpawns[i].z);
            return new LaneVignetteLayout(ArenaRadius,ArenaPortalRadius,ArenaRingRadius,ArenaLaneWidth*.5,
                pads,spawns,vignetteKeepoutSnapshot);
        }
        private void CaptureVignetteKeepout(Vector3 p,Vector3 size,Quaternion q)
        {
            if(!captureVignetteKeepouts||!buildingSpatialChunks||size.y<.25f)return;
            if(vignetteKeepouts.Count>=4096){vignetteKeepoutOverflow=true;return;}
            Vector3 x=q*(Vector3.right*size.x*.5f),y=q*(Vector3.up*size.y*.5f),z=q*(Vector3.forward*size.z*.5f);
            float ex=Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),ez=Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z);
            vignetteKeepouts.Add(new VignetteKeepout(p.x,p.z,Mathf.Sqrt(ex*ex+ez*ez)));
        }
        private void BuildCurrentLaneVignettes()
        {
            vignetteKeepoutSnapshot=vignetteKeepouts.ToArray();
            var pieces=new List<LaneVignettePiece>(1400);
            var proxies=new List<VignetteTrunkProxy>(16);
            var trees=new List<OdysseyOracle.Foundation.World.BlossomTreePlan48>(16);
            VignetteBuildResult=LaneVignetteRecipes.Build(CreateCurrentVignetteLayout(),pieces.Add,proxies.Add,trees.Add);
            // Complete/validate all native geometry and retained budgets before publishing any new mesh/proxy.
            using(var treeBuild=LaneBlossomTreeBuild.Build(trees,VignetteBuildResult))
            {
                GameObject root=new GameObject("Curated elemental canopy weather");root.transform.SetParent(geometryRoot,false);
                laneVignetteRenderer=root.AddComponent<LaneVignetteRenderer>();laneVignetteRenderer.Initialize();
                foreach(LaneVignettePiece piece in pieces)AppendLaneVignette(piece);
                AppendRefinedArenaTrees(treeBuild);
                foreach(VignetteTrunkProxy proxy in proxies)
                    BoxCollider(proxy.Kind==VignetteProxyKind.Trunk?"Curated elemental tree trunk":"Nonwalkable elemental landmark",new Vector3((float)proxy.Center.X,(float)proxy.Center.Y,(float)proxy.Center.Z),
                        new Vector3((float)proxy.Size.X,(float)proxy.Size.Y,(float)proxy.Size.Z),Quaternion.Euler(0,(float)proxy.YawDegrees,0));
                laneVignetteRenderer.Finish();
            }
            // Exactly two real, shadow-free lights for the exterior lava caldera. No per-cube light.
            Vector3 direction=LaneDirection(1),tangent=new Vector3(-direction.z,0,direction.x);
            PointLight("Lava caldera primary actual spill",direction*(ArenaRadius+6f)+Vector3.up*10f,
                new Color(1f,.23f,.035f),19f,6.8f,false);
            PointLight("Lava caldera secondary actual spill",direction*(ArenaRadius+2f)-tangent*12.5f+Vector3.up*7f,
                new Color(1f,.38f,.065f),13f,2.975f,false);
        }
        private void AppendLaneVignette(LaneVignettePiece piece)
        {
            Vector3 p=new Vector3((float)piece.Center.X,(float)piece.Center.Y,(float)piece.Center.Z);
            Vector3 size=new Vector3((float)piece.Size.X,(float)piece.Size.Y,(float)piece.Size.Z);
            Quaternion q=Quaternion.Euler(0,(float)piece.YawDegrees,0);
            if(piece.Primitive==VignettePrimitive.LavaTop)RecordLaneStamp015(p,size,q,OdysseyOracle.Foundation.World.LaneBuildMath015.Lava);
            else if(piece.Surface==VignetteSurface.Water&&piece.Primitive!=VignettePrimitive.CanopyBox&&piece.Center.Y<=.25)
                RecordLaneStamp015(p,size,q,OdysseyOracle.Foundation.World.LaneBuildMath015.Water);
            bool weather=piece.Primitive==VignettePrimitive.CanopyBox||piece.Primitive==VignettePrimitive.LavaTop||piece.Primitive==VignettePrimitive.SteamQuad;
            if(weather&&laneVignetteRenderer.ShaderReady){laneVignetteRenderer.Append(piece);return;}
            if(piece.Primitive==VignettePrimitive.SteamQuad)return; // graceful missing-shader fallback: no opaque smoke cards.
            Surface surface=VignetteWorldSurface(piece.Surface);
            if(piece.Primitive==VignettePrimitive.CanopyBox)
            {
                int theme=ArenaElementAppearance(piece.Lane);
                surface=theme==0?Surface.Autumn:theme==7?Surface.Ember:theme==6?Surface.Green:
                    theme==5?Surface.PortalCrystal:theme==3?Surface.Water:theme==4?Surface.Snow:theme==2?Surface.PaleStone:Surface.Azure;
            }
            if(piece.Primitive==VignettePrimitive.Crystal){PlaceCrystal(surface,p,size.x,size.y,q);return;}
            if(piece.Primitive!=VignettePrimitive.LavaTop){Cube(surface,p,size,q);return;}
            Batch batch=GetGeometryBatch(Surface.Flame,p,out Vector3 origin);Vector3 h=size*.5f;
            batch.Quad(p-origin+q*new Vector3(-h.x,0,h.z),p-origin+q*new Vector3(h.x,0,h.z),
                p-origin+q*new Vector3(h.x,0,-h.z),p-origin+q*new Vector3(-h.x,0,-h.z));
        }
        private static Surface VignetteWorldSurface(VignetteSurface surface)
        {
            switch(surface)
            {
                case VignetteSurface.Stone:return Surface.Stone;
                case VignetteSurface.PaleStone:return Surface.PaleStone;
                case VignetteSurface.DarkStone:return Surface.DarkStone;
                case VignetteSurface.Moss:return Surface.Moss;
                case VignetteSurface.Wood:return Surface.Wood;
                case VignetteSurface.WoodEnd:return Surface.WoodEnd;
                case VignetteSurface.Iron:return Surface.Iron;
                case VignetteSurface.Brass:return Surface.Brass;
                case VignetteSurface.Soil:return Surface.Soil;
                case VignetteSurface.Green:return Surface.Green;
                case VignetteSurface.RuneGreen:return Surface.RuneGreen;
                case VignetteSurface.Sand:return Surface.Sand;
                case VignetteSurface.Snow:return Surface.Snow;
                case VignetteSurface.Ember:return Surface.Ember;
                case VignetteSurface.Water:return Surface.Water;
                case VignetteSurface.Azure:return Surface.Azure;
                case VignetteSurface.PortalCrystal:return Surface.PortalCrystal;
                case VignetteSurface.Blossom:return Surface.Blossom;
                case VignetteSurface.BlossomPale:return Surface.BlossomPale;
                case VignetteSurface.Pine:return Surface.Pine;
                case VignetteSurface.Autumn:return Surface.Autumn;
                case VignetteSurface.Flame:return Surface.Flame;
                case VignetteSurface.FlameCore:return Surface.FlameCore;
                case VignetteSurface.Cloud:return Surface.Cloud;
                default:throw new ArgumentOutOfRangeException(nameof(surface));
            }
        }

    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing authoritative arena root/floor/routes/pads/spawns; original16tree roles use wider declared reservations before seed submission.
// Array Formats: cold16blittable seeds ->disjoint Burst occupancy/greedy outputs ->existing shared10m batches; using lease completes/disposes TempJob slabs before return.
// Dependencies/Pointers: existing World finalgeometry/collider/material owner and oldweather fallback preserved. Triangle/cell/bounds errors reject before publishing refined geometry. No runtime randomstream consumption/extraactors/newlights.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up/XZ, exact1m terrain/.25m build roots and locked active-road context.
// Array Formats: Bounded native semantic/preview/body stores, exact64B versioned command and existing2peer/4tower snapshots.
// Dependencies/Pointers: Root integrates exact guarded slices; host economy/readiness/LOS and World geometry remain canonical, prepared/source compile is not native or browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world-XZ1m ground atY0; quarter-root dry support union, unchanged raised roads/curbs and actual water/lava vignette faces.
// Array Formats: Cold bounded1024-word Morton mask/18 diagnostics leased only during ground construction; one final Surface feeds one top and its semantic record.
// Dependencies/Pointers: World retains geometry, random stream, terrain/stamp hashes and reservation ownership. Complete reader before finally disposal; no placement authority or active-frame work added.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing arena world metres/Y-up/XZ; eight radial lane strips and four signed brazier identities.
// Array Formats: Four cold scalar anchors, one conservative footprint radius; existing material/mesh/light stores unchanged.
// Dependencies/Pointers: World geometry and brazier lights consume the same guarded anchor; no admission/geometry-mask, actor, reward, protocol or runtime pool owner changes. Native/browser acceptance pending.
