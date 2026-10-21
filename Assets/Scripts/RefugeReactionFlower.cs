using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // Original cold-built pot, rigid leaves, six petals and six low-alpha aura diamonds.
    public sealed class RefugeReactionFlower : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        private MeshRenderer presentation;
        private MaterialPropertyBlock block;
        public bool Ready=>mesh!=null&&material!=null&&presentation!=null;
        private void Awake(){block=new MaterialPropertyBlock();}
        public static RefugeReactionFlower TryCreate(Transform parent,Vector3 localFloor)
        {
            Shader shader=Shader.Find("OdysseyOracle/RefugeReactionFlower");
            if(parent==null||shader==null)return null;
            GameObject go=new GameObject("Original optional Earth-bloom pot");go.transform.SetParent(parent,false);go.transform.localPosition=localFloor;
            RefugeReactionFlower owner=go.AddComponent<RefugeReactionFlower>();
            try{owner.Build(shader);return owner;}catch{Destroy(go);throw;}
        }
        private void Build(Shader shader)
        {
            const int vertices=408,indices=612;
            var points=new Vector3[vertices];var colors=new Color[vertices];var data=new Vector2[vertices];var triangles=new int[indices];int v=0,t=0;
            AddBox(points,colors,data,triangles,ref v,ref t,new Vector3(0,.04f,0),new Vector3(.34f,.08f,.34f),0,new Color(.20f,.12f,.17f),0);
            AddBox(points,colors,data,triangles,ref v,ref t,new Vector3(0,.18f,0),new Vector3(.38f,.22f,.38f),0,new Color(.36f,.20f,.27f),0);
            AddBox(points,colors,data,triangles,ref v,ref t,new Vector3(0,.32f,0),new Vector3(.43f,.08f,.43f),0,new Color(.51f,.30f,.39f),0);
            AddBox(points,colors,data,triangles,ref v,ref t,new Vector3(0,.357f,0),new Vector3(.33f,.014f,.33f),0,new Color(.13f,.075f,.045f),0);
            AddBox(points,colors,data,triangles,ref v,ref t,new Vector3(0,.725f,0),new Vector3(.045f,.74f,.045f),0,new Color(.12f,.43f,.21f),1);
            for(int i=0;i<4;i++)
            {
                float angle=i*90+30;Vector3 direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
                AddBox(points,colors,data,triangles,ref v,ref t,direction*.095f+Vector3.up*(.53f+i*.115f),new Vector3(.075f,.025f,.19f),angle,new Color(.15f,.55f,.28f),1);
            }
            Color[] petals={new Color(.83f,.20f,.51f),new Color(.67f,.29f,.90f),new Color(.24f,.70f,.87f),new Color(.91f,.49f,.25f),new Color(.91f,.76f,.24f),new Color(.95f,.38f,.64f)};
            for(int i=0;i<6;i++){float angle=i*60;Vector3 direction=Quaternion.Euler(0,angle,0)*Vector3.forward;AddBox(points,colors,data,triangles,ref v,ref t,direction*.145f+Vector3.up*1.12f,new Vector3(.12f,.065f,.20f),angle,petals[i],2);}
            AddBox(points,colors,data,triangles,ref v,ref t,new Vector3(0,1.15f,0),new Vector3(.13f,.095f,.13f),0,new Color(1,.87f,.35f),2);
            for(int i=0;i<6;i++)
            {
                float angle=i*Mathf.PI/3;Vector3 center=new Vector3(Mathf.Sin(angle)*.23f,.85f+(i%3)*.18f,Mathf.Cos(angle)*.23f);int start=v;
                Vector3 side=new Vector3(Mathf.Cos(angle),0,-Mathf.Sin(angle))*.035f;
                points[v]=center+Vector3.up*.055f;points[v+1]=center+side;points[v+2]=center-Vector3.up*.055f;points[v+3]=center-side;
                for(int j=0;j<4;j++){colors[v+j]=new Color(.46f,.88f,.68f,.12f);data[v+j]=new Vector2(3,i);}v+=4;
                triangles[t++]=start;triangles[t++]=start+1;triangles[t++]=start+2;triangles[t++]=start;triangles[t++]=start+2;triangles[t++]=start+3;
            }
            if(v!=vertices||t!=indices)throw new System.InvalidOperationException("Fixed original flower geometry capacity differs.");
            mesh=new Mesh{name="Original Earth bloom fixed408"};mesh.vertices=points;mesh.colors=colors;mesh.uv=data;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.bounds=new Bounds(new Vector3(0,.75f,0),new Vector3(.60f,1.50f,.60f));
            material=new Material(shader){name="Original colored Earth bloom"};gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;presentation=gameObject.AddComponent<MeshRenderer>();presentation.sharedMaterial=material;presentation.shadowCastingMode=ShadowCastingMode.Off;presentation.receiveShadows=false;
            BoxCollider support=gameObject.AddComponent<BoxCollider>();support.center=new Vector3(0,.18f,0);support.size=new Vector3(.43f,.36f,.43f);ApplyGrowth(0,0);
        }
        private static void AddBox(Vector3[] p,Color[] c,Vector2[] d,int[] indices,ref int v,ref int t,Vector3 center,Vector3 size,float yaw,Color color,int kind)
        {
            Quaternion rotation=Quaternion.Euler(0,yaw,0);Vector3 half=size*.5f;
            for(int face=0;face<6;face++)
            {
                int start=v;
                for(int corner=0;corner<4;corner++)
                {
                    float a=corner==0||corner==3?-1:1,b=corner<2?-1:1;Vector3 point;
                    switch(face){case 0:point=new Vector3(1,a,b);break;case 1:point=new Vector3(-1,a,-b);break;case 2:point=new Vector3(a,1,-b);break;case 3:point=new Vector3(a,-1,b);break;case 4:point=new Vector3(a,b,1);break;default:point=new Vector3(-a,b,-1);break;}
                    p[v]=center+rotation*Vector3.Scale(point,half);c[v]=color;d[v]=new Vector2(kind,0);v++;
                }
                indices[t++]=start;indices[t++]=start+1;indices[t++]=start+2;indices[t++]=start;indices[t++]=start+2;indices[t++]=start+3;
            }
        }
        public void ApplyGrowth(float growth,float clock)
        {
            if(!Ready||block==null)return;block.SetFloat("_Growth",Mathf.Clamp01(growth));block.SetFloat("_PreviewClock",float.IsNaN(clock)||float.IsInfinity(clock)?0:clock);presentation.SetPropertyBlock(block);
        }
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original pot rests at exact supported refuge local Y0; full flower remains within .60x1.50x.60m and grows from soil Y.364. +Z forward/Y-up; rigid vertices, no bones.
// Array Formats: Cold fixed408 vertices/612 indices plus six fixed aura diamonds; one owned mesh/material/MPB. Repeated writes update two uniforms only; Awake creates native MPB, never a Mono constructor/field initializer.
// Dependencies/Pointers: World owns one optional pot child and numeric host growth state; borrowed existing spell audio stays under Combat. No rewards, inventory, saves, additional real light or gameplay shrine/portal mutation.
