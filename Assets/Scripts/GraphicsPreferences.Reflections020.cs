using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GraphicsPreferences
    {
        const string ReflectionKey020="Oracle.Graphics.Reflections020";
        static readonly int ReflectionStrengthId020=Shader.PropertyToID("_OracleReflectionStrength020");
        float originalReflectionStrength020,originalReflectionIntensity020,lastReflectionStrength020,lastReflectionIntensity020;
        bool reflectionLease020,reflectionApplied020;
        public bool ReflectionsEnabled020 {get;private set;}=true;

        void CaptureReflectionPolicy020()
        {
            if(reflectionLease020)return;
            originalReflectionStrength020=Shader.GetGlobalFloat(ReflectionStrengthId020);
            float value=RenderSettings.reflectionIntensity;
            originalReflectionIntensity020=float.IsNaN(value)||float.IsInfinity(value)||value<0f?1f:value;
            reflectionLease020=true;reflectionApplied020=false;
        }
        void LoadReflectionPreference020(){ReflectionsEnabled020=PlayerPrefs.GetInt(ReflectionKey020,1)!=0;}
        void SaveReflectionPreference020(){PlayerPrefs.SetInt(ReflectionKey020,ReflectionsEnabled020?1:0);}
        public void SetReflectionsEnabled020(bool value)
        {
            if(ReflectionsEnabled020!=value){ReflectionsEnabled020=value;Save();}
            ApplyReflectionPolicy020();
        }
        void ApplyReflectionPolicy020()
        {
            if(!initialized||!isActiveAndEnabled)return;
            CaptureReflectionPolicy020();
            lastReflectionStrength020=ReflectionsEnabled020?1f:0f;
            lastReflectionIntensity020=ReflectionsEnabled020?originalReflectionIntensity020:0f;
            Shader.SetGlobalFloat(ReflectionStrengthId020,lastReflectionStrength020);
            RenderSettings.reflectionIntensity=lastReflectionIntensity020;reflectionApplied020=true;
        }
        void RestoreReflectionPolicy020()
        {
            if(!reflectionLease020)return;
            if(reflectionApplied020)
            {
                if(Shader.GetGlobalFloat(ReflectionStrengthId020)==lastReflectionStrength020)Shader.SetGlobalFloat(ReflectionStrengthId020,originalReflectionStrength020);
                if(RenderSettings.reflectionIntensity==lastReflectionIntensity020)RenderSettings.reflectionIntensity=originalReflectionIntensity020;
            }
            reflectionLease020=reflectionApplied020=false;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing game-wide shader global and RenderSettings scalar state; no camera/world/probe resources.
// Array Formats: One saved Boolean, one cached property ID, four scalar channel values and two lease flags; no new Update or allocation loop.
// Dependencies/Pointers: Existing GraphicsPreferences Save owns persistence. Off gates global and existing captured reflection intensity; On preserves the captured policy. Reacquire is cold and teardown restores only channels still held. Renderer must consume the global without a material property shadow.
