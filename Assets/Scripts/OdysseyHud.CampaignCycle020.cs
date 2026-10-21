using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        RectTransform campaignCycleHudRoot020;
        CampaignCycleClockGraphic020 campaignCycleGraphic020;

#if UNITY_EDITOR
        public bool TryReadCampaignCycleHud020(out CampaignCycleView020 snapshot, out bool visible)
        {
            snapshot = default;
            visible = campaignCycleHudRoot020 != null && campaignCycleHudRoot020.gameObject.activeInHierarchy;
            return campaignCycleGraphic020 != null && campaignCycleGraphic020.TryReadEditorSnapshot020(out snapshot);
        }
#endif
        void BuildCampaignCycleHud020()
        {
            if (campaignCycleHudRoot020 != null || minimapRoot == null) return;
            campaignCycleHudRoot020 = Frame(minimapRoot, "AcceptedCampaignDayNightClock020", Vector2.zero, new Vector2(0, 1),
                new Vector2(0, -ReadinessHudMath.Gap), new Vector2(229, ReadinessHudMath.FooterHeight), Panel, MutedGold);
            var pass = campaignCycleHudRoot020.gameObject.AddComponent<CanvasGroup>();
            pass.blocksRaycasts = false;
            pass.interactable = false;
            var node = Rect(campaignCycleHudRoot020, "RetainedNumericVoxelClock020", Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(229, ReadinessHudMath.FooterHeight));
            campaignCycleGraphic020 = node.gameObject.AddComponent<CampaignCycleClockGraphic020>();
            campaignCycleGraphic020.raycastTarget = false;
            campaignCycleHudRoot020.gameObject.SetActive(false);
        }

        void RefreshCampaignCycleHud020()
        {
            if (campaignCycleHudRoot020 == null || campaignCycleGraphic020 == null) return;
            CampaignCycleView020 view = default;
            bool visible = CampaignHubHudMode020 && session != null && session.HasStarted && session.World != null &&
                session.CampaignHubActive020 && campaignHubMinimapPresentationActive020 && readinessFooterFits &&
                minimapRoot != null && minimapRoot.gameObject.activeInHierarchy && !IntroActive && !LoadingActive &&
                !ShopVisible && !DownedActive && !session.IsDead && !session.IsDowned && !session.Victory &&
                session.CampaignAtmosphere020 != null && session.CampaignAtmosphere020.TryReadCampaignCycle020(out view) &&
                view.Generation == session.World.CampaignHubGeneration020 && view.Revision == session.World.WorldRevision020;
            if (campaignCycleHudRoot020.gameObject.activeSelf != visible) campaignCycleHudRoot020.gameObject.SetActive(visible);
            if (visible) campaignCycleGraphic020.SetSnapshot020(view);
            else campaignCycleGraphic020.ClearSnapshot020();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing minimap-local229x62 readiness footer in the accepted campaign-hub mode; no world or screen-space camera object.
// Array Formats: One cold retained RectTransform/CanvasGroup/Graphic and one value-only cycle snapshot; uGUI geometry rebuilds only when its numeric label changes.
// Dependencies/Pointers: OdysseyHud builds after the minimap/readiness footer and refreshes after the campaign minimap call. It hides for invalid scope, non-hub mode, or existing protected overlays. Editor-only diagnostic read returns the existing visibility/snapshot without refreshing or changing it. No input, network, NPC, save, or Gameplay authority.


