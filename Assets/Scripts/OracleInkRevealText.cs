using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OdysseyOracle
{
    /// <summary>Reveals one existing full Text mesh through vertex alpha; never slices the book string.</summary>
    [DisallowMultipleComponent]
    public sealed class OracleInkRevealText : UnityEngine.UI.Text, IPointerClickHandler
    {
        const int MaximumAnimatedGlyphs = 8192;
        const float GlyphsPerSecond = 42f;
        float inkPosition;
        int quadCount;
        bool revealing, presentationActive, applicationPaused, writing, meshLimitReached;
        public Action<bool> WritingStateChanged;
        public Action<bool> RevealStateChanged;
        public bool IsRevealing => revealing;

        public void BeginReveal(string fullText, bool instant)
        {
            SetWriting(false);
            text = fullText ?? "";
            supportRichText = false;
            inkPosition = 0;
            quadCount = 0;
            meshLimitReached = false;
            revealing = !instant && !string.IsNullOrWhiteSpace(text) && text.Length <= MaximumAnimatedGlyphs;
            RevealStateChanged?.Invoke(revealing);
            SetVerticesDirty();
        }

        public void SetPresentationActive(bool active)
        {
            presentationActive = active;
            if (!active) SetWriting(false);
        }

        public void FinishReveal()
        {
            if (!revealing) { SetWriting(false); return; }
            revealing = false;
            inkPosition = quadCount + 2;
            SetWriting(false);
            RevealStateChanged?.Invoke(false);
            SetVerticesDirty();
        }

        public void CancelReveal()
        {
            presentationActive = false;
            FinishReveal();
            SetWriting(false);
        }

        void Update()
        {
            if (meshLimitReached) { meshLimitReached = false; FinishReveal(); return; }
            if (!revealing || !presentationActive || !Application.isFocused || applicationPaused || !isActiveAndEnabled)
            { SetWriting(false); return; }
            if (quadCount == 0) { SetWriting(false); SetVerticesDirty(); return; }
            SetWriting(true);
            inkPosition += Mathf.Clamp(Time.unscaledDeltaTime, 0, .05f) * GlyphsPerSecond;
            if (inkPosition >= quadCount + 2) { FinishReveal(); return; }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertices)
        {
            base.OnPopulateMesh(vertices);
            quadCount = vertices.currentVertCount / 4;
            if (!revealing) return;
            if (quadCount > MaximumAnimatedGlyphs) { meshLimitReached = true; return; }
            var vertex = new UIVertex();
            for (var index = 0; index < vertices.currentVertCount; index++)
            {
                vertices.PopulateUIVertex(ref vertex, index);
                var alpha = OracleInkRevealMath.Alpha(index / 4, inkPosition);
                vertex.color.a = (byte)(vertex.color.a * alpha);
                vertices.SetUIVertex(vertex, index);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging || !revealing) return;
            FinishReveal();
            eventData.Use(); // Finishing never closes the book or advances any guide.
        }

        void SetWriting(bool active)
        {
            if (writing == active) return;
            writing = active;
            WritingStateChanged?.Invoke(active);
        }

        void OnApplicationFocus(bool focused) { if (!focused) SetWriting(false); }
        void OnApplicationPause(bool paused) { applicationPaused = paused; if (paused) SetWriting(false); }
        protected override void OnDisable() { SetWriting(false); base.OnDisable(); }
        protected override void OnDestroy()
        {
            SetWriting(false);
            WritingStateChanged = null;
            RevealStateChanged = null;
            base.OnDestroy();
        }
    }

    internal static class OracleInkRevealMath
    {
        public static float Alpha(int glyph, float position)
        {
            if (float.IsNaN(position) || float.IsInfinity(position)) return 1f;
            return (float)Math.Max(0, Math.Min(1, (position - Math.Max(0, glyph)) * .5f));
        }
    }

    internal static class OracleBookAccessibility
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int OracleBookPrefersReducedMotion();
#endif
        public static bool PrefersReducedMotion
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return OracleBookPrefersReducedMotion() != 0;
#else
                return false; // Native uses the explicit, saved INSTANT TEXT control.
#endif
            }
        }
    }

    // # ARCHITECTURE SNAPSHOT
    // Space: existing Canvas text vertices. Format: one full immutable book string,
    // scalar glyph progress, reused UIVertex structs; no per-character objects/strings.
    // HUD owns modal visibility; this helper owns presentation clock and transition
    // callbacks only. GameAudioDirector owns actual playback and its volume gates.
}
