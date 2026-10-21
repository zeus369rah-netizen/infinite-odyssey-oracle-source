using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    /// <summary>One saved keyboard map and one capture owner for every game action.</summary>
    public static class GameActionBindings
    {
        public static readonly string[] ActionNames = { "Forward", "Back", "Left", "Right", "Sprint", "Jump",
            "Interact", "SkillTree", "Ability1", "Ability2", "Ability3", "Ability4", "MusicSkip", "MusicPause" };
        private static readonly string[] Names = { "Move forward", "Move backward", "Move left", "Move right",
            "Sprint", "Jump", "Interact / read / build", "Skill tree", "Fire", "Water", "Earth", "Air", "Skip song", "Pause / resume music" };
        private static readonly Key[] Defaults = { Key.W, Key.S, Key.A, Key.D, Key.LeftShift, Key.Space,
            Key.E, Key.P, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.N, Key.M };
        private static readonly Key[] Keys = new Key[ActionNames.Length];
        private const string Prefix = "OdysseyOracle.ActionKey.";
        private static int captureAfterFrame, lastCaptureFrame = -1;
        public static int RebindingIndex { get; private set; } = -1;
        public static bool IsRebinding => RebindingIndex >= 0;
        public static string CurrentAction => IsRebinding ? ActionNames[RebindingIndex] : "";
        public static string LastMessage { get; private set; } = "";
        internal static int LastChangeFrame { get; private set; } = -100;

        static GameActionBindings()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                int legacy = i >= 8 && i < 12 ? PlayerPrefs.GetInt("OdysseyOracle.ElementKey." + (i - 8), (int)Defaults[i]) : (int)Defaults[i];
                Key candidate = (Key)PlayerPrefs.GetInt(Prefix + ActionNames[i], legacy);
                if (!IsBindable(candidate) || UsedEarlier(candidate, i))
                {
                    candidate = Defaults[i];
                    if (UsedEarlier(candidate, i))
                        for (int k = (int)Key.A; k <= (int)Key.Digit0; k++)
                            if (IsBindable((Key)k) && !UsedEarlier((Key)k, i)) { candidate = (Key)k; break; }
                }
                Keys[i] = candidate;
            }
        }
        public static Key GetKey(string action) => Keys[Index(action)];
        public static string GetLabel(string action) => KeyLabel(GetKey(action));
        public static string GetDisplayName(string action) => Names[Index(action)];
        public static void BeginRebind(string action)
        {
            RebindingIndex = Index(action);
            captureAfterFrame = Time.frameCount + 1;
            LastMessage = "Press one key for " + Names[RebindingIndex] + ". Escape cancels.";
        }
        public static void CancelRebind()
        { if (IsRebinding) LastMessage = "Rebinding canceled."; RebindingIndex = -1; }
        public static void UpdateCapture()
        {
            if (!IsRebinding || Time.frameCount <= captureAfterFrame || lastCaptureFrame == Time.frameCount) return;
            lastCaptureFrame = Time.frameCount;
            if (!Application.isFocused) { CancelRebind(); return; }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) { LastMessage = "A keyboard is needed to change controls."; return; }
            if (keyboard.escapeKey.wasPressedThisFrame) { CancelRebind(); return; }
            if (!keyboard.anyKey.wasPressedThisFrame) return;
            var allKeys = keyboard.allKeys;
            for (int i = 0; i < allKeys.Count; i++)
            {
                if (!allKeys[i].wasPressedThisFrame) continue;
                Key key = allKeys[i].keyCode;
                if (!IsBindable(key)) { LastMessage = KeyLabel(key) + " is reserved for browser or pause controls."; return; }
                bool modifier = key == Key.LeftShift || key == Key.RightShift || key == Key.LeftCtrl ||
                    key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt;
                if (!modifier && (keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed || keyboard.shiftKey.isPressed ||
                    keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed))
                { LastMessage = "Use a single key, without a key combination."; return; }
                for (int other = 0; other < Keys.Length; other++)
                {
                    if (other == RebindingIndex || Keys[other] != key) continue;
                    LastMessage = KeyLabel(key) + " already controls " + Names[other] + ". Choose another key.";
                    return;
                }
                int slot = RebindingIndex;
                Keys[slot] = key;
                PlayerPrefs.SetInt(Prefix + ActionNames[slot], (int)key);
                PlayerPrefs.Save();
                LastMessage = Names[slot] + " now uses " + KeyLabel(key) + ".";
                LastChangeFrame = Time.frameCount;
                RebindingIndex = -1;
                return;
            }
        }
        public static void ResetDefaults()
        {
            CancelRebind();
            for (int i = 0; i < Keys.Length; i++)
            { Keys[i] = Defaults[i]; PlayerPrefs.SetInt(Prefix + ActionNames[i], (int)Keys[i]); }
            PlayerPrefs.Save();
            LastChangeFrame = Time.frameCount;
            LastMessage = "All controls restored. Escape and right mouse stay fixed.";
        }
        public static bool IsBindable(Key key)
        {
            int code = (int)key;
            return code > (int)Key.None && code <= (int)Key.OEM5 && key != Key.Escape && key != Key.Tab &&
                key != Key.Enter && key != Key.NumpadEnter && key != Key.LeftMeta && key != Key.RightMeta &&
                key != Key.PrintScreen && key != Key.Pause && !(code >= (int)Key.F1 && code <= (int)Key.F12);
        }
        private static int Index(string action)
        {
            int index = Array.IndexOf(ActionNames, action);
            if (index < 0) throw new ArgumentException("Unknown game action.", nameof(action));
            return index;
        }
        private static bool UsedEarlier(Key key, int count)
        { for (int i = 0; i < count; i++) if (Keys[i] == key) return true; return false; }
        private static string KeyLabel(Key key)
        {
            if (Keyboard.current != null && key != Key.None)
            {
                string name = Keyboard.current[key].displayName;
                if (!string.IsNullOrEmpty(name)) return name;
            }
            if (key == Key.Digit0) return "0";
            if ((int)key >= (int)Key.Digit1 && (int)key <= (int)Key.Digit9) return (1 + (int)key - (int)Key.Digit1).ToString();
            return key.ToString();
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Saved local keyboard commands, no gameplay or audio-space mutation.
// Array Formats: Fourteen cold key/name/default slots; original twelve indices preserved, legacy element migration limited to8..11.
// Dependencies/Pointers: One existing capture owner/conflict validator, persisted MusicSkip and MusicPause; transport dispatch observes rebind/text/focus fences.
