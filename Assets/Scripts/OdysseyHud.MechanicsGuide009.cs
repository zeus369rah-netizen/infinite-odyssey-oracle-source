using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        JourneyPanel mechanicsGuideReturn=JourneyPanel.Settings;
        UnityEngine.UI.Text mechanicsGuideKeys;
        UnityEngine.UI.Text mechanicsGuideOverview009;
        bool mechanicsGuideCaptionActive009;
        const string MechanicsGuideOverview009="40 WAVES · 8 MILESTONES OF FIVE\nProtect the green shrine through five waves, then regroup in the refuge. Build towers and cast to cover the active approaches.";
        readonly UnityEngine.UI.Text[] mechanicsElementKeys=new UnityEngine.UI.Text[4];
        public bool IsMechanicsGuideVisible=>built&&currentPanel==JourneyPanel.MechanicsGuide&&
            panels.ContainsKey(JourneyPanel.MechanicsGuide)&&panels[JourneyPanel.MechanicsGuide].gameObject.activeInHierarchy;
        void BuildMechanicsGuide009()
        {
            var panel=PanelContent(JourneyPanel.MechanicsGuide,"MechanicsGuidePanel",new Vector2(960,980));
            MenuLabel(panel,"MechanicsGuideTitle","YOUR JOURNEY · QUICK GUIDE",29,Ivory,52);
            MenuLabel(panel,"CriticalRefugeLoop","DEFEND → REFUGE → UPGRADE → READY",23,Gold,48);
            var content=ScrollingContent(panel,"MechanicsGuideScroll",500,out _);
            mechanicsGuideOverview009=MenuLabel(content,"GuideCombatLoop",MechanicsGuideOverview009,20,Ivory,148);
            MenuLabel(content,"GuideRefugeLoop","CAMPAIGN · BETWEEN MILESTONES\nAfter five waves clear, use an active monster portal to return to the refuge. Eight milestones span forty waves; another lane opens each milestone.",20,Gold,100);
            MenuLabel(content,"GuideUpgradeLoop","VISIT BOTH MENTORS\nUse the Tower Anvil to inspect tower upgrades. Visit Raykon for spells. Check the cost before confirming an upgrade.",20,Ivory,100);
            MenuLabel(content,"GuidePartyLoop","NEXT LEVEL · THE VIOLET PORTAL\nSolo: ready yourself. Co-op: both players visit the mentors and ready up. The same party continues into the next level.",20,Gold,100);
            MenuLabel(content,"GuideElementsTitle","ELEMENTS · ACTUAL KEYS & EFFECTS",16,Muted,32);
            BuildElementCards015(content);
            mechanicsGuideKeys=MenuLabel(content,"ActualGuideKeys","",16,Muted,90);
            MenuLabel(content,"GuideTrainingChoice","In the starting refuge, use the training alcoves to compare towers and stationary monsters. Attacks can be switched off. Follow the compass toward your next objective.",17,Ivory,74);
            mechanicsPlaybackButton015=MenuButton(panel,"HearMechanicsGuide","PLAY NARRATION",()=>{session.RefugeActivities?.ToggleMechanicsGuide015();RefreshMechanicsPlayback015();},42);
            mechanicsPlaybackLabel015=mechanicsPlaybackButton015.GetComponentInChildren<UnityEngine.UI.Text>();
            mechanicsTranscriptButton015=MenuButton(panel,"MechanicsTranscriptDisclosure","SHOW TRANSCRIPT",()=>{mechanicsTranscriptExpanded015=!mechanicsTranscriptExpanded015;RefreshMechanicsPlayback015();},34);
            mechanicsTranscriptLabel015=mechanicsTranscriptButton015.GetComponentInChildren<UnityEngine.UI.Text>();
            
            MenuButton(panel,"ReturnFromMechanicsGuide","BACK",()=>{session.RefugeActivities?.StopMechanicsGuide();SetPanel(mechanicsGuideReturn);},48);
        }
        public void SetMechanicsVoiceCaption009(RefugeActivityDirector owner,bool playing)
        {
            if(session==null||!object.ReferenceEquals(owner,session.RefugeActivities)||mechanicsGuideOverview009==null||mechanicsGuideCaptionActive009==playing)return;
            mechanicsGuideCaptionActive009=playing;
            mechanicsGuideOverview009.fontSize=playing?18:20;
            RefreshMechanicsPlayback015();
        }
        void OpenMechanicsGuide009(JourneyPanel from)
        {
            mechanicsGuideReturn=from;mechanicsTranscriptExpanded015=false;AbilityBindings.CancelRebind();
            if(session.HasStarted)session.Pause();
            for(int i=0;i<4;i++)mechanicsElementKeys[i].text=AbilityBindings.GetLabel(i);
            mechanicsGuideKeys.text="MOVE  ["+GameActionBindings.GetLabel("Forward")+"] ["+GameActionBindings.GetLabel("Left")+"] ["+GameActionBindings.GetLabel("Back")+"] ["+GameActionBindings.GetLabel("Right")+"]\nINTERACT  ["+GameActionBindings.GetLabel("Interact")+"]   ·   CAST  [LEFT CLICK]   ·   SKILLS  ["+GameActionBindings.GetLabel("SkillTree")+"]   ·   PAUSE  [ESC]";
            SetPanel(JourneyPanel.MechanicsGuide);RefreshMechanicsPlayback015();
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas reference pixels and original menu-fit/scroll owner; no actor/world pose mutation.
// Array Formats: Four cached element key labels, one cold help panel and event-only binding strings. No active Update or new audio source.
// Dependencies/Pointers: Actual campaign/refuge/host acceptance remains authoritative. Optional narration uses the existing Activity voice owner only after explicit click; menu exit/focus/mute cancels it.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.
