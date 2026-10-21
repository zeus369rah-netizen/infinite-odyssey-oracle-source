// PSEUDOCODE: reuse Sound menu button and slider; persist only user changes through audio owner; refresh labels only when their displayed value changes.
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        UnityEngine.UI.Text weatherAudioToggle020,weatherAudioVolumeText020,weatherAudioStatus020;
        UnityEngine.UI.Slider weatherAudioSlider020;
        int weatherAudioShownVolume020=-1,weatherAudioShownEnabled020=-1;
        WeatherAmbienceState020 weatherAudioShownState020=(WeatherAmbienceState020)(-1);
        void BuildWeatherAmbienceControls020(RectTransform content)
        {
            MenuLabel(content,"WeatherAmbienceHeading020","WEATHER AMBIENCE",15,Gold,28);
            var toggle=MenuButton(content,"WeatherAmbienceToggle020","",()=>
            {if(session.Audio!=null)session.Audio.WeatherAmbienceEnabled020=!session.Audio.WeatherAmbienceEnabled020;RefreshWeatherAmbienceControls020();},38);
            weatherAudioToggle020=toggle.GetComponentInChildren<UnityEngine.UI.Text>();
            weatherAudioSlider020=VolumeRow(content,"WeatherAmbienceVolume020","RAIN VOLUME",value=>
            {if(session.Audio!=null)session.Audio.WeatherAmbienceVolume020=value;},out weatherAudioVolumeText020);
            MenuLabel(content,"WeatherAmbienceMix020","Night storms only. Uses Master and Sound Effects. Advanced Graphics > Weather Effects also controls this sound.",13,Muted,52);
            weatherAudioStatus020=MenuLabel(content,"WeatherAmbienceStatus020","",13,Muted,38);
            RefreshWeatherAmbienceControls020();
        }
        void RefreshWeatherAmbienceControls020()
        {
            if(weatherAudioToggle020==null||session.Audio==null)return;
            var audio=session.Audio;int enabled=audio.WeatherAmbienceEnabled020?1:0;
            if(enabled!=weatherAudioShownEnabled020){weatherAudioShownEnabled020=enabled;weatherAudioToggle020.text=enabled==1?"WEATHER AMBIENCE | ON":"WEATHER AMBIENCE | OFF";}
            int percent=Mathf.RoundToInt(audio.WeatherAmbienceVolume020*100);
            if(percent!=weatherAudioShownVolume020){weatherAudioShownVolume020=percent;weatherAudioSlider020.SetValueWithoutNotify(audio.WeatherAmbienceVolume020);weatherAudioVolumeText020.text=percent+"%";}
            var state=audio.WeatherAmbienceReadout020.State;
            if(state==weatherAudioShownState020)return;weatherAudioShownState020=state;
            switch(state)
            {
                case WeatherAmbienceState020.MissingClip:weatherAudioStatus020.text="Rain recording unavailable. Weather ambience stays silent.";break;
                case WeatherAmbienceState020.LoadingClip:weatherAudioStatus020.text="Rain recordings are loading.";break;
                case WeatherAmbienceState020.FailedClip:weatherAudioStatus020.text="Rain recording could not load. Weather ambience stays silent.";break;
                case WeatherAmbienceState020.GraphicsDisabled:weatherAudioStatus020.text="Weather Effects is off in Advanced Graphics.";break;
                case WeatherAmbienceState020.Disabled:weatherAudioStatus020.text="Weather ambience is off.";break;
                case WeatherAmbienceState020.Held:weatherAudioStatus020.text="Weather ambience is held while gameplay is paused.";break;
                case WeatherAmbienceState020.Muted:weatherAudioStatus020.text="Weather ambience is muted or waiting for gameplay audio.";break;
                case WeatherAmbienceState020.NoAmbientCapacity:case WeatherAmbienceState020.PoolWithheld:weatherAudioStatus020.text="Weather ambience is waiting for an available ambience voice.";break;
                case WeatherAmbienceState020.Playing:weatherAudioStatus020.text="Night-storm rain. Shelter softens the sound.";break;
                case WeatherAmbienceState020.FadingOut:weatherAudioStatus020.text="Rain is fading out.";break;
                case WeatherAmbienceState020.ClearWeather:weatherAudioStatus020.text="Rain is quiet until a night storm.";break;
                default:weatherAudioStatus020.text="Weather ambience becomes available in the campaign hub.";break;
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing Sound settings uGUI scroll panel and unchanged game palette.
// Array Formats: One cold button, slider and cached scalar label values; standard navigation/input remains owned by existing UI.
// Dependencies/Pointers: Audio owner alone persists explicit user choices. UI readout does not fabricate clip/native playback acceptance or mutate graphics/time/capture state.
