// PSEUDOCODE: append one fixed visual layer after the existing HUD is built;
// it subscribes to the current combat owner and retires with the HUD canvas.
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        private bool combatFeedbackBuilt022;

        private void Start()
        {
            if (combatFeedbackBuilt022 || !built || hudRoot == null || session == null) return;
            BuildCombatFeedbackHud022(hudRoot, session);
            combatFeedbackBuilt022 = true;
        }

        private void BuildCombatFeedbackHud022(RectTransform parent, GameSession currentSession)
        {
            if (parent == null || currentSession == null) return;
            var root = new GameObject("CombatFeedbackLayer022", typeof(RectTransform), typeof(CombatDamageNumberLayer022));
            var rect = root.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .5f);
            root.GetComponent<CombatDamageNumberLayer022>().Initialize(currentSession);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing full-screen Odyssey HUD Canvas and its anchored local pixel space.
// Array Formats: HUD owns one cold-created bounded 24-slot glyph layer; no per-hit UI object or text allocation.
// Dependencies/Pointers: One guarded Initialize hook in OdysseyHud; child retires with the existing HUD root. OracleEnemy remains the sole HP bar owner.

