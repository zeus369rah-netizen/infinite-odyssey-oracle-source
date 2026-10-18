using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Presentation-only adapter: fixed two-slot CPU clock/geometry arrays, two owned
    // meshes/render objects, one borrowed retained emissive material in five slots.
    // Optional polish uses the geometry writer's larger fixed buffers; all pool,
    // upload, lifetime, quality admission, palette and accepted-event APIs are exact.
    // Unity object calls stay at upload/render boundaries, outside pure geometry math.
    // No damage, physics, lights, particles, shader lookup, jobs or native containers.
    // Play/Tick contain no deliberate managed allocation; Unity API allocation cost
    // and runtime CPU/GPU performance have not been measured by these source checks.
    [DisallowMultipleComponent]
    public sealed class CosmicNovaVfx : MonoBehaviour
    {
        private sealed class Burst
        {
            public GameObject Root;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public CosmicNovaVertex[] Vertices;
            public ushort[] Indices;
            public int[] Starts, Counts;
            public MaterialPropertyBlock[] Blocks;
        }
        private Burst[] bursts;
        private CosmicNovaPoolClock clock;
        private Material borrowedMaterial;
        private Material[] materialSlots;
        private bool initialized,disposed,hasBaseColor,hasColor,hasEmission;
        private int baseColorId,colorId,emissionId;
        public int ActiveBurstCount=>clock!=null?clock.ActiveCount:0;

        // Caller supplies the existing retained white/emissive FX material (Combat.pale).
        // The caller owns its lifetime. This component never changes/destroys that material.
        public void Initialize(Material sharedFxMaterial)
        {
            if(disposed||sharedFxMaterial==null)return;
            if(initialized&&borrowedMaterial==sharedFxMaterial)return;
            borrowedMaterial=sharedFxMaterial;
            baseColorId=Shader.PropertyToID("_BaseColor");colorId=Shader.PropertyToID("_Color");emissionId=Shader.PropertyToID("_EmissionColor");
            hasBaseColor=borrowedMaterial.HasProperty(baseColorId);hasColor=borrowedMaterial.HasProperty(colorId);
            hasEmission=borrowedMaterial.HasProperty(emissionId)&&borrowedMaterial.IsKeywordEnabled("_EMISSION");
            if(!initialized)
            {
                clock=new CosmicNovaPoolClock();
                bursts=new Burst[CosmicNovaPoolClock.Capacity];
                materialSlots=new Material[CosmicNovaGeometry.PaletteCount];
                var layout=new[]{new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3)};
                for(int slot=0;slot<bursts.Length;slot++)
                {
                    var burst=new Burst
                    {
                        // Unparented world-space pool avoids inherited player/root scale changing the advertised AoE.
                        Root=new GameObject("Pooled cosmic nova "+slot),
                        Mesh=new Mesh{name="Fixed cosmic nova geometry "+slot},
                        Vertices=new CosmicNovaVertex[CosmicNovaGeometry.MaximumVertices],
                        Indices=new ushort[CosmicNovaGeometry.MaximumIndices],
                        Starts=new int[CosmicNovaGeometry.PaletteCount],Counts=new int[CosmicNovaGeometry.PaletteCount],
                        Blocks=new MaterialPropertyBlock[CosmicNovaGeometry.PaletteCount]
                    };
                    bursts[slot]=burst;
                    burst.Root.AddComponent<MeshFilter>().sharedMesh=burst.Mesh;
                    burst.Renderer=burst.Root.AddComponent<MeshRenderer>();
                    burst.Renderer.shadowCastingMode=ShadowCastingMode.Off;burst.Renderer.receiveShadows=false;
                    burst.Renderer.lightProbeUsage=LightProbeUsage.Off;burst.Renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    burst.Renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
                    burst.Renderer.enabled=false;
                    burst.Mesh.MarkDynamic();
                    burst.Mesh.SetVertexBufferParams(CosmicNovaGeometry.MaximumVertices,layout);
                    burst.Mesh.SetIndexBufferParams(CosmicNovaGeometry.MaximumIndices,IndexFormat.UInt16);
                    burst.Mesh.subMeshCount=CosmicNovaGeometry.PaletteCount;
                    burst.Mesh.bounds=new Bounds(Vector3.zero,Vector3.one*2f);
                    for(int band=0;band<burst.Blocks.Length;band++)burst.Blocks[band]=new MaterialPropertyBlock();
                }
                initialized=true;
            }
            ResetBursts();
            for(int band=0;band<materialSlots.Length;band++)materialSlots[band]=borrowedMaterial;
            for(int slot=0;slot<bursts.Length;slot++)bursts[slot].Renderer.sharedMaterials=materialSlots;
        }

        public void Play(Vector3 worldCenter,float radius,int quality)
        {
            if(!initialized||disposed||!isActiveAndEnabled||borrowedMaterial==null||
                !CosmicNovaGeometry.ValidCenter(worldCenter.x,worldCenter.y,worldCenter.z))return;
            GameSession game=GameSession.Instance;
            if(game!=null&&!game.SimulationActive)return;
            if(game!=null&&game.Graphics!=null)quality=CosmicNovaGeometry.CapQuality(quality,game.Graphics.EffectiveParticleFactor);
            int slot=clock.Begin(radius,quality);
            if(slot<0)return;
            Burst burst=bursts[slot];
            burst.Root.transform.SetPositionAndRotation(worldCenter,Quaternion.identity);
            burst.Root.transform.localScale=Vector3.one*radius;
            Upload(slot,true);
            burst.Renderer.enabled=true;
        }

        private void Update()
        {
            GameSession game=GameSession.Instance;
            Tick(Time.deltaTime,Application.isFocused&&(game==null||game.SimulationActive));
        }

        // Explicit adapter boundary also lets root run deterministic isolated engine checks later.
        public void Tick(float deltaTime,bool simulate)
        {
            if(!initialized||disposed)return;
            clock.Advance(deltaTime,simulate);
            for(int slot=0;slot<bursts.Length;slot++)
            {
                Burst burst=bursts[slot];
                bool visible=simulate&&clock.IsActive(slot)&&borrowedMaterial!=null;
                burst.Renderer.enabled=visible;
                if(visible)Upload(slot,false);
            }
        }

        private void Upload(int slot,bool topology)
        {
            Burst burst=bursts[slot];
            float phase=clock.Phase(slot);
            CosmicNovaGeometry.Build(phase,clock.Quality(slot),burst.Vertices,burst.Indices,burst.Starts,burst.Counts,topology,
                out int vertices,out int indices);
            const MeshUpdateFlags flags=MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices;
            burst.Mesh.SetVertexBufferData(burst.Vertices,0,0,vertices,0,flags);
            if(topology)
            {
                burst.Mesh.SetIndexBufferData(burst.Indices,0,0,indices,flags);
                for(int band=0;band<burst.Starts.Length;band++)
                    burst.Mesh.SetSubMesh(band,new SubMeshDescriptor(burst.Starts[band],burst.Counts[band],MeshTopology.Triangles)
                    {firstVertex=0,vertexCount=vertices,bounds=burst.Mesh.bounds},flags);
            }
            float fade=CosmicNovaGeometry.Envelope(phase);
            for(int band=0;band<burst.Blocks.Length;band++)
            {
                Color palette=Palette(band);
                MaterialPropertyBlock block=burst.Blocks[band];
                Color baseColor=palette*(hasEmission ? .25f : .8f)*fade;baseColor.a=1f;
                if(hasBaseColor)block.SetColor(baseColorId,baseColor);
                if(hasColor)block.SetColor(colorId,baseColor);
                if(hasEmission)block.SetColor(emissionId,palette*(band==0?1.8f:2.2f)*fade);
                burst.Renderer.SetPropertyBlock(block,band);
            }
        }

        public void ResetBursts()
        {
            if(clock!=null)clock.Reset();
            if(bursts==null)return;
            for(int slot=0;slot<bursts.Length;slot++)if(bursts[slot]!=null&&bursts[slot].Renderer!=null)bursts[slot].Renderer.enabled=false;
        }

        private static Color Palette(int band)
        {
            switch(band)
            {
                case 0:return new Color(.92f,.97f,1f);
                case 1:return new Color(1f,.16f,.72f);
                case 2:return new Color(.12f,.86f,1f);
                case 3:return new Color(.16f,1f,.52f);
                default:return new Color(1f,.74f,.12f);
            }
        }
        private void OnDisable()=>ResetBursts();
        private void OnDestroy()
        {
            if(disposed)return;disposed=true;ResetBursts();
            if(bursts!=null)for(int slot=0;slot<bursts.Length;slot++)
            {
                Burst burst=bursts[slot];if(burst==null)continue;
                DisposeOwned(burst.Root);DisposeOwned(burst.Mesh);
            }
            borrowedMaterial=null;bursts=null;clock=null;materialSlots=null;
        }
        private static void DisposeOwned(UnityEngine.Object value)
        {if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
