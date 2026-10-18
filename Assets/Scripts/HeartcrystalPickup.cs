using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>Visual for a bounded solo reward pool; ElementProgression owns collection and saving.</summary>
    public sealed class HeartcrystalPickup : MonoBehaviour
    {
        private Transform visual;
        private enum Motion { Settling, Idle, Attracting, AwaitingSave }
        internal const float AttractionRange = PickupReachRules.BaseRangeMetres, ArrivalRange = .25f, MaximumSpeed = 3f;
        private Vector3 fallStart, resting;
        private float age, settlingAge, flightAge, speed, arcScale;
        private Motion motion;
        private bool motionAllowed;
        internal bool Active { get; private set; }
        internal bool Settled => motion != Motion.Settling;
        internal bool IsMagnetized => motion == Motion.Attracting || motion == Motion.AwaitingSave;
        internal bool Arrived => motion == Motion.AwaitingSave;
        internal string RewardToken { get; private set; }
        internal bool LastConsumptionAwarded { get; private set; }
        internal Vector3 WorldPosition => visual != null ? visual.position : transform.position;

        internal void Initialize(Mesh sharedMesh, Material sharedMaterial)
        {
            var model = new GameObject("Small faceted pink Heartstone", typeof(MeshFilter), typeof(MeshRenderer));
            visual = model.transform;
            visual.SetParent(transform, false);
            model.GetComponent<MeshFilter>().sharedMesh = sharedMesh;
            MeshRenderer renderer = model.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Deactivate();
        }

        internal void Activate(Vector3 start, Vector3 destination, string rewardToken = null)
        {
            RewardToken = rewardToken;
            LastConsumptionAwarded = false;
            fallStart = start + Vector3.right * .24f;
            resting = destination + Vector3.right * .24f;
            transform.position = fallStart;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            age = settlingAge = flightAge = speed = 0f;
            arcScale = 1f;
            motion = Motion.Settling;
            motionAllowed = true;
            Active = true;
            gameObject.SetActive(true);
        }

        internal void Tick(float delta, bool canMove = true)
        {
            if (!Active) return;
            motionAllowed = canMove;
            if (!canMove || float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0f) return;
            age += delta;
            if (motion == Motion.Settling)
            {
                settlingAge += delta;
                float fall = Mathf.Clamp01(settlingAge / .45f);
                transform.position = Vector3.Lerp(fallStart, resting, fall * fall);
                if (settlingAge >= .45f) motion = Motion.Idle;
            }
            visual.localPosition = IsMagnetized ? Vector3.zero : Vector3.up * (Mathf.Sin(age * 2.4f) * .025f);
            visual.localRotation = Quaternion.Euler(5f, age * 34f, 0f);
            visual.localScale = Vector3.one * (1f + Mathf.Sin(age * 2.4f) * .025f);
        }

        internal bool BeginAttraction()
        {
            if (!Active || !motionAllowed || motion != Motion.Idle) return false;
            transform.position = WorldPosition;
            visual.localPosition = Vector3.zero;
            motion = Motion.Attracting;
            speed = .2f;
            flightAge = 0f;
            return true;
        }

        internal Vector3 ProposeMagnetStep(Vector3 target, float delta)
        {
            if (!Active || !motionAllowed || !IsMagnetized || delta <= 0f || float.IsNaN(delta) || float.IsInfinity(delta)) return transform.position;
            Vector3 offset = target - transform.position;
            float distance = offset.magnitude;
            if (distance <= ArrivalRange) return transform.position;
            motion = Motion.Attracting;
            flightAge += delta;
            speed = Mathf.MoveTowards(speed, MaximumSpeed, 6f * delta);
            float arc = Mathf.Clamp01((distance - ArrivalRange) / 1.5f) * arcScale;
            Vector3 sideways = Vector3.Cross(Vector3.up, offset.normalized);
            Vector3 waypoint = target + Vector3.up * (Mathf.Sin(flightAge * 3f) * .24f * arc) +
                sideways * (Mathf.Sin(flightAge * 4f) * .15f * arc);
            return Vector3.MoveTowards(transform.position, waypoint, speed * delta);
        }

        internal void ApplyMagnetStep(Vector3 position, Vector3 target)
        {
            if (!Active || !motionAllowed || !IsMagnetized) return;
            transform.position = position;
            visual.localPosition = Vector3.zero;
            motion = (position - target).sqrMagnitude <= ArrivalRange * ArrivalRange ? Motion.AwaitingSave : Motion.Attracting;
        }

        internal void InterruptAttraction(Vector3 groundRest)
        {
            if (!Active || !IsMagnetized) return;
            fallStart = WorldPosition;
            transform.position = fallStart;
            visual.localPosition = Vector3.zero;
            resting = groundRest;
            settlingAge = speed = flightAge = 0f;
            arcScale *= .5f; // Repeated corner obstructions progressively straighten the next attempt.
            motion = Motion.Settling;
        }

        internal bool TryConsumeAt(Vector3 target, bool canCollect, System.Func<bool> acceptSavedReward)
        {
            if (!Active || !motionAllowed || !canCollect || !Arrived || acceptSavedReward == null ||
                (WorldPosition - target).sqrMagnitude > ArrivalRange * ArrivalRange) return false;
            if (!ElementProgression.TryCommitTutorialReward(RewardToken, true, acceptSavedReward, out bool awarded)) return false;
            LastConsumptionAwarded = awarded;
            Deactivate();
            return true;
        }

        internal static bool IsTravelClear(Vector3 from, Vector3 to, RaycastHit[] hits, Collider[] overlaps)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude > .000001f)
            {
                int count = Physics.SphereCastNonAlloc(from, .08f, delta.normalized, hits, delta.magnitude,
                    ~(1 << 2), QueryTriggerInteraction.Ignore);
                if (count == hits.Length) return false;
                for (int i = 0; i < count; i++) if (BlocksTravel(hits[i].collider)) return false;
            }
            int overlapCount = Physics.OverlapSphereNonAlloc(to, .08f, overlaps, ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (overlapCount == overlaps.Length) return false;
            for (int i = 0; i < overlapCount; i++) if (BlocksTravel(overlaps[i])) return false;
            return true;
        }

        private static bool BlocksTravel(Collider hit) => hit != null && hit.GetComponentInParent<OraclePlayer>() == null &&
            hit.GetComponentInParent<RemoteOracleAvatar>() == null && hit.GetComponentInParent<OracleEnemy>() == null &&
            hit.GetComponentInParent<OracleProjectile>() == null;

        internal void Deactivate()
        {
            Active = false;
            RewardToken = null;
            motionAllowed = false;
            gameObject.SetActive(false);
        }

        internal static Mesh CreateSharedMesh()
        {
            // A notched, two-lobed heart silhouette with a pointed tip, beveled
            // front/back facets and a closed rim. All pool entries share this mesh.
            Vector2[] profile = { new Vector2(0f,.065f), new Vector2(.055f,.13f), new Vector2(.12f,.125f),
                new Vector2(.15f,.055f), new Vector2(.13f,-.015f), new Vector2(0f,-.15f),
                new Vector2(-.13f,-.015f), new Vector2(-.15f,.055f), new Vector2(-.12f,.125f), new Vector2(-.055f,.13f) };
            var vertices = new Vector3[profile.Length * 12];
            var triangles = new int[vertices.Length];
            int cursor = 0;
            for (int side = 0; side < profile.Length; side++)
            {
                Vector2 a = profile[side], b = profile[(side + 1) % profile.Length];
                Vector3 frontA = new Vector3(a.x,a.y,-.035f), frontB = new Vector3(b.x,b.y,-.035f);
                Vector3 backA = new Vector3(a.x,a.y,.035f), backB = new Vector3(b.x,b.y,.035f);
                AddTriangle(vertices, ref cursor, new Vector3(0f,-.015f,-.075f), frontA, frontB);
                AddTriangle(vertices, ref cursor, new Vector3(0f,-.015f,.075f), backB, backA);
                AddTriangle(vertices, ref cursor, frontA, backA, backB);
                AddTriangle(vertices, ref cursor, frontA, backB, frontB);
            }
            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
            var mesh = new Mesh { name = "Shared little faceted Heartstone" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTriangle(Vector3[] vertices, ref int cursor, Vector3 a, Vector3 b, Vector3 c)
        { vertices[cursor++] = a; vertices[cursor++] = b; vertices[cursor++] = c; }

#if UNITY_EDITOR
        /// <summary>Root may run in the native Editor; creates and cleans isolated transient fixtures only.</summary>
        public static string RunEngineChecks()
        {
            int assertions = 0;
            System.Action<bool, string> check = (condition, name) =>
            { assertions++; if (!condition) throw new System.InvalidOperationException(name); };
            Mesh mesh = null;
            Material material = null;
            GameObject fixture = null;
            try
            {
                mesh = CreateSharedMesh();
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                int[] triangles = mesh.triangles;
                check(vertices.Length <= 256 && triangles.Length % 3 == 0, "Small triangle mesh budget");
                check(mesh.bounds.size.x < .35f && mesh.bounds.size.y < .35f && mesh.bounds.size.z < .2f, "Little heart bounds");
                var welded = new System.Collections.Generic.Dictionary<Vector3, int>();
                var edges = new System.Collections.Generic.Dictionary<ulong, int>();
                int[] ids = new int[vertices.Length];
                float leftTop = -1f, rightTop = -1f, cleft = -1f, volume = 0f;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 v = vertices[i];
                    check(!float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z), "Finite mesh vertex");
                    check(normals[i].sqrMagnitude > .99f && normals[i].sqrMagnitude < 1.01f, "Unit facet normal");
                    if (!welded.TryGetValue(v, out int id)) { id = welded.Count; welded.Add(v, id); }
                    ids[i] = id;
                    if (v.x < -.03f) leftTop = Mathf.Max(leftTop, v.y);
                    if (v.x > .03f) rightTop = Mathf.Max(rightTop, v.y);
                    if (Mathf.Abs(v.x) < .001f && v.y > 0f) cleft = Mathf.Max(cleft, v.y);
                }
                check(cleft > 0f && leftTop > cleft + .03f && rightTop > cleft + .03f, "Two lobes and a visible central heart cleft");
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    check(Vector3.Cross(b - a, c - a).sqrMagnitude > .0000000001f, "No degenerate facets");
                    volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
                    for (int edge = 0; edge < 3; edge++)
                    {
                        int first = ids[triangles[i + edge]], second = ids[triangles[i + (edge + 1) % 3]];
                        ulong key = ((ulong)Mathf.Min(first, second) << 32) | (uint)Mathf.Max(first, second);
                        edges.TryGetValue(key, out int count);
                        edges[key] = count + 1;
                    }
                }
                foreach (int count in edges.Values) check(count == 2, "Closed welded mesh boundary");
                check(volume > .00001f, "Outward-wound positive-volume heart");

                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
                check(shader != null, "Runtime material shader exists");
                material = new Material(shader);
                fixture = new GameObject("Transient Heartstone engine checks") { hideFlags = HideFlags.HideAndDontSave };
                fixture.transform.position = new Vector3(0f, 200f, 0f);
                HeartcrystalPickup pickup = fixture.AddComponent<HeartcrystalPickup>();
                pickup.Initialize(mesh, material);
                Vector3 floor = new Vector3(0f, 200f, 0f), target = floor + new Vector3(3f, .85f, 0f);
                pickup.Activate(floor + Vector3.up * .55f, floor + Vector3.up * .26f);
                check(fixture.GetComponentsInChildren<Collider>(true).Length == 0 &&
                    fixture.GetComponentsInChildren<Light>(true).Length == 0, "Pickup has no attack-blocking collider or light");
                int saves = 0;
                System.Func<bool> save = () => { saves++; return true; };
                check(!pickup.BeginAttraction() && !pickup.TryConsumeAt(target, true, save) && saves == 0, "Unsettled drop cannot fly or award");
                pickup.Tick(.449f);
                check(!pickup.Settled, "Settlement cannot complete early");
                pickup.Tick(.002f);
                check(pickup.Settled, "Settlement completes after visible delay");
                Vector3 frozen = pickup.WorldPosition;
                pickup.Tick(.25f, false);
                check((pickup.WorldPosition - frozen).sqrMagnitude < .000000001f && !pickup.BeginAttraction(), "Pause/downed gate freezes idle motion and capture");
                pickup.Tick(.01f, true);
                check(pickup.BeginAttraction(), "Settled visible drop begins its actual flight");
                frozen = pickup.WorldPosition;
                pickup.Tick(.25f, false);
                check(pickup.ProposeMagnetStep(target, .25f) == pickup.transform.position &&
                    (pickup.WorldPosition - frozen).sqrMagnitude < .000000001f, "Pause/downed gate freezes flight");
                pickup.Tick(.01f, true);
                int flightFrames = 0;
                for (; flightFrames < 600 && !pickup.Arrived; flightFrames++)
                {
                    pickup.Tick(1f / 60f, true);
                    Vector3 before = pickup.WorldPosition;
                    Vector3 next = pickup.ProposeMagnetStep(target, 1f / 60f);
                    check((next - before).magnitude <= MaximumSpeed / 60f + .00001f, "Actual flight obeys maximum travel speed");
                    pickup.ApplyMagnetStep(next, target);
                    if (!pickup.Arrived) check(!pickup.TryConsumeAt(target, true, save) && saves == 0, "Flight cannot award before arrival");
                }
                check(pickup.Arrived && pickup.Active && flightFrames < 600, "Flight arrives without auto-awarding");
                check(!pickup.TryConsumeAt(target, false, save) && saves == 0, "Paused/downed arrival cannot award");
                check(!pickup.TryConsumeAt(target, true, () => false) && pickup.Active, "Rejected save preserves arrived pickup");
                check(pickup.TryConsumeAt(target, true, save) && !pickup.Active && saves == 1, "Accepted save consumes exactly once");
                check(!pickup.TryConsumeAt(target, true, save) && saves == 1, "Consumed pickup cannot duplicate award");

                pickup.Activate(floor + Vector3.up * .55f, floor + Vector3.up * .26f);
                pickup.Tick(.5f);
                pickup.BeginAttraction();
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Transient Heartstone obstruction";
                wall.transform.SetParent(fixture.transform, true);
                wall.transform.position = floor + new Vector3(1.25f, .65f, 0f);
                wall.transform.localScale = new Vector3(.15f, 2f, 1.5f);
                Physics.SyncTransforms();
                check(!IsTravelClear(pickup.WorldPosition, target, new RaycastHit[16], new Collider[16]), "Native swept query detects a wall");
                pickup.InterruptAttraction(new Vector3(pickup.WorldPosition.x, floor.y + .26f, pickup.WorldPosition.z));
                check(!pickup.IsMagnetized && !pickup.Settled && pickup.Active && !pickup.BeginAttraction(), "Obstruction interrupts flight and returns to settlement");
                pickup.Tick(.5f);
                check(pickup.Settled && Mathf.Abs(pickup.transform.position.y - floor.y - .26f) < .00001f, "Interrupted drop settles at supplied safe floor");
                return "PASS: " + assertions + " native Heartstone geometry, flight, obstruction and save-boundary checks.";
            }
            finally
            {
                if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
#endif
    }
}
