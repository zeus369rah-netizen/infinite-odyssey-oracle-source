using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>Collider-free pedestal with fixed shared cosmetic slots. Only the defense authority deals damage.</summary>
    public sealed partial class PlacedElementShrine : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new List<Mesh>(3);
        private GameSession session;
        private Transform crystal;
        SavedTowerModelPresentation towerModel;
        internal SavedTowerModelPresentation WorkshopModel015=>towerModel;
        uint acceptedVisualShotSerial;
        float acceptedModelInterval=1.5f,lastVisualShotInterval=1.5f;
        private float fireRemaining, pulse, supportCueRemaining;
        private bool supportInside;
        private Material feedbackMaterial;
        private bool remoteOnly;
        private Renderer[] healthVisuals;
        private bool destructionShown;
        private readonly OracleEnemy[] secondaryTargets = new OracleEnemy[2];
        private static Mesh cubeMesh;
        public const int MaximumRegisteredTowers = 4; // Explicit005 current2 four-platform design.
        private static readonly PlacedElementShrine[] registered = new PlacedElementShrine[MaximumRegisteredTowers];
        private static FeedbackPool feedback;

        // The director relays these already-selected targets, never client damage.
        public static event Action<int, Vector3, Vector3, Vector3, int> AuthoritativeShot;

        // # ARCHITECTURE SNAPSHOT
        // Unscaled current002 tower silhouette: .95m square bottom, no collider.
        // Six registry entries are reused by logical swept actor clearance only.
        internal const float FootprintRadius = .671752f; // .95 * sqrt(2) / 2, rounded up.
        public int Id { get; private set; }
        public int Element { get; private set; }
        public Vector3 Position => transform.position;
        public Vector3 AimPoint => Position + Vector3.up * .9f;
        public bool IsSupport => Element == 9;
        public const float DefaultMaxHealth = CampaignBalance015.BaseTowerHealth;
        int acceptedPolicyTier015 = 1, acceptedPolicyWave015 = 1;
        float acceptedMaximumHealth015 = DefaultMaxHealth;
        public float MaxHealth => acceptedMaximumHealth015;
        public float Health { get; private set; } = DefaultMaxHealth;
        public bool IsDestroyed => Health <= 0f;

        internal void Initialize(GameSession gameSession, int id, int element, Material green, Material gold, Material elementMaterial,SavedTowerModelLibrary modelLibrary=null,int acceptedTier=1,int acceptedRevision=0,bool deferRegistration015=false)
        {
            session = gameSession;
            Id = id;
            Element = element;
            feedbackMaterial = elementMaterial;
            if(modelLibrary!=null&&TowerForgeDefinitions.ValidType(element))
            {
                towerModel=gameObject.AddComponent<SavedTowerModelPresentation>();
                towerModel.Initialize(modelLibrary,id,element,acceptedTier,acceptedRevision);
                acceptedModelInterval=lastVisualShotInterval=MatchTowerForgeRules.Interval(acceptedTier);
            }
            else crystal = CreateVisual(transform, green, gold, elementMaterial, meshes);
            healthVisuals = GetComponentsInChildren<Renderer>();
            acceptedPolicyTier015 = MatchTowerForgeRules.ValidTier(acceptedTier) ? acceptedTier : 1;
            acceptedPolicyWave015 = session != null && session.Defense != null && session.Defense.Running ? CurrentTowerPolicyWave015 : 1;
            acceptedMaximumHealth015 = TowerForgeDefinitions.ValidType(Element) ? CampaignBalance015.TowerMaximumHealth(acceptedPolicyTier015, acceptedPolicyWave015) : DefaultMaxHealth;
            Health = acceptedMaximumHealth015;
            if (TowerForgeDefinitions.ValidType(Element)) acceptedModelInterval = lastVisualShotInterval = CampaignBalance015.TowerShotInterval(MatchTowerForgeRules.Interval(acceptedPolicyTier015), acceptedPolicyWave015);
            fireRemaining = .25f + (id % 7) * .07f;
            if(!deferRegistration015){RegisterFeedback();RegisterAcousticTower015();}
        }

        private void OnEnable() { if (session != null) { RegisterFeedback(); RegisterAcousticTower015(); } }

        private void RegisterFeedback()
        {
            for (int i = 0; i < registered.Length; i++) if (registered[i] == this) return;
            for (int i = 0; i < registered.Length; i++)
                if (registered[i] == null || !registered[i].gameObject.activeInHierarchy) { registered[i] = this; break; }
            if (feedback == null || feedback.Root == null || feedback.Owner != session)
            {
                if (feedback != null) feedback.Dispose();
                feedback = new FeedbackPool(session, feedbackMaterial);
            }
        }

        public void SetRemote(bool remote) { remoteOnly = remote; }

        public void TakeDamage(float damage)
        {
            bool lab=TrainingLabAuthorized;
            if (remoteOnly || IsDestroyed || session == null || !lab&&(session.Defense == null ||
                !session.Defense.IsAuthority || !session.Defense.Running) || !session.SimulationActive ||
                float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f) return;
            if(!lab&&TowerForgeDefinitions.ValidType(Element))ApplyAcceptedTowerPolicy015(session.Defense.GetTowerTier(Id,Element),CurrentTowerPolicyWave015);
            float trainingLabBefore=Health;SetHealth(Mathf.Max(0f, Health - damage));
            if(lab)trainingLabOwner.ObserveTowerDamage(this,trainingLabBefore,Health);
        }

        public void ApplyRemoteHealth(float health)
        {
            if (session == null || session.Defense == null || session.Defense.IsAuthority ||
                float.IsNaN(health) || float.IsInfinity(health)) return;
            if(TowerForgeDefinitions.ValidType(Element))ApplyAcceptedTowerPolicy015(session.Defense.GetTowerTier(Id,Element),Mathf.Max(acceptedPolicyWave015,CurrentTowerPolicyWave015));
            SetHealth(Mathf.Clamp(health, 0f, MaxHealth));
        }

        int CurrentTowerPolicyWave015 => TutorialPracticeAuthorized || TrainingLabAuthorized || SharedTrainingLabReplica ? 1 :
            session != null && session.Defense != null ? Mathf.Clamp(session.Defense.WaveNumber, 1, CampaignBalance015.WaveCount) : 1;
        internal bool ApplyAcceptedTowerPolicy015(int tier, int wave)
        {
            if (!TowerForgeDefinitions.ValidType(Element) || !MatchTowerForgeRules.ValidTier(tier) || !CampaignBalance015.ValidWave(wave)) return false;
            if (tier == acceptedPolicyTier015 && wave == acceptedPolicyWave015) return true;
            if (!CampaignBalance015.TryAdvanceTowerHealth(acceptedPolicyTier015, acceptedPolicyWave015, acceptedMaximumHealth015, Health,
                tier, wave, out float maximum, out float health)) return false;
            acceptedPolicyTier015 = tier; acceptedPolicyWave015 = wave; acceptedMaximumHealth015 = maximum; Health = health;
            acceptedModelInterval = CampaignBalance015.TowerShotInterval(MatchTowerForgeRules.Interval(tier), wave);
            return true;
        }

        private void SetHealth(float health)
        {
            Health = health;
            if (!IsDestroyed || destructionShown) return;
            destructionShown = true;
            RemoveAcousticTower015();
            towerModel?.OnAcceptedDestroyed();
            if (session != null && session.Combat != null)
                session.Combat.HitEffect(AimPoint, ShrinePlacementBuilder.TowerColors[Element], .5f);
            session?.Audio?.PlayCue("enemy_impact", AimPoint);
            if (healthVisuals != null)
                for (int i = 0; i < healthVisuals.Length; i++) if (healthVisuals[i] != null) healthVisuals[i].enabled = false;
        }

        public static bool TryGetRegistered(int id, out PlacedElementShrine target)
        {
            for (int i = 0; i < registered.Length; i++)
                if (registered[i] != null && registered[i].Id == id)
                { target = registered[i]; return true; }
            target = null;
            return false;
        }

        // The endpoint may be the shrine or an aggro player behind a tower.
        // These logical bodies never become PhysX walls or pathfinding obstacles.
        public static bool TryFindBlockingTower(Vector3 enemyPosition, Vector3 corePosition, float range, out PlacedElementShrine target)
        { return TryFindBlockingTower(enemyPosition, corePosition, range, 0f, out target); }

        public static bool TryFindBlockingTower(Vector3 enemyPosition, Vector3 corePosition, float range,
            float enemyBodyRadius, out PlacedElementShrine target)
        {
            target = null;
            if (!Finite(enemyPosition) || !Finite(corePosition) || float.IsNaN(range) || float.IsInfinity(range) || range <= 0f) return false;
            Vector3 path = corePosition - enemyPosition;
            path.y = 0f;
            float length = path.magnitude;
            if (length < .1f) return false;
            Vector3 forward = path / length;
            float nearest = Mathf.Min(range, 16f);
            nearest *= nearest;
            for (int i = 0; i < registered.Length; i++)
            {
                PlacedElementShrine tower = registered[i];
                if (tower == null || tower.IsDestroyed || !tower.gameObject.activeInHierarchy || tower.session == null ||
                    tower.session.Defense == null || !tower.session.Defense.IsAuthority || !tower.session.Defense.Running) continue;
                Vector3 delta = tower.Position - enemyPosition;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                float along = Vector3.Dot(delta, forward);
                if (distance > nearest || along < -.15f || along > length ||
                    (delta - forward * along).sqrMagnitude > Mathf.Pow(Mathf.Max(1.1f, tower.MovementClearanceRadius(enemyPosition.y,enemyPosition.y+1.8f) + Mathf.Clamp(enemyBodyRadius, 0f, 2f)), 2f) ||
                    target != null && Mathf.Approximately(distance, nearest) && tower.Id >= target.Id) continue;
                target = tower;
                nearest = distance;
            }
            return target != null;
        }

        internal static Vector3 LimitMovementAroundTowers(Vector3 origin, Vector3 movement, float bodyRadius, float minimumVisualY)
        { return LimitMovementAroundTowers(origin, movement, bodyRadius, minimumVisualY, float.MaxValue); }

        internal static Vector3 LimitMovementAroundTowers(Vector3 origin, Vector3 movement, float bodyRadius, float minimumVisualY, float maximumVisualY)
        {
            for (int i = 0; i < registered.Length; i++)
            {
                PlacedElementShrine tower = registered[i];
                if (tower == null || tower.IsDestroyed || !tower.gameObject.activeInHierarchy) continue;
                movement = PresentationFootprint.LimitSweptXZ(origin, movement, tower.Position,
                    bodyRadius + tower.MovementClearanceRadius(minimumVisualY, maximumVisualY) + .02f);
            }
            return movement;
        }

        internal float MovementClearanceRadius(float minimumWorldY,float maximumWorldY)
        {
            if(towerModel!=null)return TowerModelClearanceProfiles.RadiusBetween(Element,towerModel.AcceptedTier,
                minimumWorldY-Position.y,maximumWorldY-Position.y);
            return ClearanceRadiusAtHeight(minimumWorldY);
        }

        float ClearanceRadiusAtHeight(float worldY)
        {
            // Upper focus including its bounded .12 scale pulse; the full base
            // remains conservative below .29m, and brass crown below .86m.
            float height = worldY - Position.y;
            return height > .86f ? .36f : height > .29f ? .581f : FootprintRadius;
        }

        // Side pads are not physical path walls. A lane-bound enemy can choose
        // a forward tower in its own lane strip, then stop for a direct volley.
        public static bool TryFindLaneThreatTower(Vector3 enemyPosition, Vector3 laneOrigin, Vector3 corePosition,
            float range, out PlacedElementShrine target)
        {
            target = null;
            if (!Finite(enemyPosition) || !Finite(laneOrigin) || !Finite(corePosition) ||
                float.IsNaN(range) || float.IsInfinity(range) || range <= 0f) return false;
            Vector3 path = corePosition - laneOrigin; path.y = 0f;
            float length = path.magnitude;
            if (length < .1f) return false;
            Vector3 forward = path / length;
            float nearest = Mathf.Min(range, 8f); nearest *= nearest;
            for (int i = 0; i < registered.Length; i++)
            {
                PlacedElementShrine tower = registered[i];
                if (tower == null || tower.IsDestroyed || !tower.gameObject.activeInHierarchy || tower.session == null ||
                    tower.session.Defense == null || !tower.session.Defense.IsAuthority || !tower.session.Defense.Running) continue;
                Vector3 delta = tower.Position - enemyPosition; delta.y = 0f;
                Vector3 fromOrigin = tower.Position - laneOrigin; fromOrigin.y = 0f;
                float along = Vector3.Dot(fromOrigin, forward), distance = delta.sqrMagnitude;
                if (distance > nearest || Vector3.Dot(delta, forward) < -.15f || along < 0f || along > length ||
                    (fromOrigin - forward * along).sqrMagnitude > 7f * 7f ||
                    !tower.session.Combat.HasLineOfSight(enemyPosition + Vector3.up * 1.2f, tower.AimPoint) ||
                    target != null && Mathf.Approximately(distance, nearest) && tower.Id >= target.Id) continue;
                target = tower; nearest = distance;
            }
            return target != null;
        }

        private void Update()
        {
            bool lab=TrainingLabAuthorized;
            bool training=TutorialPracticeAuthorized||lab;
            bool running = training || session != null && session.SimulationActive && (SharedTrainingLabReplica || session.Defense != null && session.Defense.Running);
            if (feedback != null) feedback.Tick(running ? Time.deltaTime : 0f, running);
            if (!running || IsDestroyed) return;
            float dt = Time.deltaTime;
            if(towerModel!=null)
            {
                if(!remoteOnly&&(training||session.Defense.IsAuthority))towerModel.ApplyAcceptedCooldown(fireRemaining,lastVisualShotInterval);
                bool near=session.Player!=null&&(session.Player.transform.position-Position).sqrMagnitude<=900f;
                towerModel.Tick(dt,true,near);
            }
            pulse = Mathf.Max(0f, pulse - dt * 4f);
            if (crystal != null)
            {
                crystal.localRotation *= Quaternion.Euler(0f, dt * 22f, 0f);
                float supportPulse = IsSupport ? .035f * (1f + Mathf.Sin(Time.time * 1.8f + Id)) : 0f;
                crystal.localScale = Vector3.one * (1f + pulse * .12f + supportPulse);
            }
            // Support is a local enter-aura cue; its authoritative multiplier is
            // still owned by the director. It never enters the attack path.
            if (IsSupport)
            {
                supportCueRemaining = Mathf.Max(0f, supportCueRemaining - dt);
                bool inside = session.Player != null && (session.Player.transform.position - Position).sqrMagnitude <= 64f;
                if (inside && !supportInside && supportCueRemaining <= 0f)
                {
                    supportCueRemaining = 4f;
                    pulse = 1f;
                    if (session.Audio != null) session.Audio.PlaySupport();
                    feedback?.Play(Element, Position + Vector3.up * .08f, Position, Position, Position, 0, feedbackMaterial);
                }
                supportInside = inside;
                return;
            }
            if (remoteOnly || !training&&!session.Defense.IsAuthority || !training&&!session.CombatUnlocked || session.Combat == null) return;
            if(lab&&!trainingLabOwner.TowerAttacking)return;
            fireRemaining = Mathf.Max(0f, fireRemaining - dt);
            if (fireRemaining > 0f) return;
            Vector3 origin = transform.position + Vector3.up * 1.25f;
            OracleEnemy target;
            if (!(training?(lab?TryGetTrainingLabTarget(origin,out target):TryGetTutorialPracticeTarget(origin,out target)):TryFindEligibleTarget(origin,out target)))
            { fireRemaining = .2f; return; }
            int acceptedTier=training?1:Element<4?session.Defense.GetTowerTier(Id,Element):1;
            float baseInterval015=Element<4?MatchTowerForgeRules.Interval(acceptedTier):Element==8?1.8f:1.5f;
            fireRemaining=!training&&Element<4?CampaignBalance015.TowerShotInterval(baseInterval015,CurrentTowerPolicyWave015):baseInterval015;
            Vector3 hitPoint = target.AimPoint;
            Color color = ShrinePlacementBuilder.TowerColors[Element];
            session.Combat.HitEffect(origin, color, .28f);
            session.Combat.HitEffect(hitPoint, color, .5f);
            // Additional hits are selected before the primary can die or move.
            int secondaryCount = Element == 4 || Element == 5
                ? FindSecondaryTargets(origin, target, hitPoint) : 0;
            Vector3 secondaryA = secondaryCount > 0 ? secondaryTargets[0].AimPoint : hitPoint;
            Vector3 secondaryB = secondaryCount > 1 ? secondaryTargets[1].AimPoint : secondaryA;
            ShowShotFeedback(hitPoint, secondaryA, secondaryB, secondaryCount);
            if(!training)AuthoritativeShot?.Invoke(Id, hitPoint, secondaryA, secondaryB, secondaryCount);
            float practiceBefore=training?target.HP:0f;
            target.TakeHit(Element<4?(training?MatchTowerForgeRules.Damage(1):session.Defense.GetAcceptedTowerDamage015(Id,Element)):Element==8?12f:8f,hitPoint-origin,Element>=0&&Element<4?1<<Element:0,hitPoint);
            if(training){if(lab)ObserveTrainingLabTowerShot(target,practiceBefore);else ObserveTutorialPracticeDamage(target,practiceBefore);}
            if (target.Alive)
            {
                if (Element == 0) target.ApplyBurn(2f, 3f);
                else if (Element == 7) target.ApplyBurn(4f, 3f);
                else if (Element == 1 || Element == 6) target.ApplyElementEffect(1, hitPoint - origin);
                else if (Element == 2) target.ApplyElementEffect(2, hitPoint - origin);
                else if (Element == 3) target.ApplyElementEffect(3, hitPoint - origin);
                if (Element == 6) target.ApplyElementEffect(2, hitPoint - origin, false);
            }
            for (int i = 0; i < secondaryCount; i++)
            {
                OracleEnemy secondary = secondaryTargets[i];
                secondaryTargets[i] = null;
                if (secondary == null || !secondary.Targetable) continue;
                session.Combat.HitEffect(secondary.AimPoint, color, .4f);
                secondary.TakeHit(Element == 4 ? 5f : 6f,secondary.AimPoint-origin,0,secondary.AimPoint);
            }
        }

        private void ShowShotFeedback(Vector3 primary, Vector3 secondaryA, Vector3 secondaryB, int count)
        {
            pulse = 1f;
            if(towerModel!=null)
            {
                lastVisualShotInterval=remoteOnly||session!=null&&session.Defense!=null&&!session.Defense.IsAuthority?acceptedModelInterval:Mathf.Clamp(fireRemaining,.05f,10f);
                towerModel.OnAcceptedShot(++acceptedVisualShotSerial,primary,lastVisualShotInterval);
            }
            Vector3 visualOrigin=towerModel!=null?towerModel.VisualMuzzle:Position+Vector3.up*1.25f;
            // Canonical cue0..7 matches the eight elemental tower types;
            // Crossbow reuses Earth until its own mechanical cue is supplied.
            if(session.Audio!=null)
            {
                if(towerModel!=null&&Element==2)session.Audio.PlayCrossbow(visualOrigin);
                else session.Audio.PlayTower(Element==8?2:Element,visualOrigin);
            }
            if(Element>=4||towerModel==null||!towerModel.ElementPresentationReady)
                feedback?.Play(Element, visualOrigin, primary, secondaryA, secondaryB,
                    Mathf.Clamp(count, 0, 2), feedbackMaterial);
        }

        public bool ApplyAcceptedModelTier(int tier,int revision,float acceptedInterval)
        {
            if(towerModel==null||float.IsNaN(acceptedInterval)||float.IsInfinity(acceptedInterval)||acceptedInterval<.05f||acceptedInterval>10f)return false;
            if(!towerModel.ApplyAcceptedTier(tier,revision))return false;
            if(!ApplyAcceptedTowerPolicy015(tier,Mathf.Max(acceptedPolicyWave015,CurrentTowerPolicyWave015)))return false;
            acceptedModelInterval=CampaignBalance015.TowerShotInterval(acceptedInterval,acceptedPolicyWave015);RegisterAcousticTower015();return true;
        }

        public static void PlayRemoteShotFeedback(int towerId, Vector3 primary, Vector3 secondaryA, Vector3 secondaryB, int secondaryCount)
        {
            if (!Finite(primary) || !Finite(secondaryA) || !Finite(secondaryB) || secondaryCount < 0 || secondaryCount > 2) return;
            for (int i = 0; i < registered.Length; i++)
            {
                PlacedElementShrine shrine = registered[i];
                if (shrine == null || shrine.IsDestroyed || shrine.Id != towerId || !shrine.gameObject.activeInHierarchy || shrine.IsSupport ||
                    shrine.session == null || shrine.session.Defense == null || !shrine.session.Defense.Running ||
                    !shrine.session.SimulationActive || shrine.session.Defense.IsAuthority) continue;
                Vector3 origin = shrine.Position + Vector3.up * 1.25f;
                if ((primary - origin).sqrMagnitude > 85f || (secondaryA - origin).sqrMagnitude > 85f ||
                    (secondaryB - origin).sqrMagnitude > 85f) return;
                shrine.ShowShotFeedback(primary, secondaryA, secondaryB, secondaryCount);
                return;
            }
        }

        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private bool Eligible(OracleEnemy enemy)
        {
            if (enemy == null || !enemy.Targetable) return false;
            if (TowerForgeDefinitions.ValidType(Element)) return CampaignBalance015.TowerTargetGroupAllows(Element,enemy.IsFlying);
            return (Element != 7 && Element != 8) || !enemy.IsFlying;
        }

        private int FindSecondaryTargets(Vector3 origin, OracleEnemy primary, Vector3 primaryPoint)
        {
            int count = 0;
            Vector3 previous = primaryPoint;
            Vector3 direction = (primaryPoint - origin).normalized;
            float primaryDistance = Vector3.Distance(origin, primaryPoint);
            IReadOnlyList<OracleEnemy> enemies = session.Combat.ActiveEnemies;
            for (int slot = 0; slot < secondaryTargets.Length; slot++)
            {
                OracleEnemy selected = null;
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < enemies.Count; i++)
                {
                    OracleEnemy enemy = enemies[i];
                    if (!Eligible(enemy) || enemy == primary || (slot > 0 && enemy == secondaryTargets[0])) continue;
                    Vector3 aim = enemy.AimPoint;
                    if ((aim - origin).sqrMagnitude > 81f) continue;
                    float distance;
                    if (Element == 4)
                    {
                        distance = (aim - previous).sqrMagnitude;
                        if (distance > 3.5f * 3.5f || !session.Combat.HasLineOfSight(previous, aim)) continue;
                    }
                    else
                    {
                        Vector3 delta = aim - origin;
                        float along = Vector3.Dot(delta, direction);
                        if (along <= primaryDistance + .1f || (delta - direction * along).sqrMagnitude > .75f * .75f ||
                            !session.Combat.HasLineOfSight(origin, aim)) continue;
                        distance = along;
                    }
                    if (distance > nearest || (selected != null && distance == nearest && enemy.TargetId >= selected.TargetId)) continue;
                    nearest = distance;
                    selected = enemy;
                }
                if (selected == null) break;
                secondaryTargets[count++] = selected;
                previous = selected.AimPoint;
            }
            return count;
        }

        private bool TryFindEligibleTarget(Vector3 origin, out OracleEnemy target)
        {
            target = null;
            float nearestSquared = 9f * 9f;
            IReadOnlyList<OracleEnemy> enemies = session.Combat.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                OracleEnemy enemy = enemies[i];
                if (!Eligible(enemy)) continue;
                Vector3 aim = enemy.transform.position + Vector3.up * 1.2f;
                float squared = (aim - origin).sqrMagnitude;
                if (squared > nearestSquared || (target != null && squared == nearestSquared && enemy.TargetId >= target.TargetId) ||
                    !session.Combat.HasLineOfSight(origin, aim)) continue;
                target = enemy;
                nearestSquared = squared;
            }
            return target != null;
        }

        internal static Transform CreateVisual(Transform parent, Material green, Material gold, Material crystal, List<Mesh> resources)
        {
            EnsureCubeMesh();
            BuildBatch(parent, "Emerald pedestal", green, new[]
            {
                Part(new Vector3(0,.1f,0), new Vector3(.95f,.2f,.95f)),
                Part(new Vector3(0,.4f,0), new Vector3(.48f,.4f,.48f)),
                Part(new Vector3(0,.72f,0), new Vector3(.72f,.18f,.72f)),
                Part(new Vector3(-.32f,.22f,-.32f), new Vector3(.18f,.14f,.18f)),
                Part(new Vector3(.32f,.22f,-.32f), new Vector3(.18f,.14f,.18f)),
                Part(new Vector3(-.32f,.22f,.32f), new Vector3(.18f,.14f,.18f)),
                Part(new Vector3(.32f,.22f,.32f), new Vector3(.18f,.14f,.18f))
            }, resources);
            BuildBatch(parent, "Brass shrine bands and focus", gold, new[]
            {
                Part(new Vector3(0,.28f,0), new Vector3(.53f,.055f,.53f)),
                Part(new Vector3(0,.59f,0), new Vector3(.53f,.055f,.53f)),
                Part(new Vector3(0,.83f,0), new Vector3(.8f,.055f,.8f)),
                Part(new Vector3(0,.89f,0), new Vector3(.31f,.07f,.31f), Quaternion.Euler(0,45,0))
            }, resources);
            return BuildBatch(parent, "Element focus crystal", crystal, new[]
            {
                Part(new Vector3(0,1.15f,0), new Vector3(.26f,.4f,.26f), Quaternion.Euler(12,45,15))
            }, resources);
        }

        private static CombineInstance Part(Vector3 position, Vector3 size)
        {
            return Part(position, size, Quaternion.identity);
        }

        private static CombineInstance Part(Vector3 position, Vector3 size, Quaternion rotation)
        {
            return new CombineInstance { mesh = cubeMesh, transform = Matrix4x4.TRS(position, rotation, size) };
        }

        private static Transform BuildBatch(Transform parent, string name, Material material, CombineInstance[] pieces, List<Mesh> resources)
        {
            Mesh mesh = new Mesh { name = name + " combined geometry" };
            mesh.CombineMeshes(pieces, true, true, false);
            mesh.RecalculateBounds();
            resources.Add(mesh);
            GameObject batch = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            batch.transform.SetParent(parent, false);
            batch.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = batch.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return batch.transform;
        }

        private static void EnsureCubeMesh()
        {
            if (cubeMesh != null) return;
            GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.SetActive(false);
            cubeMesh = source.GetComponent<MeshFilter>().sharedMesh;
            Destroy(source);
        }

        // Twelve slots shared by all six towers. Every object/renderer is made
        // once at initialization; attacks only reuse transforms and materials.
        private sealed class FeedbackPool
        {
            private sealed class Slot
            {
                public Transform Root;
                public LineRenderer Line;
                public readonly Transform[] Bits = new Transform[6];
                public readonly MeshRenderer[] Renderers = new MeshRenderer[6];
                public Vector3 Origin, Primary, SecondaryA, SecondaryB;
                public int Type, Count;
                public float Age, Duration;
            }

            public readonly GameSession Owner;
            public readonly Transform Root;
            private readonly Slot[] slots = new Slot[12];
            private int cursor, updatedFrame = -1;

            public FeedbackPool(GameSession owner, Material material)
            {
                Owner = owner;
                Root = new GameObject("Shared bounded tower feedback").transform;
                Root.SetParent(owner.transform, false);
                for (int i = 0; i < slots.Length; i++)
                {
                    Slot slot = slots[i] = new Slot();
                    slot.Root = new GameObject("Pooled tower cue " + i).transform;
                    slot.Root.SetParent(Root, false);
                    slot.Line = slot.Root.gameObject.AddComponent<LineRenderer>();
                    slot.Line.useWorldSpace = true;
                    slot.Line.sharedMaterial = material;
                    slot.Line.positionCount = 16;
                    slot.Line.startWidth = slot.Line.endWidth = 1f;
                    slot.Line.numCapVertices = slot.Line.numCornerVertices = 0;
                    slot.Line.shadowCastingMode = ShadowCastingMode.Off;
                    slot.Line.receiveShadows = false;
                    for (int j = 0; j < slot.Bits.Length; j++)
                    {
                        var bit = new GameObject("Shared mesh cue bit", typeof(MeshFilter), typeof(MeshRenderer));
                        bit.transform.SetParent(slot.Root, false);
                        bit.GetComponent<MeshFilter>().sharedMesh = cubeMesh;
                        MeshRenderer renderer = bit.GetComponent<MeshRenderer>();
                        renderer.sharedMaterial = material;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                        slot.Bits[j] = bit.transform;
                        slot.Renderers[j] = renderer;
                    }
                    slot.Root.gameObject.SetActive(false);
                }
            }

            public void Play(int type, Vector3 origin, Vector3 primary, Vector3 secondaryA,
                Vector3 secondaryB, int count, Material material)
            {
                Slot slot = slots[cursor];
                cursor = (cursor + 1) % slots.Length;
                slot.Type = type; slot.Origin = origin; slot.Primary = primary;
                slot.SecondaryA = secondaryA; slot.SecondaryB = secondaryB; slot.Count = count;
                slot.Age = 0f;
                slot.Duration = type == 9 ? .8f : type == 0 || type == 7 ? .5f : .3f;
                slot.Line.sharedMaterial = material;
                for (int i = 0; i < slot.Renderers.Length; i++) slot.Renderers[i].sharedMaterial = material;
                slot.Root.gameObject.SetActive(true);
                Draw(slot);
            }

            public void Tick(float dt, bool running)
            {
                if (Root == null || updatedFrame == Time.frameCount) return;
                updatedFrame = Time.frameCount;
                for (int i = 0; i < slots.Length; i++)
                {
                    Slot slot = slots[i];
                    if (!slot.Root.gameObject.activeSelf) continue;
                    slot.Age += dt;
                    if (!running || slot.Age >= slot.Duration) slot.Root.gameObject.SetActive(false);
                    else Draw(slot);
                }
            }

            private static Vector3 Target(Slot slot, int index) => index == 0 ? slot.Primary : index == 1 ? slot.SecondaryA : slot.SecondaryB;

            private static void Draw(Slot slot)
            {
                float t = slot.Age / slot.Duration, fade = 1f - t;
                Vector3 direction = slot.Primary - slot.Origin;
                if (direction.sqrMagnitude < .001f) direction = Vector3.forward;
                direction.Normalize();
                Vector3 side = Vector3.Cross(direction, Vector3.up);
                if (side.sqrMagnitude < .001f) side = Vector3.right;
                side.Normalize();
                LineRenderer line = slot.Line;
                line.enabled = t < .65f || slot.Type == 9;
                line.widthMultiplier = (slot.Type == 2 || slot.Type == 7 ? .065f : .028f) * fade;
                if (slot.Type == 4)
                {
                    // Exactly the selected chain: origin, primary, then0..2 links.
                    line.positionCount = 1 + (slot.Count + 1) * 3;
                    line.SetPosition(0, slot.Origin);
                    Vector3 previous = slot.Origin;
                    for (int link = 0; link <= slot.Count; link++)
                    {
                        Vector3 end = Target(slot, link);
                        line.SetPosition(1 + link * 3, Vector3.Lerp(previous, end, .33f) + side * .13f * fade);
                        line.SetPosition(2 + link * 3, Vector3.Lerp(previous, end, .67f) - side * .13f * fade);
                        line.SetPosition(3 + link * 3, end);
                        previous = end;
                    }
                }
                else if (slot.Type == 5 || slot.Type == 8)
                {
                    line.positionCount = 2;
                    line.SetPosition(0, slot.Origin);
                    line.SetPosition(1, Target(slot, slot.Count));
                }
                else if (slot.Type == 9)
                {
                    line.positionCount = 16;
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI * 2f / 15f;
                        line.SetPosition(i, slot.Origin + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (.7f + t * .6f));
                    }
                }
                else
                {
                    line.positionCount = 6;
                    for (int i = 0; i < 6; i++)
                    {
                        float u = i / 5f;
                        float bend = Mathf.Sin(u * Mathf.PI) * (slot.Type == 1 ? .35f : slot.Type == 3 ? .6f : .12f);
                        Vector3 offset = slot.Type == 1 || slot.Type == 3 ? side * bend : Vector3.up * bend;
                        line.SetPosition(i, Vector3.Lerp(slot.Origin, slot.Primary, u) + offset * fade);
                    }
                }
                for (int i = 0; i < slot.Bits.Length; i++)
                {
                    Transform bit = slot.Bits[i];
                    float a = i * Mathf.PI / 3f;
                    Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 position = slot.Primary;
                    Vector3 size = Vector3.one * .08f;
                    Quaternion rotation = Quaternion.Euler(35f, i * 60f, 45f);
                    if (slot.Type == 0 || slot.Type == 7)
                    {
                        if (slot.Type == 7) position.y = .12f;
                        position += radial * (.15f + t * .3f) + Vector3.up * (t * (slot.Type == 0 ? .8f : .35f) + i % 2 * .08f);
                        size = slot.Type == 0 ? new Vector3(.045f, .13f, .045f) : new Vector3(.16f, .08f, .16f);
                    }
                    else if (slot.Type == 2)
                    {
                        position.y = .12f + .2f * fade;
                        position += radial * .38f;
                        size = new Vector3(.055f, .45f * fade, .055f);
                        rotation = Quaternion.Euler(radial.z * 28f, 0f, -radial.x * 28f);
                    }
                    else if (slot.Type == 4 || slot.Type == 5)
                    {
                        position = Target(slot, i % (slot.Count + 1)) + radial * (.06f + t * .16f);
                        size = slot.Type == 4 ? new Vector3(.04f, .15f, .04f) : new Vector3(.075f, .2f, .075f);
                    }
                    else if (slot.Type == 6)
                    {
                        position += radial * .4f + Vector3.up * Mathf.Sin(a) * .35f;
                        size = new Vector3(.07f, .3f, .07f);
                    }
                    else if (slot.Type == 8)
                    {
                        position = Vector3.Lerp(slot.Origin, slot.Primary, Mathf.Clamp01(t * 4f)) - direction * (i * .06f);
                        size = new Vector3(.025f, .025f, .16f);
                        rotation = Quaternion.LookRotation(direction);
                    }
                    else
                    {
                        position += radial * (.35f + t * .35f) + Vector3.up * Mathf.Sin(a + t * 4f) * .12f;
                        size = slot.Type == 3 ? new Vector3(.28f, .025f, .05f) : new Vector3(.09f, .04f, .14f);
                        rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, slot.Type == 3 ? 15f : 0f);
                    }
                    bit.position = position;
                    bit.rotation = rotation;
                    bit.localScale = size * fade;
                }
            }

            public void Dispose() { if (Root != null) Destroy(Root.gameObject); }
        }

        private void OnDisable()
        {
            RemoveAcousticTower015();
            for (int i = 0; i < registered.Length; i++) if (registered[i] == this) registered[i] = null;
            for (int i = 0; i < registered.Length; i++) if (registered[i] != null) return;
            if (feedback != null) feedback.Dispose();
            feedback = null;
        }

        private void OnDestroy()
        {
            RemoveAcousticTower015();
            for (int i = 0; i < meshes.Count; i++) if (meshes[i] != null) Destroy(meshes[i]);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Bounded offline tutorial adapter; existing actor/HP/tower/native owners and reward boundaries remain.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge world metres/Y-up; host-owned shared lab, model local+Z.
// Array Formats: Two native peer records and bounded fixed frames; inherited48 projectile pool and two real actors.
// Dependencies/Pointers: Dedicated shared owner validates requests/fences; guests remote-only; no rewards, migration or narrator; root integrates complete slice.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing tower Head-local/Y-up pivots and accepted target world points remain canonical; foundation/pads/target lock/range unchanged.
// Array Formats: Existing saved72 meshes/four materials remain borrowed; additive12 original FX templates, two renderers and one fixed96-byte native state/pose pair per instance.
// Dependencies/Pointers: Existing accepted tier/shot owners feed cosmetics only; Burst fences finish before Object writes. No authority/economy/radius/status/audio/network rewrite; native acceptance pending.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space:Unity World listener/emitter metres; existing settings use local uGUI.
// Array Formats:Existing pools remain fixed;8voice leases/2queries/16hits per ray; audio-local Morton refresh keys.
// Dependencies/Pointers:Audio owns native reader fences/low-pass/backend leases; accepted World/tower lifetime hooks invalidate acoustic revisions. No narration/gameplay authority replaced.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world and accepted015 campaign/source identity, original arena positions retained.
// Array Formats: Eight milestones/40waves/905logical rewards within4096native slots, fixed128B XP channel and existing bounded pools.
// Dependencies/Pointers: Root composes actual current pool/audio owners with reviewed campaign/XP hunks; source/native/browser/performance/public acceptance remain separate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted world metres/Y-up/XZ, exact1m terrain/.25m build roots and locked active-road context.
// Array Formats: Bounded native semantic/preview/body stores, exact64B versioned command and existing2peer/4tower snapshots.
// Dependencies/Pointers: Root integrates exact guarded slices; host economy/readiness/LOS and World geometry remain canonical, prepared/source compile is not native or browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted global waves1..40, tiers1..3 and existing Y-up/XZ tower roots; exact bound lesson/lab adapters remain separate.
// Array Formats: Existing fixed4 towers and unchanged host snapshots plus cached scalar policy tier/wave/maximum; no ABI, save, cost, damage, enemy-aggression or hot allocation change.
// Dependencies/Pointers: CampaignBalance015 owns target groups/capacity/additive4percent rate and durability math. Host alone accepts context; monotonic policy rebases preserve damaged fraction, repeated contexts are inert, and dead towers stay dead.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing authoritative legacy Combat/World/revision and actor pool lifetime, world metres/Y-up.
// Array Formats: Existing48 actors with bounded feedback values/Morton jobs and one synchronous accepted-damage observation; no extra damage, mesh or material owner.
// Dependencies/Pointers: Positive actual HP loss owns immediate cached MPB red flash and observer dispatch. Root composes this exact work projection and verifies native collision/flash/audio/network/GC; replicas never move authoritatively.
