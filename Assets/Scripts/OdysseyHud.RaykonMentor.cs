using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        const string RaykonCastingGuide="CASTING WITH RAYKON\nTap one element to select a castable single. Add a second distinct element for a pair. Three selected elements launch a simultaneous single-element volley, then clear. All four make Cosmic Nova. Single and pair recipes stay selected for repeat casts; your next element tap starts a new recipe.\n\nLeft-click casts a prepared recipe when ready. Or hold left-click and press fresh element keys: those fresh bits cast as soon as ready, then clear. Global cast interval: 0.5 seconds. Normal ingredients recover in 4 seconds; Nova uses all four and recovers in 5 seconds. Optional auto-cast waits at least 0.5 seconds after each accepted selection, as well as the remaining cast and ingredient cooldowns. Follow the live indicators.\n\nEarn XP through real play. A level grants a skill point; the existing spell tree shows each upgrade and its actual cost. Inspect first, then choose Learn. Opening this guide spends nothing.\n\nThe practice areas are beside this alcove. Tower ranks belong to the Tower Anvil and use separate earned campaign Forge charge between cleared levels before the next defense.";
        UnityEngine.Object raykonMentorOwner;
        Text raykonMentorStatus;
        Button raykonUpgradeButton;
        int raykonShownVisits=-1;
        bool raykonShownUnlocked,raykonMentorSnapshot,raykonShownFirstUpgrade;
        void BuildRaykonMentorPanel()
        {
            var content=PanelContent(JourneyPanel.RaykonMentor,"RaykonSpellMentor",new Vector2(900,684));
            MenuLabel(content,"RaykonTitle","RAYKON · SPELL MENTOR",32,Ivory,68);
            MenuLabel(content,"RaykonPurpose","First spell upgrade: an optional lesson. Your actual spell tree owns the points, upgrades and costs.",20,Muted,86);
            raykonMentorStatus=MenuLabel(content,"RaykonAcceptedState","",18,Gold,134);
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
            bool firstUpgrade=session.RefugeActivities!=null&&session.RefugeActivities.FirstSpellUpgradeAccepted;
            if(raykonMentorSnapshot&&raykonShownVisits==visits&&raykonShownUnlocked==unlocked&&raykonShownFirstUpgrade==firstUpgrade)return;
            raykonMentorSnapshot=true;raykonShownVisits=visits;raykonShownUnlocked=unlocked;raykonShownFirstUpgrade=firstUpgrade;
            raykonUpgradeButton.interactable=unlocked;
            raykonMentorStatus.text=!unlocked?"Complete the focus lesson to open spell upgrades. The casting guide is available now.":
                visits<0?"Inspect your spell tree. Learning uses its existing skill points; reading is free.":
                (visits&CampaignQuestState.RunekeeperRole)==0?"Raykon visit: awaiting host confirmation. Reading is free; learning still requires host acceptance.":
                "Raykon visit recorded. Inspect upgrades, then choose Learn only if you want to spend a skill point.";
            raykonMentorStatus.text+=firstUpgrade?"\nFirst spell upgrade · COMPLETE for this profile.":"\nFirst spell upgrade · A real accepted skill-tree node completes it; visiting or requesting does not.";
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

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Stable existing arena road IDs and host-owned eight-level campaign identity.
// Array Formats: Existing two peers/eight lane arrays/four shared pads/48 actors/4096 reward tombstones; no ABI growth.
// Dependencies/Pointers: Level-derived lane prefix, actual host ready/admission/recovery fences; root owns shared protocol and native acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up, input bits and owner simulation seconds.
// Array Formats: Four ingredient deadlines, fixed48 projectile pool, at most three component shots; existing host frame ABIs.
// Dependencies/Pointers: Work-only rapid-casting candidate. Existing host hit/damage/reward owners retained; root owns native/browser acceptance.
