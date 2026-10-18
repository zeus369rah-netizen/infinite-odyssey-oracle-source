#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace OdysseyOracle.Diagnostics
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit root-run observation of composed006+005, not007/eight-player geometry.
    // Public mesh/collider/light metadata and bounded Physics queries only. Never
    // builds, moves actors/camera, synchronizes transforms, changes settings/lights,
    // creates fixtures/assets, writes profiles or runs network/gameplay commands.
    public static class Release006ArenaChecks
    {
        const string Frozen006="5c97dc8bbf74f44cf6093814e4d431af9ccf70b13fa734105fc46e7f539086ea";
        const string Frozen006World="5ded40b442928910c26ccaa7cdf0e887cbaa226b7616a2ec90ca23e77636e853";
        static readonly Vector3[] Pads={new Vector3(-14,.36f,4.8f),new Vector3(-22,.36f,-4.8f),new Vector3(14,.36f,-4.8f),new Vector3(22,.36f,4.8f)};
        [Serializable] public sealed class Check {public string name,status,detail;}
        [Serializable] public sealed class LightRecord
        {public string name,type,shadows;public bool enabled,active;public Vector3 position;public float range,intensity;public string entityId;}
        [Serializable] public sealed class MeshRecord
        {public string name;public string entityId;public int vertices,submeshes;public long triangles;public bool readable;public Vector3 boundsCenter,boundsSize;}
        [Serializable] public sealed class Receipt
        {
            public string status="NOT_RUN",unityVersion,contract006Freeze=Frozen006,contract006World=Frozen006World,actualWorldSourceHash,actualArenaPartialHash;
            public bool nativeExecuted,sceneMutations,settingsWrites,profileWrites,worldBuildInvoked;
            public string limitations="Read-only instantaneous environment queries. No walking, live enemy admission, flying-fin traversal, host placement/Smith transactions, renderer pixel proof, FPS or browser acceptance.";
            public int assertions,failures,physicsQueries,querySaturations,foundationSamples,fenceRays,fenceCapsules,roadCapsules,gateCapsules,padApproaches,smithApproaches;
            public int treeProxies,landmarkProxies,fenceColliders,courseColliders,totalBoxes,barriers,blockedBarriers,activeGateFrames;
            public int recipePieces,recipeTriangles,renderers,activeRenderers,uniqueMeshes,uniqueMaterials,uniqueShaders,mainChunks,spatialChunks,worldVertices;
            public int pointLightsCreated,pointLightsEnabled,worldRealtimeShadowPoints,sceneShadowDirectionals,sceneShadowLocalLights;
            public long indexedTriangles,totalVertices;public float maximumChunkSpan,maximumRendererSpan,maximumRendererRadius,maximumRendererTop;
            public bool fieldSmithPresent;public string weatherShaderStatus,sourcePinMeaning="Contract inputs are pinned; actual merged source hashes are observations, not an exact frozen-file identity claim.";
            public List<Check> checks=new List<Check>(2200);public List<LightRecord> lights=new List<LightRecord>(12);public List<MeshRecord> meshes=new List<MeshRecord>(1800);
        }
        sealed class Query
        {internal readonly RaycastHit[] Hits=new RaycastHit[64];internal readonly Collider[] Overlaps=new Collider[64];internal Transform Arena;internal Receipt Report;}

        public static string Run(GameSession session)
        {
            var r=new Receipt{unityVersion=Application.unityVersion};
            if(!Application.isPlaying||session==null||session.World==null||!session.World.IsCoopArena||session.World.TowerPlatforms==null||
                session.World.VignetteBuildResult==null||session.World.ActiveLaneCount!=2||
                Mathf.Abs(session.World.MapBounds.width-153f)>.001f)
            {r.checks.Add(new Check{name="preflight",status="BLOCKED",detail="Root must construct006 (R76/P64) with005 four pads and configure TWO active lanes, then allow normal physics synchronization/frame. This fixture does not set up or mutate the World."});return JsonUtility.ToJson(r,true);}
            Transform arena=null;foreach(Transform child in session.World.transform)if(child.name=="Cooperative elemental wheel environment"){arena=child;break;}
            if(arena==null||!arena.gameObject.activeInHierarchy)
            {r.checks.Add(new Check{name="preflight",status="BLOCKED",detail="Actual active006 arena root was not found."});return JsonUtility.ToJson(r,true);}
            var q=new Query{Arena=arena,Report=r};r.nativeExecuted=true;
            r.actualWorldSourceHash=FileHash(Path.Combine(Application.dataPath,"Scripts/DungeonWorld.cs"));
            r.actualArenaPartialHash=FileHash(Path.Combine(Application.dataPath,"Scripts/DungeonWorld.ArenaEnvironment.cs"));
            try
            {
                Verify(r,arena.position.sqrMagnitude<.000001f&&Quaternion.Angle(arena.rotation,Quaternion.identity)<.001f&&
                    (arena.lossyScale-Vector3.one).sqrMagnitude<.000001f,"identity_metre_root","Current authored coordinates require the existing identity World/arena transform.");
                CheckRecipes(session.World,r);
                BoxCollider foundation=InventoryColliders(session.World,q);
                if(foundation!=null)CheckFoundation(foundation,q);
                CheckFence(q);CheckRoads(q);CheckPads(session.World,q);CheckSmith(session,q);CheckGates(q);
                CheckLighting(session.World,arena,r);CheckMeshes(session.World,arena,r);
                r.status=r.failures==0?"PASS_READ_ONLY_ENVIRONMENT":"FAIL";
            }
            catch(Exception error)
            {r.status="ERROR";r.failures++;r.checks.Add(new Check{name="exception",status="FAIL",detail=error.ToString()});}
            return JsonUtility.ToJson(r,true);
        }
        static void CheckRecipes(DungeonWorld world,Receipt r)
        {
            var result=world.VignetteBuildResult;r.recipePieces=result.Pieces;r.recipeTriangles=result.TotalTriangles;
            Verify(r,result.TrunkProxies==16&&result.SceneryProxies==8,"recipe_proxy_counts","Actual build result:16 trees +8 nonwalkable landmarks.");
            Verify(r,result.Pieces>0&&result.Pieces<=1400&&result.TotalTriangles<=19200,"bounded_recipe","Build-time result is measured, not assumed identical to a pure planning fixture.");
            for(int lane=0;lane<8;lane++)Verify(r,result.Trees[lane]==2&&result.Buildings[lane]==1&&result.Triangles[lane]<=2400,
                "lane_vignette_"+lane,"Two trees/one landmark and bounded recipe triangles per visible road, independent of admissions.");
            var weather=world.GetComponentInChildren<LaneVignetteRenderer>();
            Verify(r,weather!=null,"weather_owner","Actual006 shared-mesh weather owner exists.");
            if(weather!=null)
            {
                r.weatherShaderStatus=weather.ShaderReady?"SUPPORTED":"STATIC_FALLBACK";
                Verify(r,weather.MeshCount<=192&&weather.MaximumChunkSpan<=26f,"weather_bounds","Bounded spatial weather mesh envelopes; fallback is reported separately.");
                if(!weather.ShaderReady)r.checks.Add(new Check{name="weather_shader_visuals",status="UNVISITED_FALLBACK",detail="Static existing-palette fallback is active; animated canopy/lava/steam appearance is not accepted by this check."});
            }
        }
        static BoxCollider InventoryColliders(DungeonWorld world,Query q)
        {
            var r=q.Report;BoxCollider foundation=null;int foundations=0;
            foreach(var c in q.Arena.GetComponentsInChildren<BoxCollider>(true))
            {
                r.totalBoxes++;
                if(c.name=="Continuous arena foundation"){foundation=c;foundations++;}
                else if(c.name=="Arena perimeter")r.fenceColliders++;
                else if(c.name=="Curated elemental tree trunk")r.treeProxies++;
                else if(c.name=="Nonwalkable elemental landmark")r.landmarkProxies++;
                else if(c.name.StartsWith("Tower platform ",StringComparison.Ordinal))r.courseColliders++;
                else if(c.name=="Invasion portal frame"&&c.enabled&&c.gameObject.activeInHierarchy)r.activeGateFrames++;
            }
            foreach(var gate in q.Arena.GetComponentsInChildren<LaneGateVolume>(true))
            {r.barriers++;if(gate.Blocked)r.blockedBarriers++;}
            Verify(r,foundations==1&&foundation!=null,"one_foundation","Exactly one independent continuous physics foundation.");
            Verify(r,r.treeProxies==16&&r.landmarkProxies==8,"actual_proxy_inventory","Actual authored collider identities match16/8 build result.");
            Verify(r,r.fenceColliders==192&&r.courseColliders==12,"fence_pad_inventory","192 separate fence boxes and12 physical pad courses.");
            Verify(r,r.activeGateFrames==4&&r.barriers==8&&r.blockedBarriers==6,"current_two_lane_gates","Two3m gate assemblies active, six other corridors solidly blocked.");
            Verify(r,r.totalBoxes==243,"world_collision_budget","Frozen006+005 authored World box budget243; no extra canopy/leaf collision.");
            if(foundation!=null)
            {
                Verify(r,foundation.enabled&&foundation.gameObject.activeInHierarchy&&!foundation.isTrigger,"foundation_solid","Floor physics is persistent and separate from frustum-cullable meshes.");
                Verify(r,(foundation.bounds.center-new Vector3(0,-.35f,0)).sqrMagnitude<.000001f&&
                    (foundation.bounds.size-new Vector3(154,.7f,154)).sqrMagnitude<.000001f,"foundation_bounds","Actual continuous topY0,154m square physics slab.");
                Verify(r,foundation.GetComponent<Renderer>()==null,"floor_renderer_separation","No renderer enable state can disable the independent floor object.");
            }
            return foundation;
        }
        static void CheckFoundation(BoxCollider floor,Query q)
        {
            // Intersect the ACTUAL authored collider, so a nearer prop does not
            // falsely reject the floor or falsely substitute for its surface.
            for(int x=-72;x<=72;x+=4)for(int z=-72;z<=72;z+=4)if(x*x+z*z<=74*74)
            {
                q.Report.physicsQueries++;q.Report.foundationSamples++;
                bool hit=floor.Raycast(new Ray(new Vector3(x,2,z),Vector3.down),out RaycastHit h,3);
                Verify(q.Report,hit&&h.normal.y>.99f&&Mathf.Abs(h.point.y)<.001f,"floor_"+x+"_"+z,"Real foundation top ray, including non-road terrain.");
            }
        }
        static void CheckFence(Query q)
        {
            for(int step=0;step<768;step++)
            {
                float a=step*Mathf.PI*2/768;Vector3 direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                foreach(float y in new[]{.3f,1.5f,2.8f})
                {
                    q.Report.fenceRays++;q.Report.physicsQueries++;
                    int count=Physics.RaycastNonAlloc(direction*72+Vector3.up*y,direction,q.Hits,5,~(1<<2),QueryTriggerInteraction.Ignore);
                    bool hit=false;if(count==q.Hits.Length)q.Report.querySaturations++;
                    else for(int i=0;i<count;i++)if(q.Hits[i].collider!=null&&q.Hits[i].collider.name=="Arena perimeter"&&q.Hits[i].collider.transform.IsChildOf(q.Arena)&&
                        !q.Hits[i].collider.isTrigger&&q.Hits[i].distance>2.9f&&q.Hits[i].distance<3.3f){hit=true;break;}
                    Verify(q.Report,hit,"fence_ray_"+step+"_"+y,"Dense radial rays find the actual continuous fence, not a nearer prop.");
                }
                if(step%4==0)
                {
                    q.Report.fenceCapsules++;q.Report.physicsQueries++;
                    Vector3 foot=direction*72+Vector3.up*.06f;Capsule(foot,.29f,1.85f,out Vector3 a0,out Vector3 b0);
                    int count=Physics.CapsuleCastNonAlloc(a0,b0,.29f,direction,q.Hits,5,~(1<<2),QueryTriggerInteraction.Ignore);bool hit=false;
                    if(count==q.Hits.Length)q.Report.querySaturations++;
                    else for(int i=0;i<count;i++)if(q.Hits[i].collider!=null&&q.Hits[i].collider.name=="Arena perimeter"&&q.Hits[i].collider.transform.IsChildOf(q.Arena)){hit=true;break;}
                    Verify(q.Report,hit,"fence_body_"+step,"Actual player-sized outward capsule hits the persistent fence.");
                }
            }
        }
        static void CheckRoads(Query q)
        {
            foreach(float side in new[]{-1f,1f})for(int r=9;r<=59;r+=2)foreach(float lateral in new[]{-2.2f,0,2.2f})
            {
                Vector3 foot=new Vector3(side*r,.06f,lateral);q.Report.roadCapsules++;
                Verify(q.Report,CapsuleClear(q,foot,.35f,1.85f,out string blocker),"road_"+side+"_"+r+"_"+lateral,
                    "Actual5.6m East/West road body clearance; blocker="+blocker);
            }
            foreach(float side in new[]{-1f,1f})for(int r=10;r<=58;r+=4)
            {
                q.Report.roadCapsules++;Verify(q.Report,CapsuleClear(q,new Vector3(side*r,.06f,0),.844f,2.28f,out string blocker),
                    "ground_body_"+side+"_"+r,"Current grounded-body centre lane envelope; not a live steering/admission test. blocker="+blocker);
            }
        }
        static void CheckPads(DungeonWorld world,Query q)
        {
            Verify(q.Report,world.TowerBuildPads.Length==4&&ReferenceEquals(world.TowerBuildPads,world.TowerPlatforms.TopCenters),"four005_canonical_pads","Shared canonical array retained; this fixture rejects007 bastion coordinates.");
            var unique=new HashSet<Collider>();
            for(int pad=0;pad<Math.Min(4,world.TowerBuildPads.Length);pad++)
            {
                Vector3 p=world.TowerBuildPads[pad];Verify(q.Report,(p-Pads[pad]).sqrMagnitude<.000001f,"pad_id_"+pad,"Exact005 centre and raised top.36.");
                Verify(q.Report,world.TowerPlatforms.TryGetTopSurface(pad,out RaycastHit top)&&Mathf.Abs(top.point.y-.36f)<.002f&&top.normal.y>.99f,
                    "pad_top_"+pad,"Actual registered top is unobstructed; run on an empty prepared arena.");
                float previousTop=0;
                for(int course=0;course<3;course++)
                {
                    Collider c=world.TowerPlatforms.GetCourseCollider(pad,course);TowerPlatformGeometry.GetCourse(p,course,out Vector3 center,out Vector3 size);
                    bool valid=c!=null&&unique.Add(c)&&c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&world.TowerPlatforms.IsPadCollider(pad,c);
                    Verify(q.Report,valid,"course_"+pad+"_"+course,"Unique registered active solid course.");if(c==null)continue;
                    Verify(q.Report,(c.bounds.center-center).sqrMagnitude<.000001f&&(c.bounds.size-size).sqrMagnitude<.000001f,
                        "course_bounds_"+pad+"_"+course,"Actual collider matches visible three-course geometry dimensions.");
                    Verify(q.Report,Mathf.Abs(c.bounds.min.y-previousTop)<.001f&&Mathf.Abs(c.bounds.max.y-previousTop-.12f)<.001f,
                        "course_riser_"+pad+"_"+course,"Three flush.12m risers; no gap/float, below existing.30m player step setting.");previousTop=c.bounds.max.y;
                }
                Vector3 approach=new Vector3(p.x,.06f,p.z-Mathf.Sign(p.z)*1.65f);q.Report.padApproaches++;
                Verify(q.Report,CapsuleClear(q,approach,.29f,1.85f,out string blocker),"pad_approach_"+pad,"Road-side step approach stays clear. blocker="+blocker);
            }
            Verify(q.Report,unique.Count==12,"twelve_registered_courses","All four bastion-free005 pads retain twelve physical courses.");
        }
        static void CheckSmith(GameSession session,Query q)
        {
            Vector3 site=session.World.MatchForgePosition;Quaternion yaw=Quaternion.Euler(0,session.World.MatchForgeYaw,0);
            Verify(q.Report,(site-new Vector3(5.6f,0,-5.6f)).sqrMagnitude<.000001f&&Mathf.Abs(session.World.MatchForgeYaw-315)<.001f,
                "smith_anchor","Existing005 reserved Smith site/yaw retained.");
            foreach(float distance in new[]{2.4f,2.8f})
            {
                Vector3 p=site+yaw*Vector3.forward*distance;p.y=.06f;q.Report.smithApproaches++;
                Verify(q.Report,CapsuleClear(q,p,.29f,1.85f,out string blocker),"smith_approach_"+distance,"Actual front approach capsule clear; no NPC/setting mutation. blocker="+blocker);
            }
            foreach(var npc in Object.FindObjectsByType<NpcBlacksmith>())if((npc.transform.position-site).sqrMagnitude<.1f)
            {
                q.Report.fieldSmithPresent=true;Vector3 eye=site+yaw*Vector3.forward*2.4f+Vector3.up*1.65f,to=npc.AimPoint-eye;q.Report.physicsQueries++;
                Verify(q.Report,Physics.Raycast(eye,to.normalized,out RaycastHit hit,to.magnitude+.08f,~(1<<2),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<NpcBlacksmith>()==npc,
                    "actual_smith_los","Actual nearest-solid field NPC ray; no shop transaction performed.");break;
            }
            if(!q.Report.fieldSmithPresent)q.Report.checks.Add(new Check{name="actual_smith_actor_los",status="UNVISITED_ACTOR_ABSENT",detail="Reserved geometry approaches checked; no field NPC is active in this World-only setup."});
        }
        static void CheckGates(Query q)
        {
            foreach(float side in new[]{-1f,1f})
            {
                Vector3 portal=new Vector3(side*64,0,0);
                foreach(float z in new[]{-1f,1f})
                {
                    q.Report.physicsQueries++;int count=Physics.RaycastNonAlloc(portal+Vector3.up*1.65f,Vector3.forward*z,q.Hits,4,~(1<<2),QueryTriggerInteraction.Ignore);
                    float nearest=float.PositiveInfinity;if(count==q.Hits.Length)q.Report.querySaturations++;
                    else for(int i=0;i<count;i++)if(q.Hits[i].collider!=null&&q.Hits[i].collider.name=="Invasion portal frame"&&q.Hits[i].collider.transform.IsChildOf(q.Arena))nearest=Mathf.Min(nearest,q.Hits[i].distance);
                    Verify(q.Report,Mathf.Abs(nearest-1.5f)<.003f,"gate_inner_face_"+side+"_"+z,"Two real pillar inner faces give3.00m aperture at actual frame colliders.");
                }
                foreach(float lateral in new[]{-1.15f,0,1.15f})
                {
                    q.Report.gateCapsules++;Vector3 foot=portal-Vector3.right*side*2+Vector3.forward*lateral+Vector3.up*.06f;
                    Verify(q.Report,BodySweepClear(q,foot,.29f,1.85f,Vector3.right*side,4,out string blocker),"gate_player_"+side+"_"+lateral,
                        "Actual player capsule crosses retained aperture; blocker="+blocker);
                }
                q.Report.gateCapsules++;Verify(q.Report,BodySweepClear(q,portal-Vector3.right*side*2+Vector3.up*.06f,.844f,2.28f,Vector3.right*side,4,out string groundBlocker),
                    "gate_ground_"+side,"Current grounded capsule clears aperture/header; flying fin/pathfinding acceptance remains separate. blocker="+groundBlocker);
            }
        }
        static void CheckLighting(DungeonWorld world,Transform arena,Receipt r)
        {
            Light primary=null,secondary=null;
            foreach(var light in arena.GetComponentsInChildren<Light>(true))
            {
                r.lights.Add(new LightRecord{name=light.name,type=light.type.ToString(),shadows=light.shadows.ToString(),enabled=light.enabled,
                    active=light.gameObject.activeInHierarchy,position=light.transform.position,range=light.range,intensity=light.intensity,entityId=light.GetEntityId().ToString()});
                if(light.type==LightType.Point){r.pointLightsCreated++;if(light.enabled&&light.gameObject.activeInHierarchy)r.pointLightsEnabled++;
                    if(light.shadows!=LightShadows.None)r.worldRealtimeShadowPoints++;}
                if(light.name=="Lava caldera primary actual spill")primary=light;
                if(light.name=="Lava caldera secondary actual spill")secondary=light;
            }
            Verify(r,r.pointLightsCreated==9&&r.pointLightsEnabled==9,"environment_point_budget","Frozen006 two-active-lane environment has9 actual persistent point-light identities; no per-tile lights.");
            CheckLavaLight(r,primary,new Vector3(57.982756f,10,57.982756f),19,6.8f,"primary");
            CheckLavaLight(r,secondary,new Vector3(63.993164f,7,46.315495f),13,2.975f,"secondary");
            Verify(r,r.worldRealtimeShadowPoints==0,"no_extra_world_shadow_points","All006 persistent environment point lights retain shadowless source policy.");
            foreach(var light in Object.FindObjectsByType<Light>())if(light.enabled&&light.gameObject.activeInHierarchy&&light.shadows!=LightShadows.None)
            {if(light.type==LightType.Directional)r.sceneShadowDirectionals++;else r.sceneShadowLocalLights++;}
            Verify(r,r.sceneShadowDirectionals<=1&&r.sceneShadowLocalLights==0,"scene_shadow_budget","At most existing moon shadow directional; no new local realtime shadow light. Preset may disable moon shadows.");
            r.checks.Add(new Check{name="lava_receiver_pixel_contribution",status="UNVISITED_RENDER_CAPTURE",detail="Enabled/range/intensity/physical receiver metadata are checked; real surface contribution needs root's separate native on/off URP capture. No brightness/FPS claim."});
        }
        static void CheckLavaLight(Receipt r,Light light,Vector3 position,float range,float intensity,string label)
        {
            Verify(r,light!=null,"lava_"+label+"_exists","Exact persistent lava fixture identity.");if(light==null)return;
            Verify(r,light.type==LightType.Point&&light.enabled&&light.gameObject.activeInHierarchy&&light.shadows==LightShadows.None,
                "lava_"+label+"_permanent","Always enabled independent of player/camera proximity; this instantaneous check does not claim temporal stability.");
            Verify(r,(light.transform.position-position).sqrMagnitude<.00001f&&Mathf.Abs(light.range-range)<.001f&&Mathf.Abs(light.intensity-intensity)<.001f,
                "lava_"+label+"_values","Actual retained range/intensity/position match sparse two-light006 contract.");
        }
        static void CheckMeshes(DungeonWorld world,Transform arena,Receipt r)
        {
            var meshes=new HashSet<Mesh>();var materials=new HashSet<Material>();var shaders=new HashSet<Shader>();
            foreach(var renderer in arena.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.renderers++;if(renderer.enabled&&renderer.gameObject.activeInHierarchy)r.activeRenderers++;
                Bounds bounds=renderer.bounds;Verify(r,Finite(bounds.center)&&Finite(bounds.size),"finite_renderer_"+renderer.GetEntityId().ToString(),"Finite actual renderer bounds.");
                r.maximumRendererSpan=Mathf.Max(r.maximumRendererSpan,Mathf.Max(bounds.size.x,bounds.size.z));
                r.maximumRendererTop=Mathf.Max(r.maximumRendererTop,bounds.max.y);
                foreach(float x in new[]{bounds.min.x,bounds.max.x})foreach(float z in new[]{bounds.min.z,bounds.max.z})r.maximumRendererRadius=Mathf.Max(r.maximumRendererRadius,new Vector2(x,z).magnitude);
                foreach(var material in renderer.sharedMaterials)
                {
                    if(material==null){Verify(r,false,"missing_material_"+renderer.GetEntityId().ToString(),"World renderer contains a null material.");continue;}
                    materials.Add(material);if(material.shader!=null)shaders.Add(material.shader);
                }
                var filter=renderer.GetComponent<MeshFilter>();if(filter!=null&&filter.sharedMesh!=null)meshes.Add(filter.sharedMesh);
            }
            foreach(var mesh in meshes)
            {
                long triangles=0;for(int i=0;i<mesh.subMeshCount;i++)if(mesh.GetTopology(i)==MeshTopology.Triangles)triangles+=(long)mesh.GetIndexCount(i)/3;
                r.totalVertices+=mesh.vertexCount;r.indexedTriangles+=triangles;r.meshes.Add(new MeshRecord{name=mesh.name,entityId=mesh.GetEntityId().ToString(),vertices=mesh.vertexCount,
                    submeshes=mesh.subMeshCount,triangles=triangles,readable=mesh.isReadable,boundsCenter=mesh.bounds.center,boundsSize=mesh.bounds.size});
            }
            foreach(var shader in shaders)Verify(r,shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"shader_"+shader.name,"Actual used World shader supported and free from reported compiler errors.");
            r.uniqueMeshes=meshes.Count;r.uniqueMaterials=materials.Count;r.uniqueShaders=shaders.Count;
            r.mainChunks=world.ArenaChunkMeshCount;r.spatialChunks=world.ArenaSpatialChunkCount;r.worldVertices=world.ArenaVertexCount;r.maximumChunkSpan=world.ArenaMaximumChunkSpan;
            Verify(r,r.mainChunks>0&&r.spatialChunks>0&&r.spatialChunks<=r.mainChunks&&r.maximumChunkSpan<=26,"world_spatial_bounds","Real spatial chunk counts/span reported; floor colliders remain separate. No invented main-chunk coordinate cap.");
            Verify(r,r.maximumRendererTop<=40&&r.maximumRendererRadius<=115,"bounded_decorative_skyline","Actual combined renderer envelopes remain within bounded006 exterior vista extent; not additional walkable terrain.");
            Verify(r,r.uniqueMeshes>0&&r.uniqueMaterials<=40&&r.indexedTriangles>0,"shared_resource_inventory","Actual unique assets/index triangles measured; not frame submissions or FPS.");
        }
        static bool CapsuleClear(Query q,Vector3 feet,float radius,float height,out string blocker)
        {
            Capsule(feet,radius,height,out Vector3 a,out Vector3 b);q.Report.physicsQueries++;
            int count=Physics.OverlapCapsuleNonAlloc(a,b,radius,q.Overlaps,~(1<<2),QueryTriggerInteraction.Ignore);
            blocker="";if(count==q.Overlaps.Length){q.Report.querySaturations++;blocker="SATURATED";return false;}
            for(int i=0;i<count;i++)if(q.Overlaps[i]!=null){blocker=q.Overlaps[i].name;return false;}return true;
        }
        static bool BodySweepClear(Query q,Vector3 feet,float radius,float height,Vector3 direction,float distance,out string blocker)
        {
            if(!CapsuleClear(q,feet,radius,height,out blocker))return false;
            Capsule(feet,radius,height,out Vector3 a,out Vector3 b);q.Report.physicsQueries++;
            int count=Physics.CapsuleCastNonAlloc(a,b,radius,direction,q.Hits,distance,~(1<<2),QueryTriggerInteraction.Ignore);
            if(count==q.Hits.Length){q.Report.querySaturations++;blocker="SATURATED";return false;}
            if(count>0){blocker=q.Hits[0].collider!=null?q.Hits[0].collider.name:"UNKNOWN";return false;}return true;
        }
        static void Capsule(Vector3 feet,float radius,float height,out Vector3 a,out Vector3 b)
        {a=feet+Vector3.up*radius;b=feet+Vector3.up*(height-radius);}
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
        static void Verify(Receipt r,bool ok,string name,string detail)
        {r.assertions++;if(!ok)r.failures++;r.checks.Add(new Check{name=name,status=ok?"PASS":"FAIL",detail=detail});}
        static string FileHash(string path)
        {if(!File.Exists(path))return "MISSING";using(var sha=SHA256.Create())using(var input=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();}
    }
}
#endif
