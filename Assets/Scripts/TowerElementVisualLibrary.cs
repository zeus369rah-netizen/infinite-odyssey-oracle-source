using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using TowerElementReload=UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using TowerElementReload=System.Action;
#endif

namespace OdysseyOracle
{
    public sealed class TowerElementVisualLibrary:IDisposable
    {
        public sealed class Template
        {
            public TowerElementRecipe64 Recipe;
            public Mesh Ambient,Shot;
        }
        readonly Template[] templates=new Template[12];
        Material material;
        bool disposed,subscribed;
        TowerElementReload reload;
        public bool Ready=>!disposed&&material!=null;
        public Material Material=>material;
        private TowerElementVisualLibrary(){}
        public static TowerElementVisualLibrary TryCreate()
        {
            Shader shader=Shader.Find("Oracle/Presentation/TowerElements009");if(shader==null)return null;
            var owner=new TowerElementVisualLibrary();
            try
            {
                owner.material=new Material(shader){name="Original shared tower elemental presentation009"};
                for(int type=0;type<4;type++)for(int tier=1;tier<=3;tier++)
                {
                    var template=new Template{Recipe=TowerElementRecipes.Get(type,tier)};owner.templates[type*3+tier-1]=template;
                    template.Ambient=Build(template.Recipe,false);template.Shot=Build(template.Recipe,true);
                }
                owner.reload=owner.Dispose;LightPoolLifecycle.SubscribeBeforeReload(owner.reload);owner.subscribed=true;return owner;
            }
            catch(Exception error){owner.Dispose();Debug.LogWarning("Original tower elemental presentation unavailable: "+error.Message);return null;}
        }
        public Template Get(int type,int tier)
        {if(!Ready||!TowerElementRecipes.Valid(type,tier))throw new ArgumentOutOfRangeException();return templates[type*3+tier-1];}
        static Mesh Build(TowerElementRecipe64 recipe,bool shot)
        {
            NativeArray<TowerElementVertex32> vertices=default;NativeArray<ushort> indices=default;NativeArray<int2> counts=default;JobHandle fence=default;Mesh mesh=null;
            try
            {
                vertices=new NativeArray<TowerElementVertex32>(512,Allocator.TempJob);indices=new NativeArray<ushort>(1024,Allocator.TempJob);counts=new NativeArray<int2>(1,Allocator.TempJob);
                fence=new TowerElementMeshJob{Recipe=recipe,Shot=shot?1u:0u,Vertices=vertices,Indices=indices,Counts=counts}.Schedule();fence.Complete();
                int2 count=counts[0];if(count.x<1||count.x>512||count.y<3||count.y>1024||count.y%3!=0)throw new InvalidOperationException("Original elemental fixed mesh capacity differs.");
                mesh=new Mesh{name="Original elemental "+recipe.Type+" tier"+recipe.Tier+(shot?" accepted flight":" ambient"),indexFormat=IndexFormat.UInt16};
                mesh.SetVertexBufferParams(count.x,new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2),new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,3));
                mesh.SetVertexBufferData(vertices,0,0,count.x);mesh.SetIndexBufferParams(count.y,IndexFormat.UInt16);mesh.SetIndexBufferData(indices,0,0,count.y);
                mesh.subMeshCount=1;mesh.SetSubMesh(0,new SubMeshDescriptor(0,count.y));mesh.bounds=new Bounds(new Vector3(0,1.1f,0),new Vector3(1.9f,2.4f,1.9f));mesh.UploadMeshData(true);return mesh;
            }
            catch{if(mesh!=null)Release(mesh);throw;}
            finally{fence.Complete();if(counts.IsCreated)counts.Dispose();if(indices.IsCreated)indices.Dispose();if(vertices.IsCreated)vertices.Dispose();}
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;if(subscribed&&reload!=null)LightPoolLifecycle.UnsubscribeBeforeReload(reload);subscribed=false;
            for(int i=0;i<templates.Length;i++)if(templates[i]!=null){Release(templates[i].Ambient);Release(templates[i].Shot);templates[i]=null;}Release(material);material=null;
        }
        static void Release(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original Head-local +Z/Y-up shared ambient/flight mesh metadata, fixed1.9x2.4x1.9m ambient render bounds; per-instance accepted flight bounds are separate.
// Array Formats:12 templates each borrow no saved mesh and own one new ambient/shot mesh; one shared original material. Cold Burst job fills fixed Vertex32[512]/ushort[1024]/int2[1], fence completes before native Mesh API and disposal.
// Dependencies/Pointers: SavedTowerModelLibrary owns this additive library after borrowers retire. Saved72 model meshes/four materials remain immutable. Missing shader is fallback/unaccepted feature, not startup failure; owned new resources dispose on normal/reload lifetime.
