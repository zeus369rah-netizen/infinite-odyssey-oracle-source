using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>A visible grid shell and a matching solid corridor blocker. It owns no floor or lighting.</summary>
    [DisallowMultipleComponent]
    public sealed class LaneGateVolume : MonoBehaviour
    {
        public int LaneIndex { get; private set; }
        public bool Blocked => gameObject.activeSelf;
        Mesh ownedMesh;

        public void Initialize(int laneIndex, Vector3 localCenter, Quaternion localRotation, Vector3 size, Material material)
        {
            LaneIndex = laneIndex;
            transform.localPosition = localCenter;
            transform.localRotation = localRotation;
            Vector3 h = size * .5f;
            Vector3[] vertices =
            {
                new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z),
                new Vector3(h.x,-h.y,-h.z),new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(h.x,h.y,-h.z),
                new Vector3(h.x,-h.y,h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,h.y,h.z),
                new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,h.y,h.z),new Vector3(-h.x,h.y,-h.z),
                new Vector3(-h.x,h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z),
                new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,-h.y,h.z),new Vector3(-h.x,-h.y,h.z)
            };
            Vector3[] faceNormals = {Vector3.forward,Vector3.back,Vector3.right,Vector3.left,Vector3.up,Vector3.down};
            Vector3[] normals = new Vector3[24];
            int[] triangles = new int[36];
            for(int face=0;face<6;face++)
            {
                int v=face*4,t=face*6;
                for(int n=0;n<4;n++)normals[v+n]=faceNormals[face];
                triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;
                triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            ownedMesh=new Mesh {name="Bounded inactive lane grid shell"};
            ownedMesh.vertices=vertices;ownedMesh.normals=normals;ownedMesh.triangles=triangles;
            ownedMesh.RecalculateBounds();ownedMesh.UploadMeshData(true);
            gameObject.AddComponent<MeshFilter>().sharedMesh=ownedMesh;
            MeshRenderer renderer=gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            BoxCollider collider=gameObject.AddComponent<BoxCollider>();
            collider.size=size;
            collider.isTrigger=false;
        }

        public void SetBlocked(bool blocked) { gameObject.SetActive(blocked); }
        void OnDestroy() { if(ownedMesh!=null)Destroy(ownedMesh); }
    }
}
