using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        static readonly string[] acousticNames015 = { "BALANCED", "HEADPHONES", "SPEECH FOCUS", "CUSTOM" };
        Text acousticToggle015, acousticProfile015, acousticPlugin015, acousticNote015, acousticStrengthLabel015;
        Slider acousticStrength015;
        Button acousticPluginButton015;
        uint acousticUiRevision015;
        bool acousticUiInitialized015;
        public bool IsAcousticSettingsVisible015 => built && currentPanel == JourneyPanel.Acoustics015 && panels[JourneyPanel.Acoustics015].gameObject.activeInHierarchy;
        void BuildAcousticSettings015()
        {
            var content = PanelContent(JourneyPanel.Acoustics015, "PositionalSound015", new Vector2(860, 800));
            MenuLabel(content, "AcousticTitle015", "POSITIONAL SOUND", 30, Ivory, 58);
            var toggle = MenuButton(content, "Occlusion015", "WALL / TOWER MUFFLING: ON", () => {
                var audio = session.Audio; if (audio != null) audio.SetAcousticProfile015(audio.AcousticProfile015, !audio.AcousticEnabled015, audio.AcousticStrength015, audio.AcousticSpatializerRequested015); RefreshAcousticSettings015(true);
            }, 42); acousticToggle015 = toggle.GetComponentInChildren<Text>();
            var profile = MenuButton(content, "Profile015", "PROFILE: BALANCED", () => {
                var audio = session.Audio; if (audio != null) audio.SetAcousticProfile015((audio.AcousticProfile015 + 1) % 4, audio.AcousticEnabled015, audio.AcousticStrength015, audio.AcousticSpatializerRequested015); RefreshAcousticSettings015(true);
            }, 42); acousticProfile015 = profile.GetComponentInChildren<Text>();
            acousticStrength015 = VolumeRow(content, "CustomOcclusion015", "CUSTOM MUFFLING STRENGTH", value => {
                var audio = session.Audio; if (audio != null) audio.SetAcousticProfile015(3, audio.AcousticEnabled015, value, audio.AcousticSpatializerRequested015); RefreshAcousticSettings015(true);
            }, out acousticStrengthLabel015);
            acousticPluginButton015 = MenuButton(content, "SpatializerRequest015", "ENHANCED SPATIAL AUDIO: OFF", () => {
                var audio = session.Audio; if (audio != null) audio.SetAcousticProfile015(audio.AcousticProfile015, audio.AcousticEnabled015, audio.AcousticStrength015, !audio.AcousticSpatializerRequested015); RefreshAcousticSettings015(true);
            }, 42); acousticPlugin015 = acousticPluginButton015.GetComponentInChildren<Text>();
            MenuButton(content, "PositionTest015", "TEST LEFT, THEN RIGHT", () => {
                var audio = session.Audio; audio?.StartAudioFromGesture();
                if (audio == null || !audio.StartAcousticTest015()) acousticNote015.text = "Test unavailable while narration is playing, audio is muted or the existing recording is unavailable.";
                else acousticNote015.text = "One existing monster cue plays on your left, then your right. Headphones help compare direction.";
            }, 42);
            acousticNote015 = MenuLabel(content, "AcousticStatus015", "Captions and narration remain separately controlled.", 15, Muted, 144);
            MenuButton(content, "AcousticsBack015", "BACK TO AUDIO", () => SetPanel(JourneyPanel.Audio), 46);
        }
        void RefreshAcousticSettings015(bool force = false)
        {
            if (!IsAcousticSettingsVisible015 || session.Audio == null || acousticNote015 == null) return;
            var audio = session.Audio;
            if (!force && acousticUiInitialized015 && acousticUiRevision015 == audio.AcousticUiRevision015) return;
            acousticUiInitialized015 = true; acousticUiRevision015 = audio.AcousticUiRevision015;
            acousticToggle015.text = audio.AcousticEnabled015 ? "WALL / TOWER MUFFLING: ON" : "WALL / TOWER MUFFLING: OFF";
            acousticProfile015.text = "PROFILE: " + acousticNames015[audio.AcousticProfile015];
            acousticStrength015.SetValueWithoutNotify(audio.AcousticStrength015);
            acousticStrengthLabel015.text = Mathf.RoundToInt(audio.AcousticStrength015 * 100) + "%";
            acousticPlugin015.text = audio.AcousticSpatializerRequested015 ? "ENHANCED SPATIAL AUDIO: REQUESTED" : "ENHANCED SPATIAL AUDIO: OFF";
            acousticPluginButton015.interactable = audio.AcousticNativePluginAvailable015 || audio.ExternalVoiceStart015 != null;
            acousticNote015.text = audio.AcousticBackendLabel015 + "\nWall/tower muffling uses a limited direct-path estimate. It does not simulate sound reflecting around corners. Captions and narration remain separately controlled.";
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing local uGUI Audio Settings subpanel only.
// Array Formats:Four static profile labels, fixed cold controls and revision-gated text; no new frame-loop formatting.
// Dependencies/Pointers:Existing GameAudioDirector owns all tests/preferences/backends. UI never loads/generates audio or changes gameplay/narration ownership; root accepts device/browser behavior.
