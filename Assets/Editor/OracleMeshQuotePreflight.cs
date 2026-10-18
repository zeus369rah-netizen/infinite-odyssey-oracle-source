#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OdysseyOracle.Editor
{
    // Root-run, quote-only UI fixture. No automatic initialization or generation entry point.
    public static class OracleMeshQuotePreflight
    {
        public const string CreateMenuPath = "Assets/Create/3D/Generate 3D Object";
        const string GeneratorType = "Unity.AI.Mesh.Windows.MeshGeneratorWindow";
        const string PickerType = "Unity.AI.ModelSelector.Windows.ModelSelectorWindow";
        const string RequestTimeKey = "Oracle.MeshQuotePreflight.NameRequestTime";
        const int MaximumNodes = 8192;
        const int MaximumInventoryNodes = 768;
        const int MaximumDepth = 28;

        [Serializable] public sealed class NodeInfo
        {
            public int index, parent, depth;
            public string type, name, classes, text, value, tooltip;
            public bool visible, enabled;
            public float x, y, width, height;
        }
        [Serializable] public sealed class WindowInfo
        {
            public string type, title, entityId;
            public bool truncated;
            public List<NodeInfo> nodes = new List<NodeInfo>();
        }
        [Serializable] public sealed class InventoryInfo
        {
            public string observedUtc;
            public bool stoppedAndSettled;
            public List<WindowInfo> windows = new List<WindowInfo>();
        }
        [Serializable] public sealed class QuoteInfo
        {
            public string observedUtc, title, selectedModel, prompt, referenceAssetPath;
            public string visibleCostText, providerOrFeedbackTooltip, limitation;
            public bool costHasResponseFormat, generateEnabled, imageReferenceVisible;
            public bool nativeAcceptanceVerified;
        }
        [Serializable] public sealed class ReferenceInfo
        {
            public string assetPath, sha256;
            public int width, height, nontransparentPixels;
            public bool fourCornersTransparent;
        }
        [Serializable] sealed class ActionInfo
        {
            public string action, result, nextStep;
            public bool eventDispatched, paidGenerationInvoked;
        }

        public static string Inventory()
        {
            RequireStopped();
            var result = new InventoryInfo { observedUtc = DateTime.UtcNow.ToString("O"), stoppedAndSettled = true };
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (!window || (window.GetType().FullName != GeneratorType && window.GetType().FullName != PickerType)) continue;
                var info = new WindowInfo { type = window.GetType().FullName, title = window.titleContent.text, entityId = window.GetEntityId().ToString() };
                AddInventory(window.rootVisualElement, -1, 0, info);
                result.windows.Add(info);
            }
            return JsonUtility.ToJson(result, true);
        }

        // Starts Unity's normal asset-name flow. It does not call an internal window factory.
        public static string RequestBlankMeshAsset()
        {
            RequireStopped();
            if (FindWindows(GeneratorType).Count != 0) throw new InvalidOperationException("A mesh window already exists; inventory it before creating another asset.");
            bool accepted = EditorApplication.ExecuteMenuItem(CreateMenuPath);
            SessionState.SetFloat(RequestTimeKey, accepted ? (float)EditorApplication.timeSinceStartup : -1f);
            return Action("request blank prefab", accepted, accepted
                ? "Unity accepted the asset menu. Filename editing may still be pending; no window or asset is claimed yet."
                : "Unity rejected or disabled the menu. Inspect normal project/account UI; no setting was bypassed.",
                "Read Inventory() on a later editor turn. If the Project filename field is pending, ConfirmRequestedAssetName() sends one unmodified Return only to the focused Project window.");
        }

        // Optional, one-use UI confirmation following our exact menu request. Never targets a generator.
        public static string ConfirmRequestedAssetName()
        {
            RequireStopped();
            float requestedAt = SessionState.GetFloat(RequestTimeKey, -1f);
            SessionState.EraseFloat(RequestTimeKey);
            if (requestedAt < 0f || EditorApplication.timeSinceStartup - requestedAt > 30d)
                throw new InvalidOperationException("No recent unconsumed menu request. No key was sent.");
            if (FindWindows(GeneratorType).Count != 0)
                throw new InvalidOperationException("The generator is already open. No key was sent.");
            var project = EditorWindow.focusedWindow;
            if (!project || project.GetType().FullName != "UnityEditor.ProjectBrowser")
                throw new InvalidOperationException("The focused window is not Unity's Project window. No key was sent.");
            bool handled = project.SendEvent(Event.KeyboardEvent("return"));
            return Action("confirm requested asset name", true,
                "One ordinary Return was sent to the Project window; SendEvent handled=" + handled + ". Public APIs do not expose the pending IMGUI rename state.",
                "Inventory on the next editor turn. If no mesh window opens, stop and inspect normal UI; do not repeat keys or use internal APIs.");
        }

        public static string OpenModelPicker(string exactGeneratorTitle = null)
        {
            RequirePickerClosed();
            var root = GenerationTab(Generator(exactGeneratorTitle).rootVisualElement);
            var selectors = Find(root, e => e.GetType().FullName == "Unity.AI.Mesh.Components.ModelSelectorButton" && IsVisible(e));
            var selector = ExactlyOne(selectors, "visible mesh model-selector control");
            var buttons = Find(selector, e => e is Button b && b.text == "Change" && IsVisible(e));
            Submit((Button)ExactlyOne(buttons, "visible Change button"));
            return Action("open model picker", true, "Normal navigation-submit event sent to the visible Change button.", "Inventory later; confirm the Select AI Model window actually appeared.");
        }

        public static string OpenVisibleModelDetails(string exactVisibleModelName)
        {
            RequireNonempty(exactVisibleModelName, "model name");
            var picker = ExactlyOneWindow(PickerType, null);
            var tiles = Find(picker.rootVisualElement, e => e.ClassListContains("model-tile") && IsVisible(e));
            var matches = new List<VisualElement>();
            foreach (var tile in tiles)
            {
                var labels = Find(tile, e => e is Label l && e.ClassListContains("model-title-card-label") && l.text == exactVisibleModelName && IsVisible(e));
                if (labels.Count == 1) matches.Add(tile);
            }
            var matched = ExactlyOne(matches, "visible model tile named " + exactVisibleModelName);
            var cards = Find(matched, e => e.GetType().FullName == "Unity.AI.ModelSelector.Components.ModelTitleCard" && IsVisible(e));
            var card = ExactlyOne(cards, "model title card");
            RequireUsable(card);
            var position = card.worldBound.center;
            // The installed custom tile attaches Clickable to this exact non-button card.
            // Ordinary pooled pointer events are used; no private callback or model/store state is invoked.
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, clickCount = 1, mousePosition = position }))
            { down.target = card; card.SendEvent(down); }
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, clickCount = 1, mousePosition = position }))
            { up.target = card; card.SendEvent(up); }
            return Action("open visible model details", true, "Normal pointer events sent to the exact visible model title card.", "Inventory later; verify the details label matches before ConfirmVisibleModelSelection(name). If no details appear, this public event route is unsupported in this window; stop.");
        }

        public static string ConfirmVisibleModelSelection(string exactVisibleModelName)
        {
            RequireNonempty(exactVisibleModelName, "model name");
            var picker = ExactlyOneWindow(PickerType, null);
            var buttons = Find(picker.rootVisualElement, e => e is Button && e.ClassListContains("model-title-card-select") && IsVisible(e));
            var button = (Button)ExactlyOne(buttons, "visible Select model button");
            if (button.text != "Select model") throw new InvalidOperationException("The expected model-selection label changed. No event sent.");
            VisualElement details = button.parent;
            var labels = Find(details, e => e is Label l && e.ClassListContains("model-title-card-label") && IsVisible(e) && l.text == exactVisibleModelName);
            ExactlyOne(labels, "matching visible model details label");
            Submit(button);
            return Action("select visible model", true, "Normal navigation-submit event sent only to Select model.", "Inventory on a later editor turn; verify the generator's selected-model label changed. Consent/provider/account dialogs require their ordinary authorized UI flow.");
        }

        public static string ValidateReference(string importedPngAssetPath)
        {
            RequireStopped();
            return JsonUtility.ToJson(ReadReference(importedPngAssetPath), true);
        }

        public static string SetPromptAndReference(string prompt, string importedPngAssetPath, string exactGeneratorTitle = null)
        {
            if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 1024) throw new ArgumentException("Prompt must contain 1..1024 characters.");
            RequirePickerClosed();
            var window = Generator(exactGeneratorTitle);
            var root = GenerationTab(window.rootVisualElement);
            var fields = Find(root, e => e is TextField && e.name == "prompt" && IsVisible(e));
            var field = (TextField)ExactlyOne(fields, "visible prompt TextField");
            RequireUsable(field);
            if (field.maxLength >= 0 && prompt.Length > field.maxLength) throw new ArgumentException("Prompt exceeds the actual field maxLength.");
            var containers = Find(root, e => e.name == "single-image-reference-container" && IsVisible(e));
            var container = ExactlyOne(containers, "visible single-image reference container; selected model may require another input mode");
            var references = Find(container, e => e is ObjectField && e.name == "image-reference-object-field__input-field" && IsVisible(e));
            var reference = (ObjectField)ExactlyOne(references, "visible image ObjectField");
            RequireUsable(reference);
            ReadReference(importedPngAssetPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(importedPngAssetPath);
            if (!texture) throw new InvalidOperationException("The PNG has not been imported as Texture2D. Parent must import it using normal Editor APIs first.");
            // Public value setters deliver the installed controls' registered ChangeEvent callbacks.
            // Neither setter submits a generation. The package may request its ordinary estimate.
            reference.value = texture;
            field.value = prompt;
            window.Repaint();
            return Action("set visible prompt/reference", true, "Assigned the imported transparent reference and prompt through normal value-change events. Paid Generate was not touched.", "Allow normal UI/network updates, then ReadVisibleQuote(title). A visible estimate can be stale; the public UI does not expose request correlation.");
        }

        public static string ReadVisibleQuote(string exactGeneratorTitle = null)
        {
            var window = Generator(exactGeneratorTitle);
            var root = GenerationTab(window.rootVisualElement);
            var generate = ExactlyOne(Find(root, e => e.GetType().FullName == "Unity.AI.Mesh.Components.GenerateButton" && IsVisible(e)), "visible Generate control");
            var cost = ExactlyOne(Find(generate, e => e is Label && e.name == "points-indicator"), "credit label") as Label;
            var button = ExactlyOne(Find(generate, e => e is Button), "Generate button") as Button;
            var model = ExactlyOne(Find(root, e => e.GetType().FullName == "Unity.AI.Mesh.Components.SelectedModelTitleCard" && IsVisible(e)), "visible selected-model card");
            var modelLabel = ExactlyOne(Find(model, e => e is Label && e.ClassListContains("model-title-card-label")), "selected-model label") as Label;
            var prompt = ExactlyOne(Find(root, e => e is TextField && e.name == "prompt" && IsVisible(e)), "visible prompt") as TextField;
            var refs = Find(root, e => e is ObjectField && e.name == "image-reference-object-field__input-field" && IsVisible(e));
            var reference = refs.Count == 1 ? refs[0] as ObjectField : null;
            string costText = IsVisible(cost) ? cost.text : "";
            return JsonUtility.ToJson(new QuoteInfo
            {
                observedUtc = DateTime.UtcNow.ToString("O"), title = window.titleContent.text,
                selectedModel = modelLabel.text, prompt = prompt.value,
                referenceAssetPath = reference != null && reference.value ? AssetDatabase.GetAssetPath(reference.value) : "",
                visibleCostText = costText, costHasResponseFormat = CostHasResponseFormat(costText),
                generateEnabled = button.enabledInHierarchy, imageReferenceVisible = reference != null,
                providerOrFeedbackTooltip = generate.tooltip,
                nativeAcceptanceVerified = false,
                limitation = "Visible estimate only. Template 5 is not a quote. Public UI exposes no quote request ID, freshness or success flag; do not infer those from button enabled or a hidden/zero badge. Generate remains unpressed."
            }, true);
        }

        static bool CostHasResponseFormat(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.EndsWith(" cr", StringComparison.Ordinal)) return false;
            string number = text.Substring(0, text.Length - 3);
            if (number.Length == 0 || number.Length > 32) return false;
            foreach (char c in number) if (!(c >= '0' && c <= '9') && c != '.' && c != ',' && c != '-') return false;
            string[] bounds = number.Split('-');
            if (bounds.Length < 1 || bounds.Length > 2 || !PositiveNumber(bounds[0], out decimal lower)) return false;
            return bounds.Length == 1 || PositiveNumber(bounds[1], out decimal upper) && upper >= lower;
        }
        static bool PositiveNumber(string value, out decimal parsed)
        {
            return (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed) ||
                decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)) && parsed > 0m;
        }

        static ReferenceInfo ReadReference(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") || path.Contains("\\") || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Reference must be an imported Assets/...png path without traversal.");
            string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, path.Substring(7)));
            string assetRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Reference escaped Assets.");
            var file = new FileInfo(absolute);
            if (!file.Exists || file.Length < 24 || file.Length > 8 * 1024 * 1024) throw new ArgumentException("Reference file is missing or exceeds 8 MiB.");
            byte[] bytes = File.ReadAllBytes(absolute);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < signature.Length; i++) if (bytes[i] != signature[i]) throw new ArgumentException("Reference is not an actual PNG.");
            // Bound decoded size before LoadImage allocates from the file's IHDR dimensions.
            if (bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R' ||
                bytes[16] != 0 || bytes[17] != 0 || bytes[18] != 2 || bytes[19] != 0 ||
                bytes[20] != 0 || bytes[21] != 0 || bytes[22] != 2 || bytes[23] != 0)
                throw new ArgumentException("Reference PNG header must declare exactly 512 by 512 pixels.");
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!image.LoadImage(bytes, false) || image.width != 512 || image.height != 512) throw new ArgumentException("Reference must decode as a 512 by 512 image.");
                var pixels = image.GetPixels32();
                bool corners = pixels[0].a == 0 && pixels[511].a == 0 && pixels[511 * 512].a == 0 && pixels[pixels.Length - 1].a == 0;
                int filled = 0;
                foreach (var pixel in pixels) if (pixel.a > 0) filled++;
                if (!corners || filled == 0 || filled == pixels.Length) throw new ArgumentException("Actual reference must contain artwork with four transparent corners.");
                using (var sha = SHA256.Create())
                    return new ReferenceInfo { assetPath = path, sha256 = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""), width = 512, height = 512, nontransparentPixels = filled, fourCornersTransparent = corners };
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        static EditorWindow Generator(string title)
        {
            RequireStopped();
            return ExactlyOneWindow(GeneratorType, title);
        }
        static VisualElement GenerationTab(VisualElement root)
        {
            return ExactlyOne(Find(root, e => e is Tab tab && tab.label == "Generate" && IsVisible(e)), "visible Generate tab (switch tabs using ordinary UI if needed)");
        }
        static void RequirePickerClosed()
        {
            RequireStopped();
            if (FindWindows(PickerType).Count != 0) throw new InvalidOperationException("Finish or cancel the actual model picker before changing generator controls.");
        }
        static EditorWindow ExactlyOneWindow(string type, string title)
        {
            RequireStopped();
            var windows = FindWindows(type);
            if (title != null) windows.RemoveAll(w => w.titleContent.text != title);
            if (windows.Count != 1) throw new InvalidOperationException("Expected exactly one " + type + "; observed " + windows.Count + ". Inventory titles and pass an exact one.");
            return windows[0];
        }
        static List<EditorWindow> FindWindows(string type)
        {
            var result = new List<EditorWindow>();
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>()) if (window && window.GetType().FullName == type) result.Add(window);
            return result;
        }
        static void Submit(Button button)
        {
            RequireUsable(button);
            // Only callers' hard-coded Change / Select model controls reach this helper.
            if (button.text != "Change" && button.text != "Select model") throw new InvalidOperationException("Button is outside the explicit setup allowlist.");
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        static void RequireStopped()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Run only while Play is stopped and imports/compilation have settled.");
        }
        static void RequireNonempty(string value, string label)
        {
            RequireStopped();
            if (string.IsNullOrWhiteSpace(value) || value.Length > 160) throw new ArgumentException("Supply an exact visible " + label + " of 1..160 characters.");
        }
        static void RequireUsable(VisualElement element)
        {
            if (element == null || !element.enabledInHierarchy || !IsVisible(element)) throw new InvalidOperationException("The actual UI control is hidden, disabled or unattached. Inspect normal UI blockers; nothing was bypassed.");
        }
        static bool IsVisible(VisualElement element)
        {
            if (element == null || element.panel == null || !element.worldBound.Overlaps(element.panel.visualTree.worldBound)) return false;
            if (element.worldBound.width <= 0 || element.worldBound.height <= 0) return false;
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None || current.resolvedStyle.visibility != Visibility.Visible || current.resolvedStyle.opacity <= 0f) return false;
                if (current is ScrollView scroll && !element.worldBound.Overlaps(scroll.contentViewport.worldBound)) return false;
            }
            return true;
        }
        static List<VisualElement> Find(VisualElement root, Predicate<VisualElement> predicate)
        {
            var result = new List<VisualElement>();
            int visited = 0;
            FindChildren(root, predicate, result, 0, ref visited);
            return result;
        }
        static void FindChildren(VisualElement element, Predicate<VisualElement> predicate, List<VisualElement> result, int depth, ref int visited)
        {
            if (element == null || depth > MaximumDepth || ++visited > MaximumNodes) throw new InvalidOperationException("UI traversal exceeded its bound; inspect a smaller visible surface.");
            if (predicate(element)) result.Add(element);
            for (int i = 0; i < element.hierarchy.childCount; i++) FindChildren(element.hierarchy.ElementAt(i), predicate, result, depth + 1, ref visited);
        }
        static VisualElement ExactlyOne(List<VisualElement> elements, string label)
        {
            if (elements.Count != 1) throw new InvalidOperationException("Expected one " + label + "; found " + elements.Count + ". No action taken.");
            return elements[0];
        }
        static string Action(string action, bool sent, string result, string next)
        {
            return JsonUtility.ToJson(new ActionInfo { action = action, eventDispatched = sent, result = result, nextStep = next, paidGenerationInvoked = false }, true);
        }
        static void AddInventory(VisualElement element, int parent, int depth, WindowInfo output)
        {
            if (element == null) return;
            if (depth > MaximumDepth || output.nodes.Count >= MaximumInventoryNodes) { output.truncated = true; return; }
            int index = output.nodes.Count;
            bool password = false;
            for (var p = element; p != null; p = p.parent) if (p is TextField tf && tf.isPasswordField) password = true;
            string value = "";
            if (element is TextField field) value = password ? "[redacted]" : field.name == "prompt" ? field.value : "[other field not inspected]";
            else if (element is ObjectField obj) value = obj.value ? AssetDatabase.GetAssetPath(obj.value) : "";
            var rect = element.worldBound;
            output.nodes.Add(new NodeInfo { index = index, parent = parent, depth = depth,
                type = element.GetType().FullName, name = element.name, classes = Clip(string.Join(" ", element.GetClasses()), 240),
                text = password ? "[redacted]" : element is TextElement text ? Clip(text.text, 320) : "", value = Clip(value, 1100), tooltip = Clip(element.tooltip, 320),
                visible = IsVisible(element), enabled = element.enabledInHierarchy,
                x = Finite(rect.x), y = Finite(rect.y), width = Finite(rect.width), height = Finite(rect.height) });
            for (int i = 0; i < element.hierarchy.childCount; i++) AddInventory(element.hierarchy.ElementAt(i), index, depth + 1, output);
        }
        static float Finite(float value) { return float.IsNaN(value) || float.IsInfinity(value) ? 0f : (float)Math.Round(value, 1); }
        static string Clip(string text, int max) { return string.IsNullOrEmpty(text) ? "" : text.Length > max ? text.Substring(0, max) + "..." : text; }
    }
}
#endif
