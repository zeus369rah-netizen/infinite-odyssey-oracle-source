using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine;

namespace OdysseyOracle
{
    internal static class MonsterElementPresentation
    {
        // Saved families draw at most17 parts plus health, alert, telegraph and two fins.
        static readonly List<Renderer> scratchRenderers = new List<Renderer>(32);
        internal static Color ColorOf(float3 value) => new Color(value.x, value.y, value.z, 1f);

        // Admission-only presentation enumeration, never a spatial query or per-frame operation.
        internal static void Apply(Transform root, Material stone, Material joints, Material rune,
            MaterialPropertyBlock scratch, MonsterElementPalette palette)
        {
            root.GetComponentsInChildren(true, scratchRenderers);
            for (int i = 0; i < scratchRenderers.Count; i++)
            {
                Renderer renderer = scratchRenderers[i];
                Material source = renderer.sharedMaterial;
                if (source == stone) Tint(renderer, scratch, palette.Stone, 0f);
                else if (source == joints) Tint(renderer, scratch, palette.Joints, 0f);
                else if (source == rune) Tint(renderer, scratch, palette.Rune, palette.Emission);
            }
            scratchRenderers.Clear();
            scratch.Clear();
        }

        internal static void Tint(Renderer renderer, MaterialPropertyBlock scratch, float3 color, float emission)
        {
            scratch.Clear();
            Color value = ColorOf(color);
            scratch.SetColor("_BaseColor", value);
            scratch.SetColor("_Color", value);
            scratch.SetColor("_EmissionColor", new Color(value.r * emission, value.g * emission, value.b * emission, 1f));
            renderer.SetPropertyBlock(scratch);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Main-thread cold presentation only: existing local pivots and saved V1/V2 meshes remain exact.
// Caller lends one MPB and three shared materials; renderers copy the block, no materials are cloned.
// A reused32-capacity renderer list is cold scratch, cleared after admission; no Update,
// spatial loop, per-enemy renderer array or native memory owner is added.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing metre/Y-up/XZ routes, saved art local+Z with existing1.20 scale only.
// Array Formats: Four fixed numeric tower value slots; stable-ID four-theme palette; no new spatial/native storage.
// Dependencies/Pointers: Main-thread registry/LOS adapter is separate from pure math; V1 owns pivots/gait and composite V2 lease owns disposal; host HP/damage/projectile authority remains unchanged.
