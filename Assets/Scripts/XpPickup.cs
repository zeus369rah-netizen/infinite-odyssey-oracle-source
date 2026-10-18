using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>A pooled, visible XP mote; proximity and line of sight are required to collect it.</summary>
    public sealed class XpPickup : MonoBehaviour
    {
        private ElementProgression owner;
        private Transform visual;
        private int amount;
        private float age;
        private float claimRetryAt;
        internal Vector3 ClaimPosition { get; private set; }
        internal bool ReadyToCollect => Active && age >= .45f;
        internal bool ClaimDue(float now) => NetworkDropId < 0 || now >= claimRetryAt;
        internal string RewardToken { get; private set; }
        internal int NetworkDropId { get; private set; } = -1;
        internal bool Active { get; private set; }

        internal void Initialize(ElementProgression progression, Material material)
        {
            owner = progression;
            GameObject diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diamond.name = "Living-light experience crystal";
            visual = diamond.transform;
            visual.SetParent(transform, false);
            visual.localScale = new Vector3(.17f, .23f, .17f);
            visual.localRotation = Quaternion.Euler(35f, 45f, 20f);
            Collider collider = diamond.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            Renderer renderer = diamond.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Deactivate();
        }

        internal void Activate(Vector3 position, int xpAmount, string rewardToken = null, Vector3? originalDrop = null)
        {
            transform.position = position;
            ClaimPosition = originalDrop ?? position - Vector3.up * .55f;
            amount = xpAmount;
            age = 0f;
            claimRetryAt = 0f;
            NetworkDropId = -1;
            RewardToken = rewardToken;
            visual.localPosition = Vector3.zero;
            Active = true;
            gameObject.SetActive(true);
        }

        internal void ActivateNetwork(Vector3 position, int xpAmount, int dropId, Vector3? originalDrop = null)
        {
            Activate(position, xpAmount, null, originalDrop);
            NetworkDropId = dropId;
        }

        internal void Deactivate()
        {
            Active = false;
            amount = 0;
            claimRetryAt = 0f;
            NetworkDropId = -1;
            RewardToken = null;
            gameObject.SetActive(false);
        }

        // ElementProgression owns cadence and closest-valid selection. This mote
        // remains at the original visual/drop position, including in network play.
        internal void Tick(float dt)
        {
            if (!Active || dt <= 0f) return;
            age += dt;
            visual.localPosition = Vector3.up * Mathf.Sin(age * 2.4f) * .065f;
            visual.Rotate(0f, dt * 65f, 0f, Space.World);
        }
        internal bool CollectOrRequest(float now)
        {
            if (!ReadyToCollect || owner == null || !owner.CanCollect || !ClaimDue(now)) return false;
            if (NetworkDropId >= 0)
            {
                claimRetryAt = now + 1.5f;
                return owner.RequestNetworkXp(NetworkDropId); // Only host ack hides/grants.
            }
            if (!owner.TryCollectXp(amount, RewardToken)) return false;
            Deactivate();return true;
        }
    }
}
