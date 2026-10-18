using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OdysseyOracle.Editor
{
    public static class OdysseyProjectSetup
    {
        const string ScenePath = "Assets/Scenes/OracleTrial.unity";

        [MenuItem("Odyssey/Configure first-person project")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before configuring.");
            PlayerSettings.companyName = "ZeusRa";
            PlayerSettings.productName = "Infinite Odyssey: Oracle's Return";
            PlayerSettings.bundleVersion = "0.0.2";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 });
            PlayerSettings.defaultWebScreenWidth = 1600;
            PlayerSettings.defaultWebScreenHeight = 900;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.WebGPU, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.template = "PROJECT:Odyssey";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.WebGL.maximumMemorySize = 1024;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pipeline == null) throw new InvalidOperationException("3D URP template rendering asset is missing.");
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 2;
            pipeline.renderScale = 1f;
            pipeline.useSRPBatcher = true;
            pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
            pipeline.maxAdditionalLightsCount = 8;
            var rendering = new SerializedObject(pipeline);
            rendering.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            rendering.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            rendering.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            rendering.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            rendering.FindProperty("m_SoftShadowsSupported").boolValue = true;
            rendering.FindProperty("m_AnyShadowsSupported").boolValue = true;
            rendering.ApplyModifiedPropertiesWithoutUndo();
            var primaryRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            const string fallbackPath = "Assets/Settings/OracleForwardFallback.asset";
            var fallbackRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(fallbackPath);
            if (fallbackRenderer == null)
            {
                fallbackRenderer = UnityEngine.Object.Instantiate(primaryRenderer);
                fallbackRenderer.name = "Oracle Forward fallback";
                AssetDatabase.CreateAsset(fallbackRenderer, fallbackPath);
            }
            fallbackRenderer.renderingMode = RenderingMode.Forward;
            EditorUtility.SetDirty(fallbackRenderer);
            rendering.Update();
            var rendererList = rendering.FindProperty("m_RendererDataList");
            rendererList.arraySize = 2;
            rendererList.GetArrayElementAtIndex(0).objectReferenceValue = primaryRenderer;
            rendererList.GetArrayElementAtIndex(1).objectReferenceValue = fallbackRenderer;
            rendering.ApplyModifiedPropertiesWithoutUndo();
            pipeline.mainLightShadowmapResolution = 2048;
            pipeline.additionalLightsShadowmapResolution = 2048;
            pipeline.shadowDistance = 35f;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            RetainRuntimeShaders();
            ConfigureAudioImports();
            const string tutorialSkyPath = "Assets/Resources/OracleTutorialNightSky.mat";
            var tutorialCube = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Night Skybox Cubemap for tutorial.png");
            var tutorialSkyShader = Shader.Find("Skybox/Cubemap");
            if (tutorialCube != null && tutorialSkyShader != null)
            {
                var tutorialSky = AssetDatabase.LoadAssetAtPath<Material>(tutorialSkyPath);
                if (tutorialSky == null)
                {
                    tutorialSky = new Material(tutorialSkyShader) { name = "Oracle tutorial night sky" };
                    AssetDatabase.CreateAsset(tutorialSky, tutorialSkyPath);
                }
                tutorialSky.SetTexture("_Tex", tutorialCube);
                tutorialSky.SetFloat("_Exposure", .8f);
                EditorUtility.SetDirty(tutorialSky);
            }
            const string logoPath = "Assets/Resources/Interface/OracleReturnLogo.png";
            var logoImporter = AssetImporter.GetAtPath(logoPath) as TextureImporter;
            if (logoImporter != null)
            {
                logoImporter.textureType = TextureImporterType.Sprite;
                logoImporter.spriteImportMode = SpriteImportMode.Single;
                logoImporter.alphaIsTransparency = true;
                logoImporter.mipmapEnabled = false;
                logoImporter.maxTextureSize = 2048;
                logoImporter.SaveAndReimport();
            }
            const string atmospherePath = "Assets/Resources/OracleAtmosphere.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(atmospherePath) == null)
            {
                var atmosphere = GameSession.CreateAtmosphereProfile();
                AssetDatabase.CreateAsset(atmosphere, atmospherePath);
                foreach (var effect in atmosphere.components) AssetDatabase.AddObjectToAsset(effect, atmosphere);
                EditorUtility.SetDirty(atmosphere);
            }
            else
            {
                var atmosphere = AssetDatabase.LoadAssetAtPath<VolumeProfile>(atmospherePath);
                if (atmosphere.TryGet<ColorAdjustments>(out var grade))
                {
                    grade.postExposure.Override(.65f);
                    grade.contrast.Override(0f);
                    grade.saturation.Override(0f);
                    EditorUtility.SetDirty(grade);
                }
                if (atmosphere.TryGet<Vignette>(out var vignette))
                {
                    vignette.intensity.Override(.08f);
                    EditorUtility.SetDirty(vignette);
                }
                EditorUtility.SetDirty(atmosphere);
            }
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Infinite Odyssey — Oracle", typeof(GameSession));
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save Oracle scene.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ODYSSEY_CONFIGURED " + Application.unityVersion + " WebGPU, WebGL2 fallback, URP, OracleTrial.");
        }

        static void RetainRuntimeShaders()
        {
            // Procedural world materials have no scene references; Resources keeps their shaders in the player.
            const string folder = "Assets/Resources/OdysseyShaderRetention";
            Directory.CreateDirectory(folder);
            foreach (string name in new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit", "UI/Default", "Sprites/Default" })
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;
                string stem = name.Replace('/', '_').Replace(' ', '_');
                for (int variant = 0; variant < 3; variant++)
                {
                    string path = folder + "/" + stem + "_" + variant + ".mat";
                    // Existing retained assets must be repaired too: older imports can
                    // lose local emission keywords while keeping their HDR color.
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    bool create = material == null;
                    if (create) material = new Material(shader);
                    material.enableInstancing = true;
                    if (variant > 0 && material.HasProperty("_EmissionColor") &&
                        shader.keywordSpace.FindKeyword("_EMISSION").isValid)
                    {
                        material.SetColor("_EmissionColor", Color.white * 2f);
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                        material.EnableKeyword("_EMISSION");
                    }
                    if (variant == 2 && material.HasProperty("_Surface"))
                    {
                        material.SetFloat("_Surface", 1);
                        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_ZWrite", 0);
                        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        material.renderQueue = (int)RenderQueue.Transparent;
                    }
                    if (create) AssetDatabase.CreateAsset(material, path);
                    else EditorUtility.SetDirty(material);
                }
            }
        }

        public static void RepairRuntimeShaderRetention()
        {
            if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Stop Play and builds before repairing retained shader materials.");
            RetainRuntimeShaders();
            AssetDatabase.SaveAssets();
        }

        static void ConfigureAudioImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio", "Assets/Resources/Tutorial" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;
                var settings = importer.defaultSampleSettings;
                bool music = path.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("shattered_olympus", StringComparison.OrdinalIgnoreCase) >= 0;
                settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = music ? .55f : .65f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !music;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
            }
        }

        [MenuItem("Odyssey/Switch to Web")]
        public static void SwitchToWeb()
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Unity could not activate its installed Web module.");
            AssetDatabase.SaveAssets();
            Debug.Log("ODYSSEY_WEB_TARGET_READY");
        }

        [MenuItem("Odyssey/Build website")]
        public static void BuildWeb()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Switch to Web first, wait for import, then build.");
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../WebBuild"));
            Directory.CreateDirectory(output);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            // Quality overrides must already be persistent/live here. A prior
            // Play/import shader query can leave the public SRP build-data cache
            // holding an asset wrapper that has since been destroyed/reimported.
            // Refresh the cache before the build starts; never dispose it during
            // BuildPipeline.BuildPlayer or mutate package internals.
            UnityEditor.Rendering.CoreBuildData.instance.Dispose();
            var report = BuildPipeline.BuildPlayer(options);
            var receipt = new BuildReceipt
            {
                unityVersion = Application.unityVersion,
                result = report.summary.result.ToString(),
                bytes = report.summary.totalSize.ToString(),
                errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings,
                utc = DateTime.UtcNow.ToString("o"),
                graphicsAPIs = string.Join(", ", PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL).Select(api => api.ToString())),
                outputPath = output
            };
            File.WriteAllText(Path.Combine(output, "build-receipt.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log("ODYSSEY_WEB_BUILD " + JsonUtility.ToJson(receipt));
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Web build failed; see build receipt and console.");
        }

        [Serializable]
        sealed class BuildReceipt
        {
            public string unityVersion, result, bytes, utc, graphicsAPIs, outputPath;
            public int errors, warnings;
        }
    }
}
