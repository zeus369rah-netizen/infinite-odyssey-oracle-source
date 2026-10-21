using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // Visual representation only. The local CharacterController remains the movement owner.
    public sealed partial class RemoteOracleAvatar : MonoBehaviour
    {
        public ulong ClientId { get; private set; }
        Vector3 targetPosition;
        Quaternion targetRotation;
        Transform wand,visualBody;
        OracleAvatarModel appearanceModel;
        bool initialized;
        float motion;
        float phase;
        static Material emerald, charcoal, brass, turquoise, shadow;
        static int materialUsers;

        public void Initialize(ulong clientId, Vector3 position, float yaw)
        {
            if (initialized) return;
            var rooms=RoomConnection.Instance;
            if(rooms!=null&&rooms.Connected&&clientId==rooms.LocalClientId){gameObject.SetActive(false);return;}
            ClientId = clientId;
            gameObject.name = "Remote Oracle " + clientId;
            CreateMaterials();
            materialUsers++;
            Build();
            appearanceModel.Apply(OracleAvatarAppearance.StartingLook((int)(clientId & 7UL)));
            targetPosition = position;
            targetRotation = Quaternion.Euler(0f, yaw, 0f);
            transform.SetPositionAndRotation(position, targetRotation);
            initialized = true;
        }

        public bool ApplyAppearance(uint packed) => initialized && appearanceModel != null && appearanceModel.Apply(packed);

        public bool TryGetHealth(out PeerHealthSnapshot snapshot)
        {
            snapshot=default;var owner=GameSession.Instance;
            return initialized&&owner!=null&&owner.TryGetPeerHealth(ClientId,out snapshot);
        }
        public void ApplyPose(Vector3 position, float yaw)
        {
            if (!initialized) return;
            targetPosition = position;
            targetRotation = Quaternion.Euler(0f, yaw, 0f);
            // Joining, portals and host-approved stage changes must not interpolate through walls.
            if ((transform.position - position).sqrMagnitude > 64f) {
                nativePose015?.Reset();
                transform.SetPositionAndRotation(position, targetRotation);
            }
        }

        void Update()
        {
            if (!initialized) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            float blend = 1f - Mathf.Exp(-14f * dt);
            Vector3 previous = transform.position;
            transform.position = Vector3.Lerp(previous, targetPosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
            motion = Mathf.Lerp(motion, Mathf.Clamp01((transform.position - previous).magnitude / Mathf.Max(.001f, dt) / 3f), blend);
            phase += dt * 8f;
            bool knownDowned=TryGetHealth(out var health)&&health.Downed;
            bool nativePose=EvaluatePlayerPose015(dt,knownDowned);
            if(!nativePose)appearanceModel.Animate(phase,motion);
            if(visualBody!=null)visualBody.localRotation=Quaternion.Slerp(visualBody.localRotation,
                knownDowned?Quaternion.Euler(0f,0f,72f):Quaternion.identity,blend);
            if (wand != null && !nativePose)
                wand.localRotation = Quaternion.Euler(6f + Mathf.Sin(phase) * motion * 5f, 0f, -13f);
        }

        void Build()
        {
            // Existing validated-feet root/downed pivot and shared material owner are preserved.
            visualBody=new GameObject("Oracle visual body").transform;visualBody.SetParent(transform,false);
            appearanceModel=new OracleAvatarModel(visualBody,emerald,brass,charcoal,turquoise);
            wand=appearanceModel.Wand;
        }

        static void Part(string label, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        static void CreateMaterials()
        {
            if (emerald != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            emerald = Paint(shader, "Remote Oracle emerald", new Color(.045f, .25f, .16f));
            charcoal = Paint(shader, "Remote Oracle charcoal", new Color(.018f, .037f, .031f));
            brass = Paint(shader, "Remote Oracle brass", new Color(.68f, .49f, .20f));
            turquoise = Paint(shader, "Remote Oracle turquoise", new Color(.28f, .87f, .78f));
            shadow = Paint(shader, "Remote Oracle black eyes", new Color(.008f, .015f, .018f));
            if (turquoise.HasProperty("_EmissionColor"))
            {
                turquoise.EnableKeyword("_EMISSION");
                turquoise.SetColor("_EmissionColor", new Color(.12f, .55f, .44f));
            }
        }

        static Material Paint(Shader shader, string label, Color color)
        {
            var paint = new Material(shader) { name = label };
            if (paint.HasProperty("_BaseColor")) paint.SetColor("_BaseColor", color);
            else paint.color = color;
            if (paint.HasProperty("_Smoothness")) paint.SetFloat("_Smoothness", .22f);
            return paint;
        }

        void OnDestroy()
        {
            DisposePlayerPose015();
            appearanceModel?.Dispose();appearanceModel=null;
            if (!initialized) return;
            materialUsers--;
            if (materialUsers > 0) return;
            if (emerald != null) Destroy(emerald);
            if (charcoal != null) Destroy(charcoal);
            if (brass != null) Destroy(brass);
            if (turquoise != null) Destroy(turquoise);
            if (shadow != null) Destroy(shadow);
            emerald = charcoal = brass = turquoise = shadow = null;
            materialUsers = 0;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge world metres/Y-up, first-person camera and host ownership.
// Array Formats: Fixed spawn/admission state; no new hot allocations or native ownership.
// Dependencies/Pointers: Host pose preserved; accepted guest spawn commits before camera/body/pose publication; root owns integration/native tests.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original Oracle accepted world roots and camera/child-local poses only; no authority geometry changes.
// Array Formats: Fixed31shared body slots/2prewarmed player graphs and original palette/pose/feedback buffers; no wire ABI growth.
// Dependencies/Pointers: Player/Rooms/Combat remain sole owners; root integrates default-OFF original native player pilot and verifies real bindings/peer/clearance.
