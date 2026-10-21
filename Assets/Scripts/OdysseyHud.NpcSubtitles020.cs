using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        UnityEngine.UI.Text npcSubtitleEnabledLabel020, npcSubtitleSizeLabel020, npcSubtitleBackgroundLabel020;
        bool npcSubtitleSettingsBound020;
        static readonly string[] NpcSubtitleSizeLabels020 = {
            "SUBTITLE TEXT: SMALL", "SUBTITLE TEXT: MEDIUM", "SUBTITLE TEXT: LARGE"
        };

        // One caller-owned insertion in the existing scrollable Settings content.
        void BuildDialogueSettings020(RectTransform content)
        {
            MenuLabel(content, "NpcSubtitlesTitle020", "NPC SUBTITLES", 16, Gold, 30);
            npcSubtitleEnabledLabel020 = MenuButton(content, "NpcSubtitlesVisible020", "", ToggleNpcSubtitles020, 38).GetComponentInChildren<UnityEngine.UI.Text>();
            npcSubtitleSizeLabel020 = MenuButton(content, "NpcSubtitlesSize020", "", CycleNpcSubtitleSize020, 38).GetComponentInChildren<UnityEngine.UI.Text>();
            npcSubtitleBackgroundLabel020 = MenuButton(content, "NpcSubtitlesBackground020", "", ToggleNpcSubtitleBackground020, 38).GetComponentInChildren<UnityEngine.UI.Text>();
            MenuLabel(content, "NpcSubtitleScope020", "The current spoken line can finish after you close its reading panel. Subtitle settings do not change voice volume.", 14, Muted, 58);
            if (!npcSubtitleSettingsBound020 && interfacePreferences != null)
            {
                interfacePreferences.Changed += RefreshDialogueSettings020;
                npcSubtitleSettingsBound020 = true;
            }
            RefreshDialogueSettings020();
        }

        void DetachDialogueSettings020()
        {
            if (npcSubtitleSettingsBound020 && interfacePreferences != null)
                interfacePreferences.Changed -= RefreshDialogueSettings020;
            npcSubtitleSettingsBound020 = false;
        }

        void ToggleNpcSubtitles020()
        {
            if (interfacePreferences != null) interfacePreferences.SetNpcSubtitlesEnabled(!interfacePreferences.NpcSubtitlesEnabled);
        }
        void CycleNpcSubtitleSize020()
        {
            if (interfacePreferences != null) interfacePreferences.SetNpcSubtitleSize((interfacePreferences.NpcSubtitleSize + 1) % 3);
        }
        void ToggleNpcSubtitleBackground020()
        {
            if (interfacePreferences != null) interfacePreferences.SetNpcSubtitleBackground(!interfacePreferences.NpcSubtitleBackground);
        }
        void RefreshDialogueSettings020()
        {
            if (interfacePreferences == null) return;
            if (npcSubtitleEnabledLabel020 != null) npcSubtitleEnabledLabel020.text = interfacePreferences.NpcSubtitlesEnabled ? "NPC SUBTITLES: ON" : "NPC SUBTITLES: OFF";
            if (npcSubtitleSizeLabel020 != null) npcSubtitleSizeLabel020.text = NpcSubtitleSizeLabels020[interfacePreferences.NpcSubtitleSize];
            if (npcSubtitleBackgroundLabel020 != null) npcSubtitleBackgroundLabel020.text = interfacePreferences.NpcSubtitleBackground ? "SUBTITLE BACKGROUND: ON" : "SUBTITLE BACKGROUND: OFF";
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing settings scroll content and Canvas reference pixels; no gameplay or pointer authority.
// Array Formats: Three cached control labels and three fixed size strings; cold UI construction and explicit preference changes only.
// Dependencies/Pointers: Existing OracleInterfacePreferences owns persistence/events; parent HUD invokes one build and one detach hook; narration/resources remain unchanged.
