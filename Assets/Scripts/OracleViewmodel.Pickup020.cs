using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleViewmodel
    {
        float pickupAge020 = 1f;
        bool pickupCollected020;

        static bool CanShowStaff020(GameSession value)
        {
            if (value == null || !value.HasStarted || value.Victory || value.IsDowned || value.IsDead) return false;
            bool collected = value.World != null && value.World.IsTutorialRefuge && value.World.TrainingStaffCollected015;
            return collected || value.CombatUnlocked || value.Tutorial != null && value.Tutorial.WandPreviewVisible;
        }

        void ObserveStaffPickup020(GameSession value)
        {
            bool collected = value != null && value.HasStarted && value.World != null &&
                value.World.IsTutorialRefuge && value.World.TrainingStaffCollected015;
            if (collected && !pickupCollected020) pickupAge020 = 0f;
            else if (!collected) pickupAge020 = 1f;
            pickupCollected020 = collected;
        }

        void ApplyStaffPickup020(float delta)
        {
            if (pickupAge020 >= .65f) return;
            pickupAge020 = Mathf.Min(.65f, pickupAge020 + Mathf.Clamp(delta, 0f, .05f));
            float t = Mathf.Clamp01(pickupAge020 / .65f);
            float ease = t * t * t * (t * (t * 6f - 15f) + 10f);
            float remaining = 1f - ease;
            wandRoot.localPosition += new Vector3(.04f, -.24f, .04f) * remaining;
            wandRoot.localRotation *= Quaternion.Euler(10f * remaining, 0f, -12f * remaining);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Camera-local cosmetic pickup pose; existing world pickup identity is read-only.
// Array Formats: Two scalar animation fields; no new clip, Object, collider, command or frame allocation.
// Dependencies/Pointers: World.TakeTrainingStaff owns collection; pause freezes settling; existing clearance runs afterward.
