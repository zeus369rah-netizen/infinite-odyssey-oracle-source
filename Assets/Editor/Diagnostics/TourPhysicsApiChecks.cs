#if UNITY_EDITOR
using System;
using UnityEngine;
using OdysseyOracle;

// Optional ROOT-RUN native prerequisite. Synthetic isolated floor/step/wall geometry
// validates the PhysX APIs used by the tour; it does not validate the dungeon itself.
public static class TourPhysicsApiChecks
{
    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run in native Play mode after Stop/import.");
        if (GameSession.Instance != null && GameSession.Instance.Rooms != null && GameSession.Instance.Rooms.Connected)
            throw new InvalidOperationException("Do not run an isolated physics fixture in a live room.");
        Vector3 origin = new Vector3(1000f, 0f, 1000f);
        if (Physics.CheckBox(origin + Vector3.up * 1.5f, new Vector3(8f, 4f, 6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException("Isolated test area is occupied; no objects were created.");
        GameObject root = null;
        int assertions = 0;
        Action<bool, string> require = (ok, label) => {
            assertions++;
            if (!ok) throw new InvalidOperationException(label);
        };
        try
        {
            root = new GameObject("Transient native tour API checks") { hideFlags = HideFlags.HideAndDontSave };
            Box(root.transform, "Fixture floor", origin + Vector3.down * .2f, new Vector3(12f, .4f, 8f));
            Box(root.transform, "Fixture step", origin + new Vector3(.8f, .075f, 0f), new Vector3(.6f, .15f, 4f));
            BoxCollider wall = Box(root.transform, "Fixture wall", origin + new Vector3(3f, 1.5f, 0f), new Vector3(.4f, 3f, 6f));
            var actor = new GameObject("Fixture CharacterController"); actor.transform.SetParent(root.transform, false);
            actor.transform.position = origin + Vector3.up * .1f;
            CharacterController controller = actor.AddComponent<CharacterController>();
            controller.height = 1.85f; controller.radius = .29f; controller.center = Vector3.up * .925f;
            controller.stepOffset = .3f; controller.skinWidth = .035f; controller.minMoveDistance = 0f;
            Physics.SyncTransforms();
            for (int i = 0; i < 40; i++) controller.Move(Vector3.down * (2f / 60f));
            require(controller.isGrounded, "Actual CharacterController settles onto the synthetic native floor");
            require(Mathf.Abs(actor.transform.position.y) < .1f, "Floor contact stays within the controller skin envelope");
            bool side = false; float maximumY = actor.transform.position.y;
            for (int i = 0; i < 120; i++)
            {
                CollisionFlags flags = controller.Move(new Vector3(4.2f / 60f, -2f / 60f, 0f));
                side |= (flags & CollisionFlags.Sides) != 0;
                maximumY = Mathf.Max(maximumY, actor.transform.position.y);
            }
            require(maximumY > .08f, "Actual movement climbs the .15m step within the .3m step offset");
            require(side && actor.transform.position.x - origin.x > 2.4f && actor.transform.position.x - origin.x < 2.7f,
                "Actual wall contact stops the controller before the wall's near face");
            require(controller.isGrounded, "Native floor support remains after step and wall traversal");

            var proxyObject = new GameObject("Disabled query capsule"); proxyObject.transform.SetParent(root.transform, false);
            CapsuleCollider proxy = proxyObject.AddComponent<CapsuleCollider>();
            proxy.enabled = false; proxy.height = controller.height; proxy.radius = controller.radius; proxy.center = controller.center;
            require(Physics.ComputePenetration(proxy, origin + new Vector3(2.9f, 0f, 0f), Quaternion.identity,
                wall, wall.transform.position, wall.transform.rotation, out _, out float knownDepth) && knownDepth > .1f,
                "Disabled supported capsule reports a known deep overlap at explicit poses");
            bool overlapping = Physics.ComputePenetration(proxy, actor.transform.position, Quaternion.identity,
                wall, wall.transform.position, wall.transform.rotation, out _, out float stoppedDepth);
            require(!overlapping || stoppedDepth <= .08f, "Stopped controller is not deeply inside the synthetic wall");
            return "PASS: " + assertions + " native CharacterController/floor/step/wall/disabled-capsule API assertions. " +
                "Synthetic fixture only; actual dungeon/arena tour and frame timing remain separate.";
        }
        finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
    }

    private static BoxCollider Box(Transform root, string label, Vector3 position, Vector3 size)
    {
        var item = new GameObject(label); item.transform.SetParent(root, false); item.transform.position = position;
        BoxCollider collider = item.AddComponent<BoxCollider>(); collider.size = size; return collider;
    }
}
#endif
