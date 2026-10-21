using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        const string AchievementAtlasResource020 = "UI/AchievementBadges020/badges020";
        Texture2D achievementAtlas020; // Borrowed from Resources. Only the Sprite wrappers are owned here.
        Sprite[] achievementSprites020;
        RectTransform achievementGrid020, achievementPaper020;
        UnityEngine.UI.GridLayoutGroup achievementLayout020;
        UnityEngine.UI.LayoutElement achievementGridHeight020, achievementViewHeight020;
        UnityEngine.UI.Text achievementSaveState020;
        UnityEngine.UI.Image[] achievementIcons020, achievementCards020;
        UnityEngine.UI.Text[] achievementNames020, achievementStates020;
        GameObject achievementReadingControls020;
        GameObject[] achievementPaperMarks020;
        bool[] achievementPaperMarkStates020;
        Vector2 achievementOriginalPaperSize020, achievementLastBounds020;
        float achievementOriginalViewMin020, achievementOriginalViewPreferred020;
        bool achievementShowing020;

        void EnsureAchievementBadges020()
        {
            if (achievementGrid020 != null) return;
            achievementPaper020 = panels[JourneyPanel.Lore];
            achievementOriginalPaperSize020 = achievementPaper020.sizeDelta;
            achievementViewHeight020 = loreScroll.GetComponent<UnityEngine.UI.LayoutElement>();
            achievementOriginalViewMin020 = achievementViewHeight020.minHeight;
            achievementOriginalViewPreferred020 = achievementViewHeight020.preferredHeight;
            achievementReadingControls020 = loreShowFull.transform.parent.gameObject;
            // These fixed paper fibres belong to the full-size book; hide them only while its size adapts.
            achievementPaperMarks020 = new GameObject[12];
            achievementPaperMarkStates020 = new bool[12];
            for (int i = 0; i < achievementPaperMarks020.Length; ++i)
            {
                string name = i == 0 ? "PaperLeftShade" : i == 1 ? "PaperRightShade" : "StaticPaperFibre" + (i - 2);
                var mark = achievementPaper020.Find(name);
                if (mark == null) continue;
                achievementPaperMarks020[i] = mark.gameObject;
                achievementPaperMarkStates020[i] = mark.gameObject.activeSelf;
            }

            achievementAtlas020 = Resources.Load<Texture2D>(AchievementAtlasResource020);
            achievementSprites020 = new Sprite[AchievementBadgeRules020.AtlasCount];
            if (achievementAtlas020 != null)
                for (int i = 0; i < achievementSprites020.Length; ++i)
                {
                    var cell = AchievementBadgeRules020.AtlasCell(i, achievementAtlas020.width, achievementAtlas020.height);
                    if (cell.Width == 0 || cell.Height == 0) continue;
                    var sprite = Sprite.Create(achievementAtlas020,
                        new Rect(cell.X, cell.Y, cell.Width, cell.Height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                    sprite.name = "AchievementBadge020_" + i;
                    sprite.hideFlags = HideFlags.DontSave;
                    achievementSprites020[i] = sprite;
                }

            achievementGrid020 = Rect(loreScroll.content, "AchievementCards020", new Vector2(.5f, 1),
                new Vector2(.5f, 1), Vector2.zero, Vector2.zero);
            achievementGrid020.anchorMin = new Vector2(0, 1);
            achievementGrid020.anchorMax = new Vector2(1, 1);
            achievementLayout020 = achievementGrid020.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            achievementLayout020.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            achievementLayout020.constraintCount = 2;
            achievementLayout020.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
            achievementLayout020.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
            achievementLayout020.childAlignment = TextAnchor.UpperCenter;
            achievementLayout020.spacing = new Vector2(AchievementBadgeRules020.Gap, AchievementBadgeRules020.Gap);
            achievementLayout020.cellSize = new Vector2(AchievementBadgeRules020.CardWidth, AchievementBadgeRules020.CardHeight);
            achievementGridHeight020 = achievementGrid020.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            // Override the grid's minimum width so its parent can shrink before the column rule is recomputed.
            achievementGridHeight020.minWidth = achievementGridHeight020.preferredWidth = 0;
            achievementGridHeight020.flexibleWidth = 1;
            achievementGridHeight020.flexibleHeight = 0;
            achievementCards020 = new UnityEngine.UI.Image[AchievementBadgeRules020.DisplayCount];
            achievementIcons020 = new UnityEngine.UI.Image[AchievementBadgeRules020.DisplayCount];
            achievementNames020 = new UnityEngine.UI.Text[AchievementBadgeRules020.DisplayCount];
            achievementStates020 = new UnityEngine.UI.Text[AchievementBadgeRules020.DisplayCount];
            var ink = new Color(.23f, .18f, .115f, 1);
            for (int i = 0; i < achievementCards020.Length; ++i)
            {
                var card = Rect(achievementGrid020, "AchievementCard020_" + i, Vector2.zero,
                    new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 132));
                achievementCards020[i] = Paint(card, new Color(.90f, .87f, .80f, 1));
                var icon = Rect(card, "Badge", new Vector2(0, .5f), new Vector2(0, .5f),
                    new Vector2(16, 0), new Vector2(64, 64));
                achievementIcons020[i] = Paint(icon, Color.white);
                achievementIcons020[i].preserveAspect = true;
                var title = Label(card, "AchievementName", "SECRET", 18, ink, TextAnchor.UpperLeft,
                    Vector2.zero, Vector2.zero, new Vector2(0, 1));
                title.rectTransform.anchorMax = new Vector2(1, 1);
                title.rectTransform.offsetMin = new Vector2(96, -88);
                title.rectTransform.offsetMax = new Vector2(-14, -14);
                achievementNames020[i] = title;
                var state = Label(card, "AchievementState", "SECRET", 13, ink, TextAnchor.LowerLeft,
                    Vector2.zero, Vector2.zero, Vector2.zero);
                state.rectTransform.anchorMax = new Vector2(1, 0);
                state.rectTransform.offsetMin = new Vector2(96, 14);
                state.rectTransform.offsetMax = new Vector2(-14, 36);
                achievementStates020[i] = state;
            }
            achievementSaveState020 = MenuLabel(loreScroll.content, "AchievementSaveState020", "", 14, ink, 132);
            achievementGrid020.gameObject.SetActive(false);
            achievementSaveState020.gameObject.SetActive(false);
        }

        void ShowAchievementBadges020()
        {
            if (!built || session == null || !session.HasStarted || session.IsDead || session.IsDowned || session.Victory || ShopVisible) return;
            HideNibwickControls020();
            EnsureAchievementBadges020();
            ForgetRefugeNpcConversation();
            AbilityBindings.CancelRebind();
            session.Pause();
            session.Audio?.PlayCue("book_open");
            StopBookReveal();
            achievementShowing020 = true;
            loreBody.gameObject.SetActive(false);
            achievementReadingControls020.SetActive(false);
            for (int i = 0; i < achievementPaperMarks020.Length; ++i)
                if (achievementPaperMarks020[i] != null) achievementPaperMarks020[i].SetActive(false);
            achievementGrid020.gameObject.SetActive(true);
            achievementSaveState020.gameObject.SetActive(true);
            loreTitle.text = "Oracle achievements";
            var progress = session.RefugeActivities;
            for (int row = 0; row < achievementCards020.Length; ++row)
            {
                bool extra = row == AchievementBadgeRules020.OriginalCount;
                bool earned = progress != null && (extra ? progress.AllRefugeFlamesOutEarned020 : progress.IsAchievementEarned(row));
                bool visible = progress != null && (extra ? progress.AllRefugeFlamesOutNameVisible020 : progress.IsAchievementNameVisible(row));
                bool known = earned || visible;
                int cell = AchievementBadgeRules020.DisplayCell(row, earned, visible);
                achievementNames020[row].text = known
                    ? (extra ? RefugeFlameRules020.AchievementName : DiscoveryCatalog.AchievementName(row)) : "SECRET";
                achievementStates020[row].text = earned ? "EARNED" : known ? "NOT YET" : "SECRET";
                achievementCards020[row].color = earned ? new Color(.86f, .91f, .78f, 1)
                    : known ? new Color(.94f, .90f, .80f, 1) : new Color(.88f, .86f, .80f, 1);
                achievementIcons020[row].sprite = achievementSprites020[cell];
                achievementIcons020[row].enabled = achievementSprites020[cell] != null;
                achievementIcons020[row].color = earned ? Color.white : new Color(.85f, .85f, .85f, 1);
            }
            string status = progress == null ? "" : progress.DiscoverySaveStatus;
            string flames = progress == null ? "" : progress.RefugeFlamesSaveStatus020;
            if (!string.IsNullOrEmpty(flames) && !progress.AllRefugeFlamesOutNameVisible020 && !progress.AllRefugeFlamesOutEarned020)
                flames = "A hidden achievement's saved data could not be read safely. It was kept; its saving is unavailable.";
            achievementSaveState020.text = string.IsNullOrEmpty(status) ? flames
                : string.IsNullOrEmpty(flames) ? status : status + "\n" + flames;
            achievementSaveState020.gameObject.SetActive(!string.IsNullOrEmpty(achievementSaveState020.text));
            SetPanel(JourneyPanel.Lore);
            loreInk.SetPresentationActive(false);
            loreHint.text = achievementAtlas020 != null
                ? "Scroll / Page Up / Page Down. Hidden names and badges stay SECRET until discovered."
                : "Badge artwork is unavailable. Your saved achievements are still shown. Scroll to read.";
            achievementLastBounds020 = new Vector2(-1, -1);
            RefreshAchievementBadgeLayout020();
            Canvas.ForceUpdateCanvases();
            loreScroll.StopMovement();
            loreScroll.verticalNormalizedPosition = 1;
        }

        void RefreshAchievementBadgeLayout020()
        {
            if (!achievementShowing020 || currentPanel != JourneyPanel.Lore || canvas == null) return;
            // The ordinary fitter runs first; this mode retains readable card dimensions inside the available Canvas.
            achievementPaper020.localScale = Vector3.one;
            var bounds = (canvas.transform as RectTransform).rect.size;
            if (achievementLastBounds020 == bounds) return;
            achievementLastBounds020 = bounds;
            float width = Mathf.Max(240, Mathf.Min(1000, bounds.x - 32));
            float height = Mathf.Max(360, Mathf.Min(920, bounds.y - 32));
            achievementPaper020.sizeDelta = new Vector2(width, height);
            // 76 padding + 64 title + 32 hint + 49 return + three 11-point gaps.
            float viewHeight = Mathf.Max(96, height - 254);
            achievementViewHeight020.minHeight = achievementViewHeight020.preferredHeight = viewHeight;
            Canvas.ForceUpdateCanvases();
            var layout = AchievementBadgeRules020.Measure(achievementGrid020.rect.width);
            achievementLayout020.constraintCount = layout.Columns;
            achievementLayout020.cellSize = new Vector2(layout.Width, AchievementBadgeRules020.CardHeight);
            achievementGridHeight020.minHeight = achievementGridHeight020.preferredHeight = layout.Height;
            UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(loreScroll.content);
        }

        void HideAchievementBadges020()
        {
            if (!achievementShowing020) return;
            achievementShowing020 = false;
            achievementGrid020.gameObject.SetActive(false);
            achievementSaveState020.gameObject.SetActive(false);
            loreBody.gameObject.SetActive(true);
            achievementReadingControls020.SetActive(true);
            for (int i = 0; i < achievementPaperMarks020.Length; ++i)
                if (achievementPaperMarks020[i] != null) achievementPaperMarks020[i].SetActive(achievementPaperMarkStates020[i]);
            achievementPaper020.sizeDelta = achievementOriginalPaperSize020;
            achievementViewHeight020.minHeight = achievementOriginalViewMin020;
            achievementViewHeight020.preferredHeight = achievementOriginalViewPreferred020;
            lastScreenSize = new Vector2(-1, -1);
            FitExpandedPanels();
            RefreshBookRevealControls(false);
        }

        void DisposeAchievementBadges020()
        {
            if (achievementSprites020 != null)
                for (int i = 0; i < achievementSprites020.Length; ++i)
                    if (achievementSprites020[i] != null) Destroy(achievementSprites020[i]);
            achievementSprites020 = null;
            achievementAtlas020 = null; // Never destroy or unload the borrowed Resources texture.
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Lore ScrollRect/Canvas reference pixels; integer 5x5 atlas cells, 300x132 cards and 64-point badges.
// Array Formats: Cold fixed arrays for 25 owned Sprite wrappers, 21 cached card views and 12 borrowed book decorations; scalar resize check per active frame.
// Dependencies/Pointers: Resources owns the borrowed texture; HUD destroys only its Sprite wrappers. Existing discovery/flame adapters own saves and reward authority.
