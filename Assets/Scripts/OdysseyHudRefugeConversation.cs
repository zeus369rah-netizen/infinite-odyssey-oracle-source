using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        // Reuse the actual paper/reveal/scroll/Return modal. The actor owner supplies
        // only text and at most two accepted visit rows; no new input/quest policy.
        private UnityEngine.Object refugeConversationOwner;
        public bool IsRefugeNpcConversationVisible(UnityEngine.Object owner)=>
            !object.ReferenceEquals(owner,null)&&object.ReferenceEquals(refugeConversationOwner,owner)&&
            built&&currentPanel==JourneyPanel.Lore&&panels[JourneyPanel.Lore].gameObject.activeInHierarchy;
        public bool TryShowRefugeNpcConversation(UnityEngine.Object owner,string title,string body)
        {
            if(owner==null||!built||session==null||!session.HasStarted||session.Paused||currentPanel!=JourneyPanel.None||
                session.IsDead||session.IsDowned||session.Victory||ShopVisible||IntroActive||LoadingActive)return false;
            ShowLoreBook(title,body);
            if(currentPanel!=JourneyPanel.Lore)return false;
            refugeConversationOwner=owner;return IsRefugeNpcConversationVisible(owner);
        }
        public void RefreshRefugeNpcConversation(UnityEngine.Object owner,string acceptedBody)
        {
            if(!IsRefugeNpcConversationVisible(owner)||loreInk==null)return;
            float position=loreScroll.verticalNormalizedPosition;
            // A real accepted visit updates the two rows without restarting pencil
            // audio/reveal or moving the reader. It does not acknowledge gameplay.
            loreInk.BeginReveal(acceptedBody,true);Canvas.ForceUpdateCanvases();
            loreScroll.verticalNormalizedPosition=position;
        }
        public void HideRefugeNpcConversation(UnityEngine.Object owner)
        {
            if(IsRefugeNpcConversationVisible(owner))ClosePanels();
            if(object.ReferenceEquals(refugeConversationOwner,owner))refugeConversationOwner=null;
        }
        private void ForgetRefugeNpcConversation(){refugeConversationOwner=null;}
    }
}
