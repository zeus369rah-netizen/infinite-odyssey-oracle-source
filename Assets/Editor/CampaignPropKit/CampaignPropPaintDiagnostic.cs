#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Geometry = OdysseyOracle.CampaignLibrary.Authoring.CampaignPropGeometry;

namespace OdysseyOracle.CampaignLibrary.Authoring.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit in-memory diagnostic. Public URP validation only; no asset/scene writes,
    // no automatic execution, reflection, paid generation or installed source changes.
    public static class CampaignPropPaintDiagnostic
    {
        [Serializable] sealed class Report
        {
            public string shader, installedNormalization;
            public bool outputFolderExists, wroteAssets;
            public PaintResult[] paints;
        }
        [Serializable] sealed class PaintResult
        {
            public string name, legacyFlagsAfter, correctedFlagsAfter;
            public bool expectedEmission, legacyKeywordBefore, legacyKeywordAfter;
            public bool correctedKeywordBefore, correctedKeywordAfter, colorsPreserved;
            public Color expectedBase, expectedEmissionColor, actualBase, actualEmissionColor;
        }

        public static string DiagnosePaints()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Run the paint diagnostic with Play stopped and imports settled.");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Installed URP Lit shader is unavailable.");
            var palette = Geometry.Palette();
            var report = new Report { shader = shader.name, outputFolderExists = AssetDatabase.IsValidFolder(CampaignPropKitBuilder.OutputRoot),
                wroteAssets = false, installedNormalization = "Public UnityEditor.BaseShaderGUI.SetMaterialKeywords", paints = new PaintResult[palette.Length] };
            for (int i = 0; i < palette.Length; i++)
            {
                var paint = palette[i]; Material legacy = null, corrected = null;
                try
                {
                    legacy = Create(paint, shader, false); corrected = Create(paint, shader, true);
                    var result = new PaintResult { name = paint.Name, expectedEmission = paint.Emission > 0,
                        expectedBase = new Color(paint.R, paint.G, paint.B, 1), expectedEmissionColor = new Color(paint.R, paint.G, paint.B, 1) * paint.Emission,
                        legacyKeywordBefore = legacy.IsKeywordEnabled("_EMISSION"), correctedKeywordBefore = corrected.IsKeywordEnabled("_EMISSION") };
                    BaseShaderGUI.SetMaterialKeywords(legacy);
                    BaseShaderGUI.SetMaterialKeywords(corrected);
                    result.legacyKeywordAfter = legacy.IsKeywordEnabled("_EMISSION"); result.legacyFlagsAfter = legacy.globalIlluminationFlags.ToString();
                    result.correctedKeywordAfter = corrected.IsKeywordEnabled("_EMISSION"); result.correctedFlagsAfter = corrected.globalIlluminationFlags.ToString();
                    result.actualBase = corrected.GetColor("_BaseColor"); result.actualEmissionColor = corrected.GetColor("_EmissionColor");
                    result.colorsPreserved = Same(result.expectedBase, result.actualBase) && Same(result.expectedEmissionColor, result.actualEmissionColor);
                    report.paints[i] = result;
                }
                finally
                {
                    if (legacy) UnityEngine.Object.DestroyImmediate(legacy);
                    if (corrected) UnityEngine.Object.DestroyImmediate(corrected);
                }
            }
            return JsonUtility.ToJson(report, true);
        }

        static Material Create(Geometry.Paint paint, Shader shader, bool correct)
        {
            var material = new Material(shader) { name = "TransientPaintDiagnostic", hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_BaseColor", new Color(paint.R, paint.G, paint.B, 1));
            material.SetColor("_EmissionColor", new Color(paint.R, paint.G, paint.B, 1) * paint.Emission);
            material.SetFloat("_Metallic", paint.Metallic); material.SetFloat("_Smoothness", paint.Smoothness);
            material.SetFloat("_Surface", 0); material.SetFloat("_Cull", 2); material.SetFloat("_AlphaClip", 0);
            if (paint.Emission > 0) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = correct ? (paint.Emission > 0 ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack) : MaterialGlobalIlluminationFlags.None;
            return material;
        }
        static bool Same(Color a, Color b)
        {
            return Mathf.Abs(a.r-b.r)<.00001f && Mathf.Abs(a.g-b.g)<.00001f && Mathf.Abs(a.b-b.b)<.00001f && Mathf.Abs(a.a-b.a)<.00001f;
        }
    }
}
#endif
