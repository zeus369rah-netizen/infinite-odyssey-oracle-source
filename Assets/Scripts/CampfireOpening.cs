using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace OdysseyOracle
{
    /// <summary>One skippable opening, a shrine-facing menu, then the existing arrival.</summary>
    public sealed class CampfireOpening : MonoBehaviour
    {
        private enum Phase { Menu, Intro, Arrival, Playing }
        private const float IntroDuration = 22f;
        private const float ArrivalDuration = 1.1f;
        private const float MenuFieldOfView = 68f;
        private const float ShrineCut = 6.5f, PortalCut = 12.2f, HearthCut = 17.8f;
        private readonly Vector3 menuPosition = new Vector3(1.8f, 2.25f, -8.8f);
        private Vector3 menuLookAt => (session != null && session.World != null
            ? session.World.ShrinePosition : new Vector3(0f, 0f, 2f)) + Vector3.right * 4f + Vector3.up * 1.8f;
        private GameSession session;
        private Camera opening;
        public Camera ActiveCamera => opening != null && opening.enabled ? opening : session != null ? session.Player.ViewCamera : null;
        private Phase phase;
        private Vector3 arrivalFrom;
        private Quaternion arrivalRotation;
        private float arrivalFieldOfView, transition;
        private bool applicationPaused, skipRequested;
        private int skipFrame, suppressInputThroughFrame;

        public bool IsTransitioning => phase == Phase.Arrival;
        public bool IsIntroPlaying => phase == Phase.Intro;
        public float IntroTime { get; private set; }
        public float FadeAlpha { get; private set; }
        public string IntroTitle => !IsIntroPlaying || skipRequested || IntroTime >= HearthCut ? "" :
            IntroTime < ShrineCut ? "Welcome to Zeus Ra\u2019s\u2026" : "Infinite Odyssey";
        public string IntroSubtitle => IsIntroPlaying && !skipRequested && IntroTime >= ShrineCut &&
            IntroTime < HearthCut ? "Oracle\u2019s Return" : "";

        public void Initialize(GameSession owner)
        {
            if (opening != null || owner == null || owner.Player == null || owner.Player.ViewCamera == null) return;
            session = owner;
            opening = new GameObject("Live campfire opening camera").AddComponent<Camera>();
            opening.CopyFrom(owner.Player.ViewCamera);
            opening.fieldOfView = MenuFieldOfView;
            var data = opening.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            data.SetRenderer(SystemInfo.supportsComputeShaders ? 0 : 1);
            opening.enabled = true;
            session.Player.ViewCamera.enabled = false;
            phase = Phase.Menu;
            IntroTime = 0f;
            FadeAlpha = 0f;
            suppressInputThroughFrame = Time.frameCount + 1;
            SetMenuPose();
        }

        /// <summary>
        /// Settles the camera without starting the session. Keep the intro input
        /// blocker until the skip click has released, preventing menu click-through.
        /// </summary>
        public void SkipIntro()
        {
            if (!IsIntroPlaying || skipRequested) return;
            if (session != null && session.Audio != null) session.Audio.StartAudioFromGesture();
            skipRequested = true;
            skipFrame = Time.frameCount;
            IntroTime = IntroDuration;
            FadeAlpha = 0f;
            SetMenuPose();
        }

        public void BeginArrival()
        {
            if (IsIntroPlaying) { SkipIntro(); return; }
            if (phase != Phase.Menu || opening == null || session == null || session.Player == null) return;
            arrivalFrom = opening.transform.position;
            arrivalRotation = opening.transform.rotation;
            arrivalFieldOfView = opening.fieldOfView;
            transition = 0f;
            phase = Phase.Arrival;
            FadeAlpha = 0f;
            opening.enabled = true;
            session.Player.ViewCamera.enabled = false;
        }

        public void ReturnToMenu()
        {
            if (opening == null || session == null || session.Player == null) return;
            phase = Phase.Menu;
            skipRequested = false;
            IntroTime = IntroDuration;
            FadeAlpha = 0f;
            transition = 0f;
            opening.enabled = true;
            session.Player.ViewCamera.enabled = false;
            SetMenuPose();
        }

        private void Update()
        {
            if (!IsIntroPlaying || skipRequested || !Application.isFocused || applicationPaused ||
                Time.frameCount <= suppressInputThroughFrame) return;
            bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (click || escape) SkipIntro();
        }

        private void LateUpdate()
        {
            if (session == null || session.Player == null || session.Player.ViewCamera == null || opening == null) return;
            if (phase == Phase.Menu) { SetMenuPose(); return; }
            if (phase == Phase.Playing || applicationPaused) return;
            if (skipRequested)
            {
                // Only input/release handling needs application focus. Cinematic
                // and arrival presentation keep ticking with runInBackground.
                if (!Application.isFocused) return;
                // LateUpdate runs after UI pointer processing. The menu remains
                // blocked for the entire release frame and appears next frame.
                if (Time.frameCount > skipFrame && (Mouse.current == null || !Mouse.current.leftButton.isPressed))
                    ReturnToMenu();
                return;
            }
            float dt = Time.unscaledDeltaTime;
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) return;
            dt = Mathf.Min(dt, .1f); // A stalled or newly focused frame cannot jump between shots.
            if (IsIntroPlaying)
            {
                IntroTime = Mathf.Min(IntroDuration, IntroTime + dt);
                EvaluateIntro();
                if (IntroTime >= IntroDuration) ReturnToMenu();
                return;
            }
            // A focus-loss pause still owns gameplay/input. Finishing this
            // unscaled camera blend never resumes the session or locks input.
            if (!IsTransitioning) return;
            transition = Mathf.Min(1f, transition + dt / ArrivalDuration);
            float t = Ease(transition);
            Transform destination = session.Player.ViewCamera.transform;
            opening.transform.SetPositionAndRotation(Vector3.Lerp(arrivalFrom, destination.position, t),
                Quaternion.Slerp(arrivalRotation, destination.rotation, t));
            opening.fieldOfView = Mathf.Lerp(arrivalFieldOfView, session.Player.ViewCamera.fieldOfView, t);
            if (transition >= 1f)
            {
                phase = Phase.Playing;
                opening.enabled = false;
                session.Player.ViewCamera.enabled = true;
            }
        }

        private void EvaluateIntro()
        {
            float t = IntroTime;
            // Short black dissolves conceal each change of position. The last
            // shot finishes at the exact pose used by the campfire menu.
            FadeAlpha = t <= 1.8f ? 1f : 1f - Ease((t - 1.8f) / 1.6f);
            FadeAlpha = Mathf.Max(FadeAlpha, CutFade(t, ShrineCut));
            FadeAlpha = Mathf.Max(FadeAlpha, CutFade(t, PortalCut));
            FadeAlpha = Mathf.Max(FadeAlpha, CutFade(t, HearthCut));
            if (t < ShrineCut)
            {
                ApplyShot(new Vector3(1.15f, 1.85f, -10.35f), new Vector3(.35f, 2.05f, -9f),
                    new Vector3(-3.7f, 1.25f, -7.5f), new Vector3(-2.6f, 2.8f, -5.8f),
                    t / ShrineCut, 66f, 68f);
            }
            else if (t < PortalCut)
            {
                Vector3 ward = session.World != null ? session.World.ShrinePosition : new Vector3(0f, 0f, 2f);
                ApplyShot(new Vector3(-2.8f, 2.3f, -1.45f), new Vector3(-2.25f, 2.45f, -.25f),
                    ward + Vector3.up * 2f, ward + Vector3.up * 2.15f,
                    (t - ShrineCut) / (PortalCut - ShrineCut), 64f, 66f);
            }
            else if (t < HearthCut)
            {
                Vector3 portal = session.World != null ? session.World.PortalPosition : new Vector3(0f, .68f, 25.2f);
                ApplyShot(new Vector3(2.35f, 2.55f, 19.25f), new Vector3(1.55f, 2.45f, 20.45f),
                    portal + new Vector3(0f, 2.25f, .45f), portal + new Vector3(0f, 2.35f, .45f),
                    (t - PortalCut) / (HearthCut - PortalCut), 63f, 65f);
            }
            else
            {
                ApplyShot(new Vector3(-1f, 2.15f, -1.7f), menuPosition,
                    new Vector3(-3f, 1.2f, 5.25f), menuLookAt,
                    (t - HearthCut) / (IntroDuration - HearthCut), MenuFieldOfView, MenuFieldOfView);
            }
        }

        private void ApplyShot(Vector3 start, Vector3 end, Vector3 lookStart, Vector3 lookEnd,
            float progress, float startFov, float endFov)
        {
            float t = Ease(progress);
            Vector3 position = Vector3.Lerp(start, end, t);
            Vector3 look = Vector3.Lerp(lookStart, lookEnd, t);
            opening.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position, Vector3.up));
            opening.fieldOfView = Mathf.Lerp(startFov, endFov, t);
        }

        private void SetMenuPose()
        {
            if (opening == null) return;
            opening.transform.SetPositionAndRotation(menuPosition, Quaternion.LookRotation(menuLookAt - menuPosition, Vector3.up));
            opening.fieldOfView = MenuFieldOfView;
        }

        private static float Ease(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));
        private static float CutFade(float time, float cut) => 1f - Ease(Mathf.Abs(time - cut) / .4f);
        private void OnApplicationFocus(bool focused) { suppressInputThroughFrame = Time.frameCount + 1; }
        private void OnApplicationPause(bool paused)
        { applicationPaused = paused; suppressInputThroughFrame = Time.frameCount + 1; }
        private void OnDestroy()
        {
            if (opening != null) Destroy(opening.gameObject);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Main-thread presentation camera in Unity world metres/Y-up; scalar unscaled timeline only.
// Session/player/native/spatial owners are borrowed. No gameplay clock, pause, actor, input lock or profile writes.
// Focus gates user input/release; application suspension still gates presentation progress.
