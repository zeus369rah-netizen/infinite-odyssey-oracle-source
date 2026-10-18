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
        public static readonly Vector2 BaseReferenceResolution = new Vector2(1920, 1080);
        private UnityEngine.UI.CanvasScaler scaler;
        private bool initialized, dirty;
        private float saveAt;
        public float UiScale { get; private set; } = 1f;
        public int MinimapSize { get; private set; } = 1;
        public float MinimapOpacity { get; private set; } = 1f;
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
