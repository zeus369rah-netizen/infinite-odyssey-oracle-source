// PSEUDOCODE: subscribe to accepted HP-change facts, copy into a bounded glyph pool,
// validate actor lifetime on display, and render through the existing HUD Canvas only.
using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed class CombatDamageNumberLayer022 : MonoBehaviour
    {
        private const int SlotCount = 24;
        private const float LifetimeSeconds = .90f;
        private readonly CombatDamageNumberGlyph022[] glyphs = new CombatDamageNumberGlyph022[SlotCount];

        private GameSession session;
        private OracleCombat combat;
        private RectTransform projectionRect;
        private Camera viewCamera;
        private int nextSlot;
        private ulong lastSerial;
        private uint activeEncounter, activeWorldRevision;
        private bool initialized;

        public void Initialize(GameSession gameSession)
        {
            if (initialized || gameSession == null) return;
            session = gameSession;
            projectionRect = transform as RectTransform;
            if (projectionRect == null) return;

            for (int i = 0; i < glyphs.Length; i++)
            {
                var child = new GameObject("DamageNumberGlyph022", typeof(RectTransform), typeof(CanvasRenderer));
                var childRect = child.transform as RectTransform;
                childRect.SetParent(transform, false);
                childRect.anchorMin = childRect.anchorMax = new Vector2(.5f, .5f);
                childRect.pivot = new Vector2(.5f, .5f);
                childRect.sizeDelta = new Vector2(148f, 34f);
                glyphs[i] = child.AddComponent<CombatDamageNumberGlyph022>();
                glyphs[i].Initialize();
            }

            initialized = true;
            AttachCurrentCombat();
        }

        private void OnEnable()
        {
            if (initialized) AttachCurrentCombat();
        }

        private void OnDisable()
        {
            DetachCombat();
            ClearAll();
        }

        private void OnDestroy()
        {
            DetachCombat();
        }

        private void AttachCurrentCombat()
        {
            if (!initialized || session == null) return;
            var candidate = session.Combat;
            if (candidate == null || ReferenceEquals(candidate, combat)) return;
            DetachCombat();
            combat = candidate;
            lastSerial = 0;
            activeEncounter = 0;
            activeWorldRevision = 0;
            combat.EnemyDamageAccepted022 += OnEnemyDamageAccepted022;
        }

        private void DetachCombat()
        {
            if (combat != null) combat.EnemyDamageAccepted022 -= OnEnemyDamageAccepted022;
            combat = null;
        }

        private bool SessionCanPresent()
        {
            return session != null && session.HasStarted && !session.Paused && !session.Victory &&
                   !session.IsDead && !session.IsDowned && session.World != null &&
                   session.Player != null && session.Player.ViewCamera != null;
        }

        private void OnEnemyDamageAccepted022(EnemyAcceptedHit022 hit)
        {
            if (!SessionCanPresent() || combat == null || !ReferenceEquals(combat, session.Combat)) return;
            if (hit.Serial == 0 || hit.Serial <= lastSerial) return;
            lastSerial = hit.Serial;
            if (!combat.IsEnemyHitCurrent022(in hit) || !FinitePositive(hit.AcceptedDamage) ||
                !Finite(hit.Position.x) || !Finite(hit.Position.y) || !Finite(hit.Position.z)) return;

            if (activeEncounter != hit.Life.Encounter || activeWorldRevision != hit.Life.WorldRevision)
            {
                ClearAll();
                activeEncounter = hit.Life.Encounter;
                activeWorldRevision = hit.Life.WorldRevision;
                nextSlot = 0;
            }

            var glyph = glyphs[nextSlot];
            nextSlot = (nextSlot + 1) % glyphs.Length;
            glyph.SetHit(hit, LifetimeSeconds);
        }

        private void Update()
        {
            if (!initialized) return;
            if (combat == null || !ReferenceEquals(combat, session != null ? session.Combat : null))
            {
                DetachCombat();
                ClearAll();
                AttachCurrentCombat();
                return;
            }
            if (!SessionCanPresent())
            {
                ClearAll();
                return;
            }

            viewCamera = session.Player.ViewCamera;
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, .10f);
            for (int i = 0; i < glyphs.Length; i++)
            {
                var glyph = glyphs[i];
                if (glyph == null || !glyph.HasHit) continue;
                var hit = glyph.Hit;
                if (!combat.IsEnemyHitCurrent022(in hit))
                {
                    glyph.ClearHit();
                    continue;
                }
                glyph.Advance(dt);
                if (glyph.Age >= LifetimeSeconds)
                {
                    glyph.ClearHit();
                    continue;
                }

                var fraction = glyph.Age / LifetimeSeconds;
                var worldPoint = hit.Position + Vector3.up * (.18f + .34f * fraction);
                var screenPoint = viewCamera.WorldToScreenPoint(worldPoint);
                if (screenPoint.z <= viewCamera.nearClipPlane || screenPoint.x < 0f || screenPoint.x > Screen.width ||
                    screenPoint.y < 0f || screenPoint.y > Screen.height ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        projectionRect, new Vector2(screenPoint.x, screenPoint.y), null, out var localPoint))
                {
                    glyph.SetPresentation(localPosition: default, alpha: 0f, visible: false);
                    continue;
                }

                localPoint.y += 18f * fraction;
                float fade = 1f - fraction;
                fade *= fade;
                glyph.SetPresentation(localPoint, fade, true);
            }
        }

        private void ClearAll()
        {
            for (int i = 0; i < glyphs.Length; i++)
                if (glyphs[i] != null) glyphs[i].ClearHit();
            activeEncounter = 0;
            activeWorldRevision = 0;
            nextSlot = 0;
        }

        private static bool FinitePositive(float value) => Finite(value) && value > 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted enemy hit world metres/Y-up -> actual ViewCamera screen -> existing HUD canvas local pixels.
// Array Formats: Fixed 24-slot glyph component pool; per-hit value is a copied EnemyAcceptedHit022 struct and bounded integer tenths.
// Dependencies/Pointers: Existing OracleCombat accepted-hit observer and full life-token validator; existing HUD Canvas and OracleEnemy health bars. No gameplay writes, per-hit strings, materials, or GameObject creation.



