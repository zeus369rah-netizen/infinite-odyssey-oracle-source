using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    public sealed partial class GameSession
    {
        bool idleClockStarted, idleShutdownStarted, idleDestroyed;
        double idleLastInput, idleSoloWarningAt = -1d, idleResumeUntil;
        Vector2 idleLookDelta;
        int idleDisplayedSeconds = -1, idleDisplayedVisible = -1, idleShutdownGeneration;
        public bool LocalAfk { get; private set; }
        public string IdleShutdownError { get; private set; }
        public bool IdleApplicationEndReady { get; private set; }

        bool HasMeaningfulInput()
        {
            if (!Application.isFocused) return false;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.anyKey.wasPressedThisFrame) return true;
                if (IsPlaying && (keyboard[GameActionBindings.GetKey("Forward")].isPressed ||
                    keyboard[GameActionBindings.GetKey("Back")].isPressed || keyboard[GameActionBindings.GetKey("Left")].isPressed ||
                    keyboard[GameActionBindings.GetKey("Right")].isPressed)) return true;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame ||
                    mouse.scroll.ReadValue().sqrMagnitude > 0f) return true;
                idleLookDelta += mouse.delta.ReadValue();
                if (idleLookDelta.sqrMagnitude >= 4f) { idleLookDelta = Vector2.zero; return true; }
            }
            var pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame ||
                pad.leftStick.ReadValue().sqrMagnitude > .04f || pad.rightStick.ReadValue().sqrMagnitude > .04f)) return true;
            var touch = Touchscreen.current;
            return touch != null && (touch.primaryTouch.press.wasPressedThisFrame ||
                touch.primaryTouch.press.isPressed && touch.primaryTouch.delta.ReadValue().sqrMagnitude >= 4f);
        }

        public void RecordMeaningfulInput()
        {
            if (idleShutdownStarted || idleDestroyed) return;
            idleClockStarted = true; idleLastInput = Time.realtimeSinceStartupAsDouble;
            idleSoloWarningAt = -1d; LocalAfk = false;
            Rooms?.RecordMeaningfulLocalActivity();
        }
        [UnityEngine.Scripting.Preserve]
        public void StayConnectedAfterIdleWarning()
        {
            RecordMeaningfulInput(); Rooms?.RecordMeaningfulLocalActivity(true);
        }
        void NotifyIdleForeground(bool focused)
        { if (focused) idleResumeUntil = Time.realtimeSinceStartupAsDouble + 2d; }

        void SetIdleWarning(bool visible, int seconds)
        {
            int show = visible ? 1 : 0;
            if (idleDisplayedVisible == show && idleDisplayedSeconds == seconds) return;
            idleDisplayedVisible = show; idleDisplayedSeconds = seconds;
#if UNITY_WEBGL && !UNITY_EDITOR
            OracleIdleShowWarning(show, Math.Max(0, seconds));
#endif
        }
        internal void DisplayRoomIdleWarning(bool visible, int seconds) { SetIdleWarning(visible, seconds); }
        void UpdateSessionInactivity()
        {
            if (idleDestroyed || idleShutdownStarted) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!idleClockStarted) { idleClockStarted = true; idleLastInput = now; }
            if (HasMeaningfulInput()) RecordMeaningfulInput();
            LocalAfk = now - idleLastInput >= RoomIdlePolicy.IdleSeconds;
            if (Rooms != null && Rooms.Connected)
            {
                // An active peer protects every runtime. Do not pause/throttle an authoritative host here.
                SetIdleWarning(Rooms.IdleWarningPhase == 1, Rooms.IdleWarningSeconds);
                return;
            }
            if (Rooms != null && Rooms.Busy) { SetIdleWarning(false, 0); return; }
            if (now - idleLastInput < RoomIdlePolicy.IdleSeconds - RoomIdlePolicy.WarningSeconds)
            { SetIdleWarning(false, 0); return; }
            if (idleSoloWarningAt < 0d) idleSoloWarningAt = now;
            double deadline = Math.Max(Math.Max(idleLastInput + RoomIdlePolicy.IdleSeconds,
                idleSoloWarningAt + RoomIdlePolicy.WarningSeconds), idleResumeUntil);
            SetIdleWarning(true, Math.Max(0, (int)Math.Ceiling(deadline - now)));
            if (now >= deadline) BeginIdleApplicationEnd(false, 0);
        }

        internal async void BeginIdleApplicationEnd(bool closeHostedRoom, ulong expectedRoomEpoch)
        {
            if (idleShutdownStarted || idleDestroyed) return;
            idleShutdownStarted = true; int generation = ++idleShutdownGeneration;
            var rooms = Rooms;
            try
            {
                Task<string> cleanup;
                if (closeHostedRoom) cleanup = rooms.CloseIdleRoomAsync(expectedRoomEpoch);
                else if (expectedRoomEpoch != 0) cleanup = rooms.LeaveIdleGuestAsync(expectedRoomEpoch);
                else
                {
                    if (rooms != null && (rooms.Connected || rooms.Busy)) throw new InvalidOperationException("A connection became active before app idle closure.");
                    cleanup = rooms != null ? rooms.LeaveRoomAsync() : Task.FromResult<string>(null);
                }
                int roomGeneration = rooms != null ? rooms.InactivityLifecycleGeneration : 0;
                string failure = await cleanup;
                if (idleDestroyed || generation != idleShutdownGeneration || Instance != this) return;
                if (!ReferenceEquals(Rooms, rooms) || !ReferenceEquals(rooms, null) &&
                    (!rooms.InactivityOwnerAlive || rooms.InactivityLifecycleGeneration != roomGeneration || rooms.Connected || rooms.Busy))
                {
                    idleShutdownStarted = false; idleLastInput = Time.realtimeSinceStartupAsDouble; idleSoloWarningAt = -1d;
                    return;
                }
                if (failure != null) throw new InvalidOperationException(failure);
                Pause();
                // Flush existing accepted preferences only; no reward/progression reset or new commit.
                PlayerPrefs.Save();
                IdleApplicationEndReady = true; SetIdleWarning(false, 0);
#if UNITY_WEBGL && !UNITY_EDITOR
                OracleIdleEndReady(generation);
#endif
            }
            catch (Exception error)
            {
                IdleShutdownError = error.Message;
                Debug.LogWarning("Idle shutdown did not complete: " + error.Message);
                // Keep the existing runtime and allow explicit recovery; do not claim a successful Quit.
                idleShutdownStarted = false; idleLastInput = Time.realtimeSinceStartupAsDouble; idleSoloWarningAt = -1d;
            }
        }
        void CancelIdleShutdown()
        { idleDestroyed = true; ++idleShutdownGeneration; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void OracleIdleShowWarning(int visible, int seconds);
        [DllImport("__Internal")] static extern void OracleIdleEndReady(int generation);
#endif
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Local InputSystem intent and monotonic unscaled seconds; no movement/targeting math ownership.
// Array Formats: Scalar idle/input accumulators; room policy owns8 slots. No new per-frame managed collections, strings or rendering allocations.
// Dependencies/Pointers: Existing partial GameSession awaits the existing RoomConnection cleanup owner, flushes accepted PlayerPrefs then signals the public Web instance Quit bridge. Native/browser acceptance pending.
