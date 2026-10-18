using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>Original bounded voxel embers and soft rising smoke, visible in the live opening.</summary>
    public sealed class CampfireAtmosphere : MonoBehaviour
    {
        Material flame, smoke;
        GameSession session;
        Transform effects;
        public void Initialize(GameSession owner)
        {
            session = owner;
            effects = new GameObject("Campfire embers and smoke").transform;
            effects.SetParent(transform, false);
            effects.position = new Vector3(-4f, .35f, 5.25f);
            flame = Paint("Original voxel ember", true);
            smoke = Paint("Original hearth smoke", false);
            Create("Glowing embers", flame, false);
            Create("Drifting smoke", smoke, true);
        }
        Material Paint(string label, bool additive)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.name = label;
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            return material;
        }
        void Create(string label, Material paint, bool isSmoke)
        {
            var go = new GameObject(label);
            go.transform.SetParent(effects, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = isSmoke ? 20 : 24;
            main.startLifetime = isSmoke ? 3.8f : 1.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(.4f, isSmoke ? .75f : 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(isSmoke ? .16f : .025f, isSmoke ? .34f : .055f);
            main.startColor = isSmoke ? new Color(.27f,.30f,.32f,.2f) : new Color(2.5f,1.05f,.12f,1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission; emission.rateOverTime = isSmoke ? 4f : 10f;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = .18f; shape.angle = 8f; shape.rotation = new Vector3(-90f,0,0);
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f,1f,1f,isSmoke ? 2.7f : .2f));
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.16f),new GradientAlphaKey(0,1)});
            color.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = paint;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            particles.Play();
        }
        void Update() { if (session != null && effects != null) effects.gameObject.SetActive(!session.World.IsCoopArena); }
        void OnDestroy() { if (flame != null) Destroy(flame); if (smoke != null) Destroy(smoke); }
    }
}
