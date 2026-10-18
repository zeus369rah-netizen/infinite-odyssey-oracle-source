#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Geometry = OdysseyOracle.CampaignLibrary.Authoring.CampaignPropGeometry;

namespace OdysseyOracle.CampaignLibrary.Authoring.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit root-run authoring only. Fixed versioned Assets output, shared eight-paint palette,
    // one combined mesh/renderer per prop, empty attachment transforms. No runtime components,
    // external media, Resources, scene placement, input, automatic execution or paid generation.
    public static class CampaignPropKitBuilder
    {
        public const string OutputRoot = "Assets/Art/CampaignLibrary/OraclePropKit_v1";
        const string ManifestPath = OutputRoot + "/CampaignPropKitManifest.asset";
        const string ShaderName = "Universal Render Pipeline/Lit";

        [Serializable] sealed class BuildReceipt
        {
            public string status, outputRoot, geometryFingerprint, originalSource;
            public bool wroteAssets, idempotent, sceneContentPlaced;
            public int paletteMaterials, totalTriangles;
            public CampaignPropRecord[] props;
        }

        public static string InspectRecipes()
        {
            var recipes = Geometry.BuildAll();
            var paints = Geometry.Palette();
            return JsonUtility.ToJson(Receipt("pure recipes inspected; no assets written", recipes, paints, false, false), true);
        }

        public static string ValidateExistingLibrary()
        {
            RequireStopped();
            var recipes = Geometry.BuildAll(); var paints = Geometry.Palette();
            ValidateOwnedOutput(recipes, paints);
            return JsonUtility.ToJson(Receipt("existing owned library validated; no writes", recipes, paints, false, true), true);
        }

        public static string BuildLibrary()
        {
            RequireStopped();
            var recipes = Geometry.BuildAll(); var paints = Geometry.Palette();
            // Full pure-data validation precedes any AssetDatabase mutation.
            foreach (var recipe in recipes) Geometry.ValidateClosedPrimitives(recipe);
            if (Directory.Exists(Absolute(OutputRoot)) || AssetDatabase.IsValidFolder(OutputRoot))
            {
                ValidateOwnedOutput(recipes, paints);
                return JsonUtility.ToJson(Receipt("identical owned library already exists; no writes", recipes, paints, false, true), true);
            }
            Shader shader = Shader.Find(ShaderName);
            if (!shader) throw new InvalidOperationException("The installed URP Lit shader must be available before baking.");
            var createdAssets = new List<string>(); var createdFolders = new List<string>();
            var transientObjects = new List<UnityEngine.Object>();
            Scene preview = default;
            GameObject root = null;
            try
            {
                EnsureFolder(OutputRoot + "/Materials", createdFolders);
                EnsureFolder(OutputRoot + "/Meshes", createdFolders);
                EnsureFolder(OutputRoot + "/Prefabs", createdFolders);
                var materials = new Material[Geometry.PaletteCount];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = CreatePaint(paints[i], shader); transientObjects.Add(materials[i]);
                    CreateAsset(materials[i], MaterialPath(paints[i]), createdAssets);
                }
                preview = EditorSceneManager.NewPreviewScene();
                for (int i = 0; i < recipes.Length; i++)
                {
                    var recipe = recipes[i];
                    Mesh mesh = CreateMesh(recipe, out int[] used); transientObjects.Add(mesh);
                    CreateAsset(mesh, MeshPath(recipe), createdAssets);
                    root = new GameObject("CP_" + recipe.Name);
                    SceneManager.MoveGameObjectToScene(root, preview);
                    root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
                    root.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = root.AddComponent<MeshRenderer>();
                    var selected = new Material[used.Length]; for (int m = 0; m < used.Length; m++) selected[m] = materials[used[m]];
                    renderer.sharedMaterials = selected;
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    foreach (var anchor in recipe.Anchors)
                    {
                        var socket = new GameObject(anchor.Name);
                        SceneManager.MoveGameObjectToScene(socket, preview);
                        socket.transform.SetParent(root.transform, false);
                        socket.transform.localPosition = Vector(anchor.Position); socket.transform.localRotation = Rotation(anchor.Rotation); socket.transform.localScale = Vector3.one;
                    }
                    AssertNeutralPrefab(root, recipe, mesh, materials, used);
                    string path = PrefabPath(recipe); RequireUnused(path);
                    var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    if (!success || !saved) throw new InvalidOperationException("Prefab save failed: " + path);
                    createdAssets.Add(path);
                    UnityEngine.Object.DestroyImmediate(root); root = null;
                }
                var manifest = ScriptableObject.CreateInstance<CampaignPropKitManifest>(); transientObjects.Add(manifest);
                manifest.generatorId = Geometry.GeneratorId; manifest.recipeVersion = Geometry.RecipeVersion;
                manifest.geometryFingerprint = Geometry.Fingerprint(recipes, paints); manifest.createdUtc = DateTime.UtcNow.ToString("O");
                manifest.provenance = "Original math-built layered voxel props; no external model/image or paid generation. Metres, +Y up, +Z front, bottom-centre bounds pivot. Palette/meshes shared; anchors are empty transforms only.";
                var receipt = Receipt("created original campaign prop library", recipes, paints, true, false);
                manifest.paletteCount = materials.Length; manifest.totalTriangles = receipt.totalTriangles; manifest.props = receipt.props;
                foreach (var asset in transientObjects) if (asset && EditorUtility.IsPersistent(asset)) AssetDatabase.SaveAssetIfDirty(asset);
                manifest.ownedAssets = RecordOwnership(paints, recipes);
                CreateAsset(manifest, ManifestPath, createdAssets);
                AssetDatabase.SaveAssetIfDirty(manifest);
                ValidateOwnedOutput(recipes, paints);
                return JsonUtility.ToJson(receipt, true);
            }
            catch (Exception failure)
            {
                var cleanup = new List<string>();
                for (int i = createdAssets.Count - 1; i >= 0; i--)
                {
                    string path = createdAssets[i];
                    if (!OwnedPath(path)) { cleanup.Add("refused unsafe rollback " + path); continue; }
                    if ((File.Exists(Absolute(path)) || AssetDatabase.LoadMainAssetAtPath(path)) && !AssetDatabase.DeleteAsset(path)) cleanup.Add("could not remove fresh asset " + path);
                }
                for (int i = createdFolders.Count - 1; i >= 0; i--)
                {
                    string path = createdFolders[i];
                    if (Directory.Exists(Absolute(path)) && Directory.GetFileSystemEntries(Absolute(path)).Length == 0 && !AssetDatabase.DeleteAsset(path)) cleanup.Add("could not remove empty fresh folder " + path);
                }
                if (Directory.Exists(Absolute(OutputRoot))) cleanup.Add("incomplete output remains at " + OutputRoot + "; review it explicitly before any retry");
                throw new InvalidOperationException("Campaign prop bake failed; only newly tracked output was eligible for rollback. " + failure.Message + (cleanup.Count == 0 ? "" : " Cleanup needs review: " + string.Join("; ", cleanup)), failure);
            }
            finally
            {
                try { if (root) UnityEngine.Object.DestroyImmediate(root); }
                finally
                {
                    try { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
                    finally { foreach (var item in transientObjects) if (item && !EditorUtility.IsPersistent(item)) UnityEngine.Object.DestroyImmediate(item); }
                }
            }
        }

        static Mesh CreateMesh(Geometry.Recipe recipe, out int[] used)
        {
            var mesh = new Mesh { name = "CP_" + recipe.Name + "_Combined", indexFormat = IndexFormat.UInt16 };
            var vertices = new List<Vector3>(recipe.Vertices.Count); var normals = new List<Vector3>(recipe.Normals.Count); var uv = new List<Vector2>(recipe.UV.Count);
            foreach (var p in recipe.Vertices) vertices.Add(Vector(p)); foreach (var n in recipe.Normals) normals.Add(Vector(n)); foreach (var p in recipe.UV) uv.Add(new Vector2(p.X, p.Y));
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
            used = UsedPaints(recipe); mesh.subMeshCount = used.Length;
            for (int i = 0; i < used.Length; i++) mesh.SetTriangles(recipe.Triangles[used[i]], i, false);
            mesh.RecalculateBounds();
            // Keep authoring mesh readable so future idempotence checks inspect actual saved data.
            return mesh;
        }
        static Material CreatePaint(Geometry.Paint paint, Shader shader)
        {
            var material = new Material(shader) { name = "CP_" + paint.Name, enableInstancing = true, doubleSidedGI = false };
            material.SetColor("_BaseColor", new Color(paint.R, paint.G, paint.B, 1));
            material.SetFloat("_Metallic", paint.Metallic); material.SetFloat("_Smoothness", paint.Smoothness);
            material.SetFloat("_Surface", 0); material.SetFloat("_Cull", 2); material.SetFloat("_AlphaClip", 0);
            material.SetTexture("_BaseMap", null); material.SetTexture("_BumpMap", null);
            material.SetColor("_EmissionColor", new Color(paint.R, paint.G, paint.B, 1) * paint.Emission);
            // Unity 6.7 derives the visible emission keyword from RealtimeDirectEmission.
            // This changes material metadata only; no Light, scene GI bake or renderer flag.
            material.globalIlluminationFlags = ExpectedAuthoringGi(paint.Emission > 0);
            BaseShaderGUI.SetMaterialKeywords(material);
            return material;
        }
        // Exact authored state for this installed emission policy; reject other GI bits.
        static MaterialGlobalIlluminationFlags ExpectedAuthoringGi(bool luminous)
        {
            if(!luminous)return MaterialGlobalIlluminationFlags.EmissiveIsBlack;
#if UNITY_6000_7_OR_NEWER
            return MaterialGlobalIlluminationFlags.BakedEmission|MaterialGlobalIlluminationFlags.RealtimeDirectEmission;
#else
            return MaterialGlobalIlluminationFlags.BakedEmissive;
#endif
        }
        static void ValidateOwnedOutput(Geometry.Recipe[] recipes, Geometry.Paint[] paints)
        {
            if (!AssetDatabase.IsValidFolder(OutputRoot)) throw new InvalidOperationException("Existing directory is not a settled owned asset folder; no output was changed.");
            var manifest = AssetDatabase.LoadAssetAtPath<CampaignPropKitManifest>(ManifestPath);
            string expectedFingerprint = Geometry.Fingerprint(recipes, paints);
            if (!manifest || manifest.generatorId != Geometry.GeneratorId || manifest.recipeVersion != Geometry.RecipeVersion || manifest.geometryFingerprint != expectedFingerprint || manifest.paletteCount != Geometry.PaletteCount || manifest.props.Length != recipes.Length)
                throw new InvalidOperationException("Existing output lacks the exact matching recipe/provenance manifest. Refusing overwrite; choose a new explicit versioned kit instead.");
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ManifestPath };
            foreach (var paint in paints) allowed.Add(MaterialPath(paint));
            foreach (var recipe in recipes) { allowed.Add(MeshPath(recipe)); allowed.Add(PrefabPath(recipe)); }
            if (manifest.ownedAssets == null || manifest.ownedAssets.Length != allowed.Count - 1) throw new InvalidOperationException("Owned-asset inventory is missing or differs.");
            var ownershipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var owned in manifest.ownedAssets)
            {
                if (owned == null || !allowed.Contains(owned.path) || owned.path == ManifestPath || !ownershipPaths.Add(owned.path) ||
                    AssetDatabase.AssetPathToGUID(owned.path) != owned.guid || AssetDatabase.GetAssetDependencyHash(owned.path).ToString() != owned.dependencyHash)
                    throw new InvalidOperationException("An owned asset or its dependency state changed; refusing overwrite. Use a new explicit versioned output.");
            }
            int observedFiles = 0;
            foreach (string file in Directory.EnumerateFiles(Absolute(OutputRoot), "*", SearchOption.AllDirectories))
            {
                if (++observedFiles > 64) throw new InvalidOperationException("Existing output file bound exceeded; refusing modifications.");
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string path = OutputRoot + file.Substring(Absolute(OutputRoot).Length).Replace('\\', '/');
                if (!allowed.Contains(path)) throw new InvalidOperationException("Foreign file in the owned output: " + path + ". Nothing was changed.");
            }
            var materials = new Material[paints.Length];
            for (int i = 0; i < paints.Length; i++)
            {
                materials[i] = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(paints[i]));
                AssertPaint(materials[i], paints[i]);
            }
            int total = 0;
            for (int i = 0; i < recipes.Length; i++)
            {
                var recipe = recipes[i]; Geometry.Validate(recipe); Geometry.ValidateClosedPrimitives(recipe); total += recipe.TriangleCount;
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(recipe)); AssertMesh(mesh, recipe);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(recipe));
                if (!prefab || PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Regular) throw new InvalidOperationException("Expected a regular neutral prefab: " + recipe.Name);
                AssertNeutralPrefab(prefab, recipe, mesh, materials, UsedPaints(recipe));
                var record = manifest.props[i];
                if (record.name != recipe.Name || record.triangles != recipe.TriangleCount || record.vertices != recipe.Vertices.Count || record.meshPath != MeshPath(recipe) || record.prefabPath != PrefabPath(recipe)) throw new InvalidOperationException("Manifest record differs from the saved recipe: " + recipe.Name);
            }
            if (total != manifest.totalTriangles || total > Geometry.KitTriangleLimit) throw new InvalidOperationException("Saved kit triangle total is invalid.");
        }
        static void AssertMesh(Mesh mesh, Geometry.Recipe recipe)
        {
            int[] used = UsedPaints(recipe);
            if (!mesh || !mesh.isReadable || mesh.vertexCount != recipe.Vertices.Count || mesh.subMeshCount != used.Length) throw new InvalidOperationException("Existing mesh differs/unreadable: " + recipe.Name);
            var vertices = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
            if (normals.Length != vertices.Length || uv.Length != vertices.Length) throw new InvalidOperationException("Saved mesh channels differ.");
            for (int i = 0; i < vertices.Length; i++)
                if ((vertices[i] - Vector(recipe.Vertices[i])).sqrMagnitude > 1e-12f || (normals[i] - Vector(recipe.Normals[i])).sqrMagnitude > 1e-12f || (uv[i] - new Vector2(recipe.UV[i].X, recipe.UV[i].Y)).sqrMagnitude > 1e-12f) throw new InvalidOperationException("Saved mesh data was changed: " + recipe.Name);
            for (int s = 0; s < used.Length; s++)
            {
                int[] indices = mesh.GetTriangles(s); var expected = recipe.Triangles[used[s]];
                if (indices.Length != expected.Count) throw new InvalidOperationException("Saved index count changed.");
                for (int i = 0; i < indices.Length; i++) if (indices[i] != expected[i]) throw new InvalidOperationException("Saved topology changed.");
            }
            if ((mesh.bounds.min - Vector(recipe.Minimum)).sqrMagnitude > 1e-10f || (mesh.bounds.max - Vector(recipe.Maximum)).sqrMagnitude > 1e-10f) throw new InvalidOperationException("Saved mesh bounds differ.");
        }
        static void AssertPaint(Material material, Geometry.Paint paint)
        {
            if (!material || !material.shader || material.shader.name != ShaderName || !material.enableInstancing || material.GetTexture("_BaseMap") != null || material.GetTexture("_BumpMap") != null ||
                !Same(material.GetColor("_BaseColor"), new Color(paint.R, paint.G, paint.B, 1)) || !Same(material.GetColor("_EmissionColor"), new Color(paint.R, paint.G, paint.B, 1) * paint.Emission) ||
                !Near(material.GetFloat("_Metallic"), paint.Metallic) || !Near(material.GetFloat("_Smoothness"), paint.Smoothness) || material.IsKeywordEnabled("_EMISSION") != (paint.Emission > 0) || !Near(material.GetFloat("_Surface"), 0) || !Near(material.GetFloat("_Cull"), 2) || !Near(material.GetFloat("_AlphaClip"), 0))
                throw new InvalidOperationException("Existing palette was changed or is not this kit's exact original URP paint: " + paint.Name +
                    (material ? "; emission keyword=" + material.IsKeywordEnabled("_EMISSION") + ", expected=" + (paint.Emission > 0) + ", GI flags=" + material.globalIlluminationFlags +
                    ", base=" + material.GetColor("_BaseColor") + ", emission=" + material.GetColor("_EmissionColor") : "; material missing"));
        }
        static CampaignOwnedAssetRecord[] RecordOwnership(Geometry.Paint[] paints, Geometry.Recipe[] recipes)
        {
            var records = new List<CampaignOwnedAssetRecord>();
            foreach (var paint in paints) records.Add(Ownership(MaterialPath(paint)));
            foreach (var recipe in recipes) { records.Add(Ownership(MeshPath(recipe))); records.Add(Ownership(PrefabPath(recipe))); }
            return records.ToArray();
        }
        static CampaignOwnedAssetRecord Ownership(string path)
        {
            return new CampaignOwnedAssetRecord { path = path, guid = AssetDatabase.AssetPathToGUID(path), dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString() };
        }
        static void AssertNeutralPrefab(GameObject root, Geometry.Recipe recipe, Mesh mesh, Material[] palette, int[] used)
        {
            if (root.name != "CP_" + recipe.Name || root.transform.parent || root.transform.localPosition.sqrMagnitude > 1e-12f || Quaternion.Angle(root.transform.localRotation, Quaternion.identity) > .001f || (root.transform.localScale - Vector3.one).sqrMagnitude > 1e-12f || root.transform.childCount != recipe.Anchors.Count) throw new InvalidOperationException("Prefab root/pivot/hierarchy changed: " + recipe.Name);
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (!component || !(component is Transform) && !(component is MeshFilter) && !(component is MeshRenderer)) throw new InvalidOperationException("Nonneutral component found; no scripts, lights, audio, colliders or gameplay are allowed.");
            var filter = root.GetComponent<MeshFilter>(); var renderer = root.GetComponent<MeshRenderer>();
            if (!filter || filter.sharedMesh != mesh || !renderer || root.GetComponentsInChildren<MeshRenderer>(true).Length != 1) throw new InvalidOperationException("Prefab combined renderer differs.");
            var actual = renderer.sharedMaterials;
            if (actual.Length != used.Length) throw new InvalidOperationException("Prefab material count differs.");
            for (int i = 0; i < actual.Length; i++) if (actual[i] != palette[used[i]]) throw new InvalidOperationException("Prefab does not share the kit palette.");
            foreach (var anchor in recipe.Anchors)
            {
                var child = root.transform.Find(anchor.Name);
                if (!child || (child.localPosition - Vector(anchor.Position)).sqrMagnitude > 1e-12f || Quaternion.Angle(child.localRotation, Rotation(anchor.Rotation)) > .001f || (child.localScale - Vector3.one).sqrMagnitude > 1e-12f || child.GetComponents<Component>().Length != 1 || child.childCount != 0) throw new InvalidOperationException("Neutral attachment anchor differs: " + anchor.Name);
            }
        }
        static BuildReceipt Receipt(string status, Geometry.Recipe[] recipes, Geometry.Paint[] paints, bool wrote, bool idempotent)
        {
            var result = new BuildReceipt { status = status, outputRoot = OutputRoot, wroteAssets = wrote, idempotent = idempotent, sceneContentPlaced = false, paletteMaterials = paints.Length, geometryFingerprint = Geometry.Fingerprint(recipes, paints), originalSource = "Original pure mathematical recipes, no external models/images or paid generation.", props = new CampaignPropRecord[recipes.Length] };
            for (int i = 0; i < recipes.Length; i++)
            {
                var r = recipes[i]; var names = new string[r.Anchors.Count]; for (int a = 0; a < names.Length; a++) names[a] = r.Anchors[a].Name;
                result.totalTriangles += r.TriangleCount;
                result.props[i] = new CampaignPropRecord { name = r.Name, description = r.Description, meshPath = MeshPath(r), prefabPath = PrefabPath(r), triangles = r.TriangleCount, vertices = r.Vertices.Count, submeshes = UsedPaints(r).Length, primitives = r.Primitives.Count, closedPrimitiveEdges = Geometry.ValidateClosedPrimitives(r), minimum = Vector(r.Minimum), maximum = Vector(r.Maximum), anchorNames = names };
            }
            return result;
        }
        static int[] UsedPaints(Geometry.Recipe recipe) { var used = new List<int>(); for (int i = 0; i < Geometry.PaletteCount; i++) if (recipe.Triangles[i].Count > 0) used.Add(i); return used.ToArray(); }
        static string MaterialPath(Geometry.Paint paint) { return OutputRoot + "/Materials/CP_" + paint.Name + ".mat"; }
        static string MeshPath(Geometry.Recipe recipe) { return OutputRoot + "/Meshes/CP_" + recipe.Name + ".asset"; }
        static string PrefabPath(Geometry.Recipe recipe) { return OutputRoot + "/Prefabs/CP_" + recipe.Name + ".prefab"; }
        static void CreateAsset(UnityEngine.Object value, string path, List<string> created) { RequireUnused(path); AssetDatabase.CreateAsset(value, path); created.Add(path); }
        static void RequireUnused(string path) { if (!OwnedPath(path) || File.Exists(Absolute(path)) || AssetDatabase.LoadMainAssetAtPath(path)) throw new InvalidOperationException("Refusing to overwrite any existing/different output: " + path); }
        static bool OwnedPath(string path) { return path.StartsWith(OutputRoot + "/", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains("\\"); }
        static string Absolute(string path)
        {
            if (path != "Assets" && (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") || path.Contains("\\"))) throw new ArgumentException("Not a safe project asset path.");
            string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, path == "Assets" ? "" : path.Substring(7)));
            string assetRoot = Path.GetFullPath(Application.dataPath);
            if (absolute != assetRoot && !absolute.StartsWith(assetRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Path escaped project Assets.");
            for (var directory = new DirectoryInfo(Directory.Exists(absolute) ? absolute : Path.GetDirectoryName(absolute)); directory != null && directory.FullName.Length >= assetRoot.Length; directory = directory.Parent)
                if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Refusing an asset path through a junction/symbolic link.");
            return absolute;
        }
        static void EnsureFolder(string path, List<string> created)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (Directory.Exists(Absolute(path))) throw new InvalidOperationException("Unimported/foreign directory already occupies the requested output: " + path);
            int slash = path.LastIndexOf('/'); string parent = path.Substring(0, slash), name = path.Substring(slash + 1);
            EnsureFolder(parent, created);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, name))) throw new InvalidOperationException("Could not create output folder: " + path);
            created.Add(path);
        }
        static Vector3 Vector(Geometry.V3 p) { return new Vector3(p.X, p.Y, p.Z); }
        static Quaternion Rotation(Geometry.Rotation r) { return Quaternion.LookRotation(Vector(r.Z), Vector(r.Y)); }
        static bool Same(Color a, Color b) { return Near(a.r, b.r) && Near(a.g, b.g) && Near(a.b, b.b) && Near(a.a, b.a); }
        static bool Near(float a, float b) { return Mathf.Abs(a - b) < .00001f; }
        static void RequireStopped() { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Run only with Play stopped and compilation/imports settled."); }
    }
}
#endif

// # ARCHITECTURE SNAPSHOT
// Editor-only authored GI metadata; Unity metres/Y-up geometry and native memory/job owners remain unchanged.

// Coordinate Space: Existing authored Unity metres/Y-up; no geometry or pivot edits.
// Array Formats: Cold Editor material metadata and exact manifest hashes only.
// Dependencies/Pointers: Versioned URP11 emission policy; strict ownership/source/GUID checks remain; no runtime native owners changed.
