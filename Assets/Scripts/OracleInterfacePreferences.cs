using System;
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>Local presentation preferences only. Does not touch game resolution, input or progression saves.</summary>
    [DisallowMultipleComponent]
    public sealed class OracleInterfacePreferences : MonoBehaviour
    {
        private const string ScaleKey = "OdysseyOracle.Interface.UiScale";
        private const string MapSizeKey = "OdysseyOracle.Interface.MinimapSize";
        private const string MapOpacityKey = "OdysseyOracle.Interface.MinimapOpacity";
        private const string NpcSubtitlesKey = "OdysseyOracle.Interface.NpcSubtitles";
        private const string NpcSubtitleSizeKey = "OdysseyOracle.Interface.NpcSubtitleSize";
        private const string NpcSubtitleBackgroundKey = "OdysseyOracle.Interface.NpcSubtitleBackground";
        private const string CampaignSeenKey020 = "OdysseyOracle.Story.CampaignIntroSeen020";
        public static readonly Vector2 BaseReferenceResolution = new Vector2(1920, 1080);
        private UnityEngine.UI.CanvasScaler scaler;
        private bool initialized, dirty;
        private float saveAt;
        public float UiScale { get; private set; } = 1f;
        public int MinimapSize { get; private set; } = 1;
        public float MinimapOpacity { get; private set; } = 1f;
        public bool NpcSubtitlesEnabled { get; private set; } = true;
        public int NpcSubtitleSize { get; private set; } = 1;
        public bool NpcSubtitleBackground { get; private set; } = true;
        public bool CampaignIntroSeen020 { get; private set; }
        public int NpcSubtitleFontSize => NpcSubtitleSize == 0 ? 16 : NpcSubtitleSize == 2 ? 24 : 20;
        public string SaveStatus { get; private set; } = "Interface preferences stay on this device.";
        public float MinimapSizeFactor => MinimapSize == 0 ? .8f : MinimapSize == 2 ? 1.23f : 1f;
        public event Action Changed;

        public void Initialize(UnityEngine.UI.CanvasScaler target)
        {
            if (initialized || target == null) return;
            initialized = true;
            scaler = target;
            UiScale = ClampFinite(PlayerPrefs.GetFloat(ScaleKey, 1), .75f, 1.35f, 1);
            MinimapSize = Mathf.Clamp(PlayerPrefs.GetInt(MapSizeKey, 1), 0, 2);
            MinimapOpacity = ClampFinite(PlayerPrefs.GetFloat(MapOpacityKey, 1), .2f, 1, 1);
            NpcSubtitlesEnabled = PlayerPrefs.GetInt(NpcSubtitlesKey, 1) != 0;
            NpcSubtitleSize = Mathf.Clamp(PlayerPrefs.GetInt(NpcSubtitleSizeKey, 1), 0, 2);
            NpcSubtitleBackground = PlayerPrefs.GetInt(NpcSubtitleBackgroundKey, 1) != 0;
            CampaignIntroSeen020 = PlayerPrefs.GetInt(CampaignSeenKey020, 0) != 0;
            ApplyScale();
        }

        public void SetUiScale(float value)
        {
            value = ClampFinite(value, .75f, 1.35f, 1);
            if (Mathf.Approximately(UiScale, value)) return;
            UiScale = value;
            ApplyScale();
            MarkChanged();
        }

        public void SetMinimapSize(int value)
        {
            value = Mathf.Clamp(value, 0, 2);
            if (MinimapSize == value) return;
            MinimapSize = value;
            MarkChanged();
        }

        public void SetMinimapOpacity(float value)
        {
            value = ClampFinite(value, .2f, 1, 1);
            if (Mathf.Approximately(MinimapOpacity, value)) return;
            MinimapOpacity = value;
            MarkChanged();
        }

        public void SetNpcSubtitlesEnabled(bool value)
        {
            if (NpcSubtitlesEnabled == value) return;
            NpcSubtitlesEnabled = value; MarkChanged();
        }
        public void SetNpcSubtitleSize(int value)
        {
            value = Mathf.Clamp(value, 0, 2);
            if (NpcSubtitleSize == value) return;
            NpcSubtitleSize = value; MarkChanged();
        }
        public void SetNpcSubtitleBackground(bool value)
        {
            if (NpcSubtitleBackground == value) return;
            NpcSubtitleBackground = value; MarkChanged();
        }

        public void MarkCampaignIntroSeen020()
        {
            if(CampaignIntroSeen020)return;
            CampaignIntroSeen020=true;MarkChanged();
        }

        public void ResetMinimapDefaults()
        {
            MinimapSize = 1;
            MinimapOpacity = 1;
            MarkChanged();
            SaveNow();
        }

        public void ResetDefaults()
        {
            UiScale = 1;
            MinimapSize = 1;
            MinimapOpacity = 1;
            NpcSubtitlesEnabled = true; NpcSubtitleSize = 1; NpcSubtitleBackground = true;
            ApplyScale();
            MarkChanged();
            SaveNow();
        }

        private void ApplyScale()
        {
            if (scaler == null) return;
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = BaseReferenceResolution / UiScale;
        }

        private void MarkChanged()
        {
            dirty = true;
            saveAt = Time.unscaledTime + .25f;
            Changed?.Invoke();
        }

        private void Update() { if (dirty && Time.unscaledTime >= saveAt) SaveNow(); }
        private void OnApplicationFocus(bool focused) { if (!focused) SaveNow(); }
        private void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        private void OnDisable() => SaveNow();
        private void OnApplicationQuit() => SaveNow();
        private void SaveNow()
        {
            if (!dirty || !initialized) return;
            try
            {
                PlayerPrefs.SetFloat(ScaleKey, UiScale);
                PlayerPrefs.SetInt(MapSizeKey, MinimapSize);
                PlayerPrefs.SetFloat(MapOpacityKey, MinimapOpacity);
                PlayerPrefs.SetInt(NpcSubtitlesKey, NpcSubtitlesEnabled ? 1 : 0);
                PlayerPrefs.SetInt(NpcSubtitleSizeKey, NpcSubtitleSize);
                PlayerPrefs.SetInt(NpcSubtitleBackgroundKey, NpcSubtitleBackground ? 1 : 0);
                PlayerPrefs.SetInt(CampaignSeenKey020, CampaignIntroSeen020 ? 1 : 0);
                PlayerPrefs.Save();
                SaveStatus = "Interface preferences saved on this device.";
            }
            catch (Exception) { SaveStatus = "These preferences apply now, but this device could not save them."; }
            dirty = false;
        }
        private static float ClampFinite(float value, float minimum, float maximum, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum);
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing device-local interface settings, no actor/progression coordinates.
// Array Formats: Three subtitle scalars added to existing bounded preference owner.
// Dependencies/Pointers: Existing debounced save/focus/disable events persist visibility, text size and background; map-only reset remains separate.
