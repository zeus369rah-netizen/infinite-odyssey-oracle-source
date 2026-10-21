using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        private RectTransform staminaRoot020;
        private UnityEngine.UI.Image staminaFill020;
        private UnityEngine.UI.Text staminaStatus020;
        private byte staminaShownStatus020 = byte.MaxValue;

        private void BuildStamina020(RectTransform health)
        {
            staminaRoot020 = Rect(health, "SprintStamina020", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(18, -112), new Vector2(291, 15));
            Label(staminaRoot020, "StaminaLabel", "STAMINA", 11, Muted, TextAnchor.MiddleLeft,
                Vector2.zero, new Vector2(60, 15), new Vector2(0, 1));
            var bar = Rect(staminaRoot020, "StaminaBar", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(65, -4), new Vector2(107, 7));
            Paint(bar, new Color(.10f, .16f, .17f, 1f));
            staminaFill020 = Paint(Stretch(bar, "StaminaFill"), new Color(.44f, .78f, .81f, 1f));
            staminaStatus020 = Label(staminaRoot020, "SprintRecoveryHint", "", 11, Muted, TextAnchor.MiddleRight,
                new Vector2(179, 0), new Vector2(112, 15), new Vector2(0, 1));
        }

        private void UpdateStamina020()
        {
            if (staminaRoot020 == null || staminaFill020 == null || staminaStatus020 == null) return;
            var player = session != null ? session.Player : null;
            bool visible = session != null && session.HasStarted && player != null;
            if (staminaRoot020.gameObject.activeSelf != visible) staminaRoot020.gameObject.SetActive(visible);
            if (!visible) return;
            SetFill(staminaFill020, player.StaminaFraction020);
            staminaFill020.color = player.StaminaExhausted020 ? new Color(.92f, .64f, .30f, 1f) : new Color(.44f, .78f, .81f, 1f);
            byte status = !player.StaminaExhausted020 ? (byte)0 : player.Stamina020 < PlayerSprintRules020.ReentryMinimum ? (byte)1 : player.SprintKeyHeld020 ? (byte)2 : (byte)3;
            if (status == staminaShownStatus020) return;
            staminaShownStatus020 = status;
            staminaStatus020.text = status == 0 ? "" : status == 1 ? "RECOVER" : status == 2 ? "RELEASE SPRINT" : "PRESS SPRINT";
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing lower-left OracleVitals panel at1920x1080 reference Canvas; row18,-112 size291x15 remains inside327x137 panel.
// Array Formats: Three cold-cached UI references and one status byte;107x7 noninteractive fill, literal state copy, no active strings or hierarchy searches.
// Dependencies/Pointers: OraclePlayer alone owns resource state. Existing HUD/Canvas controls visibility, scale and destruction; no buttons, controls, health or economy changes.
