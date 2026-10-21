using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace OdysseyOracle.NextRelease
{
    /// <summary>
    /// Runtime callback-driven shop view; no GameSession, NPC or progression-model dependency.
    /// Initialize with the existing overlay canvas and live callbacks.
    /// Wallet/cost/ownership are always reread; this component never grants currency or modifies levels.
    /// Metadata must describe gameplay that the integrating host has actually implemented.
    /// Typical model bindings: Wallet=()=>model.CrystalWallet; WandLevel=model.GetWandLevel;
    /// WandUpgradeCost=model.GetWandUpgradeCost; CanBuyWand=e=>model.CanBuyWand(e,isNearNpc());
    /// TryBuyWand=(e,near)=>model.TryBuyWand(e,near); PassiveCost=(e,n)=>HeartstoneProgressionModel.GetPassiveUnlockCost(n);
    /// PassiveUnlocked=model.IsPassiveUnlocked; CanUnlockPassive=(e,n)=>model.CanUnlockPassive(e,n,isNearNpc());
    /// TryUnlockPassive=(e,n,near)=>model.TryUnlockPassive(e,n,near). The host saves successful transactions.
    /// InteractKeyLabel must read the live binding, e.g. ()=>GameActionBindings.GetLabel("Interact").
    /// </summary>
    public sealed class BlacksmithShopPanel : MonoBehaviour
    {
        public sealed class Bindings
        {
            // Index contract from HeartstoneProgressionModel: Fire, Water, Earth, Air (0..3).
            // Water/Ice naming stays caller-owned; tower catalog indices are unrelated.
            public string[] WandNames;
            public Color[] WandColors;
            public string CurrencyName = "crystals";
            public Func<int> Wallet;
            public Func<int, int> WandLevel;
            public Func<int, int> WandUpgradeCost;
            public Func<int, bool> CanBuyWand;
            public Func<int, bool, bool> TryBuyWand;
            public Func<int, string> CurrentWandEffect;
            public Func<int, string> NextWandEffect;
            public Func<bool> IsNearBlacksmith;
            public Func<bool> IsLookingAtBlacksmith;
            public Func<string> InteractKeyLabel;
            public Func<bool> IsPaused;
            public Action<bool> SetPaused;
            public Action Closed;

            // Optional passive purchasing. Leaving these callbacks unset keeps the tree read-only.
            public Func<int, int, string> PassiveName;
            public Func<int, int, string> PassiveDescription;
            public Func<int, int, int> PassiveCost;
            public Func<int, int, bool> PassiveUnlocked;
            public Func<int, int, bool> CanUnlockPassive;
            public Func<int, int, bool, bool> TryUnlockPassive;
            // Supply verified descriptions from gameplay, not the wallet model. Requested guide topics:
            // Fire burn ticks, Water/Ice slow (name unresolved), Air push, Earth stagger, tower/ally bonuses.
            // The draft progression model does not implement any of those effects by itself.
            public string[] BranchHighlights;
            public Func<string> TrainingNote;
            public bool DrawHoverHint = true;

            internal void Validate()
            {
                if (WandNames == null || WandNames.Length != 4 || WandColors == null || WandColors.Length != 4)
                    throw new ArgumentException("The blacksmith requires four named/colored player wands, not tower types.");
                if (Wallet == null || WandLevel == null || WandUpgradeCost == null || CanBuyWand == null ||
                    TryBuyWand == null || IsNearBlacksmith == null || IsLookingAtBlacksmith == null || InteractKeyLabel == null ||
                    IsPaused == null || SetPaused == null)
                    throw new ArgumentException("Live economy, target/range, bound-interact-label, and pause callbacks are required.");
                if (string.IsNullOrWhiteSpace(CurrencyName)) CurrencyName = "crystals";
            }
        }

        private sealed class WandRow
        {
            public UnityEngine.UI.Text Title, Level, Effect, BuyText, Availability;
            public UnityEngine.UI.Button Buy;
        }

        private static readonly Color Gold = new Color(.72f, .60f, .34f);
        private static readonly Color Ivory = new Color(.92f, .92f, .86f);
        private static readonly Color Muted = new Color(.66f, .70f, .66f);
        private static readonly Color Emerald = new Color(.31f, .82f, .63f);
        private Bindings data;
        private Font font;
        private RectTransform canvasRoot, overlay, card, wandPage, passivePage;
        private UnityEngine.UI.Text walletText, statusText, guidanceText, pageTitle, hoverText;
        private UnityEngine.UI.Button wandTab, passiveTab;
        private WandRow[] rows;
        private UnityEngine.UI.Button[,] passiveButtons;
        private UnityEngine.UI.Text[,] passiveLabels;
        private bool initialized, preview, passiveSelected, resumeOnClose;
        private float nextRefresh;
        private Vector2 lastCanvasSize;
        readonly UnityEngine.UI.Image[] elementUpgradeImages011=new UnityEngine.UI.Image[4];
        readonly float[] elementUpgradeEnd011=new float[4];int elementUpgradeMask011;

        public bool Visible => overlay != null && overlay.gameObject.activeSelf;
        public bool RuntimeBindingsReady => initialized && RuntimeBindingsUsable() && RuntimeViewsUsable();
        public string LifecycleFailure { get; private set; } = "";
        public int LifecycleInvalidations { get; private set; }
        public bool IsReadOnly => !RuntimeBindingsReady || preview || !data.IsNearBlacksmith();
        public string InteractionHint
        {
            get
            {
                if (!EnsureRuntimeReady() || Visible || !data.IsNearBlacksmith() || data.IsLookingAtBlacksmith == null || !data.IsLookingAtBlacksmith()) return "";
                var key = data.InteractKeyLabel();
                return "Blacksmith [" + key + "] Shop";
            }
        }

        public void Initialize(RectTransform existingCanvasRoot, Bindings bindings)
        {
            if (initialized || !string.IsNullOrEmpty(LifecycleFailure))
                throw new InvalidOperationException("Initialize a new blacksmith panel once. An invalidated view requires a fresh owner/session.");
            if (existingCanvasRoot == null || existingCanvasRoot.GetComponentInParent<Canvas>() == null)
                throw new ArgumentException("Pass a RectTransform within the existing overlay canvas.");
            data = bindings ?? throw new ArgumentNullException(nameof(bindings));
            data.Validate();
            canvasRoot = existingCanvasRoot;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureInput(existingCanvasRoot.GetComponentInParent<Canvas>());
            Build();
            initialized = true;
            overlay.gameObject.SetActive(false);
            hoverText.gameObject.SetActive(false);
        }

        /// <summary>The caller checks its current bound Interact key and invokes this from a real NPC interaction.</summary>
        public void ShowShop()
        {
            RequireInitialized();
            if (!data.IsNearBlacksmith() || !data.IsLookingAtBlacksmith()) return;
            Open(false, false);
        }

        /// <summary>Use the rebound passive-tree action (default P). This entry point never spends currency.</summary>
        public void ShowPassiveOverview()
        {
            RequireInitialized();
            Open(true, true);
        }

        public void Hide(bool restorePreviousPause = true)
        {
            if (!EnsureRuntimeReady() || !Visible) return;
            ClearElementUpgradeFeedback011();
            overlay.gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (restorePreviousPause && resumeOnClose) data.SetPaused(false);
            data.Closed?.Invoke();
        }

        private void Open(bool readOnly, bool showPassives)
        {
            if (!Visible) resumeOnClose = !data.IsPaused();
            preview = readOnly;
            passiveSelected = showPassives;
            data.SetPaused(true);
            overlay.SetAsLastSibling();
            overlay.gameObject.SetActive(true);
            statusText.text = "";
            Refresh();
            Fit();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject((showPassives ? passiveTab : wandTab).gameObject);
        }

        private void Build()
        {
            hoverText = Text(canvasRoot, "Blacksmith2DHover", "", 20, Gold, TextAnchor.MiddleCenter,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -104), new Vector2(720, 38));
            var hoverShadow = hoverText.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            hoverShadow.effectColor = new Color(0, 0, 0, .95f);
            hoverShadow.effectDistance = new Vector2(1, -1);
            overlay = Stretch(canvasRoot, "BlacksmithOverlay");
            Paint(overlay, new Color(.012f, .022f, .016f, .82f), true);
            card = Rect(overlay, "BlacksmithCard", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1400, 940));
            Paint(card, new Color(.035f, .05f, .04f, .98f));
            Border(card, Gold);
            pageTitle = Text(card, "PageTitle", "BLACKSMITH", 34, Ivory, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -32), new Vector2(860, 52));
            walletText = Text(card, "ActualCrystalWallet", "", 21, Gold, TextAnchor.UpperRight,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-44, -40), new Vector2(420, 40));
            wandTab = Button(card, "WandTab", "WANDS", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(44, -108), new Vector2(290, 48), () => { passiveSelected = false; Refresh(); });
            passiveTab = Button(card, "PassiveTab", "PASSIVE TRAINING", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(354, -108), new Vector2(340, 48), () => { passiveSelected = true; Refresh(); });
            guidanceText = Text(card, "ActualShopAvailability", "", 16, Muted, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -176), new Vector2(1312, 64));
            wandPage = Rect(card, "FourWands", new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -245), new Vector2(1312, 526));
            passivePage = Rect(card, "FourPassiveBranches", new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -245), new Vector2(1312, 526));
            BuildWands();
            BuildPassives();
            BuildElementUpgradeFeedback011();
            statusText = Text(card, "PurchaseFeedback", "", 18, Ivory, TextAnchor.UpperLeft,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(44, 102), new Vector2(1000, 58));
            Button(card, "CloseBlacksmith", "RETURN", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-44, 34), new Vector2(240, 52), () => Hide());
        }

        void BuildElementUpgradeFeedback011()
        {
            for(int element=0;element<4;element++)
            {
                // Unused header space after the tabs, above the guidance text; same fitted card.
                var rect=Rect(card,"AcceptedElementUpgrade"+element,new Vector2(0,1),new Vector2(0,1),
                    new Vector2(734+48*element,-112),new Vector2(32,32));
                var image=Paint(rect,data.WandColors[element]);image.raycastTarget=false;
                image.sprite=OdysseyOracle.OracleElementEmblems.Get(element);image.preserveAspect=true;image.enabled=false;
                elementUpgradeImages011[element]=image;
            }
        }
        /// <summary>The bound shop calls this only after its existing save transaction succeeds.</summary>
        public void ShowAcceptedElementUpgrade011(int element)
        {
            if(!initialized||preview||!Visible||element<0||element>3||elementUpgradeImages011[element]==null)return;
            elementUpgradeEnd011[element]=Time.unscaledTime+.55f;elementUpgradeMask011|=1<<element;
            Color tint=data.WandColors[element];tint.a=.8f;elementUpgradeImages011[element].color=tint;
            elementUpgradeImages011[element].enabled=true;
        }
        void TickElementUpgradeFeedback011()
        {
            if(elementUpgradeMask011==0)return;
            if(!Visible||!Application.isFocused){ClearElementUpgradeFeedback011();return;}
            float now=Time.unscaledTime;
            for(int element=0;element<4;element++)if((elementUpgradeMask011&(1<<element))!=0)
            {
                var image=elementUpgradeImages011[element];float remaining=elementUpgradeEnd011[element]-now;
                if(image==null||remaining<=0){if(image!=null)image.enabled=false;elementUpgradeMask011&=~(1<<element);continue;}
                Color tint=data.WandColors[element];tint.a=.8f*Mathf.Clamp01(remaining/.55f);image.color=tint;
            }
        }
        void ClearElementUpgradeFeedback011()
        {
            if(elementUpgradeMask011==0)return;
            for(int element=0;element<4;element++)if(elementUpgradeImages011[element]!=null)elementUpgradeImages011[element].enabled=false;
            elementUpgradeMask011=0;
        }

        private void BuildWands()
        {
            rows = new WandRow[4];
            for (var element = 0; element < rows.Length; element++)
            {
                var e = element;
                var row = Rect(wandPage, "PlayerWand" + element, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(0, -element * 130), new Vector2(1312, 118));
                Paint(row, new Color(.06f, .085f, .067f));
                Border(row, new Color(.35f, .38f, .27f));
                Paint(Rect(row, "ElementColor", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(5, 118)), data.WandColors[element]);
                var view = new WandRow();
                view.Title = Text(row, "WandName", data.WandNames[element], 22, data.WandColors[element], TextAnchor.UpperLeft,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -12), new Vector2(330, 30));
                view.Level = Text(row, "ActualWandLevel", "", 16, Gold, TextAnchor.UpperLeft,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -48), new Vector2(270, 26));
                view.Effect = Text(row, "ActualWandEffect", "", 16, Ivory, TextAnchor.UpperLeft,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(325, -15), new Vector2(665, 88));
                view.Buy = Button(row, "BuyWandUpgrade", "", new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-18, -16), new Vector2(275, 60), () => BuyWand(e));
                view.BuyText = view.Buy.GetComponentInChildren<UnityEngine.UI.Text>();
                view.Availability = Text(row, "ActualUpgradeAvailability", "", 13, Muted, TextAnchor.UpperRight,
                    new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -84), new Vector2(275, 23));
                rows[element] = view;
            }
        }

        private void BuildPassives()
        {
            passiveButtons = new UnityEngine.UI.Button[4, 3];
            passiveLabels = new UnityEngine.UI.Text[4, 3];
            for (var element = 0; element < 4; element++)
            {
                var x = element * 334;
                Text(passivePage, "Branch" + element, data.WandNames[element].ToUpperInvariant(), 23, data.WandColors[element], TextAnchor.UpperCenter,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, 0), new Vector2(310, 34));
                var highlight = data.BranchHighlights != null && element < data.BranchHighlights.Length ? data.BranchHighlights[element] : "";
                Text(passivePage, "CallerVerifiedBranchHighlight" + element, highlight, 15, Muted, TextAnchor.UpperCenter,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -41), new Vector2(310, 66));
                for (var node = 0; node < 3; node++)
                {
                    var e = element;
                    var n = node;
                    var button = Button(passivePage, "Passive" + element + "_" + node, "", new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(x, -118 - node * 132), new Vector2(310, 116), () => BuyPassive(e, n));
                    passiveButtons[element, node] = button;
                    passiveLabels[element, node] = button.GetComponentInChildren<UnityEngine.UI.Text>();
                    passiveLabels[element, node].fontSize = 15;
                }
            }
        }

        private void Update()
        {
            if (!EnsureRuntimeReady()) return;
            TickElementUpgradeFeedback011();
            var hint = data.DrawHoverHint ? InteractionHint : "";
            hoverText.text = hint;
            hoverText.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            if (!Visible) return;
            nextRefresh -= Time.unscaledDeltaTime;
            if (nextRefresh <= 0) { nextRefresh = .1f; Refresh(); }
            if (canvasRoot.rect.size != lastCanvasSize) Fit();
        }

        private void Refresh()
        {
            if (!EnsureRuntimeReady()) return;
            var wallet = Mathf.Max(0, data.Wallet());
            var readOnly = IsReadOnly;
            walletText.text = data.CurrencyName.ToUpperInvariant() + "  " + wallet;
            pageTitle.text = readOnly ? "PASSIVE OVERVIEW" : "BLACKSMITH";
            guidanceText.text = readOnly
                ? "You have " + wallet + " " + data.CurrencyName + ". " + (data.IsNearBlacksmith()
                    ? "Open the blacksmith's shop to spend them." : "Visit the blacksmith to spend them.")
                : "Spend " + data.CurrencyName + " at the blacksmith to train your wands and available passives.";
            if (data.TrainingNote != null) guidanceText.text += "\n" + data.TrainingNote();
            wandPage.gameObject.SetActive(!passiveSelected);
            passivePage.gameObject.SetActive(passiveSelected);
            for (var element = 0; element < 4; element++)
            {
                var row = rows[element];
                var level = Mathf.Clamp(data.WandLevel(element), 1, 5);
                var cost = Mathf.Max(0, data.WandUpgradeCost(element));
                var capped = level >= 5;
                row.Level.text = "LEVEL " + level + " / 5";
                row.Effect.text = data.CurrentWandEffect != null ? data.CurrentWandEffect(element) : "";
                if (!capped && data.NextWandEffect != null)
                    row.Effect.text += (string.IsNullOrEmpty(row.Effect.text) ? "" : "\n") + "Next: " + data.NextWandEffect(element);
                row.BuyText.text = capped ? "LEVEL 5  ·  MAX" : "UPGRADE TO LEVEL " + (level + 1) + "\n" + cost + " " + data.CurrencyName.ToUpperInvariant();
                row.Buy.interactable = !readOnly && !capped && cost > 0 && wallet >= cost && data.CanBuyWand(element);
                row.Availability.text = capped ? "FULLY TRAINED" : readOnly ? "BLACKSMITH ONLY" : wallet < cost
                    ? "NEEDS " + (cost - wallet) + " MORE " + data.CurrencyName.ToUpperInvariant() : "";
                RefreshPassive(element, wallet, readOnly);
            }
        }

        private void RefreshPassive(int element, int wallet, bool readOnly)
        {
            var hasMetadata = data.PassiveName != null && data.PassiveDescription != null;
            var configured = hasMetadata && data.PassiveCost != null &&
                data.PassiveUnlocked != null && data.CanUnlockPassive != null && data.TryUnlockPassive != null;
            for (var node = 0; node < 3; node++)
            {
                var button = passiveButtons[element, node];
                var label = passiveLabels[element, node];
                if (!hasMetadata)
                {
                    label.text = "PASSIVE " + (node + 1);
                    button.interactable = false;
                    continue;
                }
                var owned = data.PassiveUnlocked != null && data.PassiveUnlocked(element, node);
                var cost = data.PassiveCost != null ? Mathf.Max(0, data.PassiveCost(element, node)) : 0;
                label.text = data.PassiveName(element, node) + "\n" + data.PassiveDescription(element, node)
                    + "\n" + (owned ? "LEARNED" : cost > 0 ? cost + " " + data.CurrencyName.ToUpperInvariant() : "VIEW ONLY");
                label.color = owned ? Emerald : Ivory;
                button.interactable = configured && !readOnly && !owned && cost > 0 && wallet >= cost && data.CanUnlockPassive(element, node);
            }
        }

        private void BuyWand(int element)
        {
            if (!EnsureRuntimeReady()) return;
            if (IsReadOnly) { statusText.text = "Visit the blacksmith to spend your " + data.CurrencyName + "."; return; }
            if (!data.CanBuyWand(element)) { ExplainWandFailure(element); Refresh(); return; }
            var purchased = data.TryBuyWand(element, data.IsNearBlacksmith());
            if (purchased) statusText.text = data.WandNames[element] + " trained to level " + data.WandLevel(element) + ".";
            else ExplainWandFailure(element);
            Refresh();
        }

        private void ExplainWandFailure(int element)
        {
            statusText.text = data.WandLevel(element) >= 5 ? "This wand is fully trained."
                : data.Wallet() < data.WandUpgradeCost(element) ? "You need more " + data.CurrencyName + "."
                : "This upgrade is not available. Check its requirements at the blacksmith.";
        }

        private void BuyPassive(int element, int node)
        {
            if (!EnsureRuntimeReady()) return;
            if (IsReadOnly || data.TryUnlockPassive == null) { statusText.text = "Visit the blacksmith to spend your " + data.CurrencyName + "."; return; }
            var purchased = data.TryUnlockPassive(element, node, data.IsNearBlacksmith());
            statusText.text = purchased ? data.PassiveName(element, node) + " learned."
                : "This passive needs sufficient " + data.CurrencyName + " and its preceding training.";
            Refresh();
        }

        private void Fit()
        {
            Canvas.ForceUpdateCanvases();
            lastCanvasSize = canvasRoot.rect.size;
            var scale = Mathf.Min(1, (lastCanvasSize.x - 64) / 1400, (lastCanvasSize.y - 64) / 940);
            card.localScale = Vector3.one * Mathf.Max(.1f, scale);
        }

        private void RequireInitialized()
        {
            if (!EnsureRuntimeReady()) throw new InvalidOperationException(string.IsNullOrEmpty(LifecycleFailure)
                ? "Initialize the panel before opening it." : LifecycleFailure);
        }

        // # ARCHITECTURE SNAPSHOT: view callbacks are runtime-only, not a saved
        // model. Reload recovery must come from a fresh owner/Play session; these
        // guards never rebuild a wallet, assume delegates survived or resume input.
        private static bool LiveCallback(Delegate callback, bool optional = false)
        {
            if (callback == null) return optional;
            return !(callback.Target is UnityEngine.Object owner) || owner != null;
        }

        private bool RuntimeBindingsUsable()
        {
            return data != null && data.WandNames != null && data.WandNames.Length == 4
                && data.WandColors != null && data.WandColors.Length == 4
                && LiveCallback(data.Wallet) && LiveCallback(data.WandLevel) && LiveCallback(data.WandUpgradeCost)
                && LiveCallback(data.CanBuyWand) && LiveCallback(data.TryBuyWand)
                && LiveCallback(data.IsNearBlacksmith) && LiveCallback(data.IsLookingAtBlacksmith)
                && LiveCallback(data.InteractKeyLabel) && LiveCallback(data.IsPaused) && LiveCallback(data.SetPaused)
                && LiveCallback(data.CurrentWandEffect, true) && LiveCallback(data.NextWandEffect, true)
                && LiveCallback(data.Closed, true) && LiveCallback(data.PassiveName, true)
                && LiveCallback(data.PassiveDescription, true) && LiveCallback(data.PassiveCost, true)
                && LiveCallback(data.PassiveUnlocked, true) && LiveCallback(data.CanUnlockPassive, true)
                && LiveCallback(data.TryUnlockPassive, true) && LiveCallback(data.TrainingNote, true);
        }

        private bool RuntimeViewsUsable()
        {
            if (canvasRoot == null || overlay == null || card == null || hoverText == null || walletText == null
                || statusText == null || guidanceText == null || pageTitle == null || wandPage == null || passivePage == null
                || wandTab == null || passiveTab == null || rows == null || rows.Length != 4
                || passiveButtons == null || passiveLabels == null || passiveButtons.GetLength(0) != 4
                || passiveButtons.GetLength(1) != 3 || passiveLabels.GetLength(0) != 4 || passiveLabels.GetLength(1) != 3) return false;
            for (var element = 0; element < 4; element++)
            {
                var row = rows[element];
                if (row == null || row.Title == null || row.Level == null || row.Effect == null || row.BuyText == null
                    || row.Availability == null || row.Buy == null) return false;
                for (var node = 0; node < 3; node++)
                    if (passiveButtons[element, node] == null || passiveLabels[element, node] == null) return false;
            }
            return true;
        }

        private bool EnsureRuntimeReady()
        {
            if (!initialized) return false;
            if (RuntimeBindingsUsable() && RuntimeViewsUsable()) return true;
            InvalidateRuntimeView();
            return false;
        }

        private void InvalidateRuntimeView()
        {
            // This one-way transition stops the Update exception loop. No owner
            // callbacks run because their target may already have been destroyed.
            if (!initialized) return;
            initialized = false;
            LifecycleInvalidations++;
            LifecycleFailure = "Blacksmith runtime callbacks or view objects were lost. Stop Play and start a fresh session; saved progression was not changed.";
            HideOwnedVisuals();
            ReleaseOwnedCallbacks();
            data = null;
            Debug.LogError(LifecycleFailure, this);
        }

        private void HideOwnedVisuals()
        {
            ClearElementUpgradeFeedback011();
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && canvasRoot != null && selected.transform.IsChildOf(canvasRoot))
                EventSystem.current.SetSelectedGameObject(null);
            if (overlay != null) overlay.gameObject.SetActive(false);
            if (hoverText != null) hoverText.gameObject.SetActive(false);
            resumeOnClose = false;
        }

        private void ReleaseOwnedCallbacks()
        {
            if (overlay == null) return;
            // Every button in this private subtree was constructed by this view.
            // RemoveAllListeners removes its runtime listeners, not persistent ones.
            foreach (var button in overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button != null) button.onClick.RemoveAllListeners();
        }

        private void OnEnable()
        {
            // AddComponent invokes this before Initialize: an empty new view is
            // valid. A surviving initialized view must retain its runtime graph.
            if (initialized) EnsureRuntimeReady();
        }
        private void OnDisable() { HideOwnedVisuals(); }
        private void OnDestroy()
        {
            HideOwnedVisuals();
            ReleaseOwnedCallbacks();
            initialized = false;
            data = null;
        }

        private static void EnsureInput(Canvas canvas)
        {
            if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var events = FindAnyObjectByType<EventSystem>();
            if (events == null) events = new GameObject("BlacksmithEventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var module = events.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = events.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
            foreach (var other in events.GetComponents<BaseInputModule>()) other.enabled = other == module;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Stretch(Transform parent, string name)
        {
            var rect = Rect(parent, name, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static UnityEngine.UI.Image Paint(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private UnityEngine.UI.Text Text(Transform parent, string name, string text, int size, Color color, TextAnchor alignment,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 dimensions)
        {
            var label = Rect(parent, name, anchor, pivot, position, dimensions).gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private UnityEngine.UI.Button Button(Transform parent, string name, string text, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, UnityEngine.Events.UnityAction clicked)
        {
            var rect = Rect(parent, name, anchor, pivot, position, size);
            var background = Paint(rect, new Color(.09f, .14f, .10f), true);
            Border(rect, Gold);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(clicked);
            var colors = button.colors;
            colors.highlightedColor = colors.selectedColor = new Color(1.5f, 1.4f, 1.15f);
            colors.pressedColor = new Color(.65f, .8f, .7f);
            button.colors = colors;
            var label = Text(rect, "Label", text, 16, Ivory, TextAnchor.MiddleCenter,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, size - new Vector2(16, 8));
            return button;
        }

        private static void Border(RectTransform parent, Color color)
        {
            for (var i = 0; i < 4; i++)
            {
                var edge = Rect(parent, "Border" + i, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                if (i < 2)
                {
                    edge.anchorMin = new Vector2(0, i);
                    edge.anchorMax = new Vector2(1, i);
                    edge.pivot = new Vector2(.5f, i);
                    edge.sizeDelta = new Vector2(0, 1);
                }
                else
                {
                    edge.anchorMin = new Vector2(i - 2, 0);
                    edge.anchorMax = new Vector2(i - 2, 1);
                    edge.pivot = new Vector2(i - 2, .5f);
                    edge.sizeDelta = new Vector2(1, 0);
                }
                Paint(edge, color);
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted elemental shop/UI/listener or cosmetic atlas space; no authority or saved-key changes.
// Array Formats: Four bounded clip/image identities and accepted-element bits; existing12models/fourcuevoices and workshop mesh.
// Dependencies/Pointers: Root follow-up imports four retained generated cues; baseline/replay/request failure stays silent, no hot loads/new audio sources or media/provider work here.
