using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>
    /// Voxel golems use the original Stalker (58 HP / 5 melee) and Shade
    /// (80 HP / 7*.6375 ranged) authored baselines. Their small-room movement,
    /// readable windups, and 32-source-unit metre scale are first-person adaptations.
    /// </summary>
    public sealed partial class OracleEnemy : MonoBehaviour
    {
        // # ARCHITECTURE SNAPSHOT
        // Only body presentation scales. Foot-root world metres, authority,
        // damage, aim/reach and all attack clocks retain their existing owners.
        // Mesh-derived scalar footprints are measured at admission, not per frame.
        public const float PresentationScale = 1.20f;
        public static float SpawnFootprintRadius(bool flying) => flying ? 1.48f : 1.30f;
        PresentationFootprint presentation;
        public Bounds PresentationHitbox => presentation.Hitbox;
        public float CrowdRadius => presentation.CrowdRadius;
        public float NavigationRadius => presentation.NavigationRadius;
        public float NavigationBottom => presentation.CapsuleBottom;
        public float NavigationTop => presentation.CapsuleTop;
        internal float TowerBodyRadius => IsFlying ? presentation.LowerBodyRadius : presentation.NavigationRadius;
        internal float MinimumVisualY => transform.position.y + presentation.MinimumVisualY;
        internal float MaximumVisualY => transform.position.y + (body!=null?body.localPosition.y:0f) + presentation.Hitbox.max.y;
        public bool Alive => hp > 0f;
        public bool Targetable => Alive && !dormant && seatedBlend <= .001f;
        public bool IsAwake => !dormant && seatedBlend <= .001f;
        public int TargetId { get; private set; }
        public float HP => hp;
        public float MaxHP => maxHP;
        public bool IsRanged => ranged;
        public bool IsFlying { get; private set; }
        public bool IsAggroedOnPlayer { get; private set; }
        public enum MonsterArchetype { Stalker, Shade, Spider }
        public MonsterArchetype Archetype { get; private set; }
        public enum GuardianAttackPhase { Dormant, Ready, Windup, Strike, Recovery, Dead }
        public GuardianAttackPhase AttackPhase => !Alive ? GuardianAttackPhase.Dead : dormant || seatedBlend > .001f ? GuardianAttackPhase.Dormant :
            windup > 0f ? GuardianAttackPhase.Windup : strikeRemaining > 0f ? GuardianAttackPhase.Strike :
            attackCooldown > 0f ? GuardianAttackPhase.Recovery : GuardianAttackPhase.Ready;
        public int AttackPresentationSequence { get; private set; }
        public int AttackPresentationStyle => (pendingTowerVolley || ranged && pendingTowerAttack) &&
            (windup > 0f || strikeRemaining > 0f) ? 2 : ranged ? 1 : 0;
        public float AttackWindupSeconds => pendingTowerVolley ? Mathf.Max(.95f, configuredWindup) : configuredWindup;
        public float AttackStrikeSeconds => ranged || pendingTowerVolley ? .12f : .18f;
        public float AttackRecoverySeconds => pendingTowerVolley ? 2.5f : configuredRecovery;
        public Vector3 LockedAttackDirection => aimedDirection.sqrMagnitude > .0001f ? aimedDirection.normalized : laneForward;
        public float AttackProgress => AttackPhase == GuardianAttackPhase.Windup ? Mathf.Clamp01(1f - windup / AttackWindupSeconds) :
            AttackPhase == GuardianAttackPhase.Strike ? Mathf.Clamp01(1f - strikeRemaining / AttackStrikeSeconds) :
            AttackPhase == GuardianAttackPhase.Recovery ? Mathf.Clamp01(1f - attackCooldown / AttackRecoverySeconds) : 0f;
        // First-person Earth adaptation: one shared incoming modifier, never stacked.
        public const float EarthStaggerDamageMultiplier = 1.25f;
        public float VulnerabilityRemaining => Mathf.Min(earthVulnerabilityRemaining, staggerRemaining);
        public float IncomingDamageMultiplier => VulnerabilityRemaining > 0f ? EarthStaggerDamageMultiplier : 1f;
        internal Vector3 AimPoint => transform.position + Vector3.up * 1.2f;
        OracleCombat owner;
        GameSession session;
        OraclePlayer cachedPlayer;
        Camera cachedCamera;
        bool peerTargetValid,pendingPeerAttack;
        ulong peerTargetId,pendingPeerId;
        Vector3 peerTargetFeet,peerTargetAim;
        Transform aggroAlert;
        Vector3 laneForward;
        PlacedElementShrine blockingTower, pendingTower;
        int crowdMotionFrame = -1;
        GroundedGolemMotion bodyMotion;
        MonsterVisualLease cosmeticParts;
        internal PlacedElementShrine AttackTower => blockingTower;
        internal OracleShrine AttackCore => coreFan && owner != null && !IsAggroedOnPlayer &&
            (blockingTower == null || blockingTower.IsDestroyed) ? owner.Shrine : null;
        bool attackFan, coreFan;
        public bool AttackSlotReady { get; private set; }
        public Vector3 TowerApproachPoint { get; private set; }
        public Vector3 AttackApproachPoint => TowerApproachPoint;
        public bool IsCoreAttackFan => attackFan && coreFan;
        public int TargetTowerId => blockingTower != null && !blockingTower.IsDestroyed ? blockingTower.Id : -1;
        internal float AttackLaneHalfWidth => IsCoreAttackFan ? 2.15f : attackFan ? IsFlying ? 1.30f : 1.48f : .8f;
        int remoteAttackPhase = -1;
        int remoteAttackStyle, remoteWindupCueSequence = -1;
        float remoteAttackProgress, remoteSnapshotProgress, remotePhaseElapsed;
        float remoteWindupSeconds = .55f, remoteStrikeSeconds = .18f, remoteRecoverySeconds = .7f;
        RemoteAttackPresentationLedger remoteAttackLedger;
        bool alertShown;
        Vector3 remoteTargetPoint, committedAttackPoint;
        int committedTowerId;
        bool remoteAggroPrimed, remoteMovementPrimed;
        public GuardianAttackPhase PresentedAttackPhase => owner != null && owner.RemotePresentation && remoteAttackPhase >= 0 ? (GuardianAttackPhase)remoteAttackPhase : AttackPhase;
        public float PresentedAttackProgress => owner != null && owner.RemotePresentation && remoteAttackPhase >= 0 ? remoteAttackProgress : AttackProgress;
        public bool AttackWarningVisible => warning != null && warning.gameObject.activeSelf;
        public int AttackPresentationTargetId => AttackPresentationStyle == 2 ? committedTowerId : 0;
        public Vector3 LockedAttackEndpoint => AttackPresentationStyle == 2 ? committedAttackPoint : AimPoint;

        public void ApplyRemoteAttackPresentation(int phase, float progress, bool aggro)
        {
            // Compatibility hook only; network snapshots use the sequenced overload.
            if (owner == null || !owner.RemotePresentation || !RemoteDangerPolicy.Phase(phase, progress, 0)) return;
            remoteAttackPhase = phase; remoteAttackProgress = remoteSnapshotProgress = progress; remotePhaseElapsed = 0f;
            ApplyRemoteAggroState(aggro);
        }
        public bool ApplyRemoteAttackPresentation(int phase, float progress, bool aggro, int attackSequence,
            int snapshotSequence, Vector3 lockedDirection, int style, float windupSeconds, float strikeSeconds, float recoverySeconds,
            int targetTowerId, Vector3 targetPoint)
        {
            if (owner == null || !owner.RemotePresentation || !RemoteDangerPolicy.Direction(lockedDirection) ||
                !RemoteDangerPolicy.Style(style, windupSeconds, strikeSeconds, recoverySeconds) ||
                !RemoteDangerPolicy.Point(targetPoint) || targetTowerId < 0 || targetTowerId > 65535 ||
                (style == 2 && (targetTowerId == 0 || (targetPoint - transform.position).sqrMagnitude > 81f)) ||
                (style != 2 && targetTowerId != 0) ||
                !remoteAttackLedger.Apply(snapshotSequence, attackSequence, phase, progress)) return false;
            remoteAttackPhase = phase; remoteAttackProgress = remoteSnapshotProgress = progress; remotePhaseElapsed = 0f;
            remoteTargetPoint = targetPoint;
            remoteAttackStyle = style; remoteWindupSeconds = windupSeconds; remoteStrikeSeconds = strikeSeconds; remoteRecoverySeconds = recoverySeconds;
            if (phase == (int)GuardianAttackPhase.Windup || phase == (int)GuardianAttackPhase.Strike)
            { Vector3 flat = lockedDirection; flat.y = 0f; if (flat.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(flat); }
            ApplyRemoteAggroState(aggro);
            if (style == 2) warning.position = targetPoint + Vector3.up * .025f;
            else warning.localPosition = new Vector3(0f, .025f, 0f);
            warning.gameObject.SetActive(Alive && phase == (int)GuardianAttackPhase.Windup);
            if (phase == (int)GuardianAttackPhase.Windup && remoteWindupCueSequence != attackSequence)
            {
                remoteWindupCueSequence = attackSequence;
                if (progress < .9f) PlayEnemyCue(false);
            }
            UpdateAlertVisibility();
            return true;
        }
        void TickRemoteAttackPresentation(float dt)
        {
            if (remoteAttackPhase < 0 || dt <= 0f) return;
            strideClock += dt;
            windupCueRemaining = Mathf.Max(0f, windupCueRemaining - dt);
            impactCueRemaining = Mathf.Max(0f, impactCueRemaining - dt);
            remotePhaseElapsed = Mathf.Min(RemoteDangerPolicy.MaximumExtrapolation, remotePhaseElapsed + dt);
            float duration = remoteAttackPhase == (int)GuardianAttackPhase.Windup ? remoteWindupSeconds :
                remoteAttackPhase == (int)GuardianAttackPhase.Strike ? remoteStrikeSeconds : remoteRecoverySeconds;
            remoteAttackProgress = Mathf.Clamp01(remoteSnapshotProgress + remotePhaseElapsed / duration);
            if (warning != null && remoteAttackPhase == (int)GuardianAttackPhase.Windup)
            {
                float size = (remoteAttackStyle == 2 ? 1.8f : remoteAttackStyle == 0 ? 1.4f : 1.1f) * (1f + remoteAttackProgress * .3f);
                if (remoteAttackStyle == 2) warning.position = remoteTargetPoint + Vector3.up * .025f;
                warning.localScale = new Vector3(size, .025f, size);
            }
        }
        float baseHealth, baseMeleeDamage = 5f, strideClock;
        float aggroSearchRemaining, windupCueRemaining, impactCueRemaining;
        bool ownsAlertResources;
        static Mesh alertMesh;
        static Material alertMaterial;
        static int alertUsers;
        bool ranged, defenseConfigured;
        float configuredWindup = .55f, configuredRecovery = .7f;
        float hp, maxHP;
        float attackCooldown, windup, strikeRemaining, flash, deathTime;
        Vector3 aimedDirection;
        Vector3 home;
        Transform body, leftArm, rightArm, leftLeg, rightLeg, warning, healthBar, healthFill;
        readonly List<Renderer> stoneRenderers = new List<Renderer>();
        MaterialPropertyBlock flashBlock;
        BoxCollider solid;
        float soundWalkDistance, nextAggroSound;
        int soundSpiderCycle = -1;
        int ProductionRole => tutorialDuel ? 0 : Archetype == MonsterArchetype.Shade ? 2 : Archetype == MonsterArchetype.Spider ? 3 : 1;
        void RecordLocomotionSound(Vector3 before)
        {
            if (session == null || session.Audio == null || IsFlying || !Targetable) return;
            Vector3 delta = transform.position - before; delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= .0001f || distance > .6f) return;
            soundWalkDistance += distance;
            float stride = ProductionRole == 0 ? 1.25f : Archetype == MonsterArchetype.Spider ? .8f : 1f;
            if (soundWalkDistance >= stride)
            { soundWalkDistance %= stride; session.Audio.PlayProductionCue(ProductionSoundBank.Enemy(ProductionRole, 2), AimPoint); }
            float phase = strideClock + TargetId * .17f;
            int cycle = Mathf.FloorToInt(phase / 2.4f);
            if (Archetype == MonsterArchetype.Spider && Mathf.Repeat(phase, 2.4f) < .45f && cycle != soundSpiderCycle)
            { soundSpiderCycle = cycle; session.Audio.PlayProductionCue(ProductionCue.SpiderSpecial, AimPoint); }
        }
        bool dormant, pendingShrine, pendingTowerAttack, pendingTowerVolley, tutorialDuel, hadVulnerabilityTint;
        float seatedBlend, slowRemaining, staggerRemaining, earthVulnerabilityRemaining;
        float movementScale = 1f, damageScale = 1f;
        float burnRemaining, burnDps, weakenRemaining, hoverBaseY, hoverPhase;
        Material flyingAccent;
        MonsterElementPalette elementPalette;
        Color appearanceStone = new Color(.26f, .29f, .31f);
        bool elementStyled;
        public int PresentedElementTheme => elementStyled ? (int)elementPalette.Theme : -1;

        internal void Initialize(OracleCombat combat, bool isShade, Material stone, Material joints,
            Material turquoise, Material violet, Material attack, Material barBackground, int targetId, MonsterVoxelCosmetics.Style visualStyle)
        {
            owner = combat;
            session = GameSession.Instance;
            cachedPlayer = combat.Player;
            cachedCamera = cachedPlayer != null ? cachedPlayer.ViewCamera : null;
            flyingAccent = turquoise;
            TargetId = targetId;
            ranged = isShade;
            configuredWindup = ranged ? .7f : .55f;
            configuredRecovery = ranged ? 3.5f : .7f;
            Archetype = ranged ? MonsterArchetype.Shade : MonsterArchetype.Stalker;
            hp = maxHP = baseHealth = Foundation.CombatConfig.Default.MonsterHealth;
            home = transform.position;
            laneForward = combat.Shrine != null ? combat.Shrine.transform.position - home : Vector3.forward;
            laneForward.y = 0f;
            laneForward = laneForward.sqrMagnitude > .001f ? laneForward.normalized : Vector3.forward;
            aggroSearchRemaining = Mathf.Abs(targetId % 10) * .01f;
            attackCooldown = ranged ? 1.4f : .7f;
            flashBlock = new MaterialPropertyBlock();
            body = new GameObject("Block-built golem body").transform;
            body.SetParent(transform, false);
            body.localScale = Vector3.one * PresentationScale;
            Material rune = ranged ? violet : turquoise;
            leftArm = new GameObject("Left arm pivot").transform;
            leftArm.SetParent(body, false); leftArm.localPosition = new Vector3(-.53f, 1.36f, 0f);
            rightArm = new GameObject("Right arm pivot").transform;
            rightArm.SetParent(body, false); rightArm.localPosition = new Vector3(.53f, 1.36f, 0f);
            cosmeticParts = MonsterVisualFactory.Create(visualStyle, body, leftArm, rightArm,
                stone, joints, rune, stoneRenderers);
            leftLeg = cosmeticParts.LeftLeg; rightLeg = cosmeticParts.RightLeg;
            warning = OracleCombat.CreateBlock(transform, "Attack telegraph", new Vector3(0f, .025f, 0f),
                new Vector3(1.2f, .025f, 1.2f), attack);
            warning.gameObject.SetActive(false);
            healthBar = new GameObject("Guardian health").transform;
            healthBar.SetParent(transform, false); healthBar.localPosition = new Vector3(0f, 2.24f * PresentationScale, 0f);
            OracleCombat.CreateBlock(healthBar, "Health background", Vector3.zero, new Vector3(.82f, .075f, .035f), barBackground);
            healthFill = OracleCombat.CreateBlock(healthBar, "Health fill", new Vector3(0f, 0f, -.025f),
                new Vector3(.76f, .045f, .026f), rune);
            solid = gameObject.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, 1f, 0f);
            solid.size = new Vector3(.9f, 1.95f, .75f);
            solid.isTrigger = true; // Hits remain queryable; actors never hard-block each other.
            RefreshPresentationFootprint(false);
            CreateAggroAlert(attack);
            bodyMotion = new GroundedGolemMotion(transform, body, leftLeg, rightLeg, leftArm, rightArm, cosmeticParts.OriginalForMotion);
            elementStyled = visualStyle != MonsterVoxelCosmetics.Style.GuardianGolem;
            if (elementStyled)
            {
                elementPalette = MonsterElementPalette.ForStableId(TargetId);
                appearanceStone = MonsterElementPresentation.ColorOf(elementPalette.Stone);
                MonsterElementPresentation.Apply(transform, stone, joints, rune, flashBlock, elementPalette);
            }
        }

        void RefreshPresentationFootprint(bool preserveNavigation)
        {
            presentation = PresentationFootprint.Measure(transform, body, leftArm, rightArm,
                preserveNavigation ? presentation.NavigationRadius : 0f);
            solid.center = presentation.Hitbox.center;
            solid.size = presentation.Hitbox.size;
            solid.isTrigger = true;
        }

        Transform Block(Transform parent, string label, Vector3 position, Vector3 size, Material material, bool canFlash = true)
        {
            Transform block = OracleCombat.CreateBlock(parent, label, position, size, material);
            if (canFlash) stoneRenderers.Add(block.GetComponent<Renderer>());
            return block;
        }

        internal void SitDormant()
        {
            tutorialDuel = true;
            dormant = true; seatedBlend = 1f;
            healthBar.gameObject.SetActive(false);
            ApplySeatedPose();
        }

        internal void ConfigureFlying()
        {
            if (IsFlying) return;
            IsFlying = true;
            hoverBaseY = transform.position.y + 1.1f;
            transform.position = new Vector3(transform.position.x, hoverBaseY, transform.position.z);
            if (cosmeticParts != null && cosmeticParts.SpiderLegs != null) cosmeticParts.PoseSpider(0f, 0f, true);
            else leftLeg.localRotation = rightLeg.localRotation = Quaternion.Euler(-48f, 0f, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform fin = OracleCombat.CreateBlock(body, "Flying guardian aether fin", new Vector3(side * .81f, 1.2f, -.05f),
                    new Vector3(.72f, .075f, .42f), flyingAccent);
                fin.localRotation = Quaternion.Euler(0f, 0f, side * 18f);
                if (elementStyled) MonsterElementPresentation.Tint(fin.GetComponent<Renderer>(), flashBlock,
                    elementPalette.Rune, elementPalette.Emission);
            }
            RefreshPresentationFootprint(true);
        }

        bool OfflinePlayerTargetable=>session!=null&&cachedPlayer!=null&&cachedPlayer.gameObject.activeInHierarchy&&
            session.HP>0f&&!session.IsDead&&!session.IsDowned;
        bool LocalPlayerTargetable=>owner.PeerArenaCombat?peerTargetValid:OfflinePlayerTargetable;

        bool PlayerBlocksLane(float range,bool retaining)=>LocalPlayerTargetable&&
            PointBlocksLane(owner.PeerArenaCombat?peerTargetFeet:cachedPlayer.transform.position,range,retaining);

        bool PointBlocksLane(Vector3 feet,float range,bool retaining)
        {
            Vector3 offset=feet-transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > range * range || Vector3.Dot(offset, laneForward) < (retaining ? -1.5f : -.05f)) return false;
            // The corridor stays on the assigned spawn-to-shrine lane, rather
            // than rotating toward a player who tries to pull an enemy sideways.
            Vector3 fromLaneOrigin = feet - home;
            fromLaneOrigin.y = 0f;
            Vector3 across = fromLaneOrigin - laneForward * Vector3.Dot(fromLaneOrigin, laneForward);
            return across.sqrMagnitude <= 1.1f * 1.1f;
        }

        void RefreshPeerTarget()
        {
            bool had = peerTargetValid;
            ulong retainedId = peerTargetId;
            peerTargetValid = false;
            if (!session.Defense.TryGetEnemyLane(TargetId, out int lane)) return;
            int count = owner.QueryPeerTargets(out PeerCombatTarget[] peers);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                PeerCombatTarget candidate = peers[i];
                bool retaining = had && candidate.PeerId == retainedId;
                if (!candidate.Targetable || candidate.Lane != lane ||
                    !PointBlocksLane(candidate.Feet, retaining ? 6f : 4f, retaining) ||
                    !owner.HasLineOfSight(AimPoint, candidate.Aim)) continue;
                Vector3 offset = candidate.Feet - transform.position; offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (!peerTargetValid || distance < best - .00001f || Mathf.Abs(distance - best) <= .00001f && candidate.PeerId < peerTargetId)
                { best = distance; peerTargetValid = true; peerTargetId = candidate.PeerId; peerTargetFeet = candidate.Feet; peerTargetAim = candidate.Aim; }
            }
        }

        bool FindPendingPeer(out Vector3 aim)
        {
            aim = default;
            if (!owner.PeerArenaCombat || !pendingPeerAttack || !session.Defense.TryGetEnemyLane(TargetId, out int lane)) return false;
            int count = owner.QueryPeerTargets(out PeerCombatTarget[] peers);
            for (int i = 0; i < count; i++)
                if (peers[i].PeerId == pendingPeerId && peers[i].Targetable && peers[i].Lane == lane &&
                    PointBlocksLane(peers[i].Feet, 6f, true))
                { aim = peers[i].Aim; return true; }
            return false;
        }

        void UpdatePlayerAggro(float dt)
        {
            if(tutorialDuel&&!owner.PeerArenaCombat){SetPlayerAggro(OfflinePlayerTargetable);return;}
            aggroSearchRemaining -= dt;
            if (aggroSearchRemaining > 0f) return;
            aggroSearchRemaining=.1f; // At most8 validated candidates,10 searches/s/enemy.
            if(owner.PeerArenaCombat)RefreshPeerTarget();
            Vector3 sightPoint = owner.PeerArenaCombat?peerTargetAim:cachedCamera != null ? cachedCamera.transform.position : cachedPlayer != null ? cachedPlayer.transform.position + Vector3.up * 1.65f : AimPoint;
            bool blocked = LocalPlayerTargetable && PlayerBlocksLane(IsAggroedOnPlayer ? 6f : 4f, IsAggroedOnPlayer) &&
                owner.HasLineOfSight(AimPoint, sightPoint);
            Vector3 endpoint = blocked ? sightPoint : owner.Shrine != null ? owner.Shrine.AimPoint : AimPoint + laneForward * 6f;
            PlacedElementShrine.TryFindBlockingTower(transform.position, endpoint, 6f, NavigationRadius, out blockingTower);
            if (blockingTower == null && defenseConfigured && owner.Shrine != null)
                // Ground/flying roles inspect eligible towers on the authored route before player aggro.
                PlacedElementShrine.TryFindLaneThreatTower(transform.position, AimPoint, home,
                    owner.Shrine.transform.position, 8f, out blockingTower);
            if (windup > 0f && pendingTower != null && pendingTower != blockingTower)
            { windup = 0f; warning.gameObject.SetActive(false); attackCooldown = Mathf.Max(attackCooldown, .15f); }
            SetPlayerAggro(blocked && blockingTower == null);
        }

        void SetPlayerAggro(bool value, bool cancelWrongWindup = true)
        {
            if (IsAggroedOnPlayer == value) return;
            IsAggroedOnPlayer = value;
            if (value && owner != null && owner.Playing && session != null && session.Audio != null && Time.unscaledTime >= nextAggroSound)
            {
                nextAggroSound = Time.unscaledTime + 1f;
                session.Audio.PlayProductionCue(ProductionSoundBank.Enemy(ProductionRole, 0), AimPoint);
            }
            UpdateAlertVisibility();
            if (cancelWrongWindup && windup > 0f && pendingShrine == value)
            {
                windup = 0f;
                if (warning != null) warning.gameObject.SetActive(false);
                attackCooldown = Mathf.Max(attackCooldown, .15f);
            }
        }

        // Presentation hook for a future host snapshot; it never retargets an
        // authoritative enemy or invokes damage on a client.
        public void ApplyRemoteAggroState(bool aggro)
        {
            if (owner == null || !owner.RemotePresentation) return;
            bool entering = remoteAggroPrimed && aggro && Alive && !IsAggroedOnPlayer;
            remoteAggroPrimed = true;
            if (entering && session != null && session.Audio != null && Time.unscaledTime >= nextAggroSound)
            { nextAggroSound = Time.unscaledTime + 1f; session.Audio.PlayProductionCue(ProductionSoundBank.Enemy(ProductionRole, 0), AimPoint); }
            SetPlayerAggro(aggro && Alive, false);
        }

        void CreateAggroAlert(Material source)
        {
            if (alertMaterial == null)
            {
                alertMaterial = new Material(source) { name = "Shared yellow enemy blockade alert" };
                Color yellow = new Color(1f, .82f, .07f);
                alertMaterial.SetColor("_BaseColor", yellow);
                alertMaterial.SetColor("_Color", yellow);
                alertMaterial.EnableKeyword("_EMISSION");
                alertMaterial.SetColor("_EmissionColor", yellow * 1.5f);
            }
            if (alertMesh == null)
            {
                alertMesh = new Mesh { name = "Shared exclamation glyph, two quads" };
                alertMesh.vertices = new[] {
                    new Vector3(-.065f,.10f,0),new Vector3(.065f,.10f,0),new Vector3(.065f,.46f,0),new Vector3(-.065f,.46f,0),
                    new Vector3(-.065f,-.10f,0),new Vector3(.065f,-.10f,0),new Vector3(.065f,.03f,0),new Vector3(-.065f,.03f,0)
                };
                alertMesh.triangles = new[] { 0,2,1,0,3,2,4,6,5,4,7,6 };
                alertMesh.RecalculateNormals();
                alertMesh.RecalculateBounds();
                alertMesh.UploadMeshData(true);
            }
            alertUsers++; ownsAlertResources = true;
            var alert = new GameObject("Pooled enemy exclamation alert", typeof(MeshFilter), typeof(MeshRenderer));
            aggroAlert = alert.transform;
            aggroAlert.SetParent(transform, false);
            aggroAlert.localPosition = new Vector3(0f, 2.65f * PresentationScale, 0f);
            alert.GetComponent<MeshFilter>().sharedMesh = alertMesh;
            MeshRenderer renderer = alert.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = alertMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            alert.SetActive(false);
        }

        void UpdateAlertVisibility()
        {
            GuardianAttackPhase phase = owner != null && owner.RemotePresentation && remoteAttackPhase >= 0 ? (GuardianAttackPhase)remoteAttackPhase : AttackPhase;
            bool show = Alive && (IsAggroedOnPlayer || phase == GuardianAttackPhase.Windup);
            if (show == alertShown || aggroAlert == null) return;
            alertShown = show; aggroAlert.gameObject.SetActive(show);
        }

        void PlayEnemyCue(bool impact)
        {
            if (session == null || session.Audio == null) return;
            if (impact)
            {
                if (impactCueRemaining > 0f) return;
                impactCueRemaining = .35f;
            }
            else
            {
                if (windupCueRemaining > 0f) return;
                windupCueRemaining = .8f;
            }
            ProductionCue typed = ProductionSoundBank.Enemy(ProductionRole, 1);
            if (!impact && session.Audio.HasProductionCue(typed)) session.Audio.PlayProductionCue(typed, AimPoint);
            else session.Audio.PlayCue(impact ? "enemy_impact" : "enemy_windup", AimPoint);
        }

        void LateUpdate()
        {
            if (owner != null && owner.Playing && Targetable && !owner.IsTutorialTowerPracticeTarget(this) && !TrainingLabBound && crowdMotionFrame != Time.frameCount)
                MoveWithSpacing(Vector3.zero, 1.3f, Time.deltaTime);
            bool remote = owner != null && owner.RemotePresentation;
            if (session != null && (session.SimulationActive || remote && session.Defense != null && session.Defense.Running))
            {
                GuardianAttackPhase phase = remote && remoteAttackPhase >= 0 ? (GuardianAttackPhase)remoteAttackPhase : AttackPhase;
                bodyMotion?.Tick(remote ? Time.unscaledDeltaTime : Time.deltaTime, Alive, IsFlying, dormant || seatedBlend > .001f, !remote && owner.Playing,
                    phase, remote && remoteAttackPhase >= 0 ? remoteAttackProgress : AttackProgress, flash);
                if (remote && (phase == GuardianAttackPhase.Windup || phase == GuardianAttackPhase.Strike))
                {
                    float right = phase == GuardianAttackPhase.Windup ? -105f * remoteAttackProgress : Mathf.Lerp(-105f, 30f, remoteAttackProgress);
                    float leftStart = remoteAttackStyle == 0 ? -105f : -70f;
                    float left = phase == GuardianAttackPhase.Windup ? leftStart * remoteAttackProgress : Mathf.Lerp(leftStart, 30f, remoteAttackProgress);
                    rightArm.localRotation = Quaternion.Euler(right, 0f, 0f); leftArm.localRotation = Quaternion.Euler(left, 0f, 0f);
                }
                if (Alive && solid != null) solid.center = presentation.Hitbox.center + body.localPosition;
            }
            UpdateAlertVisibility();
            if (alertShown && aggroAlert != null && cachedCamera != null)
                aggroAlert.rotation = cachedCamera.transform.rotation;
        }

        void MoveWithSpacing(Vector3 desired, float speed, float dt)
        {
            if(TrainingLabBound)return;
            crowdMotionFrame = Time.frameCount;
            Vector3 move = owner.SpaceMovement(this, desired, IsCoreAttackFan ? owner.Shrine.transform.position : attackFan && blockingTower != null ? blockingTower.Position : home, laneForward, !tutorialDuel && !attackFan, speed, dt);
            if (move.sqrMagnitude > .000001f && owner.CanMove(this, move)) transform.position += move;
        }

        public void ConfigureArchetype(MonsterArchetype archetype)
        {
            Archetype = archetype;
            ranged = archetype == MonsterArchetype.Shade;
            baseHealth = Foundation.CombatConfig.Default.MonsterHealth;
            baseMeleeDamage = archetype == MonsterArchetype.Spider ? 6f : 5f;
            hp = maxHP = baseHealth;
        }

        public void ApplyBurn(float damagePerSecond, float duration)
        {
            if (!Targetable || !owner.Playing || damagePerSecond <= 0f || duration <= 0f) return;
            burnDps = Mathf.Max(burnDps, Mathf.Clamp(damagePerSecond, 0f, 100f));
            burnRemaining = Mathf.Max(burnRemaining, Mathf.Clamp(duration, 0f, 8f));
        }

        public void ApplyComboEffect(int combo, Vector3 direction, float multiplier = 1f, int statusMask = 0)
        {
            if (!Targetable || !owner.Playing) return;
            if (combo == 1) ApplyBurn(3f * multiplier, 2f);
            else if (combo == 2)
            {
                slowRemaining = Mathf.Max(slowRemaining, PassiveStatusRules.WaterSlowSeconds(statusMask, true));
                ApplyStagger(PassiveStatusRules.EarthStaggerSeconds(statusMask, 2), true);
            }
            else if (combo == 3) weakenRemaining = Mathf.Max(weakenRemaining, 2.5f);
            else if (combo == 5)
            {
                ApplyBurn(2f * multiplier, 2f);
                ApplyStagger(PassiveStatusRules.EarthStaggerSeconds(statusMask, 5), true);
            }
            else if (combo == 6)
            {
                slowRemaining = Mathf.Max(slowRemaining, PassiveStatusRules.WaterSlowSeconds(statusMask));
                ApplyElementEffect(3, direction);
            }
        }

        float CurrentDamageScale => damageScale * (weakenRemaining > 0f ? .55f : 1f);

        void ApplySeatedPose()
        {
            body.localPosition = Vector3.down * (.35f * seatedBlend * PresentationScale);
            leftLeg.localPosition = new Vector3(-.23f, Mathf.Lerp(.29f, .42f, seatedBlend), .30f * seatedBlend);
            rightLeg.localPosition = new Vector3(.23f, Mathf.Lerp(.29f, .42f, seatedBlend), .30f * seatedBlend);
            leftLeg.localRotation = rightLeg.localRotation = Quaternion.Euler(75f * seatedBlend, 0f, 0f);
            leftArm.localRotation = rightArm.localRotation = Quaternion.Euler(-68f * seatedBlend, 0f, 0f);
        }

        void Update()
        {
            if (owner == null) return;
            if (owner.RemotePresentation)
            {
                if (session != null && session.Defense != null && session.Defense.Running) TickRemoteAttackPresentation(Time.unscaledDeltaTime);
                return;
            }
            if (!owner.Playing)
            {
                if (session == null || !session.HasStarted || session.Victory || session.IsDead) SetPlayerAggro(false);
                return;
            }
            float dt = Time.deltaTime;
            strideClock += dt;
            windupCueRemaining = Mathf.Max(0f, windupCueRemaining - dt);
            impactCueRemaining = Mathf.Max(0f, impactCueRemaining - dt);
            if (!Alive)
            {
                SetPlayerAggro(false);
                deathTime += dt;
                body.localPosition = Vector3.down * deathTime * 1.7f;
                body.localScale = Vector3.one * (PresentationScale * Mathf.Max(.05f, 1f - deathTime * 1.3f));
                if (deathTime > .75f) gameObject.SetActive(false);
                return;
            }
            if(owner.IsTutorialTowerPracticeTarget(this)){UpdateFlash(dt);return;}
            if (burnRemaining > 0f)
            {
                float burnDt = Mathf.Min(dt, burnRemaining);
                burnRemaining = Mathf.Max(0f, burnRemaining - dt);
                TakeHit(burnDps * burnDt);
                if (burnRemaining <= 0f) burnDps = 0f;
                if (!Alive) return;
            }
            weakenRemaining = Mathf.Max(0f, weakenRemaining - dt);
            if (IsFlying)
            {
                hoverPhase += dt * 2f;
                transform.position = new Vector3(transform.position.x, hoverBaseY + Mathf.Sin(hoverPhase) * .08f, transform.position.z);
            }
            UpdateFlash(dt);
            if (dormant)
            {
                if (!LocalPlayerTargetable || (cachedPlayer.transform.position - transform.position).sqrMagnitude > 36f) return;
                dormant = false;
                healthBar.gameObject.SetActive(true);
                owner.SpawnImpact(AimPoint, new Color(.5f, 1f, .88f), .8f);
            }
            if (seatedBlend > .001f)
            {
                seatedBlend = Mathf.MoveTowards(seatedBlend, 0f, dt / .75f);
                ApplySeatedPose();
                return;
            }
            if (cachedCamera != null) healthBar.rotation = cachedCamera.transform.rotation;
            if(TrainingLabBound){if(!PrepareTrainingLabAttack(dt))return;}else UpdatePlayerAggro(dt);
            slowRemaining = Mathf.Max(0f, slowRemaining - dt);
            staggerRemaining = Mathf.Max(0f, staggerRemaining - dt);
            earthVulnerabilityRemaining = Mathf.Max(0f, earthVulnerabilityRemaining - dt);
            attackCooldown = Mathf.Max(0f, attackCooldown - dt);
            if (staggerRemaining > 0f) return;
            if (strikeRemaining > 0f)
            {
                strikeRemaining = Mathf.Max(0f, strikeRemaining - dt);
                float strike = 1f - strikeRemaining / AttackStrikeSeconds;
                rightArm.localRotation = Quaternion.Euler(Mathf.Lerp(-105f, 35f, strike), 0f, 0f);
                leftArm.localRotation = Quaternion.Euler(Mathf.Lerp(ranged ? -70f : -105f, 30f, strike), 0f, 0f);
                return;
            }
            OracleShrine shrine = TrainingLabBound?null:owner.Shrine;
            bool shrineValid = shrine != null && !shrine.IsDestroyed;
            Vector3 playerPoint = owner.PeerArenaCombat?peerTargetAim:cachedCamera != null ? cachedCamera.transform.position : cachedPlayer != null ?
                cachedPlayer.transform.position + Vector3.up * 1.65f : transform.position;
            bool targetShrine = shrineValid && !IsAggroedOnPlayer;
            bool targetTower = blockingTower != null && !blockingTower.IsDestroyed && !IsAggroedOnPlayer;
            if (!targetShrine && !LocalPlayerTargetable) return;
            Vector3 targetPoint = targetTower ? blockingTower.AimPoint : targetShrine ? shrine.AimPoint : playerPoint;
            bool slotCanStrike = true;
            Vector3 approachPoint = targetPoint;
            attackFan = !TrainingLabBound && targetTower && !defenseConfigured && !ranged && owner.GetTowerApproach(this, blockingTower, laneForward, out approachPoint, out slotCanStrike);
            Vector3 coreOffset = shrineValid ? transform.position - shrine.transform.position : Vector3.zero; coreOffset.y = 0f;
            bool coreEligible = targetShrine && !targetTower && !ranged && coreOffset.sqrMagnitude <= (coreFan ? 49f : 42.25f);
            coreFan = coreEligible && owner.GetCoreApproach(this, shrine, laneForward, out approachPoint, out slotCanStrike);
            if (coreFan) attackFan = true;
            AttackSlotReady = attackFan && slotCanStrike;
            TowerApproachPoint = approachPoint;
            Vector3 approachOffset = approachPoint - transform.position; approachOffset.y = 0f;
            float approachDistance = approachOffset.magnitude;
            if (windup > 0f && pendingTowerAttack && (pendingTower == null || pendingTower.IsDestroyed))
            { windup = 0f; warning.gameObject.SetActive(false); attackCooldown = Mathf.Max(attackCooldown, .15f); }
            Vector3 flatOffset = targetPoint - AimPoint; flatOffset.y = 0f;
            float distance = IsFlying ? Vector3.Distance(AimPoint, targetPoint) : flatOffset.magnitude;
            bool visible = owner.HasLineOfSight(AimPoint, targetPoint, targetShrine && !targetTower);
            if (flatOffset.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flatOffset), dt * 220f);
            if (windup > 0f)
            {
                windup -= dt;
                float normalized = 1f - Mathf.Max(0f, windup) / AttackWindupSeconds;
                if ((pendingTowerVolley || ranged && pendingTowerAttack) && pendingTower != null)
                    warning.position = pendingTower.Position + Vector3.up * .025f;
                float warningSize = (pendingTowerVolley ? 1.8f : ranged ? 1.1f : 1.4f) * (1f + normalized * .3f);
                warning.localScale = new Vector3(warningSize, .025f, warningSize);
                rightArm.localRotation = Quaternion.Euler(-105f * normalized, 0f, 0f);
                leftArm.localRotation = Quaternion.Euler((ranged ? -70f : -105f) * normalized, 0f, 0f);
                if (windup <= 0f)
                {
                    strikeRemaining = AttackStrikeSeconds;
                    warning.gameObject.SetActive(false);
                    if (pendingTowerVolley)
                    {
                        // Collider-free direct tower hit, not a dodgeable projectile.
                        // Killing, staggering or weakening this enemy is counterplay.
                        Vector3 delta = pendingTower != null ? pendingTower.Position - transform.position : Vector3.one * 100f;
                        delta.y = 0f;
                        if (pendingTower != null && !pendingTower.IsDestroyed && delta.sqrMagnitude <= 64f &&
                            owner.HasLineOfSight(AimPoint, pendingTower.AimPoint))
                        {
                            float before = pendingTower.Health;
                            pendingTower.TakeDamage(2f * baseMeleeDamage * CurrentDamageScale);
                            owner.HitEffect(pendingTower.AimPoint, new Color(1f, .65f, .18f), .6f);
                            if (pendingTower.Health < before) PlayEnemyCue(true);
                        }
                        attackCooldown = AttackRecoverySeconds;
                    }
                    else if (ranged)
                    {
                        Vector3 origin = AimPoint + transform.forward * .55f;
                        if (pendingTower != null)
                        {
                            // Towers are collider-free: a telegraphed, LOS-checked
                            // tower bolt resolves here, never through the core/player.
                            if (!pendingTower.IsDestroyed && owner.HasLineOfSight(origin, pendingTower.AimPoint))
                            {
                                float before = pendingTower.Health;
                                pendingTower.TakeDamage(7f * .6375f * CurrentDamageScale);
                                owner.HitEffect(pendingTower.AimPoint, new Color(.5f, .9f, .85f), .45f);
                                if (pendingTower.Health < before) PlayEnemyCue(true);
                            }
                        }
                        else if(owner.HasLineOfSight(origin,origin+aimedDirection*.6f,true))
                        {
                            if(owner.PeerArenaCombat)
                            {
                                bool authorized=pendingPeerAttack?FindPendingPeer(out _)&&session.Defense.AuthorizePeerAttack(
                                    TargetId,AttackPresentationSequence,pendingPeerId,origin,18.375f,true):
                                    session.Defense.AuthorizeLaneProjectile(TargetId,AttackPresentationSequence,origin,18.375f);
                                if(authorized&&session.Defense.TryGetEnemyLane(TargetId,out int lane))
                                    owner.LaunchShadeBolt(origin,aimedDirection,CurrentDamageScale,TargetId,AttackPresentationSequence,lane);
                            }
                            else owner.LaunchShadeBolt(origin,aimedDirection,CurrentDamageScale,TargetId,AttackPresentationSequence,0);
                        }
                        attackCooldown = AttackRecoverySeconds;
                    }
                    else
                    {
                        bool towerVictim = pendingTower != null;
                        bool peerValid=FindPendingPeer(out Vector3 pendingAim);
                        Vector3 impactTarget=towerVictim?pendingTower.AimPoint:pendingShrine&&shrineValid?shrine.AimPoint:
                            owner.PeerArenaCombat?pendingAim:playerPoint;
                        Vector3 flatImpact = impactTarget - AimPoint; flatImpact.y = 0f;
                        float impactDistance = IsFlying ? Vector3.Distance(AimPoint, impactTarget) : flatImpact.magnitude;
                        bool validVictim=towerVictim?!pendingTower.IsDestroyed:pendingShrine?shrineValid:owner.PeerArenaCombat?peerValid:
                            LocalPlayerTargetable&&IsAggroedOnPlayer&&(tutorialDuel||PlayerBlocksLane(6f,true));
                        if (validVictim && impactDistance < (pendingShrine || towerVictim ? 2f : 1.75f) &&
                            owner.HasLineOfSight(AimPoint, impactTarget, pendingShrine && !towerVictim))
                        {
                            float before=towerVictim?pendingTower.Health:pendingShrine?shrine.Health:owner.PeerArenaCombat?0f:session.HP;
                            if (towerVictim) pendingTower.TakeDamage(baseMeleeDamage * CurrentDamageScale);
                            else if (pendingShrine) shrine.TakeDamage(baseMeleeDamage * CurrentDamageScale);
                            else if(owner.PeerArenaCombat)
                            {
                                if(session.Defense.AuthorizePeerAttack(TargetId,AttackPresentationSequence,pendingPeerId,AimPoint,1.75f,false)&&
                                    session.Defense.ApplyPeerDamage(pendingPeerId,Mathf.Min(20f,baseMeleeDamage*CurrentDamageScale),TargetId,AttackPresentationSequence))PlayEnemyCue(true);
                            }
                            else session.TakeDamage(baseMeleeDamage*CurrentDamageScale);
                            if((!owner.PeerArenaCombat||towerVictim||pendingShrine)&&
                                (towerVictim?pendingTower.Health:pendingShrine?shrine.Health:session.HP)<before)PlayEnemyCue(true);
                        }
                        attackCooldown = AttackRecoverySeconds;
                    }
                }
                return;
            }
            rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.identity, dt * 10f);
            leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.identity, dt * 10f);
            float meleeReach = targetShrine ? 1.9f : 1.5f;
            if (visible && (!attackFan || slotCanStrike) && distance < (targetTower && defenseConfigured ? 8f : ranged ? 18.375f : meleeReach) && attackCooldown <= 0f)
            {
                pendingTowerVolley = targetTower && defenseConfigured && !ranged && distance >= 2f;
                windup = AttackWindupSeconds;
                AttackPresentationSequence = AttackPresentationSequence == int.MaxValue ? 1 : AttackPresentationSequence + 1;
                pendingShrine = targetShrine && !targetTower;
                pendingTower = targetTower ? blockingTower : null;
                pendingTowerAttack = targetTower;
                pendingPeerAttack=owner.PeerArenaCombat&&!targetShrine&&!targetTower&&peerTargetValid;
                pendingPeerId=peerTargetId;
                committedTowerId = targetTower ? blockingTower.Id : 0;
                committedAttackPoint = targetTower ? blockingTower.Position : targetPoint;
                aimedDirection = (targetPoint - (AimPoint + transform.forward * .55f)).normalized;
                warning.localPosition = new Vector3(0f, .025f, 0f);
                if ((pendingTowerVolley || ranged && targetTower) && blockingTower != null)
                    warning.position = blockingTower.Position + Vector3.up * .025f;
                warning.gameObject.SetActive(true);
                PlayEnemyCue(false);
                return;
            }
            // Off-lane tower volleys stop on the authored lane.
            if (targetTower && defenseConfigured && !attackFan) return;
            if (attackFan ? approachDistance > .01f : distance > (ranged ? 7f : targetShrine ? 1.65f : 1.35f))
            {
                Vector3 direction = attackFan ? approachOffset.normalized : flatOffset.normalized;
                float stride = Archetype == MonsterArchetype.Spider ? Mathf.Repeat(strideClock + TargetId * .17f, 2.4f) < .45f ? 1.35f : .95f : 1f;
                float speed = (95f / 32f) * movementScale * stride * (slowRemaining > 0f ? .5f : 1f);
                Vector3 move = direction * (attackFan ? Mathf.Min(speed * dt, approachDistance) : speed * dt);
                if (!owner.CanMove(this, move))
                {
                    Vector3 sideways = Vector3.Cross(Vector3.up, direction);
                    Vector3 left = (direction * .35f + sideways * .8f).normalized * move.magnitude;
                    Vector3 right = (direction * .35f - sideways * .8f).normalized * move.magnitude;
                    move = owner.CanMove(this, left) ? left : owner.CanMove(this, right) ? right : Vector3.zero;
                }
                if (move.sqrMagnitude > 0f)
                {
                    Vector3 soundBeforeMove = transform.position;
                    MoveWithSpacing(move, speed, dt);
                    RecordLocomotionSound(soundBeforeMove);
                    // GroundedGolemMotion owns visual gait and floor planting in LateUpdate.
                }
            }

        }

        public void ApplyElementEffect(int element, Vector3 direction, bool exposeWeakness = true, int statusMask = 0)
        {
            if (!Targetable || !owner.Playing) return;
            if (element == 1) slowRemaining = Mathf.Max(slowRemaining, PassiveStatusRules.WaterSlowSeconds(statusMask));
            if (element == 2 || element == 3)
            {
                ApplyStagger(element == 2 ? PassiveStatusRules.EarthStaggerSeconds(statusMask) : .12f, element == 2 && exposeWeakness);
                if (element == 3)
                {
                    direction.y = 0f;
                    Vector3 push = direction.normalized * .5f;
                    MoveWithSpacing(push, 10f, .05f);
                }
            }
        }

        void ApplyStagger(float duration, bool exposeWeakness)
        {
            staggerRemaining = Mathf.Max(staggerRemaining, duration);
            if (exposeWeakness) earthVulnerabilityRemaining = Mathf.Max(earthVulnerabilityRemaining, duration);
            windup = 0f;
            strikeRemaining = 0f;
            warning.gameObject.SetActive(false);
        }

        public void TakeHit(float damage)
        {
            if (!Targetable || damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage) || !owner.Playing) return;
            // Primary, allied-player, tower, splash and burn damage all enter here.
            // The hit that creates Earth stagger lands first; later hits gain 25%.
            float trainingLabBefore=TrainingLabBound?hp:0f;
            hp = Mathf.Max(0f, hp - damage * IncomingDamageMultiplier);
            if(TrainingLabBound)ObserveTrainingLabHit(trainingLabBefore);
            flash = .15f;
            healthFill.localScale = new Vector3(.76f * hp / maxHP, .045f, .026f);
            healthFill.localPosition = new Vector3(-.38f * (1f - hp / maxHP), 0f, -.025f);
            if (hp <= 0f)
            {
                SetPlayerAggro(false);
                solid.enabled = false;
                warning.gameObject.SetActive(false);
                healthBar.gameObject.SetActive(false);
                owner.GuardianDefeated(this);
            }
        }

        public void ConfigureDefenseWave(float healthMultiplier, float speedMultiplier, float damageMultiplier, bool? heavyOverride = null)
        {
            maxHP = baseHealth * Mathf.Clamp(healthMultiplier, .1f, 10f);
            hp = maxHP;
            defenseConfigured = true;
            bool heavy = !ranged && Archetype == MonsterArchetype.Stalker && (heavyOverride ?? maxHP >= 45f);
            configuredWindup = ranged ? .95f : heavy ? .85f : .65f;
            configuredRecovery = ranged ? 3.25f : heavy ? 1.15f : .85f;
            movementScale = Mathf.Clamp(speedMultiplier, .1f, 4f);
            damageScale = Mathf.Clamp(damageMultiplier, .05f, 5f);
            healthFill.localScale = new Vector3(.76f, .045f, .026f);
            healthFill.localPosition = new Vector3(0f, 0f, -.025f);
        }

        public void ApplyRemoteState(Vector3 position, float health, bool alive, float maximumHealth = -1f)
        {
            Vector3 before = transform.position;
            transform.position = position;
            if (maximumHealth > 0f) maxHP = maximumHealth;
            hp = alive ? Mathf.Clamp(health, .001f, maxHP) : 0f;
            dormant = false; seatedBlend = 0f;
            if (alive && !gameObject.activeSelf) gameObject.SetActive(true);
            solid.enabled = alive;
            warning.gameObject.SetActive(false);
            healthBar.gameObject.SetActive(alive);
            if (cachedCamera != null) healthBar.rotation = cachedCamera.transform.rotation;
            healthFill.localScale = new Vector3(.76f * hp / maxHP, .045f, .026f);
            healthFill.localPosition = new Vector3(-.38f * (1f - hp / maxHP), 0f, -.025f);
            if (alive && remoteMovementPrimed && owner != null && owner.RemotePresentation) RecordLocomotionSound(before);
            remoteMovementPrimed = alive;
            if (!alive) { ClearRemoteAttackPresentation(); SetPlayerAggro(false, false); gameObject.SetActive(false); }
        }

        internal void ClearRemoteAttackPresentation()
        {
            remoteAggroPrimed = remoteMovementPrimed = false;
            remoteTargetPoint = default;
            remoteAttackPhase = -1; remoteAttackProgress = remoteSnapshotProgress = remotePhaseElapsed = 0f;
            remoteWindupCueSequence = -1; remoteAttackLedger.Clear();
            if (warning != null) warning.gameObject.SetActive(false);
            SetPlayerAggro(false, false);
        }
        void OnDisable() { ClearRemoteAttackPresentation(); SetPlayerAggro(false, false); }

        void OnDestroy()
        {
            cosmeticParts?.Dispose(); cosmeticParts = null;
            if (!ownsAlertResources) return;
            ownsAlertResources = false;
            alertUsers = Mathf.Max(0, alertUsers - 1);
            if (alertUsers != 0) return;
            if (alertMesh != null) Destroy(alertMesh);
            if (alertMaterial != null) Destroy(alertMaterial);
            alertMesh = null; alertMaterial = null;
        }

        void UpdateFlash(float dt)
        {
            bool vulnerable = VulnerabilityRemaining > 0f;
            if (flash <= 0f && !vulnerable && !hadVulnerabilityTint) return;
            hadVulnerabilityTint = vulnerable;
            flash = Mathf.Max(0f, flash - dt);
            Color stone = vulnerable ? new Color(.45f, .46f, .20f) : appearanceStone;
            flashBlock.Clear();
            Color color = Color.Lerp(stone, new Color(.92f, 1f, .95f), flash / .15f);
            flashBlock.SetColor("_BaseColor", color);
            flashBlock.SetColor("_Color", color);
            foreach (Renderer renderer in stoneRenderers) renderer.SetPropertyBlock(flashBlock);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Existing metre/Y-up/XZ actor space, host Forge ledger and sole combat owners remain.
// Exact composed Refuge/interstage/balance adapter hunks; no new native/save authority.

// # ARCHITECTURE SNAPSHOT
// Bounded offline tutorial adapter; existing actor/HP/tower/native owners and reward boundaries remain.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing metre/Y-up/XZ routes, saved art local+Z with existing1.20 scale only.
// Array Formats: Four fixed numeric tower value slots; stable-ID four-theme palette; no new spatial/native storage.
// Dependencies/Pointers: Main-thread registry/LOS adapter is separate from pure math; V1 owns pivots/gait and composite V2 lease owns disposal; host HP/damage/projectile authority remains unchanged.
