using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>The living green ward: a defendable objective and a bounded automatic guardian.</summary>
    public sealed class OracleShrine : MonoBehaviour
    {
        public float MaxHealth { get; private set; } = 300f;
        public float Health { get; private set; } = 300f;
        public bool IsDestroyed => Health <= 0f;
        public bool Active => awakened && !IsDestroyed && GameSession.Instance != null && GameSession.Instance.CombatUnlocked;
        public Vector3 AimPoint => transform.position + Vector3.up * 1.8f;
        public bool InInteractionRange => owner != null && owner.Player != null &&
            (owner.Player.transform.position - transform.position).sqrMagnitude < 3.7f * 3.7f;
        public string InteractionHint => IsDestroyed ? "The living ward has fallen" :
            Active ? "Living ward · defend the green crystal" : "Living ward · the crystal awakens with the trial";

        const float Range = 396f / 32f;
        readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();
        readonly Dictionary<Material, Shape> bodyShapes = new Dictionary<Material, Shape>();
        readonly List<Transform> motes = new List<Transform>();
        readonly Renderer[] moteRenderers = new Renderer[14];
        static readonly int MoteFadeId = Shader.PropertyToID("_MoteFade"), MoteTintId = Shader.PropertyToID("_Tint");
        Material motePaint;
        MaterialPropertyBlock moteBlock;
        public bool MoteAlphaFadingSupported => motePaint != null;
        OracleCombat owner;
        SpatialTargetGrid targetGrid;
        OracleEnemy target;
        Transform focus, healthBar, healthFill;
        Light wardLight;
        int roomReactionLightNode=-1;
        Material crystal, crystalLight, rune, goldStone;
        bool awakened;
        float scanRemaining, shotRemaining, damageFlash;
        Vector3 ShotOrigin => transform.position + Vector3.up * 3.15f;

        public void Initialize(OracleCombat combat, Material darkStone, Material stone, Material turquoise, Material gold)
        {
            owner = combat;
            targetGrid = new SpatialTargetGrid(100, 2f);
            Material sageStone = Clone(stone, "Ward moss limestone", new Color(.39f, .46f, .30f), Color.black);
            Material shadowStone = Clone(darkStone, "Ward deep green stone", new Color(.19f, .25f, .17f), Color.black);
            goldStone = Clone(gold, "Ward hammered gold", new Color(.69f, .48f, .12f), new Color(.035f, .025f, .002f));
            crystal = Clone(turquoise, "Ward emerald crystal", new Color(.12f, .62f, .035f), new Color(.12f, 1.1f, .015f));
            crystalLight = Clone(turquoise, "Ward chartreuse facets", new Color(.5f, .95f, .09f), new Color(.55f, 2.5f, .06f));
            rune = Clone(turquoise, "Ward living runes", new Color(.4f, .93f, .10f), new Color(.20f, 1.5f, .025f));
            BuildSacredPodGeometry(sageStone, shadowStone);
            foreach (KeyValuePair<Material, Shape> pair in bodyShapes) CreateMesh(pair.Key.name, pair.Value, pair.Key, transform);
            bodyShapes.Clear();
            focus = new GameObject("Floating living crystal cluster").transform;
            focus.SetParent(transform, false);
            focus.localPosition = new Vector3(0, 1.94f, 0);
            Shape main = new Shape();
            main.Crystal(Vector3.zero, .39f, 2.45f, Quaternion.Euler(0, 30, 0));
            main.Crystal(new Vector3(.27f, -.37f, .08f), .14f, 1.28f, Quaternion.Euler(0, 40, -10));
            main.Crystal(new Vector3(-.25f, -.40f, -.12f), .13f, 1.10f, Quaternion.Euler(0, 0, 10));
            CreateMesh("Emerald core facets", main, crystal, focus);
            Shape highlights = new Shape();
            highlights.Crystal(new Vector3(-.1f, .12f, -.21f), .13f, 1.98f, Quaternion.Euler(0, 18, 5));
            highlights.Crystal(new Vector3(.17f, -.2f, -.26f), .12f, 1.42f, Quaternion.Euler(0, 35, -8));
            CreateMesh("Chartreuse light facets", highlights, crystalLight, focus);
            Shader moteShader = Resources.Load<Shader>("OracleLifetimeMote");
            if (moteShader != null && moteShader.isSupported)
            {
                motePaint = new Material(moteShader) { name = "Owned living-ward lifetime motes" };
                moteBlock = new MaterialPropertyBlock(); resources.Add(motePaint);
            }
            for (int i = 0; i < 14; i++)
            {
                Transform mote = OracleCombat.CreateBlock(transform, "Living crystal mote " + i, Vector3.zero, Vector3.one * (.035f + i % 3 * .012f), motePaint != null ? motePaint : rune);
                motes.Add(mote); moteRenderers[i] = mote.GetComponent<Renderer>();
            }
            wardLight = new GameObject("Living ward green illumination").AddComponent<Light>();
            wardLight.transform.SetParent(transform, false);
            wardLight.transform.localPosition = new Vector3(0, 2.25f, 0);
            wardLight.type = LightType.Point;
            wardLight.color = new Color(.42f, 1f, .13f);
            wardLight.intensity = 7.5f;
            wardLight.range = 9.5f;
            wardLight.shadows = LightShadows.None;
            wardLight.bounceIntensity = 0;
            roomReactionLightNode=GameSession.Instance?.World?.RegisterRoomReactionExternalLight(wardLight,0x70000001u)??-1;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 1.45f, 0);
            collider.size = new Vector3(1.8f, 2.9f, 1.8f);
            BuildHealthBar(shadowStone);
            ResetShrine();
        }

        // # ARCHITECTURE SNAPSHOT
        // Original emerald sacred pod: fourfold profile and interlocked square sigil, no borrowed lettering.
        // Build-time vector math writes existing Shape buffers; materials, meshes and grid keep their owners.
        static readonly Vector2[] SacredArchProfile = {
            new Vector2(.975807f, .86f), new Vector2(1.01f, 1.45f),
            new Vector2(.94f, 2.20f), new Vector2(.76f, 2.77f),
            new Vector2(.50f, 3.18f), new Vector2(.28f, 3.42f)
        };

        void BuildSacredPodGeometry(Material sageStone, Material shadowStone)
        {
            Shape dark = GetShape(shadowStone), pale = GetShape(sageStone);
            Shape trim = GetShape(goldStone), glow = GetShape(rune);
            if(GameSession.Instance?.World?.IsTutorialRefuge==true)
            {
                var dressing=new RefugeDressSink020(dark,pale,trim,glow);
                RefugeShrineDress020.Build(ref dressing);
            }
            // Closed faceted discs stay flush to y0. The top disc supports every corner of all four feet.
            dark.Cylinder(new Vector3(0, .10f, 0), 1.40f, .20f, 32);
            pale.Cylinder(new Vector3(0, .30f, 0), 1.31f, .40f, 32);
            dark.Cylinder(new Vector3(0, .53f, 0), 1.29f, .22f, 32);
            pale.Cylinder(new Vector3(0, .735f, 0), .71f, .19f, 32);
            Ring(goldStone, 1.365f, .215f, .045f, .045f, 32);
            Ring(rune, 1.345f, .212f, .022f, .022f, 32);
            Ring(goldStone, 1.275f, .515f, .035f, .045f, 32);
            Ring(goldStone, .66f, .825f, .12f, .11f, 16);
            Ring(rune, .69f, .89f, .030f, .025f, 24);

            for (int index = 0; index < 8; index++)
            {
                Quaternion q = Quaternion.Euler(0, index * 45f, 0);
                // Small asymmetric hooks form an original rim rhythm, without text copied from the reference.
                glow.Box(q * new Vector3(0, .216f, 1.33f), new Vector3(.055f, .026f, .11f), q);
                trim.Box(q * new Vector3(.055f, .217f, 1.32f), new Vector3(.065f, .024f, .035f), q);
                trim.Box(q * new Vector3(-.055f, .217f, 1.32f), new Vector3(.025f, .024f, .065f), q);
            }
            for (int square = 0; square < 2; square++)
            {
                Shape shape = square == 0 ? trim : glow;
                float angle = square * 45f;
                for (int edge = 0; edge < 4; edge++)
                {
                    Quaternion a = Quaternion.Euler(0, angle + edge * 90f, 0);
                    Quaternion b = Quaternion.Euler(0, angle + (edge + 1) * 90f, 0);
                    Vector3 from = a * new Vector3(0, .837f + square * .010f, .56f);
                    Vector3 to = b * new Vector3(0, .837f + square * .010f, .56f);
                    SacredBeam(shape, from, to, Vector3.up, .020f, .014f);
                }
            }
            for (int arch = 0; arch < 4; arch++)
            {
                float angle = (45f + arch * 90f) * Mathf.Deg2Rad;
                Vector3 radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                Vector3 tangent = new Vector3(radial.z, 0, -radial.x);
                Vector3 foot = radial * .975807f;
                trim.Box(foot + Vector3.up * .75f, new Vector3(.40f, .22f, .40f), Quaternion.identity);
                for (int segment = 0; segment < SacredArchProfile.Length - 1; segment++)
                {
                    Vector2 a = SacredArchProfile[segment], b = SacredArchProfile[segment + 1];
                    Vector3 from = radial * a.x + Vector3.up * a.y, to = radial * b.x + Vector3.up * b.y;
                    Vector3 normal = Vector3.Cross(tangent, (to - from).normalized).normalized;
                    SacredBeam(dark, from, to, tangent, .18f, .24f);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 inset = -normal * .098f + tangent * side * .102f;
                        SacredBeam(trim, from + inset, to + inset, tangent, .025f, .025f);
                    }
                    if (segment == 1 || segment == 3)
                        SacredBeam(glow, from - normal * .096f, to - normal * .096f, tangent, .020f, .035f);
                }
                foreach (int node in new[] { 1, 3 })
                {
                    Vector2 profile = SacredArchProfile[node];
                    trim.Box(radial * profile.x + Vector3.up * profile.y, new Vector3(.29f, .10f, .29f), Quaternion.LookRotation(radial));
                }
                Vector2 tip = SacredArchProfile[SacredArchProfile.Length - 1];
                trim.Box(radial * tip.x + Vector3.up * tip.y, new Vector3(.12f, .10f, .12f), Quaternion.LookRotation(radial));
            }
        }

        readonly struct RefugeDressSink020:IRefugeShrineDressSink020
        {
            readonly Shape dark,pale,trim,glow;
            public RefugeDressSink020(Shape dark,Shape pale,Shape trim,Shape glow)
            {this.dark=dark;this.pale=pale;this.trim=trim;this.glow=glow;}
            Shape Pick(RefugeShrineDressPaint020 paint)=>paint==RefugeShrineDressPaint020.Dark?dark:
                paint==RefugeShrineDressPaint020.Pale?pale:paint==RefugeShrineDressPaint020.Gold?trim:glow;
            public void Box(RefugeShrineDressPaint020 paint,float x,float y,float z,float width,float height,float depth,float yaw)
            {Pick(paint).Box(new Vector3(x,y,z),new Vector3(width,height,depth),Quaternion.Euler(0,yaw,0));}
            public void Crystal(RefugeShrineDressPaint020 paint,float x,float y,float z,float radius,float height,float yaw)
            {Pick(paint).Crystal(new Vector3(x,y,z),radius,height,Quaternion.Euler(0,yaw,0));}
        }

        static void SacredBeam(Shape shape, Vector3 from, Vector3 to, Vector3 up, float thickness, float width)
        {
            Vector3 delta = to - from;
            shape.Box((from + to) * .5f, new Vector3(thickness, width, delta.magnitude + .025f), Quaternion.LookRotation(delta, up));
        }

        public void Activate()
        {
            if (awakened || IsDestroyed || owner == null || !owner.Playing || GameSession.Instance == null || !GameSession.Instance.CombatUnlocked) return;
            awakened = true;
            scanRemaining = shotRemaining = 0f;
            owner.SpawnImpact(ShotOrigin, new Color(.35f, 1f, .08f), .65f);
        }
        public void TakeDamage(float amount)
        {
            if (!Active || owner == null || !owner.Playing || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            damageFlash = .22f;
            if (IsDestroyed) { awakened = false; target = null; }
            UpdateHealthBar();
        }
        public void ResetShrine()
        {
            Health = MaxHealth;
            awakened = false;
            target = null;
            scanRemaining = shotRemaining = damageFlash = 0f;
            if (focus != null) focus.localScale = Vector3.one;
            UpdateHealthBar();
            UpdateVisuals();
        }
        public void ApplyRemoteHealth(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            if (value < Health) damageFlash = .18f;
            Health = Mathf.Clamp(value, 0f, MaxHealth);
            awakened = !IsDestroyed && GameSession.Instance != null && GameSession.Instance.CombatUnlocked;
            if (IsDestroyed) { awakened = false; target = null; }
            UpdateHealthBar();
            UpdateVisuals();
        }
        void Update()
        {
            if (owner == null || GameSession.Instance == null) return;
            if (!awakened && !IsDestroyed && owner.Playing && GameSession.Instance.CombatUnlocked) Activate();
            float dt = owner.Playing ? Time.deltaTime : 0f;
            damageFlash = Mathf.Max(0, damageFlash - dt);
            UpdateVisuals();
            if (!Active || !owner.Playing) return;
            if (GameSession.Instance.World != null && GameSession.Instance.World.IsCoopArena &&
                GameSession.Instance.Defense != null && !GameSession.Instance.Defense.IsAuthority) return;
            shotRemaining = Mathf.Max(0f, shotRemaining - dt);
            scanRemaining -= dt;
            if (scanRemaining <= 0f) { scanRemaining = .25f; Scan(); }
            if (target == null || !target.Targetable || shotRemaining > 0f) return;
            Vector3 offset = target.AimPoint - ShotOrigin;
            if (offset.sqrMagnitude > Range * Range || !owner.HasLineOfSight(ShotOrigin, target.AimPoint)) return;
            owner.LaunchShrineBolt(ShotOrigin, target.AimPoint);
            shotRemaining = OracleCombat.AbilityCooldownSeconds;
        }
        void LateUpdate()
        {
            if (healthBar != null && owner != null && owner.Player != null && owner.Player.ViewCamera != null)
                healthBar.rotation = Quaternion.LookRotation(healthBar.position - owner.Player.ViewCamera.transform.position, Vector3.up);
        }
        void UpdateVisuals()
        {
            if (focus == null) return;
            float time = Time.time;
            focus.localRotation = Quaternion.Euler(0, time * 12f, 0);
            focus.localPosition = new Vector3(0, IsDestroyed ? 1.1f : 1.94f + Mathf.Sin(time * 1.7f) * .055f, 0);
            focus.localScale = Vector3.one * (IsDestroyed ? .25f : 1f);
            float energy = IsDestroyed ? .025f : (Active ? 1f : .78f);
            if (damageFlash > 0) energy += damageFlash * 2f;
            MoteBreathingState breath = MoteEnvelopeMath.Breath(time);
            crystal.SetColor("_EmissionColor", new Color(.12f, 1.1f, .015f) * (energy * breath.Brightness));
            crystalLight.SetColor("_EmissionColor", new Color(.55f, 2.5f, .06f) * (energy * breath.Brightness));
            if (motePaint != null) motePaint.SetColor(MoteTintId, new Color(.16f * energy * breath.Brightness, energy * breath.Brightness, .02f * energy * breath.Brightness, 1f));
            rune.SetColor("_EmissionColor", new Color(.20f, 1.5f, .025f) * energy);
            wardLight.enabled = !IsDestroyed;
            wardLight.intensity = (Active ? 7.5f : 6f) * breath.Brightness*(GameSession.Instance?.World?.RoomReactionGain(roomReactionLightNode)??1);
            wardLight.range = 9.5f * breath.RangeFactor;
            for (int i = 0; i < motes.Count; i++)
            {
                float phase = time * (.24f + i % 3 * .04f) + i * 2.399f;
                float radius = .62f + i % 4 * .12f;
                float height = Mathf.Repeat(time * .23f + i * .27f, 2.7f);
                float fade = MoteEnvelopeMath.Envelope(height / 2.7f);
                motes[i].localPosition = new Vector3(Mathf.Cos(phase) * radius, .8f + height, Mathf.Sin(phase) * radius);
                motes[i].localRotation = Quaternion.Euler(i * 27f, time * 32f, 45f);
                motes[i].localScale = Vector3.one * ((.035f + i % 3 * .012f) * fade);
                if (motePaint != null && moteRenderers[i] != null) { moteBlock.SetFloat(MoteFadeId, fade); moteRenderers[i].SetPropertyBlock(moteBlock); }
                motes[i].gameObject.SetActive(!IsDestroyed);
            }
        }
        void Scan()
        {
            target = null;
            targetGrid.Clear();
            var enemies = owner.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                OracleEnemy enemy = enemies[i];
                if (enemy == null || !enemy.Targetable) continue;
                Vector3 point = enemy.AimPoint;
                if ((point - ShotOrigin).sqrMagnitude > Range * Range || !owner.HasLineOfSight(ShotOrigin, point)) continue;
                targetGrid.Add(enemy.TargetId, point);
            }
            if (!targetGrid.TryFindNearest(ShotOrigin, Range, out int stableId, out Vector3 position)) return;
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i] != null && enemies[i].Targetable && enemies[i].TargetId == stableId) { target = enemies[i]; break; }
        }
        void BuildHealthBar(Material background)
        {
            healthBar = new GameObject("Living ward world health bar").transform;
            healthBar.SetParent(transform, false);
            healthBar.localPosition = new Vector3(0, 3.7f, 0);
            OracleCombat.CreateBlock(healthBar, "Gold health frame", Vector3.zero, new Vector3(2.72f, .21f, .035f), goldStone);
            OracleCombat.CreateBlock(healthBar, "Health recess", new Vector3(0, 0, -.025f), new Vector3(2.62f, .13f, .025f), background);
            healthFill = OracleCombat.CreateBlock(healthBar, "Green shrine health", new Vector3(0, 0, -.045f), new Vector3(2.54f, .095f, .018f), rune);
        }
        void UpdateHealthBar()
        {
            if (healthFill == null) return;
            float ratio = Mathf.Clamp01(Health / Mathf.Max(1, MaxHealth));
            healthFill.localScale = new Vector3(2.54f * Mathf.Max(.001f, ratio), .095f, .018f);
            healthFill.localPosition = new Vector3(-1.27f * (1 - ratio), 0, -.045f);
        }
        Material Clone(Material source, string label, Color color, Color emission)
        {
            Material result = new Material(source) { name = label };
            result.SetColor("_BaseColor", color); result.SetColor("_Color", color);
            result.SetFloat("_Smoothness", .14f);
            result.EnableKeyword("_EMISSION"); result.SetColor("_EmissionColor", emission);
            resources.Add(result);
            return result;
        }
        Shape GetShape(Material material)
        {
            if (!bodyShapes.TryGetValue(material, out Shape shape)) { shape = new Shape(); bodyShapes.Add(material, shape); }
            return shape;
        }
        void Add(Material material, Vector3 p, Vector3 size) { Add(material, p, size, Quaternion.identity); }
        void Add(Material material, Vector3 p, Vector3 size, Quaternion rotation) { GetShape(material).Box(p, size, rotation); }
        void Ring(Material material, float radius, float y, float depth, float height, int segments)
        {
            float length = 2f * Mathf.PI * radius / segments * 1.045f;
            for (int i = 0; i < segments; i++)
            {
                Quaternion q = Quaternion.Euler(0, i * 360f / segments, 0);
                Add(material, q * new Vector3(0, y, radius), new Vector3(length, height, depth), q);
            }
        }
        void CreateMesh(string label, Shape shape, Material material, Transform parent)
        {
            GameObject go = new GameObject(label + " combined shrine geometry");
            go.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = label, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(shape.Vertices); mesh.SetNormals(shape.Normals); mesh.SetUVs(0, shape.UVs); mesh.SetTriangles(shape.Indices, 0);
            mesh.RecalculateBounds(); mesh.UploadMeshData(true); resources.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }
        void OnDestroy()
        {
            if (targetGrid != null) targetGrid.Dispose();
            foreach (UnityEngine.Object resource in resources) if (resource != null) Destroy(resource);
        }
        sealed class Shape
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> UVs = new List<Vector2>();
            public readonly List<int> Indices = new List<int>();
            public void Box(Vector3 p, Vector3 size, Quaternion q)
            {
                Vector3 h = size * .5f;
                Quad(p,q,new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z));
                Quad(p,q,new Vector3(h.x,-h.y,-h.z),new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(h.x,h.y,-h.z));
                Quad(p,q,new Vector3(h.x,-h.y,h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,h.y,h.z));
                Quad(p,q,new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,h.y,h.z),new Vector3(-h.x,h.y,-h.z));
                Quad(p,q,new Vector3(-h.x,h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z));
                Quad(p,q,new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,-h.y,h.z),new Vector3(-h.x,-h.y,h.z));
            }
            public void Cylinder(Vector3 p, float radius, float height, int segments)
            {
                float half = height * .5f;
                for (int index = 0; index < segments; index++)
                {
                    float a = index * Mathf.PI * 2f / segments, b = (index + 1) * Mathf.PI * 2f / segments;
                    Vector3 lowA = new Vector3(Mathf.Cos(a) * radius, -half, Mathf.Sin(a) * radius);
                    Vector3 lowB = new Vector3(Mathf.Cos(b) * radius, -half, Mathf.Sin(b) * radius);
                    Vector3 highA = new Vector3(lowA.x, half, lowA.z), highB = new Vector3(lowB.x, half, lowB.z);
                    Quad(p, Quaternion.identity, lowA, highA, highB, lowB);
                    Triangle(p, Quaternion.identity, Vector3.up * half, highB, highA);
                    Triangle(p, Quaternion.identity, Vector3.down * half, lowA, lowB);
                }
            }
            public void Crystal(Vector3 p,float radius,float height,Quaternion q)
            {
                for(int i=0;i<6;i++)
                {
                    float a=i*Mathf.PI/3f,b=(i+1)*Mathf.PI/3f;
                    Vector3 loA=new Vector3(Mathf.Cos(a)*radius,-height*.23f,Mathf.Sin(a)*radius);
                    Vector3 loB=new Vector3(Mathf.Cos(b)*radius,-height*.23f,Mathf.Sin(b)*radius);
                    Vector3 hiA=new Vector3(loA.x,height*.23f,loA.z),hiB=new Vector3(loB.x,height*.23f,loB.z);
                    Quad(p,q,loA,hiA,hiB,loB);
                    Triangle(p,q,hiA,Vector3.up*height*.5f,hiB);
                    Triangle(p,q,loB,Vector3.down*height*.5f,loA);
                }
            }
            void Quad(Vector3 p,Quaternion q,Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(p,q,a,b,c);Triangle(p,q,a,c,d);}
            void Triangle(Vector3 p,Quaternion q,Vector3 a,Vector3 b,Vector3 c)
            {
                int n=Vertices.Count;
                Vector3 normal=q*Vector3.Cross(b-a,c-a).normalized;
                Vertices.Add(p+q*a);Vertices.Add(p+q*b);Vertices.Add(p+q*c);
                Normals.Add(normal);Normals.Add(normal);Normals.Add(normal);
                UVs.Add(Vector2.zero);UVs.Add(Vector2.right);UVs.Add(Vector2.up);
                Indices.Add(n);Indices.Add(n+1);Indices.Add(n+2);
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: unchanged main-refuge ward local origin, original radius1.40 footprint and box collider; open centre radius.90.
// Array Formats: cold pure16-group/40-primitive recipe adds528 triangles to the existing four body material Shape batches.
// Dependencies/Pointers: no new Object/material/light/collider/Update or tutorial mutation; all original collider/placement/gameplay source bytes retained outside exact inserted regions, native add-on visual gate pending.
