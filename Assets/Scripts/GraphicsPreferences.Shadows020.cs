// PSEUDOCODE: load or migrate user settings once; persist explicit values on a user command;
// let the existing graphics owner apply shadow tiers and let the atmosphere consume weather flags without owning another clock.
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class GraphicsPreferences
    {
        const string ShadowQualityKey020="Oracle.Graphics.ShadowQuality020";
        const string CloudsKey020="Oracle.Graphics.Clouds020";
        const string WeatherEffectsKey020="Oracle.Graphics.WeatherEffects020";
        public int ShadowQuality020{get;private set;}=3;
        public bool CloudsEnabled020{get;private set;}=true;
        public bool WeatherEffectsEnabled020{get;private set;}=true;
        public bool CloudShadowsEnabled020=>GraphicsShadowQuality020.AllowsCloudShadows(CloudsEnabled020,WeatherEffectsEnabled020,ShadowQuality020);
        public int ShadowCascadeCount020=>OwnsCurrentQualityPipeline020()&&runtimePipeline!=null?runtimePipeline.shadowCascadeCount:0;
        public bool ShadowSoftFiltering020=>OwnsCurrentQualityPipeline020()&&ActiveShadowLights020>0&&GraphicsShadowQuality020.Get(ShadowQuality020).Soft&&runtimePipeline!=null&&runtimePipeline.supportsSoftShadows;

        bool LoadShadowWeatherPreferences020()
        {
            bool hasTier=PlayerPrefs.HasKey(ShadowQualityKey020),hasLegacy=PlayerPrefs.HasKey(ShadowKey020);
            int stored=PlayerPrefs.GetInt(ShadowQualityKey020,-1);
            ShadowQuality020=GraphicsShadowQuality020.ResolveStored(stored,hasTier,Preset,hasLegacy,PlayerPrefs.GetInt(ShadowKey020,1)!=0);
            CloudsEnabled020=PlayerPrefs.GetInt(CloudsKey020,1)!=0;
            WeatherEffectsEnabled020=PlayerPrefs.GetInt(WeatherEffectsKey020,1)!=0;
            return !hasTier||stored<0||stored>=GraphicsShadowQuality020.Count||!PlayerPrefs.HasKey(CloudsKey020)||!PlayerPrefs.HasKey(WeatherEffectsKey020);
        }
        void SaveShadowWeatherPreferences020()
        {
            PlayerPrefs.SetInt(ShadowQualityKey020,ShadowQuality020);
            PlayerPrefs.SetInt(CloudsKey020,CloudsEnabled020?1:0);
            PlayerPrefs.SetInt(WeatherEffectsKey020,WeatherEffectsEnabled020?1:0);
        }
        public void SetShadowQuality020(int value)
        {
            int next=GraphicsShadowQuality020.Clamp(value);if(ShadowQuality020==next)return;
            ShadowQuality020=next;Save();RefreshWorldSettings(false);
        }
        public void SetCloudsEnabled020(bool value)
        {if(CloudsEnabled020==value)return;CloudsEnabled020=value;Save();}
        public void SetWeatherEffectsEnabled020(bool value)
        {if(WeatherEffectsEnabled020==value)return;WeatherEffectsEnabled020=value;Save();}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: User graphics settings and existing world-metre shadow budgets; no world/clock replacement.
// Array Formats: Three persisted scalar choices; computed cloud-shadow gate; no per-frame allocation or renderer buffers.
// Dependencies/Pointers: Existing GraphicsPreferences owns private pipeline/light restoration and Save; atmosphere consumes instance flags, captures never write them.
