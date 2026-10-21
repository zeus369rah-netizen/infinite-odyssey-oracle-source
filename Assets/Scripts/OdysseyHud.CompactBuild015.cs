using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        RectTransform compactBuild015,compactRemoveSelection015;
        string compactPlaceKeys015,compactRemoveKeys015;
        UnityEngine.UI.Text compactBuildKeys015,compactBuildBudget015,compactSnapLabel020;
        int displayedSnap020=-1;
        readonly RectTransform[] compactBuildSelection015=new RectTransform[4];
        int compactBuildKeyFrame015=int.MinValue,displayedBuildSelection015=-1;
        string displayedBuildPreview015;

        public bool CompactBuildPickerVisible015=>compactBuild015!=null&&compactBuild015.gameObject.activeSelf;
        void BuildCompactTowerPicker015()
        {
            PrepareLaneBuildText015();
            compactBuild015=Frame(hudRoot,"CompactTowerPicker015",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,238),new Vector2(660,220),Panel,MutedGold);
            Label(compactBuild015,"PickerTitle","TOWER PICKER",16,Gold,TextAnchor.UpperLeft,new Vector2(14,-10),new Vector2(200,24),new Vector2(0,1));
            compactBuildBudget015=Label(compactBuild015,"AcceptedBuildBudget015","",11,Muted,TextAnchor.UpperRight,new Vector2(-14,-13),new Vector2(400,22),new Vector2(1,1));
            compactBuildKeys015=Label(compactBuild015,"BoundPickerControls015","",10,Ivory,TextAnchor.UpperLeft,new Vector2(14,-36),new Vector2(632,32),new Vector2(0,1));
            towerBuildButtons=new UnityEngine.UI.Button[4];towerBuildLabels=new UnityEngine.UI.Text[4];
            for(int i=0;i<4;i++)
            {
                int type=i;
                var button=Button(compactBuild015,"ChooseTower015_"+i,"",new Vector2(0,1),new Vector2(0,1),new Vector2(14+i*160,-76),new Vector2(152,68),()=>session.Builder?.TrySelectShrine(type));
                towerBuildButtons[i]=button;var label=button.GetComponentInChildren<UnityEngine.UI.Text>();towerBuildLabels[i]=label;
                label.fontSize=11;label.alignment=TextAnchor.MiddleLeft;label.rectTransform.offsetMin=new Vector2(53,4);label.rectTransform.offsetMax=new Vector2(-4,-4);
                var icon=Rect(button.transform,"OriginalOwnedElementLogo",new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(8,0),new Vector2(38,38));
                var sprite=OracleElementEmblems.Get(i);
                if(sprite!=null){var image=Paint(icon,Color.white);image.sprite=sprite;image.preserveAspect=true;}
                else foreach(var glyph in DrawSigil(icon,i))glyph.color=ShrinePlacementBuilder.TowerColors[i];
                var selected=Stretch(button.transform,"SelectedTowerOutline015",2,2,2,2);Border(selected,Gold,2);compactBuildSelection015[i]=selected;selected.gameObject.SetActive(false);
            }
            buildFeedback=Label(compactBuild015,"CurrentGroundReason015","",12,Ivory,TextAnchor.UpperLeft,new Vector2(14,-152),new Vector2(632,26),new Vector2(0,1));
            laneRemoveButton015=Button(compactBuild015,"RemoveOwnedTower015","REMOVE",new Vector2(1,1),new Vector2(1,1),new Vector2(-100,-188),new Vector2(76,24),()=>session.Builder?.TrySelectLaneRemove020());
            var removeLabel=laneRemoveButton015.GetComponentInChildren<UnityEngine.UI.Text>();removeLabel.fontSize=11;
            compactRemoveSelection015=Stretch(laneRemoveButton015.transform,"SelectedRemoveOutline015",2,2,2,2);Border(compactRemoveSelection015,Gold,2);compactRemoveSelection015.gameObject.SetActive(false);
            var anvilInfo=Label(compactBuild015,"TowerUpgradeLocation015","ANVIL IN REFUGE",9,Muted,TextAnchor.MiddleCenter,new Vector2(-184,-188),new Vector2(104,24),new Vector2(1,1));
            // Inert compatibility component: the existing refresh owner toggles this label's GameObject only.
            laneUpgradeButton015=anvilInfo.gameObject.AddComponent<UnityEngine.UI.Button>();laneUpgradeButton015.interactable=false;
            Button(compactBuild015,"CancelTowerPicker015","CANCEL",new Vector2(1,1),new Vector2(1,1),new Vector2(-14,-188),new Vector2(78,24),()=>session.Builder?.CancelPlacement());
            var snap=Button(compactBuild015,"ToggleBuildSnap020","SNAP: ON",new Vector2(0,1),new Vector2(0,1),new Vector2(14,-188),new Vector2(104,24),()=>session.Builder?.TryToggleLaneSnap020());
            compactSnapLabel020=snap.GetComponentInChildren<UnityEngine.UI.Text>();compactSnapLabel020.fontSize=11;
            compactBuild015.gameObject.SetActive(false);RefreshCompactBuildKeys015();
        }
        void RefreshCompactBuildKeys015()
        {
            int frame=Mathf.Max(AbilityBindings.LastChangeFrame,GameActionBindings.LastChangeFrame);
            if(compactBuildKeys015==null)return;
            if(compactBuildKeyFrame015!=frame)
            {
                compactBuildKeyFrame015=frame;string key=GameActionBindings.GetLabel("Interact");
                compactPlaceKeys015="SCROLL: CHOOSE | ["+key+"]: PLACE | RMB / ESC: CLOSE\nHOLD ALT: CLICK LOGOS / SNAP; RELEASE ALT, THEN CLICK TO RESUME ON WEB";
                compactRemoveKeys015="SCROLL: CHOOSE | ["+key+"]: REMOVE | RMB / ESC: CLOSE\nHOLD ALT: CLICK LOGOS / SNAP; RELEASE ALT, THEN CLICK TO RESUME ON WEB";
            }
            bool remove=session!=null&&session.Builder!=null&&session.Builder.LaneRemoveSelected015;
            string keys=remove?compactRemoveKeys015:compactPlaceKeys015;if(compactBuildKeys015.text!=keys)compactBuildKeys015.text=keys;
            int snap=session!=null&&session.Builder!=null&&session.Builder.LaneSnap020?1:0;
            if(compactSnapLabel020!=null&&displayedSnap020!=snap){displayedSnap020=snap;compactSnapLabel020.text=snap!=0?"SNAP: ON":"SNAP: OFF";}
            if(compactRemoveSelection015!=null&&compactRemoveSelection015.gameObject.activeSelf!=remove)
            {
                compactRemoveSelection015.gameObject.SetActive(remove);
                for(int i=0;i<4;i++)compactBuildSelection015[i].gameObject.SetActive(!remove&&session!=null&&session.Builder!=null&&i==session.Builder.SelectedTowerType015);
            }
        }
        bool CompactBuildLifetime015()
        {
            if(compactBuild015==null||!compactBuild015.gameObject.activeSelf)return false;
            if(session==null||session.Builder==null||!session.Builder.IsPlacing||!session.Builder.LanePickerRetained020||currentPanel!=JourneyPanel.None||IntroActive||LoadingActive||DownedActive||ShopVisible)
            {HideCompactTowerPicker015();return false;}
            RefreshCompactBuildKeys015();return true;
        }
        void ShowCompactTowerPicker015()
        {if(compactBuild015==null)return;compactBuild015.gameObject.SetActive(true);buildSnapshotValid=false;RefreshTowerBuildMenu();}
        void HideCompactTowerPicker015()
        {if(compactBuild015!=null)compactBuild015.gameObject.SetActive(false);buildSnapshotValid=false;displayedBuildSelection015=-1;displayedBuildPreview015=null;}
        void RefreshCompactBuildSelection015(int type,int tokens,int capacity)
        {
            bool remove=session!=null&&session.Builder!=null&&session.Builder.LaneRemoveSelected015;
            for(int i=0;i<4;i++)compactBuildSelection015[i].gameObject.SetActive(!remove&&i==type);
            compactBuildBudget015.text=laneBuildFeedback015[Mathf.Clamp(tokens,0,3)*5+Mathf.Clamp(capacity,0,4)];
            displayedBuildSelection015=type;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing HUD Canvas bottom centre,660x188 atY238 above unchanged660x164 spell cards atY58; no modal/cursor/pause ownership.
// Array Formats: four cold cached original emblem/glyph buttons, four selection outlines and existing32type/tier labels+20budget labels; binding text changes only on binding revision.
// Dependencies/Pointers: sole Builder owns selection/preview and host transaction. No direct build/token/reward calls, per-frame strings/Objects, new assets, narration, native buffers or provider work. Keyboard E/scroll and actual browser/render acceptance remain root-owned.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Same compact bottom-centre HUD; four original tower icons plus visible owned-only Remove outline.
// Array Formats: One cached action outline and two binding-revision strings, one inert label compatibility component; no hot allocation or new gameplay ownership.
// Dependencies/Pointers: Scroll/E reaches canonical Remove while locked. Anvil guidance is a noninteractive label; existing Esc cancels. Root alone integrates and verifies actual input/render/receipt.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing compact HUD Canvas660x220, two-line controls, four original logos and footer snap/remove/cancel.
// Array Formats: Cold cached controls/literal snap labels/existing binding text; outline changes only on actual choice/remove change.
// Dependencies/Pointers: Existing Alt permits local choice/toggle only. Remove selects for later E confirmation; no new pointer owner, world/cost authority, hot Objects or imported assets.
