#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Cold Editor-only bridge in the runtime assembly, so the exporter can reuse
    // internal original recipes and exact procedural pivots without reflection.
    // This is never a prefab component, update owner, gameplay or mesh authority.
    public static class MonsterNeutralModelAuthoring
    {
        public enum Kind { GuardianGolem, Stalker, Shade, Spider }
        public const float CanonicalPresentationScale = 1.20f;
        public sealed class NeutralLease : IDisposable
        {
            public GameObject Root { get; private set; }
            MonsterVoxelCosmetics.Lease cosmetic;
            internal NeutralLease(GameObject root, MonsterVoxelCosmetics.Lease cosmetic)
            { Root = root; this.cosmetic = cosmetic; }
            public void Dispose()
            {
                cosmetic?.Dispose(); cosmetic = null;
                if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
                Root = null;
            }
        }

        public static NeutralLease Create(Kind kind, Material stone, Material joints, Material cyan, Material shadeMint, Scene authoringScene)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Neutral model authoring requires stopped Play mode.");
            if ((int)kind < 0 || (int)kind > 3) throw new ArgumentOutOfRangeException(nameof(kind));
            if (stone == null || joints == null || cyan == null || shadeMint == null)
                throw new ArgumentException("Four existing original palette materials are required.");
            if (!authoringScene.IsValid() || !authoringScene.isLoaded)
                throw new ArgumentException("A loaded isolated authoring scene is required.", nameof(authoringScene));
            if (!Mathf.Approximately(OracleEnemy.PresentationScale, CanonicalPresentationScale))
                throw new InvalidOperationException("The actual model scale changed; review a new library version.");
            GameObject root = null; MonsterVoxelCosmetics.Lease cosmetic = null;
            try
            {
                root = new GameObject("Oracle " + kind + " Neutral");
                SceneManager.MoveGameObjectToScene(root, authoringScene);
                Transform body = new GameObject("Body").transform; body.SetParent(root.transform, false);
                body.localScale = Vector3.one * CanonicalPresentationScale;
                Transform left = new GameObject("Left arm pivot").transform; left.SetParent(body, false);
                left.localPosition = new Vector3(-.53f, 1.36f, 0f);
                Transform right = new GameObject("Right arm pivot").transform; right.SetParent(body, false);
                right.localPosition = new Vector3(.53f, 1.36f, 0f);
                cosmetic = MonsterVoxelCosmetics.Create((MonsterVoxelCosmetics.Style)(int)kind, body, left, right,
                    stone, joints, kind == Kind.Shade ? shadeMint : cyan, null);
                // The exact actual003 constructor creates torso/head pivots and
                // preserves each part's world pose. No Tick or floor query runs.
                _ = new GroundedGolemMotion(root.transform, body, cosmetic.LeftLeg, cosmetic.RightLeg, left, right, cosmetic);
                Anchor(root.transform, "Feet", Vector3.zero);
                Anchor(root.transform, "Attack", Vector3.up * 1.2f);
                Anchor(root.transform, "SFX", Vector3.up * 1.2f);
                return new NeutralLease(root, cosmetic);
            }
            catch
            {
                cosmetic?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }
        static void Anchor(Transform parent, string name, Vector3 point)
        { var anchor = new GameObject(name).transform; anchor.SetParent(parent, false); anchor.localPosition = point; }
    }
}
#endif
