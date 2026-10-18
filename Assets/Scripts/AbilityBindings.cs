using System;
using UnityEngine.InputSystem;
namespace OdysseyOracle
{
    /// <summary>Spell-slot compatibility view; all capture belongs to GameActionBindings.</summary>
    public static class AbilityBindings
    {
        private static readonly string[] Actions = { "Ability1", "Ability2", "Ability3", "Ability4" };
        public static bool IsRebinding => GameActionBindings.IsRebinding;
        public static int RebindingSlot => GameActionBindings.RebindingIndex >= 8 ? GameActionBindings.RebindingIndex - 8 : -1;
        public static string LastMessage => GameActionBindings.LastMessage;
        internal static int LastChangeFrame => GameActionBindings.LastChangeFrame;
        public static Key GetKey(int slot) => GameActionBindings.GetKey(Action(slot));
        public static string GetLabel(int slot) => GameActionBindings.GetLabel(Action(slot));
        public static void BeginRebind(int slot) => GameActionBindings.BeginRebind(Action(slot));
        public static void CancelRebind() => GameActionBindings.CancelRebind();
        public static void UpdateCapture() => GameActionBindings.UpdateCapture();
        public static void ResetDefaults() => GameActionBindings.ResetDefaults();
        public static bool IsBindable(Key key) => GameActionBindings.IsBindable(key);
        private static string Action(int slot)
        {
            if (slot < 0 || slot >= Actions.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            return Actions[slot];
        }
    }
}
