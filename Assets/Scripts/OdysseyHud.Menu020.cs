using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        RectTransform mainExtras020,pauseExtras020,settingsExtras020;
        UnityEngine.UI.Text mainMoreLabel020,pauseMoreLabel020,settingsMoreLabel020;
        bool mainMore020,pauseMore020,settingsMore020;
        int pauseControlsBinding020=int.MinValue;

        RectTransform BuildMenuGroup020(RectTransform parent,string name)
        {
            var group=Rect(parent,name,Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
            var layout=group.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing=8;layout.childAlignment=TextAnchor.MiddleCenter;
            layout.childControlHeight=layout.childControlWidth=true;
            layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
            return group;
        }
        void ToggleMainMore020()
        {
            mainMore020=!mainMore020;
            SetMenuExtras020(mainExtras020,mainMoreLabel020,JourneyPanel.Start,mainMore020,650,850);
        }
        void TogglePauseMore020()
        {
            pauseMore020=!pauseMore020;
            SetMenuExtras020(pauseExtras020,pauseMoreLabel020,JourneyPanel.Pause,pauseMore020,460,920);
        }
        void ToggleSettingsMore020()
        {
            settingsMore020=!settingsMore020;
            SetMenuExtras020(settingsExtras020,settingsMoreLabel020,JourneyPanel.Settings,settingsMore020,520,940);
        }
        void SetMenuExtras020(RectTransform group,UnityEngine.UI.Text label,JourneyPanel panel,bool expanded,float closedHeight,float openHeight)
        {
            if(group==null)return;
            group.gameObject.SetActive(expanded);
            if(label!=null)label.text=expanded?"LESS":"MORE";
            var card=panels[panel];var size=card.sizeDelta;float height=expanded?openHeight:closedHeight;
            if(size.y!=height){size.y=height;card.sizeDelta=size;lastCanvasSize=Vector2.zero;}
        }
        void CloseMenuExtras020(JourneyPanel panel)
        {
            if(panel==JourneyPanel.Start){mainMore020=false;SetMenuExtras020(mainExtras020,mainMoreLabel020,panel,false,650,850);}
            else if(panel==JourneyPanel.Pause){pauseMore020=false;SetMenuExtras020(pauseExtras020,pauseMoreLabel020,panel,false,460,920);}
            else if(panel==JourneyPanel.Settings){settingsMore020=false;SetMenuExtras020(settingsExtras020,settingsMoreLabel020,panel,false,520,940);}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted Canvas menu cards; no world or actor coordinates.
// Array Formats: Three cold UI groups/labels and three scalar disclosure booleans; no active-loop allocation.
// Dependencies/Pointers: Existing panel, ancestry, input, settings and session owners retain authority. No source/context/pointer/save or provider ownership is added.
