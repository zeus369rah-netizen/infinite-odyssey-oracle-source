// PSEUDOCODE
// 1. Reuse the current graphics scroll panel and insert a clear Advanced Graphics heading and brief explanation.
// 2. Keep the existing shadow-quality dropdown, options, styling, navigation, and preference callback unchanged.
// 3. Let the established input module continue to own keyboard/controller focus and popup teardown.
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        UnityEngine.UI.Dropdown shadowQualityDropdown020;
        UnityEngine.UI.Text cloudsValue020,weatherEffectsValue020;
        void BuildShadowQualityDropdown020(RectTransform parent)
        {
            MenuLabel(parent,"AdvancedGraphicsHeading020","ADVANCED GRAPHICS",15,Gold,26);
            MenuLabel(parent,"AdvancedGraphicsDescription020","Controls the distance, detail and filtering used for world shadows.",14,Muted,38);
            var row=Rect(parent,"ShadowQualityRow020",Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(0,78));Height(row,78);
            Label(row,"ShadowQualityHeading020","SHADOW QUALITY",12,Gold,TextAnchor.UpperLeft,Vector2.zero,new Vector2(400,22),new Vector2(0,1));
            // Framework factory owns valid Template/Viewport/Content/Toggle wiring and popup teardown.
            // Dropdown itself positions this finite option list; a competing content fitter would overwrite that layout.
            var node=UnityEngine.UI.DefaultControls.CreateDropdown(new UnityEngine.UI.DefaultControls.Resources());node.name="ShadowQualityDropdown020";node.transform.SetParent(row,false);
            var rect=(RectTransform)node.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(3,6);rect.offsetMax=new Vector2(-3,-28);
            shadowQualityDropdown020=node.GetComponent<UnityEngine.UI.Dropdown>();
            shadowQualityDropdown020.ClearOptions();
            shadowQualityDropdown020.options.Add(new UnityEngine.UI.Dropdown.OptionData("Off"));
            shadowQualityDropdown020.options.Add(new UnityEngine.UI.Dropdown.OptionData("Low"));
            shadowQualityDropdown020.options.Add(new UnityEngine.UI.Dropdown.OptionData("Medium"));
            shadowQualityDropdown020.options.Add(new UnityEngine.UI.Dropdown.OptionData("High"));
            foreach(var text in node.GetComponentsInChildren<UnityEngine.UI.Text>(true)){text.font=font;text.fontSize=16;text.color=Ivory;text.raycastTarget=false;}
            foreach(var image in node.GetComponentsInChildren<UnityEngine.UI.Image>(true))image.color=new Color(.08f,.12f,.105f,.98f);
            var item=shadowQualityDropdown020.template.GetComponentInChildren<UnityEngine.UI.Toggle>(true);item.graphic.color=Gold;item.graphic.raycastTarget=false;
            var colors=shadowQualityDropdown020.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.12f,1.22f,1.14f,1);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.65f,.8f,.7f,1);colors.fadeDuration=.12f;
            shadowQualityDropdown020.colors=colors;item.colors=colors;
            var arrow=node.transform.Find("Arrow");if(arrow!=null){var image=arrow.GetComponent<UnityEngine.UI.Image>();if(image!=null)image.enabled=false;}
            Label(rect,"ShadowQualityArrow020","v",16,Gold,TextAnchor.MiddleCenter,new Vector2(-15,0),new Vector2(22,22),new Vector2(1,.5f));
            Border(rect,MutedGold,1);
            shadowQualityDropdown020.template.sizeDelta=new Vector2(0,192);
            var itemRect=(RectTransform)item.transform;itemRect.sizeDelta=new Vector2(0,42);
            var listRect=(RectTransform)item.transform.parent;listRect.sizeDelta=new Vector2(listRect.sizeDelta.x,50);
            shadowQualityDropdown020.SetValueWithoutNotify(session.Graphics!=null?session.Graphics.ShadowQuality020:3);shadowQualityDropdown020.RefreshShownValue();
            shadowQualityDropdown020.onValueChanged.AddListener(value=>{session.Graphics?.SetShadowQuality020(value);RefreshGraphics();});
            var clouds=MenuButton(parent,"ToggleClouds020","",()=>{if(session.Graphics!=null)session.Graphics.SetCloudsEnabled020(!session.Graphics.CloudsEnabled020);RefreshGraphics();});cloudsValue020=clouds.GetComponentInChildren<UnityEngine.UI.Text>();
            var weather=MenuButton(parent,"ToggleWeatherEffects020","",()=>{if(session.Graphics!=null)session.Graphics.SetWeatherEffectsEnabled020(!session.Graphics.WeatherEffectsEnabled020);RefreshGraphics();});weatherEffectsValue020=weather.GetComponentInChildren<UnityEngine.UI.Text>();
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing fitted graphics Canvas/scroll content and unchanged game palette.
// Array Formats: Two cold-built section labels, four dropdown options and two scalar toggle labels; no per-frame UI allocation.
// Dependencies/Pointers: Standard uGUI Dropdown owns popup/explicit item navigation/cancel/cleanup; existing InputSystemUIInputModule handles keyboard/controller; GraphicsPreferences owns persistence/render policy.
