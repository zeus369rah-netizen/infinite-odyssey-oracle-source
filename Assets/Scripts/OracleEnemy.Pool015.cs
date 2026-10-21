using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OracleEnemy
    {
        MonsterPooledView015 pooledView015;
        readonly PresentationFootprint[] pooledFootprints015 = new PresentationFootprint[8];
        readonly Transform[] pooledFins015 = new Transform[2];
        readonly Renderer[] pooledFinRenderers015 = new Renderer[2];
        Renderer pooledHealthRenderer015;
        MonsterVoxelCosmetics.Style pooledStyle015;
        internal bool PoolLeased015 { get; private set; }
        public uint PoolGeneration015 { get; private set; }
        internal bool PreparedPool015 => pooledView015 != null;
        partial void ResetRewardAssociation015();
        partial void ResetAdditionalPooledState015();

        internal void PreparePooled015(OracleCombat combat, MonsterPoolTemplates015 templates, Material stone,
            Material joints, Material turquoise, Material violet, Material attack, Material barBackground)
        {
            if (pooledView015 != null || body != null) throw new System.InvalidOperationException("One cold preparation per actor.");
            owner = combat; session = GameSession.Instance; cachedPlayer = combat.Player;
            cachedCamera = cachedPlayer != null ? cachedPlayer.ViewCamera : null; flyingAccent = turquoise;
            flashBlock = new MaterialPropertyBlock(); stoneRenderers.Capacity = 17;
            body = MonsterPoolTemplates015.Pivot(transform, "Block-built pooled original body", Vector3.zero);
            body.localScale = Vector3.one * PresentationScale;
            leftArm = MonsterPoolTemplates015.Pivot(body, "Left arm pivot", new Vector3(-.53f, 1.36f, 0f));
            rightArm = MonsterPoolTemplates015.Pivot(body, "Right arm pivot", new Vector3(.53f, 1.36f, 0f));
            pooledView015 = new MonsterPooledView015(body, leftArm, rightArm, templates, stoneRenderers);
            warning = OracleCombat.CreateBlock(transform, "Attack telegraph", new Vector3(0f, .025f, 0f), new Vector3(1.2f, .025f, 1.2f), attack);
            warning.gameObject.SetActive(false);
            healthBar = MonsterPoolTemplates015.Pivot(transform, "Guardian health", Vector3.up * (2.24f * PresentationScale));
            OracleCombat.CreateBlock(healthBar, "Health background", Vector3.zero, new Vector3(.82f, .075f, .035f), barBackground);
            healthFill = OracleCombat.CreateBlock(healthBar, "Health fill", new Vector3(0f, 0f, -.025f), new Vector3(.76f, .045f, .026f), turquoise);
            pooledHealthRenderer015 = healthFill.GetComponent<Renderer>();
            solid = gameObject.AddComponent<BoxCollider>(); solid.isTrigger = true; solid.enabled = false;
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                pooledFins015[i] = OracleCombat.CreateBlock(body, "Cached flying aether fin", new Vector3(side * .81f, 1.2f, -.05f), new Vector3(.72f, .075f, .42f), turquoise);
                pooledFins015[i].localRotation = Quaternion.Euler(0f, 0f, side * 18f);
                pooledFinRenderers015[i] = pooledFins015[i].GetComponent<Renderer>();
            }
            CreateAggroAlert(attack);
            // Cold measurement only, once for each exact style/flying envelope; no Measure on admission.
            gameObject.SetActive(true);
            for (int style = 0; style < 4; style++)
            {
                pooledView015.ApplyStyle((MonsterVoxelCosmetics.Style)style);
                leftLeg = pooledView015.LeftLeg; rightLeg = pooledView015.RightLeg;
                SetCachedFins015(false);
                if (pooledView015.IsSpider) pooledView015.PoseSpider(0f, 0f, false);
                pooledFootprints015[style * 2] = PresentationFootprint.Measure(transform, body, leftArm, rightArm);
                SetCachedFins015(true);
                if (pooledView015.IsSpider) pooledView015.PoseSpider(0f, 0f, true);
                else leftLeg.localRotation = rightLeg.localRotation = Quaternion.Euler(-48f, 0f, 0f);
                pooledFootprints015[style * 2 + 1] = PresentationFootprint.Measure(transform, body, leftArm, rightArm,
                    pooledFootprints015[style * 2].NavigationRadius);
            }
            pooledView015.ApplyStyle(MonsterVoxelCosmetics.Style.GuardianGolem);
            leftLeg = pooledView015.LeftLeg; rightLeg = pooledView015.RightLeg;
            bodyMotion = new GroundedGolemMotion(transform, body, leftLeg, rightLeg, leftArm, rightArm, null, pooledView015);
            hp = 0f; maxHP = baseHealth = Foundation.CombatConfig.Default.MonsterHealth;
            RetirePooled015();
        }

        internal bool ActivatePooled015(Vector3 position, int stableId, MonsterVoxelCosmetics.Style style, bool flying, bool seated)
        {
            if (pooledView015 == null || PoolLeased015 || PoolGeneration015 == uint.MaxValue || stableId < 1 ||
                (int)style < 0 || (int)style > 3 || !FinitePoolPoint015(position)) return false;
            ResetRewardAssociation015();
            PoolGeneration015++; PoolLeased015 = true; TargetId = stableId;
            session = GameSession.Instance; cachedPlayer = owner.Player; cachedCamera = cachedPlayer != null ? cachedPlayer.ViewCamera : null;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            body.localPosition = Vector3.zero; body.localRotation = Quaternion.identity; body.localScale = Vector3.one * PresentationScale;
            pooledStyle015 = style; pooledView015.ApplyStyle(style); leftLeg = pooledView015.LeftLeg; rightLeg = pooledView015.RightLeg;
            IsFlying = false; elementStyled = style != MonsterVoxelCosmetics.Style.GuardianGolem;
            appearanceStone = new Color(.26f, .29f, .31f); elementPalette = default;
            if (elementStyled)
            {
                elementPalette = MonsterElementPalette.ForStableId(stableId); appearanceStone = MonsterElementPresentation.ColorOf(elementPalette.Stone);
                pooledView015.Tint(flashBlock, elementPalette);
                MonsterElementPresentation.Tint(pooledHealthRenderer015, flashBlock, elementPalette.Rune, elementPalette.Emission);
                for (int i = 0; i < 2; i++) MonsterElementPresentation.Tint(pooledFinRenderers015[i], flashBlock, elementPalette.Rune, elementPalette.Emission);
            }
            else
            {
                pooledHealthRenderer015.SetPropertyBlock(null);
                for (int i = 0; i < 2; i++) pooledFinRenderers015[i].SetPropertyBlock(null);
            }
            ranged = style == MonsterVoxelCosmetics.Style.Shade;
            ConfigureArchetype(style == MonsterVoxelCosmetics.Style.Spider ? MonsterArchetype.Spider : ranged ? MonsterArchetype.Shade : MonsterArchetype.Stalker);
            defenseConfigured = false; configuredWindup = ranged ? .7f : .55f; configuredRecovery = ranged ? 3.5f : .7f;
            attackCooldown = ranged ? 1.4f : .7f;
            movementScale = damageScale = 1f;
            ResetTransientPoolState015();
            home = position; laneForward = owner.Shrine != null ? owner.Shrine.transform.position - home : Vector3.forward;
            laneForward.y = 0f; laneForward = laneForward.sqrMagnitude > .001f ? laneForward.normalized : Vector3.forward;
            aggroSearchRemaining = Mathf.Abs(stableId % 10) * .01f;
            SetCachedFins015(false); presentation = pooledFootprints015[(int)style * 2];
            solid.center = presentation.Hitbox.center; solid.size = presentation.Hitbox.size; solid.enabled = false; solid.isTrigger = true;
            bodyMotion.RebindPooled015(); ResetAdditionalPooledState015();
            healthFill.localScale = new Vector3(.76f, .045f, .026f); healthFill.localPosition = new Vector3(0f, 0f, -.025f);
            warning.localPosition = new Vector3(0f, .025f, 0f); warning.gameObject.SetActive(false); healthBar.gameObject.SetActive(true);
            gameObject.SetActive(true);
            if (flying) ConfigureFlying();
            if (seated) SitDormant();
            return true;
        }

        void ResetTransientPoolState015()
        {
            ClearFeedbackPresentation022();ClearTrainingLabBinding(); ClearRemoteAttackPresentation();
            peerTargetValid = pendingPeerAttack = false; peerTargetId = pendingPeerId = 0UL; peerTargetFeet = peerTargetAim = default;
            blockingTower = pendingTower = null; attackFan = coreFan = false; AttackSlotReady = false; TowerApproachPoint = default;
            dormant = pendingShrine = pendingTowerAttack = pendingTowerVolley = tutorialDuel = hadVulnerabilityTint = false;
            seatedBlend = slowRemaining = staggerRemaining = earthVulnerabilityRemaining = burnRemaining = burnDps = weakenRemaining = 0f;
            windup = strikeRemaining = flash = deathTime = strideClock = soundWalkDistance = nextAggroSound = windupCueRemaining = impactCueRemaining = 0f;
            hoverBaseY = hoverPhase = 0f; aimedDirection = committedAttackPoint = default; committedTowerId = 0;
            AttackPresentationSequence = 0; crowdMotionFrame = -1; soundSpiderCycle = -1;
            IsAggroedOnPlayer = false; alertShown = false;
            if (aggroAlert != null) aggroAlert.gameObject.SetActive(false);
            remoteAttackStyle = 0; remoteWindupSeconds = .55f; remoteStrikeSeconds = .18f; remoteRecoverySeconds = .7f;
            remoteAttackProgress = remoteSnapshotProgress = remotePhaseElapsed = 0f; remoteLiveStatus009 = 0u;
            flashBlock.Clear();
        }
        internal void RetirePooled015()
        {
            if (pooledView015 == null || !PoolLeased015 && !gameObject.activeSelf) return;
            ResetRewardAssociation015(); ResetAdditionalPooledState015(); ResetTransientPoolState015();
            hp = 0f; PoolLeased015 = false; solid.enabled = false;
            SetCachedFins015(false); warning.gameObject.SetActive(false); healthBar.gameObject.SetActive(false);
            ReleaseStatusHud009(); gameObject.SetActive(false);
        }
        bool ConfigurePooledFlying015()
        {
            if (pooledView015 == null) return false;
            if (IsFlying) return true;
            IsFlying = true; hoverBaseY = transform.position.y + 1.1f;
            transform.position = new Vector3(transform.position.x, hoverBaseY, transform.position.z);
            SetCachedFins015(true);
            if (pooledView015.IsSpider) pooledView015.PoseSpider(0f, 0f, true);
            else leftLeg.localRotation = rightLeg.localRotation = Quaternion.Euler(-48f, 0f, 0f);
            presentation = pooledFootprints015[(int)pooledStyle015 * 2 + 1];
            solid.center = presentation.Hitbox.center; solid.size = presentation.Hitbox.size;
            return true;
        }
        void SetCachedFins015(bool visible)
        { for (int i = 0; i < 2; i++) if (pooledFins015[i] != null) pooledFins015[i].gameObject.SetActive(visible); }
        internal void DisposePooledResources015()
        {
            if (pooledView015 == null) return;
            RetirePooled015(); pooledView015.Dispose(); pooledView015 = null;
        }
        static bool FinitePoolPoint015(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
            !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world foot-root metres and actor-local+Z/Y-up; exactly one1.20 body scale; eight cold cached style/flying footprints.
// Array Formats:48 reusable authority slots;17 cached role parts/two fins per view;nonwrapping generation;optional private partial reward/additional-state reset hooks.
// Dependencies/Pointers: Same OracleEnemy HP/attack/status owners; shared template/mesh leases outlive views; trigger retained for existing projectile authority until full numeric integration.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world metres/Y-up, original admission-cached oriented monster box; no gameplay geometry/HP changes.
// Array Formats: Existing48 actor/projectile pools consume owned numeric48 snapshots and fixed World buffers.
// Dependencies/Pointers: Atomic numeric sweep consumer migration; old triggers disabled only with complete packet; root owns native acceptance and actual source integration.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing authoritative legacy Combat/World/revision and actor pool lifetime, world metres/Y-up.
// Array Formats: Existing48 actors with bounded feedback values/Morton jobs and one synchronous accepted-damage observation; no extra damage, mesh or material owner.
// Dependencies/Pointers: Positive actual HP loss owns immediate cached MPB red flash and observer dispatch. Root composes this exact work projection and verifies native collision/flash/audio/network/GC; replicas never move authoritatively.
