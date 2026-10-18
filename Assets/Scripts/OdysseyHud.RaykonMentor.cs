using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        const string RaykonCastingGuide="CASTING WITH RAYKON\nTap an element to prepare it. Tap the same element again to confirm a single, or add a second distinct element to confirm a pair. Select all four for Nova. Three selected elements stay highlighted until you add the fourth.\n\nLeft-click casts the confirmed spell once. Auto-cast is optional in Settings; changing a selection lets the confirmed choice settle before another shot. Follow the live cooldown and cast indicator.\n\nEarn XP through play. A level gives a skill point; the existing spell tree shows each upgrade and its actual cost. Inspect first, then choose Learn. Opening this guide spends nothing.\n\nThe monster lab and tower lab are beside this alcove. Tower ranks belong to the Tower Anvil and use separate earned campaign Forge charge between stages.";
        UnityEngine.Object raykonMentorOwner;
        Text raykonMentorStatus;
        Button raykonUpgradeButton;
        int raykonShownVisits=-1;
        bool raykonShownUnlocked,raykonMentorSnapshot;
        void BuildRaykonMentorPanel()
        {
            var content=PanelContent(JourneyPanel.RaykonMentor,"RaykonSpellMentor",new Vector2(900,660));
            MenuLabel(content,"RaykonTitle","RAYKON · SPELL MENTOR",32,Ivory,68);
            MenuLabel(content,"RaykonPurpose","Choose a lesson beside the cauldron. Your actual spell tree owns all upgrades and costs.",20,Muted,86);
            raykonMentorStatus=MenuLabel(content,"RaykonAcceptedState","",18,Gold,110);
            raykonUpgradeButton=MenuButton(content,"RaykonSpellUpgrades","SPELL UPGRADES",OpenRaykonSpellTree,58);
            MenuButton(content,"RaykonCastingGuide","CASTING GUIDE",OpenRaykonCastingGuide,58);
            MenuButton(content,"RaykonReturn","RETURN TO THE REFUGE",ResumeJourney,50);
        }
        bool RaykonContext=>session!=null&&session.HasStarted&&!session.IsDead&&!session.IsDowned&&!session.Victory&&
            session.World!=null&&session.World.TryGetRaykonInteractionTarget(out _,out _,out _)&&
            (session.Defense==null||!session.Defense.Running)&&!ShopVisible&&!IntroActive&&!LoadingActive;
        public bool IsRaykonMentorVisible(UnityEngine.Object owner)=>owner!=null&&object.ReferenceEquals(owner,raykonMentorOwner)&&
            built&&currentPanel==JourneyPanel.RaykonMentor;
        public bool TryShowRaykonMentor(UnityEngine.Object owner)
        {
            if(owner==null||!built||!RaykonContext||session.Paused||currentPanel!=JourneyPanel.None)return false;
            ForgetRefugeNpcConversation();AbilityBindings.CancelRebind();
            raykonMentorOwner=owner;raykonMentorSnapshot=false;session.Pause();SetPanel(JourneyPanel.RaykonMentor);
            RefreshRaykonMentor();return IsRaykonMentorVisible(owner);
        }
        public void HideRaykonMentor(UnityEngine.Object owner)
        {
            if(!object.ReferenceEquals(owner,raykonMentorOwner))return;
            if(currentPanel==JourneyPanel.RaykonMentor)ClosePanels();
            raykonMentorOwner=null;raykonMentorSnapshot=false;
        }
        void RefreshRaykonMentor()
        {
            if(currentPanel!=JourneyPanel.RaykonMentor)return;
            if(raykonMentorOwner==null||!RaykonContext||!session.Paused){ClosePanels();raykonMentorOwner=null;return;}
            int visits=CampaignUiInRefuge?campaignUi.LocalVisitMask:-1;
            bool unlocked=session.CombatUnlocked||CampaignUiInRefuge;
            if(raykonMentorSnapshot&&raykonShownVisits==visits&&raykonShownUnlocked==unlocked)return;
            raykonMentorSnapshot=true;raykonShownVisits=visits;raykonShownUnlocked=unlocked;
            raykonUpgradeButton.interactable=unlocked;
            raykonMentorStatus.text=!unlocked?"Complete the focus lesson to open spell upgrades. The casting guide is available now.":
                visits<0?"Inspect your spell tree. Learning uses its existing skill points; reading is free.":
                (visits&CampaignQuestState.RunekeeperRole)==0?"Raykon visit: awaiting host confirmation. Reading is free; learning still requires host acceptance.":
                "Raykon visit recorded. Inspect upgrades, then choose Learn only if you want to spend a skill point.";
        }
        void OpenRaykonSpellTree()
        {
            if(currentPanel!=JourneyPanel.RaykonMentor||!RaykonContext||!(session.CombatUnlocked||CampaignUiInRefuge))return;
            session.OpenSkillTree();
        }
        void OpenRaykonCastingGuide()
        {
            if(currentPanel!=JourneyPanel.RaykonMentor||!RaykonContext)return;
            ShowLoreBook("Raykon · Casting guide",RaykonCastingGuide);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing CanvasScaler reference pixels; actor/lab anchors remain owned by World in Y-up metres.
// Array Formats: One cold existing-style panel, two choice buttons, static guide and cached accepted-state scalar snapshot; no per-frame strings when unchanged.
// Dependencies/Pointers: Existing SkillTree/Progression/host transactions alone learn/spend; FieldSmith campaign bridge supplies accepted visits. This modal owns presentation only, no input poll/economy/save/native authority.
