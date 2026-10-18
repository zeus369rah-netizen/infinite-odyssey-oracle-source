#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace OdysseyOracle.Editor
{
    /// <summary>Explicit root-run reference renderer. Never initializes a smith, scene or game session.</summary>
    public static class OracleAnvilReferenceFixture
    {
        const int Size = 512, Steel = 7;
        const string NpcSourceSha256 = "C782C30A1279E4FE66D37A525C04EC4CAA06471AF7B202DB19482415F6EF9CE4";
        [Serializable] public sealed class MetalBox
        {
            public string name;
            public Vector3 originalNpcLocalCenter, size;
            public float bevel;
        }
        [Serializable] public sealed class Receipt
        {
            public string status = "not rendered", utc, pngPath = "", pngSha256 = "", sourceSha256 = "", exception = "";
            public int width, height, steelBoxes, vertices, triangles, opaquePixels;
            public int[] cornerAlpha;
            public Vector3 originalWorkstationAnchor, originalBoundsCenter, originalBoundsSize, previewTranslation;
            public Vector3 cameraPosition, cameraEuler;
            public float cameraFieldOfView;
            public Rect pixelContentBounds;
            public bool convertedLinearToSrgb, previewCleaned, assetsImported, unityAiInvoked;
            public MetalBox[] boxes;
        }

        // Coordinates, bevels and material index are exactly the steel-only anvil
        // calls in NpcBlacksmith.BuildForgeAndAnvil. The source mesh builder and
        // hammered-grain material are invoked through reflection, not rewritten.
        public static MetalBox[] OriginalMetalBoxes()
        {
            var anchor = new Vector3(1.10f, 0, .12f);
            var boxes = new MetalBox[8];
            boxes[0] = Spec("Metal baseplate", anchor + Vector3.up * .576f, new Vector3(.57f, .067f, .41f), .011f);
            boxes[1] = Spec("Metal waist", anchor + Vector3.up * .690f, new Vector3(.29f, .192f, .29f), .017f);
            boxes[2] = Spec("Working face", anchor + Vector3.up * .817f, new Vector3(.65f, .104f, .39f), .011f);
            for (var index = 0; index < 4; index++)
                boxes[3 + index] = Spec("Horn step " + index,
                    anchor + new Vector3(.338f + index * .057f, .821f + index * .002f, 0),
                    new Vector3(.075f, .094f - index * .019f, .30f - index * .067f), .007f);
            boxes[7] = Spec("Rear heel", anchor + new Vector3(-.348f, .819f, 0), new Vector3(.096f, .109f, .37f), .008f);
            return boxes;
        }
        static MetalBox Spec(string name, Vector3 center, Vector3 size, float bevel)
            => new MetalBox { name = name, originalNpcLocalCenter = center, size = size, bevel = bevel };

        public static string RenderReference()
        {
            var receipt = new Receipt { utc = DateTime.UtcNow.ToString("o"), width = Size, height = Size,
                originalWorkstationAnchor = new Vector3(1.10f, 0, .12f), boxes = OriginalMetalBoxes() };
            PreviewRenderUtility preview = null;
            GameObject root = null;
            Mesh mesh = null;
            Texture2D image = null;
            var oldTarget = RenderTexture.active;
            var opened = false;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                    throw new InvalidOperationException("Run only with Play stopped, compilation and asset updating settled.");
                var source = Path.Combine(Application.dataPath, "Scripts/NpcBlacksmith.cs");
                receipt.sourceSha256 = Hash(File.ReadAllBytes(source));
                if (!string.Equals(receipt.sourceSha256, NpcSourceSha256, StringComparison.Ordinal))
                    throw new InvalidOperationException("NpcBlacksmith source changed. Re-audit exact anvil boxes/material before rendering a reference.");
                preview = new PreviewRenderUtility { ambientColor = new Color(.38f, .40f, .43f, 1) };
                root = new GameObject("Isolated current anvil metal only") { hideFlags = HideFlags.HideAndDontSave };
                preview.AddSingleGO(root);
                // This empty component supplies its original private material functions.
                // Initialize is never called: no body, stand, forge, collider or animation is created.
                var sourceOwner = root.AddComponent<NpcBlacksmith>();
                var npc = typeof(NpcBlacksmith);
                const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                var pattern = npc.GetMethod("Pattern", privateInstance) ?? throw new MissingMethodException("NpcBlacksmith.Pattern");
                var paint = npc.GetMethod("Paint", privateInstance) ?? throw new MissingMethodException("NpcBlacksmith.Paint");
                var grain = (Texture2D)pattern.Invoke(sourceOwner, new object[] { "Smith original hammered grain", 32, 0 });
                var metal = (Material)paint.Invoke(sourceOwner, new object[] {
                    "Hammer and anvil steel", new Color(.27f, .29f, .28f), .66f, .38f, grain });
                var modelType = npc.GetNestedType("Model", BindingFlags.NonPublic) ?? throw new MissingMemberException("NpcBlacksmith.Model");
                var model = Activator.CreateInstance(modelType, true);
                var boxMethod = modelType.GetMethod("Box", BindingFlags.Instance | BindingFlags.Public) ?? throw new MissingMethodException("Model.Box");
                var finish = modelType.GetMethod("Finish", BindingFlags.Instance | BindingFlags.Public) ?? throw new MissingMethodException("Model.Finish");
                var originalBounds = new Bounds(receipt.boxes[0].originalNpcLocalCenter, receipt.boxes[0].size);
                foreach (var box in receipt.boxes)
                {
                    originalBounds.Encapsulate(new Bounds(box.originalNpcLocalCenter, box.size));
                    boxMethod.Invoke(model, new object[] { box.originalNpcLocalCenter, box.size, box.bevel, Steel, null });
                }
                var palette = new Material[10]; palette[Steel] = metal;
                var finishArgs = new object[] { "Exact current anvil steel, eight beveled boxes", palette, null };
                mesh = (Mesh)finish.Invoke(model, finishArgs);
                receipt.steelBoxes = receipt.boxes.Length;
                receipt.vertices = mesh.vertexCount;
                receipt.triangles = (int)mesh.GetIndexCount(0) / 3;
                receipt.originalBoundsCenter = mesh.bounds.center;
                receipt.originalBoundsSize = mesh.bounds.size;
                if ((mesh.bounds.center - originalBounds.center).sqrMagnitude > .00000001f
                    || (mesh.bounds.size - originalBounds.size).sqrMagnitude > .00000001f || mesh.subMeshCount != 1
                    || receipt.steelBoxes != 8 || receipt.vertices != 768 || receipt.triangles != 352)
                    throw new InvalidOperationException("Original mesh builder output differs from the eight-box source recipe.");
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = (Material[])finishArgs[2];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                receipt.previewTranslation = -originalBounds.center;
                root.transform.position = receipt.previewTranslation;
                var camera = preview.camera;
                camera.allowHDR = false; camera.allowMSAA = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.fieldOfView = 35; camera.nearClipPlane = .01f; camera.farClipPlane = 20;
                var distance = originalBounds.extents.magnitude / Mathf.Sin(camera.fieldOfView * .5f * Mathf.Deg2Rad) * 1.25f;
                camera.transform.position = new Vector3(1.35f, .90f, -1.70f).normalized * distance;
                camera.transform.LookAt(Vector3.zero, Vector3.up);
                var cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.requiresColorTexture = cameraData.requiresDepthTexture = false;
                var lights = preview.lights;
                lights[0].transform.rotation = Quaternion.Euler(35, -35, 0); lights[0].intensity = 1.25f;
                lights[1].transform.rotation = Quaternion.Euler(320, 145, 0); lights[1].intensity = .65f;
                foreach (var light in lights) light.shadows = LightShadows.None;
                var points = Size / Mathf.Max(.01f, EditorGUIUtility.pixelsPerPoint);
                preview.BeginPreview(new Rect(0, 0, points, points), GUIStyle.none); opened = true;
                camera.pixelRect = new Rect(0, 0, Size, Size);
                preview.Render(true, false);
                var target = preview.EndPreview() as RenderTexture; opened = false;
                if (target == null || target.width != Size || target.height != Size)
                    throw new InvalidOperationException("Native preview target did not produce the exact 512-square reference.");
                RenderTexture.active = target;
                image = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
                image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); image.Apply(false, false);
                var pixels = image.GetPixels32();
                receipt.cornerAlpha = new[] { (int)pixels[0].a, pixels[Size - 1].a, pixels[(Size - 1) * Size].a, pixels[Size * Size - 1].a };
                var minimumX = Size; var minimumY = Size; var maximumX = -1; var maximumY = -1;
                receipt.convertedLinearToSrgb = QualitySettings.activeColorSpace == ColorSpace.Linear && !target.sRGB;
                for (var index = 0; index < pixels.Length; index++)
                {
                    var pixel = pixels[index];
                    if (pixel.a > 0)
                    {
                        receipt.opaquePixels++;
                        var x = index % Size; var y = index / Size;
                        minimumX = Math.Min(minimumX, x); minimumY = Math.Min(minimumY, y);
                        maximumX = Math.Max(maximumX, x); maximumY = Math.Max(maximumY, y);
                    }
                    if (receipt.convertedLinearToSrgb)
                    {
                        pixel.r = GammaByte(pixel.r); pixel.g = GammaByte(pixel.g); pixel.b = GammaByte(pixel.b);
                        pixels[index] = pixel;
                    }
                }
                foreach (var alpha in receipt.cornerAlpha) if (alpha != 0) throw new InvalidOperationException("Reference corners must be transparent.");
                if (receipt.opaquePixels < 1024 || minimumX < 16 || minimumY < 16 || maximumX > Size - 17 || maximumY > Size - 17)
                    throw new InvalidOperationException("Reference is empty or clips the required transparent margin.");
                receipt.pixelContentBounds = Rect.MinMaxRect(minimumX, minimumY, maximumX + 1, maximumY + 1);
                if (receipt.convertedLinearToSrgb) { image.SetPixels32(pixels); image.Apply(false, false); }
                receipt.cameraPosition = camera.transform.position; receipt.cameraEuler = camera.transform.eulerAngles; receipt.cameraFieldOfView = camera.fieldOfView;
                var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
                var directory = Path.GetFullPath(Path.Combine(repoRoot, "work/post-publication/anvil-reference"));
                if (!directory.StartsWith(repoRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Reference destination escaped the workspace.");
                Directory.CreateDirectory(directory);
                var bytes = image.EncodeToPNG();
                receipt.pngPath = Path.Combine(directory, "AnvilMetalReference.png");
                File.WriteAllBytes(receipt.pngPath, bytes); receipt.pngSha256 = Hash(bytes); receipt.status = "rendered; root visual acceptance and import pending";
            }
            catch (Exception exception) { receipt.status = "failed; no reference accepted"; receipt.exception = exception.ToString(); }
            finally
            {
                if (opened && preview != null) preview.EndPreview();
                RenderTexture.active = oldTarget;
                preview?.Cleanup();
                if (root != null) Object.DestroyImmediate(root);
                if (mesh != null) Object.DestroyImmediate(mesh);
                if (image != null) Object.DestroyImmediate(image);
                receipt.previewCleaned = true;
            }
            return JsonUtility.ToJson(receipt, true);
        }
        static byte GammaByte(byte channel) => (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.LinearToGammaSpace(channel / 255f)) * 255f);
        static string Hash(byte[] bytes)
        {
            using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "");
        }
    }
    // # ARCHITECTURE SNAPSHOT: Editor-only bounded 512 RGBA preview of eight
    // source-space metal boxes. Original Npc Model/Pattern/Paint own geometry and
    // materials; temporary preview owns only its objects. No game initialization,
    // save mutation, scene serialization, asset import, UI automation or AI call.
}
#endif
