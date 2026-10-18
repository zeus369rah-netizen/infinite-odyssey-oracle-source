using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    /// <summary>Three original refuge pages, read by looking at their physical books.</summary>
    public sealed class LoreLibrary : MonoBehaviour
    {
        private sealed class Book
        {
            public string Title, Body;
            public Collider Collider;
        }

        private readonly List<Object> resources = new List<Object>(20);
        private readonly Book[] books = new Book[3];
        private GameSession session;
        private GameObject libraryRoot;
        private Mesh cubeMesh;
        private Material paper, gold;
        public string Hint { get; private set; } = "";

        public void Initialize(GameSession gameSession)
        {
            session = gameSession;
            if (libraryRoot != null) return;
            libraryRoot = new GameObject("Refuge reading books");
            libraryRoot.transform.SetParent(session.World.transform, false);
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.SetActive(false);
            cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);
            paper = CreateMaterial("Warm book pages", new Color(.88f, .81f, .64f));
            gold = CreateMaterial("Refuge cover gold", new Color(.83f, .61f, .24f));
            Material wood = CreateMaterial("Reading desk walnut", new Color(.19f, .105f, .055f));
            // The warm campfire is at (-4,0,5.25). These desks flank it outside
            // the central route; the third book sits on the entry alcove's crate.
            var deskPieces = new List<CombineInstance>(10);
            AddDesk(deskPieces, new Vector3(-3.2f, 0f, 3.9f));
            AddDesk(deskPieces, new Vector3(-3.2f, 0f, 7.3f));
            Batch(libraryRoot.transform, "Two campfire reading desks", wood, deskPieces.ToArray());

            books[0] = CreateBook("The Ember Refuge",
                "If you found this room by following the smoke, you are welcome here.\n\n" +
                "The kettle belongs to everyone. The dry seat beside the fire belongs to whoever needs it next. " +
                "There is no watch to keep until you have warmed your hands.\n\n" +
                "Before you leave, set one small stick beside the hearth. Someone after you may arrive tired, too.",
                new Vector3(-3.2f, .96f, 3.9f), -8f, new Color(.045f, .24f, .13f));
            books[1] = CreateBook("The Living Ward",
                "A ward begins as a small kindness: a roof repaired, a lamp relit, a door held open. " +
                "Repeat that kindness long enough and even old stone remembers.\n\n" +
                "The green light in the shrine is that memory made visible. It asks for no grand oath. " +
                "Stay close when danger comes. Leave the path clear for a friend.\n\n" +
                "A refuge survives because someone chooses to care for it.",
                new Vector3(-3.2f, .96f, 7.3f), 7f, new Color(.055f, .13f, .24f));
            books[2] = CreateBook("The Four Elements",
                "We learn the elements around an ordinary hearth. Fire warms the room. Water fills the cup. " +
                "Earth holds the walls. Air carries the song.\n\n" +
                "Beyond the refuge, they offer different answers. Fire parts a crowd. Water slows a hurried foe. " +
                "Earth interrupts a heavy blow. Air makes room to breathe.\n\n" +
                "Choose what the moment needs. There is time to learn the rest.",
                new Vector3(8.65f, .86f, -1.55f), 90f, new Color(.29f, .075f, .045f));
        }

        private void Update()
        {
            Hint = "";
            if (session == null || libraryRoot == null || session.World == null) return;
            bool visible = !session.World.IsCoopArena;
            if (libraryRoot.activeSelf != visible) libraryRoot.SetActive(visible);
            if (!visible || !session.IsPlaying || (session.Revival != null && session.Revival.IsHelping) || session.Player == null || session.Player.ViewCamera == null ||
                AbilityBindings.IsRebinding || Cursor.lockState != CursorLockMode.Locked) return;
            Transform eye = session.Player.ViewCamera.transform;
            // The nearest physical hit enforces both range and clear line of sight.
            if (!Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, 2.5f, ~(1 << 2), QueryTriggerInteraction.Ignore)) return;
            for (int i = 0; i < books.Length; i++)
            {
                Book book = books[i];
                if (book == null || hit.collider != book.Collider) continue;
                Hint = "[" + GameActionBindings.GetLabel("Interact") + "] Read " + book.Title;
                if (Keyboard.current != null && Keyboard.current[GameActionBindings.GetKey("Interact")].wasPressedThisFrame && session.Hud != null)
                    session.Hud.ShowLoreBook(book.Title, book.Body);
                return;
            }
        }

        private Book CreateBook(string title, string body, Vector3 position, float yaw, Color color)
        {
            var root = new GameObject(title);
            root.transform.SetParent(libraryRoot.transform, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Material cover = CreateMaterial(title + " cloth cover", color);
            Batch(root.transform, "Cloth cover and spine", cover, new[]
            {
                Part(new Vector3(0f,-.06f,0f), new Vector3(.64f,.02f,.74f)),
                Part(new Vector3(0f,.06f,0f), new Vector3(.64f,.02f,.74f)),
                Part(new Vector3(-.3f,0f,0f), new Vector3(.06f,.13f,.74f))
            });
            Batch(root.transform, "Bound paper", paper, new[] { Part(Vector3.zero, new Vector3(.56f,.1f,.67f)) });
            Batch(root.transform, "Gold four-point cover emblem", gold, new[]
            {
                Part(new Vector3(0f,.078f,-.22f), new Vector3(.19f,.008f,.035f)),
                Part(new Vector3(0f,.078f,-.22f), new Vector3(.035f,.008f,.19f)),
                Part(new Vector3(0f,.08f,-.22f), new Vector3(.07f,.01f,.07f), Quaternion.Euler(0f,45f,0f)),
                Part(new Vector3(-.31f,0f,-.23f), new Vector3(.025f,.15f,.025f)),
                Part(new Vector3(-.31f,0f,.23f), new Vector3(.025f,.15f,.025f))
            });
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(.66f, .16f, .76f);
            return new Book { Title = title, Body = body, Collider = collider };
        }

        private void AddDesk(List<CombineInstance> pieces, Vector3 position)
        {
            pieces.Add(Part(position + Vector3.up * .84f, new Vector3(1.05f,.1f,.85f)));
            foreach (float x in new[] { -.4f, .4f })
            foreach (float z in new[] { -.3f, .3f })
                pieces.Add(Part(position + new Vector3(x,.41f,z), new Vector3(.1f,.82f,.1f)));
            GameObject tabletop = new GameObject("Reading desk collision");
            tabletop.transform.SetParent(libraryRoot.transform, false);
            tabletop.transform.position = position + Vector3.up * .84f;
            tabletop.AddComponent<BoxCollider>().size = new Vector3(1.05f,.1f,.85f);
            foreach(float x in new[]{-.4f,.4f})foreach(float z in new[]{-.3f,.3f}) {
                var leg=new GameObject("Reading desk solid leg");leg.transform.SetParent(libraryRoot.transform,false);
                leg.transform.localPosition=position+new Vector3(x,.41f,z);
                leg.AddComponent<BoxCollider>().size=new Vector3(.1f,.82f,.1f);
            }
        }

        private CombineInstance Part(Vector3 position, Vector3 size)
        { return Part(position, size, Quaternion.identity); }

        private CombineInstance Part(Vector3 position, Vector3 size, Quaternion rotation)
        { return new CombineInstance { mesh = cubeMesh, transform = Matrix4x4.TRS(position, rotation, size) }; }

        private void Batch(Transform parent, string name, Material material, CombineInstance[] pieces)
        {
            var mesh = new Mesh { name = name + " combined" };
            mesh.CombineMeshes(pieces, true, true);
            resources.Add(mesh);
            var batch = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            batch.transform.SetParent(parent, false);
            batch.GetComponent<MeshFilter>().sharedMesh = mesh;
            batch.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .12f);
            resources.Add(material);
            return material;
        }

        private void OnDestroy()
        {
            if (libraryRoot != null) Destroy(libraryRoot);
            for (int i = 0; i < resources.Count; i++) if (resources[i] != null) Destroy(resources[i]);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact existing Y-up room lattice and supported furniture bases.
// Array Formats: Cold room mesh recipes; borrowed saved geometry and fixed colliders.
// Dependencies/Pointers: DungeonWorld owns render resources; visible skins stand 30mm before backing, never coplanar.
