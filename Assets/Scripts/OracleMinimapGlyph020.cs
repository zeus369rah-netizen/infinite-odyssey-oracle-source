using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        static OracleMinimapGlyph020 CreateMapIcon020(RectTransform parent,int kind,Color tint,float extent)
        {
            var rect=Rect(parent,"OriginalMapPixel020",new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(extent,extent));
            var graphic=rect.gameObject.AddComponent<OracleMinimapGlyph020>();graphic.raycastTarget=false;
            graphic.SetKind020(kind);graphic.color=tint;return graphic;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OracleMinimapGlyph020 : MaskableGraphic
    {
        // Original monochrome silhouettes. Intrinsic data is shared; position/yaw/tint stay on the accepted marker parent.
        static readonly ulong[] Patterns020={
            0x66FFDBDBFF7E2418UL, // monster practice
            0x3C665A5A663C183CUL, // runekeeper staff/orb
            0x667E3C3C3C3C7E7EUL, // tower practice
            0x00FF7E3C183C7E00UL, // anvil
            0x2466247EFF7E4242UL, // teammate pair
            0x3C66C3DBDBDBFFFFUL, // portal arch
            0x183C7EFF3C3C3C18UL  // player arrow: local+Y is world north at yaw0
        };
        int kind020=-1;
        public void SetKind020(int value)
        {
            if(value<0||value>=Patterns020.Length)value=-1;
            if(kind020==value)return;kind020=value;SetVerticesDirty();
        }
        public static bool TryGetPattern020(int kind,out ulong bits)
        {
            bits=0;if(kind<0||kind>=Patterns020.Length)return false;bits=Patterns020[kind];return true;
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();if(!TryGetPattern020(kind020,out ulong bits))return;
            var bounds=GetPixelAdjustedRect();float dx=bounds.width*.125f,dy=bounds.height*.125f;
            if(!(dx>0)||!(dy>0))return;Color32 tint=color;
            for(int row=0;row<8;row++)
            {
                uint mask=(uint)((bits>>((7-row)*8))&255);int column=0;
                while(column<8)
                {
                    if((mask&(1u<<column))==0){column++;continue;}
                    int first=column;do{column++;}while(column<8&&(mask&(1u<<column))!=0);
                    float left=bounds.xMin+first*dx,right=bounds.xMin+column*dx;
                    float bottom=bounds.yMax-(row+1)*dy,top=bottom+dy;int start=helper.currentVertCount;
                    helper.AddVert(new Vector3(left,bottom,0),tint,Vector2.zero);helper.AddVert(new Vector3(left,top,0),tint,Vector2.zero);
                    helper.AddVert(new Vector3(right,top,0),tint,Vector2.zero);helper.AddVert(new Vector3(right,bottom,0),tint,Vector2.zero);
                    helper.AddTriangle(start,start+1,start+2);helper.AddTriangle(start+2,start+3,start);
                }
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Retained marker-local pixels; unchanged parent world-XZ projection and facing yaw, standard uGUI RectMask2D clipping.
// Array Formats: Shared immutable ulong[7] intrinsic patterns and row-run quad emission into the caller-owned VertexHelper; no texture, sprite, material, per-pixel object or frame-updater allocation.
// Dependencies/Pointers: Actual existing marker parents own locations/admission/lifetime; no new objective/quest target exists. Root alone integrates and verifies actual Canvas/native/browser readability.
