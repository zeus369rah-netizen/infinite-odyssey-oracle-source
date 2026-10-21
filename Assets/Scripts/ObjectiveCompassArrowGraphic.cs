using UnityEngine;

namespace OdysseyOracle
{
    public sealed class ObjectiveCompassArrowGraphic:UnityEngine.UI.MaskableGraphic
    {
        static readonly Vector2[] Points={new Vector2(-5,-23),new Vector2(5,-23),new Vector2(5,0),new Vector2(18,0),new Vector2(0,25),new Vector2(-18,0),new Vector2(-5,0)};
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();Layer(vh,1f,new Color32(6,12,11,255));Layer(vh,.74f,new Color32(246,232,177,255));
        }
        static void Layer(UnityEngine.UI.VertexHelper vh,float scale,Color32 tint)
        {
            int first=vh.currentVertCount;
            for(int i=0;i<7;i++){Vector2 p=Points[i]*scale;vh.AddVert(new Vector3(p.x,p.y,0),tint,Vector2.zero);}
            vh.AddTriangle(first,first+1,first+2);vh.AddTriangle(first,first+2,first+6);vh.AddTriangle(first+3,first+4,first+5);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: one UI-local +Y-forward arrow, rotated only through its RectTransform; original numeric silhouette with dark outline/ivory fill.
// Array Formats: one cold7point readonly array,14UI vertices/18indices; VertexHelper receives value records, no hot arrays/strings or instantiated sprites.
// Dependencies/Pointers: existing uGUI owns render buffers; one cold Graphic only. No skins, textures, world markers, Physics, active allocation/profile or browser-quality claim.
