using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RefugeActivityDirector
    {
        DungeonWorld npcVoiceWorld020;
        RectTransform npcSubtitleFrame020, npcSubtitleCanvas020;
        UnityEngine.UI.Image npcSubtitleBackground020;
        OracleInterfacePreferences npcSubtitlePreferences020;
        Vector2 npcSubtitleCanvasSize020 = new Vector2(-1, -1);
        int npcSubtitleFontSize020 = -1;
        bool npcSubtitleHasLine020, npcSubtitleLayoutDirty020 = true;

        // One actual Activity source owns NPC speech; reading-panel visibility is
        // admission evidence, not the lifetime of an already accepted utterance.
        public bool NpcDialogueSpeaking020 => npcVoiceOwner009 != null && voice != null &&
            npcSpeakingClip009 != null && voice.clip == npcSpeakingClip009 && voice.isPlaying &&
            !voice.mute && voice.volume > 0 && AudioListener.volume > 0 &&
            !AudioListener.pause && Application.isFocused;

        bool NpcVoiceLeaseValid020()
        {
            if (!isActiveAndEnabled || npcVoiceOwner009 == null || session == null || !session.isActiveAndEnabled ||
                !session.HasStarted || session.IsDead || session.IsDowned || session.Victory || !Application.isFocused ||
                session.World == null || !object.ReferenceEquals(session.World, npcVoiceWorld020)) return false;
            if (npcVoiceContext009 == 5) return CampaignNpcLeaseValid020();
            if (!session.World.IsTutorialRefuge || session.World.IsCoopArena) return false;
            if (npcVoiceOwner009 is Behaviour owner && !owner.isActiveAndEnabled) return false;
            if (npcVoiceContext009 == 1) return (npcVoiceRole009 == 2 || npcVoiceRole009 == 3) &&
                object.ReferenceEquals(npcVoiceOwner009, session.RefugeNpcs);
            if (npcVoiceContext009 == 4) return npcVoiceRole009 == 4 && object.ReferenceEquals(npcVoiceOwner009, this);
            if (npcVoiceRole009 != 1) return false;
            return npcVoiceContext009 == 2 ? object.ReferenceEquals(npcVoiceOwner009, session.FieldSmith) :
                npcVoiceContext009 == 3 && object.ReferenceEquals(npcVoiceOwner009, session.Shop);
        }

        void InitializeNpcSubtitle020(Canvas ownerCanvas)
        {
            if (ownerCanvas == null || npcSubtitleFrame020 != null) return;
            npcSubtitleCanvas020 = ownerCanvas.transform as RectTransform;
            npcSubtitlePreferences020 = ownerCanvas.GetComponent<OracleInterfacePreferences>();
            var frame = new GameObject("NpcSubtitleFrame020", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
            frame.transform.SetParent(ownerCanvas.transform, false);
            npcSubtitleFrame020 = (RectTransform)frame.transform;
            npcSubtitleFrame020.anchorMin = npcSubtitleFrame020.anchorMax = new Vector2(.5f, 0);
            npcSubtitleFrame020.pivot = new Vector2(.5f, 0);
            npcSubtitleFrame020.anchoredPosition = new Vector2(0, 92);
            npcSubtitleFrame020.sizeDelta = new Vector2(820, 88);
            npcSubtitleBackground020 = frame.GetComponent<UnityEngine.UI.Image>();
            npcSubtitleBackground020.raycastTarget = false;
            var group = frame.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            var text = new GameObject("NpcCurrentUtterance020", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            text.transform.SetParent(frame.transform, false);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(14, 10); rect.offsetMax = new Vector2(-14, -10);
            npcCaption009 = text.GetComponent<UnityEngine.UI.Text>();
            // Reuse the project's explicitly requested built-in runtime font.
            npcCaption009.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            npcCaption009.alignment = TextAnchor.MiddleCenter;
            npcCaption009.color = new Color(1, .97f, .88f, 1);
            npcCaption009.raycastTarget = false; npcCaption009.supportRichText = false;
            npcCaption009.horizontalOverflow = HorizontalWrapMode.Wrap;
            npcCaption009.verticalOverflow = VerticalWrapMode.Truncate;
            var outline = text.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(.01f, .01f, .015f, .94f); outline.effectDistance = new Vector2(1, -1);
            frame.SetActive(false);
        }

        void BeginNpcSubtitle020(string caption)
        {
            npcSubtitleHasLine020 = !string.IsNullOrEmpty(caption);
            if (npcCaption009 != null) npcCaption009.text = caption ?? "";
            npcSubtitleLayoutDirty020 = true;
            if (npcSubtitleFrame020 != null) npcSubtitleFrame020.SetAsLastSibling();
            TickNpcSubtitle020();
        }

        void HideNpcSubtitle020()
        {
            npcSubtitleHasLine020 = false;
            if (npcCaption009 != null) npcCaption009.text = "";
            if (npcSubtitleFrame020 != null) npcSubtitleFrame020.gameObject.SetActive(false);
        }

        void TickNpcSubtitle020()
        {
            if (npcSubtitleFrame020 == null || npcCaption009 == null || npcSubtitleCanvas020 == null) return;
            bool show = npcSubtitleHasLine020 && (npcSubtitlePreferences020 == null || npcSubtitlePreferences020.NpcSubtitlesEnabled);
            if (npcSubtitleFrame020.gameObject.activeSelf != show) npcSubtitleFrame020.gameObject.SetActive(show);
            if (!show) return;
            bool background = npcSubtitlePreferences020 == null || npcSubtitlePreferences020.NpcSubtitleBackground;
            npcSubtitleBackground020.color = background ? new Color(.018f, .022f, .029f, .80f) : Color.clear;
            int fontSize = npcSubtitlePreferences020 != null ? npcSubtitlePreferences020.NpcSubtitleFontSize : 20;
            Vector2 size = npcSubtitleCanvas020.rect.size;
            if (!npcSubtitleLayoutDirty020 && npcSubtitleFontSize020 == fontSize && size == npcSubtitleCanvasSize020) return;
            if (size.x <= 48 || size.y <= 0) return;
            npcSubtitleLayoutDirty020 = false; npcSubtitleFontSize020 = fontSize; npcSubtitleCanvasSize020 = size;
            npcCaption009.fontSize = fontSize;
            float width = Mathf.Min(820, size.x - 48);
            npcSubtitleFrame020.sizeDelta = new Vector2(width, npcSubtitleFrame020.sizeDelta.y);
            // Only a changed immutable utterance, size preference or viewport
            // rebuilds layout. Never split/format the caption in the frame loop.
            float height = Mathf.Max(48, Mathf.Ceil(npcCaption009.preferredHeight) + 24);
            npcSubtitleFrame020.sizeDelta = new Vector2(width, height);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Canvas reference pixels; NPC lifetime remains in the accepted refuge/world instance.
// Array Formats: One borrowed exact caption, one compact non-interactive panel and scalar cached preferences/layout; no queue or runtime text arrays.
// Dependencies/Pointers: Existing single Activity AudioSource/clip/owner; existing interface preference owner; close does not revoke speech, new owner/world/disable/focus does.
