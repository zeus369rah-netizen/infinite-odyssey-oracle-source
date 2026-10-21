// PSEUDOCODE: convert one accepted numeric hit to bounded seven-segment quads;
// expiry and actor-lifetime authority remain with the fixed parent pool.
using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{    public sealed class CombatDamageNumberGlyph022 : MaskableGraphic
    {
        private const float DigitWidth = 13f;
        private const float DigitHeight = 23f;
        private const float SegmentThickness = 2.2f;
        private const float DigitGap = 2f;
        private const float DotWidth = 3f;
        private const int MaxDisplayTenths = 9999999;

        private EnemyAcceptedHit022 hit;
        private float age, lifetime = .90f;
        private int displayTenths;
        private bool active, showFraction, capped;
        private RectTransform glyphRect;

        public bool HasHit => active;
        public float Age => age;
        public EnemyAcceptedHit022 Hit => hit;

        public void Initialize()
        {
            raycastTarget = false;
            maskable = false;
            glyphRect = transform as RectTransform;
            canvasRenderer.SetColor(Color.clear);
            color = Color.white;
        }

        public void SetHit(EnemyAcceptedHit022 value, float duration)
        {
            hit = value;
            age = 0f;
            lifetime = duration;
            float damage = value.AcceptedDamage;
            capped = damage >= MaxDisplayTenths / 10f;
            displayTenths = capped ? MaxDisplayTenths : Mathf.Clamp(Mathf.RoundToInt(damage * 10f), 0, MaxDisplayTenths);
            showFraction = displayTenths % 10 != 0;
            active = true;
            color = (value.Ingredients & (1 << 3)) != 0 || (value.Flags & EnemyHitFlags022.Wind) != 0
                ? new Color32(139, 220, 255, 255)
                : new Color32(255, 210, 111, 255);
            canvasRenderer.SetColor(Color.clear);
            SetVerticesDirty();
        }

        public void Advance(float deltaSeconds)
        {
            age = Mathf.Min(lifetime, age + deltaSeconds);
        }

        public void SetPresentation(Vector2 localPosition, float alpha, bool visible)
        {
            if (glyphRect != null && visible && (glyphRect.anchoredPosition - localPosition).sqrMagnitude > .0001f)
                glyphRect.anchoredPosition = localPosition;
            canvasRenderer.SetColor(visible && active ? new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)) : Color.clear);
        }

        public void ClearHit()
        {
            if (!active) return;
            active = false;
            hit = default;
            age = 0f;
            canvasRenderer.SetColor(Color.clear);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!active) return;

            int whole = displayTenths / 10;
            int fraction = displayTenths % 10;
            int digitCount = CountDigits(whole);
            float wholeWidth = digitCount * DigitWidth + (digitCount - 1) * DigitGap;
            float tailWidth = showFraction ? DigitGap + DotWidth + DigitGap + DigitWidth : 0f;
            float capWidth = capped ? DigitGap + 5f : 0f;
            float totalWidth = wholeWidth + tailWidth + capWidth;
            float x = -totalWidth * .5f;
            int divisor = DigitDivisor(digitCount);

            for (int i = 0; i < digitCount; i++)
            {
                int digit = whole / divisor % 10;
                DrawDigit(vh, x, digit, color);
                x += DigitWidth + DigitGap;
                divisor = Mathf.Max(1, divisor / 10);
            }

            if (showFraction)
            {
                x -= DigitGap;
                AddRect(vh, x, -DigitHeight * .5f, DotWidth, SegmentThickness, color);
                x += DotWidth + DigitGap;
                DrawDigit(vh, x, fraction, color);
                x += DigitWidth + DigitGap;
            }

            if (capped)
            {
                float plusX = x;
                AddRect(vh, plusX + 1.4f, -3.5f, SegmentThickness, 7f, color);
                AddRect(vh, plusX - .8f, -1.1f, 6.6f, SegmentThickness, color);
            }
        }

        private static int CountDigits(int value)
        {
            if (value >= 100000) return 6;
            if (value >= 10000) return 5;
            if (value >= 1000) return 4;
            if (value >= 100) return 3;
            if (value >= 10) return 2;
            return 1;
        }

        private static int DigitDivisor(int digits)
        {
            switch (digits)
            {
                case 6: return 100000;
                case 5: return 10000;
                case 4: return 1000;
                case 3: return 100;
                case 2: return 10;
                default: return 1;
            }
        }

        private static void DrawDigit(VertexHelper vh, float x, int digit, Color tint)
        {
            const int top = 1, upperRight = 2, lowerRight = 4, bottom = 8, lowerLeft = 16, upperLeft = 32, middle = 64;
            int mask;
            switch (digit)
            {
                case 0: mask = top | upperRight | lowerRight | bottom | lowerLeft | upperLeft; break;
                case 1: mask = upperRight | lowerRight; break;
                case 2: mask = top | upperRight | middle | lowerLeft | bottom; break;
                case 3: mask = top | upperRight | lowerRight | bottom | middle; break;
                case 4: mask = upperLeft | middle | upperRight | lowerRight; break;
                case 5: mask = top | upperLeft | middle | lowerRight | bottom; break;
                case 6: mask = top | upperLeft | middle | lowerLeft | lowerRight | bottom; break;
                case 7: mask = top | upperRight | lowerRight; break;
                case 8: mask = top | upperRight | lowerRight | bottom | lowerLeft | upperLeft | middle; break;
                default: mask = top | upperLeft | upperRight | middle | lowerRight | bottom; break;
            }

            float halfHeight = DigitHeight * .5f;
            float horizontalWidth = DigitWidth - SegmentThickness * 2f;
            if ((mask & top) != 0) AddRect(vh, x + SegmentThickness, halfHeight - SegmentThickness,
                horizontalWidth, SegmentThickness, tint);
            if ((mask & upperRight) != 0) AddRect(vh, x + DigitWidth - SegmentThickness, SegmentThickness,
                SegmentThickness, halfHeight - SegmentThickness * 1.5f, tint);
            if ((mask & lowerRight) != 0) AddRect(vh, x + DigitWidth - SegmentThickness, -halfHeight,
                SegmentThickness, halfHeight - SegmentThickness * 1.5f, tint);
            if ((mask & bottom) != 0) AddRect(vh, x + SegmentThickness, -halfHeight,
                horizontalWidth, SegmentThickness, tint);
            if ((mask & lowerLeft) != 0) AddRect(vh, x, -halfHeight,
                SegmentThickness, halfHeight - SegmentThickness * 1.5f, tint);
            if ((mask & upperLeft) != 0) AddRect(vh, x, SegmentThickness,
                SegmentThickness, halfHeight - SegmentThickness * 1.5f, tint);
            if ((mask & middle) != 0) AddRect(vh, x + SegmentThickness, -SegmentThickness * .5f,
                horizontalWidth, SegmentThickness, tint);
        }

        private static void AddRect(VertexHelper vh, float x, float y, float width, float height, Color tint)
        {
            int start = vh.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = tint;
            vertex.position = new Vector3(x, y, 0f); vh.AddVert(vertex);
            vertex.position = new Vector3(x, y + height, 0f); vh.AddVert(vertex);
            vertex.position = new Vector3(x + width, y + height, 0f); vh.AddVert(vertex);
            vertex.position = new Vector3(x + width, y, 0f); vh.AddVert(vertex);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Local UI pixels relative to one pooled number RectTransform.
// Array Formats: Fixed seven-segment vertex quads for one bounded numeric value; no dynamic mesh collections or strings.
// Dependencies/Pointers: Unity uGUI MaskableGraphic only; layer owns this component in a cold 24-slot pool.


