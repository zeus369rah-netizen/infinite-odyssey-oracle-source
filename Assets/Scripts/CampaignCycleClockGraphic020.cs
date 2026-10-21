using UnityEngine;
using UnityEngine.UI;
namespace OdysseyOracle
{
    // One retained numeric voxel clock. Fixed cells give every populated snapshot the same mesh capacity.
    [DisallowMultipleComponent]
    public sealed class CampaignCycleClockGraphic020:MaskableGraphic
    {
        public const int GlyphSlots=41,CellsPerGlyph=24,MaximumVertices=GlyphSlots*CellsPerGlyph*4,MaximumIndices=GlyphSlots*CellsPerGlyph*6;
        CampaignCycleView020 value;
        bool known;
        static readonly Color32 Gold=new Color32(224,190,119,255),Moon=new Color32(172,164,232,255),Muted=new Color32(158,162,170,255),Storm=new Color32(217,104,109,255);
        public bool SetSnapshot020(in CampaignCycleView020 next)
        {
            if(next.DayNumber==0||next.Generation==0||next.Revision==0||next.PhaseRemainingSeconds<1||next.PhaseRemainingSeconds>900||next.Night>1||next.Storm>1||next.Storm==1&&next.Night!=1)
            {ClearSnapshot020();return false;}
            if(known&&value.Generation==next.Generation&&value.Revision==next.Revision&&CampaignCycleRules020.SameHudValue(value,next))return false;
            value=next;known=true;SetVerticesDirty();return true;
        }
        public void ClearSnapshot020(){if(!known)return;known=false;value=default;SetVerticesDirty();}
#if UNITY_EDITOR
        public bool TryReadEditorSnapshot020(out CampaignCycleView020 snapshot)
        {
            snapshot = known ? value : default;
            return known;
        }
#endif
        protected override void OnPopulateMesh(VertexHelper output)
        {
            output.Clear();if(!known)return;
            Color32 phase=value.Night==1?Moon:Gold;
            Word(output,"DAY",3,7,47,1.4f,Muted);Number(output,value.DayNumber,10,34,47,1.4f,Gold);
            Word(output,value.Night==1?"NIGHT":"DAY",5,143,47,1.4f,phase);
            Glyph(output,value.Night==1?'m':'s',200,46,1.55f,phase);
            Word(output,value.Night==1?"DAWN IN":"DUSK IN",7,8,31,1.15f,Muted);
            Word(output,value.Storm==1?"STORM":"CLEAR",5,120,31,1.15f,value.Storm==1?Storm:Muted);
            Timer(output,value.PhaseRemainingSeconds,8,9,2.4f,phase);
            Timer(output,value.Storm==1?value.StormRemainingSeconds:-1,120,9,2.4f,value.Storm==1?Storm:Muted);
        }
        static void Word(VertexHelper output,string text,int slots,float x,float y,float scale,Color32 tint)
        {for(int i=0;i<slots;i++)Glyph(output,i<text.Length?text[i]:' ',x+i*5*scale,y,scale,tint);}
        static void Number(VertexHelper output,uint number,int slots,float x,float y,float scale,Color32 tint)
        {
            uint divisor=1;for(int i=0;i<9&&number/divisor>=10;i++)divisor*=10;
            bool done=false;for(int i=0;i<slots;i++)
            {char c=done?' ':(char)('0'+number/divisor);Glyph(output,c,x+i*5*scale,y,scale,tint);if(done)continue;number%=divisor;if(divisor==1)done=true;else divisor/=10;}
        }
        static void Timer(VertexHelper output,int second,float x,float y,float scale,Color32 tint)
        {
            int minute=second>=0?second/60:0,remain=second>=0?second%60:0;
            Glyph(output,second<0?'-':(char)('0'+minute/10),x,y,scale,tint);
            Glyph(output,second<0?'-':(char)('0'+minute%10),x+5*scale,y,scale,tint);
            Glyph(output,':',x+10*scale,y,scale,tint);
            Glyph(output,second<0?'-':(char)('0'+remain/10),x+15*scale,y,scale,tint);
            Glyph(output,second<0?'-':(char)('0'+remain%10),x+20*scale,y,scale,tint);
        }
        static void Glyph(VertexHelper output,char letter,float x,float y,float scale,Color32 tint)
        {
            uint bits=Mask(letter);
            for(int row=0;row<6;row++)for(int column=0;column<4;column++)
            {
                var pixel=tint;if((bits&(1u<<(row*4+column)))==0)pixel.a=0;
                float left=x+column*scale,bottom=y+(5-row)*scale,right=left+scale*.9f,top=bottom+scale*.9f;
                int start=output.currentVertCount;
                output.AddVert(new Vector3(left,bottom,0),pixel,Vector2.zero);output.AddVert(new Vector3(left,top,0),pixel,Vector2.zero);
                output.AddVert(new Vector3(right,top,0),pixel,Vector2.zero);output.AddVert(new Vector3(right,bottom,0),pixel,Vector2.zero);
                output.AddTriangle(start,start+1,start+2);output.AddTriangle(start,start+2,start+3);
            }
        }
        static uint Mask(char value)
        {
            switch(value)
            {
                case '0':return 0x699996u;case '1':return 0xe44464u;case '2':return 0xf12496u;case '3':return 0x699869u;case '4':return 0x888f99u;
                case '5':return 0x69871fu;case '6':return 0x699716u;case '7':return 0x22248fu;case '8':return 0x699696u;case '9':return 0x688e96u;
                case 'A':return 0x999f96u;case 'C':return 0xe1111eu;case 'D':return 0x799997u;case 'E':return 0xf1171fu;case 'G':return 0xe99d1eu;
                case 'H':return 0x999f99u;case 'I':return 0xe4444eu;case 'K':return 0x995359u;case 'L':return 0xf11111u;case 'M':return 0x999ff9u;
                case 'N':return 0x99ddb9u;case 'O':return 0x699996u;case 'R':return 0x995797u;case 'S':return 0x69871eu;case 'T':return 0x44444fu;
                case 'U':return 0x699999u;case 'W':return 0x9ff999u;case 'Y':return 0x444699u;
                case ':':return 0x044044u;case '-':return 0x000f00u;case 'm':return 0x6b3336u;case 's':return 0x24ff42u;
                default:return 0;
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing minimap-local229x62px footer with its established fit/scale/opacity and one noninteractive retained Graphic.
// Array Formats:41 fixed glyph slots x24cells,3936vertices/5904indices; direct digit/bitmask drawing, no formatted strings/arrays/objects in frame or mesh paths. Snapshot changes dirty only day/phase/storm/whole-second state or a new scope.
// Dependencies/Pointers: Root cold-prewarms native uGUI mesh capacity and proves warmed allocations/layout/readability; framework rebuild behavior is not inferred from source. Editor-only diagnostic getter returns the retained numeric snapshot without changing Canvas state; no clock/time/gameplay/save/input authority.


