using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Main-thread adapter for existing overlay/menu labels; no new Canvas, input, buffers or native ownership.
    // Revision checks reuse text snapshots; formatting occurs only in telemetry's bounded refresh.
    /// <summary>Bind existing HUD/menu labels. Creates no Canvas, EventSystem, input handler or new font material.</summary>
    public sealed class PerformanceTelemetryReadout : MonoBehaviour
    {
        private OraclePerformanceTelemetry telemetry;
        private UnityEngine.UI.Text overlay, menu;
        private bool menuOpen;
        private int revision = -1;

        public void Initialize(OraclePerformanceTelemetry source, UnityEngine.UI.Text existingOverlayLabel, UnityEngine.UI.Text existingMenuLabel)
        {
            if (telemetry != null && menuOpen) telemetry.SetMenuOpen(false);
            telemetry = source; overlay = existingOverlayLabel; menu = existingMenuLabel;
            if (overlay != null) overlay.raycastTarget = false;
            if (menu != null) menu.raycastTarget = false;
            revision = -1;
            if (telemetry != null) telemetry.SetMenuOpen(menuOpen);
            Refresh();
        }

        public void SetOverlayVisible(bool visible) { telemetry?.SetOverlayVisible(visible); Refresh(); }
        public void SetMenuOpen(bool open) { menuOpen = open; telemetry?.SetMenuOpen(open); Refresh(); }

        private void LateUpdate()
        { if (telemetry != null && revision != telemetry.Revision) Refresh(); }

        private void Refresh()
        {
            if (telemetry == null) return;
            revision = telemetry.Revision;
            if (overlay != null)
            {
                overlay.enabled = telemetry.OverlayVisible;
                overlay.text = telemetry.CompactText;
            }
            if (menu != null)
            {
                menu.enabled = menuOpen;
                menu.text = telemetry.DetailsText;
            }
        }

        private void OnDisable()
        {
            if (telemetry != null && menuOpen) telemetry.SetMenuOpen(false);
            if (overlay != null) overlay.enabled = false;
            if (menu != null) menu.enabled = false;
        }
        private void OnEnable()
        {
            if (telemetry != null && menuOpen) telemetry.SetMenuOpen(true);
            revision = -1;
            Refresh();
        }
        private void OnDestroy() { if (telemetry != null && menuOpen) telemetry.SetMenuOpen(false); }
    }
}
