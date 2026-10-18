#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace OdysseyOracle.Editor
{
    /// <summary>
    /// Explicit root-CLI entry point only. Run after the first rendered menu frame,
    /// or in a stable tutorial frame. No menu clicks, simulation steps or live saves.
    /// The Editor folder excludes these checks from the browser player.
    /// </summary>
    public static class OracleReleaseChecks
    {
        [Serializable] public sealed class CheckResult
        {
            public string name, status = "running", details = "", exception = "";
            public int assertions;
        }

        [Serializable] public sealed class Report
        {
            public string status = "running", utc;
            public int passed, failed, assertions, castClips, towerClips, uniqueSfx;
            public int worldRaycasts, shrineHorizontalTriangles, radiusComparisons;
            public int spatialNearestComparisons;
            public string heartstoneFixture = "", spatialFixture = "";
            public string execution = "Explicit Editor checks; source/runtime evidence only, not browser or FPS acceptance.";
            public CheckResult[] checks;
        }

        [Serializable] sealed class SpatialReport
        { public string status; public int targetCount, bruteForceComparisons; }

        static readonly string[] CastElements = { "fire", "water", "earth", "air" };
        static readonly string[] TowerElements = { "fire", "water", "nature", "air", "lightning", "crystal", "ice", "magma" };
        static readonly string[] WorldCues = {
            "portal_idle", "portal_open", "portal_travel", "pickup", "passive_unlock", "wand_upgrade",
            "downed", "revive", "enemy_windup", "enemy_impact", "book_open", "ui_hover",
            "footstep_stone_01", "footstep_stone_02", "footstep_wood_01", "footstep_wood_02",
            "jump", "land_stone", "land_wood"
        };

        /// <returns>JSON with per-check failures. Any assertion/exception makes status failed.</returns>
        public static string RunRuntimeChecks()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("o") };
            var results = new List<CheckResult>(24);
            GameSession session = GameSession.Instance;
            Run(report, results, "runtime_context", c => {
                Require(c, Application.isPlaying, "Run in the native Editor's playing scene, not Edit mode.");
                Require(c, session != null && session.isActiveAndEnabled, "An active GameSession must exist.");
                Require(c, session.World != null && !session.World.IsCoopArena, "Use the fresh menu or tutorial; this check does not switch a live arena.");
            });
            if (report.failed != 0) return Finish(report, results);

            Snapshot before = Snapshot.Capture(session);
            EventSystem events = null;
            InputSystemUIInputModule input = null;
            Sprite logo = null;
            Shader sky = null, portal = null;

            Run(report, results, "scene_references", c => {
                Require(c, session.Player != null && session.Player.ViewCamera != null, "Player camera must exist.");
                Require(c, session.Hud != null && session.Combat != null, "HUD and combat must exist.");
                Require(c, session.Combat.Shrine != null, "The objective shrine must exist before starting.");
                Require(c, session.World.gameObject.activeInHierarchy, "The authored world must be active.");
            });
            Run(report, results, "input_system_module", c => {
                events = EventSystem.current;
                Require(c, events != null && events.isActiveAndEnabled, "An active current EventSystem is required.");
                int systems = 0, modules = 0;
                foreach (var item in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude))
                    if (item.isActiveAndEnabled) systems++;
                foreach (var item in events.GetComponents<BaseInputModule>())
                    if (item.isActiveAndEnabled) { modules++; input = item as InputSystemUIInputModule; }
                Require(c, systems == 1, "Exactly one active EventSystem must route UI input.");
                Require(c, modules == 1 && input != null, "The sole enabled UI module must be InputSystemUIInputModule.");
                Require(c, events.currentInputModule == input, "EventSystem must actually select the Input System module.");
            });
            Run(report, results, "mouse_actions_enabled", c => {
                Require(c, input != null, "Input module prerequisite failed.");
                Require(c, input.point != null && input.point.action != null && input.point.action.enabled, "The point action must be assigned and enabled.");
                Require(c, input.leftClick != null && input.leftClick.action != null && input.leftClick.action.enabled, "The left-click action must be assigned and enabled.");
            });
            Run(report, results, "single_audio_listener", c => {
                int listeners = 0;
                foreach (var item in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                    if (item.isActiveAndEnabled) listeners++;
                Require(c, listeners == 1, "Exactly one active enabled AudioListener is required; disabled retiring components do not count.");
                c.details = "activeEnabledListeners=" + listeners;
            });
            Run(report, results, "logo_sprite_resource", c => {
                logo = Resources.Load<Sprite>("Interface/OracleReturnLogo");
                Require(c, logo != null && logo.texture != null, "The original title logo must import as a Sprite.");
                Require(c, logo.rect.width > 0 && logo.rect.height > 0, "Logo Sprite must contain actual pixels.");
            });
            Run(report, results, "logo_sprite_bound_to_ui", c => {
                Require(c, session.Hud != null && logo != null, "HUD/logo prerequisites must exist.");
                int bound = 0;
                foreach (var image in session.Hud.GetComponentsInChildren<Image>(true))
                    if (image.sprite == logo && image.preserveAspect) bound++;
                Require(c, bound > 0, "An actual HUD Image must use the original logo Sprite with preserved aspect.");
                c.details = "boundImages=" + bound + "; inactive menu panels are inspected without activating them";
            });
            Run(report, results, "four_cast_audio_resources", c => {
                foreach (string element in CastElements) { Clip(c, "Audio/Generated/element_" + element); report.castClips++; }
                Require(c, report.castClips == 4, "All four authored cast clips must load.");
            });
            Run(report, results, "eight_tower_audio_resources", c => {
                foreach (string element in TowerElements) { Clip(c, TowerPath(element)); report.towerClips++; }
                Require(c, report.towerClips == 8, "All eight tower element mappings must resolve.");
            });
            Run(report, results, "twenty_nine_unique_sfx_resources", c => {
                var paths = new HashSet<string>(StringComparer.Ordinal) { "Audio/odyssey_campfire_loop" };
                foreach (string element in CastElements) paths.Add("Audio/Generated/element_" + element);
                foreach (string element in TowerElements) paths.Add(TowerPath(element));
                foreach (string cue in WorldCues) paths.Add("Audio/WorldCue/" + cue);
                Require(c, paths.Count == 29, "Authored SFX set is 19 world cues, nine unique element clips and campfire.");
                foreach (string path in paths) { Clip(c, path); report.uniqueSfx++; }
                c.details = "19 world cues + 9 unique cast/tower clips + 1 campfire; no playback requested";
            });
            Run(report, results, "guide_06_audio_resource", c => Clip(c, "Tutorial/guide_06"));
            Run(report, results, "continuous_sky_shader_supported", c => {
                sky = Resources.Load<Shader>("Shaders/OracleNightSky");
                Require(c, sky != null && sky.isSupported, "Continuous sky shader must be retained and supported on the active graphics API.");
                Require(c, !ShaderUtil.ShaderHasError(sky), "The custom arena/fallback sky shader must have no compiler errors; isSupported alone is insufficient.");
            });
            Run(report, results, "continuous_sky_actually_bound", c => {
                Material actual = RenderSettings.skybox;
                Material tutorial = Resources.Load<Material>("OracleTutorialNightSky");
                Require(c, actual != null && actual.shader != null && actual.shader.isSupported, "The scene must bind a supported sky material.");
                Require(c, !ShaderUtil.ShaderHasError(actual.shader), "The bound sky material's shader must have no compiler errors.");
                bool retainedCubemap = tutorial != null && actual == tutorial && actual.HasProperty("_Tex") && actual.GetTexture("_Tex") is Cubemap;
                bool procedural = sky != null && actual.shader == sky;
                Require(c, retainedCubemap || procedural, "Bind the retained OracleTutorialNightSky material with an actual Cubemap, or the continuous procedural sky fallback.");
                Require(c, session.Player != null && session.Player.ViewCamera != null && session.Player.ViewCamera.clearFlags == CameraClearFlags.Skybox, "The actual player camera must render the skybox.");
                c.details = retainedCubemap ? "Retained tutorial material and Cubemap bound" : "Continuous procedural sky bound";
            });
            Run(report, results, "portal_shader_supported", c => {
                portal = Resources.Load<Shader>("Shaders/PortalAnomaly");
                Require(c, portal != null && portal.isSupported, "The original portal shader must be retained and supported.");
                Require(c, !ShaderUtil.ShaderHasError(portal), "The custom portal shader must have no compiler errors; isSupported alone is insufficient.");
            });
            Run(report, results, "portal_surface_actually_ready", c => {
                int ready = 0;
                foreach (var anomaly in session.World.GetComponentsInChildren<PortalAnomaly>(false)) {
                    if (!anomaly.isActiveAndEnabled || !anomaly.IsReady) continue;
                    foreach (var renderer in anomaly.GetComponentsInChildren<MeshRenderer>(false))
                        if (renderer.enabled && renderer.sharedMaterial != null && renderer.sharedMaterial.shader == portal) ready++;
                }
                Require(c, ready > 0, "An active initialized portal must actually render the supported anomaly material.");
                c.details = "readySurfaceRenderers=" + ready;
            });
            Run(report, results, "actual_world_floor_rays", c => {
                Vector3 spawn = session.World.PlayerSpawn, shrine = session.World.ShrinePosition, exit = session.World.PortalPosition;
                Floor(c, report, session.World, spawn, 0f);
                Floor(c, report, session.World, (spawn + shrine) * .5f + Vector3.right * 1.8f, 0f);
                Floor(c, report, session.World, exit + Vector3.back * .8f, exit.y);
            });
            Run(report, results, "actual_world_boundary_rays", c => {
                Vector3 origin = session.World.PlayerSpawn; origin.y = 1.5f;
                Wall(c, report, session.World, origin, Vector3.left, 7f);
                Wall(c, report, session.World, origin, Vector3.right, 7f);
                origin = session.World.PortalPosition + Vector3.up * 1.5f;
                Wall(c, report, session.World, origin, Vector3.forward, 5f);
            });
            Run(report, results, "shrine_public_render_bounds", c => ShrineBounds(c, session.Combat.Shrine));
            Run(report, results, "four_shrine_feet_supported_and_symmetric", c => ShrineFeet(c, report, session.Combat.Shrine));
            Run(report, results, "button_center_raycast_routes_without_click", c => MenuRaycast(c, session, events, input));
            Run(report, results, "native_heartstone_fixture", c => {
                report.heartstoneFixture = HeartcrystalPickup.RunEngineChecks();
                Require(c, !string.IsNullOrEmpty(report.heartstoneFixture) && report.heartstoneFixture.StartsWith("PASS: ", StringComparison.Ordinal), "Native Heartstone fixture must explicitly pass; exceptions are failures.");
                c.details = report.heartstoneFixture;
            });
            Run(report, results, "native_spatial_nearest_fixture", c => {
                report.spatialFixture = SpatialTargetGridChecks.Run();
                SpatialReport nested = JsonUtility.FromJson<SpatialReport>(report.spatialFixture);
                Require(c, nested != null && nested.status == "passed", "Existing native grid checks must explicitly pass.");
                Require(c, nested.targetCount >= 100 && nested.bruteForceComparisons > 0, "Native grid fixture must exercise real populated comparisons.");
                report.spatialNearestComparisons = nested.bruteForceComparisons;
                c.details = report.spatialFixture;
            });
            Run(report, results, "crowd_query_radius_regressions", c => RadiusChecks(c, report));
            Run(report, results, "gameplay_state_unchanged", c => before.AssertUnchanged(c, session));
            return Finish(report, results);
        }

        static void Run(Report report, List<CheckResult> results, string name, Action<CheckResult> body)
        {
            var result = new CheckResult { name = name }; results.Add(result);
            try { body(result); result.status = "passed"; report.passed++; }
            catch (Exception error) { result.status = "failed"; result.exception = error.GetType().FullName + ": " + error.Message; report.failed++; }
            report.assertions += result.assertions;
        }

        static string Finish(Report report, List<CheckResult> results)
        { report.status = report.failed == 0 ? "passed" : "failed"; report.checks = results.ToArray(); return JsonUtility.ToJson(report); }

        static void Require(CheckResult result, bool condition, string message)
        {
            result.assertions++;
            Assert.IsTrue(condition, message);
            // Remains a hard failure if the caller changed Assert.raiseExceptions or assertions were stripped.
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Clip(CheckResult result, string path)
        {
            AudioClip clip = Resources.Load<AudioClip>(path);
            Require(result, clip != null, "Missing AudioClip resource: " + path);
            Require(result, clip.samples > 0 && clip.frequency > 0 && clip.channels > 0 && Finite(clip.length) && clip.length > 0f, "AudioClip must contain valid imported samples: " + path);
        }

        static string TowerPath(string element)
        { string path = "Audio/Elements/element_" + element; return Resources.Load<AudioClip>(path) != null ? path : "Audio/Generated/element_" + element; }

        static void Floor(CheckResult result, Report report, DungeonWorld world, Vector3 anchor, float expectedY)
        {
            report.worldRaycasts++;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(anchor.x, expectedY + 2.5f, anchor.z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            foreach (RaycastHit hit in hits)
                if (Terrain(hit.collider, world) && hit.normal.y > .95f && Mathf.Abs(hit.point.y - expectedY) < .12f) { found = true; break; }
            Require(result, found, "Actual solid world floor missing at " + anchor + "; actor/trigger colliders do not satisfy this ray.");
        }

        static void Wall(CheckResult result, Report report, DungeonWorld world, Vector3 origin, Vector3 direction, float distance)
        {
            report.worldRaycasts++;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            foreach (RaycastHit hit in hits)
                if (Terrain(hit.collider, world) && hit.collider.bounds.size.y >= 2f && Vector3.Dot(hit.normal, -direction) > .8f) { found = true; break; }
            Require(result, found, "Actual tall solid architecture missing from " + origin + " toward " + direction + ".");
        }

        static bool Terrain(Collider collider, DungeonWorld world)
        { return collider != null && !collider.isTrigger && collider.enabled && collider.gameObject.activeInHierarchy && collider.GetComponentInParent<DungeonWorld>() == world; }

        static void MenuRaycast(CheckResult result, GameSession session, EventSystem events, InputSystemUIInputModule input)
        {
            Require(result, session.Hud != null && events != null && input != null && input.isActiveAndEnabled, "HUD/EventSystem prerequisites must pass.");
            Require(result, Screen.width > 0 && Screen.height > 0, "A real viewport is needed for a center-ray UI check.");
            int candidates = 0;
            foreach (Button button in session.Hud.GetComponentsInChildren<Button>(false)) {
                if (!button.isActiveAndEnabled || !button.IsInteractable()) continue;
                var rect = button.transform as RectTransform;
                var canvas = button.GetComponentInParent<Canvas>();
                if (rect == null || canvas == null || !canvas.isActiveAndEnabled) continue;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                if (point.x < 0 || point.y < 0 || point.x >= Screen.width || point.y >= Screen.height) continue;
                candidates++;
                var hits = new List<RaycastResult>(16);
                events.RaycastAll(new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left }, hits);
                if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Button>() != button) continue;
                Require(result, hits[0].module is GraphicRaycaster, "The visible button must be reached by the UI GraphicRaycaster.");
                Require(result, button.targetGraphic != null && button.targetGraphic.raycastTarget && button.targetGraphic.isActiveAndEnabled, "The real button must retain an enabled raycast target graphic.");
                result.details = "Routed center of actual button '" + button.name + "'; candidates=" + candidates + "; no click/selection/input-device event dispatched";
                return;
            }
            Require(result, false, "No active interactable button center reached its own GraphicRaycaster. Run in a stable menu/tutorial frame, after layout and outside cinematic/loading blockers.");
        }

        struct HorizontalTriangle { public Vector3 a, b, c; public float y, up; }
        static List<HorizontalTriangle> ShrineTriangles(CheckResult result, OracleShrine shrine)
        {
            Require(result, shrine != null, "Shrine must exist.");
            var triangles = new List<HorizontalTriangle>(2048);
            MeshFilter[] filters = shrine.GetComponentsInChildren<MeshFilter>(true);
            Require(result, filters.Length > 0 && filters.Length <= 128, "Shrine must expose a bounded set of public meshes.");
            foreach (MeshFilter filter in filters) {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !filter.gameObject.activeInHierarchy) continue;
                Require(result, mesh.vertexCount > 0 && mesh.vertexCount <= 200000, "Shrine mesh vertex data must be bounded.");
                // Public Editor read-only snapshot also supports UploadMeshData(true); never changes isReadable.
                using (Mesh.MeshDataArray snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh)) {
                    Mesh.MeshData data = snapshot[0];
                    var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
                    try {
                        data.GetVertices(vertices);
                        Matrix4x4 matrix = shrine.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                        for (int submesh = 0; submesh < data.subMeshCount; submesh++) {
                            var descriptor = data.GetSubMesh(submesh);
                            if (descriptor.topology != MeshTopology.Triangles) continue;
                            var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
                            try {
                                data.GetIndices(indices, submesh, true);
                                for (int i = 0; i + 2 < indices.Length; i += 3) {
                                    Vector3 a = matrix.MultiplyPoint3x4(vertices[indices[i]]), b = matrix.MultiplyPoint3x4(vertices[indices[i + 1]]), d = matrix.MultiplyPoint3x4(vertices[indices[i + 2]]);
                                    Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
                                    if (Mathf.Abs(normal.y) < .999f || Mathf.Abs(a.y - b.y) > .001f || Mathf.Abs(a.y - d.y) > .001f) continue;
                                    triangles.Add(new HorizontalTriangle { a = a, b = b, c = d, y = (a.y + b.y + d.y) / 3f, up = normal.y });
                                }
                            } finally { if (indices.IsCreated) indices.Dispose(); }
                        }
                    } finally { if (vertices.IsCreated) vertices.Dispose(); }
                }
            }
            return triangles;
        }

        static void ShrineBounds(CheckResult result, OracleShrine shrine)
        {
            Require(result, shrine != null && shrine.gameObject.activeInHierarchy, "Objective shrine must be active.");
            bool haveBounds = false; Bounds bounds = new Bounds();
            foreach (MeshFilter filter in shrine.GetComponentsInChildren<MeshFilter>(true)) {
                Mesh mesh = filter.sharedMesh; if (mesh == null || !filter.gameObject.activeInHierarchy) continue;
                Matrix4x4 matrix = shrine.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds local = mesh.bounds;
                for (int i = 0; i < 8; i++) {
                    Vector3 p = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    Require(result, Finite(p.x) && Finite(p.y) && Finite(p.z), "Actual shrine render bounds must be finite.");
                    if (!haveBounds) { bounds = new Bounds(p, Vector3.zero); haveBounds = true; } else bounds.Encapsulate(p);
                }
            }
            Require(result, haveBounds && bounds.size.x >= 1.9f && bounds.size.z >= 1.9f && bounds.size.y > 2.5f, "Shrine must retain a substantial supported footprint and tall crystal silhouette.");
            Require(result, Mathf.Abs(bounds.center.x) < .08f && Mathf.Abs(bounds.center.z) < .08f, "Shrine's overall public geometry must remain centered about both horizontal axes.");
            result.details = "localRenderBounds=" + bounds;
        }

        static void ShrineFeet(CheckResult result, Report report, OracleShrine shrine)
        {
            List<HorizontalTriangle> triangles = ShrineTriangles(result, shrine);
            report.shrineHorizontalTriangles = triangles.Count;
            Require(result, triangles.Count > 0, "Read actual horizontal shrine faces, not copied generator constants.");
            float commonBottom = float.NaN, commonTop = float.NaN;
            foreach (float x in new[] { -.69f, .69f }) foreach (float z in new[] { -.69f, .69f }) {
                // Regression samples cover each authored foot and its outer corners; supports must face upward.
                float bottom = Surface(triangles, x, z, .55f, .8f, false);
                float top = Surface(triangles, x, z, .75f, .95f, true);
                Require(result, Finite(bottom) && Finite(top) && top - bottom > .15f, "Each visible column must have a real substantial foot.");
                float support = Surface(triangles, x, z, bottom - .08f, bottom + .003f, true);
                Require(result, Finite(support) && Mathf.Abs(bottom - support) <= .004f, "Each foot bottom must be flush with a real upward-facing base, without floating.");
                if (float.IsNaN(commonBottom)) { commonBottom = bottom; commonTop = top; }
                Require(result, Mathf.Abs(bottom - commonBottom) <= .004f && Mathf.Abs(top - commonTop) <= .004f, "All four feet must share symmetric top/bottom levels.");
                foreach (float dx in new[] { -.185f, .185f }) foreach (float dz in new[] { -.185f, .185f }) {
                    float corner = Surface(triangles, x + dx, z + dz, bottom - .08f, bottom + .003f, true);
                    Require(result, Finite(corner) && Mathf.Abs(corner - bottom) <= .004f, "The rendered base must support every foot corner, including the formerly overhanging outer corners.");
                }
            }
            result.details = "Measured four rendered feet; bottom=" + commonBottom + ", top=" + commonTop + "; sixteen corner supports; read-only mesh snapshots disposed";
        }

        static float Surface(List<HorizontalTriangle> triangles, float x, float z, float low, float high, bool upward)
        {
            float found = float.NaN;
            foreach (HorizontalTriangle triangle in triangles) {
                if ((triangle.up > 0) != upward || triangle.y < low || triangle.y > high) continue;
                Vector2 a = new Vector2(triangle.a.x, triangle.a.z), b = new Vector2(triangle.b.x, triangle.b.z), c = new Vector2(triangle.c.x, triangle.c.z), p = new Vector2(x, z);
                float one = Cross(b - a, p - a), two = Cross(c - b, p - b), three = Cross(a - c, p - c);
                if (!((one >= -.0001f && two >= -.0001f && three >= -.0001f) || (one <= .0001f && two <= .0001f && three <= .0001f))) continue;
                if (float.IsNaN(found) || triangle.y > found) found = triangle.y;
            }
            return found;
        }
        static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        struct Neighbor { public int id; public Vector3 position; }
        static void RadiusChecks(CheckResult result, Report report)
        {
            SpatialTargetGrid grid = null;
            try {
                grid = new SpatialTargetGrid(1, 2f);
                var buffer = new int[12];
                Require(result, grid.QueryRadius(Vector3.zero, 2f, int.MinValue, buffer) == 0, "Empty neighbor grid must return zero.");
                grid.Add(99, Vector3.zero); grid.Add(-20, new Vector3(-1, 900, 0)); grid.Add(30, new Vector3(1, -900, 0));
                grid.Add(10, new Vector3(0, 0, 2)); grid.Add(50, new Vector3(2.01f, 0, 0));
                int count = grid.QueryRadius(Vector3.zero, 2f, 99, buffer);
                Require(result, count == 3 && buffer[0] == -20 && buffer[1] == 30 && buffer[2] == 10, "Radius is inclusive, excludes self, ignores Y and sorts signed-ID distance ties.");
                count = grid.QueryRadius(Vector3.zero, 2f, -20, buffer);
                Require(result, count == 3 && buffer[0] == 99 && buffer[1] == 30 && buffer[2] == 10, "Only the requested signed ID is excluded.");
                grid.Add(30, new Vector3(3, 0, 0));
                Require(result, grid.Count == 5 && grid.QueryRadius(Vector3.zero, 2f, 99, buffer) == 2, "Updating an existing neighbor removes its stale spatial entry.");
                Require(result, grid.QueryRadius(new Vector3(0, 800, 0), 0f, int.MinValue, buffer) == 1 && buffer[0] == 99, "Zero-radius XZ match remains valid despite vertical separation.");
                Require(result, grid.QueryRadius(Vector3.zero, 2f, int.MinValue, Array.Empty<int>()) == 0, "An empty output buffer is bounded and legal.");
                var targets = new List<Neighbor>(64);
                for (int i = 0; i < 64; i++) targets.Add(new Neighbor { id = i % 2 == 0 ? 400 + i : -400 - i, position = new Vector3((i % 8 - 4) * .55f, i * 11, (i / 8 - 4) * .55f) });
                for (int pass = 0; pass < 2; pass++) {
                    grid.Clear();
                    for (int i = 0; i < targets.Count; i++) { Neighbor item = targets[pass == 0 ? i : targets.Count - 1 - i]; grid.Add(item.id, item.position); }
                    CompareRadius(result, report, grid, targets, new Vector3(-.1f, -500, .2f), 1.8f, 12);
                    CompareRadius(result, report, grid, targets, new Vector3(-.1f, 500, .2f), float.MaxValue, 7);
                }
                Throws<ArgumentOutOfRangeException>(result, () => grid.QueryRadius(Vector3.zero, -1f, 0, buffer));
                Throws<ArgumentOutOfRangeException>(result, () => grid.QueryRadius(Vector3.zero, float.NaN, 0, buffer));
                Throws<ArgumentNullException>(result, () => grid.QueryRadius(Vector3.zero, 1f, 0, null));
                grid.Dispose();
                Require(result, !grid.IsCreated, "Neighbor fixture must release its native containers.");
                Throws<ObjectDisposedException>(result, () => grid.QueryRadius(Vector3.zero, 1f, 0, buffer));
                result.details = "Bounded 12-neighbor crowd path, signed IDs, self exclusion, truncation, reverse insertion, radius boundaries, huge-radius fallback and disposal";
            } finally { if (grid != null) grid.Dispose(); }
        }

        static void CompareRadius(CheckResult result, Report report, SpatialTargetGrid grid, List<Neighbor> targets, Vector3 origin, float radius, int capacity)
        {
            var expected = new List<Neighbor>();
            foreach (Neighbor item in targets) if (Distance(item.position, origin) <= (double)radius * radius) expected.Add(item);
            expected.Sort((a, b) => { int distance = Distance(a.position, origin).CompareTo(Distance(b.position, origin)); return distance != 0 ? distance : a.id.CompareTo(b.id); });
            var actual = new int[capacity]; int count = grid.QueryRadius(origin, radius, int.MinValue, actual);
            Require(result, count == Math.Min(capacity, expected.Count), "Neighbor query must return the bounded brute-force count.");
            for (int i = 0; i < count; i++) Require(result, actual[i] == expected[i].id, "Neighbor query must return nearest IDs in deterministic brute-force order.");
            report.radiusComparisons++;
        }
        static double Distance(Vector3 a, Vector3 b) { double x = (double)a.x - b.x, z = (double)a.z - b.z; return x * x + z * z; }
        static void Throws<T>(CheckResult result, Action action) where T : Exception
        { bool caught = false; try { action(); } catch (T) { caught = true; } Require(result, caught, "Expected " + typeof(T).Name + " from invalid/disposed radius input."); }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        sealed class Snapshot
        {
            float hp, maxHp, experience, shrineHp, timeScale;
            int wave, defeated, goal, sceneObjects;
            bool started, paused, combatUnlocked;
            Vector3 position; Quaternion rotation; CursorLockMode cursor;
            string shop; Material sky;
            public static Snapshot Capture(GameSession session)
            {
                return new Snapshot { hp = session.HP, maxHp = session.MaxHP, experience = session.Experience,
                    wave = session.Wave, defeated = session.Defeated, goal = session.Goal, started = session.HasStarted,
                    paused = session.Paused, combatUnlocked = session.CombatUnlocked, timeScale = Time.timeScale,
                    position = session.Player != null ? session.Player.transform.position : Vector3.zero,
                    rotation = session.Player != null ? session.Player.transform.rotation : Quaternion.identity,
                    shrineHp = session.Combat != null && session.Combat.Shrine != null ? session.Combat.Shrine.Health : -1,
                    shop = session.Shop != null ? JsonUtility.ToJson(session.Shop.LocalSnapshot) : "",
                    sky = RenderSettings.skybox, cursor = Cursor.lockState, sceneObjects = SceneObjectCount() };
            }
            public void AssertUnchanged(CheckResult result, GameSession session)
            {
                Require(result, session.HP == hp && session.MaxHP == maxHp && session.Experience == experience, "Checks must not change live HP or XP.");
                Require(result, session.Wave == wave && session.Defeated == defeated && session.Goal == goal, "Checks must not change live encounter progression.");
                Require(result, session.HasStarted == started && session.Paused == paused && session.CombatUnlocked == combatUnlocked, "Checks must not enter gameplay, unpause or unlock combat.");
                Require(result, session.Player != null && session.Player.transform.position == position && session.Player.transform.rotation == rotation, "Checks must not teleport or rotate the live player.");
                Require(result, session.Combat != null && session.Combat.Shrine != null && session.Combat.Shrine.Health == shrineHp, "Checks must not damage/heal the live objective.");
                Require(result, (session.Shop != null ? JsonUtility.ToJson(session.Shop.LocalSnapshot) : "") == shop, "Checks must not purchase, grant or save live Heartstone progression.");
                Require(result, Time.timeScale == timeScale && Cursor.lockState == cursor && RenderSettings.skybox == sky, "Checks must not alter global timing, pointer lock or sky binding.");
                Require(result, SceneObjectCount() == sceneObjects, "All transient fixture GameObjects must be removed synchronously in finally blocks.");
            }
            static int SceneObjectCount()
            {
                int count = 0;
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>()) if (go != null && go.scene.IsValid()) count++;
                return count;
            }
        }
    }
}
#endif
