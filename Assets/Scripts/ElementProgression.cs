using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    /// <summary>Prototype-specific elemental passives and nearby collectible XP.</summary>
    [DisallowMultipleComponent]
    public sealed class ElementProgression : MonoBehaviour
    {
        public const int MaximumLevel = 50;
        public const string TutorialRewardToken = "tutorial_guardian_v1";
        public const string TutorialXpClaimKey = "OdysseyOracle.Rewards.tutorial_guardian_v1.Xp";
        public const string TutorialHeartstoneClaimKey = "OdysseyOracle.Rewards.tutorial_guardian_v1.Heartstone";
        private static readonly HashSet<string> committingProfileRewards = new HashSet<string>();
        private readonly bool[,] nodes = new bool[4, 3];
        private readonly List<XpPickup> pickups = new List<XpPickup>(16);
        private readonly Dictionary<int, XpPickup> networkPickups = new Dictionary<int, XpPickup>(128);
        private readonly HashSet<int> seenNetworkDrops = new HashSet<int>();
        private const int MaximumHeartcrystals = 100;
        private const int MaximumAttractingHeartstones = 8;
        private readonly List<HeartcrystalPickup> heartcrystals = new List<HeartcrystalPickup>(MaximumHeartcrystals);
        private readonly PickupVisibilityQuery pickupVisibility = new PickupVisibilityQuery();
        private float nextXpScan;
        private int xpScanCursor;
        private const int MaximumXpCandidatesPerScan = 128;
        private readonly RaycastHit[] heartGroundHits = new RaycastHit[32];
        private readonly RaycastHit[] heartTravelHits = new RaycastHit[16];
        private readonly Collider[] heartTravelOverlaps = new Collider[16];
        private System.Func<bool> acceptHeartstone;
        private OraclePlayer player;
        private Material pickupMaterial;
        private Material heartcrystalMaterial;
        private Mesh heartcrystalMesh;
        private OracleTownShop observedShop;
        private int observedPermanentMask, pendingUpgradeBits;
        private bool usedPermanentTraining, heartPoolActive;
        private float nextHeartScan, heartNoticeUntil;
        private string heartNotice = "";
        private string heartTooltip = "";
        private int experience;

        public string[] ElementNames { get; } = new[] { "Fire", "Water", "Earth", "Air" };
        public Color[] ElementColors { get; } = new[]
        {
            new Color(1f, .36f, .12f), new Color(.18f, .68f, 1f),
            new Color(.62f, .78f, .29f), new Color(.66f, .91f, 1f)
        };
        public int[] Levels { get; } = new[] { 1, 1, 1, 1 };
        public int SelectedElement { get; private set; }
        public int Level { get; private set; } = 1;
        public int SkillPoints { get; private set; }
        public int Experience => experience;
        public int LastXpGainAmount { get; private set; }
        public int XpGainSequence { get; private set; }
        public int UpgradeMask { get; private set; }
        public int ExperienceToNextLevel => 2 + (Level - 1) / 2;
        public float Experience01 => Level >= MaximumLevel ? 1f : Mathf.Clamp01((float)experience / ExperienceToNextLevel);
        public float DamageMultiplier { get; private set; } = 1f;
        public float CooldownMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float PickupRangeMetres => GetPickupRangeMetres(EffectivePassiveMask);
        public string PickupRangeLabel => PickupReachRules.RangeLabel(EffectivePassiveMask);
        public string HeartcrystalHint { get; private set; } = "";
        public int ActiveHeartcrystals
        {
            get
            {
                int count = 0;
                for (int i = 0; i < heartcrystals.Count; i++) if (heartcrystals[i] != null && heartcrystals[i].Active) count++;
                return count;
            }
        }
        public int EffectivePassiveMask => UsesPermanentTraining ? observedShop.GetCombinedPassiveMask(UpgradeMask) : UpgradeMask;
        private bool SoloMode => GameSession.Instance != null && !GameSession.Instance.CoopMode;
        private bool UsesPermanentTraining => SoloMode && observedShop != null;

        internal OraclePlayer Player => player;
        internal bool CanCollect => player != null && GameSession.Instance != null && GameSession.Instance.IsPlaying;
        internal bool NetworkActive => GameSession.Instance != null && GameSession.Instance.CoopMode &&
            GameSession.Instance.Defense != null && (GameSession.Instance.Defense.Running ||
                CoopCampaignDirector.IsRefugeSession(GameSession.Instance));

        public void Initialize(OraclePlayer oraclePlayer)
        {
            player = oraclePlayer;
            acceptHeartstone = CommitHeartstone;
            if (pickupMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
                pickupMaterial = new Material(shader) { name = "Collected experience living light" };
                Color color = new Color(.23f, 1f, .66f);
                pickupMaterial.SetColor("_BaseColor", color);
                pickupMaterial.SetColor("_Color", color);
                pickupMaterial.EnableKeyword("_EMISSION");
                pickupMaterial.SetColor("_EmissionColor", color * 2.2f);
            }
            ResetProgression();
        }

        public void ResetProgression()
        {
            Level = 1;
            SkillPoints = 0;
            experience = 0;
            LastXpGainAmount = XpGainSequence = 0;
            SelectedElement = 0;
            pendingUpgradeBits = 0;
            ResetNetworkXp();
            for (int element = 0; element < 4; element++)
            {
                Levels[element] = 1;
                for (int node = 0; node < 3; node++) nodes[element, node] = false;
            }
            for (int i = 0; i < pickups.Count; i++) if (pickups[i] != null) pickups[i].Deactivate();
            ClearHeartcrystals();
            AbilityBindings.CancelRebind();
            RecalculateBonuses();
        }

        private void Update()
        {
            AbilityBindings.UpdateCapture();
            GameSession session = GameSession.Instance;
            if ((session != null ? session.Shop : null) != observedShop) RefreshPermanentTraining();
            else if (usedPermanentTraining != UsesPermanentTraining) RecalculateBonuses();
            UpdateXpPickups();
            UpdateHeartcrystals();
            if (AbilityBindings.IsRebinding || Time.frameCount <= AbilityBindings.LastChangeFrame || !CanCollect) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            for (int element = 0; element < 4; element++)
                if (keyboard[AbilityBindings.GetKey(element)].wasPressedThisFrame) SelectElement(element);
        }

        public void SelectElement(int element)
        {
            if (!ValidElement(element) || AbilityBindings.IsRebinding) return;
            GameSession session = GameSession.Instance;
            if (session != null && session.Combat != null) session.Combat.TrySelectElement(element);
            else SetSelectedElementDirect(element);
        }

        // Combat and accepted server state use this setter after enforcing the
        // element return cooldown. It intentionally performs no callbacks.
        public void SetSelectedElementDirect(int element)
        {
            if (ValidElement(element)) SelectedElement = element;
        }

        public bool IsUnlocked(int element, int node)
        {
            return ValidNode(element, node) && (EffectivePassiveMask & (1 << (element * 3 + node))) != 0;
        }

        public bool TryUpgrade(int element, int node)
        {
            if (!ValidNode(element, node) || IsUnlocked(element, node) || SkillPoints < 1 ||
                (node > 0 && !IsUnlocked(element, node - 1))) return false;
            if (NetworkActive)
            {
                // This also covers the host's own player. Only the director can
                // spend network skill points and return the accepted state.
                pendingUpgradeBits |= 1 << (element * 3 + node);
                GameSession.Instance.Defense.RequestUpgrade(element, node);
                return true;
            }
            if (GameSession.Instance != null && GameSession.Instance.CoopMode) return false;
            SkillPoints--;
            nodes[element, node] = true;
            Levels[element]++;
            RecalculateBonuses();
            PlayPassiveUnlockCue();
            return true;
        }

        public string UpgradeDescription(int element, int node)
        {
            if (!ValidNode(element, node)) return "Unknown upgrade";
            string name;
            string effect;
            if (element == 0)
            {
                name = node == 0 ? "Kindle" : node == 1 ? "Living Flame" : "Solar Focus";
                effect = "All element damage +" + (node == 0 ? "10" : node == 1 ? "12" : "15") + "%.";
            }
            else if (element == 1)
            {
                name = node == 0 ? "Flow" : node == 1 ? "Returning Tide" : "Deep Current";
                effect = "Water, Binding Mud and Water/Air slows last +0.20 s per Water node (50% slow; max 2.0 / 2.8 / 2.0 s).";
            }
            else if (element == 2)
            {
                name = node == 0 ? "Heavy Focus" : node == 1 ? "Steady Channel" : "Stone Tempo";
                effect = node == 0 ? "All element damage +6%." : node == 1 ? "Earth, Binding Mud and Fire/Earth interrupts last +0.10 s; later hits gain 25% during Earth interrupt." :
                    "All element damage +10%; Earth interrupts last another +0.10 s. Max Earth hit 0.55 s, Mud 0.75 s, Fire/Earth effect 0.40 s; effects refresh, never add.";
            }
            else
            {
                name = node == 0 ? "Light Step" : node == 1 ? "Tailwind" : "Skystride";
                effect = "Movement speed +" + (node == 0 ? "6" : node == 1 ? "8" : "10") + "%.";
                if (node > 0) effect += " Pickup reach +2m (" + (node == 1 ? "6m" : "8m") + " total).";
            }
            string status = IsUnlocked(element, node) ? "Learned" : node > 0 && !IsUnlocked(element, node - 1)
                ? "Requires previous node" : "Costs 1 skill point";
            return name + " — " + effect + " " + ElementNames[element] + " level +1 (+8% own damage). Recovery stays 4 s; accepted shots stay at least 1 s apart. " + status + ".";
        }

        public float GetElementDamageMultiplier(int element)
        {
            return GetDamageMultiplier(EffectivePassiveMask, element);
        }

        public float GetElementCooldownMultiplier(int element)
        {
            return GetCooldownMultiplier(EffectivePassiveMask, element);
        }

        public void SpawnXp(Vector3 position, int amount = 1, bool tutorialReward = false)
        {
            if (player == null || amount <= 0 || !Finite(position) ||
                (GameSession.Instance != null && GameSession.Instance.CoopMode)) return;
            string rewardToken = tutorialReward ? TutorialRewardToken : null;
            AcquirePickup().Activate(position + Vector3.up * .55f, amount, rewardToken, position);
            SpawnHeartcrystal(position, rewardToken);
        }

        public void SpawnNetworkXp(int dropId, Vector3 position, int amount)
        {
            if (player == null || dropId < 0 || amount <= 0 || !Finite(position) || seenNetworkDrops.Contains(dropId)) return;
            XpPickup pickup = AcquirePickup();
            seenNetworkDrops.Add(dropId);
            networkPickups.Add(dropId, pickup);
            pickup.ActivateNetwork(position + Vector3.up * .55f, amount, dropId, position);
        }

        public void ConfirmNetworkXp(int dropId)
        {
            if (dropId < 0) return;
            // A confirmation may arrive before the corresponding drop snapshot.
            // Retain its ID so later snapshots cannot resurrect a claimed mote.
            seenNetworkDrops.Add(dropId);
            if (!networkPickups.TryGetValue(dropId, out XpPickup pickup)) return;
            if (pickup != null) pickup.Deactivate();
            networkPickups.Remove(dropId);
        }

        public void ResetNetworkXp()
        {
            foreach (XpPickup pickup in networkPickups.Values) if (pickup != null) pickup.Deactivate();
            networkPickups.Clear();
            seenNetworkDrops.Clear();
        }

        public void ApplyAuthoritativeState(int level, int xp, int points, int mask12)
        {
            if (level < 1 || level > MaximumLevel || xp < 0 || points < 0 || mask12 < 0 || mask12 > 0xFFF) return;
            int spent = 0;
            for (int element = 0; element < 4; element++)
            {
                int branch = (mask12 >> (element * 3)) & 7;
                // Only prefix masks are valid for a sequential three-node branch.
                if (branch != 0 && branch != 1 && branch != 3 && branch != 7) return;
                spent += BranchCount(mask12, element);
            }
            if ((long)points + spent > level - 1) return;
            int previousMask = UpgradeMask;
            long previousExperience = TotalEarnedExperience(Level, experience);
            Level = level;
            experience = level >= MaximumLevel ? 0 : Mathf.Clamp(xp, 0, ExperienceToNextLevel - 1);
            SkillPoints = points;
            for (int element = 0; element < 4; element++)
            {
                Levels[element] = 1 + BranchCount(mask12, element);
                for (int node = 0; node < 3; node++) nodes[element, node] = HasNode(mask12, element, node);
            }
            RecalculateBonuses();
            long acceptedGain = TotalEarnedExperience(Level, experience) - previousExperience;
            if (acceptedGain > 0) MarkXpGain((int)System.Math.Min(int.MaxValue, acceptedGain));
            if ((mask12 & ~previousMask & pendingUpgradeBits) != 0) PlayPassiveUnlockCue();
            pendingUpgradeBits &= ~mask12;
        }

        internal bool RequestNetworkXp(int dropId)
        {
            if (!CanCollect || !NetworkActive || !networkPickups.ContainsKey(dropId)) return false;
            GameSession.Instance.Defense.RequestXpClaim(dropId);
            return true;
        }

        // One owner advances visuals; at10Hz choose the closest READY, retry-due,
        // unobstructed mote. One request/scan bounds reliable traffic to10/s.
        // Current live network registry is128; larger trusted offline pools use
        // fair128-entry windows without dropping or silently granting rewards.
        private void UpdateXpPickups()
        {
            if (!CanCollect || pickups.Count == 0) return;
            float dt = Mathf.Clamp(Time.deltaTime, 0f, .1f);
            if (float.IsNaN(dt) || float.IsInfinity(dt)) dt = 0f;
            for (int i = 0; i < pickups.Count; i++) if (pickups[i] != null && pickups[i].Active) pickups[i].Tick(dt);
            float now = Time.unscaledTime;
            if (now < nextXpScan) return;
            nextXpScan = now + .1f;
            Vector3 feet = player.transform.position, origin = feet + Vector3.up * .95f;
            float best = PickupRangeMetres * PickupRangeMetres;
            XpPickup nearest = null;
            int count = Mathf.Min(pickups.Count, MaximumXpCandidatesPerScan), start = xpScanCursor % pickups.Count;
            for (int offset = 0; offset < count; offset++)
            {
                XpPickup candidate = pickups[(start + offset) % pickups.Count];
                if (candidate == null || !candidate.ReadyToCollect || !candidate.ClaimDue(now)) continue;
                float squared = (feet - candidate.ClaimPosition).sqrMagnitude;
                if (float.IsNaN(squared) || float.IsInfinity(squared) || squared > best || nearest != null && squared == best ||
                    !pickupVisibility.IsClear(origin, candidate.ClaimPosition + Vector3.up * .4f, player.transform)) continue;
                best = squared; nearest = candidate;
            }
            xpScanCursor = (start + count) % pickups.Count;
            if (nearest != null) nearest.CollectOrRequest(now);
        }

        private XpPickup AcquirePickup()
        {
            XpPickup pickup = null;
            for (int i = 0; i < pickups.Count; i++)
                if (pickups[i] != null && !pickups[i].Active) { pickup = pickups[i]; break; }
            if (pickup == null)
            {
                pickup = new GameObject("Experience mote").AddComponent<XpPickup>();
                pickup.Initialize(this, pickupMaterial);
                pickups.Add(pickup);
            }
            return pickup;
        }

        internal bool TryCollectXp(int amount, string rewardToken = null)
        {
            if (!CanCollect || amount <= 0 || (GameSession.Instance != null && GameSession.Instance.CoopMode)) return false;
            if (!string.IsNullOrEmpty(rewardToken))
                return TryCommitTutorialReward(rewardToken, false, () => AcceptXp(amount), out _);
            return AcceptXp(amount);
        }

        private bool AcceptXp(int amount)
        {
            long previousExperience = TotalEarnedExperience(Level, experience);
            long total = (long)experience + amount;
            while (Level < MaximumLevel && total >= ExperienceToNextLevel)
            {
                total -= ExperienceToNextLevel;
                Level++;
                SkillPoints++;
            }
            experience = Level >= MaximumLevel ? 0 : (int)total;
            long acceptedGain = TotalEarnedExperience(Level, experience) - previousExperience;
            if (acceptedGain > 0) MarkXpGain((int)System.Math.Min(int.MaxValue, acceptedGain));
            return true;
        }

        internal static bool IsTutorialRewardClaimed(string token, bool heartstone) =>
            token == TutorialRewardToken && PlayerPrefs.GetInt(heartstone ? TutorialHeartstoneClaimKey : TutorialXpClaimKey, 0) == 1;

        internal static bool TryCommitTutorialReward(string token, bool heartstone, System.Func<bool> acceptReward, out bool awarded)
        {
            awarded = false;
            if (string.IsNullOrEmpty(token))
            {
                awarded = acceptReward != null && acceptReward();
                return awarded;
            }
            if (token != TutorialRewardToken) return false;
            return CommitProfileRewardKey(heartstone ? TutorialHeartstoneClaimKey : TutorialXpClaimKey, acceptReward, out awarded);
        }

        private static bool CommitProfileRewardKey(string key, System.Func<bool> acceptReward, out bool awarded)
        {
            awarded = false;
            // Read at collection, including for another active copy in this frame.
            // Run reset and spawning never reserve or consume a profile claim.
            if (PlayerPrefs.GetInt(key, 0) == 1) return true;
            if (acceptReward == null || !committingProfileRewards.Add(key)) return false;
            try
            {
                if (!acceptReward()) return false;
                awarded = true;
                PlayerPrefs.SetInt(key, 1);
                // The accepted wallet save (or XP change) precedes this separate ledger.
                // Keep the in-memory claim if the platform rejects a persistence flush,
                // preventing retries from granting a second reward in this session.
                try { PlayerPrefs.Save(); }
                catch (System.Exception exception) { Debug.LogWarning("Tutorial reward claim persistence failed: " + exception.Message); }
                return true;
            }
            finally { committingProfileRewards.Remove(key); }
        }

        private void MarkXpGain(int amount)
        {
            if (amount <= 0) return;
            LastXpGainAmount = amount;
            XpGainSequence = XpGainSequence == int.MaxValue ? 1 : XpGainSequence + 1;
        }

        private static long TotalEarnedExperience(int level, int currentExperience)
        {
            long result = currentExperience;
            for (int previous = 1; previous < level; previous++) result += 2 + (previous - 1) / 2;
            return result;
        }

        private void RecalculateBonuses()
        {
            int mask = 0;
            for (int element = 0; element < 4; element++)
            for (int node = 0; node < 3; node++)
                if (nodes[element, node]) mask |= 1 << (element * 3 + node);
            UpgradeMask = mask;
            usedPermanentTraining = UsesPermanentTraining;
            mask = EffectivePassiveMask;
            DamageMultiplier = GlobalDamage(mask);
            CooldownMultiplier = 1f;
            MoveSpeedMultiplier = GetMoveSpeedMultiplier(mask);
        }

        /// <summary>Call once after Shop.Initialize; subsequent accepted shop changes refresh automatically.</summary>
        public void RefreshPermanentTraining()
        {
            OracleTownShop current = GameSession.Instance != null ? GameSession.Instance.Shop : null;
            if (current != observedShop)
            {
                if (observedShop != null) observedShop.ProgressionChanged -= OnPermanentTrainingChanged;
                observedShop = current;
                if (observedShop != null) observedShop.ProgressionChanged += OnPermanentTrainingChanged;
            }
            // Loading a saved profile does not pretend that a purchase just occurred.
            observedPermanentMask = observedShop != null ? observedShop.PermanentPassiveMask : 0;
            RecalculateBonuses();
        }

        private void OnPermanentTrainingChanged()
        {
            int mask = observedShop != null ? observedShop.PermanentPassiveMask : 0;
            if (mask == observedPermanentMask) return;
            bool added = (mask & ~observedPermanentMask) != 0;
            observedPermanentMask = mask;
            RecalculateBonuses();
            if (added && SoloMode) PlayPassiveUnlockCue();
        }

        private static void PlayPassiveUnlockCue()
        { if (GameSession.Instance != null) GameSession.Instance.Audio?.PlayCue("passive_unlock"); }

        private void SpawnHeartcrystal(Vector3 position, string rewardToken = null)
        {
            GameSession session = GameSession.Instance;
            if (!SoloMode || NetworkActive || session == null || !session.HasStarted) return;
            HeartcrystalPickup pickup = null;
            int emptyIndex = -1;
            for (int i = 0; i < heartcrystals.Count; i++)
            {
                if (heartcrystals[i] == null) { emptyIndex = i; break; }
                if (!heartcrystals[i].Active) { pickup = heartcrystals[i]; break; }
            }
            if (pickup == null)
            {
                if (emptyIndex < 0 && heartcrystals.Count >= MaximumHeartcrystals) return;
                EnsureHeartcrystalResources();
                pickup = new GameObject("Solo Heartstone monster drop").AddComponent<HeartcrystalPickup>();
                pickup.transform.SetParent(transform, false);
                pickup.Initialize(heartcrystalMesh, heartcrystalMaterial);
                if (emptyIndex >= 0) heartcrystals[emptyIndex] = pickup;
                else heartcrystals.Add(pickup);
            }
            pickup.Activate(position + Vector3.up * .55f, HeartstoneRestingPosition(position), rewardToken);
            heartPoolActive = true;
        }

        private Vector3 HeartstoneRestingPosition(Vector3 position)
        {
            Vector3 resting = position + Vector3.up * .26f;
            int count = Physics.RaycastNonAlloc(position + Vector3.up * .15f, Vector3.down, heartGroundHits,
                20f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            if (count < heartGroundHits.Length)
                for (int i = 0; i < count; i++)
                {
                    Collider hit = heartGroundHits[i].collider;
                    if (hit == null || heartGroundHits[i].normal.y <= .8f || heartGroundHits[i].distance >= nearest ||
                        hit.GetComponentInParent<OracleEnemy>() != null || hit.GetComponentInParent<OraclePlayer>() != null ||
                        hit.GetComponentInParent<RemoteOracleAvatar>() != null || hit.GetComponentInParent<OracleProjectile>() != null) continue;
                    nearest = heartGroundHits[i].distance;
                    resting = heartGroundHits[i].point + Vector3.up * .26f;
                }
            return resting;
        }

        private void EnsureHeartcrystalResources()
        {
            if (heartcrystalMesh == null) heartcrystalMesh = HeartcrystalPickup.CreateSharedMesh();
            if (heartcrystalMaterial != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            heartcrystalMaterial = new Material(shader) { name = "Pink Heartstone shared facets", enableInstancing = true };
            Color color = new Color(1f, .12f, .34f);
            heartcrystalMaterial.SetColor("_BaseColor", color);
            heartcrystalMaterial.SetColor("_Color", color);
            heartcrystalMaterial.EnableKeyword("_EMISSION");
            heartcrystalMaterial.SetColor("_EmissionColor", color * .65f);
        }

        private void UpdateHeartcrystals()
        {
            HeartcrystalHint = "";
            if (!SoloMode) { if (heartPoolActive) ClearHeartcrystals(); return; }
            if (!CanCollect)
            {
                for (int i = 0; i < heartcrystals.Count; i++)
                    if (heartcrystals[i] != null && heartcrystals[i].Active) heartcrystals[i].Tick(0f, false);
                return;
            }
            if (!heartPoolActive)
            { HeartcrystalHint = Time.unscaledTime < heartNoticeUntil ? heartNotice : ""; return; }
            float dt = Mathf.Clamp(Time.deltaTime, 0f, .1f);
            if (float.IsNaN(dt) || float.IsInfinity(dt)) dt = 0f;
            Vector3 collector = player.transform.position + Vector3.up * .85f;
            int attracting = 0;
            for (int i = 0; i < heartcrystals.Count; i++)
            {
                HeartcrystalPickup pickup = heartcrystals[i];
                if (pickup == null || !pickup.Active) continue;
                pickup.Tick(dt, true);
                if (!pickup.IsMagnetized) continue;
                Vector3 from = pickup.WorldPosition;
                if ((from - collector).sqrMagnitude > PickupRangeMetres * PickupRangeMetres ||
                    !HeartcrystalVisible(from, collector))
                { pickup.InterruptAttraction(HeartstoneRestingPosition(from)); continue; }
                Vector3 next = pickup.ProposeMagnetStep(collector, dt);
                if (!HeartcrystalPickup.IsTravelClear(from, next, heartTravelHits, heartTravelOverlaps))
                { pickup.InterruptAttraction(HeartstoneRestingPosition(from)); continue; }
                pickup.ApplyMagnetStep(next, collector);
                attracting++;
            }
            if (Time.unscaledTime < nextHeartScan)
            { HeartcrystalHint = Time.unscaledTime < heartNoticeUntil ? heartNotice : heartTooltip; return; }
            nextHeartScan = Time.unscaledTime + .08f;
            Transform eye = player.ViewCamera != null ? player.ViewCamera.transform : player.transform;
            // Admit only two new flights per scan and eight concurrent flights.
            // All one hundred pooled drops remain visible and available.
            for (int admission = 0; admission < 2 && attracting < MaximumAttractingHeartstones; admission++)
            {
                HeartcrystalPickup candidate = null;
                float nearestIdle = PickupRangeMetres * PickupRangeMetres;
                for (int i = 0; i < heartcrystals.Count; i++)
                {
                    HeartcrystalPickup pickup = heartcrystals[i];
                    if (pickup == null || !pickup.Active || !pickup.Settled || pickup.IsMagnetized) continue;
                    float squared = (pickup.WorldPosition - collector).sqrMagnitude;
                    if (squared > nearestIdle || !HeartcrystalVisible(pickup.WorldPosition, collector)) continue;
                    nearestIdle = squared;
                    candidate = pickup;
                }
                if (candidate == null || !candidate.BeginAttraction()) break;
                attracting++;
            }
            float nearestSquared = HeartcrystalPickup.ArrivalRange * HeartcrystalPickup.ArrivalRange, lookSquared = PickupRangeMetres * PickupRangeMetres;
            HeartcrystalPickup nearest = null;
            bool looking = false;
            for (int i = 0; i < heartcrystals.Count; i++)
            {
                HeartcrystalPickup pickup = heartcrystals[i];
                if (pickup == null || !pickup.Active || !pickup.Settled) continue;
                Vector3 position = pickup.WorldPosition;
                float squared = (position - collector).sqrMagnitude;
                if (pickup.Arrived && squared <= nearestSquared && HeartcrystalVisible(position, collector))
                { nearestSquared = squared; nearest = pickup; }
                Vector3 eyeDelta = position - eye.position;
                float along = Vector3.Dot(eyeDelta, eye.forward);
                if (along > 0f && eyeDelta.sqrMagnitude <= lookSquared &&
                    (eyeDelta - eye.forward * along).sqrMagnitude <= .24f * .24f && HeartcrystalVisible(eye.position, position))
                { lookSquared = eyeDelta.sqrMagnitude; looking = true; }
            }
            if (nearest != null && observedShop != null)
            {
                // A rejected or unsaved award leaves the physical drop available.
                Vector3 collectedAt = nearest.WorldPosition;
                if (nearest.TryConsumeAt(collector, CanCollect, acceptHeartstone))
                {
                    if (ActiveHeartcrystals == 0) heartPoolActive = false;
                    if (nearest.LastConsumptionAwarded)
                    {
                        GameSession.Instance.Audio?.PlayCue("pickup", collectedAt);
                        heartNotice = "Heartstone +1 · Spend at the town blacksmith";
                        heartNoticeUntil = Time.unscaledTime + 2.2f;
                    }
                    else { heartNotice = ""; heartNoticeUntil = 0f; }
                }
                else
                {
                    nextHeartScan = Time.unscaledTime + .75f;
                    heartNotice = "Heartstone · Collection pending";
                    heartNoticeUntil = nextHeartScan;
                }
            }
            heartTooltip = looking ? PickupReachRules.HeartstoneHint(EffectivePassiveMask) : "";
            HeartcrystalHint = Time.unscaledTime < heartNoticeUntil ? heartNotice : heartTooltip;
        }

        private bool HeartcrystalVisible(Vector3 from, Vector3 to)
        {
            return pickupVisibility.IsClear(from, to, player != null ? player.transform : null);
        }

        private bool CommitHeartstone() => SoloMode && !NetworkActive && CanCollect && observedShop != null && observedShop.AddHeartcrystals(1);

        private void ClearHeartcrystals()
        {
            for (int i = 0; i < heartcrystals.Count; i++) if (heartcrystals[i] != null) heartcrystals[i].Deactivate();
            HeartcrystalHint = heartNotice = heartTooltip = "";
            heartPoolActive = false;
            nextHeartScan = heartNoticeUntil = 0f;
        }

#if UNITY_EDITOR
        public static string RunTutorialRewardChecks()
        {
            string prefix = "OdysseyOracle.Tests.TutorialRewards." + System.Guid.NewGuid().ToString("N");
            string xpKey = prefix + ".Xp", heartKey = prefix + ".Heartstone";
            int checks = 0, xpAwards = 0, walletAwards = 0;
            System.Action<bool, string> check = (condition, name) =>
            { checks++; if (!condition) throw new System.InvalidOperationException(name); };
            try
            {
                check(!CommitProfileRewardKey(heartKey, () => false, out bool awarded) && !awarded && !PlayerPrefs.HasKey(heartKey), "Rejected wallet save leaves claim unconsumed");
                check(CommitProfileRewardKey(xpKey, () => { xpAwards++; return true; }, out awarded) && awarded && xpAwards == 1, "First accepted XP writes profile claim");
                check(!PlayerPrefs.HasKey(heartKey), "XP claim does not consume Heartstone claim");
                check(CommitProfileRewardKey(xpKey, () => { xpAwards++; return true; }, out awarded) && !awarded && xpAwards == 1, "Second active XP copy dismisses without award");
                PlayerPrefs.Save();
                // No static claim cache: a fresh read uses the saved profile key.
                check(CommitProfileRewardKey(xpKey, () => { xpAwards++; return true; }, out awarded) && !awarded && xpAwards == 1, "Fresh ledger read after save cannot replay XP");
                check(CommitProfileRewardKey(heartKey, () => { walletAwards++; return true; }, out awarded) && awarded && walletAwards == 1, "First accepted wallet save writes independent claim");
                for (int copy = 0; copy < 3; copy++)
                    check(CommitProfileRewardKey(heartKey, () => { walletAwards++; return true; }, out awarded) && !awarded && walletAwards == 1, "Duplicate Heartstone resolves successfully without grant or retry");
                check(PlayerPrefs.GetInt(xpKey) == 1 && PlayerPrefs.GetInt(heartKey) == 1, "Both profile claims persist independently");
                PlayerPrefs.DeleteKey(heartKey);
                bool nestedAccepted = true;
                check(CommitProfileRewardKey(heartKey, () => {
                    nestedAccepted = CommitProfileRewardKey(heartKey, () => { walletAwards++; return true; }, out _);
                    walletAwards++; return true;
                }, out awarded) && awarded && !nestedAccepted && walletAwards == 2, "Save-event reentry cannot award a second active copy");
                return "PASS: " + checks + " tutorial profile reward ledger checks.";
            }
            finally { PlayerPrefs.DeleteKey(xpKey); PlayerPrefs.DeleteKey(heartKey); PlayerPrefs.Save(); }
        }
#endif

        public static float GetDamageMultiplier(int mask12, int element) =>
            GlobalDamage(mask12) * (1f + .08f * BranchCount(mask12, element));

        // Compatibility accessor: accepted ability recovery is fixed, including trained branches.
        public static float GetCooldownMultiplier(int mask12, int element) => 1f;

        // Shared defense passes only its accepted run mask; solo uses the existing
        // effective permanent/run OR through the public instance range getter.
        public static float GetPickupRangeMetres(int mask12) => PickupReachRules.RangeForMask(mask12);

        public static float GetMoveSpeedMultiplier(int mask12) => 1f + (HasNode(mask12, 3, 0) ? .06f : 0f) +
            (HasNode(mask12, 3, 1) ? .08f : 0f) + (HasNode(mask12, 3, 2) ? .10f : 0f);

        private static float GlobalDamage(int mask) => 1f + (HasNode(mask, 0, 0) ? .10f : 0f) +
            (HasNode(mask, 0, 1) ? .12f : 0f) + (HasNode(mask, 0, 2) ? .15f : 0f) +
            (HasNode(mask, 2, 0) ? .06f : 0f) + (HasNode(mask, 2, 2) ? .10f : 0f);

        private static bool HasNode(int mask, int element, int node) => (mask & (1 << (element * 3 + node))) != 0;

        private static int BranchCount(int mask, int element)
        {
            if (!ValidElement(element)) return 0;
            int branch = (mask >> (element * 3)) & 7;
            return (branch & 1) + ((branch >> 1) & 1) + ((branch >> 2) & 1);
        }

        private static bool ValidElement(int element) => element >= 0 && element < 4;
        private static bool ValidNode(int element, int node) => ValidElement(element) && node >= 0 && node < 3;
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void OnDestroy()
        {
            if (observedShop != null) observedShop.ProgressionChanged -= OnPermanentTrainingChanged;
            for (int i = 0; i < heartcrystals.Count; i++) if (heartcrystals[i] != null) Destroy(heartcrystals[i].gameObject);
            if (heartcrystalMesh != null) Destroy(heartcrystalMesh);
            if (heartcrystalMaterial != null) Destroy(heartcrystalMaterial);
            for (int i = 0; i < pickups.Count; i++) if (pickups[i] != null) Destroy(pickups[i].gameObject);
            if (pickupMaterial != null) Destroy(pickupMaterial);
        }
    }
}
