using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // Cold-only owner of four original template graphs. Their leases retain shared meshes.
    internal sealed class MonsterPoolTemplates015 : IDisposable
    {
        internal sealed class Template
        {
            internal MonsterVisualLease Lease;
            internal Transform Root;
            internal MonsterVoxelCosmetics.PartSpec[] Specs;
            internal Mesh[] Meshes;
            internal Material[] Materials;
            internal Bounds[] AuthorityBounds;
            internal int[] Parents;
            internal Vector3[] Positions;
            internal int LeftLeg, RightLeg;
        }
        internal readonly Template[] Styles = new Template[4];
        bool disposed;
        internal MonsterPoolTemplates015(Transform parent, Material stone, Material joints, Material turquoise, Material violet)
        {
            try
            {
                for (int index = 0; index < 4; index++)
                {
                    var style = (MonsterVoxelCosmetics.Style)index;
                    var t = Styles[index] = new Template { LeftLeg = -1, RightLeg = -1 };
                    t.Root = new GameObject("Retained original monster template " + index).transform;
                    t.Root.gameObject.SetActive(false); t.Root.SetParent(parent, false);
                    Transform left = Pivot(t.Root, "Left arm pivot", new Vector3(-.53f, 1.36f, 0f));
                    Transform right = Pivot(t.Root, "Right arm pivot", new Vector3(.53f, 1.36f, 0f));
                    t.Lease = MonsterVisualFactory.Create(style, t.Root, left, right, stone, joints,
                        style == MonsterVoxelCosmetics.Style.Shade ? violet : turquoise, null);
                    t.Specs = MonsterDetailSourceV2.Build(style);
                    int count = t.Specs.Length;
                    if (count < 1 || count > 17) throw new InvalidOperationException("Original part capacity changed.");
                    t.Meshes = new Mesh[count]; t.Materials = new Material[count]; t.AuthorityBounds = new Bounds[count];
                    t.Parents = new int[count]; t.Positions = new Vector3[count];
                    for (int part = 0; part < count; part++)
                    {
                        var spec = t.Specs[part]; Transform sourceParent = spec.Anchor == 1 ? left : spec.Anchor == 2 ? right : t.Root;
                        Transform source = sourceParent.Find(spec.Label);
                        if (source == null || source.localPosition != spec.Origin) throw new InvalidOperationException("Original template part changed.");
                        MeshFilter filter = source.GetComponent<MeshFilter>(); MeshRenderer renderer = source.GetComponent<MeshRenderer>();
                        if (filter == null || renderer == null || filter.sharedMesh == null) throw new InvalidOperationException("Original template mesh missing.");
                        t.Meshes[part] = filter.sharedMesh; t.Materials[part] = renderer.sharedMaterial;
                        t.AuthorityBounds[part] = SpiderLegPresentation009.AuthorityBounds(filter.sharedMesh);
                        int anchor = spec.Anchor;
                        if (style != MonsterVoxelCosmetics.Style.Spider && anchor == 0)
                        {
                            if (spec.Label == "Torso" || spec.Label == "Chest rune") anchor = 3;
                            else if (spec.Label == "Stone brow" || spec.Label == "Face shadow" || spec.Label == "Left rune eye" ||
                                spec.Label == "Right rune eye" || spec.Label == "Shade crown") anchor = 4;
                        }
                        t.Parents[part] = anchor;
                        t.Positions[part] = spec.Origin - (anchor == 3 ? Vector3.up * .65f : anchor == 4 ? Vector3.up * 1.48f : Vector3.zero);
                        if (spec.Label == "Left leg" || spec.Label == "Spider leg 0") t.LeftLeg = part;
                        if (spec.Label == "Right leg" || spec.Label == "Spider leg 1") t.RightLeg = part;
                    }
                    if (t.LeftLeg < 0 || t.RightLeg < 0) throw new InvalidOperationException("Original support legs missing.");
                }
            }
            catch { Dispose(); throw; }
        }
        internal static Transform Pivot(Transform parent, string label, Vector3 position)
        {
            var result = new GameObject(label).transform; result.SetParent(parent, false); result.localPosition = position; return result;
        }
        public void Dispose()
        {
            if (disposed) return;
            for (int i = 0; i < Styles.Length; i++)
            {
                Template t = Styles[i]; if (t == null) continue;
                t.Lease?.Dispose(); t.Lease = null;
                if (t.Root != null) UnityEngine.Object.Destroy(t.Root.gameObject);
            }
            disposed = true;
        }
    }

    internal sealed class MonsterPooledView015 : IDisposable
    {
        readonly MonsterPoolTemplates015 templates;
        readonly Transform body, leftArm, rightArm;
        readonly Transform[] parts = new Transform[17];
        readonly MeshFilter[] filters = new MeshFilter[17];
        readonly MeshRenderer[] renderers = new MeshRenderer[17];
        readonly Transform[] spiderLegs = new Transform[8];
        readonly Bounds[] spiderBounds = new Bounds[8];
        readonly List<Renderer> flashes;
        SpiderLegPresentation009 spider;
        MonsterPoolTemplates015.Template current;
        internal readonly Transform Spine, Head;
        internal bool IsSpider { get; private set; }
        internal Transform LeftLeg => parts[current.LeftLeg];
        internal Transform RightLeg => parts[current.RightLeg];
        internal Bounds LeftBounds => current.AuthorityBounds[current.LeftLeg];
        internal Bounds RightBounds => current.AuthorityBounds[current.RightLeg];
        bool disposed;

        internal MonsterPooledView015(Transform body, Transform leftArm, Transform rightArm,
            MonsterPoolTemplates015 templates, List<Renderer> flashes)
        {
            this.body = body; this.leftArm = leftArm; this.rightArm = rightArm; this.templates = templates; this.flashes = flashes;
            Spine = MonsterPoolTemplates015.Pivot(body, "Procedural torso pivot", Vector3.up * .65f);
            Head = MonsterPoolTemplates015.Pivot(Spine, "Procedural head pivot", Vector3.up * .83f);
            try
            {
                for (int i = 0; i < 17; i++)
                {
                    var go = new GameObject("Pooled original part " + i, typeof(MeshFilter), typeof(MeshRenderer));
                    parts[i] = go.transform; parts[i].SetParent(body, false);
                    filters[i] = go.GetComponent<MeshFilter>(); renderers[i] = go.GetComponent<MeshRenderer>();
                    renderers[i].reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                ApplyStyle(MonsterVoxelCosmetics.Style.Spider);
                for (int i = 0; i < current.Specs.Length; i++)
                    if (current.Specs[i].Label.StartsWith("Spider leg ", StringComparison.Ordinal))
                    {
                        int leg = int.Parse(current.Specs[i].Label.Substring(11));
                        spiderLegs[leg] = parts[i]; spiderBounds[leg] = current.AuthorityBounds[i];
                    }
                spider = new SpiderLegPresentation009(body, spiderLegs, spiderBounds);
                ApplyStyle(MonsterVoxelCosmetics.Style.GuardianGolem);
            }
            catch { Dispose(); throw; }
        }
        internal void ApplyStyle(MonsterVoxelCosmetics.Style style)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MonsterPooledView015));
            current = templates.Styles[(int)style]; IsSpider = style == MonsterVoxelCosmetics.Style.Spider;
            flashes.Clear(); body.localPosition = Vector3.zero; body.localRotation = Quaternion.identity;
            leftArm.localPosition = new Vector3(-.53f, 1.36f, 0f); rightArm.localPosition = new Vector3(.53f, 1.36f, 0f);
            leftArm.localRotation = rightArm.localRotation = Spine.localRotation = Head.localRotation = Quaternion.identity;
            for (int i = 0; i < 17; i++)
            {
                bool used = i < current.Specs.Length;
                if (!used) { filters[i].sharedMesh = null; renderers[i].SetPropertyBlock(null); parts[i].gameObject.SetActive(false); continue; }
                int anchor = current.Parents[i];
                Transform parent = anchor == 1 ? leftArm : anchor == 2 ? rightArm : anchor == 3 ? Spine : anchor == 4 ? Head : body;
                parts[i].SetParent(parent, false); parts[i].localPosition = current.Positions[i];
                parts[i].localRotation = Quaternion.identity; parts[i].localScale = Vector3.one;
                parts[i].gameObject.name = current.Specs[i].Label;
                filters[i].sharedMesh = current.Meshes[i]; renderers[i].sharedMaterial = current.Materials[i]; renderers[i].SetPropertyBlock(null);
                int channel = current.Specs[i].Channel;
                renderers[i].shadowCastingMode = channel == 2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderers[i].receiveShadows = channel != 2; renderers[i].lightProbeUsage = LightProbeUsage.BlendProbes;
                if (channel == 0) flashes.Add(renderers[i]); parts[i].gameObject.SetActive(true);
            }
        }
        internal void Tint(MaterialPropertyBlock scratch, MonsterElementPalette palette)
        {
            for (int i = 0; i < current.Specs.Length; i++)
            {
                int channel = current.Specs[i].Channel;
                MonsterElementPresentation.Tint(renderers[i], scratch,
                    channel == 0 ? palette.Stone : channel == 1 ? palette.Joints : palette.Rune, channel == 2 ? palette.Emission : 0f);
            }
            scratch.Clear();
        }
        internal void PoseSpider(float gait, float weight, bool flying) { if (IsSpider) spider.Pose(gait, weight, flying); }
        internal void SetNativeSpiderSole015(){if(IsSpider)spider.SetNativeGroundedSole015();}
        internal float LowestSpiderSole => spider.LowestSoleY;
        public void Dispose()
        {
            if (disposed) return;
            spider?.Dispose(); spider = null;
            // Borrowed flyweights are detached before the Combat owner releases template leases.
            for (int i = 0; i < 17; i++) if (filters[i] != null) filters[i].sharedMesh = null;
            disposed = true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original actor-local+Z/Y-up geometry;1.20 body scale stays with OracleEnemy; cached rigid pivot offsets preserve original art.
// Array Formats: Four retained template leases/52 original parts;48 universal17-part views;one fixed eight-leg native pose owner per view, no per-spawn graph or mesh clones.
// Dependencies/Pointers: Cold-only original V1/V2 factory/prototype leases; cached shared meshes/materials borrowed until all view jobs complete/detach; Combat owner destroys template roots last.
