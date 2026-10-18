using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // CharacterController, input and camera transforms remain main-thread Unity work.
    // Scalar velocity/bob state is reused; no jobs or native containers are owned here.
    // Saved explicit camera choices win over the staged fresh-profile vertical-FOV default.
    /// <summary>First-person movement. GameSession owns all requests to lock the cursor.</summary>
    [DisallowMultipleComponent]
    public sealed class OraclePlayer : MonoBehaviour
    {
        private const float EyeHeight = 1.65f;
        private const float WalkSpeed = 4.2f;
        private const float SprintSpeed = 6.2f;
        private const float Acceleration = 22f;
        private const float Deceleration = 30f;
        private const float Gravity = -24f;
        private const float JumpHeight = 1.05f;
        private const float GroundStickSpeed = -2f;
        private const float StepHeight = 0.3f;
        private const float CursorRequestGrace = 0.35f;
        public const float DefaultVerticalFieldOfView = GraphicsPresetCatalog.DefaultVerticalFieldOfView;
        private const float DefaultSensitivity = .105f, DefaultFieldOfView = DefaultVerticalFieldOfView, DefaultShake = .35f, DefaultBobbing = .65f;
        private const string SensitivityPreference = "OdysseyOracle.MouseSensitivity";
        private const string FovPreference = "OdysseyOracle.FieldOfView";
        private const string ShakePreference = "OdysseyOracle.ScreenShakeIntensity";
        private const string BobbingPreference = "OdysseyOracle.BobbingIntensity";

        [SerializeField, Range(0.035f, 0.25f)] private float mouseSensitivity = 0.105f;
        [SerializeField, Range(70f, 110f)] private float fieldOfView = DefaultFieldOfView;
        [SerializeField, Range(0f, 1f)] private float screenShakeIntensity = DefaultShake;
        [SerializeField, Range(0f, 1f)] private float bobbingIntensity = DefaultBobbing;

        private CharacterController controller;
        private Vector3 spawnPoint;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float targetYaw;
        private float targetPitch;
        private float currentYaw;
        private float currentPitch;
        private float bobPhase;
        private float bobBlend;
        private float sprintBobBlend;
        private Vector3 bobOffset;
        private bool groundedForBobbing;
        private float cursorRequestTime;
        private int suppressInputThroughFrame;
        private bool initialized;
        private bool wasPlayable;
        private bool hadLockedCursor;
        private bool windowHasFocus = true;
        private bool downedPose;
        private bool preferencesDirty, feedbackApplied;
        private float preferenceSaveAt, feedbackRemaining, feedbackTime;
        private Vector3 feedbackOffset;
        private Quaternion feedbackRotation = Quaternion.identity;

        public Camera ViewCamera { get; private set; }
        public Transform ViewRoot { get; private set; }
        internal float StepPhase => bobPhase;
        internal float StepBlend => bobBlend;
        internal float SprintBobBlend => sprintBobBlend;
        internal bool GroundedForBobbing => groundedForBobbing;

        public float BobbingIntensity
        {
            get => bobbingIntensity;
            set
            {
                bobbingIntensity = ClampFinite(value, 0f, 1f, DefaultBobbing);
                SavePreference(BobbingPreference, bobbingIntensity);
            }
        }

        public void SetBobbingIntensity(float value) => BobbingIntensity = value;

        /// <summary>Degrees per mouse pixel, independent of frame duration.</summary>
        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set
            {
                mouseSensitivity = ClampFinite(value, .035f, .25f, DefaultSensitivity);
                SavePreference(SensitivityPreference, mouseSensitivity);
            }
        }

        public float MouseSensitivityNormalized
        {
            get => Mathf.InverseLerp(.035f, .25f, mouseSensitivity);
            set => MouseSensitivity = Mathf.Lerp(.035f, .25f,
                ClampFinite(value, 0f, 1f, Mathf.InverseLerp(.035f, .25f, DefaultSensitivity)));
        }

        public float FieldOfView
        {
            get => fieldOfView;
            set
            {
                fieldOfView = ClampFinite(value, 70f, 110f, DefaultFieldOfView);
                if (ViewCamera != null) ViewCamera.fieldOfView = fieldOfView;
                SavePreference(FovPreference, fieldOfView);
            }
        }

        public float ScreenShakeIntensity
        {
            get => screenShakeIntensity;
            set
            {
                screenShakeIntensity = ClampFinite(value, 0f, 1f, DefaultShake);
                if (screenShakeIntensity <= 0f) ClearDamageFeedback();
                SavePreference(ShakePreference, screenShakeIntensity);
            }
        }

        public void ResetControlPreferences()
        {
            MouseSensitivity = DefaultSensitivity;
            FieldOfView = DefaultFieldOfView;
            ScreenShakeIntensity = DefaultShake;
            BobbingIntensity = DefaultBobbing;
            ClearDamageFeedback();
            FlushControlPreferences(true);
        }

        public void PlayDamageFeedback()
        {
            if (downedPose || screenShakeIntensity <= 0f || ViewRoot == null) return;
            feedbackRemaining = .18f;
            feedbackTime = 0f;
        }

        public void Initialize(Vector3 spawn)
        {
            mouseSensitivity = ClampFinite(PlayerPrefs.GetFloat(SensitivityPreference, DefaultSensitivity), .035f, .25f, DefaultSensitivity);
            fieldOfView = ClampFinite(PlayerPrefs.GetFloat(FovPreference, DefaultFieldOfView), 70f, 110f, DefaultFieldOfView);
            screenShakeIntensity = ClampFinite(PlayerPrefs.GetFloat(ShakePreference, DefaultShake), 0f, 1f, DefaultShake);
            bobbingIntensity = ClampFinite(PlayerPrefs.GetFloat(BobbingPreference, DefaultBobbing), 0f, 1f, DefaultBobbing);
            spawnPoint = IsFinite(spawn) ? spawn : new Vector3(0f, 0.2f, -10f);
            controller = GetComponent<CharacterController>();
            if (controller == null)
                controller = gameObject.AddComponent<CharacterController>();

            controller.height = 1.85f;
            controller.radius = 0.29f;
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            controller.slopeLimit = 48f;
            controller.stepOffset = StepHeight;
            controller.skinWidth = 0.035f;
            controller.minMoveDistance = 0f;
            controller.detectCollisions = true;

            if (ViewRoot == null)
            {
                ViewRoot = new GameObject("Oracle View").transform;
                ViewRoot.SetParent(transform, false);
                ViewRoot.localPosition = Vector3.up * EyeHeight;
                ViewCamera = ViewRoot.gameObject.AddComponent<Camera>();
                ViewCamera.tag = "MainCamera";
                ViewCamera.fieldOfView = fieldOfView;
                ViewCamera.nearClipPlane = 0.06f;
                ViewCamera.farClipPlane = 110f;
                ViewCamera.clearFlags = CameraClearFlags.SolidColor;
                ViewCamera.backgroundColor = new Color(0.017f, 0.022f, 0.029f);
                ViewRoot.gameObject.AddComponent<AudioListener>();
            }
            ViewCamera.fieldOfView = fieldOfView;

#if UNITY_WEBGL && !UNITY_EDITOR
            // Escape must really release browser control and reveal the pause menu.
            WebGLInput.stickyCursorLock = false;
#endif
            windowHasFocus = Application.isFocused;
            initialized = true;
            Teleport(spawnPoint);
        }

        public void Teleport(Vector3 position, float yaw = 0f)
        {
            ClearDamageFeedback();
            if (!IsFinite(position))
                position = spawnPoint;
            if (float.IsNaN(yaw) || float.IsInfinity(yaw))
                yaw = 0f;

            bool controllerWasEnabled = controller != null && controller.enabled;
            if (controllerWasEnabled)
                controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (controllerWasEnabled)
                controller.enabled = true;

            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            currentYaw = targetYaw = yaw;
            currentPitch = targetPitch = 0f;
            bobPhase = bobBlend = 0f;
            sprintBobBlend = 0f;
            bobOffset = Vector3.zero;
            groundedForBobbing = false;
            downedPose = false;
            wasPlayable = hadLockedCursor = false;
            suppressInputThroughFrame = Time.frameCount + 1;
            if (ViewRoot != null)
            {
                ViewRoot.localPosition = Vector3.up * EyeHeight;
                ViewRoot.localRotation = Quaternion.identity;
            }
            Physics.SyncTransforms();
        }

        public void SetDownedPose(bool downed)
        {
            ClearDamageFeedback();
            downedPose = downed;
            groundedForBobbing = false;
            bobOffset = Vector3.zero;
            bobBlend = sprintBobBlend = 0f;
            StopInput();
            verticalVelocity = 0f;
            suppressInputThroughFrame = Time.frameCount + 1;
            if (!downed && ViewRoot != null)
            {
                ViewRoot.localPosition = Vector3.up * EyeHeight;
                ViewRoot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
                bobPhase = bobBlend = 0f;
            }
        }

        private void Update()
        {
            FlushControlPreferences(false);
            RemoveDamageFeedback();
            if (!initialized || controller == null || !controller.enabled || ViewRoot == null)
                return;

            GameSession session = GameSession.Instance;
            if (downedPose)
            {
                StopInput();
                float downedDelta = Mathf.Clamp(Time.unscaledDeltaTime, 0f, .05f);
                if (float.IsNaN(downedDelta) || float.IsInfinity(downedDelta)) downedDelta = 0f;
                controller.stepOffset = 0f;
                if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = GroundStickSpeed;
                verticalVelocity = Mathf.Max(verticalVelocity + Gravity * downedDelta, -38f);
                CollisionFlags downedCollision = controller.Move(Vector3.up * verticalVelocity * downedDelta);
                if ((downedCollision & CollisionFlags.Below) != 0) verticalVelocity = GroundStickSpeed;
                float blend = 1f - Mathf.Exp(-5f * downedDelta);
                ViewRoot.localPosition = Vector3.Lerp(ViewRoot.localPosition, new Vector3(.12f, .42f, 0f), blend);
                ViewRoot.localRotation = Quaternion.Slerp(ViewRoot.localRotation, Quaternion.Euler(6f, 0f, 18f), blend);
                return;
            }
            bool playable = session != null && session.IsPlaying;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (!playable)
            {
                StopInput();
                UpdateHeadbob(0f, false, dt);
                return;
            }

            if (!windowHasFocus)
            {
                PauseForLostControl(session);
                return;
            }

            if (!wasPlayable)
            {
                wasPlayable = true;
                hadLockedCursor = false;
                cursorRequestTime = Time.realtimeSinceStartup;
                suppressInputThroughFrame = Time.frameCount + 1;
                planarVelocity = Vector3.zero;
                targetYaw = currentYaw = transform.eulerAngles.y;
                targetPitch = currentPitch;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                // Web pointer lock is asynchronous. Wait briefly, without consuming input.
                if (hadLockedCursor || Time.realtimeSinceStartup - cursorRequestTime >= CursorRequestGrace)
                    PauseForLostControl(session);
                return;
            }

            if (!hadLockedCursor)
            {
                hadLockedCursor = true;
                suppressInputThroughFrame = Time.frameCount + 1;
            }

            if (transform.position.y < -8f || !IsFinite(transform.position))
            {
                Teleport(spawnPoint);
                return;
            }

            bool suppressInput = Time.frameCount <= suppressInputThroughFrame;
            ApplyLook(suppressInput, dt);
            MovePlayer(suppressInput, dt);
        }

        private void ApplyLook(bool suppressInput, float dt)
        {
            Mouse mouse = Mouse.current;
            if (!suppressInput && mouse != null)
            {
                // DeltaControl already accumulates pixels for this frame; do not multiply by dt.
                Vector2 delta = mouse.delta.ReadValue();
                if (IsFinite(new Vector3(delta.x, delta.y, 0f)))
                {
                    targetYaw += delta.x * MouseSensitivity;
                    targetPitch = Mathf.Clamp(targetPitch - delta.y * MouseSensitivity, -84f, 84f);
                }
            }

            float smoothing = 1f - Mathf.Exp(-32f * dt);
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, smoothing);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, smoothing);
            if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, targetYaw)) < 0.01f)
                currentYaw = targetYaw;
            if (Mathf.Abs(currentPitch - targetPitch) < 0.01f)
                currentPitch = targetPitch;
            // Keep long sessions in a small numeric range without changing orientation.
            targetYaw = currentYaw + Mathf.DeltaAngle(currentYaw, targetYaw);
            if (Mathf.Abs(currentYaw) > 720f)
            {
                float turns = Mathf.Floor(currentYaw / 360f) * 360f;
                currentYaw -= turns;
                targetYaw -= turns;
            }
            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            ViewRoot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
        }

        private void MovePlayer(bool suppressInput, float dt)
        {
            Keyboard keyboard = Keyboard.current;
            Vector2 input = Vector2.zero;
            bool sprinting = false;
            bool jumpPressed = false;
            if (!suppressInput && keyboard != null)
            {
                input.x = (keyboard[GameActionBindings.GetKey("Right")].isPressed ? 1f : 0f) -
                    (keyboard[GameActionBindings.GetKey("Left")].isPressed ? 1f : 0f);
                input.y = (keyboard[GameActionBindings.GetKey("Forward")].isPressed ? 1f : 0f) -
                    (keyboard[GameActionBindings.GetKey("Back")].isPressed ? 1f : 0f);
                input = Vector2.ClampMagnitude(input, 1f);
                sprinting = keyboard[GameActionBindings.GetKey("Sprint")].isPressed;
                jumpPressed = keyboard[GameActionBindings.GetKey("Jump")].wasPressedThisFrame;
            }

            GameSession session = GameSession.Instance;
            int passiveMask = session != null && session.Progression != null ? session.Progression.EffectivePassiveMask : 0;
            float progressionSpeed = ElementProgression.GetMoveSpeedMultiplier(passiveMask);
            float speed = (sprinting ? SprintSpeed : WalkSpeed) * progressionSpeed;
            Vector3 desiredVelocity = (transform.right * input.x + transform.forward * input.y) * speed;
            float acceleration = input.sqrMagnitude > 0f ? Acceleration : Deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, acceleration * dt);
            if (input.sqrMagnitude == 0f && planarVelocity.sqrMagnitude < 0.0001f)
                planarVelocity = Vector3.zero;

            bool grounded = controller.isGrounded;
            controller.stepOffset = grounded ? StepHeight : 0f;
            if (grounded && verticalVelocity < 0f)
                verticalVelocity = GroundStickSpeed;
            if (grounded && jumpPressed)
                verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            verticalVelocity = Mathf.Max(verticalVelocity + Gravity * dt, -38f);

            Vector3 previousPosition = transform.position;
            CollisionFlags collision = controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            if ((collision & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;
            if ((collision & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
                verticalVelocity = GroundStickSpeed;

            Vector3 displacement = transform.position - previousPosition;
            displacement.y = 0f;
            UpdateHeadbob(displacement.magnitude, controller.isGrounded, dt, sprinting);
        }

        private void UpdateHeadbob(float distance, bool grounded, float dt, bool sprinting = false)
        {
            groundedForBobbing = grounded && !downedPose;
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            float actualSpeed = distance / Mathf.Max(dt, .0001f);
            bool moving = groundedForBobbing && distance > .0005f && actualSpeed < 12f;
            float blend = 1f - Mathf.Exp(-dt * 10f);
            bobBlend = Mathf.Lerp(bobBlend, moving ? Mathf.Clamp01(actualSpeed / WalkSpeed) : 0f, blend);
            sprintBobBlend = Mathf.Lerp(sprintBobBlend, moving && sprinting ? 1f : 0f, blend);
            if (!moving && bobBlend < .0001f) bobBlend = 0f;
            if (moving)
                bobPhase = Mathf.Repeat(bobPhase + distance * Mathf.Lerp(4.6f, 3.9f, sprintBobBlend), Mathf.PI * 4f);
            // Both camera and wand follow actual grounded steps; blocked travel and air time settle.
            float strength = bobBlend * bobbingIntensity * Mathf.Lerp(1f, .75f, sprintBobBlend);
            Vector3 target = groundedForBobbing ? new Vector3(
                Mathf.Sin(bobPhase * .5f) * .005f, Mathf.Sin(bobPhase) * .012f, 0f) * strength : Vector3.zero;
            bobOffset = Vector3.Lerp(bobOffset, target, 1f - Mathf.Exp(-dt * 16f));
            if (target == Vector3.zero && bobOffset.sqrMagnitude < .000000001f) bobOffset = Vector3.zero;
            ViewRoot.localPosition = Vector3.up * EyeHeight + bobOffset;
        }

        private void StopInput()
        {
            wasPlayable = false;
            hadLockedCursor = false;
            planarVelocity = Vector3.zero;
            targetYaw = currentYaw;
            targetPitch = currentPitch;
        }

        private void PauseForLostControl(GameSession session)
        {
            StopInput();
            if (session != null && session.HasStarted && !session.Paused && !session.Victory)
                session.Pause();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            windowHasFocus = hasFocus;
            suppressInputThroughFrame = Time.frameCount + 1;
            if (!hasFocus)
            {
                FlushControlPreferences(true);
                PauseForLostControl(GameSession.Instance);
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                FlushControlPreferences(true);
                PauseForLostControl(GameSession.Instance);
            }
        }

        private void OnDisable()
        {
            FlushControlPreferences(true);
            ClearDamageFeedback();
            StopInput();
            verticalVelocity = 0f;
        }

        private void LateUpdate()
        {
            RemoveDamageFeedback();
            GameSession session = GameSession.Instance;
            if (downedPose || ViewRoot == null || session == null || !session.IsPlaying || screenShakeIntensity <= 0f)
            { feedbackRemaining = 0f; return; }
            if (feedbackRemaining <= 0f) return;
            float dt = ClampFinite(Time.unscaledDeltaTime, 0f, .05f, 0f);
            feedbackRemaining = Mathf.Max(0f, feedbackRemaining - dt);
            feedbackTime += dt;
            float amount = screenShakeIntensity * feedbackRemaining / .18f;
            float phase = feedbackTime * 47f;
            // Unit-circle offset never exceeds .015m; a single rotational axis
            // never exceeds .3 degrees. Remove it before the next base pose update.
            feedbackOffset = new Vector3(Mathf.Sin(phase), Mathf.Cos(phase), 0f) * (.015f * amount);
            feedbackRotation = Quaternion.Euler(Mathf.Sin(phase) * .3f * amount, 0f, 0f);
            ViewRoot.localPosition += feedbackOffset;
            ViewRoot.localRotation *= feedbackRotation;
            feedbackApplied = true;
        }

        private void RemoveDamageFeedback()
        {
            if (!feedbackApplied) return;
            if (ViewRoot != null)
            {
                ViewRoot.localPosition -= feedbackOffset;
                ViewRoot.localRotation *= Quaternion.Inverse(feedbackRotation);
            }
            feedbackApplied = false;
            feedbackOffset = Vector3.zero;
            feedbackRotation = Quaternion.identity;
        }

        private void ClearDamageFeedback()
        {
            RemoveDamageFeedback();
            feedbackRemaining = feedbackTime = 0f;
        }

        private void SavePreference(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            preferencesDirty = true;
            preferenceSaveAt = Time.realtimeSinceStartup + .25f;
        }

        private void FlushControlPreferences(bool immediate)
        {
            if (!preferencesDirty || (!immediate && Time.realtimeSinceStartup < preferenceSaveAt)) return;
            PlayerPrefs.Save();
            preferencesDirty = false;
        }

        private void OnApplicationQuit() { FlushControlPreferences(true); }
        private static float ClampFinite(float value, float minimum, float maximum, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum);

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
